using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public sealed record ServiceAssignment(int BandId, int Until, ServiceOrder Order);
public sealed class CivicState
{
    public string Institution = "", Doctrine = "";
    public float Preparedness, Trust, Learning, Treatment;
    public int Patients, Lessons, Ceremonies, Sessions;
}

public sealed partial class Simulation
{
    [SaveOptional] public readonly Dictionary<int, ServiceAssignment> Services = new();
    [SaveOptional] public readonly Dictionary<int, CivicState> Civics = new();
    [SaveOptional] public readonly Dictionary<(int person, TechId tech), float> Learning = new();
    public static readonly string[] ServiceKinds = { "care", "teach", "research", "worship", "govern", "guard" };

    public static bool CanServe(Structure building, string service)
    {
        string id = building.Kind.Id;
        bool Fixture(params string[] kinds) => building.Layout?.Objects.Any(o => kinds.Contains(o.Kind)) == true;
        return service switch
        {
            "care" => id is "infirmary" or "medical_clinic" or "medical_table" || Fixture("medical_table"),
            "teach" => id is "library" or "lectern" or "bookcase" || Fixture("lectern", "bookcase"),
            "research" => id is "research_lab" or "alchemy_table" or "telescope" || Fixture("alchemy_table", "telescope"),
            "worship" => building.Kind.Role == StructureRole.Shrine || id is "ritual_hall" or "ceremonial_hall" || Fixture("altar", "ceremonial_screen"),
            "govern" => id is "council_hall" or "royal_hall" or "throne" || Fixture("throne"),
            "guard" => id is "barracks" or "fort_gatehouse" or "watch_post" || building.Kind.Role == StructureRole.Defence,
            _ => false,
        };
    }

    public ServiceAssignment? ServiceFor(int worker) => Services.TryGetValue(worker, out var s) && s.Until > Day ? s : null;
    static Activity ServiceActivity(string service) => service switch { "care" => Activity.Heal, "teach" => Activity.Teach, "research" => Activity.Experiment,
        "worship" => Activity.Ritual, "govern" => Activity.Govern, _ => Activity.Guard };

    bool ValidateServices(Band band, ServiceOrder[] orders, out string error)
    {
        error = "Services need distinct adult workers, owned suitable buildings and up to six distinct living recipients. Teaching/research subjects must be valid known-technique IDs.";
        if (orders.Length > 6 || orders.Any(o => o == null) || orders.Select(o => o.WorkerId).Distinct().Count() != orders.Length) return false;
        var occupied = new HashSet<int>();
        foreach (var order in orders)
        {
            if (order.WorkerId == ControlledAgentId || !Living.TryGetValue(order.WorkerId, out var worker) || worker.BandId != band.Id || !worker.IsAdult(Day) || HaulFor(worker.Id) != null || ProductionFor(worker.Id) != null || RoadFor(worker.Id) != null ||
                !Structures.TryGetValue(order.BuildingId, out var site) || !site.Standing || site.BandId != band.Id || !CanServe(site, order.Service) || !occupied.Add(site.Id) ||
                order.Recipients == null || order.Recipients.Length > 6 || order.Recipients.Distinct().Count() != order.Recipients.Length ||
                order.Recipients.Any(id => !Living.TryGetValue(id, out var recipient) || recipient.BandId != band.Id) || !ValidText(order.Subject, 80)) return false;
            if (order.Service == "care" && !worker.Knows.Has(TechId.Herbalism)) return false;
            if (order.Service == "teach" && order.Subject.Length > 0 && (!TryTech(order.Subject, out var tech) || !worker.Knows.Has(tech))) return false;
            if (order.Service == "research" && order.Subject.Length > 0) return false;   // research serves the band's experiments, not a named technique
        }
        error = ""; return true;
    }

    public static bool TryTech(string text, out TechId tech)
    {
        foreach (var t in Techs.All) if (Ids.Snake(t.Id) == text) { tech = t.Id; return true; }
        tech = default; return false;
    }

    bool ValidateWorkOrders(Band band, MindPlan plan, out string error)
    {
        if (!ValidateProduction(band, plan.Production ?? Array.Empty<ProductionOrder>(), out error) || !ValidateServices(band, plan.Services ?? Array.Empty<ServiceOrder>(), out error) || !ValidateRoads(band, plan.Roads ?? Array.Empty<RoadOrder>(), out error)) return false;
        var workers = (plan.Cargo ?? Array.Empty<ShipmentOrder>()).Select(o => o.WorkerId).Concat((plan.Production ?? Array.Empty<ProductionOrder>()).Select(o => o.WorkerId))
            .Concat((plan.Services ?? Array.Empty<ServiceOrder>()).Select(o => o.WorkerId)).Concat((plan.Roads ?? Array.Empty<RoadOrder>()).Select(o => o.WorkerId)).ToArray();
        var duplicates = workers.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        error = "Workers " + string.Join(", ", duplicates) + " have multiple assignments. Keep each worker in only one of cargo, production, services or roads; remove their other orders.";
        if (duplicates.Length > 0) return false;
        error = "The player controls person " + ControlledAgentId + "; remove that person's work orders.";
        if (workers.Contains(ControlledAgentId)) return false;
        error = "Utilities need a finite battery reserve between zero and one.";
        return plan.Utilities == null || float.IsFinite(plan.Utilities.BatteryReserve) && plan.Utilities.BatteryReserve is >= 0f and <= 1f;
    }

    void AssignWork(Band band, AcceptedPlan accepted)
    {
        foreach (int worker in Services.Where(s => s.Value.BandId == band.Id).Select(s => s.Key).ToArray()) Services.Remove(worker);
        foreach (var order in accepted.Plan.Production ?? Array.Empty<ProductionOrder>()) AddProduction(band, order);
        foreach (var order in accepted.Plan.Services ?? Array.Empty<ServiceOrder>()) Services[order.WorkerId] = new ServiceAssignment(band.Id, accepted.Until, order);
        foreach (var order in accepted.Plan.Roads ?? Array.Empty<RoadOrder>()) AddRoad(band, order);
        if (!Civics.TryGetValue(band.Id, out var civic)) Civics[band.Id] = civic = new CivicState();
        if (accepted.Plan.Institution.Length > 0) { civic.Institution = accepted.Plan.Institution; civic.Doctrine = accepted.Plan.Doctrine; }
    }

    Activity? OrderedActivity(Agent person)
    {
        if (!person.IsAdult(Day) || person.Sick || person.Health < 0.2f || person.NeedOf(Need.Energy) > 0.9f) return null;
        if (HaulFor(person.Id) != null) return Activity.Haul;
        if (ProductionFor(person.Id) != null) return Activity.Craft;
        if (RoadFor(person.Id) != null) return Activity.Build;
        return ServiceFor(person.Id) is { } service ? ServiceActivity(service.Order.Service) : null;
    }

    void PerformServices()
    {
        foreach (var civic in Civics.Values) { civic.Preparedness *= 0.99f; civic.Trust *= 0.995f; civic.Learning *= 0.99f; civic.Treatment *= 0.99f; }
        foreach (var (workerId, assignment) in Services.OrderBy(s => s.Key).ToArray())
        {
            var order = assignment.Order;
            if (assignment.Until <= Day || !Living.TryGetValue(workerId, out var worker) || worker.BandId != assignment.BandId ||
                !Bands.TryGetValue(assignment.BandId, out var band) || !Structures.TryGetValue(order.BuildingId, out var building) || !building.Standing || building.BandId != band.Id)
            { Services.Remove(workerId); continue; }
            if (worker.Today != ServiceActivity(order.Service)) continue;
            if (!Civics.TryGetValue(band.Id, out var civic)) Civics[band.Id] = civic = new CivicState();
            worker.Site = building.Cell;
            var recipients = (order.Recipients.Length == 0 ? band.Members : order.Recipients.AsEnumerable()).Where(id => Living.TryGetValue(id, out var a) && a.BandId == band.Id && AtCamp(a, band)).Select(id => Living[id]);
            switch (order.Service)
            {
                case "care":
                    foreach (var patient in recipients.Where(a => a.Sick || a.Health < 0.85f).OrderBy(a => a.Health).Take(3))
                    {
                        float strength;
                        if (band.Stores.Has(Goods.Id("medicine"), 0.2f)) { band.Stores.Take("medicine", 0.2f); strength = 0.04f; }
                        else if (band.Stores.Has(Goods.Id("herbs"), 0.5f)) { band.Stores.Take("herbs", 0.5f); strength = 0.015f; }
                        else break;
                        patient.Health = MathF.Min(1f, patient.Health + strength * (0.6f + worker.Efficacy[(int)Domain.Healing]));
                        if (patient.Sick) { patient.SickUntil = Math.Max(Day, patient.SickUntil - 2); if (patient.SickUntil <= Day) patient.Sick = false; }
                        civic.Patients++; civic.Treatment = MathF.Min(1f, civic.Treatment + 0.03f); Psychology.Practice(worker, Domain.Healing, true, 0.4f);
                    }
                    break;
                case "teach":
                    foreach (var pupil in recipients.Where(a => a.Id != worker.Id && a.Age(Day) >= 4f).Take(6))
                    {
                        var choices = (worker.Knows & ~pupil.Knows).Ids().Where(t => Techs.CanLearn(pupil.Knows, t));
                        if (order.Subject.Length > 0) choices = choices.Where(t => Ids.Snake(t) == order.Subject);
                        if (!choices.Any()) continue;
                        var tech = choices.First(); var key = (pupil.Id, tech);
                        Learning[key] = Learning.GetValueOrDefault(key) + 0.12f * (0.5f + pupil.Gene(Gene.Intellect)) * (0.7f + worker.Efficacy[(int)Domain.Social]);
                        if (Learning[key] >= 1f) { pupil.Knows.Add(tech); Learning.Remove(key); civic.Lessons++; }
                        pupil.Needs[(int)Need.Social] = MathF.Max(0f, pupil.Needs[(int)Need.Social] - 0.05f); civic.Learning = MathF.Min(1f, civic.Learning + 0.005f);
                    }
                    break;
                case "research":
                    // A place for careful work speeds the band's experiments (what they find is up to the experiments).
                    WorkExperiment(worker, band, 2f);
                    civic.Learning = MathF.Min(1f, civic.Learning + 0.01f);
                    break;
                case "worship":
                    if (civic.Institution.Length > 0 && civic.Doctrine.Length > 0 && !worker.Memes.Any(m => Memes.TryGetValue(m.Key, out var belief) && belief.OriginBandId == band.Id && belief.Name == civic.Institution))
                    {
                        var doctrine = Memes.Values.FirstOrDefault(m => m.OriginBandId == band.Id && m.Name == civic.Institution);
                        if (doctrine == null) { doctrine = NewMeme(MemeKind.SpiritBelief, StimulusCategory.Sky, civic.Institution, worker, band); doctrine.Doctrine = civic.Doctrine; doctrine.CultureId = band.CultureId; }
                        worker.Memes[doctrine.Id] = 0.7f;
                    }
                    foreach (var visitor in recipients.Take(6))
                    {
                        visitor.Emotions[(int)Emotion.Fear] *= 0.97f; visitor.Belonging = MathF.Min(1f, visitor.Belonging + 0.005f);
                        foreach (var belief in worker.Memes.Where(m => m.Value > 0.4f && Memes.ContainsKey(m.Key)).Take(2).ToArray()) visitor.Memes[belief.Key] = MathF.Min(1f, visitor.Memes.GetValueOrDefault(belief.Key) + 0.01f);
                    }
                    civic.Ceremonies++;
                    break;
                case "govern":
                    foreach (var member in recipients.Take(6)) if (member.Id != worker.Id)
                    { member.Rel(worker.Id).Trust = MathF.Min(1f, member.Rel(worker.Id).Trust + 0.003f); member.Belonging = MathF.Min(1f, member.Belonging + 0.002f); }
                    civic.Trust = MathF.Min(1f, civic.Trust + 0.015f); civic.Sessions++;
                    break;
                case "guard":
                    civic.Preparedness = MathF.Min(1f, civic.Preparedness + 0.015f + WeaponPower(worker) * 0.02f + Protection(worker) * 0.01f);
                    worker.Efficacy[(int)Domain.Hunting] = MathF.Min(1f, worker.Efficacy[(int)Domain.Hunting] + 0.001f);
                    break;
            }
        }
        foreach (var key in Learning.Keys.Where(k => !Living.ContainsKey(k.person)).ToArray()) Learning.Remove(key);
    }
}
