using Evolysia.Sim.Core;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim;

public sealed record MindRequest(int Id, int Day, int BandId, int[] Agents, string[] Buildings, string[] Research, int[] Neighbours, int[] Destinations,
    [field: SaveOptional] string[]? Subjects = null);
public sealed record WorkShare(string Activity, float Weight);
/// <summary>An experiment in the minds' own terms: an action on up to three things they can work with, and what they hope for.</summary>
public sealed record ExperimentIntent(string Action, string[] Subjects, string Idea);
public sealed record PersonalIntent(int AgentId, string Activity, string Goal, string Thought, string Speech, string Memory, int OtherId = -1, float Affinity = 0f);
public sealed record DiplomaticIntent(int BandId, string Stance);
public sealed record ShipmentOrder(int WorkerId, int SourceId, int TargetId, string Good, float Quantity, int VehicleId = -1);
public sealed record BuildingIdea(string Name, string Template, int Width, int Height, Economy.RoomObject[] Objects);
public sealed record CommunityIntent(string Action, int LeaderId = -1, int OtherBandId = -1, int[]? Followers = null);
public sealed record ProductionOrder(int WorkerId, string Product, int Count = 1, int RecipientId = -1, int OutputId = 0);
public sealed record ServiceOrder(int WorkerId, int BuildingId, string Service, int[] Recipients, string Subject = "");
public sealed record UtilitiesIntent(bool FuelPower = false, float BatteryReserve = 0.2f);
public sealed record StructureDesign(string Name, int BandId, Economy.StructureDef Definition, Economy.RoomLayout Layout);
public sealed record MindPlan(int RequestId, string Goal, WorkShare[] Labour, string[] Projects, string[] Research,
    PersonalIntent[] People, DiplomaticIntent[] Diplomacy, string Institution, string Doctrine, float Sharing = 0.5f, int Destination = -1, bool Settle = false, ShipmentOrder[]? Cargo = null, BuildingIdea[]? Designs = null, CommunityIntent? Community = null,
    [field: SaveOptional] ProductionOrder[]? Production = null, [field: SaveOptional] ServiceOrder[]? Services = null, [field: SaveOptional] UtilitiesIntent? Utilities = null,
    [field: SaveOptional] RoadOrder[]? Roads = null, [field: SaveOptional] ExperimentIntent[]? Experiments = null);
public sealed record MindMemory(int Day, string Goal, string Thought, string Speech, string Reflection);
public sealed record AcceptedPlan(int Day, int Until, int BandId, MindPlan Plan);

public sealed class PersonalMind
{
    public string Goal = "", Thought = "", Speech = "";
    public Activity Work;
    public int Until;
    public readonly List<MindMemory> Memories = new();
}
