using UnityEngine;
using Verse;
using Verse.Sound;
using HarmonyLib;
using System.Reflection;
using RimWorld;
using System.Collections.Generic;

namespace MaiyaLabeler
{
    [StaticConstructorOnStartup]
    public static class MaiyaLabelerBootstrapper
    {
        public static readonly Texture2D IconTool = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", false);

        public static readonly Texture2D IconRoom =
            ContentFinder<Texture2D>.Get("UI/Designators/BuildRoofArea", false) ??
            ContentFinder<Texture2D>.Get("UI/Designators/HomeAreaOn", false);

        public static readonly Texture2D IconGrow =
            ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Growing", false) ??
            ContentFinder<Texture2D>.Get("UI/Designators/Plan", false);

        public static readonly Texture2D IconStock =
            ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false) ??
            ContentFinder<Texture2D>.Get("UI/Designators/Plan", false);

        static MaiyaLabelerBootstrapper()
        {
            var harmony = new Harmony("com.maiya.labeler");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // 【致命崩盘修复】防死循环判定！
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.category == ThingCategory.Building)
                {
                    if (def.comps == null) def.comps = new List<CompProperties>();

                    // 必须判定：只有在没有锚点的情况下才添加，防止共享列表被无限塞入导致内存溢出！
                    if (!def.comps.Any(c => c is CompProperties_RoomAnchor))
                    {
                        def.comps.Add(new CompProperties_RoomAnchor());
                    }
                }
            }

            DesignationCategoryDef zoneCategory = DefDatabase<DesignationCategoryDef>.GetNamed("Zone");
            if (zoneCategory != null)
            {
                var field = Traverse.Create(zoneCategory).Field("resolvedDesignators");
                if (field.FieldExists())
                {
                    List<Designator> list = field.GetValue<List<Designator>>();
                    if (list != null) list.Add(new Designator_LabelConfigTool());
                }
            }

            if (MaiyaLabelerMod.Settings != null)
            {
                MaiyaLabelerMod.ApplyMainButtonVisibility();
            }
        }
    }

    public class Designator_LabelConfigTool : Designator
    {
        public Designator_LabelConfigTool()
        {
            this.defaultLabel = "MaiyaLabeler_ToolLabel".Translate();
            this.defaultDesc = "MaiyaLabeler_ToolDesc".Translate();
            this.icon = MaiyaLabelerBootstrapper.IconTool;
            this.soundDragSustain = null;
            this.soundDragChanged = null;
            this.useMouseIcon = true;
            this.soundSucceeded = SoundDefOf.Click;
        }
        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            if (Map.zoneManager.ZoneAt(c) != null) return true;
            if (c.GetRoom(Map) != null) return true;
            return "MaiyaLabeler_Msg_NoTarget".Translate();
        }
        public override void DesignateSingleCell(IntVec3 c) => LabelerMapComponent.OpenConfigWindowAt(c);
    }

    [HarmonyPatch(typeof(Verse.Zone), "GetInspectString")]
    public static class Patch_Zone_GetInspectString
    {
        static void Postfix(Verse.Zone __instance, ref string __result)
        {
            if (__instance == null || __instance.Map == null) return;
            var comp = __instance.Map.GetComponent<LabelerMapComponent>();
            if (comp == null) return;
            var data = comp.GetDataForZone(__instance);

            if (data != null)
            {
                if (!string.IsNullOrEmpty(data.customName))
                {
                    if (!string.IsNullOrEmpty(__result)) __result += "\n";
                    __result += $"{"MaiyaLabeler_NamePrefix".Translate()}: {data.customName}";
                }
                if (!string.IsNullOrEmpty(data.customDescription))
                {
                    if (!string.IsNullOrEmpty(__result)) __result += "\n";
                    __result += $"{"MaiyaLabeler_DescPrefix".Translate()}: {data.customDescription}";
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
    public static class Patch_PlaySettings
    {
        static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView) return;
            bool isActive = MaiyaLabelerMod.Settings.showRoomLabels;
            row.ToggleableIcon(ref isActive, MaiyaLabelerBootstrapper.IconTool, "MaiyaLabeler_ToggleIconDesc".Translate(), SoundDefOf.Mouseover_ButtonToggle);
            if (isActive != MaiyaLabelerMod.Settings.showRoomLabels)
            {
                MaiyaLabelerMod.ToggleVisibility();
            }
        }
    }

    public class LabelerSettings : ModSettings
    {
        public bool showRoomLabels = true;
        public bool showZoneLabels = true;
        public bool showGrowingZoneLabels = true;
        public bool showStorageZoneLabels = true;
        public bool showMainTab = true;

        public float defaultFontSize = 1.0f;
        public bool showRoomIcon = true;
        public bool showGrowingIcon = true;
        public bool showStorageIcon = true;
        public Color colorRoomDefault = new Color(1f, 0.88f, 0.6f);
        public Color colorGrowingDefault = new Color(0.8f, 1f, 0.6f);
        public Color colorStorageDefault = new Color(0.6f, 0.9f, 1f);

        public override void ExposeData()
        {
            Scribe_Values.Look(ref showRoomLabels, "showRoomLabels", true);
            Scribe_Values.Look(ref showZoneLabels, "showZoneLabels", true);
            Scribe_Values.Look(ref showGrowingZoneLabels, "showGrowingZoneLabels", true);
            Scribe_Values.Look(ref showStorageZoneLabels, "showStorageZoneLabels", true);
            Scribe_Values.Look(ref showMainTab, "showMainTab", true);

            Scribe_Values.Look(ref defaultFontSize, "defaultFontSize", 1.0f);
            Scribe_Values.Look(ref showRoomIcon, "showRoomIcon", true);
            Scribe_Values.Look(ref showGrowingIcon, "showGrowingIcon", true);
            Scribe_Values.Look(ref showStorageIcon, "showStorageIcon", true);
            Scribe_Values.Look(ref colorRoomDefault, "colorRoomDefault", new Color(1f, 0.88f, 0.6f));
            Scribe_Values.Look(ref colorGrowingDefault, "colorGrowingDefault", new Color(0.8f, 1f, 0.6f));
            Scribe_Values.Look(ref colorStorageDefault, "colorStorageDefault", new Color(0.6f, 0.9f, 1f));
            base.ExposeData();
        }
    }

    public class MaiyaLabelerMod : Mod
    {
        public static LabelerSettings Settings;
        public static LabelerMapComponent MapComp => Find.CurrentMap?.GetComponent<LabelerMapComponent>();

        public MaiyaLabelerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LabelerSettings>();
        }

        public static void ToggleVisibility()
        {
            bool newState = !Settings.showRoomLabels;
            Settings.showRoomLabels = newState;
            Settings.showZoneLabels = newState;

            Settings.Write();

            if (newState) SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(null);
            else SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);

            string statusKey = newState ? "MaiyaLabeler_Status_On" : "MaiyaLabeler_Status_Off";
            Messages.Message("MaiyaLabeler_Status_Toggle".Translate(statusKey.Translate()), MessageTypeDefOf.SilentInput, false);
        }

        public static void ApplyMainButtonVisibility()
        {
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamed("MaiyaLabeler_MainTab", false);
            if (def != null)
            {
                def.buttonVisible = Settings.showMainTab;
            }
        }

        public override string SettingsCategory() => "MaiyaLabeler_ModName".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("MaiyaLabeler_Section_Display".Translate());

            bool oldShowTab = Settings.showMainTab;
            list.CheckboxLabeled("MaiyaLabeler_ShowMainTab".Translate(), ref Settings.showMainTab);
            if (Settings.showMainTab != oldShowTab) ApplyMainButtonVisibility();

            list.GapLine();

            list.CheckboxLabeled("MaiyaLabeler_ShowRoom".Translate(), ref Settings.showRoomLabels);
            list.CheckboxLabeled("MaiyaLabeler_ShowZone".Translate(), ref Settings.showZoneLabels);
            if (Settings.showZoneLabels)
            {
                list.Gap(4f);
                list.CheckboxLabeled("MaiyaLabeler_ShowGrow".Translate(), ref Settings.showGrowingZoneLabels);
                list.CheckboxLabeled("MaiyaLabeler_ShowStock".Translate(), ref Settings.showStorageZoneLabels);
            }
            list.Gap();

            list.Label("MaiyaLabeler_Section_Style".Translate());
            DrawSettingRow(list, "MaiyaLabeler_TypeRoom", ref Settings.showRoomIcon, ref Settings.colorRoomDefault);
            DrawSettingRow(list, "MaiyaLabeler_TypeGrow", ref Settings.showGrowingIcon, ref Settings.colorGrowingDefault);
            DrawSettingRow(list, "MaiyaLabeler_TypeStock", ref Settings.showStorageIcon, ref Settings.colorStorageDefault);

            list.GapLine();
            list.Label("MaiyaLabeler_GlobalFontSize".Translate(Settings.defaultFontSize.ToStringPercent()));
            Settings.defaultFontSize = list.Slider(Settings.defaultFontSize, 0.5f, 3.0f);

            list.End();
            Settings.Write();
        }

        private void DrawSettingRow(Listing_Standard list, string labelKey, ref bool iconToggle, ref Color color)
        {
            Rect rect = list.GetRect(30f);
            Rect checkRect = new Rect(rect.x, rect.y, 250f, 30f);
            string labelText = labelKey.Translate();
            Widgets.CheckboxLabeled(checkRect, "MaiyaLabeler_ShowTypeIcon".Translate(labelText), ref iconToggle);
            Rect labelRect = new Rect(rect.x + 260f, rect.y + 5f, 80f, 30f);
            Widgets.Label(labelRect, "MaiyaLabeler_DefaultColor".Translate());
            Rect colorRect = new Rect(rect.x + 350f, rect.y + 2f, 26f, 26f);
            Widgets.DrawBoxSolid(colorRect, color);
            Widgets.DrawBox(colorRect);
            if (Mouse.IsOver(colorRect))
            {
                Widgets.DrawHighlight(colorRect);
                TooltipHandler.TipRegion(colorRect, "MaiyaLabeler_Tip_EditColor".Translate());
            }
            if (Widgets.ButtonInvisible(colorRect))
            {
                Find.WindowStack.Add(new Dialog_ChooseColor(
                    (newCol) =>
                    {
                        if (labelKey == "MaiyaLabeler_TypeRoom") Settings.colorRoomDefault = newCol;
                        else if (labelKey == "MaiyaLabeler_TypeGrow") Settings.colorGrowingDefault = newCol;
                        else if (labelKey == "MaiyaLabeler_TypeStock") Settings.colorStorageDefault = newCol;
                    },
                    color
                ));
            }
            if (Widgets.ButtonText(new Rect(rect.x + 390f, rect.y, 100f, 30f), "MaiyaLabeler_ResetColor".Translate()))
            {
                if (labelKey == "MaiyaLabeler_TypeRoom") color = new Color(1f, 0.88f, 0.6f);
                else if (labelKey == "MaiyaLabeler_TypeGrow") color = new Color(0.8f, 1f, 0.6f);
                else if (labelKey == "MaiyaLabeler_TypeStock") color = new Color(0.6f, 0.9f, 1f);
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }
        }
    }
}