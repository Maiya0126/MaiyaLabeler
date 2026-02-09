using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace MaiyaLabeler
{
    public class Dialog_LabelConfig : Window
    {
        private Verse.Zone zone;
        private Room room;
        private LabelData data;

        public static readonly List<Color> PresetColors = new List<Color>
        {
            Color.white, Color.gray, Color.black, Color.red,
            Color.green, Color.blue, Color.cyan, Color.magenta,
            Color.yellow, new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 0.5f), new Color(0f, 0.5f, 0f),
            new Color(0.5f, 0.25f, 0f), new Color(1f, 0.8f, 0.8f), new Color(0.6f, 0.8f, 1f), new Color(0.8f, 1f, 0.6f)
        };

        public override Vector2 InitialSize => new Vector2(420f, 700f);

        public Dialog_LabelConfig(Verse.Zone zone)
        {
            this.zone = zone;
            this.draggable = true; this.doCloseX = true; this.preventCameraMotion = false;
            InitData();
        }

        public Dialog_LabelConfig(Room room)
        {
            this.room = room;
            this.draggable = true; this.doCloseX = true; this.preventCameraMotion = false;
            InitData();
        }

        private void InitData()
        {
            var comp = Find.CurrentMap.GetComponent<LabelerMapComponent>();
            if (zone != null) data = comp.GetOrCreateDataForZone(zone);
            else if (room != null) data = comp.GetOrCreateDataForRoom(room);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            string title = zone != null ? zone.label : room.Role.LabelCap.ToString();
            list.Label("MaiyaLabeler_ConfigTitle".Translate(title));
            list.GapLine();

            // 1. 自定义名称
            list.Label("MaiyaLabeler_ConfigCustomName".Translate());
            string newName = list.TextEntry(data.customName);
            if (newName != data.customName) data.customName = newName;

            list.Gap();

            // 2. 自定义描述 (修复报错的部分)
            list.Label("MaiyaLabeler_ConfigCustomDesc".Translate());

            // 【核心修复】Listing_Standard 没有 TextArea，我们要手动画
            float descHeight = Text.LineHeight * 3f; // 计算3行的高度
            Rect descRect = list.GetRect(descHeight); // 向列表申请这块区域
            string newDesc = Widgets.TextArea(descRect, data.customDescription ?? ""); // 在区域里画多行输入框
            if (newDesc != data.customDescription) data.customDescription = newDesc;

            list.Gap();

            // 3. 颜色选择
            list.Label("MaiyaLabeler_ConfigColor".Translate());
            DrawColorGrid(list, data.customColor ?? Color.white, (c) => data.customColor = c);

            list.Gap();
            // 透明度
            list.Label("MaiyaLabeler_ConfigOpacity".Translate(data.opacity.ToStringPercent()));
            data.opacity = list.Slider(data.opacity, 0.1f, 1.0f);

            list.Gap();
            // 字号
            string sizeLabel = data.fontSize == 0 ? "MaiyaLabeler_ConfigFontSize_Global".Translate() : "MaiyaLabeler_ConfigFontSize_Custom".Translate(data.fontSize);
            list.Label(sizeLabel);
            float sizeVal = (float)data.fontSize;
            sizeVal = list.Slider(sizeVal, 0f, 60f);
            if (sizeVal < 8f) sizeVal = 0f;
            data.fontSize = (int)sizeVal;

            list.Gap();
            // 偏移
            list.Label("MaiyaLabeler_ConfigOffsetX".Translate(data.offset.x.ToString("F1")));
            data.offset.x = list.Slider(data.offset.x, -3f, 3f);
            list.Label("MaiyaLabeler_ConfigOffsetY".Translate(data.offset.y.ToString("F1")));
            data.offset.y = list.Slider(data.offset.y, -3f, 3f);

            bool visible = !data.isHidden;
            list.CheckboxLabeled("MaiyaLabeler_ConfigEnable".Translate(), ref visible);
            data.isHidden = !visible;

            list.CheckboxLabeled("MaiyaLabeler_ConfigShowIcon".Translate(), ref data.showIcon);

            list.GapLine();

            Rect bottomRect = list.GetRect(30f);
            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, 120f, 30f), "MaiyaLabeler_ConfigReset".Translate()))
            {
                data.customName = "";
                data.customDescription = "";
                data.customColor = null;
                data.opacity = 0.8f; // 让这里保持默认0.8，或者你可以改成 0.5f，看你喜好
                data.fontSize = 0;
                data.offset = Vector2.zero;
                data.isHidden = false;
                data.showIcon = true;
            }

            if (Widgets.ButtonText(new Rect(bottomRect.xMax - 100f, bottomRect.y, 100f, 30f), "MaiyaLabeler_ConfigClose".Translate()))
            {
                Close();
            }

            list.End();
        }

        public static void DrawColorGrid(Listing_Standard list, Color current, System.Action<Color> onSelect)
        {
            float rowHeight = 24f;
            int cols = 8;
            float colWidth = (list.ColumnWidth - 10f) / cols;
            Rect rect = list.GetRect(rowHeight * 2 + 5f);
            for (int i = 0; i < PresetColors.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                Rect colorRect = new Rect(rect.x + col * colWidth, rect.y + row * rowHeight, colWidth - 4f, rowHeight - 4f);
                Widgets.DrawBoxSolid(colorRect, PresetColors[i]);
                if (IsColorSimilar(current, PresetColors[i])) Widgets.DrawBox(colorRect.ExpandedBy(2f), 2);
                if (Widgets.ButtonInvisible(colorRect)) onSelect(PresetColors[i]);
            }
        }

        private static bool IsColorSimilar(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }

    public class Dialog_ChooseColor : Window
    {
        private System.Action<Color> onSelect;
        private Color currentColor;

        public override Vector2 InitialSize => new Vector2(350f, 180f);

        public Dialog_ChooseColor(System.Action<Color> onSelect, Color current)
        {
            this.onSelect = onSelect;
            this.currentColor = current;
            this.doCloseX = true;
            this.forcePause = false;
            this.draggable = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("MaiyaLabeler_Settings_ChooseColorTitle".Translate());
            list.Gap();
            Dialog_LabelConfig.DrawColorGrid(list, currentColor, (col) =>
            {
                onSelect(col);
                currentColor = col;
            });
            list.Gap();
            if (list.ButtonText("MaiyaLabeler_Settings_Confirm".Translate()))
            {
                Close();
            }
            list.End();
        }
    }
}