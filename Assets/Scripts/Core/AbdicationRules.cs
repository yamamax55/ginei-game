using UnityEngine;

namespace Ginei
{
    /// <summary>生前継承・摂政・引き際（HDRN-3 #1805）の調整係数。</summary>
    public readonly struct AbdicationParams
    {
        /// <summary>倦怠が退位意思に与える重み。</summary>
        public readonly float fatigueWeight;
        /// <summary>死の自覚が退位意思に与える重み。</summary>
        public readonly float mortalityWeight;
        /// <summary>宮廷圧力が退位意思に与える重み。</summary>
        public readonly float courtPressureWeight;
        /// <summary>後継準備の良さが退位を後押しする重み（後継ができているほど退位しやすい）。</summary>
        public readonly float heirReadinessWeight;
        /// <summary>退位が「計画的」とみなされる意思スコア閾値（以上で計画退位・未満で非計画）。</summary>
        public readonly float plannedThreshold;
        /// <summary>権力返上の実行しやすさ（高いほど文化的に退位を許容）。</summary>
        public readonly float powerReleaseEase;
        /// <summary>
        /// 過渡期の摂政橋渡しが必要と判断する後継の準備度の下限（準備度がこれ未満なら摂政が必要）。
        /// </summary>
        public readonly float regentBridgeThreshold;

        public AbdicationParams(float fatigueWeight, float mortalityWeight, float courtPressureWeight,
                                float heirReadinessWeight, float plannedThreshold,
                                float powerReleaseEase, float regentBridgeThreshold)
        {
            this.fatigueWeight = Mathf.Clamp01(fatigueWeight);
            this.mortalityWeight = Mathf.Clamp01(mortalityWeight);
            this.courtPressureWeight = Mathf.Clamp01(courtPressureWeight);
            this.heirReadinessWeight = Mathf.Clamp01(heirReadinessWeight);
            this.plannedThreshold = Mathf.Clamp01(plannedThreshold);
            this.powerReleaseEase = Mathf.Clamp01(powerReleaseEase);
            this.regentBridgeThreshold = Mathf.Clamp01(regentBridgeThreshold);
        }

        /// <summary>
        /// 既定＝倦怠重み0.35・死の自覚重み0.25・宮廷圧力0.2・後継準備0.2・
        /// 計画退位閾値0.6・権力返上しやすさ0.5・摂政橋渡し閾値0.5。
        /// </summary>
        public static AbdicationParams Default
            => new AbdicationParams(0.35f, 0.25f, 0.2f, 0.2f, 0.6f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 生前継承・退位・引き際の純ロジック（HDRN-3 #1805）。強いる死でなく自発的な権力返上＝
    /// 統治者が自ら玉座を降りる（生前継承・退位・譲位）の力学。
    /// 治世の倦怠・死の自覚・後継の準備・宮廷の圧力が合わさって退位の意思を高める。
    /// 意思が閾値を超えたら「計画退位」、下回ると「引き延ばし」（権力への執着）＝
    /// 最後の贈り物か、玉座に死ぬまでしがみつくかの選択。
    /// 分担：<see cref="RegencyRules"/>（幼君・摂政の一時的代理）とは別＝大人の統治者の自発的譲位／
    /// <see cref="TermLimitRules"/>（制度による任期終了）とは別＝制度外の自発的引き際／
    /// <see cref="SuccessionRules"/>（後継候補の選定）とは別＝退位の意思と時機の判断。
    /// 盤面非依存のplain引数・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AbdicationRules
    {
        /// <summary>
        /// 退位意思スコア（0..1）＝倦怠・死の自覚・宮廷圧力・後継準備の加重和×権力返上しやすさ。
        /// すべてが揃ったとき最大（権力に固執する文化では低い）。
        /// </summary>
        /// <param name="fatigue">統治の倦怠（0..1）。</param>
        /// <param name="mortalityAwareness">死の自覚（0..1）。</param>
        /// <param name="courtPressure">宮廷からの退位圧力（0..1）。</param>
        /// <param name="heirReadiness">後継の準備度（0..1、高いほど退位しやすい）。</param>
        /// <param name="p">調整係数。</param>
        public static float AbdicationWill(float fatigue, float mortalityAwareness,
                                           float courtPressure, float heirReadiness,
                                           AbdicationParams p)
        {
            float f = Mathf.Clamp01(fatigue);
            float m = Mathf.Clamp01(mortalityAwareness);
            float c = Mathf.Clamp01(courtPressure);
            float h = Mathf.Clamp01(heirReadiness);
            float wsum = p.fatigueWeight + p.mortalityWeight + p.courtPressureWeight + p.heirReadinessWeight;
            if (wsum <= 0f) return 0f;
            float raw = f * p.fatigueWeight + m * p.mortalityWeight
                      + c * p.courtPressureWeight + h * p.heirReadinessWeight;
            return Mathf.Clamp01(raw / wsum * p.powerReleaseEase);
        }

        public static float AbdicationWill(float fatigue, float mortalityAwareness,
                                           float courtPressure, float heirReadiness)
            => AbdicationWill(fatigue, mortalityAwareness, courtPressure, heirReadiness,
                              AbdicationParams.Default);

        /// <summary>
        /// 計画退位か（bool）＝退位意思が閾値以上なら計画退位（自発的・合意ある引き際）。
        /// 閾値未満なら引き延ばし＝権力への執着。
        /// </summary>
        public static bool IsPlannedAbdication(float abdicationWill, AbdicationParams p)
            => Mathf.Clamp01(abdicationWill) >= p.plannedThreshold;

        public static bool IsPlannedAbdication(float abdicationWill)
            => IsPlannedAbdication(abdicationWill, AbdicationParams.Default);

        /// <summary>
        /// 摂政橋渡しが必要か（bool）＝後継の準備度が閾値を下回るとき、
        /// 退位前後の過渡期に摂政・共同統治の橋渡しが必要と判断する。
        /// </summary>
        public static bool NeedsRegentBridge(float heirReadiness, AbdicationParams p)
            => Mathf.Clamp01(heirReadiness) < p.regentBridgeThreshold;

        public static bool NeedsRegentBridge(float heirReadiness)
            => NeedsRegentBridge(heirReadiness, AbdicationParams.Default);

        /// <summary>
        /// 過渡期の長さ（0..1の相対値）＝後継の準備不足が大きいほど長い摂政・共同統治期間が必要。
        /// transitionLength = (threshold − readiness) / threshold でクランプ。
        /// 準備十分（readiness≥threshold）なら0（即座の引継ぎが可能）。
        /// </summary>
        public static float TransitionLength(float heirReadiness, AbdicationParams p)
        {
            float h = Mathf.Clamp01(heirReadiness);
            float threshold = Mathf.Clamp01(p.regentBridgeThreshold);
            if (threshold <= 0f) return 0f;
            return Mathf.Clamp01((threshold - h) / threshold);
        }

        public static float TransitionLength(float heirReadiness)
            => TransitionLength(heirReadiness, AbdicationParams.Default);

        /// <summary>
        /// 退位による正統性の贈り物（0..1）＝計画退位×後継の準備度。
        /// 計画的に高準備の後継へ退位するほど、次の統治者への「箔」として正統性が移転される。
        /// 非計画退位・準備不足の退位は贈り物にならない。
        /// </summary>
        public static float LegacyGift(float abdicationWill, float heirReadiness, AbdicationParams p)
        {
            float w = Mathf.Clamp01(abdicationWill);
            float h = Mathf.Clamp01(heirReadiness);
            if (!IsPlannedAbdication(w, p)) return 0f;
            return Mathf.Clamp01(w * h);
        }

        public static float LegacyGift(float abdicationWill, float heirReadiness)
            => LegacyGift(abdicationWill, heirReadiness, AbdicationParams.Default);

        /// <summary>
        /// 権力執着係数（0..1）＝退位意思の補数×権力返上しやすさの逆数的圧力。
        /// = (1 − abdicationWill) × (1 − powerReleaseEase)。
        /// 退位意思が低く、権力執着文化の強い統治者はここが高い。
        /// </summary>
        public static float PowerClinging(float abdicationWill, AbdicationParams p)
        {
            float w = Mathf.Clamp01(abdicationWill);
            float ease = Mathf.Clamp01(p.powerReleaseEase);
            return Mathf.Clamp01((1f - w) * (1f - ease));
        }

        public static float PowerClinging(float abdicationWill)
            => PowerClinging(abdicationWill, AbdicationParams.Default);

        /// <summary>
        /// 共同統治フェーズの実効権力比率（0..1、旧主の権力の残り比率）。
        /// transition = 0..1 の経過（0=退位直後・1=完全移譲）。
        /// 線形に権力を渡す＝1 − transition。準備十分（NeedsRegentBridgeがfalse）なら常に0。
        /// </summary>
        public static float CoregencyPowerRatio(float transition, float heirReadiness, AbdicationParams p)
        {
            if (!NeedsRegentBridge(heirReadiness, p)) return 0f;
            return Mathf.Clamp01(1f - Mathf.Clamp01(transition));
        }

        public static float CoregencyPowerRatio(float transition, float heirReadiness)
            => CoregencyPowerRatio(transition, heirReadiness, AbdicationParams.Default);
    }
}
