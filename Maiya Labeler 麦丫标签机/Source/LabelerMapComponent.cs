using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace MaiyaLabeler
{
    public class LabelData : IExposable
    {
        public string customName = "";
        public string customDescription = "";
        public Color? customColor = null;
        public Vector2 offset = Vector2.zero;
        public bool isHidden = false;
        public int fontSize = 0;
        public float opacity = 0.5f;
        public bool showIcon = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref customName, "customName");
            Scribe_Values.Look(ref customDescription, "customDescription", "");
            Scribe_Values.Look(ref customColor, "customColor");
            Scribe_Values.Look(ref offset, "offset");
            Scribe_Values.Look(ref isHidden, "isHidden", false);
            Scribe_Values.Look(ref fontSize, "fontSize", 0);
            Scribe_Values.Look(ref opacity, "opacity", 0.5f);
            Scribe_Values.Look(ref showIcon, "showIcon", true);
        }
    }

    public class LabelerMapComponent : MapComponent
    {
        private Dictionary<int, LabelData> zoneData = new Dictionary<int, LabelData>();
        private Dictionary<IntVec3, LabelData> roomData = new Dictionary<IntVec3, LabelData>();

        private static GUIStyle cachedBaseStyle;
        private float loadTime = -1f;

        public LabelerMapComponent(Map map) : base(map) { }

        public LabelData GetDataForZone(Verse.Zone zone) { if (zone == null) return null; if (zoneData.TryGetValue(zone.ID, out LabelData data)) return data; return null; }
        public LabelData GetOrCreateDataForZone(Verse.Zone zone) { if (zoneData.TryGetValue(zone.ID, out LabelData data)) return data; data = new LabelData(); zoneData[zone.ID] = data; return data; }
        public LabelData GetDataForRoom(Room room) { if (room == null || room.PsychologicallyOutdoors) return null; IntVec3 key = room.Cells.First(); if (roomData.TryGetValue(key, out LabelData data)) return data; return null; }
        public LabelData GetOrCreateDataForRoom(Room room) { if (room == null) return null; IntVec3 key = room.Cells.First(); if (roomData.TryGetValue(key, out LabelData data)) return data; data = new LabelData(); roomData[key] = data; return data; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref zoneData, "zoneData", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref roomData, "roomData", LookMode.Value, LookMode.Deep);
            if (zoneData == null) zoneData = new Dictionary<int, LabelData>();
            if (roomData == null) roomData = new Dictionary<IntVec3, LabelData>();
        }

        // 【核心修复】将按键检测放回 Update，完美解决“按一下触发两次”的问题
        public override void MapComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.CurrentMap != map) return;

            // 1. 配置键 (=)
            if (MaiyaLabelerDefOf.MaiyaLabeler_OpenConfig != null &&
                MaiyaLabelerDefOf.MaiyaLabeler_OpenConfig.JustPressed)
            {
                OpenConfigWindowAt(UI.MouseCell());
            }

            // 2. 开关键 (-)
            if (MaiyaLabelerDefOf.MaiyaLabeler_ToggleVisibility != null &&
                MaiyaLabelerDefOf.MaiyaLabeler_ToggleVisibility.JustPressed)
            {
                MaiyaLabelerMod.ToggleVisibility();
            }
        }

        public override void MapComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.CurrentMap != map) return;

            if (loadTime < 0f) { loadTime = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - loadTime < 3.0f) return;

            // Update 里已经处理按键了，OnGUI 只负责画图

            // --- 绘制逻辑 ---
            if (Find.Camera == null) return;
            if (Find.UIRoot != null && Find.UIRoot.screenshotMode.FiltersCurrentEvent) return;
            if (!MaiyaLabelerMod.Settings.showZoneLabels && !MaiyaLabelerMod.Settings.showRoomLabels) return;

            try { DrawLabelsGUI(); } catch { }
        }

        // ... OpenConfigWindowAt 保持不变 ...
        public static void OpenConfigWindowAt(IntVec3 c)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            Verse.Zone zone = map.zoneManager.ZoneAt(c);
            if (zone != null) { Find.WindowStack.Add(new Dialog_LabelConfig(zone)); return; }
            Room room = c.GetRoom(map);
            if (room != null && !room.PsychologicallyOutdoors)
            {
                if (room.IsDoorway) { Messages.Message("MaiyaLabeler_Msg_IsDoor".Translate(), MessageTypeDefOf.RejectInput, false); return; }
                Find.WindowStack.Add(new Dialog_LabelConfig(room));
            }
            else { Messages.Message("MaiyaLabeler_Msg_NoTarget".Translate(), MessageTypeDefOf.RejectInput, false); }
        }

        // ... DrawLabelsGUI 和 DrawLabel 保持不变 (请务必保留之前的完整代码！) ...
        private void DrawLabelsGUI()
        {
            if (cachedBaseStyle == null) { cachedBaseStyle = new GUIStyle(GUI.skin.label); cachedBaseStyle.alignment = TextAnchor.MiddleCenter; cachedBaseStyle.fontStyle = FontStyle.Bold; }
            CellRect currentView = Find.CameraDriver.CurrentViewRect;
            if (MaiyaLabelerMod.Settings.showZoneLabels)
            {
                List<Verse.Zone> zones = map.zoneManager.AllZones;
                for (int i = 0; i < zones.Count; i++)
                {
                    Verse.Zone zone = zones[i];
                    if (!MaiyaLabelerMod.Settings.showGrowingZoneLabels && zone is Zone_Growing) continue;
                    if (!MaiyaLabelerMod.Settings.showStorageZoneLabels && zone is Zone_Stockpile) continue;
                    if (zone.Cells.Count > 0 && currentView.Contains(zone.Cells[0])) DrawLabel(zone, zone.Cells, zone.label, true);
                }
            }
            if (MaiyaLabelerMod.Settings.showRoomLabels)
            {
                List<Room> rooms = Traverse.Create(map.regionGrid).Field("allRooms").GetValue<List<Room>>();
                if (rooms != null)
                {
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        Room room = rooms[i];
                        if (room.PsychologicallyOutdoors || room.Fogged) continue;
                        if (room.IsDoorway) continue;
                        if (room.Cells.Any() && currentView.Contains(room.Cells.First())) DrawLabel(room, room.Cells, room.Role.LabelCap.ToString(), false);
                    }
                }
            }
        }

        private void DrawLabel(object target, IEnumerable<IntVec3> cells, string defaultText, bool isZone)
        {
            LabelData data = isZone ? GetDataForZone(target as Verse.Zone) : GetDataForRoom(target as Room);
            if (data != null && data.isHidden) return;

            int count = 0; float x = 0, z = 0;
            foreach (var c in cells) { x += c.x; z += c.z; count++; if (count > 60) break; }
            if (count == 0) return;
            Vector3 worldPos = new Vector3(x / count + 0.5f, 0, z / count + 0.5f);

            if (data != null) worldPos += new Vector3(data.offset.x, 0, data.offset.y);
            if (isZone && (data == null || data.offset == Vector2.zero)) worldPos.z -= 0.3f;

            if (Find.Camera == null) return;
            Vector3 screenPos = Find.Camera.WorldToScreenPoint(worldPos);

            screenPos.x /= Prefs.UIScale;
            screenPos.y /= Prefs.UIScale;
            screenPos.y = UI.screenHeight - screenPos.y;

            if (screenPos.z < 0) return;

            Color labelColor = Color.white;
            Texture2D icon = null;
            bool showIcon = (data != null) ? data.showIcon : true;

            if (isZone)
            {
                Verse.Zone zoneTarget = target as Verse.Zone;
                if (zoneTarget is Zone_Growing) { labelColor = MaiyaLabelerMod.Settings.colorGrowingDefault; icon = MaiyaLabelerBootstrapper.IconGrow; if (!MaiyaLabelerMod.Settings.showGrowingIcon) showIcon = false; }
                else { labelColor = MaiyaLabelerMod.Settings.colorStorageDefault; icon = MaiyaLabelerBootstrapper.IconStock; if (!MaiyaLabelerMod.Settings.showStorageIcon) showIcon = false; }
            }
            else { labelColor = MaiyaLabelerMod.Settings.colorRoomDefault; icon = MaiyaLabelerBootstrapper.IconRoom; if (!MaiyaLabelerMod.Settings.showRoomIcon) showIcon = false; }

            if (data != null && !string.IsNullOrEmpty(data.customName)) defaultText = data.customName;
            if (data != null && data.customColor.HasValue) labelColor = data.customColor.Value;

            float opacity = (data != null) ? data.opacity : 0.5f;
            int localFontSize = (data != null) ? data.fontSize : 0;

            GUIStyle style = new GUIStyle(cachedBaseStyle);
            if (localFontSize > 0) style.fontSize = localFontSize;
            else style.fontSize = (int)(12f * MaiyaLabelerMod.Settings.defaultFontSize);

            Color finalColor = new Color(labelColor.r, labelColor.g, labelColor.b, opacity);
            style.normal.textColor = finalColor;

            Vector2 textSize = style.CalcSize(new GUIContent(defaultText));
            float iconSize = style.fontSize + 4f;
            float totalWidth = textSize.x;
            if (showIcon && icon != null) totalWidth += iconSize + 2f;

            Rect totalRect = new Rect(screenPos.x - totalWidth / 2f, screenPos.y - textSize.y / 2f, totalWidth, textSize.y > iconSize ? textSize.y : iconSize);

            float textStartX = totalRect.x;
            if (showIcon && icon != null)
            {
                Rect iconRect = new Rect(totalRect.x, totalRect.y + (totalRect.height - iconSize) / 2f, iconSize, iconSize);
                GUI.color = new Color(0, 0, 0, opacity * 0.5f);
                GUI.DrawTexture(new Rect(iconRect.x + 1, iconRect.y + 1, iconRect.width, iconRect.height), icon);
                GUI.color = new Color(1, 1, 1, opacity);
                GUI.DrawTexture(iconRect, icon);
                textStartX += iconSize + 2f;
            }
            Rect textRect = new Rect(textStartX, totalRect.y + (totalRect.height - textSize.y) / 2f, textSize.x, textSize.y);
            GUI.color = Color.black;
            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = new Color(0, 0, 0, opacity);
            GUI.Label(new Rect(textRect.x + 1, textRect.y + 1, textRect.width, textRect.height), defaultText, shadowStyle);
            GUI.color = Color.white;
            GUI.Label(textRect, defaultText, style);
        }
    }
}