// How the game is lost (Zach's rule): you lose when the Town Hall is destroyed, or when no people are left.
//
// HALL HEALTH. The Town Hall has HallMaxHp. A raider that reaches the Hall to take its gold also hits it (RaiderHallHit); soldiers, walls and
// watchtowers matter because they stop raiders before they get there. Between raids the Hall is mended (HallMendPerDay). Walls and soldiers
// are the defence, so a defended town loses little. A full Hall takes 17 hits; a camp's band is at most 13 raiders and each hits once, so one camp raid cannot finish a full Hall: it takes
// two in a row on an undefended town (realm armies can be bigger). The sim only SETS the defeat flag; it
// never stops ticking (tests and tools tick worlds with no people on purpose). The game stops the clock when Defeated is true.
//
// SHELTER (World.Shelter.cs): while a raid is on, civilians run into the Hall and hide there.
using System;

namespace CrownAndHearth.Sim
{
    public enum DefeatReason : byte { None, HallDestroyed, NoPeople }

    public partial class World
    {
        public const double HallMaxHp = 100, RaiderHallHit = 6, HallMendPerDay = 4;

        /// <summary>Hit points of the Town Hall (saved). Starts full.</summary>
        public double HallHp { get; private set; } = HallMaxHp;

        public bool Defeated { get; private set; }
        public DefeatReason Defeat { get; private set; }
        public int DefeatDay { get; private set; }

        public string DefeatText => Defeat switch
        {
            DefeatReason.HallDestroyed => "Raiders have destroyed the Town Hall. Without it the settlement cannot stand.",
            DefeatReason.NoPeople => "Everyone is gone. No one is left to rebuild the settlement.",
            _ => "",
        };

        /// <summary>A blow to the Town Hall (a raider at its door). At 0 the settlement falls.</summary>
        public void HitHall(double damage)
        {
            if (Hall == null || Defeated) return;
            bool wasHealthy = HallHp > HallMaxHp / 2;
            HallHp = Math.Max(0, HallHp - damage);
            if (HallHp <= 0) { Lose(DefeatReason.HallDestroyed); return; }
            if (wasHealthy && HallHp <= HallMaxHp / 2) Events.Add("The Town Hall is badly damaged! Drive the raiders off before it falls.");
        }

        void MendHall()
        {
            bool raiding = Raid != null && !Raid.Over;
            if (!raiding && HallHp < HallMaxHp) HallHp = Math.Min(HallMaxHp, HallHp + HallMendPerDay);
        }

        void Lose(DefeatReason why)
        {
            if (Defeated) return;
            Defeated = true; Defeat = why; DefeatDay = Day;
            Events.Add(DefeatText);
        }

        /// <summary>Called once a day at the end of the tick.</summary>
        void CheckDefeat()
        {
            if (Defeated) return;
            if (Peasants + Soldiers <= 0) Lose(DefeatReason.NoPeople);
        }

        // ---- saving (written inline by SaveJson) ----
        internal void LoadDefeat(Newtonsoft.Json.Linq.JObject save)
        {
            HallHp = Math.Clamp(save.Value<double?>("hallHp") ?? HallMaxHp, 0, HallMaxHp);
            int d = save.Value<int?>("defeat") ?? 0;
            Defeated = d != 0; Defeat = (DefeatReason)d; DefeatDay = save.Value<int?>("defeatDay") ?? 0;
        }
    }
}
