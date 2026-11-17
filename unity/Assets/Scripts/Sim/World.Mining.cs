// Underground mining (Unity-only; the browser prototype still mines surface ore tiles). Design: DESIGN_TERRAIN.md section 3 and
// Zach's mining decisions: ore lives in a hidden underground layer, mines dig to reach it (deeper = richer), cave-ins are a real
// risk unless timber supports are supplied, and crowding raises the risk. Choices made 2026-11-16:
//   - miners survey Mining zones (no prospector building); a cave-in closes the mine and can kill a miner;
//   - (rework) ore lies in hills and mountains, not lowland; mines at the foot of high ground drive an ADIT (a level tunnel) into it,
//     which is cheaper and safer than a shaft; quarries and mines share a mountain (stone from its face, ore from inside);
//     surface showings sit on hill flanks over real ore.
//
//   Deposits  every tile holds iron in BANDS 1..Bands, counted down from that tile's own surface (band 1 just under it, band 3
//             deepest, and richest). Only hills and mountains hold any; mountains most. Generated once from the seed and the
//             starting terrain; only what has been surveyed (Surveyed) and taken (OreTaken) is state. The heights used for mining
//             are frozen at generation (_genLevel), so quarrying a hill or mountain face does not move or destroy its ore.
//   Reaching  a mine works Mining-zone tiles within WorkR. A band lies `rise` levels above the mine's floor when its tile stands
//             higher: NeedShaft = band - rise (floored at 0). 0 means the band is level with or above the floor and is reached
//             through the mine's adit; otherwise the shaft must reach that depth (at most MaxDepth). So a mine at a mountain's foot
//             reaches every band through its adit, and lowland would need shafts (but holds no ore).
//   Work      miners survey zone tiles first (walking, WorkT days each, reveals all bands of a 3x3 patch), then dig what is needed (the adit first,
//             else the shaft one level deeper), then go underground and bring up loads by band (MineLoad: richer bands, bigger loads).
//   Supports  the adit and each shaft level are shored with wood (AditWood, SupportWood) as soon as the store has it.
//   Cave-ins  each worker-day underground rolls CaveInChance: base x (shaft depth, or AditRisk in the adit) x crowding, x ShoredFactor
//             where shored. A cave-in closes the mine for RepairDays, smashes the supports where it happened and can kill the miner.
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int Bands = 3, MaxDepth = 3;
        /// <summary>Ore a miner brings up per trip from band 1..3 (index 0 unused). Band 1 matches the old surface mine load.</summary>
        public static readonly int[] MineLoad = { 0, 4, 6, 8 };
        /// <summary>Days underground per trip, by band: about 0.5 / 0.6 / 0.67 ore per worker-day.</summary>
        public static readonly int[] MineTripDays = { 0, 8, 10, 12 };
        /// <summary>Worker-days to dig the shaft down to level 1..3; the adit is a one-off AditWork.</summary>
        public static readonly int[] DigWork = { 0, 12, 24, 40 };
        public const int AditWork = 10;
        /// <summary>Wood to shore shaft level 1..3; the adit takes AditWood.</summary>
        public static readonly int[] SupportWood = { 0, 6, 10, 16 };
        public const int AditWood = 6;
        /// <summary>Most ore a tile can hold in band 1..3.</summary>
        public static readonly int[] LevelCapMax = { 0, 30, 50, 80 };
        public const int DigStint = 4, RepairDays = 20;
        /// <summary>A survey trip reveals the target tile and its neighbours within this Chebyshev radius (1 = a 3x3 patch).</summary>
        public const int SurveyR = 1;
        public const double CaveBase = 0.0008, CrowdStep = 0.5, ShoredFactor = 0.15, AditRisk = 0.5;
        public const double KillChanceShored = 0.25, KillChanceUnshored = 0.6;
        /// <summary>Multiplies every cave-in chance (1 in play; tests raise it to force a cave-in).</summary>
        public double CaveInRiskScale = 1;

        readonly short[][] _oreCap = new short[Bands + 1][];
        float[] _genLevel;                    // tile heights when the deposits were laid down (mining geometry never changes)
        public readonly Dictionary<int, double> OreTaken = new Dictionary<int, double>();   // key: band * N * N + y * N + x
        public readonly bool[] Surveyed = new bool[N * N];
        public int MinerDeaths, CaveIns;
        /// <summary>Messages for the player (cave-ins, new levels, empty surveys). The game shows and clears them.</summary>
        public readonly List<string> Events = new List<string>();

        // ---------- Geology ----------
        /// <summary>Lay down the deposits from vertex heights hv (the freshly generated terrain; a loaded save passes the seed's
        /// regenerated terrain). placeShowings: also move the surface showings onto the hills (new worlds only, never on load).</summary>
        void GenDeposits(int[] hv = null, bool placeShowings = true)
        {
            hv = hv ?? Hv; int M = N + 1;
            _genLevel = new float[N * N];
            for (int b = 1; b <= Bands; b++) _oreCap[b] = new short[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int k = Tk(x, y);
                    double lvl = (hv[y * M + x] + hv[y * M + x + 1] + hv[(y + 1) * M + x] + hv[(y + 1) * M + x + 1]) / 4.0;
                    _genLevel[k] = (float)lvl;
                    if (placeShowings && DecoMap[y, x] == Deco.Ore) DecoMap[y, x] = Deco.None;   // the browser's lowland showings; new ones are placed below
                    // ore needs high ground: none below level 0.75, rising to full strength on high hills, more again in mountains
                    double high = Math.Max(0, Math.Min(1, (lvl - 0.75) / 2.25)) + (lvl >= MountLevel ? 0.35 : 0);
                    if (high <= 0) continue;
                    double vein = Noise.VNoise(x * 0.3 + 90, y * 0.3 + 90, Seed + 13);
                    double f1 = high * (0.55 + vein * 0.6) - 0.55;
                    double f2 = high * (0.45 + Noise.VNoise(x * 0.22 + 40, y * 0.22 + 40, Seed + 71) * 0.5 + vein * 0.3) - 0.45;
                    double f3 = high * (0.40 + Noise.VNoise(x * 0.15 + 7, y * 0.15 + 7, Seed + 97) * 0.5 + vein * 0.3) - 0.38;
                    _oreCap[1][k] = Cap(f1, 0.6, 1); _oreCap[2][k] = Cap(f2, 0.6, 2); _oreCap[3][k] = Cap(f3, 0.65, 3);
                }
            // surface showings: open hill tiles over a strong top band, thinned so they are hints, not a carpet
            if (placeShowings) for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int k = Tk(x, y);
                    if (DecoMap[y, x] != Deco.None || IsMountain(x, y) || _genLevel[k] < 1 || TileSpread(x, y) >= 3) continue;
                    if (Math.Max(Math.Abs(x - Start), Math.Abs(y - Start)) < 7) continue;
                    if (_oreCap[1][k] >= 12 && Noise.Hash2(x, y, Seed + 7) > 0.55) DecoMap[y, x] = Deco.Ore;
                }
        }
        static short Cap(double f, double span, int band) => f <= 0 ? (short)0 : (short)Math.Max(4, Math.Min(LevelCapMax[band], Math.Round(f / span * LevelCapMax[band])));

        static int OreKey(int band, int k) => band * N * N + k;
        /// <summary>Ore still in the ground at a tile and band (whether or not anyone knows it).</summary>
        public double OreAt(int x, int y, int band)
        {
            int k = Tk(x, y), cap = _oreCap[band][k];
            return cap <= 0 ? 0 : cap - (OreTaken.TryGetValue(OreKey(band, k), out var t) ? t : 0);
        }
        /// <summary>The tile's height when the map was made (what mining uses; quarrying does not change it).</summary>
        public double GenLevel(int x, int y) => _genLevel[Tk(x, y)];
        public bool IsSurveyed(int x, int y) => Surveyed[Tk(x, y)];
        /// <summary>Ore the player knows about at a tile and band: 0 until the tile is surveyed.</summary>
        public double KnownOre(int x, int y, int band) => Surveyed[Tk(x, y)] ? OreAt(x, y, band) : 0;
        public bool HasKnownOre(int x, int y) { for (int l = 1; l <= Bands; l++) if (KnownOre(x, y, l) > 1e-9) return true; return false; }

        /// <summary>How far down the mine's shaft must go to reach this band of this tile: 0 = through the adit (the band lies level
        /// with or above the mine's floor because the tile stands higher), else the shaft level needed (may exceed MaxDepth).</summary>
        public int NeedShaft(Building b, int x, int y, int band)
        {
            int rise = (int)Math.Floor(_genLevel[Tk(x, y)] - _genLevel[Tk(b.X, b.Y)] + 1e-9);
            return Math.Max(0, band - Math.Max(0, rise));
        }
        bool Reachable(Building b, int need) => need == 0 ? b.Adit : need <= b.Depth;
        /// <summary>Are the supports in place where miners work at this need (0 = the adit, else that shaft level)?</summary>
        public bool ShoredFor(Building b, int need) => need == 0 ? b.AditShored : need <= b.Shored;

        /// <summary>Chance that one worker-day underground ends in a cave-in, where `need` is 0 (adit) or the shaft level worked.</summary>
        public double CaveInChance(Building b, int need, int underground)
        {
            double crowd = 1 + CrowdStep * Math.Max(0, underground - 1);
            return CaveBase * (need == 0 ? AditRisk : need) * crowd * (ShoredFor(b, need) ? ShoredFactor : 1) * CaveInRiskScale;
        }

        // ---------- The mine's entrance ----------
        /// <summary>
        /// Which way a mine at (x, y) opens (world face 0 = +y, 1 = -x, 2 = -y, 3 = +x): straight away from the high ground in its reach,
        /// so the adit runs into the hill and the mouth opens downhill. -1 when there is no high ground nearby (the door then faces
        /// the road like any building's).
        /// </summary>
        public int MineFaceAt(int x, int y, int size = 1)
        {
            double ux = 0, uy = 0, h0 = _genLevel[Tk(x, y)];
            double cx = x + (size - 1) * 0.5, cy = y + (size - 1) * 0.5;
            for (int j = y - WorkR; j <= y + size - 1 + WorkR; j++)
                for (int i = x - WorkR; i <= x + size - 1 + WorkR; i++)
                {
                    if (!Inb(i, j) || (i >= x && i < x + size && j >= y && j < y + size)) continue;
                    double rise = _genLevel[Tk(i, j)] - h0; if (rise <= 0) continue;
                    double d = Noise.Hypot(i - cx, j - cy);
                    ux += rise * (i - cx) / d; uy += rise * (j - cy) / d;   // points uphill
                }
            if (ux * ux + uy * uy < 0.25) return -1;
            int best = -1; double bd = double.MinValue;
            for (int f = 0; f < 4; f++) { double dot = -(FaceDirs[f, 0] * ux + FaceDirs[f, 1] * uy); if (dot > bd + 1e-9) { bd = dot; best = f; } }
            return best;
        }

        /// <summary>The tile in front of a mine's entrance.</summary>
        public static (int x, int y) EntranceTile(int x, int y, int face) => (x + FaceDirs[face, 0], y + FaceDirs[face, 1]);

        /// <summary>A mine works only with a Hall-connected road (or the Hall) right in front of its entrance.</summary>
        bool EntranceServed(Building mine, int face, bool[] conn)
        {
            foreach (var (x, y) in mine.Tiles())
            {
                var (ex, ey) = EntranceTile(x, y, face);
                if (!Inb(ex, ey) || Grid[ey, ex] == mine) continue;
                if (Grid[ey, ex] == Hall || (Grid[ey, ex]?.T == "road" && conn[Tk(ex, ey)])) return true;
            }
            return false;
        }

        // ---------- The mine's reach ----------
        bool InMineReach(int x, int y) => Inb(x, y) && ZoneMap[y, x] == Zone.Mine && Claimed(x, y);

        /// <summary>The best ore the mine can work right now: the richest band, then the most ore. False when nothing is reachable.</summary>
        bool PickOre(Building b, out int tx, out int ty, out int band, out int need)
        {
            tx = ty = band = need = -1; double best = 0;
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                {
                    if (!InMineReach(i, j)) continue;
                    for (int l = Bands; l >= 1; l--)
                    {
                        double o = KnownOre(i, j, l); if (o <= 1e-9) continue;
                        int nd = NeedShaft(b, i, j, l); if (!Reachable(b, nd)) continue;
                        double score = l * 1000 + o;
                        if (score > best) { best = score; tx = i; ty = j; band = l; need = nd; }
                    }
                }
            return band > 0;
        }

        /// <summary>What to dig next towards known ore: 0 = the adit, 1..MaxDepth = that shaft level, -1 = nothing worth digging.</summary>
        int NextDig(Building b)
        {
            bool adit = false; int shaft = int.MaxValue;
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                {
                    if (!InMineReach(i, j)) continue;
                    for (int l = 1; l <= Bands; l++)
                    {
                        if (KnownOre(i, j, l) <= 1e-9) continue;
                        int nd = NeedShaft(b, i, j, l);
                        if (nd == 0) { if (!b.Adit) adit = true; }
                        else if (nd > b.Depth && nd <= MaxDepth) shaft = Math.Min(shaft, nd);
                    }
                }
            return adit ? 0 : shaft != int.MaxValue ? b.Depth + 1 : -1;
        }

        int UnsurveyedInReach(Building b)
        {
            int n = 0;
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                    if (InMineReach(i, j) && !Surveyed[Tk(i, j)]) n++;
            return n;
        }
        bool HasOreInReach(Building b)
        {
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                    if (InMineReach(i, j) && HasKnownOre(i, j)) return true;
            return false;
        }
        bool TookOreInReach(Building b)
        {
            for (int j = b.Y - WorkR; j <= b.Y + b.Sz - 1 + WorkR; j++)
                for (int i = b.X - WorkR; i <= b.X + b.Sz - 1 + WorkR; i++)
                    if (InMineReach(i, j)) for (int l = 1; l <= Bands; l++) if (OreTaken.ContainsKey(OreKey(l, Tk(i, j)))) return true;
            return false;
        }

        /// <summary>Take up to `amount` ore from one band of one tile.</summary>
        double TakeOre(int x, int y, int band, double amount)
        {
            double e = Math.Min(amount, OreAt(x, y, band)); if (e <= 1e-9) return 0;
            int key = OreKey(band, Tk(x, y));
            OreTaken[key] = (OreTaken.TryGetValue(key, out var t) ? t : 0) + e;
            Touch();
            return e;
        }

        // ---------- The mine's crew ----------
        void StepMine(Building b, List<Worker> crew, Dictionary<Res, double> got)
        {
            if (b.Closed > 0) b.Closed--;
            // shore up what has been dug as soon as there is wood: the adit first, then the shaft level by level
            if (b.Served)
            {
                if (b.Adit && !b.AditShored && ResAmt[Res.Wood] >= AditWood) { ResAmt[Res.Wood] -= AditWood; b.AditShored = true; }
                else if (b.Shored < b.Depth && ResAmt[Res.Wood] >= SupportWood[b.Shored + 1]) { ResAmt[Res.Wood] -= SupportWood[b.Shored + 1]; b.Shored++; }
            }
            int down = 0; foreach (var o in crew) if (o.St == CrewState.Down) down++;   // crowding counts who started the day underground

            for (int ai = 0; ai < crew.Count; ai++)
            {
                var a = crew[ai];
                a.Px = a.X; a.Py = a.Y;
                switch (a.St)
                {
                    case CrewState.Home:
                    {
                        if (!b.Served || b.Closed > 0) break;
                        // 1. survey the zone first, so the crew knows what lies inside
                        _used.Clear();
                        foreach (var o in crew) if (o != a && o.St != CrewState.Home && o.Stand >= 0) _used.Add(o.Stand);
                        if (CrewTarget(b, Deco.None, ai * 7 + Day, _used, a.Path, out a.Tx, out a.Ty, survey: true))
                        {
                            a.Survey = true; a.Stand = a.Path[a.Path.Count - 1]; a.I = 0; a.Carry = 0;
                            if (a.Path.Count > 1) { a.I = 1; SetPos(a, a.Path[1]); }
                            a.St = a.I == a.Path.Count - 1 ? CrewState.Work : CrewState.Out; if (a.St == CrewState.Work) a.T = WorkT;
                            break;
                        }
                        if (ResAmt[Res.Ore] + got[Res.Ore] >= Storage) break;   // full stockpile: the ore stays in the ground
                        // 2. bring up the richest ore the adit or shaft reaches
                        if (PickOre(b, out a.Tx, out a.Ty, out int band, out int need))
                        {
                            a.St = CrewState.Down; a.Dig = false; a.Level = band; a.Need = need; a.T = MineTripDays[band];
                            break;
                        }
                        // 3. otherwise dig towards known ore: the adit into the high ground first, else the shaft one level deeper
                        int dig = NextDig(b);
                        if (dig >= 0) { a.St = CrewState.Down; a.Dig = true; a.Need = dig; a.Level = 0; a.T = DigStint; }
                        break;
                    }
                    case CrewState.Down:
                    {
                        // diggers driving the adit work its face (adit risk); shaft diggers work the level being sunk
                        if (Noise.Hash2(Tk(b.X, b.Y), Day * 16 + ai, Seed + 4001) < CaveInChance(b, a.Need, down))
                        {
                            if (CaveIn(b, crew, ai)) ai--;   // the miner died and left the crew
                            break;
                        }
                        if (a.Dig)
                        {
                            if (a.Need == 0 && !b.Adit && ++b.AditProg >= AditWork)
                            {
                                b.Adit = true; b.AditProg = 0; Touch();
                                Events.Add($"The {b.Name} at {b.X},{b.Y} has driven its adit into the high ground.");
                            }
                            else if (a.Need > 0 && b.Depth < a.Need && ++b.DigProg >= DigWork[a.Need])
                            {
                                b.Depth = a.Need; b.DigProg = 0; Touch();
                                Events.Add($"The {b.Name} at {b.X},{b.Y} has sunk its shaft to level {b.Depth}.");
                            }
                        }
                        if (--a.T > 0) break;
                        if (!a.Dig) got[Res.Ore] += TakeOre(a.Tx, a.Ty, a.Level, MineLoad[a.Level]);
                        a.St = CrewState.Home;
                        break;
                    }
                    case CrewState.Out:
                        a.I++; SetPos(a, a.Path[a.I]);
                        if (a.I == a.Path.Count - 1) { a.St = CrewState.Work; a.T = WorkT; }
                        break;
                    case CrewState.Work:
                        if (--a.T <= 0)
                        {
                            // a survey examines the ground around the target too: the 3x3 patch centred on it
                            bool fresh = false;
                            for (int dj = -SurveyR; dj <= SurveyR; dj++)
                                for (int di = -SurveyR; di <= SurveyR; di++)
                                {
                                    int sx = a.Tx + di, sy = a.Ty + dj;
                                    if (Inb(sx, sy) && !Surveyed[Tk(sx, sy)]) { Surveyed[Tk(sx, sy)] = true; fresh = true; }
                                }
                            if (fresh)
                            {
                                Touch();
                                if (UnsurveyedInReach(b) == 0 && !HasOreInReach(b))
                                    Events.Add($"The {b.Name} at {b.X},{b.Y} has surveyed its whole zone and found no ore.");
                            }
                            a.Path.Reverse(); a.I = 0; a.St = CrewState.Back;
                        }
                        break;
                    default:   // back from a survey
                        if (a.I < a.Path.Count - 1) { a.I++; SetPos(a, a.Path[a.I]); }
                        break;
                }
                if (a.St == CrewState.Back && a.I >= a.Path.Count - 1)
                {
                    a.St = CrewState.Home; a.Survey = false; a.Path.Clear(); a.Stand = -1; a.X = b.X; a.Y = b.Y;
                }
            }
        }

        /// <summary>A cave-in where worker ai works: the mine closes, everyone underground comes up, the supports there are smashed,
        /// and the miner may die. Returns true when the worker died (and was removed from the crew).</summary>
        bool CaveIn(Building b, List<Worker> crew, int ai)
        {
            var a = crew[ai]; int need = a.Need; bool shored = ShoredFor(b, need);
            CaveIns++;
            b.Closed = RepairDays; b.CaveIns++;
            if (need == 0) b.AditShored = false; else if (shored) b.Shored = need - 1;
            foreach (var o in crew) if (o.St == CrewState.Down) { o.St = CrewState.Home; o.X = b.X; o.Y = b.Y; }
            bool dead = Noise.Hash2(Tk(b.X, b.Y), Day * 16 + ai, Seed + 4002) < (shored ? KillChanceShored : KillChanceUnshored);
            if (dead)
            {
                crew.RemoveAt(ai);
                if (Peasants > 0) Peasants--;
                MinerDeaths++;
            }
            string where = need == 0 ? "in the adit" : $"at shaft level {need}";
            Events.Add($"Cave-in at the {b.Name} ({b.X},{b.Y}) {where}{(shored ? "" : ", unshored")}! " +
                       (dead ? "A miner was killed. " : "The miners escaped. ") + $"The mine is closed for {RepairDays} days of repairs.");
            Touch();
            return dead;
        }

        /// <summary>What a mine is doing, for the info panel.</summary>
        public string MineStatus(Building b)
        {
            string pre = "";
            int mf = MineFaceAt(b.X, b.Y, b.Sz);
            if (mf >= 0 && !b.Served)
            {
                var (ex, ey) = EntranceTile(b.X + (mf == 3 ? b.Sz - 1 : 0), b.Y + (mf == 0 ? b.Sz - 1 : 0), mf);
                pre = $"Needs a road to the Town Hall along its entrance edge (at {ex},{ey}, facing away from the hill)\n";
            }
            string s = pre + (b.Adit ? "Adit: driven into the high ground" + (b.AditShored ? ", shored" : $", UNSHORED (needs {AditWood} wood)")
                     : b.AditProg > 0 ? $"Adit: {100 * b.AditProg / AditWork}% driven" : "Adit: none");
            s += b.Depth > 0 ? $"\nShaft: level {b.Depth} of {MaxDepth}, {b.Shored} shored" + (b.Shored < b.Depth ? $" (level {b.Shored + 1} needs {SupportWood[b.Shored + 1]} wood)" : "")
                             : "\nShaft: none";
            if (b.Depth < MaxDepth && b.DigProg > 0) s += $". Sinking to level {b.Depth + 1}: {100 * b.DigProg / DigWork[b.Depth + 1]}%";
            if (b.Closed > 0) s += $"\nClosed for repairs after a cave-in: {b.Closed} days left";
            int unsurveyed = UnsurveyedInReach(b);
            if (unsurveyed > 0) s += $"\n{unsurveyed} zone tiles still to survey";
            else if (!HasOreInReach(b))
                s += TookOreInReach(b) ? "\nWorked out: no ore left in this mine's zone. Zone fresh high ground nearby or remove the mine"
                                       : "\nNo ore found in this mine's zone. Ore lies in hills and mountains";
            if (b.CaveIns > 0) s += $"\nCave-ins so far: {b.CaveIns}";
            return s;
        }
    }
}
