using UnityEngine;

namespace Ginei
{
    /// <summary>占領統治の調整係数。</summary>
    public readonly struct OccupationGovernanceParams
    {
        /// <summary>人口1単位あたりの基礎必要駐留兵力（人口比の駐留重み）。</summary>
        public readonly float garrisonPerCapita;
        /// <summary>地形難度が必要駐留を増幅する重み（隠れ場所が多いほど兵が要る）。</summary>
        public readonly float terrainWeight;
        /// <summary>寛容政策が従順度へ寄与する重み（懐柔の効き）。</summary>
        public readonly float leniencyWeight;
        /// <summary>収奪が従順度の効きを削る非線形度（冪指数・1以上）。収奪しすぎると従順でも採れない。</summary>
        public readonly float extractionPenaltyExponent;
        /// <summary>怨嗟蓄積の基礎率（収奪×弾圧×不服従に掛かる）。</summary>
        public readonly float resentmentRate;
        /// <summary>統治正統性が育つ基礎率（従順×協力×時間に掛かる per time）。</summary>
        public readonly float legitimacyRate;
        /// <summary>統治正統性の上限（0..1）。</summary>
        public readonly float maxLegitimacy;
        /// <summary>安定化の基礎率（正統性＋駐留が怨嗟を上回る余地に掛かる）。</summary>
        public readonly float stabilizationRate;
        /// <summary>駐留1単位あたりの維持費（兵を置くだけで掛かる金）。</summary>
        public readonly float garrisonUpkeep;
        /// <summary>怨嗟1単位あたりの治安コスト重み（揉めるほど高くつく）。</summary>
        public readonly float securityCostWeight;

        public OccupationGovernanceParams(float garrisonPerCapita, float terrainWeight, float leniencyWeight,
            float extractionPenaltyExponent, float resentmentRate, float legitimacyRate, float maxLegitimacy,
            float stabilizationRate, float garrisonUpkeep, float securityCostWeight)
        {
            this.garrisonPerCapita = Mathf.Max(0f, garrisonPerCapita);
            this.terrainWeight = Mathf.Max(0f, terrainWeight);
            this.leniencyWeight = Mathf.Max(0f, leniencyWeight);
            this.extractionPenaltyExponent = Mathf.Max(1f, extractionPenaltyExponent);
            this.resentmentRate = Mathf.Max(0f, resentmentRate);
            this.legitimacyRate = Mathf.Max(0f, legitimacyRate);
            this.maxLegitimacy = Mathf.Clamp01(maxLegitimacy);
            this.stabilizationRate = Mathf.Max(0f, stabilizationRate);
            this.garrisonUpkeep = Mathf.Max(0f, garrisonUpkeep);
            this.securityCostWeight = Mathf.Max(0f, securityCostWeight);
        }

        /// <summary>既定＝駐留係数0.5・地形重み1・寛容重み1・収奪冪指数2・怨嗟率1・正統性率0.001・正統性上限1・安定化率0.5・駐留維持0.1・治安重み2。</summary>
        public static OccupationGovernanceParams Default =>
            new OccupationGovernanceParams(0.5f, 1f, 1f, 2f, 1f, 0.001f, 1f, 0.5f, 0.1f, 2f);
    }

    /// <summary>
    /// 占領統治の純ロジック（征服した星系・惑星を治める）。必要駐留兵力、住民の従順/抵抗、
    /// 収奪と懐柔のトレードオフ、統治の安定化、占領経済からの収益、統治正統性を扱う。
    /// 収奪は短期の収益を生むが従順度を削り怨嗟を積む＝弾圧で抑えても恨みが治安コストを膨らませる。
    /// 寛容と現地協力で正統性が時間をかけて育つと安定化が進み、駐留も怨嗟も軽くなる。
    /// 採算（収益が統治コストを上回るか）で占領の持続可否を判定する。
    /// 乱数なし・決定論・実効値パターン（基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OccupationGovernanceRules
    {
        /// <summary>
        /// 必要駐留兵力＝人口×駐留係数×敵意×（1＋地形難度×地形重み）。
        /// 人口が多く敵意が高く地形が険しいほど多くの兵が要る。敵意0なら駐留不要（占領が歓迎されている）。
        /// </summary>
        public static float GarrisonRequirement(float population, float hostility, float terrain,
            OccupationGovernanceParams p)
        {
            float pop = Mathf.Max(0f, population);
            float h = Mathf.Clamp01(hostility);
            float t = Mathf.Clamp01(terrain);
            return pop * p.garrisonPerCapita * h * (1f + t * p.terrainWeight);
        }

        public static float GarrisonRequirement(float population, float hostility, float terrain)
            => GarrisonRequirement(population, hostility, terrain, OccupationGovernanceParams.Default);

        /// <summary>
        /// 住民の従順度（0..1）＝駐留×寛容/(駐留×寛容＋文化的距離)。駐留と寛容政策が文化的距離を
        /// 上回るほど住民は従う。駐留も寛容もゼロなら従順0（剥き出しの占領）。文化的距離0なら従順1。
        /// </summary>
        public static float PopulationCompliance(float garrisonStrength, float leniency, float culturalDistance,
            OccupationGovernanceParams p)
        {
            float g = Mathf.Max(0f, garrisonStrength);
            float len = Mathf.Clamp01(leniency);
            float dist = Mathf.Clamp01(culturalDistance);
            float persuasion = g * (1f + len * p.leniencyWeight);
            float denom = persuasion + dist;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(persuasion / denom);
        }

        public static float PopulationCompliance(float garrisonStrength, float leniency, float culturalDistance)
            => PopulationCompliance(garrisonStrength, leniency, culturalDistance, OccupationGovernanceParams.Default);

        /// <summary>
        /// 占領経済からの収益＝占領経済×収奪率×（従順度を収奪率の冪で割り引いた実効従順）。
        /// 収奪率が高いほど名目の取り分は増えるが、従順度の効きが冪で薄まる＝収奪しすぎると
        /// 住民が従わず実収益が伸びない（収奪と従順のトレードオフ）。収奪0なら収益0。
        /// </summary>
        public static float ExtractionYield(float occupiedEconomy, float exploitationRate, float compliance,
            OccupationGovernanceParams p)
        {
            float econ = Mathf.Max(0f, occupiedEconomy);
            float rate = Mathf.Clamp01(exploitationRate);
            float comp = Mathf.Clamp01(compliance);
            // 収奪が強いほど従順の寄与を冪で割り引く（強収奪は従順を頼れない）
            float complianceFactor = Mathf.Lerp(1f, Mathf.Pow(comp, p.extractionPenaltyExponent), rate);
            return econ * rate * complianceFactor;
        }

        public static float ExtractionYield(float occupiedEconomy, float exploitationRate, float compliance)
            => ExtractionYield(occupiedEconomy, exploitationRate, compliance, OccupationGovernanceParams.Default);

        /// <summary>
        /// 怨嗟の蓄積（0..1）＝収奪率×（弾圧の重み）×不服従（1−従順度）×怨嗟率。
        /// 収奪が強く、弾圧で抑え込み、住民が従わないほど恨みが積もる。従順度が高ければ抑えられる。
        /// 弾圧は短期に抑えるが恨みを増やす（rep をそのまま掛ける＝抑圧の代償）。
        /// </summary>
        public static float ResentmentBuildup(float exploitationRate, float repression, float compliance,
            OccupationGovernanceParams p)
        {
            float rate = Mathf.Clamp01(exploitationRate);
            float rep = Mathf.Clamp01(repression);
            float comp = Mathf.Clamp01(compliance);
            float defiance = 1f - comp;
            float raw = rate * rep * defiance * p.resentmentRate;
            return Mathf.Clamp01(raw);
        }

        public static float ResentmentBuildup(float exploitationRate, float repression, float compliance)
            => ResentmentBuildup(exploitationRate, repression, compliance, OccupationGovernanceParams.Default);

        /// <summary>
        /// 統治正統性（0..maxLegitimacy）＝従順度×現地協力×（時間で飽和する成長）×正統性率。
        /// 従順な住民と現地協力者がいて時間が経つほど統治が正統と認められる。時間は 1−1/(1+率×時間)
        /// で飽和（占領初期は伸びにくく、長期化で頭打ち）。協力ゼロなら正統性は育たない。
        /// </summary>
        public static float Legitimacy(float compliance, float localCollaboration, float timeOccupied,
            OccupationGovernanceParams p)
        {
            float comp = Mathf.Clamp01(compliance);
            float collab = Mathf.Clamp01(localCollaboration);
            float t = Mathf.Max(0f, timeOccupied);
            float timeFactor = 1f - 1f / (1f + p.legitimacyRate * t); // 0→1へ飽和
            float raw = comp * collab * timeFactor;
            return Mathf.Clamp(raw, 0f, p.maxLegitimacy);
        }

        public static float Legitimacy(float compliance, float localCollaboration, float timeOccupied)
            => Legitimacy(compliance, localCollaboration, timeOccupied, OccupationGovernanceParams.Default);

        /// <summary>
        /// 安定化の進捗（-1..1）＝安定化率×（正統性＋駐留の抑え − 怨嗟）。正統性と駐留が怨嗟を
        /// 上回れば正（安定へ向かう）、怨嗟が勝てば負（不安定化＝反乱の芽）。駐留は飽和させて
        /// 過大な兵力で無限に安定しないようにする。
        /// </summary>
        public static float StabilizationProgress(float legitimacy, float garrisonStrength, float resentment,
            OccupationGovernanceParams p)
        {
            float leg = Mathf.Clamp01(legitimacy);
            float g = Mathf.Max(0f, garrisonStrength);
            float res = Mathf.Clamp01(resentment);
            float garrisonControl = g / (g + 1f); // 0..1へ飽和（兵を積むほど効きが鈍る）
            float net = leg + garrisonControl - res;
            return Mathf.Clamp(p.stabilizationRate * net, -1f, 1f);
        }

        public static float StabilizationProgress(float legitimacy, float garrisonStrength, float resentment)
            => StabilizationProgress(legitimacy, garrisonStrength, resentment, OccupationGovernanceParams.Default);

        /// <summary>
        /// 統治コスト（0以上）＝駐留維持費（駐留×維持率）＋治安コスト（怨嗟×治安重み）。
        /// 兵を置くだけで金が掛かり、揉めている（怨嗟が高い）ほど治安維持の費用が膨らむ。
        /// </summary>
        public static float OccupationCost(float garrisonStrength, float resentment, OccupationGovernanceParams p)
        {
            float g = Mathf.Max(0f, garrisonStrength);
            float res = Mathf.Clamp01(resentment);
            float upkeep = g * p.garrisonUpkeep;
            float security = res * p.securityCostWeight;
            return Mathf.Max(0f, upkeep + security);
        }

        public static float OccupationCost(float garrisonStrength, float resentment)
            => OccupationCost(garrisonStrength, resentment, OccupationGovernanceParams.Default);

        /// <summary>
        /// 占領の採算が合うか（bool）＝収益 ≥ コスト×閾値。閾値1.0で収支トントン、1超で「コストの
        /// 何倍稼げて初めて占領を続ける価値あり」の保守判定。コスト0以下なら収益が正で持続可能。
        /// </summary>
        public static bool IsOccupationSustainable(float extractionYield, float occupationCost, float threshold)
        {
            float yield = Mathf.Max(0f, extractionYield);
            float cost = Mathf.Max(0f, occupationCost);
            float th = Mathf.Max(0f, threshold);
            if (cost <= 0f) return yield > 0f;
            return yield >= cost * th;
        }

        /// <summary>既定閾値1.0（収支トントン）で採算判定。</summary>
        public static bool IsOccupationSustainable(float extractionYield, float occupationCost)
            => IsOccupationSustainable(extractionYield, occupationCost, 1f);
    }
}
