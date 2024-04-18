using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Linq;
using System.Runtime.CompilerServices;
using ExileCore;
using ImGuiNET;
using Newtonsoft.Json;
using SharpDX;

namespace AltarHelper;

[Submenu]
public class ModList
{
    [JsonIgnore] public bool Updated = true;

    public ModList()
    {
        Mods = new ContentNode<Mod>
        {
            ItemFactory = () => new Mod() { List = this },
            EnableItemCollapsing = false,
            OnRemove = _ => Updated = true,
        };
        DragMe = new DragNode(this);
    }

    [ConditionalDisplay(nameof(PrepareMods))]
    public ContentNode<Mod> Mods { get; set; }

    [JsonIgnore]
    public DragNode DragMe { get; set; }

    public bool PrepareMods()
    {
        foreach (var mod in Mods.Content)
        {
            mod.List = this;
        }

        return true;
    }
}

[Submenu]
public class Mod
{
    [JsonIgnore] public ModList List;
    [JsonIgnore] public bool Updated = true;

    public Mod()
    {
        MatchingMod = new ListNode() { Value = "", OnValueSelected = s => Updated = true };
        DragMe = new DragNode(this);
    }

    [JsonIgnore]
    public DragNode DragMe { get; set; }

    [ConditionalDisplay(nameof(PrepareMatchingMod))]
    public ListNode MatchingMod { get; set; }

    public bool PrepareMatchingMod()
    {
        if (AltarHelperCore.Instance.ModTexts is null or { Count: 0 })
        {
            ImGui.Text("Enable the plugin first or reload the area to configure this");
            return false;
        }

        MatchingMod.Values = AltarHelperCore.Instance.ModTexts;
        return true;
    }
}

[Submenu(RenderMethod = nameof(Render))]
public class DragNode
{
    private readonly ModList _modList;
    private static int I = 0;
    private readonly Mod _mod;
    private readonly int _i = I++;
    private static readonly ConditionalWeakTable<DragNode, object> DragNodes = [];

    public DragNode(Mod mod)
    {
        DragNodes.Add(this, _i);
        _mod = mod;
    }

    public DragNode(ModList modList)
    {
        _modList = modList;
        DragNodes.Add(this, _i);
    }

    public void Render()
    {
        var dropTargetStart = ImGui.GetCursorPos();
        ImGui.PushStyleColor(ImGuiCol.Button, 0);
        ImGui.Button(_mod == null ? "Drag on me" : "Drag me");
        ImGui.PopStyleColor();
        if (_mod != null && ImGui.BeginDragDropSource())
        {
            ImGuiHelpers.SetDragDropPayload("AltarHelperDragNodeIndex", _i);
            ImGui.Text("...");
            ImGui.EndDragDropSource();
        }

        if (ImGuiHelpers.DrawAllColumnsBox("##dropBox", dropTargetStart) && ImGui.BeginDragDropTarget())
        {
            var sourceId = ImGuiHelpers.AcceptDragDropPayload<int>("AltarHelperDragNodeIndex");
            if (sourceId != null)
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    var source = DragNodes.FirstOrDefault(x => (int)x.Value == sourceId).Key;
                    if (source != null)
                    {
                        source._mod.List.Mods.Content.Remove(source._mod);
                        var targetList = (_mod?.List ?? _modList).Mods.Content;
                        var index = targetList.IndexOf(_mod);
                        source._mod.Updated = true;
                        targetList.Insert(index == -1 ? targetList.Count : index, source._mod);
                    }
                }
            }

            ImGui.EndDragDropTarget();
        }
    }
}

public class Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public DisplaySettings DisplaySettings { get; set; } = new DisplaySettings();
    public DebugSettings DebugSettings { get; set; } = new DebugSettings();

    public ModList BisList { get; set; } = new ModList();
    public ModList BrickList { get; set; } = new ModList();
    public ModList OtherList { get; set; } = new ModList();
}

[Submenu]
public class DisplaySettings
{
    public RangeNode<int> FrameThickness { get; set; } = new RangeNode<int>(2, 1, 5);
    public ColorNode BisColor { get; set; } = new(Color.Violet);
    public ColorNode BrickColor { get; set; } = new(Color.Red);
    public ColorNode PickColor { get; set; } = new(Color.LightGreen);
    public ColorNode NuisanceColor { get; set; } = new(Color.Yellow);
}

[Submenu]
public class DebugSettings
{
    public ToggleNode DebugRawText { get; set; } = new ToggleNode(false);
}