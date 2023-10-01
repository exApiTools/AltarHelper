using SharpDX;

namespace AltarHelper.Models
{
    public class TextDrawingSetup
    {
        public Vector2 vector { get; }
        public Color color { get; }
        public string text { get; }
        public TextDrawingSetup(Vector2 vector, Color color, string text)
        {
            this.vector = vector;
            this.color = color;
            this.text = text;
        }

    }
}
