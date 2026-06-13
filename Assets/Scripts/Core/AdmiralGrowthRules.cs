using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の成長を実効能力へ繋ぐ唯一の窓口（ADM-2 #2303）。`GrowthRules`#537-543（経験→ボーナス）と
    /// `TenchijinRules`#軍神（天地人で限界突破）を束ね、「基準能力＋成長ぶん（軍神は100超）」を返す。
    /// 数式は両者へ委譲し二重実装しない。基準フィールドは非破壊（実効値パターン）。test-first。
    /// </summary>
    public static class AdmiralGrowthRules
    {
        /// <summary>基準能力＋成長ボーナス（軍神でない通常の伸び・上限100）。growth が null なら基準そのまま。</summary>
        public static int GrownStat(int baseStat, Growth growth)
        {
            int b = Mathf.Clamp(baseStat, 0, AdmiralData.MaxStatValue);
            if (growth == null) return b;
            return b + GrowthRules.EffectiveStatBonus(growth, baseStat);
        }

        /// <summary>
        /// 基準能力＋成長ボーナス（軍神＝<see cref="AdmiralData.isTranscendent"/> は天地人が揃えば100超）。
        /// 限界突破の上限/実現は <see cref="TenchijinRules"/> に委譲（数式は二重実装しない）。
        /// </summary>
        public static int GrownStat(int baseStat, Growth growth, AdmiralData admiral, Tenchijin tenchijin)
            => TenchijinRules.EffectiveStat(baseStat, growth, admiral, tenchijin);

        /// <summary>提督の成長後の攻撃（軍神＋天地人で100超）。基準は admiral.attack（参謀補完は別系統）。</summary>
        public static int Attack(AdmiralData admiral, Tenchijin tenchijin)
            => admiral == null ? 0 : GrownStat(admiral.attack, admiral.growth, admiral, tenchijin);

        /// <summary>提督の成長後の統率。</summary>
        public static int Leadership(AdmiralData admiral, Tenchijin tenchijin)
            => admiral == null ? 0 : GrownStat(admiral.leadership, admiral.growth, admiral, tenchijin);

        /// <summary>提督の成長後の防御。</summary>
        public static int Defense(AdmiralData admiral, Tenchijin tenchijin)
            => admiral == null ? 0 : GrownStat(admiral.defense, admiral.growth, admiral, tenchijin);

        /// <summary>提督の成長後の機動。</summary>
        public static int Mobility(AdmiralData admiral, Tenchijin tenchijin)
            => admiral == null ? 0 : GrownStat(admiral.mobility, admiral.growth, admiral, tenchijin);
    }
}
