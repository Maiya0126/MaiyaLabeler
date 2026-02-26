using Verse;
using RimWorld;

namespace MaiyaLabeler
{
    public class CompProperties_RoomAnchor : CompProperties
    {
        public CompProperties_RoomAnchor()
        {
            this.compClass = typeof(CompRoomAnchor);
        }
    }

    public class CompRoomAnchor : ThingComp
    {
        public LabelData savedData;

        public override void PostExposeData()
        {
            base.PostExposeData();
            // 让标签数据跟着这个建筑存读档（飞船转移物品时也会触发）
            Scribe_Deep.Look(ref savedData, "maiyaLabelData");
        }
    }
}