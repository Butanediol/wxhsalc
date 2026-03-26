namespace ClashXW.Models
{
    internal sealed class AppState
    {
        public string? CurrentConfig { get; set; }
        public WindowPlacementState? DashboardPlacement { get; set; }
    }

    internal sealed class WindowPlacementState
    {
        public int Flags { get; set; }
        public int ShowCmd { get; set; }
        public int NormalLeft { get; set; }
        public int NormalTop { get; set; }
        public int NormalRight { get; set; }
        public int NormalBottom { get; set; }
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
    }
}
