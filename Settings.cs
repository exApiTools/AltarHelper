using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
namespace AltarHelper
{

    public class Settings : ISettings
    {

        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        public AltarSettings AltarSettings { get; set; } = new AltarSettings();
        public DebugSettings DebugSettings { get; set; } = new DebugSettings();

    }
    [Submenu]
    public class AltarSettings
    {
        public ButtonNode RefreshFile { get; set; } = new ButtonNode();
        public RangeNode<int> FrameThickness { get; set; } = new RangeNode<int>(2, 1, 5);
        public ColorSettings ColorSettings { get; set; } = new ColorSettings();
        [Menu("Switch Mode", "0 = Filter | 1 =  Only Minions and Player | 2 = Only Boss and Players ")]
        public RangeNode<int> SwitchMode { get; set; } = new RangeNode<int>(1, 1, 3); // Any | Only Minions and Player | Only Boss and Player
        //public ListNode Mode { get; set; } = new ListNode { Values = new List<string> { Enum.GetValues(typeof(DecisionEnum)).GetValue(0).GetDescription, DecisionMode.MinionPlayer.ToString(), DecisionMode.BossPlayer.ToString() } };
        public WeightSettings WeightSettings { get; set; } = new WeightSettings();
        public HotkeyNode HotkeyMode { get; set; } = new HotkeyNode(Keys.F7);
    }
    [Submenu]
    public class WeightSettings
    {
        public ToggleNode EnableExtraWeight { get; set; } = new ToggleNode(false);
        [Menu("Extra Minion Weight", "Add this value to minions mod Type")]
        public RangeNode<int> ExtraMinionWeight { get; set; } = new RangeNode<int>(0, 0, 100);
        [Menu("Extra Boss Weight", "Add this value to boss mod Type")]
        public RangeNode<int> ExtraBossWeight { get; set; } = new RangeNode<int>(0, 0, 100);
        [Menu("Extra Player Weight", "Add this value to player mod Type")]
        public RangeNode<int> ExtraPlayerWeight { get; set; } = new RangeNode<int>(0, 0, 100);
    }
    [Submenu]
    public class ColorSettings
    {
        public ColorNode MinionColor { get; set; } = new ColorNode(SharpDX.Color.LightGreen);
        public ColorNode PlayerColor { get; set; } = new ColorNode(SharpDX.Color.LightCyan);
        public ColorNode BossColor { get; set; } = new ColorNode(SharpDX.Color.LightBlue);
        public ColorNode BadColor { get; set; } = new ColorNode(SharpDX.Color.Red);
    }
    [Submenu]
    public class FilterList
    {
        public ListNode DecisionMode { get; set; } = new ListNode { Values = new List<string> { "Filter", "Only Minions and Player", "Only Boss and Players" } };
    }

    [Submenu]
    public class DebugSettings
    {
        public ToggleNode DebugRawText { get; set; } = new ToggleNode(false);
        public ToggleNode DebugBuffs { get; set; } = new ToggleNode(false);
        public ToggleNode DebugDebuffs { get; set; } = new ToggleNode(false);
        public ToggleNode DebugWeight { get; set; } = new ToggleNode(false);

    }
    public enum DecisionEnum
    {
        [Description("Filter")]
        Filter = 0,
        [Description("Only Minions and Player")]
        MinionPlayer = 1,
        [Description("Only Boss and Players")]
        BossPlayer = 2,
    }

}