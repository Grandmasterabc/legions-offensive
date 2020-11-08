namespace GamePlay
{
    public class SpaceDataHandler {
        public UnitType unitType;

        public bool Friendly => friendly && Occupied;
        public bool Enemy => !friendly && Occupied;
        public bool friendly;

        public bool Occupied => unitType != UnitType.None;

        public SpaceDataHandler() { }

        public SpaceDataHandler(UnitType isUnitType, bool isFriendly) {
            unitType = isUnitType;
            friendly = isFriendly;
        }
    }
}