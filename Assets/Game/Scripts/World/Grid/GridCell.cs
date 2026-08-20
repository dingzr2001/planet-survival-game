namespace PlanetSurvival.World.Grid
{
    public readonly struct GridCell
    {
        public GridCell(GridCoordinate coordinate, float surfaceHeight, float undergroundDepth)
        {
            Coordinate = coordinate;
            SurfaceHeight = surfaceHeight;
            UndergroundDepth = undergroundDepth;
        }

        public GridCoordinate Coordinate { get; }
        public float SurfaceHeight { get; }
        public float UndergroundDepth { get; }
    }
}
