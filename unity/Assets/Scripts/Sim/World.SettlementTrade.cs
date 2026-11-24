// Trading with NPC settlements (Settlements.cs). A settlement that is willing (big enough and warm enough towards you, see
// Settlements.WillTrade) sells what it makes cheaply and buys what it needs at a good price, up to how much a place its size trades in a
// month. Prices move with its attitude. Goods travel by caravan at marching pace: when you buy you pay now and the goods arrive after the
// march; when you sell your goods leave now and the gold arrives after the march. Each road-connected Market runs one caravan at a time.
// A finished trade warms the settlement a little. A gift of gold (no market needed) warms a settlement that will not trade with you yet.
// Caravans on the road are saved.
using System;
using System.Collections.Generic;
using System.Linq;
using SettlementRules = CrownAndHearth.Sim.Settlements;

namespace CrownAndHearth.Sim
{
    public enum CaravanKind : byte { Buy, Sell, Gift }

    /// <summary>Goods or gold on the road between your settlement and an NPC settlement.</summary>
    public sealed class Caravan
    {
        public int SettlementId, Amount, Gold, ArriveDay;
        public CaravanKind Kind;
        public Res Res;
    }

    public partial class World
    {
        /// <summary>Most a caravan carries in one trip.</summary>
        public const int CaravanLoad = 100;
        /// <summary>A gift: this much gold warms a settlement by GiftWarmth, but gifts alone never warm it past GiftCap.</summary>
        public const int GiftGold = 20, GiftWarmth = 3, GiftCap = 50;
        /// <summary>Trading alone warms a settlement up to this attitude (more for a mercantile one per trade, not higher).</summary>
        public const int TradeWarmthCap = 60;

        public readonly List<Caravan> Caravans = new List<Caravan>();

        /// <summary>What a unit of each good is worth in gold, before a settlement's mark-up or discount.</summary>
        public static double BasePrice(Res r) => r switch
        {
            Res.Food => 0.2, Res.Logs => 1, Res.Wood => 2, Res.Stone => 2, Res.Flax => 2, Res.Ore => 3, Res.Wool => 3, Res.Metal => 8, _ => 2,
        };

        public int Markets => Blds.Count(b => b.T == "market" && b.Served);
        /// <summary>Trading caravans on the road (gifts go by messenger and do not count).</summary>
        public int TradeCaravansOut => Caravans.Count(c => c.Kind != CaravanKind.Gift);

        /// <summary>How much a settlement trades in a month, all its goods together: about one unit for every 8 people, from 10 up to 400.</summary>
        public static int MonthlyTrade(Settlement s) => (int)Math.Round(Math.Clamp((int)Math.Round(s.Population / 8), 10, 400) * (s.Devastated ? WasteFactor : 1));

        /// <summary>How much of one good it trades in a month: its share of the whole, a main good more than a side one (at least 5).</summary>
        public static int MonthlyTrade(Settlement s, Res r) => Math.Max(5, (int)Math.Round(MonthlyTrade(s) * s.ShareOf(r) * 1.6)) * TradeUnit(r);

        /// <summary>Grain is counted in food units, 5 for every unit of anything else (one villager eats a food a day), so a "unit" of grain is a fifth
        /// as big and a fifth the price (BasePrice), and caravans and monthly volumes carry five times as many.</summary>
        public static int TradeUnit(Res r) => r == Res.Food ? 5 : 1;

        /// <summary>Gold you pay for one unit of a good it produces: cheap where it is plentiful (80% of its worth, its main goods cheapest),
        /// cheaper the more it likes you.</summary>
        public static double PriceToBuy(Settlement s, Res r) => BasePrice(r) * 0.8 * (1.15 - 0.3 * s.ShareOf(r)) * (1 - s.Attitude / 400.0);
        /// <summary>Gold it pays you for one unit of a good it needs: dear where it is scarce (150% of its worth, its main needs dearest),
        /// more the more it likes you.</summary>
        public static double PriceToSell(Settlement s, Res r) => BasePrice(r) * 1.5 * (0.85 + 0.3 * s.ShareOf(r)) * (1 + s.Attitude / 400.0);
        public static double PriceToBuy(Settlement s) => PriceToBuy(s, s.Makes);
        public static double PriceToSell(Settlement s) => PriceToSell(s, s.Needs);

        public static int LeftToBuy(Settlement s, Res r) => s.Produces(r) ? Math.Max(0, MonthlyTrade(s, r) - s.SoldOf.GetValueOrDefault(r)) : 0;
        public static int LeftToSell(Settlement s, Res r) => s.Wanting(r) ? Math.Max(0, MonthlyTrade(s, r) - s.BoughtOf.GetValueOrDefault(r)) : 0;
        public static int LeftToBuy(Settlement s) => LeftToBuy(s, s.Makes);
        public static int LeftToSell(Settlement s) => LeftToSell(s, s.Needs);

        /// <summary>Whether a caravan can set out for this settlement now, and why not.</summary>
        public Result CanTrade(Settlement s)
        {
            if (s == null) return Result.No("Choose a settlement");
            if (s.Tier == SettlementTier.Hamlet) return Result.No($"{s.Name} is a hamlet: too small to have a market");
            if (!s.WillTrade) return Result.No($"{s.Name} will not trade with you yet: it needs to feel at least {SettlementRules.AttitudeName(SettlementRules.TradeThreshold(s.Personality))} towards you ({SettlementRules.TradeThreshold(s.Personality):+0;-0;0}). Gifts help.");
            if (Markets == 0) return Result.No("Build a Market (Trade, 7) beside a road: caravans set out from it");
            if (TradeCaravansOut >= Markets) return Result.No(Markets == 1 ? "Your market's caravan is already on the road. Build another market for a second caravan." : $"All {Markets} of your markets' caravans are on the road");
            return Result.Yes();
        }

        /// <summary>Buy a good it produces (its main good if none is named): pay now, the caravan brings it home after the march.</summary>
        public Result BuyFrom(Settlement s, int amount, Res? good = null)
        {
            var ok = CanTrade(s); if (!ok.Ok) return ok;
            var r = good ?? s.Makes;
            if (!s.Produces(r)) return Result.No($"{s.Name} does not produce {GoodName(r)}");
            amount = Math.Min(Math.Min(amount, CaravanLoad * TradeUnit(r)), LeftToBuy(s, r));
            if (amount <= 0) return Result.No($"{s.Name} has no more {GoodName(r)} to sell this month");
            int cost = (int)Math.Ceiling(amount * PriceToBuy(s, r));
            if (ResAmt[Res.Gold] < cost) return Result.No($"That costs {cost} gold; you have {ResAmt[Res.Gold]:0}");
            ResAmt[Res.Gold] -= cost;
            s.SoldThisMonth += amount; s.SoldOf[r] = s.SoldOf.GetValueOrDefault(r) + amount;
            Caravans.Add(new Caravan { SettlementId = s.Id, Kind = CaravanKind.Buy, Res = r, Amount = amount, Gold = cost, ArriveDay = Day + SettlementMarchDays(s) });
            return Result.Yes(amount);
        }

        /// <summary>Sell it a good it needs (its main need if none is named): it leaves now, the gold comes home after the march.</summary>
        public Result SellTo(Settlement s, int amount, Res? good = null)
        {
            var ok = CanTrade(s); if (!ok.Ok) return ok;
            var r = good ?? s.Needs;
            if (!s.Wanting(r)) return Result.No($"{s.Name} does not need {GoodName(r)}");
            amount = Math.Min(Math.Min(amount, CaravanLoad * TradeUnit(r)), LeftToSell(s, r));
            if (amount <= 0) return Result.No($"{s.Name} wants no more {GoodName(r)} this month");
            if (ResAmt[r] < amount) return Result.No($"You have only {ResAmt[r]:0} {GoodName(r)}");
            int gold = (int)Math.Floor(amount * PriceToSell(s, r));
            ResAmt[r] -= amount;
            s.BoughtThisMonth += amount; s.BoughtOf[r] = s.BoughtOf.GetValueOrDefault(r) + amount;
            Caravans.Add(new Caravan { SettlementId = s.Id, Kind = CaravanKind.Sell, Res = r, Amount = amount, Gold = gold, ArriveDay = Day + SettlementMarchDays(s) });
            return Result.Yes(amount);
        }

        /// <summary>Send a gift of gold by messenger: it warms the settlement when it arrives. Works whether or not it trades with you.</summary>
        public Result SendGift(Settlement s)
        {
            if (s == null) return Result.No("Choose a settlement");
            if (s.Attitude + PendingGiftWarmth(s) >= GiftCap) return Result.No($"Gifts alone will not win {s.Name} over any further: trade with it instead");
            if (ResAmt[Res.Gold] < GiftGold) return Result.No($"A gift costs {GiftGold} gold");
            ResAmt[Res.Gold] -= GiftGold;
            Caravans.Add(new Caravan { SettlementId = s.Id, Kind = CaravanKind.Gift, Gold = GiftGold, ArriveDay = Day + SettlementMarchDays(s) });
            return Result.Yes();
        }

        int PendingGiftWarmth(Settlement s) => Caravans.Count(c => c.Kind == CaravanKind.Gift && c.SettlementId == s.Id) * GiftWarmth;

        public static string GoodName(Res r) => r switch { Res.Food => "grain", Res.Wood => "timber", Res.Ore => "iron ore", _ => r.ToString().ToLower() };

        /// <summary>Caravans that reach their end of the road today: goods into your stores (what does not fit is lost), gold into your
        /// treasury, gifts warm the settlement; every finished trade warms it a little.</summary>
        void StepCaravans()
        {
            for (int i = Caravans.Count - 1; i >= 0; i--)
            {
                var c = Caravans[i];
                if (Day < c.ArriveDay) continue;
                Caravans.RemoveAt(i);
                var s = SettlementById(c.SettlementId);
                string name = s?.Name ?? "a settlement";
                switch (c.Kind)
                {
                    case CaravanKind.Buy:
                    {
                        double room = Math.Max(0, CropStorage(c.Res) - ResAmt[c.Res]);
                        double stored = Math.Min(room, c.Amount);
                        ResAmt[c.Res] += stored;
                        Events.Add($"The caravan from {name} is home with {stored:0} {GoodName(c.Res)}" + (stored < c.Amount ? $" ({c.Amount - stored:0} did not fit in your stores and were lost)." : "."));
                        break;
                    }
                    case CaravanKind.Sell:
                        ResAmt[Res.Gold] += c.Gold;
                        Events.Add($"The caravan is back from {name} with {c.Gold} gold for {c.Amount} {GoodName(c.Res)}.");
                        break;
                    default:
                        if (s != null) s.Attitude = Math.Min(100, Math.Max(s.Attitude, Math.Min(GiftCap, s.Attitude + GiftWarmth)));
                        Events.Add(s != null ? $"{name} received your gift. It now feels {SettlementRules.AttitudeName(s.Attitude)} towards you." : "Your gift was delivered.");
                        continue;
                }
                if (s != null && s.Attitude < TradeWarmthCap)
                    s.Attitude = Math.Min(TradeWarmthCap, s.Attitude + (s.Personality == Personality.Mercantile ? 2 : 1));
            }
        }
    }
}
