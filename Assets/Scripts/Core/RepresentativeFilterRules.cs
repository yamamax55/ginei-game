using UnityEngine;

namespace Ginei
{
    /// <summary>代表による派閥濾過の調整係数（『ザ・フェデラリスト』第10篇＝代表制が民意を精錬する）。</summary>
    public readonly struct RepresentativeFilterParams
    {
        /// <summary>選挙区規模が濾過強度に効く重み（区が大きいほど代表が広い公益で考える）。</summary>
        public readonly float sizeWeight;
        /// <summary>代表の質が濾過強度に効く重み（賢明な代表ほど情念を冷ます）。</summary>
        public readonly float qualityWeight;
        /// <summary>大選挙区とみなす規模の閾値（これを超えると民意からの遊離が始まる）。</summary>
        public readonly float detachmentThreshold;
        /// <summary>良い代表制とみなす濾過強度の閾値（これ以上で派閥熱を精錬できる）。</summary>
        public readonly float wellFilteredThreshold;

        public RepresentativeFilterParams(float sizeWeight, float qualityWeight, float detachmentThreshold, float wellFilteredThreshold)
        {
            this.sizeWeight = Mathf.Clamp01(sizeWeight);
            this.qualityWeight = Mathf.Clamp01(qualityWeight);
            this.detachmentThreshold = Mathf.Clamp01(detachmentThreshold);
            this.wellFilteredThreshold = Mathf.Clamp01(wellFilteredThreshold);
        }

        /// <summary>既定＝規模重み0.5・質重み0.5・遊離閾値0.8・良判定閾値0.5。</summary>
        public static RepresentativeFilterParams Default => new RepresentativeFilterParams(0.5f, 0.5f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 代表による派閥濾過の純ロジック（FED-6 #1494・『ザ・フェデラリスト』第10篇マディソン）。
    /// 「代表制（representation）は直接民主制に勝る＝民意を選ばれた少数の代表に通すことで世論を濾過し精錬する
    /// （refine and enlarge）。賢明な代表が一時の党派的情念を冷まし、広い公益へ昇華する。ただし選挙区が
    /// 小さすぎると地域派閥に乗っ取られ、大きすぎると代表が民意から離れる（エリートの遊離）」を式に出す。
    /// 選挙区規模が派閥濾過強度を決め、派閥的歪みを低減する。
    /// 党勢と首班（<see cref="PartyRules"/>）・住民投票の一回性票（<see cref="PlebisciteRules"/>）・
    /// 扇動家の直接動員（<see cref="PlebiscitaryRules"/>＝逆向き）・派閥の多数性（<see cref="FactionMultiplicityRules"/>＝同EPIC FED）
    /// とは別系統＝こちらは「代表制が直接民主の派閥熱を濾す（選挙区規模→濾過強度）」を担う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RepresentativeFilterRules
    {
        /// <summary>
        /// 派閥濾過の強度（0..1）＝選挙区が大きく代表の質が高いほど強い（民意を精錬する力）。
        /// 規模×規模重み＋質×質重みの加重和。区が広いほど代表は広い公益で考え、賢明なほど情念を冷ます。
        /// </summary>
        public static float FilterStrength(float districtSize, float representativeQuality, RepresentativeFilterParams p)
        {
            float size = Mathf.Clamp01(districtSize);
            float quality = Mathf.Clamp01(representativeQuality);
            return Mathf.Clamp01(size * p.sizeWeight + quality * p.qualityWeight);
        }

        public static float FilterStrength(float districtSize, float representativeQuality)
            => FilterStrength(districtSize, representativeQuality, RepresentativeFilterParams.Default);

        /// <summary>
        /// 党派的情念の冷却後の値（0..1）＝代表が一時の情念を冷ます。生の情念 popularPassion から
        /// 濾過強度ぶんを差し引く（直接投票の熱を間接化で薄める）。濾過強度1で完全に冷め、0で生のまま。
        /// </summary>
        public static float PassionCooling(float filterStrength, float popularPassion)
        {
            float passion = Mathf.Clamp01(popularPassion);
            return Mathf.Clamp01(passion * (1f - Mathf.Clamp01(filterStrength)));
        }

        /// <summary>
        /// 世論の精錬値（0..1）＝生の世論 rawOpinion を広い公益（中庸0.5）へ精錬・拡大する（refine and enlarge）。
        /// 濾過が強いほど両極の生の世論が中庸の公益側へ引き寄せられる。濾過0なら生のまま。
        /// </summary>
        public static float PublicViewRefinement(float filterStrength, float rawOpinion)
        {
            float raw = Mathf.Clamp01(rawOpinion);
            return Mathf.Clamp01(Mathf.Lerp(raw, 0.5f, Mathf.Clamp01(filterStrength)));
        }

        /// <summary>
        /// 小選挙区の乗っ取り度（0..1）＝選挙区が小さすぎると地域派閥に握られる（少数の利益が握る）。
        /// 区の小ささ（1−規模）×派閥集中度。区が大きいほど派閥は薄まって乗っ取りにくい。
        /// </summary>
        public static float SmallDistrictCapture(float districtSize, float factionalConcentration)
        {
            float small = 1f - Mathf.Clamp01(districtSize);
            return Mathf.Clamp01(small * Mathf.Clamp01(factionalConcentration));
        }

        /// <summary>
        /// 大選挙区の遊離度（0..1）＝選挙区が大きすぎると代表が民意から離れる（エリートの遊離）。
        /// 規模が閾値を超えた超過ぶんを残り幅で正規化。閾値以下なら遊離なし。
        /// </summary>
        public static float LargeDistrictDetachment(float districtSize, float threshold)
        {
            float size = Mathf.Clamp01(districtSize);
            float th = Mathf.Clamp01(threshold);
            if (size <= th) return 0f;
            return Mathf.Clamp01((size - th) / Mathf.Max(0.0001f, 1f - th));
        }

        public static float LargeDistrictDetachment(float districtSize, RepresentativeFilterParams p)
            => LargeDistrictDetachment(districtSize, p.detachmentThreshold);

        /// <summary>
        /// 濾過と民意反映の両立する最適な選挙区規模（0..1）＝小さすぎず大きすぎず。
        /// 民意の多様性が高いほど派閥を薄めるために大きめの区が要る。中庸0.5を起点に多様性で上振れ。
        /// </summary>
        public static float OptimalDistrictSize(float populationDiversity)
        {
            float diversity = Mathf.Clamp01(populationDiversity);
            return Mathf.Clamp01(0.4f + 0.4f * diversity);
        }

        /// <summary>
        /// 扇動家への抵抗力（0..1）＝代表制が扇動家の直接動員を濾して防ぐ（<see cref="PlebiscitaryRules"/> の逆）。
        /// 濾過強度がそのまま抵抗力＝代表を介すほど大衆の直接動員が届かない。
        /// </summary>
        public static float DemagogueResistance(float filterStrength)
        {
            return Mathf.Clamp01(filterStrength);
        }

        /// <summary>
        /// 良い代表制か＝派閥に乗っ取られず民意を精錬する。濾過強度が閾値以上、かつ乗っ取りリスクが閾値未満。
        /// 濾しても派閥に握られていれば良い代表制とは言えない。
        /// </summary>
        public static bool IsWellFiltered(float filterStrength, float captureRisk, float threshold)
        {
            float th = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(filterStrength) >= th && Mathf.Clamp01(captureRisk) < th;
        }

        public static bool IsWellFiltered(float filterStrength, float captureRisk, RepresentativeFilterParams p)
            => IsWellFiltered(filterStrength, captureRisk, p.wellFilteredThreshold);

        public static bool IsWellFiltered(float filterStrength, float captureRisk)
            => IsWellFiltered(filterStrength, captureRisk, RepresentativeFilterParams.Default);
    }
}
