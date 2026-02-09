using HarmonyLib;
using System;
using System.Reflection;
using Verse;
using RimWorld;
using System.Linq;
using System.Text;

namespace MaiyaLabeler
{
    [StaticConstructorOnStartup]
    public static class MaiyaLabeler_RimTalk_Patch
    {
        static MaiyaLabeler_RimTalk_Patch()
        {
            if (!ModsConfig.ActiveModsInLoadOrder.Any(m => m.PackageId != null && m.PackageId.ToLower().Contains("rimtalk")))
                return;

            try
            {
                Type targetType = AccessTools.TypeByName("RimTalk.Service.ContextBuilder");
                if (targetType == null) return;

                MethodInfo targetMethod = AccessTools.Method(targetType, "BuildLocationContext");
                if (targetMethod == null) return;

                var harmony = new Harmony("com.maiya.labeler.rimtalk_integration");
                harmony.Patch(targetMethod, postfix: new HarmonyMethod(typeof(MaiyaLabeler_RimTalk_Patch), nameof(Postfix_BuildLocationContext)));

                Log.Message("[Maiya Labeler] RimTalk 联动补丁挂载成功！(Description Support)");
            }
            catch (Exception ex)
            {
                Log.Error("[Maiya Labeler] RimTalk 联动挂载失败: " + ex.Message);
            }
        }

        public static void Postfix_BuildLocationContext(object[] __args)
        {
            if (__args == null || __args.Length < 3) return;

            StringBuilder sb = __args[0] as StringBuilder;
            if (sb == null || sb.Length == 0) return;

            Pawn pawn = __args[2] as Pawn;
            if (pawn == null || pawn.Map == null) return;

            Room room = pawn.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return;

            var comp = pawn.Map.GetComponent<LabelerMapComponent>();
            if (comp == null) return;

            var data = comp.GetDataForRoom(room);
            // 如果既没有名字也没有描述，就直接退出
            if (data == null || (string.IsNullOrEmpty(data.customName) && string.IsNullOrEmpty(data.customDescription))) return;

            // 1. 替换名字 (逻辑保持不变)
            if (!string.IsNullOrEmpty(data.customName))
            {
                string originalName = room.Role.LabelCap.ToString();
                string originalNameLower = room.Role.label;

                bool replaced = false;
                string currentText = sb.ToString();

                if (currentText.Contains(originalName))
                {
                    sb.Replace(originalName, data.customName);
                    replaced = true;
                }
                else if (currentText.Contains(originalNameLower))
                {
                    sb.Replace(originalNameLower, data.customName);
                    replaced = true;
                }

                if (!replaced && !sb.ToString().Contains(data.customName))
                {
                    sb.Append($"; {data.customName}");
                }
            }

            // 2. 【核心新增】追加描述
            // 我们在上下文末尾追加一行 "Context: [描述]"
            // RimTalk 的 AI 会读取这些内容作为环境背景
            if (!string.IsNullOrEmpty(data.customDescription))
            {
                sb.Append($"\n(Location Context: {data.customDescription})");
            }
        }
    }
}