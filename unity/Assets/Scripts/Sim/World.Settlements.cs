// The NPC settlements of the world, seen from your settlement (Settlements.cs makes them from the world seed). The game hands them over
// once the world map exists, like the raider camps. Each month they grow towards what their land can hold, and what they trade with you
// starts afresh (World.SettlementTrade.cs). What a save keeps of them is only what changes: their people, their attitude towards you and
// this month's trade; a save loaded before the world map exists keeps that state waiting until the settlements arrive.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SettlementRules = CrownAndHearth.Sim.Settlements;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int SettlementMonth = 30;

        List<Settlement> _settlements = new List<Settlement>();
        /// <summary>Saved rows by settlement id (id, people, attitude, sold, bought, liege, allegiance, garrison; older saves have fewer),
        /// from a loaded save, waiting for the settlements to arrive.</summary>
        readonly Dictionary<int, JArray> _settlementRows = new Dictionary<int, JArray>();

        public IReadOnlyList<Settlement> Settlements => _settlements;
        public Settlement SettlementById(int id) => id >= 0 && id < _settlements.Count ? _settlements[id] : null;

        /// <summary>Hand over this world's settlements (generated from its seed); any saved state is put back onto them.</summary>
        public void SetSettlements(IEnumerable<Settlement> settlements)
        {
            _settlements = settlements.ToList();
            bool realmsChanged = false;
            foreach (var kv in _settlementRows)
            {
                if (!(SettlementById(kv.Key) is Settlement s)) continue;
                var row = kv.Value;
                s.Population = row[1].Value<double>(); s.Attitude = Math.Clamp(row[2].Value<int>(), -100, 100);
                if (row.Count >= 5) { s.SoldThisMonth = row[3].Value<int>(); s.BoughtThisMonth = row[4].Value<int>(); }
                if (row.Count >= 8)
                {
                    int liege = row[5].Value<int>();
                    if (liege != s.Liege) { s.Liege = liege; realmsChanged = true; }
                    s.Allegiance = (Allegiance)row[6].Value<int>();
                    // rows of 9 keep infantry and archers apart; older rows of 8 held one total, which was all infantry then
                    s.GarrisonInfantry = row[7].Value<int>(); s.GarrisonArchers = row.Count >= 9 ? row[8].Value<int>() : 0;
                }
                if (row.Count >= 12) { s.DefendersLost = row[10].Value<double>(); s.DevastatedUntil = row[11].Value<int>(); s.Devastated = Day < s.DevastatedUntil; }
                if (row.Count >= 15) { s.NextMarchDay = row[12].Value<int>(); s.Unrest = row[13].Value<double>(); s.ConqueredDay = row[14].Value<int>(); }
                s.SoldOf.Clear(); s.BoughtOf.Clear();
                if (row.Count >= 10 && row[9] is JObject goods)
                {
                    ReadGoods(goods["sold"] as JObject, s.SoldOf); ReadGoods(goods["bought"] as JObject, s.BoughtOf);
                }
                else
                {   // older rows kept one total each: it was all the main good then
                    if (s.SoldThisMonth > 0) s.SoldOf[s.Makes] = s.SoldThisMonth;
                    if (s.BoughtThisMonth > 0) s.BoughtOf[s.Needs] = s.BoughtThisMonth;
                }
            }
            _settlementRows.Clear();
            foreach (var s in _settlements)   // a liege that does not exist (or is itself) is no liege
                if (s.Liege >= 0 && (SettlementById(s.Liege) == null || s.Liege == s.Id)) { s.Liege = -1; realmsChanged = true; }
            foreach (var s in _settlements) s.LastNeeded = s.Allegiance == Allegiance.Occupied ? GarrisonNeeded(s) : 0;
            if (realmsChanged) RecomputeRealms();
        }

        /// <summary>Distance in km (straight line) from your settlement.</summary>
        public int SettlementKm(Settlement s) => (int)Math.Round(Math.Sqrt((s.X - HomeCellX) * (double)(s.X - HomeCellX) + (s.Y - HomeCellY) * (double)(s.Y - HomeCellY)));
        public int SettlementMarchDays(Settlement s) => Math.Max(1, (SettlementKm(s) + MarchKmPerDay - 1) / MarchKmPerDay);

        /// <summary>The settlements that answer to this one directly.</summary>
        public IEnumerable<Settlement> SubjectsOf(Settlement s) => _settlements.Where(o => o.Liege == s.Id);

        /// <summary>Everyone below this one in its realm (subjects, their subjects, and so on).</summary>
        public int AllSubjects(Settlement s)
        {
            var seen = new HashSet<int> { s.Id }; var stack = new Stack<Settlement>(SubjectsOf(s));
            while (stack.Count > 0) { var o = stack.Pop(); if (!seen.Add(o.Id)) continue; foreach (var c in SubjectsOf(o)) stack.Push(c); }
            return seen.Count - 1;
        }

        /// <summary>Caravans arrive every day; once a month the settlements grow towards what their land can hold (a big one near you
        /// growing into a new size is news) and their trade with you starts afresh.</summary>
        void StepSettlements()
        {
            RefreshTradeBoost();
            StepCaravans();
            StepDetachments();   // World.Conquest.cs: reinforcements reaching their garrisons
            StepRealmArmies();   // World.Realms.cs: armies mustering, setting out and arriving
            if (_settlements.Count == 0 || Day % SettlementMonth != 0) return;
            foreach (var s in _settlements) { s.SoldThisMonth = 0; s.BoughtThisMonth = 0; s.SoldOf.Clear(); s.BoughtOf.Clear(); }
            RecoverSettlements();   // World.Conquest.cs: fallen defenders replaced, sacked places recovering
            StepConquered();   // World.Conquest.cs: tribute and occupied settlements
            foreach (var s in SettlementRules.Grow(_settlements))
                if (s.Tier >= SettlementTier.Town && SettlementKm(s) <= RaidFarKm)
                    Events.Add($"{s.Name} has grown into a {SettlementRules.TierName(s.Tier).ToLower()} ({SettlementKm(s)} km away).");
        }

        // ---------- saving ----------

        JArray SaveSettlements()
        {
            var rows = new JArray();
            if (_settlements.Count > 0)
                foreach (var s in _settlements)
                    rows.Add(new JArray(s.Id, Math.Round(s.Population, 2), s.Attitude, s.SoldThisMonth, s.BoughtThisMonth, s.Liege, (int)s.Allegiance,
                        s.GarrisonInfantry, s.GarrisonArchers, GoodsTraded(s), Math.Round(s.DefendersLost, 2), s.DevastatedUntil,
                        s.NextMarchDay, Math.Round(s.Unrest, 2), s.ConqueredDay));
            else
                foreach (var kv in _settlementRows.OrderBy(k => k.Key)) rows.Add(kv.Value.DeepClone());
            return rows;
        }

        // this month's trade by good: { sold: { Food: 20 }, bought: { Metal: 5 } }
        static JObject GoodsTraded(Settlement s) => new JObject
        {
            ["sold"] = new JObject(s.SoldOf.Where(kv => kv.Value > 0).Select(kv => new JProperty(kv.Key.ToString(), kv.Value))),
            ["bought"] = new JObject(s.BoughtOf.Where(kv => kv.Value > 0).Select(kv => new JProperty(kv.Key.ToString(), kv.Value))),
        };

        static void ReadGoods(JObject o, Dictionary<Res, int> into)
        {
            foreach (var prop in o?.Properties() ?? Enumerable.Empty<JProperty>())
                if (Enum.TryParse<Res>(prop.Name, out var r)) into[r] = (int)prop.Value;
        }

        /// <summary>Your soldiers in garrisons according to the saved rows, before the settlements themselves have arrived.</summary>
        int SavedGarrisons() => _settlementRows.Values.Sum(r => r.Count >= 8 ? r[7].Value<int>() + (r.Count >= 9 ? r[8].Value<int>() : 0) : 0);

        // the army waiting for a decision, reinforcements on the road, and an army caught mid-fight by a save
        JObject SaveConquest()
        {
            var o = new JObject
            {
                ["realmArmies"] = SaveRealmArmies(),   // World.Realms.cs
                ["detachments"] = new JArray(Detachments.Select(d => new JObject
                    { ["to"] = d.SettlementId, ["infantry"] = d.Infantry, ["archers"] = d.Archers, ["arrive"] = d.ArriveDay })),
            };
            if (PendingConquest is PendingConquest p)
                o["pending"] = new JObject
                {
                    ["town"] = p.SettlementId, ["camp"] = p.CampId, ["infantry"] = p.Infantry, ["archers"] = p.Archers,
                    ["loot"] = new JObject(p.Loot.Where(kv => kv.Value > 0).Select(kv => new JProperty(kv.Key.ToString(), kv.Value))),
                };
            // a fight cannot be saved: an army in one when the game is saved is written as marching home, so it is never lost
            if (TroopsInBattleInfantry + TroopsInBattleArchers > 0)
                o["inBattle"] = new JObject { ["camp"] = _inBattleCamp, ["infantry"] = TroopsInBattleInfantry, ["archers"] = TroopsInBattleArchers, ["returnDay"] = Day + _inBattleDays };
            return o;
        }

        void LoadConquest(JObject o)
        {
            Detachments.Clear(); PendingConquest = null; PendingBattle = null; RealmArmies.Clear();
            if (o == null) return;   // older saves have none
            LoadRealmArmies(o["realmArmies"] as JArray);
            foreach (var item in o["detachments"] as JArray ?? new JArray())
                Detachments.Add(new Detachment { SettlementId = (int)item["to"], Infantry = (int)item["infantry"], Archers = (int)item["archers"], ArriveDay = (int)item["arrive"] });
            if (o["pending"] is JObject p)
            {
                var pc = new PendingConquest { SettlementId = (int)p["town"], CampId = (int)p["camp"], Infantry = (int)p["infantry"], Archers = (int)p["archers"] };
                foreach (var prop in (p["loot"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                    if (Enum.TryParse<Res>(prop.Name, out var r)) pc.Loot[r] = (int)prop.Value;
                PendingConquest = pc;
                Events.Add("Your army is waiting at the settlement it took for your decision: open the world map (M) and click it.");
            }
            if (o["inBattle"] is JObject b)
            {
                Expeditions.Add(new Expedition { CampId = (int)b["camp"], Infantry = (int)b["infantry"], Archers = (int)b["archers"], ReturnDay = (int)b["returnDay"] });
                Events.Add("The game was saved during a fight: the army broke off and is marching home.");
            }
        }

        JArray SaveCaravans() => new JArray(Caravans.Select(c => new JObject
        {
            ["to"] = c.SettlementId, ["kind"] = c.Kind.ToString(), ["res"] = c.Res.ToString(), ["amount"] = c.Amount, ["gold"] = c.Gold, ["arrive"] = c.ArriveDay, ["mode"] = c.Mode.ToString(), ["depart"] = c.DepartDay, ["km"] = c.RouteKm,
        }));

        void LoadSettlements(JArray rows, JArray caravans)
        {
            _settlementRows.Clear();
            if (rows != null)   // older saves have none: the settlements start as generated
                foreach (var item in rows)
                    if (item is JArray row && row.Count >= 3) _settlementRows[row[0].Value<int>()] = (JArray)row.DeepClone();
            Caravans.Clear();
            foreach (var item in caravans ?? new JArray())
                if (item is JObject c && Enum.TryParse<CaravanKind>((string)c["kind"], out var kind))
                    Caravans.Add(new Caravan
                    {
                        SettlementId = (int)c["to"], Kind = kind, Res = Enum.TryParse<Res>((string)c["res"], out var r) ? r : Res.Food,
                        Amount = (int)c["amount"], Gold = (int)c["gold"], ArriveDay = (int)c["arrive"],
                        Mode = Enum.TryParse<TradeMode>((string)c["mode"], out var mode) ? mode : TradeMode.Overland,   // older saves: every caravan walked
                        DepartDay = c["depart"] != null ? (int)c["depart"] : (int)c["arrive"], RouteKm = c["km"] != null ? (int)c["km"] : 0,
                    });
        }
    }
}
