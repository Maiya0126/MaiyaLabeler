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

        public LabelData() { }

        public void ExposeData()
        {
            Scribe_Values.Look(ref customName, "customName", "");
            Scribe_Values.Look(ref customDescription, "customDescription", "");
            Scribe_Values.Look(ref customColor, "customColor");
            Scribe_Values.Look(ref offset, "offset", Vector2.zero);
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

        public LabelData GetDataForRoom(Room room)
        {
            if (room == null || room.PsychologicallyOutdoors || room.Map == null) return null;

            // 1. 优先查本地字典缓存（最快）
            IntVec3 key = room.Cells.First();
            if (roomData.TryGetValue(key, out LabelData cachedData)) return cachedData;

            // 2. 飞船落地恢复逻辑：只扫描纯内部地砖，绝对不碰墙和门
            foreach (IntVec3 c in room.Cells)
            {
                List<Thing> things = c.GetThingList(room.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t is Building b && !(b is Building_Door))
                    {
                        var comp = b.GetComp<CompRoomAnchor>();
                        if (comp != null && comp.savedData != null && (!string.IsNullOrEmpty(comp.savedData.customName) || !string.IsNullOrEmpty(comp.savedData.customDescription)))
                        {
                            // 发现有效数据！直接设为本房间的新本体
                            LabelData recovered = comp.savedData;
                            roomData[key] = recovered;

                            // 立刻感染本房间的所有建筑，确保后续新建家具也能跟上
                            SyncRoomBuildings(room, recovered);

                            return recovered;
                        }
                    }
                }
            }
            return null;
        }

        public LabelData GetOrCreateDataForRoom(Room room)
        {
            if (room == null || room.Map == null) return null;

            LabelData existingData = GetDataForRoom(room);
            IntVec3 key = room.Cells.First();

            if (existingData != null)
            {
                SyncRoomBuildings(room, existingData);
                return existingData;
            }

            LabelData newData = new LabelData();
            roomData[key] = newData;

            // 实时同步本体，你在窗口敲字，家具上立刻生效！
            SyncRoomBuildings(room, newData);

            return newData;
        }

        // 【核心工具】强行把数据塞给房间内部的所有家具
        private void SyncRoomBuildings(Room room, LabelData data)
        {
            if (room == null || room.Map == null) return;
            foreach (IntVec3 c in room.Cells)
            {
                List<Thing> things = c.GetThingList(room.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t is Building b && !(b is Building_Door))
                    {
                        var comp = b.GetComp<CompRoomAnchor>();
                        if (comp != null) comp.savedData = data;
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref zoneData, "zoneData", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref roomData, "roomData", LookMode.Value, LookMode.Deep);
            if (zoneData == null) zoneData = new Dictionary<int, LabelData>();
            if (roomData == null) roomData = new Dictionary<IntVec3, LabelData>();
        }

        public override void MapComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.CurrentMap != map) return;

            // 【静默同步 & 防崩锁】每120帧(约2秒)自动备份一次
            // 使用 .ToList() 创建副本进行遍历，防止字典被修改时造成崩溃
            if (Find.TickManager.TicksGame % 120 == 0)
            {
                var currentRooms = roomData.ToList();
                foreach (var kvp in currentRooms)
                {
                    Room r = kvp.Key.GetRoom(this.map);
                    if (r != null && !r.PsychologicallyOutdoors)
                    {
                        SyncRoomBuildings(r, kvp.Value);
                    }
                }
            }

            if (MaiyaLabelerDefOf.MaiyaLabeler_OpenConfig != null && MaiyaLabelerDefOf.MaiyaLabeler_OpenConfig.JustPressed)
                OpenConfigWindowAt(UI.MouseCell());

            if (MaiyaLabelerDefOf.MaiyaLabeler_ToggleVisibility != null && MaiyaLabelerDefOf.MaiyaLabeler_ToggleVisibility.JustPressed)
                MaiyaLabelerMod.ToggleVisibility();
        }

        public override void MapComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.CurrentMap != map) return;
            if (loadTime < 0f) { loadTime = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - loadTime < 3.0f) return;
            if (Find.Camera == null) return;
            if (Find.UIRoot != null && Find.UIRoot.screenshotMode.FiltersCurrentEvent) return;
            if (!MaiyaLabelerMod.Settings.showZoneLabels && !MaiyaLabelerMod.Settings.showRoomLabels) return;

            try { DrawLabelsGUI(); } catch { }
        }

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
            float worldZ = z / count + 0.5f - 0.4f;
            Vector3 worldPos = new Vector3(x / count + 0.5f, 0, worldZ);

            if (data != null) worldPos += new Vector3(data.offset.x, 0, data.offset.y);
            if (isZone && (data == null || data.offset == Vector2.zero)) worldPos.z -= 0.3f;

            if (Find.Camera == null) return;
            Vector3 screenPixelPos = Find.Camera.WorldToScreenPoint(worldPos);

            if (screenPixelPos.z < 0) return;

            Vector2 screenPos = new Vector2(
                screenPixelPos.x / Prefs.UIScale,
                (float)UI.screenHeight - screenPixelPos.y / Prefs.UIScale
            );

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