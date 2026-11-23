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
        /// <summary>Saved state by settlement id, from a loaded save, waiting for (or already applied to) the settlements.</summary>
        Dictionary<int, (double pop, int att, int sold, int bought)> _settlementState = new Dictionary<int, (double, int, int, int)>();

        public IReadOnlyList<Settlement> Settlements => _settlements;
        public Settlement SettlementById(int id) => id >= 0 && id < _settlements.Count ? _settlements[id] : null;

        /// <summary>Hand over this world's settlements (generated from its seed); any saved state is put back onto them.</summary>
        public void SetSettlements(IEnumerable<Settlement> settlements)
        {
            _settlements = settlements.ToList();
            foreach (var kv in _settlementState)
                if (SettlementById(kv.Key) is Settlement s)
                {
                    s.Population = kv.Value.pop; s.Attitude = Math.Clamp(kv.Value.att, -100, 100);
                    s.SoldThisMonth = kv.Value.sold; s.BoughtThisMonth = kv.Value.bought;
                }
            _settlementState.Clear();
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
            foreach (var s in SettlementRules.Grow(_settlements))
                if (s.Tier >= SettlementTier.Town && SettlementKm(s) <= RaidFarKm)
                    Events.Add($"{s.Name} has grown into a {SettlementRules.TierName(s.Tier).ToLower()} ({SettlementKm(s)} km away).");
        }

        // ---------- saving ----------

        JArray SaveSettlements()
        {
            var rows = new JArray();
            if (_settlements.Count > 0)
                foreach (var s in _settlements) rows.Add(new JArray(s.Id, Math.Round(s.Population, 2), s.Attitude, s.SoldThisMonth, s.BoughtThisMonth));
            else
                foreach (var kv in _settlementState.OrderBy(k => k.Key)) rows.Add(new JArray(kv.Key, Math.Round(kv.Value.pop, 2), kv.Value.att, kv.Value.sold, kv.Value.bought));
            return rows;
        }

        JArray SaveCaravans() => new JArray(Caravans.Select(c => new JObject
        {
            ["to"] = c.SettlementId, ["kind"] = c.Kind.ToString(), ["res"] = c.Res.ToString(), ["amount"] = c.Amount, ["gold"] = c.Gold, ["arrive"] = c.ArriveDay,
        }));

        void LoadSettlements(JArray rows, JArray caravans)
        {
            _settlementState.Clear();
            if (rows != null)   // older saves have none: the settlements start as generated
                foreach (var item in rows)
                    if (item is JArray row && row.Count >= 3)
                        _settlementState[row[0].Value<int>()] = (row[1].Value<double>(), row[2].Value<int>(),
                            row.Count >= 5 ? row[3].Value<int>() : 0, row.Count >= 5 ? row[4].Value<int>() : 0);
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
