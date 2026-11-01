using Evolysia.Sim.Core;

namespace Evolysia.Sim.Agents;

/// <summary>What people can do to things when they experiment.</summary>
public enum Method : byte
{
    Strike, Shape, Heat, Burn, Soak, Dry, Mix, Grind, Tie, Weave, Plant, Tame, Dig, Stack, Float, Carry,
    Watch, Mark, Sing, Trade, Council, Bury, Combine,
}

/// <summary>
/// How a technique is found: doing the right thing (<see cref="Method"/>) to the right things
/// (<see cref="Subjects"/>). Hidden from the minds: they only learn what their experiments show.
/// <see cref="Sign"/> is what the experimenters see when it works; <see cref="Use"/> is how the
/// people would put their new knowledge in words (never its mechanics).
/// </summary>
public sealed record DiscoveryKey(TechId Tech, Method Method, string[] Subjects, string Sign, string Use);

/// <summary>
/// The hidden ways into every technique, and the things people can work with. Minds propose an
/// action and the things it acts on; the simulation alone judges what (if anything) comes of it.
/// </summary>
public static class Discovery
{
    /// <summary>Things that can be named in an experiment (what each means is in <see cref="SubjectNote"/>).</summary>
    public static readonly string[] Subjects =
    {
        "wood", "stone", "flint", "clay", "sand", "reeds", "fiber", "hide", "bone", "meat", "fish", "plants", "seeds",
        "animals", "herd", "water", "fire", "earth", "ore", "metal", "charcoal", "glass", "the_dead", "sky", "people",
        "tools", "machines", "power",
    };

    static readonly Dictionary<TechId, DiscoveryKey> Keys = new();

    static void K(TechId t, Method m, string subjects, string sign, string use) =>
        Keys[t] = new DiscoveryKey(t, m, subjects.Split(','), sign, use);

    static Discovery()
    {
        K(TechId.StoneTools, Method.Strike, "stone,flint", "A struck stone split into a sharp edge.", "knap sharp edges from stone");
        K(TechId.ControlledFire, Method.Strike, "flint,plants", "Sparks from struck flint caught in dry grass and became flame.", "make fire and keep it alive");
        K(TechId.Cooking, Method.Heat, "meat,fire", "Meat held over the fire grew soft, and those who ate it stayed well.", "cook food over fire");
        K(TechId.Spear, Method.Tie, "wood,stone", "A sharp stone bound to a long stick struck deep.", "make spears for the hunt");
        K(TechId.HideClothing, Method.Shape, "hide,bone", "Hides cut and pierced with bone could be worn against the cold.", "make clothes from hides");
        K(TechId.Shelter, Method.Stack, "wood,plants", "Branches leaned together and covered kept off wind and rain.", "build shelters");
        K(TechId.Baskets, Method.Weave, "fiber,plants", "Grass stems woven together held berries without spilling.", "weave baskets to carry and keep things");
        K(TechId.Fishing, Method.Watch, "fish,water", "Watching the shallows, we learned where fish rest and how to take them.", "catch fish");
        K(TechId.Herbalism, Method.Soak, "plants,water", "Certain leaves steeped and given to the sick eased their fever.", "heal with plants");
        K(TechId.FoodStorage, Method.Bury, "plants,earth", "Food buried in a dry pit was still good when the cold came.", "keep food for lean times");
        K(TechId.Bow, Method.Tie, "wood,fiber", "A bent branch strung with cord flung a small spear far and true.", "make bows");
        K(TechId.Pottery, Method.Heat, "clay,fire", "Clay left in the fire came out hard and held water.", "shape clay and harden it in fire");
        K(TechId.Weaving, Method.Weave, "fiber,wood", "Threads stretched across a wooden frame became cloth.", "weave cloth");
        K(TechId.Boats, Method.Float, "wood,water", "A hollowed log carried us across the water.", "make boats");
        K(TechId.Domestication, Method.Tame, "animals", "Young animals fed by hand followed us and stayed.", "keep tame animals");
        K(TechId.Agriculture, Method.Plant, "seeds,earth", "Seeds pressed into soft earth came up where we put them.", "sow and harvest crops");
        K(TechId.Copper, Method.Heat, "ore,fire", "Green stone in a hot fire wept bright metal.", "draw copper from stone");
        K(TechId.Bronze, Method.Mix, "metal,ore", "Two molten metals together became harder than either.", "make bronze");
        K(TechId.Woodcutting, Method.Strike, "wood,stone", "Hard blows with a stone edge brought a tree down.", "fell trees for timber");
        K(TechId.Butchery, Method.Shape, "meat,stone", "Cutting a carcass along its joints gave clean meat, hide and bone.", "butcher animals");
        K(TechId.BoneTools, Method.Shape, "bone,stone", "Ground bone took a point finer than stone.", "make tools of bone");
        K(TechId.Cordage, Method.Tie, "fiber", "Twisted plant fibres made a cord that would not break.", "twist cord and rope");
        K(TechId.Quarrying, Method.Dig, "stone,earth", "Prying at a rock face freed great blocks.", "quarry stone");
        K(TechId.Pigments, Method.Grind, "earth,stone", "Coloured earth ground fine stained skin and stone.", "make colours");
        K(TechId.Music, Method.Sing, "wood", "Hollow wood struck in rhythm made the people move together.", "make music");
        K(TechId.Burial, Method.Bury, "the_dead,earth", "Laying our dead in the earth eased the grief of the living.", "bury the dead with honour");
        K(TechId.Carving, Method.Shape, "wood,stone", "Cutting wood with stone made figures and handles.", "carve wood");
        K(TechId.Drying, Method.Dry, "meat,fiber", "Strips of meat hung on cords in the smoke kept for months.", "dry and smoke food");
        K(TechId.Tanning, Method.Soak, "hide,plants", "Hides soaked with bark became soft and did not rot.", "tan leather");
        K(TechId.Netting, Method.Weave, "fiber,fish", "A web of cords caught many fish at once.", "fish with nets");
        K(TechId.Rafts, Method.Tie, "wood,water", "Logs lashed together floated with a load.", "build rafts");
        K(TechId.Sledges, Method.Carry, "wood", "A frame of poles dragged loads easily over snow.", "haul loads on sledges");
        K(TechId.Grinding, Method.Grind, "seeds,stone", "Seeds crushed between stones became flour.", "grind grain");
        K(TechId.Dairy, Method.Tame, "herd", "The tame mothers let us draw their milk.", "milk our animals");
        K(TechId.Beekeeping, Method.Burn, "wood,animals", "Smoke quieted the bees so we could take their honey.", "keep bees");
        K(TechId.Milling, Method.Grind, "seeds,water", "A stone turned by running water ground grain all day.", "mill grain");
        K(TechId.Baking, Method.Heat, "seeds,clay", "Dough baked in a clay oven rose into bread.", "bake bread");
        K(TechId.Brewing, Method.Soak, "seeds,water", "Soaked grain left in a pot bubbled and grew strong.", "brew");
        K(TechId.Irrigation, Method.Dig, "water,seeds", "Channels dug from the stream greened the dry fields.", "water fields through channels");
        K(TechId.Wells, Method.Dig, "water,stone", "Digging deep and lining the hole with stone found water that did not fail.", "dig wells");
        K(TechId.Orchards, Method.Plant, "plants,wood", "Shoots cut from fruit trees grew where we set them.", "plant orchards");
        K(TechId.Plough, Method.Tie, "herd,wood", "A beast harnessed to a pointed beam broke the soil faster than ten hands.", "plough with animals");
        K(TechId.Thatching, Method.Stack, "reeds,wood", "Reeds layered thick on a frame shed every drop.", "thatch roofs");
        K(TechId.Mudbrick, Method.Dry, "clay,sky", "Mud shaped into blocks and dried in the sun stacked into walls.", "build with mud bricks");
        K(TechId.Masonry, Method.Stack, "stone", "Fitted stones stacked high without falling.", "build in stone");
        K(TechId.Carpentry, Method.Combine, "wood,tools", "Pieces of wood cut to fit one another held fast without cord.", "join timber");
        K(TechId.Kilns, Method.Stack, "clay,fire", "Fire walled in clay grew hotter than any open hearth.", "build kilns");
        K(TechId.FiredBrick, Method.Heat, "clay,sand", "Mud blocks fired in the kiln rang like stone.", "fire bricks");
        K(TechId.Wheel, Method.Carry, "wood,stone", "A round slice of trunk rolled loads we could not lift.", "make wheels");
        K(TechId.Sailing, Method.Float, "fiber,sky", "Cloth raised on a pole caught the wind and drove the boat.", "sail");
        K(TechId.Trade, Method.Trade, "people", "Giving our goods to strangers brought back things we could not make.", "trade with others");
        K(TechId.Writing, Method.Mark, "clay,people", "Marks pressed in clay kept what was said after the speaker left.", "write");
        K(TechId.Counting, Method.Mark, "bone,animals", "A notch for every beast told us when one was missing.", "count and keep tallies");
        K(TechId.Governance, Method.Council, "people", "When elders met and agreed, everyone followed the same course.", "govern by council");
        K(TechId.Monarchy, Method.Council, "people,sky", "One voice, followed by all, carried the will of the heavens.", "rule through a single ruler");
        K(TechId.Priesthood, Method.Sing, "the_dead,sky", "Those who sang to the sky and the dead were listened to by all.", "keep priests");
        K(TechId.Medicine, Method.Mark, "plants,people", "Writing down which leaves cured which sickness healed many more.", "practise medicine");
        K(TechId.Fortification, Method.Stack, "wood,earth", "A ring of sharpened stakes and earth turned raiders away.", "fortify");
        K(TechId.Armour, Method.Weave, "hide", "Layers of hardened hide stitched together turned blows.", "make armour");
        K(TechId.Alchemy, Method.Mix, "plants,ore", "Strange mixtures changed colour and smell; some cured, some killed.", "practise alchemy");
        K(TechId.Mysticism, Method.Watch, "sky,the_dead", "Watching the stars, we read the will of hidden powers.", "read the hidden powers");
        K(TechId.Charcoal, Method.Burn, "wood,earth", "Wood smothered under earth while burning turned black and burned hotter.", "make charcoal");
        K(TechId.Iron, Method.Heat, "ore,charcoal", "Red earth in a charcoal fire left a dark, hard metal.", "smelt iron");
        K(TechId.Steel, Method.Heat, "metal,charcoal", "Iron folded in a charcoal fire became springy and keen.", "make steel");
        K(TechId.Glass, Method.Heat, "sand,charcoal", "Sand in the hottest fire melted into something clear.", "make glass");
        K(TechId.Mining, Method.Dig, "ore,wood", "Tunnels propped with timber followed the ore underground.", "mine");
        K(TechId.Swords, Method.Shape, "metal,fire", "Hot metal hammered long made a blade.", "forge swords");
        K(TechId.Crossbow, Method.Tie, "wood,metal", "A bow set in a stock with a metal catch shot further than any arm.", "make crossbows");
        K(TechId.Roads, Method.Stack, "stone,sand", "Packed stone laid in a line made a way carts could follow in all weather.", "build roads");
        K(TechId.Riding, Method.Tame, "herd,hide", "Sitting on a tame beast's back carried us further in a day than walking in three.", "ride");
        K(TechId.Shipbuilding, Method.Shape, "wood,water", "Planks bent around ribs made ships that could cross the sea.", "build ships");
        K(TechId.Schools, Method.Council, "people,clay", "Children taught marks together learned faster than one by one.", "keep schools");
        K(TechId.Sanitation, Method.Dig, "water,people", "Carrying filth away in channels ended the sickness of crowded places.", "keep places clean");
        K(TechId.Machinery, Method.Combine, "metal,wood", "Toothed wheels turning one another multiplied our strength.", "build machines");
        K(TechId.Printing, Method.Mark, "metal,fiber", "Raised metal letters inked and pressed copied a page a hundred times.", "print");
        K(TechId.SteamPower, Method.Heat, "water,metal", "Water boiled in a closed metal vessel pushed with terrible force.", "harness steam");
        K(TechId.Railways, Method.Carry, "metal,power", "Engines rolling on metal rails pulled a whole village's goods.", "run railways");
        K(TechId.PowerLooms, Method.Weave, "fiber,power", "The engine wove cloth faster than a hundred weavers.", "weave by engine");
        K(TechId.Chemistry, Method.Mix, "ore,glass", "Measured mixtures in glass vessels behaved the same way every time.", "practise chemistry");
        K(TechId.Firearms, Method.Burn, "metal,charcoal", "A pinch of black powder in a metal tube threw a stone with a thunderclap.", "make firearms");
        K(TechId.Concrete, Method.Mix, "stone,sand", "Burned lime, sand and stone mixed with water set as hard as rock.", "pour concrete");
        K(TechId.Hospitals, Method.Council, "people,plants", "Gathering the sick where clean hands tended them saved most of them.", "keep hospitals");
        K(TechId.Electricity, Method.Combine, "metal,power", "Metal spun near lodestone made a spark that crawled along wire.", "make electric power");
        K(TechId.Refrigeration, Method.Combine, "power,water", "Machines driven by power made ice in summer.", "keep things cold");
        K(TechId.Telecommunication, Method.Mark, "power,metal", "Pulses sent down a wire spoke words far away.", "send words along wires");
        K(TechId.Motors, Method.Combine, "power,machines", "Power fed into a coil turned a shaft by itself.", "build motors");
        K(TechId.Aviation, Method.Float, "machines,sky", "A light frame with a spinning blade rose into the sky.", "fly");
        K(TechId.SolarPower, Method.Watch, "sky,glass", "Certain glass under the sun gave power without fire.", "draw power from the sun");
        K(TechId.Batteries, Method.Soak, "metal,power", "Metal plates in sour liquid stored power for later.", "store power");
        K(TechId.Electronics, Method.Combine, "glass,power", "Tiny gates of glass and wire steered power precisely.", "build electronics");
        K(TechId.Computing, Method.Mark, "machines", "Machines that marked and counted thought faster than any scribe.", "compute");
        K(TechId.Robotics, Method.Tame, "machines", "Machines that sensed and moved on their own did our work.", "build robots");
        K(TechId.Recycling, Method.Grind, "machines,metal", "Broken things ground down became the stuff of new things.", "recycle");
        K(TechId.Networks, Method.Council, "machines,people", "Thinking machines joined together let everyone speak to everyone.", "join minds by network");
        K(TechId.Biotech, Method.Watch, "people,machines", "Reading the patterns inside living things let us reshape them.", "reshape living things");
        K(TechId.Hydroponics, Method.Plant, "seeds,water", "Seeds rooted in water under light grew without soil.", "grow crops without soil");
        K(TechId.Fabrication, Method.Shape, "machines,metal", "Machines that shaped any material made anything we could describe.", "fabricate");
        K(TechId.AdvancedMaterials, Method.Mix, "machines,glass", "Materials built grain by grain were lighter and stronger than any found.", "make new materials");
        K(TechId.FusionPower, Method.Heat, "water,machines", "A tiny sun held in a machine gave power without end.", "make fusion power");
        K(TechId.Hover, Method.Float, "power,machines", "Machines rose and hung over the ground on unseen force.", "hover");
        K(TechId.EnergyTools, Method.Shape, "power,metal", "Tools of focused energy cut and joined anything.", "wield energy tools");
        K(TechId.Rocketry, Method.Burn, "machines,sky", "A machine riding its own fire climbed beyond the sky.", "build rockets");
        K(TechId.LifeSupport, Method.Plant, "people,machines", "Sealed gardens kept people alive where nothing else could.", "keep life where none can live");
        K(TechId.SpaceHabitats, Method.Stack, "machines,sky", "Sealed halls lifted beyond the sky held air and warmth for whole families.", "live beyond the sky");
        K(TechId.OrbitalIndustry, Method.Shape, "machines,sky", "Machines working beyond the sky shaped what could never be made below.", "make things beyond the sky");
        K(TechId.Interplanetary, Method.Float, "sky,power", "A ship driven by tamed fire crossed to another world.", "travel between worlds");
    }

    public static DiscoveryKey Key(TechId t) => Keys.TryGetValue(t, out var k) ? k
        : new DiscoveryKey(t, Method.Combine, new[] { "tools" }, "Something new took shape.", Techs.Name(t).Replace('_', ' '));

    public static bool HasKey(TechId t) => Keys.ContainsKey(t);

    /// <summary>The verb as people say it ("heat", "heating").</summary>
    public static string Verb(Method m) => Ids.Snake(m);

    public static string Gerund(Method m) => m switch
    {
        Method.Shape => "shaping", Method.Mix => "mixing", Method.Stack => "stacking", Method.Trade => "trading", Method.Combine => "combining",
        Method.Tie => "tying", Method.Dig => "digging", Method.Plant => "planting", Method.Strike => "striking", Method.Tame => "taming",
        Method.Dry => "drying", Method.Carry => "carrying", Method.Bury => "burying",
        Method.Council => "holding council over", Method.Sing => "singing to", Method.Watch => "watching",
        _ => Ids.Snake(m) + "ing",
    };

    public static string SubjectNote(string subject) => subject.Replace('_', ' ');

    /// <summary>
    /// How well an experiment touches a technique's way in: half for the right action, half for the
    /// share of the right things, less a little for each thing that does not belong.
    /// </summary>
    public static float Match(DiscoveryKey key, Method method, IReadOnlyCollection<string> subjects)
    {
        int overlap = subjects.Count(s => key.Subjects.Contains(s));
        int stray = subjects.Count - overlap;
        return (method == key.Method ? 0.5f : 0f) + 0.5f * overlap / key.Subjects.Length - 0.08f * stray;
    }
}
