using AltarHelper.Models;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
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
    private Dictionary<string, (ModRankType Type, int Rank, bool IsDownside, string Sound)?> _computedModRanking;
    public static AltarHelperCore Instance;
    private const string TangleAltar = "Metadata/MiscellaneousObjects/PrimordialBosses/TangleAltar";
    private const string FireAltar = "Metadata/MiscellaneousObjects/PrimordialBosses/CleansingFireAltar";
    private readonly Dictionary<uint, bool> _soundPlayedTracker = new Dictionary<uint, bool>();

    private enum ModRankType
    {
        Bis,
        Brick,
        Normal
    };

    public AltarHelperCore()
    {
        Instance = this;
        _labelCache = new FramesCache<List<LabelOnGround>>(() =>
        {
            return GameController.EntityListWrapper.OnlyValidEntities.Any(x =>
                x.Metadata is TangleAltar or FireAltar && x.TryGetComponent<StateMachine>(out var stateComp)
                                                       && stateComp.States.Any(state => state.Name == "activated" && state.Value != 1))
                ? GameController.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Where(label => label.ItemOnGround.Metadata is TangleAltar or FireAltar).ToList()
                : [];
        });
    }

    public override void AreaChange(AreaInstance area)
    {
        _soundPlayedTracker.Clear();

        if (_mods == null || !_mods.Any() || _mods.Any(x => x.Value.Contains('<')))
        {
            _mods = GameController.Files.AtlasPrimordialAltarChoices.EntriesList.ToDictionary(x => x,
                x =>
                {
                    var stats = x.Mod.StatNames.Select(x => x.MatchingStat).ToList();
                    var specificTranslation = GameController.Files.PrimordialAltarStatDescriptions.TranslateMod(stats, 0, s => "#");
                    if (specificTranslation.Contains('<'))
                    {
                        specificTranslation = GameController.Files.StatDescriptions.TranslateMod(stats, 0, s => "#");
                    }

                    return $"[{x.Type.Id switch { "InfluencedMonsters" => "Monsters", var s => s }}] {specificTranslation}";
                });
            _reverseMods = _mods.ToLookup(x => x.Value, x => x.Key);
            ModTexts = _reverseMods.Select(x => x.Key).Order().ToList();
        }

        _altarCache.Clear();
    }

    public override Job Tick()
    {
        var modLists = new[] { Settings.MustPickMods, Settings.BrickMods, Settings.OtherMods, };
        var updatedItems = modLists.SelectMany(x => x.Mods.Content).Where(x => x.Updated).ToList();
        var updatedLists = modLists.Where(x => x.Updated).ToList();
        if (updatedItems.Any() || updatedLists.Any() || _computedModRanking == null)
        {
            _computedModRanking = new[]
            {
                (List: Settings.MustPickMods, Type: ModRankType.Bis),
                (List: Settings.BrickMods, Type: ModRankType.Brick),
                (List: Settings.OtherMods, Type: ModRankType.Normal),
            }.SelectMany(l => l.List.Mods.Content.SelectMany((m, i) =>
                _reverseMods[m.MatchingMod.Value] switch
                {
                    var v when !v.Any() => LogEmpty<(string, ModRankType, int, bool, string)>(m.MatchingMod.Value),
                    var v => v.Select(am => (am.Mod.Key, l.Type, i, IsDownside: am.Mod.Key.Contains("Downside", StringComparison.Ordinal),
                        Sound: string.IsNullOrWhiteSpace(m.Sound.Value) ? null : m.Sound.Value))
                })).ToDictionary(x => x.Key, x => ((ModRankType, int, bool, string)?)(x.Type, x.i, x.IsDownside, x.Sound));

            foreach (var updatedItem in updatedItems)
            {
                updatedItem.Updated = false;
            }

            foreach (var updatedList in updatedLists)
            {
                updatedList.Updated = false;
            }

            _altarCache.Clear();
            _soundPlayedTracker.Clear();
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

    private (int? bisRank, bool brick, int? pickRank, int? nuisanceRank, HashSet<string> sound) ParseModList(IReadOnlyCollection<string> mods)
    {
        if (_computedModRanking == null)
        {
            DebugWindow.LogError("Computed mod ranking is missing...");
            return default;
        }

        var aggregate = mods.Aggregate<string, (int? bisRank, bool brick, int? pickRank, int? nuisanceRank, HashSet<string> sound)>((null, false, null, null, null), (a, mod) =>
        {
            var modRanking = _computedModRanking.GetValueOrDefault(mod);
            if (modRanking == null)
            {
                return a;
            }

            if (modRanking.Value.Type == ModRankType.Bis)
            {
                return (Math.Min(a.bisRank ?? modRanking.Value.Rank, modRanking.Value.Rank), a.brick, null, null,
                    modRanking.Value.Sound == null ? a.sound : (a.sound ?? []).Union([modRanking.Value.Sound]).ToHashSet());
            }

            if (modRanking.Value.Type == ModRankType.Brick)
            {
                return (a.bisRank, true, null, null, modRanking.Value.Sound == null ? a.sound : (a.sound ?? []).Union([modRanking.Value.Sound]).ToHashSet());
            }

            if (modRanking.Value.IsDownside)
            {
                return (a.bisRank, a.brick, a.pickRank, Math.Min(a.nuisanceRank ?? modRanking.Value.Rank, modRanking.Value.Rank),
                    modRanking.Value.Sound == null ? a.sound : (a.sound ?? []).Union([modRanking.Value.Sound]).ToHashSet());
            }
            else
            {
                return (a.bisRank, a.brick, Math.Min(a.pickRank ?? modRanking.Value.Rank, modRanking.Value.Rank), a.nuisanceRank,
                    modRanking.Value.Sound == null ? a.sound : (a.sound ?? []).Union([modRanking.Value.Sound]).ToHashSet());
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
            var topMods = ((List<AltarEntity.AltarMod>)[
                    ..altarEntity.TopUpsides, 
                    ..altarEntity.TopDownsides, ])
                .Select(x => x.Mod.Key)
                .ToList();
            var bottomMods = ((List<AltarEntity.AltarMod>)[
                    ..altarEntity.BottomUpsides,
                    ..altarEntity.BottomDownsides, ])
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
            if (topRanking.sound?.Union(bottomRanking.sound ?? []).Where(x => x != null).ToList() is { Count: > 0 } sounds &&
                _soundPlayedTracker.TryAdd(altarlabel.ItemOnGround.Id, true))
            {
                var dir = Path.Join(Core.Directory, "sounds");
                if (!Directory.Exists(dir))
                {
                    DebugWindow.LogMsg("Sound directory is missing, unable to play a sound!");
                }
                else
                {
                    foreach (var sound in sounds)
                    {
                        var path = Path.Join(dir, sound);
                        if (!File.Exists(path))
                        {
                            DebugWindow.LogError($"File {path} is missing, unable to play it");
                        }
                        else
                        {
                            GameController.SoundController.PlaySound(path);
                        }
                    }
                }
            }
        }
    }

    private void CleanCalculatedAltarsDictionary()
    {
        foreach (var (altarId, altar) in _altarCache.ToList())
        {
            try
            {
                if (altar == null ||
                    altar.Entity?.IsValid != true ||
                    !altar.Entity.TryGetComponent<StateMachine>(out var stateMachineComp) ||
                    stateMachineComp.States[_AltarActivated].Value == 1)
                {
                    _altarCache.Remove(altarId);
                    _soundPlayedTracker.Remove(altarId);
                }
            }
            catch
            {
                _altarCache.Remove(altarId);
                _soundPlayedTracker.Remove(altarId);
            }
        }
    }
}