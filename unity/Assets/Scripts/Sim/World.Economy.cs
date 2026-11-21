// The daily tick, ported from src/sim_tick.js in the same task order:
//   sync -> farms + foresters -> crews -> townsfolk -> deliver logs -> sawing -> nature -> economy (food, gold, population, barracks) -> develop
// The walking work crews and townsfolk live in World.Crews.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public void Tick()
        {
            // sync
            var d = Recalc();
            var before = ResAmt.ToDictionary(kv => kv.Key, kv => kv.Value);
            int employed = d.Employed;
            double food = 0, wood = 0;
            var got = new Dictionary<Res, double> { { Res.Logs, 0 }, { Res.Stone, 0 }, { Res.Ore, 0 }, { Res.Food, 0 } };

            // farms + foresters
            var crops = SeasonalFarming ? WorkSeasonalFields() : FarmOutput();
            food += SeasonalFarming ? Math.Min(crops.food, Math.Max(0, CropStorage(Res.Food) - ResAmt[Res.Food])) : crops.food;
            ResAmt[Res.Flax] += SeasonalFarming ? Math.Min(crops.flax, Math.Max(0, CropStorage(Res.Flax) - ResAmt[Res.Flax])) : crops.flax;
            foreach (var b in Blds.Where(b => b.T == "forester" && b.Served).ToList())
            {
                if (b.Eff <= 0) continue;
                b.Prog += b.Eff;
                if (b.Prog >= PlantEvery) b.Prog = PlantSapling(b.X, b.Y, ForesterR) ? 0 : PlantEvery;
            }

            // crews, then deliver logs
            got[Res.Food] = food;
            StepCrews(got);
            food = got[Res.Food];
            StepFolk();
            ResAmt[Res.Logs] += got[Res.Logs];

            // sawing: sawmills at full yield, then the Town Hall pit-saw at half yield
            foreach (var b in Blds.Where(b => b.T == "sawmill" && b.Served))
                if (ResAmt[Res.Logs] > 0) { double c = Math.Min(ResAmt[Res.Logs], SawRate * b.Eff); ResAmt[Res.Logs] -= c; wood += c; }
            if (ResAmt[Res.Logs] > 0) { double c = Math.Min(ResAmt[Res.Logs], HallSaw); ResAmt[Res.Logs] -= c; wood += c * HallYield; }

            // nature: stumps rot, saplings grow
            foreach (var k in StumpT.Keys.ToList())
                if (--StumpT[k] <= 0) { StumpT.Remove(k); int y = k / N, x = k % N; if (DecoMap[y, x] == Deco.Stump) { DecoMap[y, x] = Deco.None; Touch(); } }
            foreach (var k in SapT.Keys.ToList())
                if (--SapT[k] <= 0) { SapT.Remove(k); int y = k / N, x = k % N; if (DecoMap[y, x] == Deco.Sapling) { DecoMap[y, x] = Deco.Tree; Touch(); } }

            // drinking water (World.Water.cs): wells pump, cisterns fill or cover shortfalls, homes count days without enough
            var water = StepWater();
            double watered = water.Demand > 1e-9 ? 1 - water.Unmet / water.Demand : 1;

            // economy
            double eat = DailyFoodDemand;
            ResAmt[Res.Food] += food - eat; ResAmt[Res.Wood] += wood; ResAmt[Res.Stone] += got[Res.Stone]; ResAmt[Res.Ore] += got[Res.Ore];
            ResAmt[Res.Gold] += employed * 0.12 - Soldiers * 0.1;
            Starving = false;
            if (ResAmt[Res.Food] <= 0)
            {
                ResAmt[Res.Food] = 0; Starving = true;
                if (Peasants > 0) Peasants--; else if (Soldiers > 0) Soldiers--;
            }
            else
            {
                double free = Math.Max(0, 1 - Peasants / (double)Math.Max(1, d.Civ));
                double births = Peasants * 0.04 * free * (ResAmt[Res.Food] > 40 ? 1 : 0.5) * watered;   // thirsty towns grow slower
                PopAcc += births - Peasants * 0.004 + (Peasants < 2 ? 0.05 : 0);
                while (PopAcc >= 1 && Peasants < d.Civ) { Peasants++; PopAcc -= 1; }
                if (PopAcc > 0.99) PopAcc = 0.99;
                while (PopAcc <= -1 && Peasants > 0) { Peasants--; PopAcc += 1; }
            }
            if (Peasants > d.Civ) Peasants--;
            if (Soldiers > d.Bunks) Soldiers--;
            foreach (var b in Blds.Where(b => b.T == "barracks" && b.Served))
            {
                b.Prog = Math.Min(4, b.Prog + 1);
                if (b.Prog >= 4 && Drafting && Soldiers < d.Bunks && Peasants >= 3 && ResAmt[Res.Wood] >= 2 && ResAmt[Res.Gold] >= 1 && ResAmt[Res.Ore] >= 1)
                {
                    Soldiers++; Peasants--; Trained++; ResAmt[Res.Wood] -= 2; ResAmt[Res.Gold] -= 1; ResAmt[Res.Ore] -= 1; b.Prog = 0;
                }
            }
            foreach (var k in ResAmt.Keys.ToList())
            {
                int limit = CropStorage(k);
                // Disconnecting storage prevents additions until the excess is used or sold.
                double available = k == Res.Food || k == Res.Flax ? Math.Max(limit, before[k]) : limit;
                ResAmt[k] = Math.Max(0, Math.Min(available, ResAmt[k]));
            }
            foreach (var k in ResAmt.Keys.ToList())
                Rate[k] = (k == Res.Logs || k == Res.Stone || k == Res.Ore) ? 0.85 * Rate[k] + 0.15 * (ResAmt[k] - before[k]) : ResAmt[k] - before[k];
            Day++;

            // develop: zones grow buildings
            Recalc();
            Develop();
            _statistics.Add(CaptureStatistics(Day - 1));
        }

        public (int x, int y)? NearestZoned(int x, int y, int r, Deco kind, Zone zone)
        {
            (int, int)? best = null; int bd = int.MaxValue;
            for (int j = y - r; j <= y + r; j++) for (int i = x - r; i <= x + r; i++)
                if (Inb(i, j) && DecoMap[j, i] == kind && ZoneMap[j, i] == zone && (kind != Deco.Rock || RockWorkable(i, j)))
                { int dd = (i - x) * (i - x) + (j - y) * (j - y); if (dd < bd) { bd = dd; best = (i, j); } }
            return best;
        }

        bool ForesterCovers(int x, int y) => Blds.Any(b => b.T == "forester" && Math.Max(Math.Abs(b.X - x), Math.Abs(b.Y - y)) <= ForesterR);

        void FellAt(int x, int y)
        {
            int k = Tk(x, y);
            DecoMap[y, x] = Deco.Stump;
            if (!ForesterCovers(x, y)) ZoneMap[y, x] = Zone.None;
            StumpT[k] = StumpDays; Chop.Remove(k);
            Touch();
        }

        bool PlantSapling(int x, int y, int r)
        {
            (int x, int y)? best = null; int bd = int.MaxValue;
            for (int j = y - r; j <= y + r; j++) for (int i = x - r; i <= x + r; i++)
            {
                if (!Inb(i, j) || ZoneMap[j, i] != Zone.Wood || Grid[j, i] != null || DecoMap[j, i] != Deco.None) continue;
                if (IsSolid(i, j) || TileSpread(i, j) >= 2) continue;
                int dd = (i - x) * (i - x) + (j - y) * (j - y); if (dd < bd) { bd = dd; best = (i, j); }
            }
            if (best == null) return false;
            DecoMap[best.Value.y, best.Value.x] = Deco.Sapling; SapT[Tk(best.Value.x, best.Value.y)] = SaplingDays; Touch();
            return true;
        }

        double MineRock(int x, int y, int r, double amount)
        {
            double got = 0;
            for (int n = 0; n < 4 && amount > 1e-9; n++)
            {
                (int x, int y)? best = null; int bd = int.MaxValue;
                for (int j = y - r; j <= y + r; j++) for (int i = x - r; i <= x + r; i++)
                    if (Inb(i, j) && ZoneMap[j, i] == Zone.Mine && RockWorkable(i, j)) { int dd = (i - x) * (i - x) + (j - y) * (j - y); if (dd < bd) { bd = dd; best = (i, j); } }
                if (best == null) break;
                var (bx, by) = best.Value; int k = Tk(bx, by);
                double e = Math.Min(amount, RockLeft(bx, by));
                Dig[k] = DigDone(bx, by) + e; got += e; amount -= e;
                if (RockLeft(bx, by) <= 1e-9) { Dig[k] = RockCap(bx, by); ZoneMap[by, bx] = Zone.None; }
                Touch();
            }
            return got;
        }
    }
}
