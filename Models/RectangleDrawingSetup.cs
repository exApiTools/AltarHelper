using ExileCore.PoEMemory;
using SharpDX;

namespace AltarHelper.Models
{
    public class RectangleDrawingSetup
    {
        public Element Label { get; }
        public Color Color { get; }
        public int FrameThickness { get; }
        public RectangleDrawingSetup(Element label, Color color, int frameThickness)
        {
            Label = label;
            Color = color;
            FrameThickness = frameThickness;
        }
        public RectangleF? TryGetRectangleFfromLabel()
        {
            if (Label == null) return null;
            return Label.GetClientRect();
        }

    }
}
