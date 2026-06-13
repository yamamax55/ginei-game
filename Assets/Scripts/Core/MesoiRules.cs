using UnityEngine;

namespace Ginei
{
    /// <summary>中間層安定化の調整値（#1495・マジックナンバー回避）。`Default` を既定に使う。top-level。</summary>
    public readonly struct MesoiParams
    {
        /// <summary>中間層シェア→政体安定倍率の最大上乗せ（中間層が満杯のとき 1.0+これ）。</summary>
        public readonly float stabilityGain;

        /// <summary>穏健化作用の最大値（中間層が満杯のとき極端政策をこの強さで抑える）。</summary>
        public readonly float moderationMax;

        /// <summary>格差拡大→中間層の空洞化速度（per 単位時間・上下に引き裂く）。</summary>
        public readonly float hollowingRate;

        /// <summary>砂時計型社会の既定閾値（中間層シェアがこれ未満で二極化＝砂時計）。</summary>
        public readonly float hourglassThreshold;

        public MesoiParams(float stabilityGain, float moderationMax, float hollowingRate, float hourglassThreshold)
        {
            this.stabilityGain = stabilityGain;
            this.moderationMax = moderationMax;
            this.hollowingRate = hollowingRate;
            this.hourglassThreshold = hourglassThreshold;
        }

        public static MesoiParams Default => new MesoiParams(
            stabilityGain: 0.5f,
            moderationMax: 0.8f,
            hollowingRate: 0.5f,
            hourglassThreshold: 0.25f);
    }

    /// <summary>
    /// 中間層安定化係数の純ロジック（#1495・アリストテレス『政治学』＝中庸の徳の政治版）。富者と貧者の間の<b>中間層（hoi mesoi）</b>が
    /// 分厚い政体（ポリテイア）ほど穏健で安定し（<see cref="PolityStabilityFactor"/>）、中間層が空洞化して富者貧者に二極化すると
    /// 対立が激化して不安定になる（<see cref="ClassPolarization"/>・<see cref="IsHourglassSociety"/>）＝中間層は理性的で穏健、富者の傲慢と
    /// 貧者の卑屈・嫉妬を緩衝する（<see cref="ModerationEffect"/>・<see cref="BufferingCapacity"/>）。核は <see cref="MiddleClassShare"/> と
    /// <see cref="PolityStabilityFactor"/>。<see cref="RedistributionRules"/>(税の累進性と階級対立)/<see cref="CoalitionRules"/>(連立)/
    /// <see cref="CapitalRules"/>(格差集中)/`CommonGoodOrientationRules`(政体品質・同EPIC ARIS) とは別＝中間層シェアが政体の安定倍率を決める。
    /// 係数は #106・実効値（基準非破壊）。乱数なし決定論・値は常に Clamp。test-first。
    /// </summary>
    public static class MesoiRules
    {
        /// <summary>中間層シェア＝1−富者シェア−貧者シェア（0..1 にクランプ）。富者・貧者の間に残る中産階級。</summary>
        public static float MiddleClassShare(float richShare, float poorShare)
            => Mathf.Clamp01(1f - Mathf.Clamp01(richShare) - Mathf.Clamp01(poorShare));

        /// <summary>政体安定倍率（≥1.0・中間層が分厚いほど高い＝中庸の政体ポリテイア）。中間層シェアに比例して上乗せ。</summary>
        public static float PolityStabilityFactor(float middleClassShare)
            => PolityStabilityFactor(middleClassShare, MesoiParams.Default);

        /// <summary>政体安定倍率（≥1.0）。1.0+中間層シェア×`stabilityGain`。</summary>
        public static float PolityStabilityFactor(float middleClassShare, MesoiParams p)
            => 1f + Mathf.Clamp01(middleClassShare) * Mathf.Max(0f, p.stabilityGain);

        /// <summary>階級の二極化度（0..1・中間層が薄く富者貧者に偏るほど高い＝砂時計型社会）。富者貧者の合計シェアから中間層を引く。</summary>
        public static float ClassPolarization(float richShare, float poorShare, float middleClassShare)
        {
            // 富者貧者に質量が集まり中間層が痩せるほど二極化が進む。
            float poles = Mathf.Clamp01(richShare) + Mathf.Clamp01(poorShare);
            return Mathf.Clamp01(poles - Mathf.Clamp01(middleClassShare));
        }

        /// <summary>穏健化作用（0..1・中間層が極端政策＝富者の寡頭化/貧者の扇動を抑える）。中間層シェアに比例。</summary>
        public static float ModerationEffect(float middleClassShare)
            => ModerationEffect(middleClassShare, MesoiParams.Default);

        /// <summary>穏健化作用（0..1）。中間層シェア×`moderationMax`。</summary>
        public static float ModerationEffect(float middleClassShare, MesoiParams p)
            => Mathf.Clamp01(Mathf.Clamp01(middleClassShare) * Mathf.Max(0f, p.moderationMax));

        /// <summary>緩衝能力（0..1・中間層が富者と貧者の対立を緩衝する＝対立が強くても中間層が分厚いほど吸収できる残力）。</summary>
        public static float BufferingCapacity(float middleClassShare, float classTension)
        {
            // 中間層シェアが緩衝の上限。対立が強いほど能力を食い潰す。
            float capacity = Mathf.Clamp01(middleClassShare);
            return Mathf.Clamp01(capacity * (1f - Mathf.Clamp01(classTension)));
        }

        /// <summary>1tick の中間層空洞化＝格差拡大が中間層を上下に引き裂き薄くした後の新シェア（0..1）。state は受け取らず新値を返す。</summary>
        public static float HollowingOutTick(float middleClassShare, float inequality, float dt)
            => HollowingOutTick(middleClassShare, inequality, dt, MesoiParams.Default);

        /// <summary>1tick の中間層空洞化。中間層シェア−格差×`hollowingRate`×dt をクランプ。</summary>
        public static float HollowingOutTick(float middleClassShare, float inequality, float dt, MesoiParams p)
        {
            float erosion = Mathf.Clamp01(inequality) * Mathf.Max(0f, p.hollowingRate) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(middleClassShare) - erosion);
        }

        /// <summary>ポリテイアの品質（0..1・中間層＋法の支配で最良の現実的政体）。両者の積＝どちらが欠けても良政体にならない。</summary>
        public static float PoliteiaQuality(float middleClassShare, float ruleOfLaw)
            => Mathf.Clamp01(Mathf.Clamp01(middleClassShare) * Mathf.Clamp01(ruleOfLaw));

        /// <summary>砂時計型社会の判定（中間層が消え二極化＝中間層シェアが既定閾値未満で true）。</summary>
        public static bool IsHourglassSociety(float middleClassShare)
            => IsHourglassSociety(middleClassShare, MesoiParams.Default.hourglassThreshold);

        /// <summary>砂時計型社会の判定（中間層シェアが threshold 未満で true）。</summary>
        public static bool IsHourglassSociety(float middleClassShare, float threshold)
            => Mathf.Clamp01(middleClassShare) < Mathf.Clamp01(threshold);
    }
}
