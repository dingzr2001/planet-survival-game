namespace PlanetSurvival.World.Grid
{
    public readonly struct GridCell
    {
        public GridCell(GridCoordinate coordinate)
        {
            Coordinate = coordinate;
        }

        public GridCoordinate Coordinate { get; }
    }
}
