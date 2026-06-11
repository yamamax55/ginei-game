using UnityEngine;

namespace Ginei
{
    /// <summary>公益志向の政体品質スコアの調整値（#1499・マジックナンバー回避）。`Default` を既定に使う。top-level。</summary>
    public readonly struct CommonGoodOrientationParams
    {
        /// <summary>私益志向（負のスコア）時の腐敗加速の最大上乗せ（完全私益＝−1.0 のとき腐敗速度が 1.0+これ倍）。</summary>
        public readonly float corruptionAccelMax;

        /// <summary>公益志向（正のスコア）時の腐敗抑制の最大値（完全公益＝+1.0 のとき腐敗速度をこの割合まで減速＝×(1−これ)）。</summary>
        public readonly float corruptionBrakeMax;

        /// <summary>累進度と制度的エリート制約の合成における累進度の重み（残りはエリート制約の重み）。</summary>
        public readonly float progressivityWeight;

        /// <summary>堕落政体（僭主/寡頭/衆愚）と判定する公益志向度の既定閾値（これ未満で堕落形態）。</summary>
        public readonly float degenerateThreshold;

        public CommonGoodOrientationParams(float corruptionAccelMax, float corruptionBrakeMax, float progressivityWeight, float degenerateThreshold)
        {
            this.corruptionAccelMax = Mathf.Max(0f, corruptionAccelMax);
            this.corruptionBrakeMax = Mathf.Clamp01(corruptionBrakeMax);
            this.progressivityWeight = Mathf.Clamp01(progressivityWeight);
            this.degenerateThreshold = degenerateThreshold;
        }

        /// <summary>既定＝私益で腐敗が最大+1.0倍加速・公益で最大0.5まで減速・累進度/制約は同等(0.5)・堕落閾値0（私益志向で堕落）。</summary>
        public static CommonGoodOrientationParams Default => new CommonGoodOrientationParams(
            corruptionAccelMax: 1f,
            corruptionBrakeMax: 0.5f,
            progressivityWeight: 0.5f,
            degenerateThreshold: 0f);
    }

    /// <summary>
    /// 公益-私益の政体品質スコアの純ロジック（#1499・アリストテレス『政治学』参考）。政体の正・不正は「<b>誰の利益のために統治するか</b>」で分かれる＝
    /// 公益（共通の利益）のために統治する政体（王政・貴族政・ポリテイア）は正しく、支配者の私益のための政体（僭主政・寡頭政・衆愚政）は堕落形態。
    /// 公益志向か私益志向かが政体の品質を決め（<see cref="CommonGoodScore"/>・<see cref="PolityQuality"/>）、私益志向ほど腐敗が加速する（<see cref="CorruptionAcceleration"/>）。
    /// 正しい政体も私益追求で堕落形態へ転化し（<see cref="DegenerateForm"/>）、公益志向は累進度とエリート制約で制度に根付き（<see cref="Progressivity"/>・<see cref="InstitutionalConstraint"/>）、
    /// 公益志向の政体ほど民の信頼を得る（<see cref="PublicTrust"/>）。<see cref="JusticeRules"/>(5つの正義観の是認)/<see cref="RegimeRules"/>(天命と腐敗の進行)/
    /// <see cref="MesoiRules"/>(中間層の厚みで政体安定)/`ChrematisticsRules`(収奪志向・同EPIC ARIS) とは別＝政体が公益志向か私益志向かの品質スコア（腐敗加速係数）。
    /// 係数は <see cref="CorruptionAcceleration"/> を <see cref="RegimeRules"/> の腐敗速度へ掛ける想定（実効値・基準非破壊）。乱数なし決定論・値は常に Clamp。test-first。
    /// </summary>
    public static class CommonGoodOrientationRules
    {
        /// <summary>
        /// 政体の公益志向度（−1私益〜+1公益）＝公益への貢献−私益の横領。
        /// 公益のために統治するほど +1 へ、支配者の私益（横領）のためほど −1 へ。
        /// </summary>
        public static float CommonGoodScore(float publicBenefit, float privateCapture)
            => Mathf.Clamp(Mathf.Clamp01(publicBenefit) - Mathf.Clamp01(privateCapture), -1f, 1f);

        /// <summary>政体の品質（0..1・公益志向×法の支配＝正しい政体ほど高い）。公益志向度（−1..+1）を 0..1 に写し法の支配を掛ける。</summary>
        public static float PolityQuality(float commonGoodScore, float ruleOfLaw)
        {
            // 公益志向度を 0..1 へ写す（私益−1で0・公益+1で1）。法の支配が欠ければ良政体にならない。
            float orientation = (Mathf.Clamp(commonGoodScore, -1f, 1f) + 1f) * 0.5f;
            return Mathf.Clamp01(orientation * Mathf.Clamp01(ruleOfLaw));
        }

        /// <summary>腐敗加速係数（≥1−brake・私益志向ほど腐敗が加速する＝<see cref="RegimeRules"/> の腐敗速度へ掛ける）。</summary>
        public static float CorruptionAcceleration(float commonGoodScore)
            => CorruptionAcceleration(commonGoodScore, CommonGoodOrientationParams.Default);

        /// <summary>
        /// 腐敗加速係数。私益志向（負のスコア）は 1.0 を超えて加速（最大 1.0+corruptionAccelMax）、
        /// 公益志向（正のスコア）は 1.0 を下回って減速（最小 1.0−corruptionBrakeMax）。スコア0で等倍1.0。
        /// </summary>
        public static float CorruptionAcceleration(float commonGoodScore, CommonGoodOrientationParams p)
        {
            float s = Mathf.Clamp(commonGoodScore, -1f, 1f);
            if (s < 0f)
            {
                // 私益志向ほど腐敗が加速する（負の深さに比例して上乗せ）。
                return 1f + (-s) * Mathf.Max(0f, p.corruptionAccelMax);
            }
            // 公益志向は腐敗を抑える（正の高さに比例して減速）。
            return Mathf.Max(0f, 1f - s * Mathf.Clamp01(p.corruptionBrakeMax));
        }

        /// <summary>公益志向が制度に根付く度合い（0..1・累進度と制度的なエリート制約の加重和）。富の偏在を正し権力を縛るほど公益志向が制度化する。</summary>
        public static float Progressivity(float taxProgressivity, float eliteConstraint)
            => Progressivity(taxProgressivity, eliteConstraint, CommonGoodOrientationParams.Default);

        /// <summary>公益志向の制度化度（0..1）。累進度×progressivityWeight＋エリート制約×(1−progressivityWeight)。</summary>
        public static float Progressivity(float taxProgressivity, float eliteConstraint, CommonGoodOrientationParams p)
        {
            float prog = Mathf.Clamp01(taxProgressivity);
            float constraint = Mathf.Clamp01(eliteConstraint);
            float w = Mathf.Clamp01(p.progressivityWeight);
            return Mathf.Clamp01(prog * w + constraint * (1f - w));
        }

        /// <summary>
        /// 正しい政体が堕落形態へ転化する度合い（0..1・王政→僭主政・貴族政→寡頭政・ポリテイア→衆愚政）。
        /// 正しい政体ほど（legitimateForm が高いほど）失うものが大きく、私益追求が強いほど転化が進む＝両者の積。
        /// </summary>
        public static float DegenerateForm(float legitimateForm, float selfInterest)
            => Mathf.Clamp01(Mathf.Clamp01(legitimateForm) * Mathf.Clamp01(selfInterest));

        /// <summary>公的信頼（0..1・公益志向の政体ほど民の信頼を得る）。政体の品質をそのまま信頼として返す。</summary>
        public static float PublicTrust(float polityQuality)
            => Mathf.Clamp01(polityQuality);

        /// <summary>
        /// エリートを公益に縛る制度的制約（0..1・説明責任と透明性の積）。
        /// 説明責任か透明性のどちらかが欠ければ制約は効かない＝両者揃って初めてエリートを公益へ縛れる。
        /// </summary>
        public static float InstitutionalConstraint(float eliteAccountability, float transparency)
            => Mathf.Clamp01(Mathf.Clamp01(eliteAccountability) * Mathf.Clamp01(transparency));

        /// <summary>堕落政体（僭主/寡頭/衆愚＝私益のための政体）の判定（公益志向度が既定閾値0未満で true）。</summary>
        public static bool IsDegeneratePolity(float commonGoodScore)
            => IsDegeneratePolity(commonGoodScore, CommonGoodOrientationParams.Default.degenerateThreshold);

        /// <summary>堕落政体の判定（公益志向度が threshold 未満で true＝私益のための堕落形態）。</summary>
        public static bool IsDegeneratePolity(float commonGoodScore, float threshold)
            => Mathf.Clamp(commonGoodScore, -1f, 1f) < threshold;
    }
}
