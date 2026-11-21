// Land ownership: the city map is 256 x 256 tiles from the first day, but you own only a 64 x 64 district round the Town Hall and buy more,
// one 16 x 16 parcel at a time, next to land you already own (the vision doc's owned district that grows to the whole map).
//
// The land beyond your border is on the map, drawn and walkable, so nothing ever moves when the border grows; but you may only build, zone,
// lay roads, clear land or work it (fell, quarry, mine) on land you own. Walking, roads that already exist and water never ask: unowned
// land stays passable. A parcel costs gold, a little more for each one you have bought beyond the start.
using System;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class World
    {
        public const int ParcelSize = 16, ParcelsPerSide = N / ParcelSize, StartDistrict = 64;
        public const int StartParcels = (StartDistrict / ParcelSize) * (StartDistrict / ParcelSize);
        /// <summary>Gold for a parcel: this much, plus ParcelCostStep for every parcel already bought beyond the starting district.</summary>
        public const int ParcelBaseCost = 25, ParcelCostStep = 3;
        public const string NotYours = "That land is not yours yet. Claim the parcel first (Land, L)";

        readonly bool[] _claimed = new bool[ParcelsPerSide * ParcelsPerSide];
        public int ClaimedParcels { get; private set; }

        public static int ParcelOf(int x, int y) => (y / ParcelSize) * ParcelsPerSide + x / ParcelSize;
        public static (int px, int py) ParcelXY(int parcel) => (parcel % ParcelsPerSide, parcel / ParcelsPerSide);

        /// <summary>True when the tile is on land you own (JS claimed). Asked by placing, zoning, road laying, clearing and every work crew.</summary>
        public bool Claimed(int x, int y) => Inb(x, y) && _claimed[ParcelOf(x, y)];
        public bool ParcelClaimed(int px, int py) => px >= 0 && py >= 0 && px < ParcelsPerSide && py < ParcelsPerSide && _claimed[py * ParcelsPerSide + px];

        /// <summary>The 4 x 4 parcels (64 x 64 tiles) centred on the Town Hall's start tile.</summary>
        void ClaimStartDistrict()
        {
            int lo = (Start - StartDistrict / 2) / ParcelSize, n = StartDistrict / ParcelSize;
            for (int py = lo; py < lo + n; py++) for (int px = lo; px < lo + n; px++) SetClaimed(px, py, true);
        }

        void SetClaimed(int px, int py, bool owned)
        {
            int i = py * ParcelsPerSide + px;
            if (_claimed[i] == owned) return;
            _claimed[i] = owned; ClaimedParcels += owned ? 1 : -1;
        }

        /// <summary>Own every parcel that touches the square [x0, x0 + size) x [y0, y0 + size): what a saved city keeps when it is placed in
        /// the bigger map, so a city you already built is entirely yours.</summary>
        void ClaimSquare(int x0, int y0, int size)
        {
            for (int py = y0 / ParcelSize; py <= (y0 + size - 1) / ParcelSize; py++)
                for (int px = x0 / ParcelSize; px <= (x0 + size - 1) / ParcelSize; px++)
                    if (px >= 0 && py >= 0 && px < ParcelsPerSide && py < ParcelsPerSide) SetClaimed(px, py, true);
        }

        /// <summary>Own the whole map (tests and old saves).</summary>
        public void ClaimAll()
        {
            for (int i = 0; i < _claimed.Length; i++) SetClaimed(i % ParcelsPerSide, i / ParcelsPerSide, true);
            Touch(); Recalc();
        }

        /// <summary>Gold for the next parcel.</summary>
        public int ParcelCost => ParcelBaseCost + ParcelCostStep * Math.Max(0, ClaimedParcels - StartParcels);

        public bool ParcelTouchesYours(int px, int py) =>
            ParcelClaimed(px - 1, py) || ParcelClaimed(px + 1, py) || ParcelClaimed(px, py - 1) || ParcelClaimed(px, py + 1);

        /// <summary>Whether the parcel can be bought now: inside the map, not yours, next to your land, and you have the gold.</summary>
        public Result CanClaim(int px, int py)
        {
            if (px < 0 || py < 0 || px >= ParcelsPerSide || py >= ParcelsPerSide) return Result.No("Outside the map");
            if (_claimed[py * ParcelsPerSide + px]) return Result.No("You already own this land");
            if (!ParcelTouchesYours(px, py)) return Result.No("Land must be next to land you own");
            if (ResAmt[Res.Gold] < ParcelCost) return Result.No($"Not enough gold: this parcel costs {ParcelCost}");
            return Result.Yes();
        }

        public Result ClaimParcel(int px, int py)
        {
            var c = CanClaim(px, py);
            if (!c.Ok) return c;
            ResAmt[Res.Gold] -= ParcelCost;
            SetClaimed(px, py, true);
            Touch(); Recalc();
            return Result.Yes();
        }
    }
}
