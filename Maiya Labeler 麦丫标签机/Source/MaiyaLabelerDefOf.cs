using RimWorld;
using Verse;

namespace MaiyaLabeler
{
    [DefOf]
    public static class MaiyaLabelerDefOf
    {
        // 自动绑定 XML 中的 Def
        public static KeyBindingDef MaiyaLabeler_OpenConfig;
        public static KeyBindingDef MaiyaLabeler_ToggleVisibility;

        // 注意：MainButtonDef 不需要在这里定义，因为 Worker 会自动处理
        // 如果这里报错，说明 XML 里名字没对上，但通常这里只需要 KeyBinding

        static MaiyaLabelerDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MaiyaLabelerDefOf));
        }
    }
}