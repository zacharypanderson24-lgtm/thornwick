// Trade by water (DESIGN_WATER.md, "Transport modes"). Every trade now has a transport mode and the two kinds of capacity are separate:
//   Markets give land caravans (one each); ports give ships (one each). A land caravan on the road never uses a ship's place and a ship at sea
//   never uses a market's.
//   A ship is used only where the world map shows one sea or lake joining your region to the settlement (WaterAtlas), the port stands on that
//   kind of water, and a ship is free. The ship's travel time comes from the length of the water route: it is not always faster than walking
//   (the route may wind round a coast), and a shipment goes by whichever of the two ways you have available arrives sooner.
//   Rivers are not offered between settlements: the world map has no river courses to prove a connection.
using System;
using System.Collections.Generic;
using System.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public struct TradePlan
    {
        public bool Ok; public TradeMode Mode; public int Days, RouteKm; public string Reason;
        public static TradePlan No(string why) => new TradePlan { Reason = why };
    }

    public partial class World
    {
        public const int ShipKmPerDay = 30, ShipLoadDays = 2, ShipsPerPort = 1;

        public WaterAtlas Atlas { get; private set; }
        readonly Dictionary<int, int> _routeKm = new Dictionary<int, int>();

        /// <summary>Hand over the world map's water (called with the settlements). Without it no trade goes by water.</summary>
        public void SetAtlas(WaterAtlas atlas) { Atlas = atlas; _routeKm.Clear(); }

        /// <summary>What kind of water a port stands on: sea (Coastal), Lake or River; Overland (none) if it has no boat-deep water to use.</summary>
        public TradeMode PortMode(Building port)
        {
            var launch = Waters == null ? null : LaunchTile(port);
            if (!launch.HasValue) return TradeMode.Overland;
            var body = Waters.BodyAt(launch.Value.x, launch.Value.y);
            if (body == null) return TradeMode.Overland;
            if (SurfaceWater.Salty(body.Kind) || body.Kind == WaterClass.Estuary) return TradeMode.Coastal;
            return body.Standing ? TradeMode.Lake : TradeMode.River;
        }

        /// <summary>Land caravans out (they use a market's place) and ships out (they use a port's).</summary>
        public int LandCaravansOut => Caravans.Count(c => c.Kind != CaravanKind.Gift && c.Mode == TradeMode.Overland);
        public int ShipsOut => Caravans.Count(c => c.Kind != CaravanKind.Gift && c.Mode != TradeMode.Overland);
        public int ShipCapacity => Blds.Count(b => b.T == "port" && b.Served) * ShipsPerPort;
        public int CaravanCapacity => Markets;

        /// <summary>The length of the water route (km) from your region to a settlement, and why not when there is none.</summary>
        public (bool ok, TradeMode mode, int km, string why) MaritimeRoute(Settlement s)
        {
            if (Ports == 0) return (false, TradeMode.Overland, 0, "you have no port");
            if (Atlas == null) return (false, TradeMode.Overland, 0, "the world map's water is not known here");
            var home = Atlas.AccessAt(HomeCellX, HomeCellY);
            if (!home.HasValue) return (false, TradeMode.Overland, 0, "your region has no sea or lake on the world map");
            var dest = Atlas.AccessAt(s.X, s.Y);
            if (!dest.HasValue) return (false, TradeMode.Overland, 0, $"{s.Name} is not on the sea or a lake");
            if (home.Value.Mode != dest.Value.Mode || home.Value.Body != dest.Value.Body) return (false, TradeMode.Overland, 0, $"{s.Name} is not on the same water as your region");
            bool portOnIt = Blds.Any(b => b.T == "port" && b.Served && PortMode(b) == home.Value.Mode);
            if (!portOnIt) return (false, TradeMode.Overland, 0, home.Value.Mode == TradeMode.Coastal ? "your port is not on the sea" : "your port is not on that lake");
            var key = s.Id;
            if (!_routeKm.TryGetValue(key, out int km)) _routeKm[key] = km = Atlas.RouteKm(home.Value, dest.Value);
            if (km < 0) return (false, TradeMode.Overland, 0, $"no water route joins you to {s.Name}");
            return (true, home.Value.Mode, km, null);
        }

        /// <summary>How a caravan to this settlement would go, how long it takes, or why none can set out now.</summary>
        public TradePlan PlanTrade(Settlement s)
        {
            bool landFree = Markets > 0 && LandCaravansOut < Markets;
            var sea = MaritimeRoute(s);
            bool seaFree = sea.ok && ShipsOut < ShipCapacity;
            int landDays = SettlementMarchDays(s);
            int seaDays = sea.ok ? Math.Max(1, (sea.km + ShipKmPerDay - 1) / ShipKmPerDay + ShipLoadDays) : 0;
            if (landFree && seaFree) return seaDays < landDays ? new TradePlan { Ok = true, Mode = sea.mode, Days = seaDays, RouteKm = sea.km } : new TradePlan { Ok = true, Mode = TradeMode.Overland, Days = landDays, RouteKm = SettlementKm(s) };
            if (landFree) return new TradePlan { Ok = true, Mode = TradeMode.Overland, Days = landDays, RouteKm = SettlementKm(s) };
            if (seaFree) return new TradePlan { Ok = true, Mode = sea.mode, Days = seaDays, RouteKm = sea.km };
            // nothing can set out: say why in terms of what the player can do
            if (Markets == 0 && Ports == 0) return TradePlan.No("Build a Market (Trade, 7) beside a road, or a Port on the water: caravans and ships set out from them");
            if (Markets == 0) return sea.ok ? TradePlan.No($"All {ShipCapacity} of your ships are at sea. Build another Port, or a Market for an overland caravan") : TradePlan.No($"You have no Market for an overland caravan, and a ship cannot reach {s.Name}: {sea.why}");
            if (sea.ok && ShipCapacity > 0) return TradePlan.No($"Your {(Markets == 1 ? "market's caravan is" : $"{Markets} markets' caravans are")} on the road and {(ShipCapacity == 1 ? "your ship is" : $"all {ShipCapacity} ships are")} at sea");
            return TradePlan.No(Markets == 1 ? "Your market's caravan is already on the road. Build another market for a second caravan." : $"All {Markets} of your markets' caravans are on the road");
        }

        public static string ModeName(TradeMode m) => m switch { TradeMode.Coastal => "by sea", TradeMode.Lake => "by lake", TradeMode.River => "by river", _ => "overland" };
    }
}
