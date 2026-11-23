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
            Res.Food => 1, Res.Logs => 1, Res.Wood => 2, Res.Stone => 2, Res.Flax => 2, Res.Ore => 3, Res.Wool => 3, Res.Metal => 8, _ => 2,
        };

        public int Markets => Blds.Count(b => b.T == "market" && b.Served);
        /// <summary>Trading caravans on the road (gifts go by messenger and do not count).</summary>
        public int TradeCaravansOut => Caravans.Count(c => c.Kind != CaravanKind.Gift);

        /// <summary>How much of a good a settlement trades in a month: about one unit for every 8 people, from 10 up to 400.</summary>
        public static int MonthlyTrade(Settlement s) => Math.Clamp((int)Math.Round(s.Population / 8), 10, 400);

        /// <summary>Gold you pay for one unit of what it makes: cheap where it is plentiful (80% of its worth), cheaper the more it likes you.</summary>
        public static double PriceToBuy(Settlement s) => BasePrice(s.Makes) * 0.8 * (1 - s.Attitude / 400.0);
        /// <summary>Gold it pays you for one unit of what it needs: dear where it is scarce (150% of its worth), more the more it likes you.</summary>
        public static double PriceToSell(Settlement s) => BasePrice(s.Needs) * 1.5 * (1 + s.Attitude / 400.0);

        public static int LeftToBuy(Settlement s) => Math.Max(0, MonthlyTrade(s) - s.SoldThisMonth);
        public static int LeftToSell(Settlement s) => Math.Max(0, MonthlyTrade(s) - s.BoughtThisMonth);

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

        /// <summary>Buy goods it makes: pay now, the caravan brings them home after the march.</summary>
        public Result BuyFrom(Settlement s, int amount)
        {
            var ok = CanTrade(s); if (!ok.Ok) return ok;
            amount = Math.Min(Math.Min(amount, CaravanLoad), LeftToBuy(s));
            if (amount <= 0) return Result.No($"{s.Name} has no more {GoodName(s.Makes)} to sell this month");
            int cost = (int)Math.Ceiling(amount * PriceToBuy(s));
            if (ResAmt[Res.Gold] < cost) return Result.No($"That costs {cost} gold; you have {ResAmt[Res.Gold]:0}");
            ResAmt[Res.Gold] -= cost;
            s.SoldThisMonth += amount;
            Caravans.Add(new Caravan { SettlementId = s.Id, Kind = CaravanKind.Buy, Res = s.Makes, Amount = amount, Gold = cost, ArriveDay = Day + SettlementMarchDays(s) });
            return Result.Yes(amount);
        }

        /// <summary>Sell it goods it needs: they leave now, the gold comes home after the march.</summary>
        public Result SellTo(Settlement s, int amount)
        {
            var ok = CanTrade(s); if (!ok.Ok) return ok;
            amount = Math.Min(Math.Min(amount, CaravanLoad), LeftToSell(s));
            if (amount <= 0) return Result.No($"{s.Name} wants no more {GoodName(s.Needs)} this month");
            if (ResAmt[s.Needs] < amount) return Result.No($"You have only {ResAmt[s.Needs]:0} {GoodName(s.Needs)}");
            int gold = (int)Math.Floor(amount * PriceToSell(s));
            ResAmt[s.Needs] -= amount;
            s.BoughtThisMonth += amount;
            Caravans.Add(new Caravan { SettlementId = s.Id, Kind = CaravanKind.Sell, Res = s.Needs, Amount = amount, Gold = gold, ArriveDay = Day + SettlementMarchDays(s) });
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
