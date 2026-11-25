// Seasonal water (DESIGN_WATER.md section 23): winter ice, spring floods, summer drought. Everything here is a pure function of the day and the
// world seed, so nothing is saved and an old save simply shows the right season when loaded. Dry-world maps are untouched (no water, no effect).
//
//   Ice      From autumn's end (day 271) to the year's end the small fresh waters freeze: ponds, lakes, streams and small rivers bear walkers (and
//            raiders), boats are hauled out. Major rivers, estuaries and the sea never freeze.
//   Flood    Most springs (about half, more in wet country) the big rivers flood on day 50: the flood plains are drowned for a while and leave
//            silt, so their harvest that year is a fifth bigger. A message tells you.
//   Drought  About one summer in six (more in dry country): streams run dry (you can walk their beds), the soil is drier (harvests a fifth
//            smaller), and freshwater fishing is poorer.
using System;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int IceFrom = 271, FloodDay = 50, DroughtFrom = 91, DroughtTo = 270;
        public const double SiltYield = 1.2, DroughtYield = 0.8;

        bool _iceSeen, _droughtSeen;
        int _floodAnnounced = -1, _droughtAnnounced = -1;

        public int YearNo => (Math.Max(1, Day) - 1) / YearLength;

        /// <summary>Winter, when the small fresh waters are frozen over.</summary>
        public bool IceOn => Waters != null && YearDay >= IceFrom;

        public static bool Freezes(WaterClass c) => c == WaterClass.Pond || c == WaterClass.Lake || c == WaterClass.Stream || c == WaterClass.SmallRiver;

        /// <summary>True when ice bears people on this water tile now.</summary>
        public bool IceBears(int x, int y) => IceOn && Inb(x, y) && Freezes(Waters.Class[y * N + x]) && Waters.Salinity[y * N + x] < 40;

        /// <summary>Whether this summer is a drought (a function of the year and the seed; dry country has them more often).</summary>
        public bool DroughtYear(int year)
        {
            if (Waters == null) return false;
            double rain = HydroParams.For(Waters.Preset).Rainfall;
            double p = Math.Clamp(0.17 * (1.6 - Math.Min(1.3, rain)) / 0.6, 0.04, 0.6);   // 0.17 at rainfall 1.0; arid about 0.4, wetlands rare
            return Noise.Hash2(year, 7, Seed + 6161) < p;
        }

        public bool DroughtOn => Waters != null && YearDay >= DroughtFrom && YearDay <= DroughtTo && DroughtYear(YearNo);

        /// <summary>True when this spring's flood happens (the river valley and wet country flood most years; the arid never).</summary>
        public bool FloodYear(int year)
        {
            if (Waters == null) return false;
            double rain = HydroParams.For(Waters.Preset).Rainfall;
            if (rain < 0.8) return false;
            return Noise.Hash2(year, 11, Seed + 6262) < Math.Clamp(0.35 + 0.25 * (rain - 1), 0.2, 0.75);
        }

        /// <summary>True during the year of a flood from its day onwards (the silt is down for the harvest).</summary>
        public bool SiltThisYear => Waters != null && YearDay >= FloodDay && FloodYear(YearNo);

        /// <summary>A stream with no water in it: a drought bed that can be walked across.</summary>
        public bool StreamDry(int x, int y) => DroughtOn && Inb(x, y) && Waters.Class[y * N + x] == WaterClass.Stream;

        /// <summary>Water you can cross now without a bridge: a frozen small water or a dry stream bed.</summary>
        public bool Crossable(int x, int y) => Waters != null && (IceBears(x, y) || StreamDry(x, y));

        /// <summary>The seasons of the water: call once a day. Touches the world when ice or drought comes or goes (so paths, drawing and
        /// reach recompute) and tells the player about floods and droughts.</summary>
        void StepWaterSeason()
        {
            if (Waters == null) return;
            bool ice = IceOn, drought = DroughtOn;
            if (ice != _iceSeen || drought != _droughtSeen)
            {
                if (ice && !_iceSeen) Events.Add("Winter: ponds, lakes and small rivers freeze. People and raiders can walk across the ice, and the boats are hauled out.");
                else if (!ice && _iceSeen) Events.Add("The ice has gone. Water is a barrier again.");
                if (drought && !_droughtSeen) Events.Add("Drought: the streams are running dry, the soil is parched and the harvest will be poor. You can walk the dry stream beds.");
                else if (!drought && _droughtSeen && YearDay > DroughtTo) Events.Add("The drought is over: the streams are running again.");
                _iceSeen = ice; _droughtSeen = drought;
                Touch();
            }
            if (YearDay == FloodDay && FloodYear(YearNo) && _floodAnnounced != YearNo && AnyFloodPlain())
            {
                _floodAnnounced = YearNo;
                Events.Add("The spring flood: the rivers have spilled over their flood plains. Fields there will give a richer harvest from the silt this year.");
            }
        }

        bool AnyFloodPlain()
        {
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (FloodPlain(x, y)) return true;
            return false;
        }

        /// <summary>The season's mark on the water for the tile panel: empty when there is nothing to say.</summary>
        public string SeasonNote(int x, int y)
        {
            if (Waters == null || !Inb(x, y)) return null;
            if (IceBears(x, y)) return "Frozen: the ice bears people and raiders until spring.";
            if (StreamDry(x, y)) return "Dry stream bed (drought): you can walk across it.";
            if (SiltThisYear && FloodPlain(x, y)) return "Flooded this spring: the silt gives crops here a richer harvest this year.";
            if (DroughtOn && Waters.Class[y * N + x] == WaterClass.None && SoilMoisture(x, y) < 0.8) return "Drought: the soil is drier than usual.";
            return null;
        }
    }
}
