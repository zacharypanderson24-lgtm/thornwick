// Vessels (DESIGN_WATER.md, transport stabilization): boats are things the town builds, owns and keeps up, not free sprites.
//
// A vessel is built at a BOATYARD from wood, flax (rope and sailcloth) and metal fittings by the boatyard's craftsmen; an order waits until the
// materials are there. Vessels are assigned automatically to what needs them, by type: a ferry boat to a ferry route, a fishing boat to a
// fishing dock (up to 2), a cargo barge to a port (it is the ship a port's trade uses). A vessel wears with use (and slowly when idle); a
// staffed boatyard repairs the worst ones for wood; below MinService condition a vessel is out of service until repaired. A vessel outlives
// the building it was assigned to: it becomes idle and is reassigned.
//
// Old saves (before vessels) get one vessel for each thing that was already working, so nothing stops: a ferry boat per route, two fishing
// boats per dock (what a dock launched) and a cargo barge per port, all in perfect condition and with a 30-day crew grace for ports. They
// are not paid for and nothing else is created.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public enum VesselType : byte { FishingBoat, FerryBoat, CargoBarge }
    public enum AssignKind : byte { None, FerryRoute, Dock, Port }

    public sealed class VesselSpec
    {
        public string Name; public int Crew, Passengers, Cargo, Wood, Flax, Metal, BuildDays; public double WearPerDay;
    }

    public sealed class Vessel
    {
        public int Id; public VesselType Type; public double Condition = 100;
        public AssignKind Kind; public int Key;   // a ferry route's id, or the tile (y * N + x) of the dock or port it serves
        public int BuiltDay;
    }

    public sealed class VesselOrder { public VesselType Type; public double Progress; public bool Started; }

    public partial class World
    {
        public const double MinService = 25;     // below this condition a vessel is out of service
        public const double IdleWear = 0.02, RepairPerYardDay = 3, WoodPerRepairPoint = 0.1;
        public const int PortCrew = 1, DockBoatsMax = 2;

        public static VesselSpec Spec(VesselType t) => t switch
        {
            VesselType.FishingBoat => new VesselSpec { Name = "Fishing boat", Crew = 2, Passengers = 0, Cargo = 10, Wood = 12, Flax = 2, Metal = 0, BuildDays = 15, WearPerDay = 0.12 },
            VesselType.FerryBoat => new VesselSpec { Name = "Ferry boat", Crew = 2, Passengers = FerryPassengers, Cargo = FerryCargo, Wood = 20, Flax = 4, Metal = 2, BuildDays = 25, WearPerDay = 0.15 },
            _ => new VesselSpec { Name = "Cargo barge", Crew = 3, Passengers = 4, Cargo = 100, Wood = 30, Flax = 4, Metal = 2, BuildDays = 30, WearPerDay = 0.10 },
        };

        public readonly List<Vessel> Vessels = new List<Vessel>();
        public readonly List<VesselOrder> VesselOrders = new List<VesselOrder>();
        int _nextVesselId = 1;
        public int LegacyGraceUntil;   // old saves: ports and docks need no crew until this day
        internal bool PendingVesselMigration;

        internal void RestoreVessel(Vessel v) { Vessels.Add(v); _nextVesselId = Math.Max(_nextVesselId, v.Id + 1); }

        public bool InService(Vessel v) => v.Condition >= MinService;

        // ---------- the boatyard and its orders ----------
        public double YardCapacity => Blds.Where(b => b.T == "boatyard" && b.Served).Sum(b => b.Eff);   // staffed boatyards, as full crews
        public int Boatyards => Blds.Count(b => b.T == "boatyard" && b.Served);

        public Result CanOrder(VesselType t, int quantity = 1)
        {
            if (quantity < 1) return Result.No("Order at least one");
            if (Boatyards == 0) return Result.No("Build a Boatyard (Water menu) beside a road on the shore: vessels are built there");
            if (VesselOrders.Count + quantity > 20) return Result.No("The boatyard's list is full (20 orders)");
            return Result.Yes();
        }

        /// <summary>Order vessels: they join the list and are built one at a time as the boatyard's crews and materials allow.</summary>
        public Result OrderVessel(VesselType t, int quantity = 1)
        {
            var c = CanOrder(t, quantity); if (!c.Ok) return c;
            for (int i = 0; i < quantity; i++) VesselOrders.Add(new VesselOrder { Type = t });
            return Result.Yes(quantity);
        }

        public bool CancelOrder(int index)
        {
            if (index < 0 || index >= VesselOrders.Count) return false;
            var o = VesselOrders[index];
            if (o.Started) { var s = Spec(o.Type); ResAmt[Res.Wood] += s.Wood; ResAmt[Res.Flax] += s.Flax; ResAmt[Res.Metal] += s.Metal; }   // materials come back
            VesselOrders.RemoveAt(index); return true;
        }

        public string OrderStatus(VesselOrder o)
        {
            var s = Spec(o.Type);
            if (o.Started) return $"building: {o.Progress / s.BuildDays * 100:0}%";
            return HasMaterials(s) ? "waiting for the boatyard" : $"waiting for materials (wood {s.Wood}, flax {s.Flax}" + (s.Metal > 0 ? $", metal {s.Metal})" : ")");
        }

        bool HasMaterials(VesselSpec s) => ResAmt[Res.Wood] >= s.Wood && ResAmt[Res.Flax] >= s.Flax && ResAmt[Res.Metal] >= s.Metal;

        // ---------- the daily step ----------
        public void StepVessels()
        {
            if (Waters == null) return;
            double work = YardCapacity;
            // build: the first order in the list, if its materials are there
            if (work > 0 && VesselOrders.Count > 0)
            {
                var o = VesselOrders[0]; var s = Spec(o.Type);
                if (!o.Started && HasMaterials(s)) { ResAmt[Res.Wood] -= s.Wood; ResAmt[Res.Flax] -= s.Flax; ResAmt[Res.Metal] -= s.Metal; o.Started = true; }
                if (o.Started)
                {
                    o.Progress += work;
                    if (o.Progress >= s.BuildDays) { Vessels.Add(new Vessel { Id = _nextVesselId++, Type = o.Type, BuiltDay = Day }); VesselOrders.RemoveAt(0); Events.Add($"A {s.Name.ToLower()} is finished at the boatyard."); }
                }
            }
            // wear: vessels at work wear their type's rate, the rest slowly
            foreach (var v in Vessels)
            {
                bool working = v.Kind != AssignKind.None && InService(v) && TargetRuns(v);
                v.Condition = Math.Max(0, v.Condition - (working ? Spec(v.Type).WearPerDay : IdleWear));
            }
            // repair: staffed boatyards mend the worst vessels, 1 wood for every 10 points
            double repair = RepairPerYardDay * work;
            foreach (var v in Vessels.Where(x => x.Condition < 100).OrderBy(x => x.Condition))
            {
                if (repair <= 0 || ResAmt[Res.Wood] < 1) break;
                double points = Math.Min(Math.Min(repair, 100 - v.Condition), ResAmt[Res.Wood] / WoodPerRepairPoint);
                v.Condition += points; repair -= points; ResAmt[Res.Wood] -= points * WoodPerRepairPoint;
            }
            AssignVessels();
        }

        /// <summary>True when what the vessel is assigned to is currently running (so the vessel is in use).</summary>
        bool TargetRuns(Vessel v)
        {
            switch (v.Kind)
            {
                case AssignKind.FerryRoute: { var r = FerryRoutes.FirstOrDefault(x => x.Id == v.Key); return r != null && (StateOf(r) == FerryState.Operational); }
                case AssignKind.Dock: { var d = Grid[v.Key / N, v.Key % N]; return d != null && d.T == "dock" && d.Served && d.Workers > 0; }
                case AssignKind.Port: { var p = Grid[v.Key / N, v.Key % N]; return p != null && p.T == "port" && p.Served; }
            }
            return false;
        }

        bool TargetExists(Vessel v)
        {
            switch (v.Kind)
            {
                case AssignKind.FerryRoute: return FerryRoutes.Any(x => x.Id == v.Key);
                case AssignKind.Dock: return Grid[v.Key / N, v.Key % N]?.T == "dock";
                case AssignKind.Port: return Grid[v.Key / N, v.Key % N]?.T == "port";
            }
            return false;
        }

        /// <summary>Vessels with no job go to what needs one of their type: ferry boats to routes without a ferry boat, fishing boats to docks
        /// that want more (up to DockBoatsMax), barges to ports without one. A vessel whose target is gone becomes idle.</summary>
        void AssignVessels()
        {
            foreach (var v in Vessels) if (v.Kind != AssignKind.None && !TargetExists(v)) { v.Kind = AssignKind.None; v.Key = 0; }
            foreach (var v in Vessels.Where(x => x.Kind == AssignKind.None).OrderBy(x => x.Id))
            {
                switch (v.Type)
                {
                    case VesselType.FerryBoat:
                        var route = FerryRoutes.Where(r => !Vessels.Any(o => o.Kind == AssignKind.FerryRoute && o.Key == r.Id)).OrderBy(r => r.Id).FirstOrDefault();
                        if (route != null) { v.Kind = AssignKind.FerryRoute; v.Key = route.Id; }
                        break;
                    case VesselType.FishingBoat:
                        var dock = Blds.Where(b => b.T == "dock" && Vessels.Count(o => o.Kind == AssignKind.Dock && o.Key == Tk(b.X, b.Y)) < DockBoatsMax).OrderBy(b => Tk(b.X, b.Y)).FirstOrDefault();
                        if (dock != null) { v.Kind = AssignKind.Dock; v.Key = Tk(dock.X, dock.Y); }
                        break;
                    case VesselType.CargoBarge:
                        var port = Blds.Where(b => b.T == "port" && !Vessels.Any(o => o.Kind == AssignKind.Port && o.Key == Tk(b.X, b.Y))).OrderBy(b => Tk(b.X, b.Y)).FirstOrDefault();
                        if (port != null) { v.Kind = AssignKind.Port; v.Key = Tk(port.X, port.Y); }
                        break;
                }
            }
        }

        public Vessel VesselOfRoute(FerryRoute r) => Vessels.FirstOrDefault(v => v.Kind == AssignKind.FerryRoute && v.Key == r.Id);
        public IEnumerable<Vessel> VesselsOfDock(Building dock) => Vessels.Where(v => v.Kind == AssignKind.Dock && v.Key == Tk(dock.X, dock.Y));
        public Vessel VesselOfPort(Building port) => Vessels.FirstOrDefault(v => v.Kind == AssignKind.Port && v.Key == Tk(port.X, port.Y));
        public int DockBoatsInService(Building dock) => VesselsOfDock(dock).Count(InService);

        public string AssignmentText(Vessel v)
        {
            switch (v.Kind)
            {
                case AssignKind.FerryRoute: { var r = FerryRoutes.FirstOrDefault(x => x.Id == v.Key); return r == null ? "idle" : $"ferry route {r.AX},{r.AY} to {r.BX},{r.BY}"; }
                case AssignKind.Dock: return $"dock at {v.Key % N},{v.Key / N}";
                case AssignKind.Port: return $"port at {v.Key % N},{v.Key / N}";
            }
            return "idle: waiting for something that needs it";
        }

        // ---------- old saves ----------
        /// <summary>One vessel for everything that was already working (see the file header). Runs once, on load.</summary>
        public int MigrateLegacyVessels(int graceDays = FerryMigrationGraceDays)
        {
            if (Waters == null) return 0; int made = 0;
            Vessel Add(VesselType t, AssignKind k, int key) { var v = new Vessel { Id = _nextVesselId++, Type = t, Kind = k, Key = key, BuiltDay = Day }; Vessels.Add(v); made++; return v; }
            foreach (var r in FerryRoutes) Add(VesselType.FerryBoat, AssignKind.FerryRoute, r.Id);
            foreach (var d in Blds.Where(b => b.T == "dock")) for (int i = 0; i < DockBoatsMax; i++) Add(VesselType.FishingBoat, AssignKind.Dock, Tk(d.X, d.Y));
            foreach (var p in Blds.Where(b => b.T == "port")) Add(VesselType.CargoBarge, AssignKind.Port, Tk(p.X, p.Y));
            if (made > 0) { LegacyGraceUntil = Day + graceDays; Events.Add($"Boats are now vessels the town owns and keeps up. {made} vessel{(made == 1 ? "" : "s")} were given to your existing ferries, docks and ports so nothing stops; build a Boatyard to make and mend more. Ports and docks have {graceDays} days to find their crews."); }
            return made;
        }
    }
}
