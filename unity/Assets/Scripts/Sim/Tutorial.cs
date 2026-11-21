// The in-game tutorial: a guided walk through the existing mechanics, one step at a time. Pure C# (no UnityEngine) so EditMode tests can
// drive it; the HUD shows it (Game/Hud.cs) and GameController feeds it the player's actions it cannot see in the world (camera, panels).
// A step is done when its check sees the player actually did the thing in the world (counted against a snapshot taken when the step
// began, so earlier towns don't count); steps without a check are explanations that wait for Next. Every step can be skipped.
using System;
using System.Collections.Generic;
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
        public struct Snapshot { public int Roads, ResByRoad, Houses, Farmland, Lumber, Lodges, Sawmills, Wells, Comm, Silos, Mining, Parcels; }

        // actions the world cannot show, reported by the game
        public bool Panned, Zoomed, Rotated, SpeedChanged, WorkersOpened, UndergroundViewed;

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
                else if (b.T == "well") n.Wells++;
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

        public static readonly IReadOnlyList<Step> Steps = new List<Step>
        {
            new Step { Id = "welcome", Title = "Welcome to Crown & Hearth",
                Body = "Six settlers, a Town Hall, two huts and a farm. Grow them into a thriving town.\n\n" +
                       "The bar at the top shows your stores (food, wood, logs, stone, ore, gold) with their daily change, and your people: " +
                       "peasants and housing, idle villagers and jobs.\n\nThis tutorial walks you through everything the game can do. " +
                       "Each step finishes by itself when you do it; Skip moves on, Close hides it (the Tutorial button brings it back).",
                Goal = "Press Next" },
            new Step { Id = "camera", Title = "Look around",
                Body = "Pan with W A S D, the arrow keys, or by dragging with the right or middle mouse button.\n" +
                       "Zoom with the mouse wheel. Rotate the view with Q and E (buildings show their other sides).",
                Goal = "Pan, then zoom or rotate", Done = (t, w) => t.Panned && (t.Zoomed || t.Rotated) },
            new Step { Id = "time", Title = "Time",
                Body = "One day passes every second. Space pauses; the 1x, 2x and 4x buttons speed things up while you wait for the town to grow.",
                Goal = "Pause or change the speed", Done = (t, w) => t.SpeedChanged },
            new Step { Id = "road", Title = "Roads", Tool = "road",
                Body = "Everything you build is in the bar along the bottom, grouped by job: Roads, Housing, Farming, Wood, Hunting, Mining and more. " +
                       "Hover a button to see what it does. Working buildings need a road back to the Town Hall.\n\n" +
                       "Pick Roads (1), press on a tile next to the street and drag: an L-shaped road follows the mouse and is built when you let go. " +
                       "Each tile costs 1 wood. Roads climb hills but not mountains, and cannot go through forest.",
                Goal = "Lay 3 more road tiles connected to the Town Hall", Count = s => s.Roads, By = 3 },
            new Step { Id = "housing", Title = "Homes", Tool = "zoneRes",
                Body = "You don't build houses yourself: you zone land and villagers build there when they need room.\n\n" +
                       "Pick Housing (2), press on a tile and drag over the land: green tiles will be zoned, red ones cannot be. Let go to zone them. " +
                       "Homes only grow on tiles touching a connected road, so zone along the street. Right-click or Esc puts the tool away.",
                Goal = "Zone 4 Housing tiles beside a connected road", Count = s => s.ResByRoad + s.Houses, By = 4 },
            new Step { Id = "growth", Title = "Watch it grow",
                Body = "The Homes meter in the bottom bar says what the town wants: green and full means homes are wanted. Homes are wanted when houses are nearly full " +
                       "and food is stocked. Hover a zone tile: the panel on the left says why nothing grows yet.\n\nTry 4x speed and wait for a new home.",
                Goal = "A new home grows", Count = s => s.Houses, By = 1 },
            new Step { Id = "water", Title = "Water", Tool = "well",
                Body = "Every villager drinks each day. The Town Hall's spring and cistern cover the first homes. Wells, the spring and cisterns feed " +
                       "one supply for every home on a connected road, however far away. If the town drinks more than it pumps, the cisterns run " +
                       "down, homes go short and, after 15 days, people leave.\n\n" +
                       "Open Water (9) and pick Well: wells go on the blue groundwater patches and share one patch's water. The tip shows what a well adds and whether its patch runs down.",
                Goal = "Build a well on groundwater", Count = s => s.Wells, By = 1 },
            new Step { Id = "farmland", Title = "Food", Tool = "zoneFarm",
                Body = "Villagers eat every day; with no food they starve and leave.\n\nOpen Farming (3) and pick Farmland, then drag over fertile soil (the darker, olive ground) anywhere. " +
                       "Farmhouses have no radius: their jobs are how many farmers you can have, and your fields decide the crops and how many are needed. " +
                       "The Farming menu above the build bar sets the wheat and flax mix. " +
                       "Sow in spring, harvest in summer, and store enough to reach the next harvest. Workers take other jobs when fields need no work.",
                Goal = "Zone 6 Farmland tiles", Count = s => s.Farmland, By = 6 },
            new Step { Id = "workers", Title = "Workers",
                Body = "Buildings only work when villagers staff them. Open Workers (bottom right): villagers fill the trades from the top down, " +
                       "farming first. Use Up and Dn to change who gets hands first; a row in red is short of workers.",
                Goal = "Open the Workers panel", Done = (t, w) => t.WorkersOpened },
            new Step { Id = "lumber", Title = "Wood", Tool = "zoneWood",
                Body = "Wood builds everything. Open Wood (4): build a Woodcutter Lodge beside a road, then drag a Lumber Zone over forest within its shaded work area. " +
                       "Woodcutters walk out, nearest trees first, fell the marked trees and carry logs home; the lodge sets how many you can have, the zoned trees how many are needed. Felled land turns to stumps, then open ground.\n\n" +
                       "A Forester (also in Wood) replants saplings on Lumber-zoned open ground, so a forest can last.",
                Goal = "Zone 6 Lumber tiles", Count = s => s.Lumber, By = 6 },
            new Step { Id = "sawmill", Title = "Logs into wood", Tool = "sawmill",
                Body = "Logs must be sawn into wood. The Town Hall saws 1 log a day at half yield; a Sawmill (Wood, 15 wood) saws 4 a day at full yield. " +
                       "Build one beside a road.",
                Goal = "Build a Sawmill", Count = s => s.Sawmills, By = 1 },
            new Step { Id = "services", Title = "Crop storage", Tool = "silo",
                Body = $"Build a Silo (Farming, 3) beside a connected road to store {World.SiloFood} more wheat and {World.SiloFlax} more flax.",
                Goal = "Build and connect a Silo", Count = s => s.Silos, By = 1 },
            new Step { Id = "stone", Title = "Stone and iron", Tool = "zoneMine",
                Body = "Open Mining (6). Mining Zones go on rock and on hills and mountains.\n\n" +
                       "Stone: build a Quarry, then zone rock outcrops or a mountain's edge within its shaded work area; quarries dig hills down and cut into mountains.\n\n" +
                       "Iron lies hidden inside hills and mountains (orange-veined stones on the slopes hint at it). Zone the high ground and a lot at its " +
                       "foot, then place an Iron Mine. Its entrance faces away from the hill and needs the road right at that entrance.",
                Goal = "Zone 6 Mining tiles", Count = s => s.Mining, By = 6 },
            new Step { Id = "underground", Title = "Underground",
                Body = "Press U for the underground view (U again for deeper bands). A mine's miners first survey its zone: orange shows the ore " +
                       "they found (brighter is richer), grey shows nothing, blue is not surveyed yet. Then they drive an adit into the hill and " +
                       "bring the ore up; ore below the mine's floor needs a shaft.",
                Goal = "Press U", Done = (t, w) => t.UndergroundViewed },
            new Step { Id = "danger", Title = "Supports and cave-ins",
                Body = "Mines take wood to shore up their tunnels. Unshored tunnels, deep shafts and crowded mines can cave in: the mine closes " +
                       "for repairs and a miner may die. Keep some wood in store while you mine. Click a mine to see its tunnels and supports.",
                Goal = "Press Next" },
            new Step { Id = "defence", Title = "Troops",
                Body = "A Barracks (Military, 8) trains infantry (wood, gold, iron ore) and archers (wood, gold, flax) from your peasants. Pick the share of archers in the Troops menu. Troops are a standing army: each recruit leaves your workforce for good and keeps a place in the population, so births do not replace them. They eat half as much again and do no other work, " +
                       "so every one is a farmer or woodcutter you give up.",
                Goal = "Press Next" },
            new Step { Id = "land", Title = "Land", Tool = "claim",
                Body = "You own a 64 x 64 district round the Town Hall: the brighter land. The rest of the 256 x 256 map is yours to see and walk, but not to build or work until you own it.\n\n" +
                       "Open Land (L) and click a 16 x 16 parcel next to your land to claim it. Land is free; claim what you need.",
                Goal = "Claim a parcel of land", Count = s => s.Parcels, By = 1 },
            new Step { Id = "save", Title = "Saving",
                Body = "Save and Load (top right) keep one settlement on this computer. Hover or click any tile to learn what it is and what it needs.\n\n" +
                       "The Map (M, or the Map button) shows the whole world: cold in the north, hot in the south. Your settlement is the gold marker.\n\n" +
                       "That's everything. Good luck with your town!",
                Goal = "Press Next to finish" },
        };
    }
}
