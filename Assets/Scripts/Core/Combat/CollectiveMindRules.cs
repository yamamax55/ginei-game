using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 集合意識型ドクトリン（ENDR-4 #2359）。女王/中核が全艦を一斉統制する種族＝
    /// 個艦が同調するほど火力が二乗的に伸びる（完全同調でボーナス最大）が、
    /// 中核（旗艦＝女王）が落ちると意思の糸が切れて崩壊する（後継者が立たない＝指揮継承不能）。
    /// 集団依存/自律分散（<see cref="CommandDoctrine"/>）とは別系統の第3ドクトリン。純ロジック・test-first。
    /// </summary>
    public static class CollectiveMindRules
    {
        /// <summary>同調が完全(intactRatio=1)のときの戦闘ボーナス最大値（係数 1+この値が上限）。</summary>
        public const float MaxSyncBonus = 0.5f;

        /// <summary>
        /// 集合意識の同調ボーナス係数。集合意識型のみ有効＝健在な艦の比率(0..1)に比例し
        /// 1.0〜(1+MaxSyncBonus) を返す（完全同調=1.0 で最大）。他ドクトリンは常に 1.0。
        /// </summary>
        public static float CollectiveSyncBonus(CommandDoctrine doctrine, float intactRatio)
        {
            if (doctrine != CommandDoctrine.集合意識型) return 1f;
            return 1f + MaxSyncBonus * Mathf.Clamp01(intactRatio);
        }

        /// <summary>
        /// 中核崩壊リスク。中核（女王=旗艦）の健全度(0..1)が下がるほど高まる＝
        /// 満タンで 0、瀕死で 1 に近づく（中核が落ちると集合意識は霧散する）。
        /// </summary>
        public static float CoreCollapseRisk(float flagshipHealthRatio)
        {
            return 1f - Mathf.Clamp01(flagshipHealthRatio);
        }

        /// <summary>
        /// 指揮継承の可否。集合意識型は個としての後継者を持たない（中核喪失＝即崩壊）ので false。
        /// 集団依存/自律分散は副官等が継承しうるので true。
        /// </summary>
        public static bool IsSuccessionPossible(CommandDoctrine doctrine)
        {
            return doctrine != CommandDoctrine.集合意識型;
        }
    }
}
