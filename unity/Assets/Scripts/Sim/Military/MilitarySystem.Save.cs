// Military save data ("military" in the settlement save, versioned by MilitaryConfig.SaveVersion).
// Old saves have no such block: their soldiers are turned into garrison companies by Reconcile (no one is added, the population is untouched).
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrownAndHearth.Sim
{
    public sealed partial class MilitarySystem
    {
        public JObject Save() => new JObject
        {
            ["version"] = MilitaryConfig.SaveVersion,
            ["nextId"] = NextId,
            ["companies"] = new JArray(Companies.Select(c => new JObject
            {
                ["id"] = c.Id, ["name"] = c.Name, ["category"] = c.Category.ToString(), ["unit"] = c.Unit.ToString(), ["home"] = c.HomeSettlement,
                ["personnel"] = c.Personnel, ["target"] = c.Target,
                ["training"] = Math.Round(c.Training, 3), ["experience"] = Math.Round(c.Experience, 3), ["morale"] = Math.Round(c.Morale, 2), ["readiness"] = Math.Round(c.Readiness, 2),
                ["mobilised"] = c.Mobilised, ["mobilisedDays"] = c.MobilisedDays,
                ["order"] = c.Order.ToString(), ["orderX"] = c.OrderX, ["orderY"] = c.OrderY, ["stationX"] = c.StationX, ["stationY"] = c.StationY,
                ["equipped"] = c.Equipped, ["location"] = c.Location, ["fatigue"] = c.Fatigue, ["formation"] = c.Formation, ["supply"] = c.Supply,
            })),
        };

        /// <summary>Restore from a save. Anything missing or out of range is repaired rather than trusted, and Reconcile then lines the companies up with the world's soldiers.</summary>
        public void Load(JObject save)
        {
            Companies.Clear(); NextId = 1; _raidRef = null; _lastKilled = 0; _wounds = 0;
            if (save == null) { Reconcile(); return; }
            int version = save.Value<int?>("version") ?? 1;
            if (version > MilitaryConfig.SaveVersion) { Reconcile(); return; }   // from a newer game: keep the population, rebuild the companies from the soldiers
            foreach (var t in save["companies"] as JArray ?? new JArray())
            {
                if (!(t is JObject o)) continue;
                if (!Enum.TryParse(o.Value<string>("category"), out MilitaryCategory cat) || cat == MilitaryCategory.Field) continue;
                if (!Enum.TryParse(o.Value<string>("unit"), out MilitaryUnitType unit)) unit = cat == MilitaryCategory.Militia ? MilitaryUnitType.PeasantLevy : MilitaryUnitType.Spearman;
                if (cat == MilitaryCategory.Militia) unit = MilitaryUnitType.PeasantLevy;
                else if (unit == MilitaryUnitType.PeasantLevy) unit = MilitaryUnitType.Spearman;
                Enum.TryParse(o.Value<string>("order"), out MilitaryOrderKind order);
                var c = new MilitaryCompany
                {
                    Id = Math.Max(1, o.Value<int?>("id") ?? 0), Name = o.Value<string>("name") ?? "", Category = cat, Unit = unit,
                    HomeSettlement = o.Value<int?>("home") ?? -1,
                    Personnel = Math.Max(0, o.Value<int?>("personnel") ?? 0), Target = Math.Max(0, o.Value<int?>("target") ?? 0),
                    Training = Clamp100(o.Value<double?>("training")), Experience = Clamp100(o.Value<double?>("experience")),
                    Morale = Clamp100(o.Value<double?>("morale") ?? MilitaryConfig.RecruitMorale), Readiness = Clamp100(o.Value<double?>("readiness")),
                    Mobilised = cat == MilitaryCategory.Militia && (o.Value<bool?>("mobilised") ?? false), MobilisedDays = Math.Max(0, o.Value<int?>("mobilisedDays") ?? 0),
                    Order = order, OrderX = o.Value<int?>("orderX") ?? -1, OrderY = o.Value<int?>("orderY") ?? -1,
                    StationX = o.Value<int?>("stationX") ?? -1, StationY = o.Value<int?>("stationY") ?? -1,
                    Equipped = Math.Max(0, o.Value<int?>("equipped") ?? 0), Location = o.Value<string>("location") ?? "At station",
                    Fatigue = Clamp100(o.Value<double?>("fatigue")), Formation = o.Value<string>("formation") ?? "", Supply = o.Value<string>("supply") ?? "Home",
                };
                if (c.Personnel > c.Max) c.Personnel = c.Max;
                if (Companies.Any(x => x.Id == c.Id)) c.Id = Companies.Max(x => x.Id) + 1;   // never two companies with one id
                if (c.Name.Length == 0) c.Name = $"{MilitaryCompanies.UnitName(c.Unit)} Company {c.Id}";
                Companies.Add(c);
            }
            NextId = Math.Max(save.Value<int?>("nextId") ?? 1, Companies.Count == 0 ? 1 : Companies.Max(c => c.Id) + 1);
            Reconcile();
        }

        static double Clamp100(double? v) => Math.Clamp(v ?? 0, 0, 100);
    }
}
