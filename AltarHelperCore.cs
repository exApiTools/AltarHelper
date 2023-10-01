using AltarHelper.Models;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AltarHelper
{
    public class AltarHelperCore : BaseSettingsPlugin<Settings>
    {
        private const string FILTER_FILE = "Filter.txt";
        private List<FilterEntry> FilterList = new();
        private TimeCache<List<LabelOnGround>> LabelCache { get; set; }
        private Dictionary<uint, Altar> CalculatedAltarDict { get; set; } = new Dictionary<uint, Altar>();

        public override bool Initialise()
        {
            Name = "AltarHelper";
            Settings.AltarSettings.RefreshFile.OnPressed += ReadFilterFile;
            ReadFilterFile();
            LabelCache = new TimeCache<List<LabelOnGround>>(UpdateAltarLabelList, 500);
            return true;
        }
        private List<LabelOnGround> UpdateAltarLabelList() => GameController.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Count == 0 ? new List<LabelOnGround>() :
            GameController.IngameState.IngameUi.ItemsOnGroundLabelsVisible.
                Where(x =>
                    x.ItemOnGround.Metadata == "Metadata/MiscellaneousObjects/PrimordialBosses/TangleAltar" ||
                    x.ItemOnGround.Metadata == "Metadata/MiscellaneousObjects/PrimordialBosses/CleansingFireAltar").ToList();
        #region FileHandling
        private void ReadFilterFile()
        {
            var path = $"{DirectoryFullName}\\{FILTER_FILE}";
            if (File.Exists(path))
            {
                ReadFile();
            }
            else
                CreateFilterFile();
        }

        private void CreateFilterFile()
        {
            var path = $"{DirectoryFullName}\\{FILTER_FILE}";
            if (File.Exists(path)) return;
            using (var streamWriter = new StreamWriter(path, true))
            {
                streamWriter.WriteLine("//Name|Weight|Choice|Sound(optional)");
                streamWriter.WriteLine("#Good");
                //  streamWriter.WriteLine("");
                streamWriter.WriteLine("Final Boss drops (2–4) additional Divine Orbs|200000|Boss|inception");
                streamWriter.WriteLine("#Bad");
                streamWriter.WriteLine("");
                streamWriter.Close();
            }
        }

        private void ReadFile()
        {
            FilterList.Clear();
            List<string> lines = File.ReadAllLines($"{DirectoryFullName}\\{FILTER_FILE}").ToList();
            bool isGood = true;
            foreach (string line in lines)
            {
                if (line.Length < 4 || line.StartsWith("//")) continue;

                if (line.StartsWith("#Bad"))
                {
                    isGood = false;
                    continue;
                }
                if (line.StartsWith("#Good"))
                {
                    isGood = true;
                    continue;
                }

                string[] splitLine = line.Split('|');
                var mod = splitLine[0];

                if (mod.Length <= 0 || splitLine[1].Length <= 0) continue;

                FilterEntry filter = new()
                {
                    Mod = mod.Contains('(') && mod.Contains(')') ?
                        Regex.Replace(mod, @"\([^()]*\)", "#") :
                        Regex.Replace(mod, @"(\d+)(?:.\d)|\d+", "#"),
                    Weight = int.Parse(splitLine[1]),
                    IsUpside = isGood,
                    Target = splitLine.Length <= 2 ?
                        AffectedTarget.Any :
                        AltarModsConstants.FilterTargetDict[splitLine[2]],
                    //Sound = splitLine[3],
                };
                FilterList.Add(filter);
            }
            _ = FilterList.OrderBy(x => x.Weight);
        }
        public override void Render()
        {
            foreach (var entry in CalculatedAltarDict)
            {
                var altar = entry.Value;
                if (altar.EntityReference == null) continue;

                var topRectangleDrawing = altar.TopRectangleDrawing;
                if (topRectangleDrawing != null)
                {
                    RectangleF? rectangle = topRectangleDrawing.TryGetRectangleFfromLabel();
                    if (rectangle != null)
                    {
                        Graphics.DrawFrame((RectangleF)rectangle, topRectangleDrawing.Color, topRectangleDrawing.FrameThickness);
                    }
                }
                var bottomRectangleDrawing = altar.BottomRectangleDrawing;
                if (bottomRectangleDrawing != null)
                {
                    RectangleF? rectangle = bottomRectangleDrawing.TryGetRectangleFfromLabel();
                    if (rectangle != null)
                    {
                        Graphics.DrawFrame((RectangleF)rectangle, bottomRectangleDrawing.Color, bottomRectangleDrawing.FrameThickness);
                    }
                }

                //var topTextDrawing = altar.TopTextDrawing;
                //if (topTextDrawing != null)
                //{
                //    Graphics.DrawText(topTextDrawing.text, topTextDrawing.vector, topTextDrawing.color);
                //}
                //var bottomTextDrawing = altar.BottomTextDrawing;
                //if (bottomTextDrawing != null)
                //{
                //    Graphics.DrawText(bottomTextDrawing.text, bottomTextDrawing.vector, bottomTextDrawing.color);
                //}
            }

        }

        #endregion
        public override Job Tick()
        {
            //Mode switching
            if (Settings.AltarSettings.HotkeyMode.PressedOnce())
            {
                Settings.AltarSettings.SwitchMode.Value += 1;
                if (Settings.AltarSettings.SwitchMode.Value == 4) Settings.AltarSettings.SwitchMode.Value = 1;
                switch (Settings.AltarSettings.SwitchMode.Value)
                {
                    case 1:
                        DebugWindow.LogMsg("AltarHelper: Changed to Filter Mode");
                        break;
                    case 2:
                        DebugWindow.LogMsg("AltarHelper: Changed to only Minions and Player Options");
                        break;
                    case 3:
                        DebugWindow.LogMsg("AltarHelper: Changed to only Bosses and Players Options");
                        break;
                }
            }
            CleanCalculatedAltarsDictionary();
            if (!CanRun()) return null;
            CalculateAltars();
            return null;
        }
        public override void AreaChange(AreaInstance area)
        {
            CalculatedAltarDict.Clear();
        }

        private void CalculateAltars()
        {
            var AltarLabels = LabelCache.Value;
            foreach (var altarlabel in AltarLabels)
            {

                Element topOptionLabel = altarlabel.Label?.GetChildAtIndex(0);
                Element bottomOptionLabel = altarlabel.Label?.GetChildAtIndex(1);

                //update some values but skip Weightcalculation
                if (CalculatedAltarDict.ContainsKey(altarlabel.ItemOnGround.Id))
                {
                    //Update the dictionary in case we moved out of the network bubble
                    CalculatedAltarDict.TryGetValue(altarlabel.ItemOnGround.Id, out Altar existingAltar);
                    if (existingAltar != null)
                    {
                        existingAltar.EntityReference = altarlabel.ItemOnGround;
                        existingAltar.TopOptionLabel = topOptionLabel;
                        existingAltar.BottomOptionLabel = bottomOptionLabel;
                        continue;
                    }
                }

                string? topOptionText = topOptionLabel?.GetChildAtIndex(1)?.GetText(512);
                string? bottomOptionText = bottomOptionLabel?.GetChildAtIndex(1)?.GetText(512);
                #region debug
                if (Settings.DebugSettings.DebugRawText == true)
                {
                    DebugWindow.LogError($"AltarBottom Length 512 : {bottomOptionText}");
                    DebugWindow.LogError($"AltarTop Length 512 : {topOptionText}");
                }
                #endregion
                if (topOptionText == null || bottomOptionText == null) continue;

                Altar altar = new(GetSelectionData(topOptionText), GetSelectionData(bottomOptionText), altarlabel.ItemOnGround, topOptionLabel, bottomOptionLabel);
                SetAltarDrawings(altar);
                CalculatedAltarDict.Add(altarlabel.ItemOnGround.Id, altar);


            }
        }
        private void SetAltarDrawings(Altar altar)
        {
            if (altar.TopSelection.UpsideWeight == 0 &&
                    altar.BottomSelection.UpsideWeight == 0 &&
                    altar.TopSelection.DownsideWeight == 0 &&
                    altar.BottomSelection.DownsideWeight == 0) return;

            int topOptionWeight = 0;
            int bottomOptionWeight = 0;
            switch ((SwitchModeEnum)Settings.AltarSettings.SwitchMode.Value)
            {
                case SwitchModeEnum.Filter:
                    topOptionWeight += altar.TopSelection.UpsideWeight - altar.TopSelection.DownsideWeight;
                    bottomOptionWeight += altar.BottomSelection.UpsideWeight - altar.BottomSelection.DownsideWeight;
                    break;
                case SwitchModeEnum.MinionPlayer:
                    if (altar.TopSelection.Target == AffectedTarget.Minions || altar.TopSelection.Target == AffectedTarget.Player)
                    {
                        topOptionWeight += altar.TopSelection.UpsideWeight - altar.TopSelection.DownsideWeight;
                    }
                    if (altar.BottomSelection.Target == AffectedTarget.Minions || altar.BottomSelection.Target == AffectedTarget.Player)
                    {
                        bottomOptionWeight += altar.BottomSelection.UpsideWeight - altar.BottomSelection.DownsideWeight;
                    }
                    break;
                case SwitchModeEnum.BossPlayer:
                    if (altar.TopSelection.Target == AffectedTarget.FinalBoss || altar.TopSelection.Target == AffectedTarget.Player)
                    {
                        topOptionWeight += altar.TopSelection.UpsideWeight - altar.TopSelection.DownsideWeight;
                    }
                    if (altar.BottomSelection.Target == AffectedTarget.FinalBoss || altar.BottomSelection.Target == AffectedTarget.Player)
                    {
                        bottomOptionWeight += altar.BottomSelection.UpsideWeight - altar.BottomSelection.DownsideWeight;
                    }
                    break;
            }
            if (Settings.AltarSettings.WeightSettings.EnableExtraWeight)
            {
                switch (altar.TopSelection.Target)
                {
                    case AffectedTarget.Minions:
                        topOptionWeight += Settings.AltarSettings.WeightSettings.ExtraMinionWeight.Value;
                        break;
                    case AffectedTarget.Player:
                        topOptionWeight += Settings.AltarSettings.WeightSettings.ExtraPlayerWeight.Value;
                        break;
                    case AffectedTarget.FinalBoss:
                        topOptionWeight += Settings.AltarSettings.WeightSettings.ExtraBossWeight.Value;
                        break;
                }
                switch (altar.BottomSelection.Target)
                {
                    case AffectedTarget.Minions:
                        bottomOptionWeight += Settings.AltarSettings.WeightSettings.ExtraMinionWeight.Value;
                        break;
                    case AffectedTarget.Player:
                        bottomOptionWeight += Settings.AltarSettings.WeightSettings.ExtraPlayerWeight.Value;
                        break;
                    case AffectedTarget.FinalBoss:
                        bottomOptionWeight += Settings.AltarSettings.WeightSettings.ExtraBossWeight.Value;
                        break;
                }
            }

            #region debug
            if (Settings.DebugSettings.DebugWeight)
            {
                altar.TopTextDrawing = new TextDrawingSetup(
                    new Vector2(altar.TopOptionLabel.GetClientRectCache.Center.X - 10, altar.TopOptionLabel.GetClientRectCache.Top - 25),
                    Color.Cyan,
                    topOptionWeight.ToString());

                altar.BottomTextDrawing = new TextDrawingSetup(
                    new Vector2(altar.BottomOptionLabel.GetClientRectCache.Center.X - 10, altar.BottomOptionLabel.GetClientRectCache.Top - 25),
                    Color.Cyan,
                    bottomOptionWeight.ToString());
            }
            #endregion
            RectangleDrawingSetup topRectangleDrawing = null;
            RectangleDrawingSetup bottomRectangleDrawing = null;
            if (topOptionWeight < 0)
            {
                topRectangleDrawing = new RectangleDrawingSetup(altar.TopOptionLabel, Settings.AltarSettings.ColorSettings.BadColor, Settings.AltarSettings.FrameThickness);
            }
            if (bottomOptionWeight < 0)
            {
                bottomRectangleDrawing = new RectangleDrawingSetup(altar.BottomOptionLabel, Settings.AltarSettings.ColorSettings.BadColor, Settings.AltarSettings.FrameThickness);
            }
            if (topOptionWeight >= bottomOptionWeight && topOptionWeight > 0)
            {
                topRectangleDrawing = new RectangleDrawingSetup(altar.TopOptionLabel, GetColor(altar.TopSelection.Target), Settings.AltarSettings.FrameThickness.Value);
            }
            if (bottomOptionWeight > topOptionWeight && bottomOptionWeight > 0)
            {
                bottomRectangleDrawing = new RectangleDrawingSetup(altar.BottomOptionLabel, GetColor(altar.BottomSelection.Target), Settings.AltarSettings.FrameThickness);
            }
            altar.TopRectangleDrawing = topRectangleDrawing;
            altar.BottomRectangleDrawing = bottomRectangleDrawing;
        }
        private void CleanCalculatedAltarsDictionary()
        {
            List<uint> removableAltars = new();
            //find inactive altars and remove them from the dictionary
            foreach (var altarID in CalculatedAltarDict.Keys)
            {
                try
                {
                    CalculatedAltarDict.TryGetValue(altarID, out Altar altar);
                    if (altar == null)
                    {
                        removableAltars.Add(altarID);
                        continue;
                    }
                    altar.EntityReference.TryGetComponent(out StateMachine stateMachineComp);
                    if (stateMachineComp == null)
                    {
                        removableAltars.Add(altarID);
                        continue;
                    }
                    const int activated = 0;
                    if (stateMachineComp.States[activated].Value == 1)
                    {
                        removableAltars.Add(altarID);
                    }
                }
                catch (Exception)
                {
                    removableAltars.Add(altarID);
                }
            }
            foreach (var id in removableAltars)
            {
                CalculatedAltarDict.Remove(id);
            }
        }

        #region helperfunctons
        public bool CanRun()
        {
            if (GameController.Area.CurrentArea.IsHideout ||
                GameController.Area.CurrentArea.IsTown ||
                GameController.IngameState.IngameUi == null ||
                GameController.IngameState.IngameUi.ItemsOnGroundLabelsVisible == null ||
                LabelCache.Value.Count < 1)
                return false;
            return true;
        }
        public Color GetColor(AffectedTarget choice)
        {
            Color color = Color.Transparent;
            if (choice == AffectedTarget.Minions) return Settings.AltarSettings.ColorSettings.MinionColor;
            if (choice == AffectedTarget.FinalBoss) return Settings.AltarSettings.ColorSettings.BossColor;
            if (choice == AffectedTarget.Player) return Settings.AltarSettings.ColorSettings.PlayerColor;

            return color;
        }

        public Selection GetSelectionData(string altarLabelText)
        {
            AffectedTarget Target;
            List<string> downsides = new();
            List<string> upsides = new();

            using (StringReader stringreader = new(altarLabelText))
            {
                string targetLine = stringreader.ReadLine();
                Target = AltarModsConstants.AltarTargetDict[targetLine[("<valuedefault>{".Length)..^1]];

                string line;
                bool upsideSectionReached = false;

                while ((line = stringreader.ReadLine()) != null)
                {
                    if (line.StartsWith("<enchanted>"))
                    {
                        upsideSectionReached = true;
                    }
                    if (upsideSectionReached)
                    {
                        if (!line.EndsWith("}"))
                        {
                            //upside split in two lines; only iiq+iir upside has this
                            line += stringreader.ReadLine();
                        }
                        line = line["<enchanted>{".Length..^1];
                        if (line.StartsWith("<rgb"))
                        {
                            line = line[(line.IndexOf('{') + 1)..^1];
                        }
                        //String with range operator to cut trim unneeded tags
                        upsides.Add(line);
                        continue;
                    }
                    downsides.Add(line);
                }
            }

            List<FilterEntry> UpsideFilterEntryMatches = new();
            List<FilterEntry> DownsideFilterEntryMatches = new();

            foreach (string entry in upsides)
            {
                var upside = Regex.Replace(entry, @"((\d+)(?:.\d)|\d+)", "#");

                if (Settings.DebugSettings.DebugBuffs) DebugWindow.LogMsg(upside);
                FilterEntry filterentry = FilterList.FirstOrDefault(element => element.Mod.Contains(upside));
                if (filterentry == null) continue;

                UpsideFilterEntryMatches.Add(filterentry);
                if (Settings.DebugSettings.DebugBuffs) DebugWindow.LogMsg($"Good Mod: {filterentry.Mod}  | Weight {filterentry.Weight}");
            }

            foreach (string entry in downsides)
            {
                if (Settings.DebugSettings.DebugDebuffs) DebugWindow.LogMsg(entry);
                FilterEntry filterentry = FilterList.FirstOrDefault(element => element.Mod.Contains(entry));
                if (filterentry == null) continue;

                DownsideFilterEntryMatches.Add(filterentry);
                if (Settings.DebugSettings.DebugDebuffs) DebugWindow.LogMsg($"Bad Mod: {filterentry.Mod}  | Weight {filterentry.Weight}");
            }

            Selection selection = new()
            {
                Upsides = upsides,
                Downsides = downsides,
                Target = Target,

                UpsideWeight = (UpsideFilterEntryMatches.Count > 0) ? UpsideFilterEntryMatches.Sum(x => x.Weight) : 0,
                DownsideWeight = (DownsideFilterEntryMatches.Count > 0) ? DownsideFilterEntryMatches.Sum(x => x.Weight) : 0,
                BuffGood = (UpsideFilterEntryMatches.FirstOrDefault(x => x.IsUpside) != null),
                DebuffGood = (DownsideFilterEntryMatches.FirstOrDefault(x => x.IsUpside) != null),
            };

            return selection;
        }
        #endregion

        public enum SwitchModeEnum
        {
            Filter = 1,
            MinionPlayer = 2,
            BossPlayer = 3
        }
    }
}
