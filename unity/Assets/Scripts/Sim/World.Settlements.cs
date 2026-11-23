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
                    s.Allegiance = (Allegiance)row[6].Value<int>(); s.Garrison = row[7].Value<int>();
                }
            }
            _settlementRows.Clear();
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
            int n = 0; var stack = new Stack<Settlement>(SubjectsOf(s));
            while (stack.Count > 0) { var o = stack.Pop(); n++; foreach (var c in SubjectsOf(o)) stack.Push(c); }
            return n;
        }

        /// <summary>Caravans arrive every day; once a month the settlements grow towards what their land can hold (a big one near you
        /// growing into a new size is news) and their trade with you starts afresh.</summary>
        void StepSettlements()
        {
            StepCaravans();
            if (_settlements.Count == 0 || Day % SettlementMonth != 0) return;
            foreach (var s in _settlements) { s.SoldThisMonth = 0; s.BoughtThisMonth = 0; }
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
                    rows.Add(new JArray(s.Id, Math.Round(s.Population, 2), s.Attitude, s.SoldThisMonth, s.BoughtThisMonth, s.Liege, (int)s.Allegiance, s.Garrison));
            else
                foreach (var kv in _settlementRows.OrderBy(k => k.Key)) rows.Add(kv.Value.DeepClone());
            return rows;
        }

        JArray SaveCaravans() => new JArray(Caravans.Select(c => new JObject
        {
            ["to"] = c.SettlementId, ["kind"] = c.Kind.ToString(), ["res"] = c.Res.ToString(), ["amount"] = c.Amount, ["gold"] = c.Gold, ["arrive"] = c.ArriveDay,
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
                    });
        }
    }
}
