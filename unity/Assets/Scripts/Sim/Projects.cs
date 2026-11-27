// Development projects: the unit of work of the organic development engine (DESIGN_ORGANIC_DEVELOPMENT.md, phase 2).
// A project is one proposed or running construction in a district. Materials are DEBITED when a project is funded and held inside it,
// so no two projects can ever hold the same materials, and a cancel can only give back what was not yet used.
using System.Collections.Generic;

namespace CrownAndHearth.Sim
{
    public enum ProjectStatus : byte { Proposed, Waiting, Building, Done, Rejected, Cancelled, Clearing }

    public class DevProject
    {
        public int Id, DistrictId;
        public string Type;
        public int X, Y, Sz = 1, Tier = 1;
        public Dictionary<Res, int> Cost = new Dictionary<Res, int>();
        public Dictionary<Res, int> Held = new Dictionary<Res, int>();   // materials taken from the store and not yet used or returned
        public ProjectStatus Status;
        public bool Paused;
        public int Rank;                 // the player's reprioritise nudge (higher goes first)
        public int BuildDays, Progress;  // 0 days: finished the day it is funded (housing); otherwise one builder per project
        public int Created, Closed;
        public string Reason = "", Benefit = "", Missing = "";
        public List<int> ClearKeys = new List<int>();   // trees on the site that must be cleared first (this project designated those clearing orders)

        public bool Open => Status == ProjectStatus.Proposed || Status == ProjectStatus.Waiting || Status == ProjectStatus.Building || Status == ProjectStatus.Clearing;
    }

    /// <summary>Optional construction reserve and cap, with defaults that need no setup.</summary>
    public class DevReserve
    {
        public int KeepWood, KeepStone, KeepGold;   // never spend the store below this on automatic projects
        public int MaxOpenProjects = 3;             // simultaneous automatic projects (proposals awaiting approval do not count)
    }
}
