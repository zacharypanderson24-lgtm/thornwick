// Drinking water: a daily service (Unity-only mechanic, not in the browser prototype).
//
// GROUNDWATER lies in aquifers: contiguous patches generated from the seed (AquiferAt). Every well on the same patch shares its
// reserve and daily recharge; each well has its own pumping limit. When the wells draw more than the recharge, the difference comes
// out of the reserve; at an empty reserve their output falls to what recharges each day; when they draw less, the aquifer refills
// up to its capacity. Example: 300 stored, 20 recharge, 60 drawn a day -> empty after 300 / (60 - 20) = 7.5 days.
//
// SURFACE DELIVERY is separate: water carriers walk from a source (the Town Hall's spring, a well) or a store (the Hall's cistern, a
// Cistern) along Hall-connected roads, up to a few road tiles, to the homes there (ReachFrom). The routing takes a "closed" test for
// road tiles water cannot pass, so a future closed gate can cut the deliveries from wells outside the walls while an inside well
// still taps an aquifer that runs beneath them (ForecastClosure is the preview for that).
//
// EACH DAY (StepWater): homes ask for water (villagers x PerHead). Sources pump only to meet that demand and to fill cistern space
// they reach; springs are used before groundwater, so a spare well leaves the aquifer alone when demand is met and storage is full.
// What the sources cannot deliver comes from stored water that reaches the home. Homes still short count days (Building.ShortDays):
// while any home is short, house growth stops and births slow; after Grace days the home's capacity halves and residents leave.
// A home that is fully watered (reached by water and not past its grace) is level 2: this replaces the old "well within 4 tiles" rule.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    /// <summary>Water tuning, in one place so it is easy to adjust.</summary>
    public static class WaterTuning
    {
        public const double PerHead = 1;          // water a villager drinks a day
        public const double HallSpring = 24;      // the Town Hall's own spring, a day; never runs dry (covers the opening town of up to 18)
        public const double HallCistern = 150;    // the Town Hall's cistern
        public const int HallReach = 8;           // road tiles the Hall's water carriers walk
        public const double WellPump = 30;        // one well's pumping limit a day
        public const double WellSeep = 6;         // a well off any aquifer (only from older saves) seeps this much a day, with no reserve
        public const int WellReach = 6;
        public const double CisternCap = 120;
        public const int CisternReach = 6;
        public const int Grace = 15;              // days a home can go short before its residents start to leave
        public const int StoreWarnDays = 8;       // warn when stored water covers a shortfall for fewer days than this
        public const double ReservePerTile = 5, RechargePerTile = 0.4;   // a 60-tile aquifer holds 300 and recharges 24 a day
        public const int MinAquiferTiles = 12;
        // the patches: about Patches of them on a 128 map, radius RadiusMin..RadiusMax tiles (about 25 to 165 tiles each, 10-13% of
        // the map), ragged edges, and one NearTown tiles from the Town Hall
        public const int Patches = 24, NearTown = 10;
        public const double RadiusMin = 3, RadiusMax = 7, Ragged = 0.6;
    }

    public enum WaterWhy : byte { Ok, NoRoad, TooFar, NotEnough }

    public sealed class Aquifer
    {
        public int Id, Tiles, Cx, Cy;
        public double Cap, Recharge, Reserve;
        public double Draw;      // what all its wells took on the last day
        public bool Limited;     // the last day's draw was held down to what was left (empty reserve)
        public string Trend => World.Trend(Reserve, Cap, Recharge, Draw, Limited);
    }

    public sealed class WaterSource
    {
        public Building B;        // null for a well that is only being previewed
        public int X, Y, Sz, Reach, Aquifer = -1;
        public double Pump;
        public bool Spring => Aquifer < 0;
    }

    /// <summary>Road tiles a water carrier reaches from one source or store, and the homes and stores beside them.</summary>
    public sealed class WaterReach
    {
        public readonly HashSet<int> Roads = new HashSet<int>();
        public readonly List<Building> Homes = new List<Building>();
        public readonly List<Building> Stores = new List<Building>();
    }

    /// <summary>One day of water: who needs what, who gets it from where, and what is left. Made by PlanWater without changing the world.</summary>
    public sealed class WaterPlan
    {
        public double Demand, Delivered, FromStore, StoreFill, Stored, StoreCap;
        public double Unmet => Math.Max(0, Demand - Delivered - FromStore);
        /// <summary>The shortfall the sources leave, before stored water covers it.</summary>
        public double Shortfall => Math.Max(0, Demand - Delivered);
        public readonly Dictionary<Building, double> Need = new Dictionary<Building, double>();
        public readonly Dictionary<Building, double> Got = new Dictionary<Building, double>();       // from sources
        public readonly Dictionary<Building, double> Stocked = new Dictionary<Building, double>();   // from stores
        public readonly Dictionary<Building, WaterWhy> Why = new Dictionary<Building, WaterWhy>();
        public readonly Dictionary<WaterSource, double> Pumped = new Dictionary<WaterSource, double>();
        public readonly Dictionary<WaterSource, WaterReach> SourceReach = new Dictionary<WaterSource, WaterReach>();
        public readonly Dictionary<Building, WaterReach> StoreReach = new Dictionary<Building, WaterReach>();
        public readonly Dictionary<Building, double> StoreAfter = new Dictionary<Building, double>();
        public double[] AqDraw = new double[0];
        public bool[] AqLimited = new bool[0];
        public List<WaterSource> Sources = new List<WaterSource>();
        public int ShortHomes => Need.Count(kv => kv.Value - Got[kv.Key] - Stocked[kv.Key] > 1e-6);
        public bool IsShort(Building h) => Need.TryGetValue(h, out var n) && n - Got[h] - Stocked[h] > 1e-6;
        public double GroundwaterDraw => AqDraw.Sum();
        /// <summary>Days the stored water lasts if it keeps covering the shortfall at today's rate (null when no stored water was needed,
        /// either because the sources keep up or because no store reaches the short homes).</summary>
        public double? StoreDays => FromStore > 1e-6 ? Stored / FromStore : (double?)null;
    }

    public partial class Building
    {
        public double Water;        // stored water (Town Hall and Cistern), saved
        public int ShortDays;       // homes: days in a row without enough water, saved
        // the last day's water (runtime; recomputed by the next day)
        public double WaterNeed, WaterGot, WaterStocked, Pumped;
        public WaterWhy WaterWhy;
        public bool Parched => ShortDays >= WaterTuning.Grace && T == "house";
        /// <summary>Homes this house holds now: half when it has gone without water past its grace, so residents leave.</summary>
        public int Housing => Parched ? Capacity / 2 : Capacity;
    }

    public partial class World
    {
        // ---------- Aquifers ----------
        public readonly int[] AquiferMap = new int[N * N];   // aquifer id per tile, -1 = no groundwater
        public readonly List<Aquifer> Aquifers = new List<Aquifer>();
        public int AquiferAt(int x, int y) => Inb(x, y) ? AquiferMap[Tk(x, y)] : -1;

        /// <summary>Groundwater from the seed alone (not the terrain), so a save always gets the same patches back. Patches are numbered
        /// in scan order, which keeps their ids (and the saved reserves) stable.</summary>
        void GenAquifers()
        {
            // round patches with ragged edges at seeded centres, kept apart so they never merge into one huge aquifer;
            // the first is always about NearTown tiles from the Town Hall
            int draws = 0; double Rnd() => Noise.Hash2(draws++, 41, Seed + 953);
            var patches = new List<(double x, double y, double r)>();
            double ang = Rnd() * Math.PI * 2;
            patches.Add((Start + Math.Cos(ang) * WaterTuning.NearTown, Start + Math.Sin(ang) * WaterTuning.NearTown, WaterTuning.RadiusMin + 1));
            int want = (int)Math.Round(WaterTuning.Patches * (N / 128.0) * (N / 128.0));
            for (int t = 0; t < 2000 && patches.Count < want; t++)
            {
                double r = WaterTuning.RadiusMin + Rnd() * (WaterTuning.RadiusMax - WaterTuning.RadiusMin), x = 4 + Rnd() * (N - 8), y = 4 + Rnd() * (N - 8);
                bool clear = true;
                foreach (var p in patches) if (Noise.Hypot(p.x - x, p.y - y) < p.r + r + 4) { clear = false; break; }
                if (clear) patches.Add((x, y, r));
            }
            var wet = new bool[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    foreach (var p in patches)
                    {
                        double d = Noise.Hypot(x - p.x, y - p.y) / p.r + (Noise.VNoise(x * 0.3, y * 0.3, Seed + 954) - 0.5) * WaterTuning.Ragged;
                        if (d < 1) { wet[Tk(x, y)] = true; break; }
                    }
            Array.Fill(AquiferMap, -1);
            Aquifers.Clear();
            var stack = new Stack<int>(); var tiles = new List<int>();
            for (int k0 = 0; k0 < N * N; k0++)
            {
                if (!wet[k0] || AquiferMap[k0] != -1) continue;
                tiles.Clear(); stack.Push(k0); AquiferMap[k0] = -2;
                while (stack.Count > 0)
                {
                    int k = stack.Pop(); tiles.Add(k);
                    int x = k % N, y = k / N;
                    for (int n = 0; n < 4; n++)
                    {
                        int i = x + Nb[n, 0], j = y + Nb[n, 1];
                        if (!Inb(i, j)) continue;
                        int kk = Tk(i, j);
                        if (wet[kk] && AquiferMap[kk] == -1) { AquiferMap[kk] = -2; stack.Push(kk); }
                    }
                }
                if (tiles.Count < WaterTuning.MinAquiferTiles) { foreach (int k in tiles) AquiferMap[k] = -3; continue; }
                var a = new Aquifer { Id = Aquifers.Count, Tiles = tiles.Count, Cap = tiles.Count * WaterTuning.ReservePerTile, Recharge = tiles.Count * WaterTuning.RechargePerTile };
                a.Reserve = a.Cap;
                a.Cx = (int)Math.Round(tiles.Average(k => k % N)); a.Cy = (int)Math.Round(tiles.Average(k => k / N));
                foreach (int k in tiles) AquiferMap[k] = a.Id;
                Aquifers.Add(a);
            }
            for (int k = 0; k < N * N; k++) if (AquiferMap[k] < 0) AquiferMap[k] = -1;
        }

        /// <summary>Plain-language state of an aquifer: Refilling, Stable, Drawing down, or Dry (empty, giving only its recharge).</summary>
        public static string Trend(double reserve, double cap, double recharge, double draw, bool limited)
        {
            if (limited && reserve <= 1e-6) return "Dry";
            if (draw > recharge + 1e-6) return "Drawing down";
            if (draw < recharge - 1e-6 && reserve < cap - 1e-6) return "Refilling";
            return "Stable";
        }

        /// <summary>Days until an aquifer is empty at this draw (null when it is not drawing down).</summary>
        public static double? DaysLeft(double reserve, double recharge, double draw) =>
            draw > recharge + 1e-6 ? reserve / (draw - recharge) : (double?)null;

        // ---------- Sources, stores, homes ----------
        static bool IsHome(Building b) => b.T == "house" || b.T == "hall";
        static bool IsStore(Building b) => b.T == "hall" || b.T == "cistern";
        public static double StoreCapOf(Building b) => b.T == "hall" ? WaterTuning.HallCistern : b.T == "cistern" ? WaterTuning.CisternCap : 0;
        public static int StoreReachOf(Building b) => b.T == "hall" ? WaterTuning.HallReach : WaterTuning.CisternReach;

        public WaterSource SourceFor(Building b)
        {
            if (b.T == "hall") return new WaterSource { B = b, X = b.X, Y = b.Y, Sz = b.Sz, Pump = WaterTuning.HallSpring, Reach = WaterTuning.HallReach };
            if (b.T != "well") return null;
            int a = AquiferAt(b.X, b.Y);
            return new WaterSource { B = b, X = b.X, Y = b.Y, Sz = b.Sz, Aquifer = a, Pump = a >= 0 ? WaterTuning.WellPump : WaterTuning.WellSeep, Reach = WaterTuning.WellReach };
        }

        /// <summary>A well that is not built yet, for the placement preview.</summary>
        public WaterSource PlannedWell(int x, int y) =>
            new WaterSource { X = x, Y = y, Sz = 1, Aquifer = AquiferAt(x, y), Pump = AquiferAt(x, y) >= 0 ? WaterTuning.WellPump : 0, Reach = WaterTuning.WellReach };

        /// <summary>Road tiles within reach of a footprint, walking only Hall-connected roads that are not closed, and the homes and
        /// stores beside them (or touching the footprint itself). conn: the connected-road map (Recalc's, or D.Conn).</summary>
        public WaterReach ReachFrom(int fx, int fy, int sz, int reach, bool[] conn = null, Func<int, int, bool> closed = null)
        {
            conn = conn ?? D.Conn;
            var r = new WaterReach();
            var dist = new Dictionary<int, int>(); var q = new Queue<int>();
            for (int j = fy; j < fy + sz; j++)
                for (int i = fx; i < fx + sz; i++)
                    for (int n = 0; n < 4; n++)
                    {
                        int x = i + Nb[n, 0], y = j + Nb[n, 1];
                        if (!Inb(x, y) || (x >= fx && x < fx + sz && y >= fy && y < fy + sz)) continue;
                        int k = Tk(x, y);
                        if (!conn[k] || (closed != null && closed(x, y)) || dist.ContainsKey(k)) continue;
                        dist[k] = 1; q.Enqueue(k);
                    }
            while (q.Count > 0)
            {
                int k = q.Dequeue(), d = dist[k];
                r.Roads.Add(k);
                if (d >= reach) continue;
                int x = k % N, y = k / N;
                for (int n = 0; n < 4; n++)
                {
                    int i = x + Nb[n, 0], j = y + Nb[n, 1];
                    if (!Inb(i, j)) continue;
                    int kk = Tk(i, j);
                    if (!conn[kk] || (closed != null && closed(i, j)) || dist.ContainsKey(kk)) continue;
                    dist[kk] = d + 1; q.Enqueue(kk);
                }
            }
            foreach (var b in Blds)
            {
                if (!IsHome(b) && !IsStore(b)) continue;
                if (!Touches(b, r.Roads, fx, fy, sz)) continue;
                if (IsHome(b)) r.Homes.Add(b);
                if (IsStore(b)) r.Stores.Add(b);
            }
            return r;
        }

        static bool Touches(Building b, HashSet<int> roads, int fx, int fy, int sz)
        {
            // the building itself, or one beside the footprint, or beside a reached road tile
            if (b.X < fx + sz && fx < b.X + b.Sz && b.Y < fy + sz && fy < b.Y + b.Sz) return true;
            foreach (var (x, y) in b.Tiles())
                for (int n = 0; n < 4; n++)
                {
                    int i = x + Nb[n, 0], j = y + Nb[n, 1];
                    if (!Inb(i, j)) continue;
                    if (roads.Contains(Tk(i, j))) return true;
                    if (i >= fx && i < fx + sz && j >= fy && j < fy + sz) return true;
                }
            return false;
        }

        /// <summary>Homes any source or store reaches (Recalc: level 2 needs water).</summary>
        HashSet<Building> WaterCover(bool[] conn)
        {
            var cover = new HashSet<Building>();
            foreach (var b in Blds)
            {
                int reach = b.T == "well" ? WaterTuning.WellReach : IsStore(b) ? StoreReachOf(b) : -1;
                if (reach < 0) continue;
                foreach (var h in ReachFrom(b.X, b.Y, b.Sz, reach, conn).Homes) cover.Add(h);
            }
            return cover;
        }

        // ---------- The daily plan ----------
        /// <summary>Work out one day of water without changing anything. closed: road tiles water cannot pass (a shut gate, later);
        /// extra: a well that is only being previewed.</summary>
        public WaterPlan PlanWater(Func<int, int, bool> closed = null, WaterSource extra = null)
        {
            var p = new WaterPlan();
            int civ = Math.Max(1, D?.Civ ?? HallCap);
            double occ = Math.Min(1, Peasants / (double)civ);
            var homes = Blds.Where(IsHome).ToList();
            foreach (var h in homes)
            {
                // a house with no road to the Hall houses nobody (Recalc), so it drinks nothing; it is still marked NoRoad below
                double need = h.T == "hall" ? HallCap * occ * WaterTuning.PerHead : h.Served ? h.Housing * occ * WaterTuning.PerHead : 0;
                p.Need[h] = need; p.Got[h] = 0; p.Stocked[h] = 0; p.Demand += need;
            }
            foreach (var b in Blds) { var s = SourceFor(b); if (s != null) p.Sources.Add(s); }
            if (extra != null) p.Sources.Add(extra);
            foreach (var s in p.Sources) { p.SourceReach[s] = ReachFrom(s.X, s.Y, s.Sz, s.Reach, null, closed); p.Pumped[s] = 0; }
            var stores = Blds.Where(IsStore).ToList();
            var level = new Dictionary<Building, double>();
            foreach (var st in stores)
            {
                level[st] = Math.Max(0, Math.Min(StoreCapOf(st), st.Water));
                p.StoreReach[st] = ReachFrom(st.X, st.Y, st.Sz, StoreReachOf(st), null, closed);
                p.StoreCap += StoreCapOf(st);
            }
            var budget = Aquifers.Select(a => a.Reserve + a.Recharge).ToArray();
            p.AqDraw = new double[Aquifers.Count]; p.AqLimited = new bool[Aquifers.Count];
            var rem = p.Sources.ToDictionary(s => s, s => s.Pump);

            double Avail(WaterSource s) => s.Aquifer >= 0 ? Math.Min(rem[s], budget[s.Aquifer]) : rem[s];
            double Take(WaterSource s, double want)
            {
                double got = Math.Min(want, Avail(s));
                if (s.Aquifer >= 0 && got < want - 1e-9 && rem[s] > got + 1e-9) p.AqLimited[s.Aquifer] = true;
                if (got <= 0) return 0;
                rem[s] -= got; p.Pumped[s] += got;
                if (s.Aquifer >= 0) { budget[s.Aquifer] -= got; p.AqDraw[s.Aquifer] += got; }
                return got;
            }
            // springs first, then the wells whose aquifers are fullest: groundwater is the last resort
            IEnumerable<WaterSource> Order(IEnumerable<WaterSource> list) => list
                .OrderBy(s => s.Spring ? 0 : 1)
                .ThenByDescending(s => s.Aquifer >= 0 && Aquifers[s.Aquifer].Cap > 0 ? Aquifers[s.Aquifer].Reserve / Aquifers[s.Aquifer].Cap : 1)
                .ThenByDescending(s => rem[s]);

            var reachHome = homes.ToDictionary(h => h, h => p.Sources.Where(s => p.SourceReach[s].Homes.Contains(h)).ToList());
            // homes with the fewest sources first, so a well shared by two quarters serves the one that has nothing else
            foreach (var h in homes.OrderBy(h => reachHome[h].Count).ThenBy(h => h.Y).ThenBy(h => h.X))
            {
                double need = p.Need[h];
                foreach (var s in Order(reachHome[h]))
                {
                    if (need <= 1e-9) break;
                    double got = Take(s, need); need -= got; p.Got[h] += got; p.Delivered += got;
                }
            }
            // stored water covers what the sources could not, where it reaches
            foreach (var h in homes)
            {
                double need = p.Need[h] - p.Got[h];
                if (need <= 1e-9) continue;
                foreach (var st in stores.Where(st => p.StoreReach[st].Homes.Contains(h)).OrderByDescending(st => level[st]))
                {
                    if (need <= 1e-9) break;
                    double got = Math.Min(need, level[st]); level[st] -= got; need -= got; p.Stocked[h] += got; p.FromStore += got;
                }
            }
            // then the sources fill the cisterns they reach (only as much as there is room for)
            foreach (var st in stores)
                foreach (var s in Order(p.Sources.Where(s => p.SourceReach[s].Stores.Contains(st))))
                {
                    double room = StoreCapOf(st) - level[st];
                    if (room <= 1e-9) break;
                    double got = Take(s, room); level[st] += got; p.StoreFill += got;
                }
            foreach (var st in stores) { p.StoreAfter[st] = level[st]; p.Stored += level[st]; }
            foreach (var h in homes)
            {
                bool covered = p.Sources.Any(s => p.SourceReach[s].Homes.Contains(h)) || stores.Any(st => p.StoreReach[st].Homes.Contains(h));
                p.Why[h] = h.T != "hall" && !h.Served ? WaterWhy.NoRoad
                    : p.Need[h] - p.Got[h] - p.Stocked[h] <= 1e-6 ? WaterWhy.Ok : !covered ? WaterWhy.TooFar : WaterWhy.NotEnough;
            }
            return p;
        }

        /// <summary>The last day's water, for the HUD (null before the first day).</summary>
        public WaterPlan LastWater { get; private set; }
        bool _warnedShort, _warnedStore;
        readonly HashSet<int> _warnedDry = new HashSet<int>();

        /// <summary>Run one day of water and apply it: stores, aquifers, homes, warnings. Returns the plan.</summary>
        public WaterPlan StepWater()
        {
            var p = PlanWater();
            foreach (var kv in p.StoreAfter) kv.Key.Water = kv.Value;
            for (int i = 0; i < Aquifers.Count; i++)
            {
                var a = Aquifers[i];
                a.Draw = p.AqDraw[i]; a.Limited = p.AqLimited[i];
                a.Reserve = Math.Max(0, Math.Min(a.Cap, a.Reserve + a.Recharge - a.Draw));
                if (a.Reserve <= 1e-6 && a.Limited) { if (_warnedDry.Add(a.Id)) Events.Add($"An aquifer has run dry: its wells now give only what it recharges, {a.Recharge:0} water a day. Build a cistern or wells on another patch."); }
                else if (a.Reserve > a.Cap * 0.25) _warnedDry.Remove(a.Id);
            }
            foreach (var b in Blds) b.Pumped = 0;
            foreach (var kv in p.Pumped) if (kv.Key.B != null) kv.Key.B.Pumped = kv.Value;
            int newlyParched = 0;
            foreach (var h in p.Need.Keys)
            {
                h.WaterNeed = p.Need[h]; h.WaterGot = p.Got[h]; h.WaterStocked = p.Stocked[h]; h.WaterWhy = p.Why[h];
                if (p.Need[h] - p.Got[h] - p.Stocked[h] <= 1e-6) h.ShortDays = 0;   // nobody thirsty (an empty or cut-off home counts as fine)
                else { h.ShortDays++; if (h.ShortDays == WaterTuning.Grace && h.T == "house") newlyParched++; }
            }
            int shortHomes = p.ShortHomes;
            if (newlyParched > 0) Events.Add($"Residents are leaving {newlyParched} home{(newlyParched == 1 ? "" : "s")} that went {WaterTuning.Grace} days without enough water.");
            else if (shortHomes > 0 && !_warnedShort)
                Events.Add($"{shortHomes} home{(shortHomes == 1 ? " is" : "s are")} short of water ({WhyText(MainReason(p))}). Homes stop growing; residents leave after {WaterTuning.Grace} days without water.");
            _warnedShort = shortHomes > 0;
            if (p.FromStore > 1e-6 && p.StoreDays is double days && days < WaterTuning.StoreWarnDays)
            {
                if (!_warnedStore) Events.Add($"The wells cannot keep up: stored water covers the shortfall for about {Math.Floor(days)} more days.");
                _warnedStore = true;
            }
            else if (p.FromStore <= 1e-6) _warnedStore = false;
            LastWater = p;
            return p;
        }

        static WaterWhy MainReason(WaterPlan p) =>
            p.Why.Where(kv => p.IsShort(kv.Key)).Select(kv => kv.Value).GroupBy(w => w).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).FirstOrDefault();

        public static string WhyText(WaterWhy w) => w switch
        {
            WaterWhy.NoRoad => "no road to the Town Hall",
            WaterWhy.TooFar => "too far along the roads from any well or cistern",
            WaterWhy.NotEnough => "the wells there cannot pump enough",
            _ => "watered",
        };

        // ---------- Previews ----------
        public sealed class WellForecast
        {
            public int Aquifer = -1;
            public double MaxPump, ToHomes, ToStorage, AqDraw, AqRecharge, AqReserve, AqCap;
            public string Trend;
            public double? DaysLeft;
            public WaterReach Reach;
            public int HomesGaining;
        }

        /// <summary>What a well at x,y would do today: its pumping limit, the extra water the town would actually receive (to homes and
        /// into storage), and what the draw would do to its aquifer.</summary>
        public WellForecast ForecastWell(int x, int y)
        {
            var well = PlannedWell(x, y);
            var before = PlanWater(); var after = PlanWater(null, well);
            var f = new WellForecast { Aquifer = well.Aquifer, MaxPump = well.Pump, Reach = after.SourceReach[well] };
            f.ToHomes = Math.Max(0, after.Delivered + after.FromStore - before.Delivered - before.FromStore);
            f.ToStorage = Math.Max(0, after.Stored - before.Stored);
            foreach (var h in before.Need.Keys) if (after.Got[h] + after.Stocked[h] > before.Got[h] + before.Stocked[h] + 1e-6) f.HomesGaining++;
            if (well.Aquifer >= 0)
            {
                var a = Aquifers[well.Aquifer];
                f.AqDraw = after.AqDraw[a.Id]; f.AqRecharge = a.Recharge; f.AqReserve = a.Reserve; f.AqCap = a.Cap;
                f.Trend = Trend(a.Reserve, a.Cap, a.Recharge, f.AqDraw, after.AqLimited[a.Id]);
                f.DaysLeft = DaysLeft(a.Reserve, a.Recharge, f.AqDraw);
            }
            return f;
        }

        public sealed class ClosureForecast
        {
            public readonly List<Building> HomesLosingService = new List<Building>();
            public double GroundwaterBefore, GroundwaterAfter, ShortfallAfter, StoredAfter, StoreDrawAfter;
            /// <summary>How long the stored water lasts covering what it can reach of the shortfall that follows the closure.</summary>
            public double? StoreDays => StoreDrawAfter > 1e-6 ? StoredAfter / StoreDrawAfter : (double?)null;
        }

        /// <summary>For the future gate preview: which homes lose water if these road tiles are closed, how groundwater output changes
        /// and how long stored water lasts against the shortfall that follows. Changes nothing.</summary>
        public ClosureForecast ForecastClosure(Func<int, int, bool> closed)
        {
            var before = PlanWater(); var after = PlanWater(closed);
            var f = new ClosureForecast
            {
                GroundwaterBefore = before.GroundwaterDraw, GroundwaterAfter = after.GroundwaterDraw,
                ShortfallAfter = after.Shortfall, StoredAfter = before.Stored, StoreDrawAfter = after.FromStore,
            };
            foreach (var h in before.Need.Keys) if (after.Got[h] < before.Got[h] - 1e-6) f.HomesLosingService.Add(h);
            return f;
        }

        /// <summary>Total stored water and room (Town Hall cistern plus Cisterns).</summary>
        public (double stored, double cap) WaterStored()
        {
            double s = 0, c = 0;
            foreach (var b in Blds) if (IsStore(b)) { s += Math.Min(b.Water, StoreCapOf(b)); c += StoreCapOf(b); }
            return (s, c);
        }
    }
}
