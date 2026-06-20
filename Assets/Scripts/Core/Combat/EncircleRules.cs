using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 敵旗艦の包囲（フルボッコ）のリング配置ロジック（純ロジック・test-first）。
    /// 配下艦を失い裸になった敵旗艦に、味方配下艦が群がって取り囲むときの各艦の配置点を返す。
    /// <see cref="Squadron"/> が消費する。基準値は持たず幾何だけを担う（実効値パターンの方針）。
    /// </summary>
    public static class EncircleRules
    {
        /// <summary>
        /// 中心 <paramref name="center"/> の周りに <paramref name="total"/> 隻を等間隔で並べたリング上の、
        /// <paramref name="index"/> 番目の点を返す。<paramref name="phase"/> で全体の回転位相をずらせる
        /// （部隊ごとに変えて重なりを散らす）。index は total で巡回する（隻数より多い添字でも安全）。
        /// </summary>
        public static Vector2 RingSlot(Vector2 center, int index, int total, float radius, float phase = 0f)
        {
            if (total < 1) total = 1;
            if (index < 0) index = 0;
            float ang = phase + Mathf.PI * 2f * (index % total) / total;
            float r = Mathf.Max(0f, radius);
            return center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        }
    }
}
