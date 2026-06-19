using UnityEngine;

namespace Ginei
{
    /// <summary>補助部隊（従属国軍・現地徴募兵）の調整係数。すべて Clamp 済みで保持。</summary>
    public readonly struct AuxiliaryForcesParams
    {
        /// <summary>補助兵の質スケール（訓練×士気の幾何平均に掛ける）。</summary>
        public readonly float qualityScale;
        /// <summary>現地知識の価値スケール（地形熟知×住民との繋がりの幾何平均に掛ける）。</summary>
        public readonly float localKnowledgeScale;
        /// <summary>忠誠の不確かさ＝文化的距離の重み。</summary>
        public readonly float culturalWeight;
        /// <summary>忠誠の不確かさ＝不満の重み。</summary>
        public readonly float grievanceWeight;
        /// <summary>依存リスクの逓増指数（補助兵比率の累乗・小さいほど早く立ち上がる）。</summary>
        public readonly float dependencyExponent;
        /// <summary>反乱潜在のスケール（不確かさ×補助兵力に掛ける）。</summary>
        public readonly float rebellionScale;
        /// <summary>正味価値で反乱潜在を差し引く強さ。</summary>
        public readonly float rebellionPenalty;
        /// <summary>費用対効果の上限（質/コストが暴れないよう頭打ち）。</summary>
        public readonly float maxEfficiency;

        public AuxiliaryForcesParams(float qualityScale, float localKnowledgeScale, float culturalWeight,
            float grievanceWeight, float dependencyExponent, float rebellionScale, float rebellionPenalty,
            float maxEfficiency)
        {
            this.qualityScale = Mathf.Clamp(qualityScale, 0f, 2f);
            this.localKnowledgeScale = Mathf.Clamp(localKnowledgeScale, 0f, 2f);
            this.culturalWeight = Mathf.Clamp01(culturalWeight);
            this.grievanceWeight = Mathf.Clamp01(grievanceWeight);
            this.dependencyExponent = Mathf.Clamp(dependencyExponent, 0.1f, 4f);
            this.rebellionScale = Mathf.Clamp(rebellionScale, 0f, 2f);
            this.rebellionPenalty = Mathf.Clamp(rebellionPenalty, 0f, 2f);
            this.maxEfficiency = Mathf.Max(1f, maxEfficiency);
        }

        /// <summary>既定＝質1.0・現地0.9・文化0.6/不満0.4・依存指数0.7・反乱0.8・反乱罰1.0・効率上限3。</summary>
        public static AuxiliaryForcesParams Default =>
            new AuxiliaryForcesParams(1f, 0.9f, 0.6f, 0.4f, 0.7f, 0.8f, 1f, 3f);
    }

    /// <summary>
    /// 補助部隊（従属勢力・現地兵）の純ロジック。正規軍を安く補うが、質・連携・忠誠は不確かで、依存しすぎれば
    /// 本国軍が手薄になり、不満が募れば反乱の刃となる。質×連携で価値を生み、反乱潜在がそれを蝕む。
    /// 解決は本クラスが唯一の窓口。決定論・実効値パターン・入力 Clamp。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AuxiliaryForcesRules
    {
        /// <summary>補助兵の質（0..1）＝訓練×士気の幾何平均×質スケール。どちらか低いと崩れる。</summary>
        public static float AuxiliaryQuality(float training, float motivation, AuxiliaryForcesParams p)
        {
            float t = Mathf.Clamp01(training);
            float m = Mathf.Clamp01(motivation);
            return Mathf.Clamp01(Mathf.Sqrt(t * m) * p.qualityScale);
        }

        public static float AuxiliaryQuality(float training, float motivation)
            => AuxiliaryQuality(training, motivation, AuxiliaryForcesParams.Default);

        /// <summary>本国軍との連携（0..1）＝指揮の互換性×(1−言語障壁)。言葉が通じぬほど連携は崩れる。</summary>
        public static float Integration(float commandCompatibility, float languageBarrier, AuxiliaryForcesParams p)
        {
            float c = Mathf.Clamp01(commandCompatibility);
            float b = Mathf.Clamp01(languageBarrier);
            return Mathf.Clamp01(c * (1f - b));
        }

        public static float Integration(float commandCompatibility, float languageBarrier)
            => Integration(commandCompatibility, languageBarrier, AuxiliaryForcesParams.Default);

        /// <summary>現地知識の価値（0..1）＝地形熟知×住民との繋がりの幾何平均×現地スケール。両方あって初めて効く。</summary>
        public static float LocalKnowledgeValue(float terrainFamiliarity, float populationTies, AuxiliaryForcesParams p)
        {
            float t = Mathf.Clamp01(terrainFamiliarity);
            float pt = Mathf.Clamp01(populationTies);
            return Mathf.Clamp01(Mathf.Sqrt(t * pt) * p.localKnowledgeScale);
        }

        public static float LocalKnowledgeValue(float terrainFamiliarity, float populationTies)
            => LocalKnowledgeValue(terrainFamiliarity, populationTies, AuxiliaryForcesParams.Default);

        /// <summary>忠誠の不確かさ（0..1）＝文化的距離×重み＋不満×重み。遠く不満が募るほど信用ならない。</summary>
        public static float LoyaltyUncertainty(float culturalDistance, float grievances, AuxiliaryForcesParams p)
        {
            float cd = Mathf.Clamp01(culturalDistance);
            float g = Mathf.Clamp01(grievances);
            return Mathf.Clamp01(cd * p.culturalWeight + g * p.grievanceWeight);
        }

        public static float LoyaltyUncertainty(float culturalDistance, float grievances)
            => LoyaltyUncertainty(culturalDistance, grievances, AuxiliaryForcesParams.Default);

        /// <summary>依存リスク（0..1）＝補助兵比率の累乗。比率が上がるほど本国軍が手薄になる危うさ。</summary>
        public static float DependencyRisk(float auxiliaryRatio, AuxiliaryForcesParams p)
        {
            float r = Mathf.Clamp01(auxiliaryRatio);
            return Mathf.Clamp01(Mathf.Pow(r, p.dependencyExponent));
        }

        public static float DependencyRisk(float auxiliaryRatio)
            => DependencyRisk(auxiliaryRatio, AuxiliaryForcesParams.Default);

        /// <summary>反乱の潜在（0..1）＝忠誠の不確かさ×補助兵力×反乱スケール。不確かで数が多いほど刃になる。</summary>
        public static float RebellionPotential(float loyaltyUncertainty, float auxiliaryStrength, AuxiliaryForcesParams p)
        {
            float u = Mathf.Clamp01(loyaltyUncertainty);
            float s = Mathf.Clamp01(auxiliaryStrength);
            return Mathf.Clamp01(u * s * p.rebellionScale);
        }

        public static float RebellionPotential(float loyaltyUncertainty, float auxiliaryStrength)
            => RebellionPotential(loyaltyUncertainty, auxiliaryStrength, AuxiliaryForcesParams.Default);

        /// <summary>費用対効果（0..maxEfficiency）＝質/コスト。補助兵は安いので質が並でも効率は高い。</summary>
        public static float CostEfficiency(float auxiliaryQuality, float auxiliaryCost, AuxiliaryForcesParams p)
        {
            float q = Mathf.Clamp01(auxiliaryQuality);
            float cost = Mathf.Clamp01(auxiliaryCost);
            float eff = q / Mathf.Max(1e-4f, cost);
            return Mathf.Clamp(eff, 0f, p.maxEfficiency);
        }

        public static float CostEfficiency(float auxiliaryQuality, float auxiliaryCost)
            => CostEfficiency(auxiliaryQuality, auxiliaryCost, AuxiliaryForcesParams.Default);

        /// <summary>
        /// 補助部隊の正味価値（-1..1）＝質×連携で生む戦力から反乱潜在×罰を差し引く。
        /// 質も連携も高く反乱が小さければ正、忠誠が崩れれば負（味方に刃を向ける）。
        /// </summary>
        public static float AuxiliaryValue(float auxiliaryQuality, float integration, float rebellionPotential,
            AuxiliaryForcesParams p)
        {
            float q = Mathf.Clamp01(auxiliaryQuality);
            float integ = Mathf.Clamp01(integration);
            float reb = Mathf.Clamp01(rebellionPotential);
            float v = q * integ - reb * p.rebellionPenalty;
            return Mathf.Clamp(v, -1f, 1f);
        }

        public static float AuxiliaryValue(float auxiliaryQuality, float integration, float rebellionPotential)
            => AuxiliaryValue(auxiliaryQuality, integration, rebellionPotential, AuxiliaryForcesParams.Default);
    }
}
