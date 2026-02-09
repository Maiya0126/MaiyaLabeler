using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MaiyaLabeler
{
    public class MainButtonWorker_Labeler : MainButtonWorker
    {
        public override void DoButton(Rect rect)
        {
            if (!MaiyaLabelerMod.Settings.showMainTab)
                return;

            // 【核心修复】必须先检测右键，并且放在 base.DoButton 之前！
            // 否则 base.DoButton 会吃掉点击事件，导致右键检测失效。
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(rect))
            {
                Event.current.Use(); // 吃掉事件

                // 调用总闸，确保和快捷键效果一模一样
                MaiyaLabelerMod.ToggleVisibility();

                return; // 拦截成功，不再执行后续
            }

            base.DoButton(rect);
        }

        public override void Activate()
        {
            // 左键逻辑
            try
            {
                if (Find.ReverseDesignatorDatabase != null)
                {
                    Designator_LabelConfigTool tool = Find.ReverseDesignatorDatabase.AllDesignators
                        .Find(d => d is Designator_LabelConfigTool) as Designator_LabelConfigTool;

                    if (tool != null)
                    {
                        Find.DesignatorManager.Select(tool);
                        SoundDefOf.Click.PlayOneShotOnCamera(null);
                        return;
                    }
                }
            }
            catch { }

            DesignationCategoryDef zoneCat = DefDatabase<DesignationCategoryDef>.GetNamed("Zone");
            if (zoneCat != null)
            {
                List<Designator> designators = Traverse.Create(zoneCat).Field("resolvedDesignators").GetValue<List<Designator>>();
                if (designators != null)
                {
                    var gizmo = designators.Find(d => d is Designator_LabelConfigTool);
                    if (gizmo != null)
                    {
                        Find.DesignatorManager.Select(gizmo);
                        SoundDefOf.Click.PlayOneShotOnCamera(null);
                    }
                }
            }
        }
    }
}