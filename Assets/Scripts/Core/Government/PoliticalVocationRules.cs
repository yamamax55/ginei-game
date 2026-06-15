using UnityEngine;

namespace Ginei
{
    /// <summary>政治家の志向＝政治を「天職として（für）」生きる召命型か「政治によって（von）＝生計の手段で」生きる生業型か（WEBR-3 #1531）。</summary>
    public enum VocationOrientation
    {
        召命型,
        生業型,
        均衡,
    }

    /// <summary>政治の職業化＝召命 vs 生業の調整係数（ウェーバー『職業としての政治』）。</summary>
    public readonly struct PoliticalVocationParams
    {
        /// <summary>召命型ほど理想に殉じうる献身の最大値（使命のための政治の振れ幅）。</summary>
        public readonly float idealismScale;
        /// <summary>生計依存が出世主義へ傾ける1tickあたりのドリフト速度（保身・栄達への流れ）。</summary>
        public readonly float careerismRate;
        /// <summary>党機械依存による官僚化の最大値（党組織に飼われた職業政治家の硬直）。</summary>
        public readonly float bureaucratizationScale;
        /// <summary>生計依存が招く腐敗傾性の最大値（監督ゼロ・生計全依存での最悪値）。</summary>
        public readonly float corruptionScale;
        /// <summary>召命型が地位を捨てて信念を貫ける自己犠牲の最大値。</summary>
        public readonly float sacrificeScale;
        /// <summary>召命型/生業型を弁別する志向値の既定閾値（|値|がこれ未満は均衡）。</summary>
        public readonly float typeThreshold;

        public PoliticalVocationParams(
            float idealismScale, float careerismRate, float bureaucratizationScale,
            float corruptionScale, float sacrificeScale, float typeThreshold)
        {
            this.idealismScale = Mathf.Clamp01(idealismScale);
            this.careerismRate = Mathf.Max(0f, careerismRate);
            this.bureaucratizationScale = Mathf.Clamp01(bureaucratizationScale);
            this.corruptionScale = Mathf.Clamp01(corruptionScale);
            this.sacrificeScale = Mathf.Clamp01(sacrificeScale);
            this.typeThreshold = Mathf.Clamp(typeThreshold, 0f, 1f);
        }

        /// <summary>既定＝理想献身1.0・出世ドリフト0.3・官僚化0.8・腐敗0.7・自己犠牲0.9・弁別閾値0.25。</summary>
        public static PoliticalVocationParams Default => new PoliticalVocationParams(1f, 0.3f, 0.8f, 0.7f, 0.9f, 0.25f);
    }

    /// <summary>
    /// 政治の職業化の純ロジック（WEBR-3 #1531・マックス・ウェーバー『職業としての政治』）。
    /// 政治を「天職・使命として（für）」生きる召命型は理想に殉じうるが、「政治によって（von）＝生計の手段として」
    /// 生きる生業型＝党機械の職業政治家は官僚化・腐敗しやすい＝召命 vs 生業の軸を式に出す。
    /// 分担：<see cref="PatronageRules"/> は「猟官＝買えた忠誠と行政劣化」（縁故配分の損益）、
    /// <see cref="PartyRules"/> は「党勢と首班」（最小選挙）、
    /// <see cref="PoliticalEthicsRules"/> は「心情倫理 vs 責任倫理」（同 EPIC WEBR の倫理判断）、
    /// ここは「召命 vs 生業」（職業政治家の官僚化と腐敗傾性そのもの）を扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PoliticalVocationRules
    {
        /// <summary>
        /// 志向値（−1=召命〜+1=生業）＝生計依存（von）− 使命感（für）。
        /// 使命のために生きる者は負（召命）へ、政治によって生計を立てる者は正（生業）へ振れる。
        /// </summary>
        public static float VocationOrientationValue(float calling, float livelihoodDependence)
        {
            return Mathf.Clamp01(livelihoodDependence) - Mathf.Clamp01(calling);
        }

        /// <summary>志向値を召命型/生業型/均衡へ弁別（|値|が閾値未満は均衡）。</summary>
        public static VocationOrientation TypeOf(float orientationValue, float threshold)
        {
            float t = Mathf.Clamp(threshold, 0f, 1f);
            if (orientationValue <= -t) return VocationOrientation.召命型;
            if (orientationValue >= t) return VocationOrientation.生業型;
            return VocationOrientation.均衡;
        }

        public static VocationOrientation TypeOf(float orientationValue)
            => TypeOf(orientationValue, PoliticalVocationParams.Default.typeThreshold);

        /// <summary>
        /// 理想に殉じうる献身（0..idealismScale）＝使命感 calling × 信条の強さ principleStrength。
        /// 召命型ほど＝信条が強いほど、理想のために政治を生きる（使命のための政治）。
        /// </summary>
        public static float IdealismDrivenService(float calling, float principleStrength, PoliticalVocationParams p)
        {
            return Mathf.Clamp01(calling) * Mathf.Clamp01(principleStrength) * p.idealismScale;
        }

        public static float IdealismDrivenService(float calling, float principleStrength)
            => IdealismDrivenService(calling, principleStrength, PoliticalVocationParams.Default);

        /// <summary>
        /// 出世主義ドリフト（≥0）＝生計依存 livelihoodDependence × 経過 dt × ドリフト速度。
        /// 政治によって生きる者ほど保身・栄達へ傾く（信念より地位を選ぶ流れ）。1tickの増分を返す。
        /// </summary>
        public static float CareerismDrift(float livelihoodDependence, float dt, PoliticalVocationParams p)
        {
            return Mathf.Clamp01(livelihoodDependence) * Mathf.Max(0f, dt) * p.careerismRate;
        }

        public static float CareerismDrift(float livelihoodDependence, float dt)
            => CareerismDrift(livelihoodDependence, dt, PoliticalVocationParams.Default);

        /// <summary>
        /// 党機械による官僚化（0..bureaucratizationScale）＝生計依存 × 党機械の強さ machineStrength。
        /// 党組織に生計を握られた職業政治家ほど機械の歯車となり官僚化する（自前の使命を失う）。
        /// </summary>
        public static float PartyMachineBureaucratization(float livelihoodDependence, float machineStrength, PoliticalVocationParams p)
        {
            return Mathf.Clamp01(livelihoodDependence) * Mathf.Clamp01(machineStrength) * p.bureaucratizationScale;
        }

        public static float PartyMachineBureaucratization(float livelihoodDependence, float machineStrength)
            => PartyMachineBureaucratization(livelihoodDependence, machineStrength, PoliticalVocationParams.Default);

        /// <summary>
        /// 腐敗傾性（0..corruptionScale）＝生計依存 × 監督の緩さ(1−oversight)。
        /// 生計を政治に握られた者ほど、かつ監督が緩いほど腐敗しやすい（生業型の罠）。監督1で無毒。
        /// </summary>
        public static float CorruptionPropensity(float livelihoodDependence, float oversight, PoliticalVocationParams p)
        {
            float laxity = 1f - Mathf.Clamp01(oversight);
            return Mathf.Clamp01(livelihoodDependence) * laxity * p.corruptionScale;
        }

        public static float CorruptionPropensity(float livelihoodDependence, float oversight)
            => CorruptionPropensity(livelihoodDependence, oversight, PoliticalVocationParams.Default);

        /// <summary>
        /// 政治家三要件の総合（0..1）＝情熱 passion × 判断力 judgment × 責任感 responsibility（ウェーバー）。
        /// どれか一つでも欠ければ崩れる積＝召命型の理想（情熱だけの煽動家でも、責任なき判断でもない）。
        /// </summary>
        public static float PassionResponsibilityProportion(float passion, float judgment, float responsibility)
        {
            return Mathf.Clamp01(passion) * Mathf.Clamp01(judgment) * Mathf.Clamp01(responsibility);
        }

        /// <summary>
        /// 自己犠牲の覚悟（0..sacrificeScale）＝使命感 calling × 地位への非執着(1−生計依存)。
        /// 召命型ほど＝生計を政治に頼らぬ者ほど、地位を捨てて信念を貫ける（生業型は地位に執着して退けない）。
        /// </summary>
        public static float SacrificeWillingness(float calling, float livelihoodDependence, PoliticalVocationParams p)
        {
            float detachment = 1f - Mathf.Clamp01(livelihoodDependence);
            return Mathf.Clamp01(calling) * detachment * p.sacrificeScale;
        }

        public static float SacrificeWillingness(float calling, float livelihoodDependence)
            => SacrificeWillingness(calling, livelihoodDependence, PoliticalVocationParams.Default);

        /// <summary>
        /// 生計のための職業政治家に堕したか＝志向値が閾値以上の生業型（von に偏った政治家）。
        /// </summary>
        public static bool IsCareerPolitician(float orientationValue, float threshold)
        {
            return TypeOf(orientationValue, threshold) == VocationOrientation.生業型;
        }

        public static bool IsCareerPolitician(float orientationValue)
            => IsCareerPolitician(orientationValue, PoliticalVocationParams.Default.typeThreshold);
    }
}
