namespace AltarHelper.Models
{
    public class FilterEntry
    {
        public string Mod { get; set; }
        public int Weight { get; set; }
        public AffectedTarget Target { get; set; }
        public bool IsUpside { get; set; }
        public string Sound { get; set; }
    }
}
