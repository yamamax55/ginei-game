using UnityEngine;

namespace Ginei
{
    /// <summary>軍の質（技術×練度×訓練）→戦力倍率の調整係数。</summary>
    public readonly struct ForceQualityParams
    {
        /// <summary>全入力0のときの戦力倍率（質の下限＝練度も技術も無い烏合の頭数）。</summary>
        public readonly float floor;
        /// <summary>全入力最大のときの戦力倍率（質の上限＝精強な少数）。</summary>
        public readonly float ceiling;
        /// <summary>技術水準 techLevel をこの段で割って 0..1 へ正規化する基準（reference）。</summary>
        public readonly float techReference;
        /// <summary>合成質に占める技術の重み。</summary>
        public readonly float techWeight;
        /// <summary>合成質に占める練度の重み。</summary>
        public readonly float veterancyWeight;
        /// <summary>合成質に占める訓練技能の重み。</summary>
        public readonly float trainingWeight;

        public ForceQualityParams(float floor, float ceiling, float techReference,
            float techWeight, float veterancyWeight, float trainingWeight)
        {
            this.floor = Mathf.Max(0f, floor);
            this.ceiling = Mathf.Max(this.floor, ceiling);
            this.techReference = Mathf.Max(0.0001f, techReference);
            this.techWeight = Mathf.Max(0f, techWeight);
            this.veterancyWeight = Mathf.Max(0f, veterancyWeight);
            this.trainingWeight = Mathf.Max(0f, trainingWeight);
        }

        /// <summary>既定＝下限0.7／上限1.5・技術基準10段・重み 技術0.3/練度0.4/訓練0.3。</summary>
        public static ForceQualityParams Default => new ForceQualityParams(0.7f, 1.5f, 10f, 0.3f, 0.4f, 0.3f);
    }

    /// <summary>
    /// 軍の質（技術×練度×訓練）→戦力倍率の薄い橋渡し（純ロジック・read-only・test-first・唯一の窓口）。
    /// 徴兵の頭数だけでなく「質」を1つの戦力倍率へ変換する＝<see cref="RecruitmentProgramRules.TrainedStrength"/> の
    /// trainingQuality 入力や <see cref="FleetPool"/> へ加える兵力の質係数に使う。
    /// <b>数式は二重実装せず既存窓口へ委譲する</b>＝練度は <see cref="VeterancyRules.CombatFactor"/>、
    /// 訓練技能は <see cref="SkillEffectRules.MilitaryQuality"/>（#2034/#106）。技術は段を基準で正規化するだけ。
    /// 個体粒度へ降りない＝集約された質を1倍率にまとめる（スケーラビリティ規律）。乱数なし・決定論。
    /// 会戦の戦闘力倍率（下士官団×即応）を担う <see cref="ForceQualityRules"/> とは別物（こちらは徴兵供給側の質係数）。
    /// </summary>
    public static class ForceQualityFactorRules
    {
        /// <summary>
        /// 戦力の質倍率（floor..ceiling）。
        /// techLevel（0..上限なし）／veterancy（0..1）／trainingSkill（0..1）を 0..1 の質スコアへ合成し
        /// floor〜ceiling へ線形写像する。全入力0で下限、全高で上限。各入力で単調非減少。
        /// 練度寄与は <see cref="VeterancyRules.CombatFactor"/>（1..1+maxCombatBonus）、
        /// 訓練寄与は <see cref="SkillEffectRules.MilitaryQuality"/>（baseline=1 で 0.6..1.0）を 0..1 へ正規化して用いる。
        /// </summary>
        public static float QualityFactor(float techLevel, float veterancy, float trainingSkill, ForceQualityParams p)
        {
            // 技術＝段を基準で正規化（0..1 へクランプ）。
            float techScore = Mathf.Clamp01(Mathf.Max(0f, techLevel) / p.techReference);

            // 練度＝既存 CombatFactor（1..1+maxCombatBonus）を 0..1 へ正規化（委譲・二重実装しない）。
            var vp = VeterancyParams.Default;
            float vetCombat = VeterancyRules.CombatFactor(Mathf.Clamp01(veterancy) * vp.eliteXp, vp);
            float vetScore = vp.maxCombatBonus > 0f
                ? Mathf.Clamp01((vetCombat - 1f) / vp.maxCombatBonus)
                : 0f;

            // 訓練技能＝既存 MilitaryQuality（baseline=1 で 0.6..1.0）を 0..1 へ正規化（委譲）。
            float trainQuality = SkillEffectRules.MilitaryQuality(Mathf.Clamp01(trainingSkill), 1f);
            float trainScore = Mathf.Clamp01((trainQuality - 0.6f) / 0.4f);

            float wSum = p.techWeight + p.veterancyWeight + p.trainingWeight;
            float quality = wSum > 0f
                ? (techScore * p.techWeight + vetScore * p.veterancyWeight + trainScore * p.trainingWeight) / wSum
                : 0f;
            quality = Mathf.Clamp01(quality);

            return Mathf.Lerp(p.floor, p.ceiling, quality);
        }

        /// <summary>既定パラメータの簡易窓口。</summary>
        public static float QualityFactor(float techLevel, float veterancy, float trainingSkill)
            => QualityFactor(techLevel, veterancy, trainingSkill, ForceQualityParams.Default);
    }
}
