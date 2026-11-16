// Zoning demand and automatic development, ported from src/sim_develop.js. Same eligibility, same scoring (including the
// per-day hash noise) and the same tie-breaks (lowest score, then lowest y*N+x). The browser keeps per-chunk candidate caches
// to avoid scanning the map; here the scan is direct, which gives identical picks and is cheap at 128x128.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public static readonly Zone[] ZoneKinds = { Zone.Res, Zone.Farm, Zone.Comm, Zone.Wood, Zone.Mine };
        public const double DemandThreshold = 0.12;

        /// <summary>Demand per zone kind, -1..1 (JS: S.dem).</summary>
        public readonly Dictionary<Zone, double> Dem = ZoneKinds.ToDictionary(k => k, k => 0.0);
        /// <summary>Why a zone kind is not growing right now (JS: S.note).</summary>
        public readonly Dictionary<Zone, string> Note = ZoneKinds.ToDictionary(k => k, k => "");

        static double Clamp1(double v) => Math.Max(-1, Math.Min(1, v));

        public void ComputeDemand()
        {
            var d = D;
            double occ = d.Civ > 0 ? Peasants / (double)d.Civ : 1;
            double eat = Math.Max(1, (Peasants + Soldiers * 1.5) * 0.2);
            double prod = 0;
            foreach (var b in Blds) if (b.T == "farm" && b.Served) prod += b.Food * b.Eff;
            bool staffed = d.Jobs <= Peasants + 4;
            double food = ResAmt[Res.Food];
            double res = (Starving || food < 10) ? -0.3 : Clamp1((occ - 0.7) * 4);
            if (Rate[Res.Food] < -0.3 && food < 120) res = Math.Min(res, 0);
            double farm = Clamp1((1.25 - prod / eat) * 3 + (food < 40 ? 0.3 : 0) - (food > 250 ? 0.5 : 0));
            if (Starving) farm = 1;
            double comm = d.HouseCap < 8 ? -0.2 : Clamp1((1 - d.Cov) * 1.6 - 0.15);
            double wood = Clamp1((120 - ResAmt[Res.Wood] - ResAmt[Res.Logs] * 0.7) / 120);
            double mine = Clamp1(Math.Max((60 - ResAmt[Res.Stone]) / 60, (30 - ResAmt[Res.Ore]) / 30));
            if (!staffed) { farm = Math.Min(farm, 0); wood = Math.Min(wood, 0); mine = Math.Min(mine, 0); }
            Dem[Zone.Res] = res; Dem[Zone.Farm] = farm; Dem[Zone.Comm] = comm; Dem[Zone.Wood] = wood; Dem[Zone.Mine] = mine;
        }

        bool ZoneServed(int x, int y)
        {
            for (int k = 0; k < 4; k++)
            {
                int a = x + Nb[k, 0], b = y + Nb[k, 1];
                if (Inb(a, b) && (D.Conn[Tk(a, b)] || Grid[b, a] == Hall)) return true;
            }
            return false;
        }

        bool LotOk(Zone kind, int x, int y, int sz)
        {
            for (int j = 0; j < sz; j++) for (int i = 0; i < sz; i++)
            {
                int a = x + i, c = y + j;
                if (!Inb(a, c) || ZoneMap[c, a] != kind || Grid[c, a] != null) return false;
                if (IsSolid(a, c) || HasTree(a, c)) return false;
                if (kind == Zone.Farm && !Fert[c, a]) return false;
            }
            return true;
        }

        bool LotServed(int x, int y, int sz)
        {
            for (int j = 0; j < sz; j++) for (int i = 0; i < sz; i++) for (int k = 0; k < 4; k++)
            {
                int nx = x + i + Nb[k, 0], ny = y + j + Nb[k, 1];
                if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                if (nx >= x && ny >= y && nx < x + sz && ny < y + sz) continue;
                if (D.Conn[Tk(nx, ny)] || Grid[ny, nx] == Hall) return true;
            }
            return false;
        }

        int CountZoned(int x, int y, int r, Deco kind, Zone zone)
        {
            int n = 0;
            for (int j = y - r; j <= y + r; j++) for (int i = x - r; i <= x + r; i++)
                if (Inb(i, j) && DecoMap[j, i] == kind && ZoneMap[j, i] == zone && (kind != Deco.Rock || RockWorkable(i, j))) n++;
            return n;
        }

        int CountDeco(int x, int y, int r, Deco kind)
        {
            int n = 0;
            for (int j = y - r; j <= y + r; j++) for (int i = x - r; i <= x + r; i++) if (Inb(i, j) && DecoMap[j, i] == kind) n++;
            return n;
        }

        public class Pick { public int X, Y, Sz; public string Type; public int Cost; public double Score; }

        static double ChebPt(Building b, double x, double y) => Math.Max(Math.Abs(b.X + b.Ctr - x), Math.Abs(b.Y + b.Ctr - y));

        /// <summary>The best lot or site for a zone kind, or why there is none. JS: choose.</summary>
        public (Pick pick, int wait) Choose(Zone kind)
        {
            var h = Hall;
            double Dist(double cx, double cy) => Math.Abs(cx - h.X) + Math.Abs(cy - h.Y);
            string ltype = kind == Zone.Res ? "house" : kind == Zone.Farm ? "farm" : kind == Zone.Comm ? "market" : null;
            if (ltype != null)
            {
                (int a, double score)? sBest = null, bBest = null;
                void Consider(int a, int sz, double score)
                {
                    var cur = sz == 2 ? bBest : sBest;
                    if (cur == null || score < cur.Value.score || (score == cur.Value.score && a < cur.Value.a))
                    { if (sz == 2) bBest = (a, score); else sBest = (a, score); }
                }
                List<Building> unc = null;
                if (kind == Zone.Comm) unc = Blds.Where(b => b.T == "house" && b.Served && !b.Covered).ToList();
                if (kind != Zone.Comm || unc.Count > 0)
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            if (ZoneMap[y, x] != kind || Grid[y, x] != null) continue;
                            for (int sz = 1; sz <= 2; sz++)
                            {
                                if (sz == 2 && (x + 1 >= N || y + 1 >= N)) continue;
                                if (!LotOk(kind, x, y, sz) || !LotServed(x, y, sz)) continue;
                                int a = Tk(x, y);
                                if (kind != Zone.Comm)
                                {
                                    double score = sz == 2 ? Dist(x + 0.5, y + 0.5) + Noise.Hash2(x, y, Day + 2) * 2.5
                                                           : Dist(x, y) + Noise.Hash2(x, y, Day + 1) * 2.5;
                                    Consider(a, sz, score);
                                }
                                else
                                {
                                    double cx = x + (sz - 1) / 2.0, cy = y + (sz - 1) / 2.0, R = sz == 2 ? 7 : 5;
                                    int gain = 0;
                                    foreach (var b in unc) if (ChebPt(b, cx, cy) <= R) gain += b.Capacity;
                                    if (gain <= 0) continue;
                                    Consider(a, sz, -gain * 2 + Dist(cx, cy) * 0.1);
                                }
                            }
                        }
                Pick Mk((int a, double score)? b, int sz) => b == null ? null
                    : new Pick { X = b.Value.a % N, Y = b.Value.a / N, Type = ltype, Sz = sz, Cost = CostOf(ltype, sz)[Res.Wood], Score = b.Value.score };
                var small = Mk(sBest, 1); var big = Mk(bBest, 2);
                if (small == null && big == null) return (null, 0);
                double wood = ResAmt[Res.Wood];
                bool bigOk = big != null && wood >= big.Cost, smallOk = small != null && wood >= small.Cost;
                bool preferBig = bigOk && (!smallOk || Noise.Hash2(Day, 77, Seed) < 0.7);
                if (preferBig) return (big, 0);
                if (smallOk) return (small, 0);
                return (null, Math.Min(small?.Cost ?? 999, big?.Cost ?? 999));
            }

            // wood and mine: one candidate per site tile; no daily noise
            (int a, int x, int y, string type, double score)? best = null;
            double needOre = (30 - ResAmt[Res.Ore]) / 30, needStone = (60 - ResAmt[Res.Stone]) / 60;
            var woods = Blds.Where(b => b.T == "wood").ToList(); var quarries = Blds.Where(b => b.T == "quarry").ToList(); var mines = Blds.Where(b => b.T == "mine").ToList();
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (ZoneMap[y, x] != kind || Grid[y, x] != null || !ZoneServed(x, y)) continue;
                    string type; int cnt;
                    if (kind == Zone.Wood)
                    {
                        cnt = CountZoned(x, y, WorkR, Deco.Tree, Zone.Wood);
                        if (cnt < 3) continue;
                        if (woods.Any(b => ChebPt(b, x, y) <= 2)) continue;
                        type = "wood";
                    }
                    else
                    {
                        int rk = CountZoned(x, y, WorkR, Deco.Rock, Zone.Mine), ok = CountDeco(x, y, WorkR, Deco.Ore);
                        if (rk < 2 && ok < 2) continue;
                        type = null; cnt = 0;
                        if (ok >= 2 && (rk < 2 || needOre >= needStone)) { type = "mine"; cnt = ok; }
                        else if (rk >= 2) { type = "quarry"; cnt = rk; }
                        if (type == null || (type == "mine" ? mines : quarries).Any(b => ChebPt(b, x, y) <= 2)) continue;
                    }
                    double score = -cnt * 2 + Dist(x, y) * 0.1;
                    int a = Tk(x, y);
                    if (best == null || score < best.Value.score || (score == best.Value.score && a < best.Value.a)) best = (a, x, y, type, score);
                }
            if (best == null) return (null, 0);
            var cand = new Pick { X = best.Value.x, Y = best.Value.y, Type = best.Value.type, Sz = 1, Cost = Cost[best.Value.type][Res.Wood], Score = best.Value.score };
            return ResAmt[Res.Wood] >= cand.Cost ? (cand, 0) : (null, cand.Cost);
        }

        static readonly Dictionary<Zone, string> IdleNote = new Dictionary<Zone, string>
        {
            { Zone.Res, "No demand yet. Homes grow once houses are nearly full and food is stocked." },
            { Zone.Farm, "No demand yet. Farms grow when food runs low." },
            { Zone.Comm, "No demand yet. Markets grow when homes have no market within 5 tiles." },
            { Zone.Wood, "No demand yet. Woodcutters grow when wood runs low." },
            { Zone.Mine, "No demand yet. Mines and quarries grow when stone or ore run low." },
        };
        static readonly Dictionary<Zone, string> NoSiteNote = new Dictionary<Zone, string>
        {
            { Zone.Res, "Needs empty zone tiles right beside a road that leads to the Town Hall." },
            { Zone.Farm, "Needs empty fertile zone tiles right beside a road that leads to the Town Hall." },
            { Zone.Comm, "Needs a road-linked commerce lot near homes that lack a market." },
            { Zone.Wood, "Needs a road-linked lumber tile with 3+ trees in range, and space from other woodcutters." },
            { Zone.Mine, "Needs a road-linked mining tile with 2+ rock or ore tiles in range." },
        };

        /// <summary>Grow at most one building per zone kind. JS: develop. Call after Recalc.</summary>
        public void Develop()
        {
            ComputeDemand();
            foreach (var kind in ZoneKinds)
            {
                Note[kind] = "";
                if (Dem[kind] <= DemandThreshold) { Note[kind] = IdleNote[kind]; continue; }
                var (p, wait) = Choose(kind);
                if (p == null) { Note[kind] = wait > 0 ? $"Waiting for {wait} wood to build." : NoSiteNote[kind]; continue; }
                ResAmt[Res.Wood] -= p.Cost;
                AddBld(p.Type, p.X, p.Y, p.Sz).Zoned = true;
                Recalc();
            }
        }
    }
}
