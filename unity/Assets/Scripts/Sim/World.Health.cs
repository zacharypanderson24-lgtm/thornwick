// Town health: a plain summary of how the settlement is doing, for the HUD strip under the build bar. Food and Water have their own
// meters (Game/Hud.Needs.cs); this adds Work (are people employed, are jobs filled) and Safety (raiders coming, soldiers at home).
// Pure C# so EditMode tests can check every flag on a seeded town. Nothing here changes the world.
using System;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public enum HealthLevel : byte { Good, Watch, Bad }

    public sealed class HealthCard
    {
        public string Name, Value, Warning, Detail;
        public HealthLevel Level;
    }

    public partial class World
    {
        /// <summary>People without work: at least this many, and at least this share of the peasants, is a problem (below it, a little idleness is normal).</summary>
        public const int IdleWatchMin = 3, WorkMinPeasants = 10;
        public const double IdleBadShare = 0.4, IdleWatchShare = 0.15;

        /// <summary>Employment: idle peasants and unfilled jobs. Both are fixed by the same lever (homes and work in step), so the tip says which way it is off.</summary>
        public HealthCard WorkHealth() => WorkCard(Peasants, D.Idle, D.Jobs, D.Employed);

        /// <summary>The Work card for these numbers. A town under WorkMinPeasants is never flagged: the opening village has idle hands by design,
        /// until the first farms and lodges are zoned.</summary>
        public static HealthCard WorkCard(int peasants, int idle, int jobs, int employed)
        {
            int vacancies = Math.Max(0, jobs - employed);
            var card = new HealthCard { Name = "Work", Value = $"{employed} / {jobs}", Level = HealthLevel.Good, Warning = "" };
            double share = peasants > 0 ? idle / (double)peasants : 0;
            bool settled = peasants >= WorkMinPeasants;
            if (settled && idle >= IdleWatchMin && share >= IdleBadShare)
            {
                card.Level = HealthLevel.Bad; card.Warning = "Many idle";
                card.Detail = $"{idle} of {peasants} peasants have no work. Zone farmland, lumber or mining, or build a workshop, so they can earn their keep.";
            }
            else if (settled && idle >= IdleWatchMin && share >= IdleWatchShare)
            {
                card.Level = HealthLevel.Watch; card.Warning = "Idle hands";
                card.Detail = $"{idle} peasants have no work and {vacancies} jobs are open elsewhere. Check roads and the Workers priorities.";
            }
            else if (settled && vacancies >= IdleWatchMin && idle == 0)
            {
                card.Level = HealthLevel.Watch; card.Warning = "Jobs unfilled";
                card.Detail = $"{vacancies} jobs have no one to fill them. Zone more housing, or set the Workers priorities to what matters most.";
            }
            else card.Detail = $"{employed} of {peasants} peasants are working, {idle} idle, {jobs} jobs in all.";
            return card;
        }

        /// <summary>Safety: a spotted warband is red; a town big enough to be raided with no soldiers is amber; otherwise green.</summary>
        public HealthCard SafetyHealth()
        {
            int towers = Blds.Count(b => b.T == "watchtower"), barracks = Blds.Count(b => b.T == "barracks"), walls = Blds.Count(b => b.T == "wall");
            var card = new HealthCard { Name = "Safety", Level = HealthLevel.Good, Warning = "" };
            card.Value = Soldiers > 0 ? $"{Soldiers} troops" : "No troops";
            string kit = $"{barracks} barracks, {towers} watchtowers, {walls} wall tiles.";
            if (Warband is Warband wb && wb.Spotted)
            {
                int days = Math.Max(0, wb.ArriveDay - Day);
                var camp = CampById(wb.CampId);
                card.Level = HealthLevel.Bad;
                card.Warning = days <= 0 ? "Raiders here" : $"Raiders in {days}d";
                card.Detail = $"A warband of {wb.Band} is marching on the town" + (camp != null ? $" from the {CampSideName(camp)}" : "") + $". You have {Soldiers} troops. {kit}";
            }
            else if (Peasants + Soldiers >= RaidMinPopulation && Soldiers == 0)
            {
                card.Level = HealthLevel.Watch; card.Warning = "No defenders";
                card.Detail = $"The town is big enough to be raided and has no soldiers. Build a barracks and train some. {kit}";
            }
            else if (Peasants + Soldiers < RaidMinPopulation)
                card.Detail = $"Too small to attract raiders yet (they come at {RaidMinPopulation} people). {kit}";
            else card.Detail = $"{Soldiers} troops at home. {kit}";
            return card;
        }
    }
}
