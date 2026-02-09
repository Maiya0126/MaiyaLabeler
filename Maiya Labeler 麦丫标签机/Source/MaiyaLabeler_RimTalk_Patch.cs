using HarmonyLib;
using System;
using System.Reflection;
using Verse;
using RimWorld;
using System.Linq;
using System.Text; // 必须引用这个

namespace MaiyaLabeler
{
    [StaticConstructorOnStartup]
    public static class MaiyaLabeler_RimTalk_Patch
    {
        static MaiyaLabeler_RimTalk_Patch()
        {
            // 1. 检查 Mod 是否存在
            if (!ModsConfig.ActiveModsInLoadOrder.Any(m => m.PackageId != null && m.PackageId.ToLower().Contains("rimtalk")))
                return;

            try
            {
                // 2. 反射获取类
                Type targetType = AccessTools.TypeByName("RimTalk.Service.ContextBuilder");
                if (targetType == null) return;

                // 3. 反射获取方法 (这是 void 方法)
                MethodInfo targetMethod = AccessTools.Method(targetType, "BuildLocationContext");
                if (targetMethod == null) return;

                // 4. 挂载补丁
                var harmony = new Harmony("com.maiya.labeler.rimtalk_integration");
                harmony.Patch(targetMethod, postfix: new HarmonyMethod(typeof(MaiyaLabeler_RimTalk_Patch), nameof(Postfix_BuildLocationContext)));

                Log.Message("[Maiya Labeler] RimTalk 联动补丁挂载成功！(StringBuilder模式)");
            }
            catch (Exception ex)
            {
                Log.Error("[Maiya Labeler] RimTalk 联动挂载失败: " + ex.Message);
            }
        }

        // --- 核心补丁 ---
        // 方法签名：void BuildLocationContext(StringBuilder sb, ContextSettings settings, Pawn mainPawn)
        public static void Postfix_BuildLocationContext(object[] __args)
        {
            // 1. 安全检查参数数量
            if (__args == null || __args.Length < 3) return;

            // 2. 获取 StringBuilder (它是被修改的对象)
            StringBuilder sb = __args[0] as StringBuilder;
            if (sb == null || sb.Length == 0) return;

            // 3. 获取小人 (第三个参数是 Pawn)
            Pawn pawn = __args[2] as Pawn;
            if (pawn == null || pawn.Map == null) return;

            // 4. 获取房间与自定义名字
            Room room = pawn.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return;

            var comp = pawn.Map.GetComponent<LabelerMapComponent>();
            if (comp == null) return;

            var data = comp.GetDataForRoom(room);
            if (data == null || string.IsNullOrEmpty(data.customName)) return;

            // 5. --- 执行替换 ---
            string originalName = room.Role.LabelCap.ToString(); // "餐厅"
            string originalNameLower = room.Role.label;         // "餐厅"

            // 这里的 Replace 会直接修改 RimTalk 正在构建的那个字符串对象
            // StringBuilder.Replace 是高效且直接的
            bool replaced = false;

            // 把 StringBuilder 转成 string 检查一下包含关系 (为了性能，只在需要时替换)
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

            // 【保底措施】
            // 如果 Replace 没成功 (比如 RimTalk 用了奇怪的格式拼接)，强制追加
            // 检查是否已经包含了新名字，防止重复追加
            if (!replaced && !sb.ToString().Contains(data.customName))
            {
                // RimTalk 的格式通常是 "Location: 室内;21C;餐厅"
                // 我们追加在后面 "; 米其林三星餐厅"
                sb.Append($"; {data.customName}");
            }
        }
    }
}