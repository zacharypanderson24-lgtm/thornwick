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
        bool _sawHinted;   // the one-time hint about unsawn logs (runtime only)
        double _wheat;
        public double WheatStored => Math.Clamp(_wheat, 0, Math.Max(0, ResAmt[Res.Food]));
        public double? RecentFoodProduction { get; private set; }

        // Food stays interchangeable for gameplay. When food is eaten or stolen, remove the same share of each source.
        double WheatShare(double amount) => ResAmt[Res.Food] > 0 ? WheatStored * Math.Clamp(amount / ResAmt[Res.Food], 0, 1) : 0;

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
            if (CropYield != 1.0) crops = (crops.food * CropYield, crops.flax * CropYield);   // a failed harvest (World.Scenarios.cs)
            food += SeasonalFarming ? Math.Min(crops.food, Math.Max(0, CropStorage(Res.Food) - ResAmt[Res.Food])) : crops.food;
            double wheat = food;
            ResAmt[Res.Flax] += SeasonalFarming ? Math.Min(crops.flax, Math.Max(0, CropStorage(Res.Flax) - ResAmt[Res.Flax])) : crops.flax;
            foreach (var b in Blds.Where(b => b.T == "forester" && b.Served).ToList())
            {
                if (b.Eff <= 0) continue;
                b.Prog += b.Eff;
                if (b.Prog >= PlantEvery) b.Prog = PlantSapling(b) ? 0 : PlantEvery;
            }

            // crews, then deliver logs
            got[Res.Food] = food;
            StepCrews(got);
            food = got[Res.Food];
            StepFolk();
            StepGates();   // World.Walls.cs: shut or open the gates, mend walls
            StepRaid();
            StepCampRaids();
            StepSettlements();
            StepExpeditions();
            ResAmt[Res.Logs] += got[Res.Logs];
            BurnCharcoal();

            // sawing: sawmills at full yield, then the Town Hall pit-saw at half yield
            foreach (var b in Blds.Where(b => b.T == "sawmill" && b.Served))
                if (ResAmt[Res.Logs] > 0) { double c = Math.Min(ResAmt[Res.Logs], SawRate * b.Eff); ResAmt[Res.Logs] -= c; wood += c; }
            if (ResAmt[Res.Logs] > 0) { double c = Math.Min(ResAmt[Res.Logs], HallSaw); ResAmt[Res.Logs] -= c; wood += c * HallYield; }

            if (!_sawHinted && ResAmt[Res.Logs] >= 30 && !Blds.Any(s => s.T == "sawmill" && s.Served))
            {
                _sawHinted = true;
                Events.Add("Logs are piling up. Build a Sawmill (Wood menu): it turns logs into the wood you build with, the Town Hall saws only 1 a day at half yield.");
            }

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
            _freshMeat = FreshMeatStored + Math.Max(0, food - wheat);
            _wheat = WheatStored + wheat;
            ResAmt[Res.Food] += food;
            var eggs = WorkLivestock();
            ResAmt[Res.Eggs] += eggs;
            double foodEquivalent = food + eggs / (double)EggsPerFood;
            RecentFoodProduction = RecentFoodProduction is double recent ? (6 * recent + foodEquivalent) / 7 : foodEquivalent;
            PreserveMeat();
            double fromEggs = Math.Min(ResAmt[Res.Eggs] / EggsPerFood, Math.Max(0, eat - ResAmt[Res.Food]));
            ResAmt[Res.Eggs] -= fromEggs * EggsPerFood;
            eat -= fromEggs;
            double fromReserve = Math.Min(ResAmt[Res.SmokedMeat], Math.Max(0, eat - ResAmt[Res.Food]));
            ResAmt[Res.SmokedMeat] -= fromReserve;
            eat -= fromReserve;
            bool reserveCoveredMeal = (fromEggs > 0 || fromReserve > 0) && eat <= ResAmt[Res.Food];
            _freshMeat = FreshMeatStored - FreshMeatShare(eat);
            _wheat -= WheatShare(eat);
            ResAmt[Res.Food] -= eat; ResAmt[Res.Wood] += wood; ResAmt[Res.Stone] += got[Res.Stone]; ResAmt[Res.Ore] += got[Res.Ore];
            // Newly delivered ore is available to road-linked smiths on the same day.
            foreach (var b in Blds.Where(b => b.T == "smithery" && b.Served))
            {
                double room = CropStorage(Res.Metal) - ResAmt[Res.Metal];
                if (room <= 0 || ResAmt[Res.Ore] <= 0) break;
                double metal = Math.Min(Math.Min(room, SmeltRate * b.Eff), ResAmt[Res.Ore] / OrePerMetal);
                double boost = Math.Min(Math.Min(room - metal, b.Eff), Math.Min(ResAmt[Res.Charcoal], ResAmt[Res.Ore] / OrePerMetal - metal));
                ResAmt[Res.Charcoal] -= boost;
                metal += boost;
                ResAmt[Res.Ore] -= metal * OrePerMetal;
                ResAmt[Res.Metal] += metal;
            }
            int returning = TroopsReturning;   // World.Conquest.cs: soldiers away still draw wages; those coming home need a bunk and a home here
            ResAmt[Res.Gold] += employed * 0.12 - (Soldiers + TroopsAway) * 0.1;
            Starving = false;
            if (ResAmt[Res.Food] <= 0 && !reserveCoveredMeal)
            {
                ResAmt[Res.Food] = 0; Starving = true;
                if (Peasants > 0) Peasants--; else LoseSoldier();
            }
            else
            {
                double free = Math.Max(0, 1 - (Peasants + Soldiers) / (double)Math.Max(1, d.Civ));
                double births = Peasants * 0.04 * free * (ResAmt[Res.Food] > 40 * FoodScale ? 1 : 0.5) * watered;   // thirsty towns grow slower
                PopAcc += births - Peasants * 0.004 + (Peasants < 2 ? 0.05 : 0);
                while (PopAcc >= 1 && Peasants + Soldiers + returning < d.Civ) { Peasants++; PopAcc -= 1; }
                if (PopAcc > 0.99) PopAcc = 0.99;
                while (PopAcc <= -1 && Peasants > 0) { Peasants--; PopAcc += 1; }
            }
            ApplyDehydration(water);
            if (Peasants + Soldiers > d.Civ) { if (Peasants > 0) Peasants--; else LoseSoldier(); }   // a standing army takes places in the population: its soldiers are not refilled by births
            if (Soldiers > d.Bunks) LoseSoldier();
            foreach (var b in Blds.Where(b => b.T == "barracks" && b.Served))
            {
                b.Prog = Math.Min(TrainDays, b.Prog + 1);
                if (b.Prog >= TrainDays && Drafting && Soldiers + returning < d.Bunks && Peasants >= 3 && CanAfford(NextRecruit))
                {
                    TrainOne(NextRecruit); b.Prog = 0;
                }
            }
            int haul = Blds.Where(b => b.T == "storehouse" && b.Served).Sum(b => b.Workers) * HaulPerWorker;
            double hauling = haul;
            foreach (var k in ResAmt.Keys.ToList())
            {
                int limit = CropStorage(k);
                // Disconnecting storage prevents additions until the excess is used or sold.
                double available = k == Res.Food || k == Res.Flax ? Math.Max(limit, before[k]) : limit;
                if (StoredByHaulers(k))
                {
                    double existing = Math.Max(Storage, before[k]);
                    double moved = Math.Min(hauling, Math.Max(0, Math.Min(limit, ResAmt[k]) - existing));
                    available = existing + moved;
                    hauling -= moved;
                }
                if (k == Res.Food)
                {
                    double lost = Math.Max(0, ResAmt[k] - available);
                    _freshMeat = FreshMeatStored - FreshMeatShare(lost);
                    _wheat = WheatStored - WheatShare(lost);
                }
                ResAmt[k] = Math.Max(0, Math.Min(available, ResAmt[k]));
            }
            foreach (var k in ResAmt.Keys.ToList())
                Rate[k] = (k == Res.Logs || k == Res.Stone || k == Res.Ore) ? 0.85 * Rate[k] + 0.15 * (ResAmt[k] - before[k]) : ResAmt[k] - before[k];
            Day++;

            // develop: zones grow buildings
            Recalc();
            Develop();
            _statistics.Add(CaptureStatistics(Day - 1));
            MendHall(); StepCampaign(); StepIncidents(); CheckDefeat();   // World.Defeat.cs, World.Campaign.cs
        }

        public (int x, int y)? NearestZoned(int x, int y, int r, Deco kind, Zone zone)
        {
            (int, int)? best = null; int bd = int.MaxValue;
            for (int j = y - r; j <= y + r; j++) for (int i = x - r; i <= x + r; i++)
                if (Inb(i, j) && DecoMap[j, i] == kind && ZoneMap[j, i] == zone && (kind != Deco.Rock || RockWorkable(i, j)))
                { int dd = (i - x) * (i - x) + (j - y) * (j - y); if (dd < bd) { bd = dd; best = (i, j); } }
            return best;
        }

        bool ForesterCovers(int x, int y) => Blds.Any(b => b.T == "forester" && x >= b.X - ForesterR && x < b.X + b.Sz + ForesterR &&
            y >= b.Y - ForesterR && y < b.Y + b.Sz + ForesterR);

        void FellAt(int x, int y)
        {
            int k = Tk(x, y);
            DecoMap[y, x] = Deco.Stump;
            if (!ForesterCovers(x, y)) ZoneMap[y, x] = Zone.None;
            StumpT[k] = StumpDays; Chop.Remove(k);
            Touch();
        }

        bool PlantSapling(Building b)
        {
            int x = b.X, y = b.Y, r = ForesterR;
            (int x, int y)? best = null; int bd = int.MaxValue;
            for (int j = y - r; j < y + b.Sz + r; j++) for (int i = x - r; i < x + b.Sz + r; i++)
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
