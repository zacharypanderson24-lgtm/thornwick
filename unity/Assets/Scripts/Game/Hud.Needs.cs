using System;
using System.Collections.Generic;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        const double LowReserve = 0.1, LowSupply = 0.8;
        static readonly Color WheatBar = new Color(0.78f, 0.62f, 0.28f);
        static readonly Color WaterBar = new Color(0.35f, 0.65f, 0.74f);
        static readonly Color AlertBar = new Color(0.74f, 0.30f, 0.20f);
        static readonly Color WorkBar = new Color(0.55f, 0.66f, 0.38f);
        static readonly Color SafetyBar = new Color(0.62f, 0.55f, 0.74f);
        NeedMeter _foodMeter, _waterMeter, _workMeter, _safetyMeter;
        WaterPlan _initialWater;
        readonly List<(Text total, RectTransform menu, (Text value, Res resource)[] rows)> _stockGroups = new();

        sealed class NeedMeter
        {
            public Text Amount, Alert;
            public Image Fill, Track;
            public RectTransform Card;
        }

        void BuildStockpileDropdown(RectTransform row, string title, params (string label, Res resource)[] entries)
        {
            var total = MakeStat(row, title, title + "  ?");
            var card = (RectTransform)total.transform.parent;
            var surface = card.GetComponent<Image>();

            const int line = 28, padding = 6;
            var menu = MakePanel(card, "Breakdown", Vector2.zero, Vector2.right,
                new Vector2(0, -entries.Length * line - 2 * padding), Vector2.zero);
            menu.GetComponent<Image>().color = new Color(0.16f, 0.12f, 0.09f, 1);
            var counts = new (Text, Res)[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var (label, resource) = entries[i];
                var y = -padding - i * line;
                var entry = MakePanel(menu, label, Vector2.up, Vector2.one, new Vector2(6, y - line + 1), new Vector2(-6, y));
                entry.GetComponent<Image>().color = i % 2 == 0 ? Btn : Panel;
                var name = MakeText(entry, "Name", 14, TextAnchor.MiddleLeft);
                name.text = label;
                Stretch(name.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
                var value = MakeText(entry, "Count", 15, TextAnchor.MiddleRight);
                Stretch(value.rectTransform, new Vector2(8, 0), new Vector2(-8, 0));
                counts[i] = (value, resource);
            }
            _stockGroups.Add((total, menu, counts));
            menu.gameObject.SetActive(false);
            AddHover(card.gameObject, on =>
            {
                surface.color = on ? BtnHover : Btn;
                menu.gameObject.SetActive(on);
                if (on)
                {
                    row.SetAsLastSibling();
                    if (_world != null) UpdateStockpiles(_world);
                }
            });
        }

        void UpdateStockpiles(World world)
        {
            var rate = world.Rate[Res.Gold];
            var gold = Math.Floor(world.ResAmt[Res.Gold]);
            _gold.text = Math.Abs(rate) < 0.05 ? $"{gold:0}" : $"{gold:0}  {rate:+0.0;-0.0}";
            _gold.color = rate < -0.05 ? BadInk : Ink;
            foreach (var (total, menu, rows) in _stockGroups)
            {
                double sum = 0;
                bool showBreakdown = menu.gameObject.activeSelf;
                foreach (var (value, resource) in rows)
                {
                    double count = Math.Floor(resource == Res.Food ? world.WheatStored : world.ResAmt[resource]);
                    sum += count;
                    if (showBreakdown) value.text = $"{count:0}";
                }
                total.text = $"{sum:0}";
            }
        }

        void BuildNeeds(RectTransform root)
        {
            var strip = MakePanel(root, "Needs", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, NeedsH));
            _foodMeter = BuildNeedMeter(strip, "Food", 0, 0.2f, WheatBar, w =>
                $"Stored {w.EdibleFoodStored:0.#} of {w.EdibleFoodCapacity:0.#} food units. " +
                $"Fresh food: {w.ResAmt[Res.Food]:0.#}; smoked meat: {w.ResAmt[Res.SmokedMeat]:0.#}; " +
                $"eggs: {w.ResAmt[Res.Eggs]:0.#} ({w.ResAmt[Res.Eggs] / World.EggsPerFood:0.#} food; each egg counts as {1.0 / World.EggsPerFood:0.##} food). " +
                $"The town eats {w.DailyFoodDemand:0.#} per day. " +
                (w.DailyFoodDemand > 0 ? $"Stored supplies cover about {w.EdibleFoodStored / w.DailyFoodDemand:0.#} days before new production or livestock feed. " : "") +
                (w.RecentFoodProduction is double food ? $"Recent production averages {food:0.#} per day." : ""));
            _workMeter = BuildNeedMeter(strip, "Work", 0.4f, 0.6f, WorkBar, w => w.WorkHealth().Detail);
            _safetyMeter = BuildNeedMeter(strip, "Safety", 0.6f, 0.8f, SafetyBar, w => w.SafetyHealth().Detail, false);
            BuildMood(strip);
            _waterMeter = BuildNeedMeter(strip, "Water", 0.2f, 0.4f, WaterBar, w =>
            {
                var plan = w.LastWater ?? _initialWater;
                var storage = plan.StoreDays is double days ? $" Reserve lasts about {Math.Floor(days)} days at this rate." : "";
                return $"Sources supplied {plan.Delivered:0.#} of {plan.Demand:0.#} needed per day." + storage +
                    $" Sustained shortages can be fatal after {WaterTuning.Grace} days.";
            });
        }

        NeedMeter BuildNeedMeter(RectTransform strip, string title, float left, float right, Color accent, Func<World, string> details, bool showBar = true)
        {
            var card = MakePanel(strip, title + "Reserve", new Vector2(left, 0), new Vector2(right, 1),
                new Vector2(6, 5), new Vector2(-6, -4));
            card.GetComponent<Image>().color = Btn;
            var label = MakeText(card, "Label", 15, TextAnchor.MiddleLeft);
            label.text = title;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.Lerp(accent, Ink, 0.62f);
            Stretch(label.rectTransform, new Vector2(10, 13), new Vector2(-10, -2));
            var amount = MakeText(card, "Amount", 15, TextAnchor.MiddleRight);
            Stretch(amount.rectTransform, new Vector2(10, 13), new Vector2(-10, -2));
            var alert = MakeText(card, "Warning", 14, TextAnchor.MiddleCenter);
            alert.fontStyle = FontStyle.Bold;
            alert.color = BadInk;
            Stretch(alert.rectTransform, new Vector2(62, 13), new Vector2(-120, -2));
            FitText(alert, 10, 14);
            var track = MakePanel(card, "Track", Vector2.zero, Vector2.right, new Vector2(10, 5), new Vector2(-10, 13));
            var fill = MakePanel(track, "Fill", Vector2.zero, Vector2.up, Vector2.zero, Vector2.zero);
            fill.GetComponent<Image>().raycastTarget = false;
            track.GetComponent<Image>().raycastTarget = false;
            track.gameObject.SetActive(showBar);
            AddHover(card.gameObject, on =>
            {
                if (_world != null) ShowTip(on, card, title, on ? details(_world) : "", "");
            });
            return new NeedMeter { Amount = amount, Alert = alert, Track = track.GetComponent<Image>(), Fill = fill.GetComponent<Image>(), Card = card };
        }

        void UpdateNeeds(World world)
        {
            double food = world.EdibleFoodStored, capacity = world.EdibleFoodCapacity;
            bool foodDeficit = world.RecentFoodProduction < world.DailyFoodDemand * LowSupply;
            var foodWarning = world.Starving && food < world.DailyFoodDemand ? "Starvation" : food < capacity * LowReserve ? "Low reserve" :
                foodDeficit ? "Production below demand" : "";
            SetNeedMeter(_foodMeter, food, capacity, WheatBar, foodWarning);

            var (water, room) = world.WaterStored();
            var plan = world.LastWater ?? _initialWater;
            var waterWarning = world.WaterShortDays >= WaterTuning.Grace ? "Dehydration" :
                plan.Unmet > 0.05 ? "Water shortage" : water < room * LowReserve ? "Low reserve" :
                plan.Delivered < plan.Demand * LowSupply ? "Pumping below demand" : "";
            SetNeedMeter(_waterMeter, water, room, WaterBar, waterWarning);

            var work = world.WorkHealth();
            SetNeedMeter(_workMeter, world.D.Employed, Math.Max(1, world.D.Jobs), WorkBar, work.Level == HealthLevel.Good ? "" : work.Warning, work.Value);
            var safety = world.SafetyHealth();
            SetNeedMeter(_safetyMeter, 0, 1, SafetyBar, safety.Level == HealthLevel.Good ? "" : safety.Warning, safety.Value);
            UpdateMood(world);
        }

        static void SetNeedMeter(NeedMeter meter, double stored, double capacity, Color accent, string warning, string amount = null)
        {
            var low = warning.Length > 0;
            meter.Amount.text = amount ?? $"{stored:0.#} / {capacity:0}";
            meter.Alert.text = warning;
            meter.Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)(stored / Math.Max(1, capacity))), 1);
            meter.Fill.color = low ? AlertBar : accent;
            meter.Track.color = low ? new Color(0.42f, 0.16f, 0.11f) : new Color(0.12f, 0.09f, 0.07f);
        }
    }
}
