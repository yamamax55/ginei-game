using UnityEngine;

namespace Ginei
{
    /// <summary>傀儡政権の運営調整係数。</summary>
    public readonly struct PuppetGovernmentParams
    {
        /// <summary>正統性に占める現地有力者の権威の重み（0..1・残りが民衆の負託）。既定で半々。</summary>
        public readonly float figureheadWeight;
        /// <summary>見かけの自治を割り引く宗主の介入の重み（0以上）。大きいほど介入が体裁を崩す。</summary>
        public readonly float interferenceWeight;
        /// <summary>板挟みの非線形度（忠誠×支持の積の冪・0以上1以下）。小さいほど両立が苦しい。</summary>
        public readonly float dilemmaExponent;
        /// <summary>間接統治の効率の天井（0..1・直接占領より安いが完全ではない）。</summary>
        public readonly float ruleEfficiencyCap;
        /// <summary>反抗圧における民の不支持の重み（0..1・残りが民族主義）。</summary>
        public readonly float discontentWeight;
        /// <summary>安定における板挟みの減点重み（0..1）。</summary>
        public readonly float dilemmaPenaltyWeight;
        /// <summary>安定における反抗圧の減点重み（0..1）。</summary>
        public readonly float defiancePenaltyWeight;
        /// <summary>宗主の統制が反抗圧で弱まる重み（0..1・反抗が高いと後ろ盾でも抑えきれない）。</summary>
        public readonly float controlErosionWeight;

        public PuppetGovernmentParams(float figureheadWeight, float interferenceWeight,
            float dilemmaExponent, float ruleEfficiencyCap, float discontentWeight,
            float dilemmaPenaltyWeight, float defiancePenaltyWeight, float controlErosionWeight)
        {
            this.figureheadWeight = Mathf.Clamp01(figureheadWeight);
            this.interferenceWeight = Mathf.Max(0f, interferenceWeight);
            this.dilemmaExponent = Mathf.Clamp01(dilemmaExponent);
            this.ruleEfficiencyCap = Mathf.Clamp01(ruleEfficiencyCap);
            this.discontentWeight = Mathf.Clamp01(discontentWeight);
            this.dilemmaPenaltyWeight = Mathf.Clamp01(dilemmaPenaltyWeight);
            this.defiancePenaltyWeight = Mathf.Clamp01(defiancePenaltyWeight);
            this.controlErosionWeight = Mathf.Clamp01(controlErosionWeight);
        }

        /// <summary>既定＝有力者重み0.5・介入重み1・板挟み冪0.5・効率天井0.9・不支持重み0.5・板挟み減点0.5・反抗減点0.5・統制侵食0.5。</summary>
        public static PuppetGovernmentParams Default =>
            new PuppetGovernmentParams(0.5f, 1f, 0.5f, 0.9f, 0.5f, 0.5f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 傀儡政権の純ロジック（占領地に立てる従属政府の運営）。征服地に現地人の従属政府（傀儡）を
    /// 樹立して間接統治する＝傀儡の正統性、宗主国への忠誠と現地民の支持の板挟み、自治の体裁、
    /// 自立化（反抗）リスク、間接統治の効率、傀儡の崩壊を式に出す。
    /// 「現地人の顔で間接統治すれば安く治まるが、宗主に忠実すぎれば民が離れ、民に寄りすぎれば
    /// 自立して反抗する」という傀儡の構造的ジレンマを扱う。
    /// 倍率/指標は実効値パターンで使う（基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PuppetGovernmentRules
    {
        /// <summary>
        /// 傀儡の正統性（0..1）。現地有力者の権威と民衆の負託の重み付き和＝現地人の顔と民意の両輪。
        /// どちらかが欠けても正統性は下がる（有力者だけ・民意だけでは傀儡は安定しない）。
        /// </summary>
        public static float PuppetLegitimacy(float localFigurehead, float popularMandate,
            PuppetGovernmentParams p)
        {
            float figurehead = Mathf.Clamp01(localFigurehead);
            float mandate = Mathf.Clamp01(popularMandate);
            float legit = figurehead * p.figureheadWeight + mandate * (1f - p.figureheadWeight);
            return Mathf.Clamp01(legit);
        }

        public static float PuppetLegitimacy(float localFigurehead, float popularMandate)
            => PuppetLegitimacy(localFigurehead, popularMandate, PuppetGovernmentParams.Default);

        /// <summary>
        /// 見かけの自治（0..1）＝見かけの主権/(主権＋宗主の介入×重み)。宗主が露骨に介入するほど
        /// 「自治の体裁」が薄れて傀儡だと見透かされる。主権0なら自治なし、介入0なら主権そのまま。
        /// </summary>
        public static float PerceivedAutonomy(float visibleSovereignty, float overlordInterference,
            PuppetGovernmentParams p)
        {
            float sov = Mathf.Clamp01(visibleSovereignty);
            float interf = Mathf.Clamp01(overlordInterference);
            float denom = sov + interf * p.interferenceWeight;
            if (denom <= 0.0001f) return 0f; // 主権も介入も無ければ体裁を語れない
            return Mathf.Clamp01(sov / denom);
        }

        public static float PerceivedAutonomy(float visibleSovereignty, float overlordInterference)
            => PerceivedAutonomy(visibleSovereignty, overlordInterference, PuppetGovernmentParams.Default);

        /// <summary>
        /// 板挟み度（0..1）。宗主への忠誠と民の支持は両立しにくい＝両者の積が低いほど傀儡は苦しい。
        /// 1−(忠誠×支持)^冪 ＝ どちらかに偏れば板挟みが深まる（宗主に忠実すぎれば民が離れ、民に寄れば
        /// 宗主に睨まれる）。両方が高い理想状態でのみ板挟みが緩む。
        /// </summary>
        public static float CollaborationDilemma(float overlordLoyalty, float popularSupport,
            PuppetGovernmentParams p)
        {
            float loyalty = Mathf.Clamp01(overlordLoyalty);
            float support = Mathf.Clamp01(popularSupport);
            float harmony = Mathf.Pow(loyalty * support, p.dilemmaExponent);
            return Mathf.Clamp01(1f - harmony);
        }

        public static float CollaborationDilemma(float overlordLoyalty, float popularSupport)
            => CollaborationDilemma(overlordLoyalty, popularSupport, PuppetGovernmentParams.Default);

        /// <summary>
        /// 間接統治の効率（0..1）＝傀儡の正統性×自治の体裁を効率天井で抑えた値。現地人の顔で治めれば
        /// 直接占領より安く統治できる（駐留・行政コストを現地が肩代わり）が、正統性も体裁も無ければ
        /// 間接統治の旨味は消える（結局直接占領と変わらない）。天井で完全には達しない。
        /// </summary>
        public static float IndirectRuleEfficiency(float puppetLegitimacy, float perceivedAutonomy,
            PuppetGovernmentParams p)
        {
            float legit = Mathf.Clamp01(puppetLegitimacy);
            float autonomy = Mathf.Clamp01(perceivedAutonomy);
            return Mathf.Clamp01(legit * autonomy * p.ruleEfficiencyCap);
        }

        public static float IndirectRuleEfficiency(float puppetLegitimacy, float perceivedAutonomy)
            => IndirectRuleEfficiency(puppetLegitimacy, perceivedAutonomy, PuppetGovernmentParams.Default);

        /// <summary>
        /// 反抗圧（0..1）。民の支持を失い民族主義が高まるほど傀儡が宗主に反抗（自立化）する圧が増す。
        /// 民の不支持(1−支持)と民族主義の重み付き和＝民が離れ独立志向が燃えると傀儡は宗主の手綱を
        /// 振りほどこうとする。逆に民の支持が厚く民族主義が低ければ反抗圧は小さい。
        /// </summary>
        public static float DefiancePressure(float popularSupport, float nationalistSentiment,
            PuppetGovernmentParams p)
        {
            float support = Mathf.Clamp01(popularSupport);
            float nationalism = Mathf.Clamp01(nationalistSentiment);
            float discontent = 1f - support;
            float pressure = discontent * p.discontentWeight + nationalism * (1f - p.discontentWeight);
            return Mathf.Clamp01(pressure);
        }

        public static float DefiancePressure(float popularSupport, float nationalistSentiment)
            => DefiancePressure(popularSupport, nationalistSentiment, PuppetGovernmentParams.Default);

        /// <summary>
        /// 傀儡の安定（-1..1）＝正統性−板挟み×重み−反抗圧×重み。正統性が支え、板挟みと反抗が削る。
        /// 正の値は安定（傀儡が機能）、負の値は崩壊へ向かう（民にも宗主にも見放され反抗で空中分解）。
        /// </summary>
        public static float PuppetStability(float legitimacy, float dilemma, float defiance,
            PuppetGovernmentParams p)
        {
            float legit = Mathf.Clamp01(legitimacy);
            float dil = Mathf.Clamp01(dilemma);
            float def = Mathf.Clamp01(defiance);
            float stability = legit - dil * p.dilemmaPenaltyWeight - def * p.defiancePenaltyWeight;
            return Mathf.Clamp(stability, -1f, 1f);
        }

        public static float PuppetStability(float legitimacy, float dilemma, float defiance)
            => PuppetStability(legitimacy, dilemma, defiance, PuppetGovernmentParams.Default);

        /// <summary>
        /// 宗主の統制力（0..1）＝忠誠×駐留後ろ盾の幾何平均を、反抗圧で侵食した値。宗主への忠誠と
        /// 現地に置いた駐留軍の後ろ盾の両方が揃って傀儡を御せる（どちらか欠ければ統制は崩れる＝積）。
        /// 反抗圧が高いほど後ろ盾でも抑えきれず統制が削られる。傀儡の反抗を抑える宗主側の手綱。
        /// </summary>
        public static float OverlordControl(float overlordLoyalty, float garrisonBackup,
            float defiancePressure, PuppetGovernmentParams p)
        {
            float loyalty = Mathf.Clamp01(overlordLoyalty);
            float backup = Mathf.Clamp01(garrisonBackup);
            float defiance = Mathf.Clamp01(defiancePressure);
            float grip = Mathf.Sqrt(loyalty * backup);
            float eroded = grip * (1f - defiance * p.controlErosionWeight);
            return Mathf.Clamp01(eroded);
        }

        public static float OverlordControl(float overlordLoyalty, float garrisonBackup,
            float defiancePressure)
            => OverlordControl(overlordLoyalty, garrisonBackup, defiancePressure,
                PuppetGovernmentParams.Default);

        /// <summary>
        /// 傀儡が持ちこたえるか（true/false）。傀儡の安定に宗主の統制を足した総合余力が閾値を超えれば
        /// 維持＝安定が崩れても宗主の強い後ろ盾が支えれば傀儡は倒れない（逆も真）。閾値未満で崩壊。
        /// </summary>
        public static bool WillPuppetHold(float puppetStability, float overlordControl, float threshold)
        {
            float stability = Mathf.Clamp(puppetStability, -1f, 1f);
            float control = Mathf.Clamp01(overlordControl);
            float thr = Mathf.Clamp(threshold, -1f, 1f);
            return stability + control >= thr;
        }

        public static bool WillPuppetHold(float puppetStability, float overlordControl)
            => WillPuppetHold(puppetStability, overlordControl, 0.2f);
    }
}
