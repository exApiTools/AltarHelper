using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;

namespace AltarHelper.Models
{
    public class Altar
    {
        public Entity EntityReference { get; set; }
        public Element TopOptionLabel { get; set; }
        public Element BottomOptionLabel { get; set; }
        public Selection TopSelection { get; set; }
        public Selection BottomSelection { get; set; }

        public RectangleDrawingSetup? TopRectangleDrawing { get; set; }
        public RectangleDrawingSetup? BottomRectangleDrawing { get; set; }
        public TextDrawingSetup? TopTextDrawing { get; set; }
        public TextDrawingSetup? BottomTextDrawing { get; set; }

        public Altar(Selection top, Selection bottom, Entity entity, Element topLabel, Element bottomLabel)
        {
            TopSelection = top;
            BottomSelection = bottom;
            EntityReference = entity;
            TopOptionLabel = topLabel;
            BottomOptionLabel = bottomLabel;
        }
    }
}
