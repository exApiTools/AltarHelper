using AltarHelper.Models;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory.FilesInMemory;

namespace AltarHelper;

public class AltarHelperCore : BaseSettingsPlugin<Settings>
{
    private readonly CachedValue<List<LabelOnGround>> _labelCache;
    private readonly Dictionary<uint, Altar> _altarCache = [];
    private const int _AltarActivated = 0;
    private Dictionary<AtlasPrimordialAltarChoice, string> _mods;
    private ILookup<string, AtlasPrimordialAltarChoice> _reverseMods;
    internal List<string> ModTexts;
    private Dictionary<string, (ModRankType Type, int Rank, bool IsDownside)?> _computedModRanking;
    public static AltarHelperCore Instance;

    private enum ModRankType
    {
        Bis,
        Brick,
        Normal
    };

    public AltarHelperCore()
    {
        Instance = this;
        _labelCache = new FramesCache<List<LabelOnGround>>(() => GameController.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Where(x =>
            x.ItemOnGround.Metadata == "Metadata/MiscellaneousObjects/PrimordialBosses/TangleAltar" ||
            x.ItemOnGround.Metadata == "Metadata/MiscellaneousObjects/PrimordialBosses/CleansingFireAltar").ToList());
    }

    public override void AreaChange(AreaInstance area)
    {
        if (_mods == null || !_mods.Any() || _mods.Any(x => x.Value.Contains('<')))
        {
            _mods = GameController.Files.AtlasPrimordialAltarChoices.EntriesList.ToDictionary(x => x,
                x =>
                {
                    var specificTranslation = GameController.Files.PrimordialAltarStatDescriptions.TranslateMod(x.Mod.StatNames.ToDictionary(s => s.MatchingStat, s => 0), 0);
                    if (specificTranslation.Contains('<'))
                    {
                        specificTranslation = GameController.Files.StatDescriptions.TranslateMod(x.Mod.StatNames.ToDictionary(s => s.MatchingStat, s => 0), 0);
                    }

                    return $"[{x.Type.Id}] {specificTranslation}";
                });
            _reverseMods = _mods.ToLookup(x => x.Value, x => x.Key);
            ModTexts = _reverseMods.Select(x => x.Key).Order().ToList();
        }

        _altarCache.Clear();
    }

    public override Job Tick()
    {
        var modLists = new[] { Settings.BisList, Settings.BrickList, Settings.OtherList, };
        var updatedItems = modLists.SelectMany(x => x.Mods.Content).Where(x => x.Updated).ToList();
        var updatedLists = modLists.Where(x => x.Updated).ToList();
        if (updatedItems.Any() || updatedLists.Any() || _computedModRanking == null)
        {
            _computedModRanking = new[]
            {
                (List: Settings.BisList, Type: ModRankType.Bis),
                (List: Settings.BrickList, Type: ModRankType.Brick),
                (List: Settings.OtherList, Type: ModRankType.Normal),
            }.SelectMany(l => l.List.Mods.Content.SelectMany((m, i) =>
                _reverseMods[m.MatchingMod.Value] switch
                {
                    var v when !v.Any() => LogEmpty<(string, ModRankType, int, bool)>(m.MatchingMod.Value),
                    var v => v.Select(am => (am.Mod.Key, l.Type, i, IsDownside: am.Mod.Key.Contains("Downside", StringComparison.Ordinal)))
                })).ToDictionary(x => x.Key, x => ((ModRankType, int, bool)?)(x.Type, x.i, x.IsDownside));

            foreach (var updatedItem in updatedItems)
            {
                updatedItem.Updated = false;
            }

            foreach (var updatedList in updatedLists)
            {
                updatedList.Updated = false;
            }

            _altarCache.Clear();
        }

        CleanCalculatedAltarsDictionary();
        CalculateAltars();
        return null;
    }

    public override void Render()
    {
        foreach (var entry in _altarCache)
        {
            var altar = entry.Value;
            if (altar.Entity == null) continue;

            RenderOptionFrame(altar.TopOptionLabel.GetClientRect(), altar.TopOptionConfig);
            RenderOptionFrame(altar.BottomOptionLabel.GetClientRect(), altar.BottomOptionConfig);
        }
    }

    private void RenderOptionFrame(RectangleF rect, AltarOptionConfig config)
    {
        if (config.IsBis)
        {
            Graphics.DrawFrame(rect, Settings.DisplaySettings.BisColor, Settings.DisplaySettings.FrameThickness);
            if (config.IsBrick)
            {
                rect.Inflate(-Settings.DisplaySettings.FrameThickness.Value, -Settings.DisplaySettings.FrameThickness.Value);
                Graphics.DrawFrame(rect, Settings.DisplaySettings.BrickColor, Settings.DisplaySettings.FrameThickness);
            }
        }
        else if (config.IsBrick)
        {
            Graphics.DrawFrame(rect, Settings.DisplaySettings.BrickColor, Settings.DisplaySettings.FrameThickness);
        }
        else if (config.IsPick)
        {
            Graphics.DrawFrame(rect, Settings.DisplaySettings.PickColor, Settings.DisplaySettings.FrameThickness);
        }
        else if (config.IsNuisance)
        {
            Graphics.DrawFrame(rect, Settings.DisplaySettings.NuisanceColor, Settings.DisplaySettings.FrameThickness);
        }
    }

    private IReadOnlyCollection<T> LogEmpty<T>(string value)
    {
        DebugWindow.LogError($"No matching mod found for string '{value}'");
        return [];
    }

    private (int? bisRank, bool brick, int? pickRank, int? nuisanceRank) ParseModList(IReadOnlyCollection<string> mods)
    {
        if (_computedModRanking == null)
        {
            DebugWindow.LogError("Computed mod ranking is missing...");
            return default;
        }

        var aggregate = mods.Aggregate<string, (int? bisRank, bool brick, int? pickRank, int? nuisanceRank)>((null, false, null, null), (a, mod) =>
        {
            var modRanking = _computedModRanking.GetValueOrDefault(mod);
            if (modRanking == null)
            {
                return a;
            }

            if (modRanking.Value.Type == ModRankType.Bis)
            {
                return (Math.Min(a.bisRank ?? modRanking.Value.Rank, modRanking.Value.Rank), a.brick, null, null);
            }

            if (modRanking.Value.Type == ModRankType.Brick)
            {
                return (a.bisRank, true, null, null);
            }

            if (modRanking.Value.IsDownside)
            {
                return (a.bisRank, a.brick, a.pickRank, Math.Min(a.nuisanceRank ?? modRanking.Value.Rank, modRanking.Value.Rank));
            }
            else
            {
                return (a.bisRank, a.brick, Math.Min(a.pickRank ?? modRanking.Value.Rank, modRanking.Value.Rank), a.nuisanceRank);
            }
        });

        if (aggregate.bisRank != null || aggregate.brick)
        {
            aggregate.pickRank = null;
            aggregate.nuisanceRank = null;
        }

        if (aggregate is { pickRank: { } pickRank, nuisanceRank: { } nuisanceRank })
        {
            if (pickRank < nuisanceRank)
            {
                aggregate.nuisanceRank = null;
            }
            else
            {
                aggregate.pickRank = null;
            }
        }

        return aggregate;
    }

    private void CalculateAltars()
    {
        var altarLabels = _labelCache.Value;
        foreach (var altarlabel in altarLabels)
        {
            var topOptionLabel = altarlabel.Label?.GetChildAtIndex(0);
            var bottomOptionLabel = altarlabel.Label?.GetChildAtIndex(1);

            //update some values but skip Weightcalculation
            if (_altarCache.TryGetValue(altarlabel.ItemOnGround.Id, out var existingAltar))
            {
                //Update the dictionary in case we moved out of the network bubble
                existingAltar.Entity = altarlabel.ItemOnGround;
                existingAltar.TopOptionLabel = topOptionLabel;
                existingAltar.BottomOptionLabel = bottomOptionLabel;
                continue;
            }

            var altarEntity = altarlabel.ItemOnGround.AsObject<AltarEntity>();
            var topMods = new[] { altarEntity.TopUpside1, altarEntity.TopUpside2, altarEntity.TopDownside1, altarEntity.TopDownside2 }
                .Where(x => x != null)
                .Select(x => x.Mod.Key)
                .ToList();
            var bottomMods = new[] { altarEntity.BottomUpside1, altarEntity.BottomUpside2, altarEntity.BottomDownside1, altarEntity.BottomDownside2 }
                .Where(x => x != null)
                .Select(x => x.Mod.Key)
                .ToList();


            if (Settings.DebugSettings.DebugRawText == true)
            {
                DebugWindow.LogError($"Top mods: {string.Join(", ", topMods)}");
                DebugWindow.LogError($"Bottom mods: {string.Join(", ", bottomMods)}");
            }

            var topRanking = ParseModList(topMods);
            var bottomRanking = ParseModList(bottomMods);

            var altar = new Altar
            {
                Entity = altarEntity,
                BottomOptionConfig = new AltarOptionConfig(bottomRanking.bisRank != null, bottomRanking.brick,
                    bottomRanking.pickRank != null && (topRanking.pickRank == null || topRanking.pickRank >= bottomRanking.pickRank),
                    bottomRanking.nuisanceRank != null && (topRanking.nuisanceRank == null || topRanking.nuisanceRank >= bottomRanking.nuisanceRank)),
                BottomOptionLabel = bottomOptionLabel,
                TopOptionConfig = new AltarOptionConfig(topRanking.bisRank != null, topRanking.brick,
                    topRanking.pickRank != null && (bottomRanking.pickRank == null || bottomRanking.pickRank >= topRanking.pickRank),
                    topRanking.nuisanceRank != null && (bottomRanking.nuisanceRank == null || bottomRanking.nuisanceRank >= topRanking.nuisanceRank)),
                TopOptionLabel = topOptionLabel,
            };
            _altarCache[altarlabel.ItemOnGround.Id] = altar;
        }
    }

    private void CleanCalculatedAltarsDictionary()
    {
        foreach (var (altarId, altar) in _altarCache.ToList())
        {
            try
            {
                if (altar == null ||
                    !altar.Entity.TryGetComponent<StateMachine>(out var stateMachineComp) ||
                    stateMachineComp.States[_AltarActivated].Value == 1)
                {
                    _altarCache.Remove(altarId);
                }
            }
            catch
            {
                _altarCache.Remove(altarId);
            }
        }
    }
}