// Tunnel mining (Unity-only; the browser prototype still mines surface ore tiles). Design: DESIGN_TERRAIN.md section 3 and Zach's
// rework: a mine digs a TUNNEL, tile by tile, into the Mining zone you paint, and you can see it growing.
//
//   Zone      paint Mining ground that touches the mine (its walls are where the tunnel starts). The tunnel only runs through
//             zoned tiles joined to the mine. Hills and mountains are the point: ore lies in them, not in the lowland.
//   Digging   miners dig the next tunnel tile for TileDigDays worker-days. The tunnel is drawn as dark tiles (brown timber
//             where shored). Each tile dug reveals the ore around it (RevealR tiles); the miners head for known ore, else uphill.
//   Ore       hidden deposits (generated from the seed, in bands 1..Bands under each hill tile; hills and mountains only, mountains
//             richest). Miners take ore from the walls: the dug tile and its neighbours (WallR). When the face has no ore left
//             to pick, the tunnel moves on. Longer tunnels mean longer trips underground.
//   Supports  each tunnel tile is shored with TileWood timber as soon as the store has it. With "require supports" on, miners
//             wait for timber instead of working an unshored tunnel.
//   Cave-ins  each worker-day underground rolls CaveInChance: base x tunnel length x how much of it is unshored x crowding. A
//             cave-in closes the mine for RepairDays, smashes the supports at the far end and can kill a miner.
//   A mine's entrance still faces away from the hill and needs its road in front (MineFaceAt, EntranceServed).
using System;
using System.Collections.Generic;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int Bands = 3;
        /// <summary>Ore a miner brings up per trip from band 1..3 (index 0 unused).</summary>
        public static readonly int[] MineLoad = { 0, 4, 6, 8 };
        /// <summary>Days underground per ore trip: TripBase + the band + 2 per tile the face is from the mine.</summary>
        public const int TripBase = 5;
        /// <summary>Worker-days to dig one tunnel tile, and the timber to shore it.</summary>
        public const int TileDigDays = 6, TileWood = 2;
        /// <summary>Most ore a tile can hold in band 1..3.</summary>
        public static readonly int[] LevelCapMax = { 0, 30, 50, 80 };
        public const int DigStint = 3, RepairDays = 20;
        /// <summary>Digging a tile reveals the deposits within this many tiles (2 = a 5x5 patch).</summary>
        public const int RevealR = 2;
        /// <summary>Miners pick ore from the dug tile and from tiles this close to it (1 = the walls around it).</summary>
        public const int WallR = 1;
        public const double CaveBase = 0.0008, CrowdStep = 0.5, ShoredFactor = 0.15, LengthStep = 0.1;
        public const double KillChanceShored = 0.25, KillChanceUnshored = 0.6;
        /// <summary>Multiplies every cave-in chance (1 in play; tests raise it to force a cave-in).</summary>
        public double CaveInRiskScale = 1;

        public bool RequireMineSupports { get; private set; }

        public void SetMineSupportsRequired(bool required) => RequireMineSupports = required;

        readonly short[][] _oreCap = new short[Bands + 1][];
        float[] _genLevel;                    // tile heights when the deposits were laid down (mining geometry never changes)
        public readonly Dictionary<int, double> OreTaken = new Dictionary<int, double>();   // key: band * N * N + y * N + x
        /// <summary>Tiles whose ore is known (revealed by digging beside them). Saves from before tunnels keep what was surveyed.</summary>
        public readonly bool[] Surveyed = new bool[N * N];
        public int MinerDeaths, CaveIns;
        /// <summary>Messages for the player (cave-ins, tunnel milestones, empty surveys). The game shows and clears them.</summary>
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
        /// <summary>Ore the player knows about at a tile and band: 0 until a tunnel has been dug beside it.</summary>
        public double KnownOre(int x, int y, int band) => Surveyed[Tk(x, y)] ? OreAt(x, y, band) : 0;
        public bool HasKnownOre(int x, int y) { for (int l = 1; l <= Bands; l++) if (KnownOre(x, y, l) > 1e-9) return true; return false; }

        // ---------- Tunnels ----------
        readonly HashSet<int> _tunnelTiles = new HashSet<int>(); int _tunnelVersion = -1;
        void RefreshTunnelTiles()
        {
            if (_tunnelVersion == Version) return;
            _tunnelVersion = Version; _tunnelTiles.Clear();
            foreach (var b in Blds) if (b.T == "mine") foreach (int k in b.Tunnel) _tunnelTiles.Add(k);
        }
        /// <summary>Has a mine dug this tile? (A tunnel is solid ground to everything else: nothing is built or quarried on it.)</summary>
        public bool IsTunnel(int x, int y) { if (!Inb(x, y)) return false; RefreshTunnelTiles(); return _tunnelTiles.Count > 0 && _tunnelTiles.Contains(Tk(x, y)); }
        public bool IsShored(Building b, int x, int y) => b.Shored.Contains(Tk(x, y));
        static int MCheb(int x, int y, int ax, int ay) => Math.Max(Math.Abs(x - ax), Math.Abs(y - ay));

        bool Diggable(Building b, int x, int y)
        {
            if (!Inb(x, y) || ZoneMap[y, x] != Zone.Mine || Grid[y, x] != null || !Claimed(x, y) || IsTunnel(x, y)) return false;
            if (x < b.X - WorkR || x > b.X + b.Sz - 1 + WorkR || y < b.Y - WorkR || y > b.Y + b.Sz - 1 + WorkR) return false;
            var d = DecoMap[y, x];
            return d != Deco.Tree && d != Deco.Sapling;
        }

        static readonly int[,] Nb4 = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        /// <summary>Every tile the tunnel could be driven to next: zoned and diggable, touching the mine or its tunnel.</summary>
        void Frontier(Building b, List<int> into)
        {
            into.Clear();
            var seen = new HashSet<int>();
            void Around(int cx, int cy)
            {
                for (int k = 0; k < 4; k++)
                {
                    int x = cx + Nb4[k, 0], y = cy + Nb4[k, 1];
                    if (Diggable(b, x, y) && seen.Add(Tk(x, y))) into.Add(Tk(x, y));
                }
            }
            foreach (var (x, y) in b.Tiles()) Around(x, y);
            foreach (int t in b.Tunnel) Around(t % N, t / N);
        }

        readonly List<int> _front = new List<int>();

        /// <summary>The tile to dig next: next to known ore if there is any, else heading uphill, then nearest the mine. -1 = none.</summary>
        int NextDigTile(Building b)
        {
            Frontier(b, _front);
            int best = -1; double bs = double.MinValue;
            foreach (int k in _front)
            {
                int x = k % N, y = k / N;
                double s = _genLevel[k] * 3 - MCheb(x, y, b.X, b.Y) * 0.5;
                int nearest = 99;
                for (int j = y - 3; j <= y + 3; j++)
                    for (int i = x - 3; i <= x + 3; i++)
                        if (Inb(i, j) && HasKnownOre(i, j)) nearest = Math.Min(nearest, MCheb(i, j, x, y));
                if (nearest <= 3) s += (4 - nearest) * 30;
                if (s > bs + 1e-9) { bs = s; best = k; }
            }
            return best;
        }

        /// <summary>How many tiles a mine could work: zoned ground its tunnel could still reach, plus tunnel faces with ore to pick.
        /// (Sets how many jobs it needs.)</summary>
        int MineWorkTiles(Building b)
        {
            int n = 0;
            Frontier(b, _front);
            var seen = new HashSet<int>(_front); var queue = new Queue<int>(_front);
            while (queue.Count > 0)
            {
                int k = queue.Dequeue(); n++;
                for (int d = 0; d < 4; d++)
                {
                    int x = k % N + Nb4[d, 0], y = k / N + Nb4[d, 1];
                    if (Diggable(b, x, y) && seen.Add(Tk(x, y))) queue.Enqueue(Tk(x, y));
                }
            }
            return n + OreTilesNearTunnel(b);
        }

        int OreTilesNearTunnel(Building b)
        {
            if (b.Tunnel.Count == 0) return 0;
            var seen = new HashSet<int>(); int n = 0;
            foreach (int t in b.Tunnel)
                for (int j = t / N - WallR; j <= t / N + WallR; j++)
                    for (int i = t % N - WallR; i <= t % N + WallR; i++)
                        if (Inb(i, j) && Claimed(i, j) && HasKnownOre(i, j) && seen.Add(Tk(i, j))) n++;
            return n;
        }

        /// <summary>The best ore the tunnel's walls offer right now: the richest band, then the most ore, nearer trips first.</summary>
        bool PickOre(Building b, out int tx, out int ty, out int band)
        {
            tx = ty = band = -1; double best = double.MinValue;
            foreach (int t in b.Tunnel)
                for (int j = t / N - WallR; j <= t / N + WallR; j++)
                    for (int i = t % N - WallR; i <= t % N + WallR; i++)
                    {
                        if (!Inb(i, j) || !Claimed(i, j)) continue;
                        for (int l = Bands; l >= 1; l--)
                        {
                            double o = KnownOre(i, j, l); if (o <= 1e-9) continue;
                            double score = l * 1000 + o - 30 * MCheb(i, j, b.X, b.Y);
                            if (score > best) { best = score; tx = i; ty = j; band = l; }
                        }
                    }
            return band > 0;
        }

        bool TookOreInReach(Building b)
        {
            foreach (int t in b.Tunnel)
                for (int j = t / N - WallR; j <= t / N + WallR; j++)
                    for (int i = t % N - WallR; i <= t % N + WallR; i++)
                        if (Inb(i, j)) for (int l = 1; l <= Bands; l++) if (OreTaken.ContainsKey(OreKey(l, Tk(i, j)))) return true;
            return false;
        }

        /// <summary>With "require supports" on, miners work only a fully shored tunnel.</summary>
        bool MineSafe(Building b) => !RequireMineSupports || b.Shored.Count >= b.Tunnel.Count;

        /// <summary>Chance that one worker-day underground ends in a cave-in: longer tunnels and unshored stretches are worse, and so is
        /// a crowd.</summary>
        public double CaveInChance(Building b, int underground)
        {
            int len = Math.Max(1, b.Tunnel.Count), shored = Math.Min(len, b.Shored.Count);
            double exposure = ((len - shored) + ShoredFactor * shored) / len;
            double crowd = 1 + CrowdStep * Math.Max(0, underground - 1);
            return CaveBase * (1 + LengthStep * len) * exposure * crowd * CaveInRiskScale;
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

        // ---------- The mine's entrance ----------
        /// <summary>
        /// Which way a mine at (x, y) opens (world face 0 = +y, 1 = -x, 2 = -y, 3 = +x): straight away from the high ground in its reach,
        /// so the tunnel runs into the hill and the mouth opens downhill. -1 when there is no high ground nearby (the door then faces
        /// the road like any building's).
        /// </summary>
        public int MineFaceAt(int x, int y, int size = 1)
        {
            double ux = 0, uy = 0, h0 = _genLevel[Tk(x, y)];
            double cx = x + (size - 1) * 0.5, cy = y + (size - 1) * 0.5;
            for (int j = y - FaceR; j <= y + size - 1 + FaceR; j++)
                for (int i = x - FaceR; i <= x + size - 1 + FaceR; i++)
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

        // ---------- The mine's crew ----------
        void StepMine(Building b, List<Worker> crew, Dictionary<Res, double> got)
        {
            if (b.Closed > 0) b.Closed--;
            // shore up what has been dug as soon as there is wood: the stretch nearest the mine first
            if (b.Served && b.Shored.Count < b.Tunnel.Count && ResAmt[Res.Wood] >= TileWood)
            {
                int pick = -1;
                foreach (int t in b.Tunnel)
                    if (!b.Shored.Contains(t) && (pick < 0 || MCheb(t % N, t / N, b.X, b.Y) < MCheb(pick % N, pick / N, b.X, b.Y))) pick = t;
                if (pick >= 0) { ResAmt[Res.Wood] -= TileWood; b.Shored.Add(pick); Touch(); }
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
                        if (!b.Served || b.Closed > 0 || !MineSafe(b)) break;
                        if (ResAmt[Res.Ore] + got[Res.Ore] >= Storage) break;   // full stockpile: the ore stays in the ground
                        // 1. pick ore from the walls of the tunnel
                        if (PickOre(b, out a.Tx, out a.Ty, out int band))
                        {
                            int steps = Math.Max(1, MCheb(a.Tx, a.Ty, b.X, b.Y));
                            a.St = CrewState.Down; a.Dig = false; a.Level = band; a.T = TripBase + band + 2 * steps;
                            break;
                        }
                        // 2. the face is clean: drive the tunnel on
                        if (NextDigTile(b) >= 0) { a.St = CrewState.Down; a.Dig = true; a.Level = 0; a.T = DigStint; }
                        break;
                    }
                    case CrewState.Down:
                    {
                        if (!MineSafe(b) && !a.Dig) { a.St = CrewState.Home; break; }
                        if (Noise.Hash2(Tk(b.X, b.Y), Day * 16 + ai, Seed + 4001) < CaveInChance(b, down))
                        {
                            if (CaveIn(b, crew, ai)) ai--;   // the miner died and left the crew
                            break;
                        }
                        if (a.Dig && ++b.DigProg >= TileDigDays)
                        {
                            b.DigProg = 0;
                            int next = NextDigTile(b);
                            if (next >= 0) DigTile(b, next);
                        }
                        if (--a.T > 0) break;
                        if (!a.Dig) got[Res.Ore] += TakeOre(a.Tx, a.Ty, a.Level, MineLoad[a.Level]);
                        a.St = CrewState.Home;
                        break;
                    }
                    default:   // runtime-only crews never hold the old surveying states any more
                        a.St = CrewState.Home; a.Path.Clear(); a.Stand = -1; a.X = b.X; a.Y = b.Y;
                        break;
                }
            }
        }

        void DigTile(Building b, int k)
        {
            b.Tunnel.Add(k);
            int x = k % N, y = k / N;
            for (int j = y - RevealR; j <= y + RevealR; j++)
                for (int i = x - RevealR; i <= x + RevealR; i++)
                    if (Inb(i, j)) Surveyed[Tk(i, j)] = true;
            Touch();
            int n = b.Tunnel.Count;
            if (n == 1 || n % 5 == 0) Events.Add($"The {b.Name} at {b.X},{b.Y} has dug its tunnel {n} tile{(n == 1 ? "" : "s")} in.");
            if (!HasOreInReach(b) && NextDigTile(b) < 0 && n >= 3 && !TookOreInReach(b))
                Events.Add($"The {b.Name} at {b.X},{b.Y} has dug all it can reach and found no ore.");
        }

        /// <summary>Is there ore the tunnel's walls could still give?</summary>
        bool HasOreInReach(Building b) => OreTilesNearTunnel(b) > 0;

        /// <summary>A cave-in where worker ai works: the mine closes, everyone underground comes up, the supports at the far end are
        /// smashed, and the miner may die. Returns true when the worker died (and was removed from the crew).</summary>
        bool CaveIn(Building b, List<Worker> crew, int ai)
        {
            bool shored = b.Tunnel.Count > 0 && b.Shored.Count >= b.Tunnel.Count;
            CaveIns++;
            b.Closed = RepairDays; b.CaveIns++;
            // the three shored tiles farthest from the mine lose their timber
            for (int n = 0; n < 3 && b.Shored.Count > 0; n++)
            {
                int far = -1;
                foreach (int t in b.Shored) if (far < 0 || MCheb(t % N, t / N, b.X, b.Y) > MCheb(far % N, far / N, b.X, b.Y)) far = t;
                b.Shored.Remove(far);
            }
            foreach (var o in crew) if (o.St == CrewState.Down) { o.St = CrewState.Home; o.X = b.X; o.Y = b.Y; }
            bool dead = Noise.Hash2(Tk(b.X, b.Y), Day * 16 + ai, Seed + 4002) < (shored ? KillChanceShored : KillChanceUnshored);
            if (dead)
            {
                crew.RemoveAt(ai);
                if (Peasants > 0) Peasants--;
                MinerDeaths++;
            }
            Events.Add($"Cave-in in the tunnel of the {b.Name} ({b.X},{b.Y}){(shored ? "" : ", unshored")}! " +
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
            int len = b.Tunnel.Count;
            string s = pre + (len == 0 ? "Tunnel: not started" : $"Tunnel: {len} tile{(len == 1 ? "" : "s")} dug, {Math.Min(len, b.Shored.Count)} shored");
            if (len > b.Shored.Count) s += $" (each needs {TileWood} wood)";
            Frontier(b, _front);
            bool canDig = _front.Count > 0;
            int oreTiles = OreTilesNearTunnel(b);
            if (b.Closed > 0) s += $"\nClosed for repairs after a cave-in: {b.Closed} days left";
            else if (!MineSafe(b)) s += "\nWaiting for timber: supports are required before miners work the tunnel";
            else if (oreTiles > 0) s += $"\nMining ore from the tunnel walls ({oreTiles} tile{(oreTiles == 1 ? "" : "s")} with ore beside it)";
            else if (canDig) s += len == 0 ? "\nDigging the first tunnel tile" : $"\nTunnel face is clean: driving on ({100 * b.DigProg / TileDigDays}% of the next tile)";
            if (!canDig && oreTiles == 0)
            {
                if (len == 0) s += "\nNothing to dig: zone Mining ground that touches the mine. The tunnel starts at its walls and runs through zoned tiles.";
                else s += TookOreInReach(b) ? "\nWorked out: no ore left within reach of this tunnel. Zone more high ground beside it, or build another mine"
                                            : "\nNo ore found in this mine's zone. Ore lies in hills and mountains: put the mine at the foot of a hill";
            }
            if (b.CaveIns > 0) s += $"\nCave-ins so far: {b.CaveIns}";
            return s;
        }
    }
}
