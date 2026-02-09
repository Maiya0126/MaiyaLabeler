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
        // 图标定义
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

            // 注入工具
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

            // 初始化按钮可见性
            if (MaiyaLabelerMod.Settings != null)
            {
                MaiyaLabelerMod.ApplyMainButtonVisibility();
            }
        }
    }

    // Designator 类
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

    // 区域 InspectString 补丁
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

    // 【修改】右下角 PlaySettings 补丁
    // 这里的逻辑也改为调用公共方法，保证同步
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
    public static class Patch_PlaySettings
    {
        static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView) return;

            // 使用当前的设置状态
            bool isActive = MaiyaLabelerMod.Settings.showRoomLabels;

            // 绘制按钮，如果点击了，它会返回 true 并改变 isActive 的值
            row.ToggleableIcon(ref isActive, MaiyaLabelerBootstrapper.IconTool, "MaiyaLabeler_ToggleIconDesc".Translate(), SoundDefOf.Mouseover_ButtonToggle);

            // 如果点击导致状态改变了，我们调用总闸
            if (isActive != MaiyaLabelerMod.Settings.showRoomLabels)
            {
                MaiyaLabelerMod.ToggleVisibility();
            }
        }
    }

    // Settings 类
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

        // 【新增】核心总闸：切换显示状态
        // 任何地方要切换，都必须调这个方法！
        // 在 MaiyaLabelerMod 类中找到这个方法并修改：
        public static void ToggleVisibility()
        {
            bool newState = !Settings.showRoomLabels;
            Settings.showRoomLabels = newState;
            Settings.showZoneLabels = newState;

            Settings.Write();

            if (newState)
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(null);
            else
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);

            // 【核心修复】这里不再硬编码 "ON"/"OFF"，而是使用翻译 Key
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