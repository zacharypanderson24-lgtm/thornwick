// The in-game tutorial: a guided walk through the existing mechanics, one step at a time. Pure C# (no UnityEngine) so EditMode tests can
// drive it; the HUD shows it (Game/Hud.cs) and GameController feeds it the player's actions it cannot see in the world (camera, panels).
// A step is done when its check sees the player actually did the thing in the world (counted against a snapshot taken when the step
// began, so earlier towns don't count); steps without a check are explanations that wait for Next. Every step can be skipped.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public sealed class Tutorial
    {
        public sealed class Step
        {
            public string Id, Title, Body, Tool, Goal;   // Tool: toolbar button to light up (GameController tool id); Goal: what finishes the step
            public Func<Tutorial, World, bool> Done;     // a goal the world cannot count (camera moved, panel opened...)
            public Func<Snapshot, int> Count; public int By;   // a counted goal: done once Count has grown by By since the step began
            public bool Explanation => Done == null && Count == null;   // finished with Next
        }

        // what the world looked like when the current step began
        public struct Snapshot { public int Roads, ResByRoad, Houses, Farmland, Lumber, Lodges, Sawmills, Wells, Comm, Silos, Mining, Parcels, Hunters, Mines, Quarries; }

        // actions the world cannot show, reported by the game
        public bool Panned, Zoomed, Rotated, SpeedChanged, WorkersOpened, UndergroundViewed;

        public string BodyText => Current?.Body ?? "";

        public bool Active { get; private set; }
        public int Index { get; private set; }
        public Snapshot At { get; private set; }
        public Step Current => Index < Steps.Count ? Steps[Index] : null;
        public bool Finished => Index >= Steps.Count;

        public void Start(World w, int from = 0) { Active = true; Index = Math.Max(0, Math.Min(from, Steps.Count - 1)); Begin(w); }
        public void Close() => Active = false;
        public void Next(World w) { if (Index < Steps.Count) Index++; if (Finished) Active = false; else Begin(w); }
        public void Back(World w) { if (Index > 0) { Index--; Begin(w); } }

        void Begin(World w)
        {
            At = Snap(w);
            Panned = Zoomed = Rotated = SpeedChanged = WorkersOpened = UndergroundViewed = false;
            // A survival lesson must never spawn an artificial raid.
        }

        /// <summary>Advance when the current step's goal has been met. Returns the finished step (for a "well done" message), or null.</summary>
        public Step Check(World w)
        {
            if (!Active || Finished) return null;
            var s = Current;
            bool done = s.Count != null ? Gained(w) >= s.By : s.Done != null && s.Done(this, w);
            if (!done) return null;
            Next(w);
            return s;
        }

        public static Snapshot Snap(World w)
        {
            var n = new Snapshot();
            n.Parcels = w.ClaimedParcels;
            foreach (var b in w.Blds)
            {
                if (b.T == "road" && b.Served) n.Roads++;
                else if (b.T == "house") n.Houses++;
                else if (b.T == "wood") n.Lodges++;
                else if (b.T == "sawmill") n.Sawmills++;
                else if (b.T == "well" && b.Served) n.Wells++;
                else if (b.T == "hunter" && b.Served) n.Hunters++;
                else if (b.T == "mine" && b.Served) n.Mines++;
                else if (b.T == "quarry" && b.Served) n.Quarries++;
                else if (b.T == "silo" && b.Served) n.Silos++;
            }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (w.Grid[y, x] != null) continue;
                    switch (w.ZoneMap[y, x])
                    {
                        case Zone.Res: if (w.BesideConnectedRoad(x, y)) n.ResByRoad++; break;
                        case Zone.Farm: n.Farmland++; break;
                        case Zone.Wood: n.Lumber++; break;
                        case Zone.Comm: n.Comm++; break;
                        case Zone.Mine: n.Mining++; break;
                    }
                }
            return n;
        }


        int Gained(World w) => Current.Count(Snap(w)) - Current.Count(At);

        /// <summary>How far the current counted goal has got, such as "2 / 4" (null for other steps).</summary>
        public string Progress(World w) => Active && !Finished && Current.Count != null ? $"{Math.Max(0, Math.Min(Current.By, Gained(w)))} / {Current.By}" : null;

        static bool Working(World w, string type) => w.Blds.Any(b => b.T == type && b.Served && b.Workers > 0);
        static bool Connected(World w, string type) => w.Blds.Any(b => b.T == type && b.Served);
        static bool IndustryReady(World w) => Connected(w, "hunter") && Connected(w, "wood") && Connected(w, "sawmill") && Connected(w, "quarry") && Connected(w, "mine");
        static bool WinterReserve(World w) => w.EdibleFoodStored >= w.DailyFoodDemand * Math.Max(100, World.YearLength - w.Day + 1 + World.WheatGrowth);

        public static readonly IReadOnlyList<Step> Steps = new List<Step>
        {
            new Step { Id = "welcome", Title = "Your first year: food before growth",
                Body = "Start with six settlers, two homes, a farmhouse and 1,300 food (the six settlers eat 6 a day). Our goal is to survive winter with hunting, lumber and mining running. Keep the starting homes: adding housing now grows mouths before the first harvest. We will add 18 wheat fields, a silo and one hunting lodge before building industry. This is a fresh-start plan; an older village needs food scaled to its population.", Goal = "Press Next" },
            new Step { Id = "time", Title = "Day 1: pause before planning",
                Body = "Space pauses; the time buttons also pause or change speed. One day normally passes each second. Pause while reading and laying out the first buildings. Keep time paused through the early setup, then use 1x to watch staffing and food. Do not leave a hungry village running at 4x.", Goal = "Pause or change the speed", Done = (t,w) => t.SpeedChanged },
            new Step { Id = "camera", Title = "Find the farm and nearby forest",
                Body = "Pan with W A S D, arrows, or a right/middle-button drag. Zoom with the wheel and rotate with Q/E. Find the starting farmhouse, fertile ground within 10 tiles of it, and two separate patches of forest: one for hunting, another for timber. Inspect shows tile coordinates and building status.", Goal = "Pan, then zoom or rotate", Done = (t,w) => t.Panned && (t.Zoomed || t.Rotated) },
            new Step { Id = "workers", Title = "Protect food jobs first",
                Body = "Open Workers at bottom right. Villagers fill trades from top to bottom. Keep Farming first, Hunting second, then Timber and Woodworking. An idle villager cannot help a farm without more workable fields. Before autumn, 18 fields need two farmers; one lodge has two hunter jobs. Extra industries wait for spare hands.", Goal = "Open Workers", Done = (t,w) => t.WorkersOpened },
            new Step { Id = "farmland", Title = "Day 1: 18 wheat fields",
                Tool = "zoneFarm", Body = "In Farming, select Farmland and zone fertile, clear ground within 10 tiles of the starting farmhouse. Aim for 18 usable plots TOTAL, including the starting eight. Open the Farming panel and set flax to 0%: year-one crops are all wheat. Two farmers can tend 18 plots. Fields outside the farm's reach lie fallow; look at the farmhouse's field count, not just painted tiles.",
                Goal = "Have at least 18 farm-managed plots and no flax planned", Done = (t,w) => w.FarmFields().Count >= 18 && w.PlannedCrops().flax == 0 },
            new Step { Id = "services", Title = "Day 1: save the harvest",
                Tool = "silo", Body = "Build a Silo beside a road connected to the Town Hall. It costs 20 wood and 5 stone and adds 5,000 food storage. Keep it off the 18 fields. Wheat sown on day 1 ripens on day 101; build storage before harvest or food that does not fit is lost. Storage holds food; it does not produce any.", Goal = "Have a road-connected silo", Done = (t,w) => Connected(w,"silo") },
            new Step { Id = "road", Title = "Roads to the forest",
                Tool = "road", Body = "Extend the starting street toward a forest edge. Roads cost one wood per tile; keep them short and do not pave your wheat fields. Working buildings need a continuous connection to the Town Hall. Drag an L-shaped road, then release. Clear only the trees needed for its route; leave the hunting forest intact.", Goal = "Have at least eight connected road tiles", Done = (t,w) => Snap(w).Roads >= 8 },
            new Step { Id = "hunting", Title = "Spring: hunting feeds the gap",
                Tool = "hunter", Body = "Build ONE Hunting Lodge by a connected road at a forest edge. Hunters need nearby reachable trees on owned land; they do not need a Lumber Zone. Short walks give more food: each hunter brings at most three per trip. Do not place the lodge far from its forest or clear its trees. Unpause at 1x and check that at least one hunter is employed.", Goal = "A connected Hunting Lodge has a worker", Done = (t,w) => Working(w,"hunter") },
            new Step { Id = "lumber", Title = "By day 30: a separate timber patch",
                Tool = "zoneWood", Body = "In Wood, build a Woodcutter Lodge on a connected road near a DIFFERENT forest. Mark at least 18 trees in its work area as Lumber. Keep the first seven tiles around the hunting lodge unmarked where possible. Woodcutters fell marked trees and deliver logs; if nobody staffs the lodge, keep food priorities and let the population grow within the starting homes.", Goal = "Mark 18 Lumber tiles by a connected lodge, or start delivering logs", Done = (t,w) => Connected(w,"wood") && (Snap(w).Lumber >= 18 || (Working(w,"wood") && w.Rate[Res.Logs] > 0)) },
            new Step { Id = "sawmill", Title = "By day 60: turn logs into building wood",
                Tool = "sawmill", Body = "Build a road-connected Sawmill (15 wood). Logs are not construction wood: the Town Hall saws only one log a day at half yield. A staffed sawmill turns logs into wood much faster. Keep Farming and Hunting above Woodworking. Pause if food is close to empty; more housing or livestock will make that worse.", Goal = "A connected Sawmill has a worker", Done = (t,w) => Working(w,"sawmill") },
            new Step { Id = "harvest", Title = "Days 101-109: your first wheat",
                Body = "Wheat grows for 100 days after sowing, and each farmer sows one field a day. With two farmers starting on day 1, 18 wheat plots are sown by day 9 and normally harvested on days 101-109. Later sowing delays this. Keep both farmers through summer and the silo connected. Hunting and the starting stores must bridge spring; more fields do not give immediate food.", Goal = "Reach the first summer harvest with wheat in storage", Done = (t,w) => w.YearDay > World.WheatGrowth && w.WheatStored > 0 },
            new Step { Id = "reserve", Title = "Check the food you can actually eat",
                Body = "The Food reserve includes fresh food, smoked meat and eggs, each egg counting as one food. Hover it for the breakdown and days of supply. Seasonal crop production is zero between harvests: a production warning alone is not starvation. Leave housing expansion, livestock and recruitment until this year's food plan is secure. A full-size hen or sheep pen also consumes wheat.", Goal = "Store at least 90 days of food at current population", Done = (t,w) => w.EdibleFoodStored >= w.DailyFoodDemand * 90 },
            new Step { Id = "stone", Title = "After harvest: quarry stone",
                Tool = "zoneMine", Body = "In Mining, place a Quarry beside a connected road near reachable rock. Mark at least six rock/hill tiles in its shaded work area as Mining. Check that quarrymen are employed and stone starts arriving. Do not confuse a painted Mining Zone with a working quarry. Keep the farmland and hunting forest out of its building and road footprint.", Goal = "A staffed connected Quarry and six Mining tiles", Done = (t,w) => Working(w,"quarry") && Snap(w).Mining >= 6 },
            new Step { Id = "mine", Title = "Days 110-180: an iron mine at the hill",
                Tool = "mine", Body = "Build an Iron Mine at a hill or mountain's foot, preferably near orange-veined rock. Put its connected road exactly in front of the entrance shown by the placement tip, and zone nearby high ground for Mining. Keep food and timber jobs first. The first site may be barren: miners must survey before you know where ore lies.", Goal = "An Iron Mine is road-connected and staffed", Done = (t,w) => Working(w,"mine") },
            new Step { Id = "underground", Title = "Survey before expanding the mine",
                Body = "Press U to see the underground layers. Blue is unsurveyed, grey is barren, orange is known ore. The mine must reach the orange tiles in its Mining Zone. If surveys show no ore, extend the zone toward another hill or choose a better mine site; another worker in a barren mine cannot produce iron. Do not spend your opening wood on a deep shaft.", Goal = "Open the underground view", Done = (t,w) => t.UndergroundViewed },
            new Step { Id = "danger", Title = "Keep miners supported",
                Body = "In the Mining panel, require supports. Start with the adit and allow at most one shaft level if the ore needs it. Keep six wood for the adit supports and another six for the first shaft. Even supported mines have risks; deep or unshored shafts are worse. If the mine has no work, inspect its road entrance, zone, surveys and support supply instead of moving farmers away from a growing crop.", Goal = "Require supports and limit the shaft to zero or one", Done = (t,w) => w.RequireMineSupports && w.ShaftDepthLimit <= 1 },
            new Step { Id = "ore", Title = "Verify real mining output",
                Body = "Let the miners survey, drive the supported adit and carry ore home. Watch the ore stock/rate, not just the mine sprite. If there is no reachable ore in this hill, widen the survey or move the mine while there is still time in year one. A quarry supplies stone; only the iron mine supplies ore.", Goal = "A connected mine is delivering ore", Done = (t,w) => Connected(w,"mine") && w.Rate[Res.Ore] > 0 },
            new Step { Id = "autumn", Title = "Autumn: keep the economy working",
                Body = "Autumn is days 181-270. Farmers clear the harvested fields, then release those jobs; in winter the workforce can help hunting, timber, sawing and mining. Keep both forest patches separate. Once the marked timber is cut, mark another patch away from the hunting lodge. Do not assume a connected lodge has trees left to work.", Goal = "Reach autumn with hunting, lumber, sawing, quarrying and mining connected", Done = (t,w) => w.Day > 180 && IndustryReady(w) },
            new Step { Id = "winter", Title = "Winter: reserve food for the next spring",
                Body = "Winter is days 271-360, followed by another spring without a harvest. Keep enough stored food to last until roughly day 461, when next year's earliest wheat ripens. Continue hunting. Do not buy animals or recruit troops out of this reserve. If food is short, pause, check staffed hunting and buy food through trade if available; extra winter fields cannot rescue the current meal.", Goal = "Reach winter with stored food covering the next earliest wheat harvest", Done = (t,w) => w.Day > 270 && WinterReserve(w) },
            new Step { Id = "year", Title = "Finish year one before growing again",
                Body = "At the year boundary, check that hunting, woodcutting, sawing, quarrying and the iron mine are all connected. Keep at least 100 days of stored food for the next spring. The tested 18-field opening kept the original homes, all-wheat farming and no livestock or recruitment. Expand food and water before adding new homes next year.", Goal = "Reach year 2 with all five industries connected and 100 days of food", Done = (t,w) => w.Day > World.YearLength && IndustryReady(w) && w.EdibleFoodStored >= w.DailyFoodDemand * 100 },
            new Step { Id = "housing", Title = "Year two: expand with a food budget",
                Tool = "zoneRes", Body = "Only now zone a few Housing tiles beside a connected road. Houses grow when wanted; do not paint a huge suburb. Each villager eats 1 food a day, 360 a year. One farmer can tend nine wheat plots: 3,600 food per full annual harvest, enough for 10 villagers. Scale farmed wheat and storage for the new mouths, and allow for animal feed and soldiers too.", Goal = "Press Next" },
            new Step { Id = "water", Title = "Before passing 24 people: more drinking water",
                Tool = "well", Body = "The Town Hall's spring supplies 24 water/day and its cistern stores 150. This covers the opening village; a well is not your first food task. Before the town outgrows that spring, connect a well on a blue groundwater patch and inspect its recharge. Roads join wells and homes to one supply. After sustained shortages, people can die.", Goal = "Press Next" },
            new Step { Id = "defence", Title = "Fund troops before recruiting",
                Body = "A Barracks recruits from your workers, and soldiers eat 1.5 food a day each instead of 1. Build defense when food, water and replacement labor can afford it. Natural raids still happen; this tutorial does not spawn an extra attack to force the lesson. Keep gates and supply roads usable, and protect your food stores.", Goal = "Press Next" },
            new Step { Id = "land", Title = "Expand only where you can supply it",
                Tool = "claim", Body = "Your starting district is 64 by 64 tiles. Claim neighboring parcels from Land when you need another forest, ore deposit or farm site. Owning distant resources is not enough: connect the building and keep its work and hauling trips reasonable. Keep the first hunting forest alive while opening new lumber patches.", Goal = "Press Next" },
            new Step { Id = "save", Title = "Save your surviving village",
                Body = "Use Save at top right to keep this settlement. Save again before experimenting with rapid growth, livestock or recruitment. Your first-year foundation is wheat and storage, nearby hunting, separate timber and a sawmill, and surveyed, supported mining. Keep food for the next spring, then grow in small steps.", Goal = "Press Finish" },
        };
    }
}
