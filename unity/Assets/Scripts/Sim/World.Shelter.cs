// Shelter (Zach's rule): when raiders come, every civilian runs into the Town Hall and hides there until the raid is over.
// Work crews and townsfolk keep their own state machines (World.Crews.cs); while a raid is on those machines hand each agent to the
// two functions here first, and an agent that is hiding is not drawn. Runtime only, never saved: after a load everyone starts at home.
//
// They run down the same distance field raiders use to reach the Hall (RaidField), two tiles a day, so they follow roads and open ground and
// stop at closed gates and walls. Anyone with no way to the Hall hides where they are. Miners underground stay down (World.Mining.cs).
// While they hide the work stops: raids cost the town its day's output, which is part of what a raid is. A load a worker was carrying is lost.
using System;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        /// <summary>Tiles a fleeing civilian covers in a day (a soldier marches at FighterSpeed).</summary>
        public const int FleeSpeed = 2;

        /// <summary>True from the day before the first raider walks onto the map until the raid is over (and there is a Hall to run to).</summary>
        public bool Sheltering
        {
            get
            {
                if (Raid == null || Raid.Over || Defeated || Hall == null) return false;
                int first = int.MaxValue;
                foreach (var a in Raid.Raiders)
                    if (a.St != RaiderState.Dead && a.St != RaiderState.Gone) first = Math.Min(first, a.St == RaiderState.Waiting ? a.Delay : 0);
                return first != int.MaxValue && Day - Raid.StartDay >= first - 1;
            }
        }

        /// <summary>How many civilians are inside the Hall right now (townsfolk and work crews).</summary>
        public int Sheltered
        {
            get
            {
                int n = 0;
                foreach (var f in Folk) if (f.St == FolkState.Shelter) n++;
                foreach (var b in Blds) foreach (var a in b.Crew) if (a.St == CrewState.Shelter) n++;
                return n;
            }
        }

        /// <summary>A walkable tile beside a building with a way to the Hall, nearest first; -1 when the building has none.</summary>
        int ShelterExit(Building b, int[] field)
        {
            int best = -1, bd = int.MaxValue;
            for (int y = b.Y - 1; y <= b.Y + b.Sz; y++)
                for (int x = b.X - 1; x <= b.X + b.Sz; x++)
                {
                    if (!Inb(x, y)) continue;
                    int d = field[y * N + x];
                    if (d >= 0 && d < bd) { bd = d; best = y * N + x; }
                }
            return best;
        }

        /// <summary>Move up to FleeSpeed tiles down the field. Returns true when the agent has reached the Hall's door.</summary>
        bool Flee(ref int x, ref int y, int[] field)
        {
            for (int s = 0; s < FleeSpeed; s++)
            {
                int d = field[y * N + x];
                if (d <= 1) return true;
                int bx = x, by = y, bd = d;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + Nb[k, 0], ny = y + Nb[k, 1];
                    if (!Inb(nx, ny)) continue;
                    int nd = field[ny * N + nx];
                    if (nd >= 0 && nd < bd) { bd = nd; bx = nx; by = ny; }
                }
                if (bx == x && by == y) return false;   // stuck: keep still this day
                x = bx; y = by;
            }
            return field[y * N + x] <= 1;
        }

        /// <summary>One work-crew agent of building b during a raid. Returns true when it was handled here (the normal step is skipped).</summary>
        bool ShelterWorker(Building b, Worker a)
        {
            if (a.St == CrewState.Down) return false;
            if (!Sheltering)
            {
                if (a.St != CrewState.Hide && a.St != CrewState.Shelter) return false;
                a.St = CrewState.Home; a.Path.Clear(); a.Stand = -1; a.Carry = 0; a.X = b.X; a.Y = b.Y;
                return true;   // back at the workplace; the next day it goes to work as usual
            }
            if (a.St == CrewState.Shelter) return true;
            var field = RaidField(Raid, Hall);
            if (a.St != CrewState.Hide)
            {
                int start = a.St == CrewState.Home ? ShelterExit(b, field) : a.Y * N + a.X;
                if (start < 0 || field[start] < 0) { a.St = CrewState.Shelter; a.X = Hall.X; a.Y = Hall.Y; return true; }
                a.X = start % N; a.Y = start / N; a.St = CrewState.Hide; a.Path.Clear(); a.Stand = -1; a.Carry = 0;
                if (a.Px < 0) { a.Px = a.X; a.Py = a.Y; }
            }
            int x = a.X, y = a.Y;
            if (Flee(ref x, ref y, field)) { a.St = CrewState.Shelter; a.X = Hall.X; a.Y = Hall.Y; }
            else { a.X = x; a.Y = y; }
            return true;
        }

        /// <summary>One townsperson during a raid. Returns true when handled here.</summary>
        bool ShelterFolk(Villager w)
        {
            if (!Sheltering)
            {
                if (w.St != FolkState.Hide && w.St != FolkState.Shelter) return false;
                w.St = FolkState.Home; w.Path.Clear(); w.X = w.Home.X; w.Y = w.Home.Y; w.T = 2 + w.Id % 4;   // they come out over the next few days
                return true;
            }
            if (w.St == FolkState.Shelter) return true;
            var field = RaidField(Raid, Hall);
            if (w.St != FolkState.Hide)
            {
                int start = w.St == FolkState.Home ? ShelterExit(w.Home, field) : w.Y * N + w.X;
                if (start < 0 || field[start] < 0) { w.St = FolkState.Shelter; w.X = Hall.X; w.Y = Hall.Y; return true; }
                w.X = start % N; w.Y = start / N; w.St = FolkState.Hide; w.Path.Clear();
            }
            int x = w.X, y = w.Y;
            if (Flee(ref x, ref y, field)) { w.St = FolkState.Shelter; w.X = Hall.X; w.Y = Hall.Y; }
            else { w.X = x; w.Y = y; }
            return true;
        }
    }
}
