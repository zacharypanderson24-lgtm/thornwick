// Notable citizens of this settlement (Sim/Characters). Phase 1 of DESIGN_GOVERNMENT.md: the registry lives here for now; the civilization will own it in Phase 3
// (the data does not change). Characters never touch the economy yet: they only live, age, learn and die.
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        /// <summary>The settlement id of your starting settlement in character data (NPC settlements are 0, 1, 2 ...).</summary>
        public const int CapitalId = -1;

        public CharacterRegistry Characters { get; private set; } = new CharacterRegistry();

        /// <summary>The people of this settlement's pool, alive and not retired.</summary>
        public int NotableCount => Characters.PoolCount(CapitalId);

        /// <summary>What the generator sees: the size and the real economy of this settlement decide where new notables come from.</summary>
        public PoolContext CharacterPool()
        {
            int farms = 0, markets = 0, ports = 0, works = 0, schools = 0;
            foreach (var b in Blds)
                switch (b.T)
                {
                    case "farm": farms++; break;
                    case "market": markets++; break;
                    case "port": case "dock": case "ferry": ports++; break;
                    case "mine": case "quarry": case "sawmill": case "smithery": case "charcoal": works++; break;
                    case "hall": schools++; break;
                }
            var ctx = new PoolContext { SettlementId = CapitalId, Population = Peasants + Soldiers, Seed = Seed, Era = Era };
            var w = ctx.Weights;
            w[(int)Background.Local] = 30;
            w[(int)Background.Farmer] = Math.Min(40, 8 + 6 * farms);
            w[(int)Background.Merchant] = Math.Min(40, 3 + 8 * markets + 6 * ports);
            w[(int)Background.Craftsman] = Math.Min(40, 5 + 5 * works);
            w[(int)Background.Veteran] = Math.Min(30, 3 + (Soldiers + Archers) / 2.0);
            w[(int)Background.Educated] = 4 + 3 * Math.Max(0, Era - 1) + 2 * schools;
            w[(int)Background.Foreign] = Math.Min(15, 2 + 3 * ports);
            return ctx;
        }

        void InitCharacters()
        {
            Characters = new CharacterRegistry();
            Characters.CreateFounders(CharacterPool(), Day);
        }

        /// <summary>Once a month, from Tick.</summary>
        void StepCharacters()
        {
            if (Day % CharacterBalance.Month != 0) return;
            Characters.StepMonth(Day, new[] { CharacterPool() });
        }

        JObject SaveCharacters() => Characters.Save();

        /// <summary>A save with no characters (every save made before this system) gets its notables made now, deterministically from its seed and size.</summary>
        void LoadCharacters(JObject saved)
        {
            if (saved != null) { Characters = CharacterRegistry.Load(saved); return; }
            Characters = new CharacterRegistry();
            Characters.FillToTarget(CharacterPool(), Day);
        }
    }
}
