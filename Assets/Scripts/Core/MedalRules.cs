using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勲章システムの純ロジック（#2263・史実参考）。戦功→等級の叙勲判定と、保有勲章による<b>恩給増加</b>・<b>名誉増加</b>を算定する。
    /// 史実：金鵄勲章は功級ごとに年金（恩給）が付き、叙勲は名誉・席次を与えた。効果は上限付きの係数で背景的に（タイクン化回避）。
    /// 数値は既存（<see cref="RetirementRules"/> 恩給・支持#113）へ接続する想定。実効値パターン・test-first。
    /// </summary>
    public static class MedalRules
    {
        // 恩給・名誉のスケールと上限。
        public const float PensionPerValue = 0.10f;  // 勲章価値1あたりの恩給加算
        public const float MaxPensionBonus = 0.50f;  // 恩給増加の上限（+50%）
        public const float PrestigePerValue = 10f;   // 勲章価値1あたりの名誉点
        public const float MaxPrestige = 50f;        // 名誉点の上限

        /// <summary>戦功(0..100)に対する叙勲の等級。大功ほど高位（一級）。</summary>
        public static MedalGrade GradeForMerit(float meritScore)
        {
            float m = Mathf.Clamp(meritScore, 0f, 100f);
            if (m >= 90f) return MedalGrade.一級;
            if (m >= 70f) return MedalGrade.二級;
            if (m >= 50f) return MedalGrade.三級;
            if (m >= 30f) return MedalGrade.四級;
            return MedalGrade.五級;
        }

        /// <summary>等級の価値係数（一級=1.0〜五級=0.32・等級が下がるほど線形に低下）。</summary>
        public static float GradeFactor(MedalGrade grade)
        {
            int idx = (int)grade; // 一級=0 .. 五級=4
            return Mathf.Clamp(1f - idx * 0.17f, 0f, 1f); // 1.0/0.83/0.66/0.49/0.32
        }

        /// <summary>種別の価値係数（勲功章=1.0/武功章=0.9/戦功章=0.75/従軍章=0.4）。</summary>
        public static float KindFactor(MedalKind kind)
        {
            switch (kind)
            {
                case MedalKind.勲功章: return 1.0f;
                case MedalKind.武功章: return 0.9f;
                case MedalKind.戦功章: return 0.75f;
                default:               return 0.4f; // 従軍章（広く授与・名誉低）
            }
        }

        /// <summary>1つの勲章の価値（等級×種別）。</summary>
        public static float Value(Decoration d) => GradeFactor(d.grade) * KindFactor(d.kind);

        /// <summary>保有勲章の合計価値。</summary>
        public static float TotalValue(IReadOnlyList<Decoration> decorations)
        {
            if (decorations == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < decorations.Count; i++) sum += Value(decorations[i]);
            return sum;
        }

        /// <summary>
        /// 保有勲章による恩給倍率（1.0＋合計価値×加算・上限 +50%）。<see cref="RetirementRules.PensionFactor"/> に乗じる想定。
        /// </summary>
        public static float PensionFactor(IReadOnlyList<Decoration> decorations)
            => 1f + Mathf.Min(TotalValue(decorations) * PensionPerValue, MaxPensionBonus);

        /// <summary>保有勲章による名誉点（合計価値×係数・上限あり）。支持#113・人望へ。</summary>
        public static float Prestige(IReadOnlyList<Decoration> decorations)
            => Mathf.Min(TotalValue(decorations) * PrestigePerValue, MaxPrestige);

        /// <summary>戦功と種別から叙勲する勲章を作る（叙勲判定の窓口）。</summary>
        public static Decoration Award(MedalKind kind, float meritScore, int year = 0, string citation = "")
            => new Decoration(kind, GradeForMerit(meritScore), year, citation);
    }
}
