using System.Collections.Generic;

namespace AltarHelper.Models
{
    public class Selection
    {
        public AffectedTarget Target { get; set; }
        public List<string> Downsides { get; set; }
        public List<string> Upsides { get; set; }
        public int UpsideWeight { get; set; }
        public int DownsideWeight { get; set; }
        public bool BuffGood { get; set; }
        public bool DebuffGood { get; set; }
    }
}
