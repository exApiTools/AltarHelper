using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;

namespace AltarHelper.Models;

public class Altar
{
    public Entity Entity;
    public AltarOptionConfig TopOptionConfig;
    public AltarOptionConfig BottomOptionConfig;
    public Element TopOptionLabel;
    public Element BottomOptionLabel;
}