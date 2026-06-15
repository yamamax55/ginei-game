using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 投資環境＝投資家心理（血気）の純データ（KEYN-3 #1545）。
    /// 信認・楽観・投資水準を保持する可変フィールドの容れ物（ロジックは <see cref="AnimalSpiritsRules"/> が持つ）。
    /// </summary>
    public struct InvestmentClimate
    {
        /// <summary>信認（0..1）＝市場・将来への確信。崩れると投資が一斉に凍結する。</summary>
        public float confidence;
        /// <summary>楽観（0..1・0.5中立）＝血気の楽観/悲観の度合い（高いほど強気）。</summary>
        public float optimism;
        /// <summary>投資水準（0..1）＝実際に投じられる投資の量。</summary>
        public float investmentLevel;

        public InvestmentClimate(float confidence, float optimism, float investmentLevel)
        {
            this.confidence = Mathf.Clamp01(confidence);
            this.optimism = Mathf.Clamp01(optimism);
            this.investmentLevel = Mathf.Clamp01(investmentLevel);
        }
    }

    /// <summary>アニマルスピリッツ（血気＝投資家心理）の調整係数。</summary>
    public readonly struct AnimalSpiritsParams
    {
        /// <summary>信認がこの閾値を割ると投資が一斉に凍結する（突然の手控え）。</summary>
        public readonly float freezeThreshold;
        /// <summary>信認崩壊（投資の総凍結）とみなす下限。これ未満は血気が完全に失われた状態。</summary>
        public readonly float collapseThreshold;
        /// <summary>経済ニュースが信認に効く速さ（/戦略秒）。</summary>
        public readonly float newsSensitivity;
        /// <summary>群集心理（herding）が信認の振れを増幅する強さ（群れるほど振れる）。</summary>
        public readonly float herdAmplification;
        /// <summary>需要不足が悲観を強める速さ（/戦略秒・負のスパイラル）。</summary>
        public readonly float pessimismRate;
        /// <summary>好況が楽観を強める速さ（/戦略秒・正のスパイラル）。</summary>
        public readonly float optimismRate;
        /// <summary>政策の信認回復シグナルが血気を取り戻す効き（期待への働きかけ）。</summary>
        public readonly float revivalStrength;

        public AnimalSpiritsParams(float freezeThreshold, float collapseThreshold, float newsSensitivity,
            float herdAmplification, float pessimismRate, float optimismRate, float revivalStrength)
        {
            this.freezeThreshold = Mathf.Clamp01(freezeThreshold);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
            this.newsSensitivity = Mathf.Max(0f, newsSensitivity);
            this.herdAmplification = Mathf.Max(0f, herdAmplification);
            this.pessimismRate = Mathf.Max(0f, pessimismRate);
            this.optimismRate = Mathf.Max(0f, optimismRate);
            this.revivalStrength = Mathf.Max(0f, revivalStrength);
        }

        /// <summary>
        /// 既定＝凍結閾値0.3・崩壊閾値0.15・ニュース感応0.5・群集増幅0.5・悲観率0.4・楽観率0.3・回復強度0.5。
        /// </summary>
        public static AnimalSpiritsParams Default =>
            new AnimalSpiritsParams(0.3f, 0.15f, 0.5f, 0.5f, 0.4f, 0.3f, 0.5f);
    }

    /// <summary>
    /// アニマルスピリッツ（血気）の純ロジック（KEYN-3 #1545・ケインズ『一般理論』参考）。
    /// 投資は合理的計算でなく「血気（animal spirits）＝楽観・悲観の心理」に左右され、信認が崩れると投資が
    /// 一斉に凍結し需要不足を呼び、それがさらに悲観を強める自己強化スパイラルを生む＝美人投票（他人の予想を
    /// 予想する集団心理）。「投資は心理に駆動され、信認崩壊が投資を凍結し需要不足を呼びさらに悲観を強める
    /// 自己強化スパイラル」を式に出す。<see cref="StockMarketRules"/>（株価の公正価値と暴落）／
    /// <see cref="CrisisCycleRules"/>（ミンスキー金融循環の相遷移）とは別＝こちらは投資家心理（血気）の
    /// 集合的な楽観/悲観のスパイラル。需要不足そのものの帰結は <see cref="EffectiveDemandRules"/>（同EPIC・
    /// 有効需要）へ委譲する。全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AnimalSpiritsRules
    {
        /// <summary>
        /// 投資意欲（0..1）＝信認×楽観×期待収益。心理（信認・楽観）が投資を駆動する＝合理的計算だけでは
        /// 決まらず、血気が掛かって初めて投資が湧く。いずれか1つでも0なら意欲は0（積＝心理が支える）。
        /// </summary>
        public static float InvestmentAppetite(float confidence, float optimism, float expectedReturn)
        {
            float c = Mathf.Clamp01(confidence);
            float o = Mathf.Clamp01(optimism);
            float r = Mathf.Clamp01(expectedReturn);
            return Mathf.Clamp01(c * o * r);
        }

        /// <summary>
        /// 1tick の信認の変動（0..1）＝経済ニュース(-1..1)で上下し、群集心理(herding)がその振れを増幅する
        /// （群れるほど同じ方向へ大きく振れる＝過剰反応）。良いニュースで信認が高まり、悪いニュースで崩れる。
        /// </summary>
        public static float ConfidenceTick(float confidence, float economicNews, float herding, float dt, AnimalSpiritsParams p)
        {
            float c = Mathf.Clamp01(confidence);
            float news = Mathf.Clamp(economicNews, -1f, 1f);
            float herd = Mathf.Clamp01(herding);
            float step = Mathf.Max(0f, dt);
            // 群集心理は振れを増幅する＝(1 + herdAmplification×herding) 倍
            float amplification = 1f + p.herdAmplification * herd;
            float delta = news * p.newsSensitivity * amplification * step;
            return Mathf.Clamp01(c + delta);
        }

        public static float ConfidenceTick(float confidence, float economicNews, float herding, float dt)
            => ConfidenceTick(confidence, economicNews, herding, dt, AnimalSpiritsParams.Default);

        /// <summary>
        /// 投資凍結＝信認が閾値を割ると投資が一斉に凍結するか（突然の手控え）。
        /// 信認 &lt; threshold で true（血気が失われ、合理的に見合う案件でも投資が止まる）。
        /// </summary>
        public static bool InvestmentFreeze(float confidence, float threshold)
        {
            return Mathf.Clamp01(confidence) < Mathf.Clamp01(threshold);
        }

        public static bool InvestmentFreeze(float confidence, AnimalSpiritsParams p)
            => InvestmentFreeze(confidence, p.freezeThreshold);

        public static bool InvestmentFreeze(float confidence)
            => InvestmentFreeze(confidence, AnimalSpiritsParams.Default);

        /// <summary>
        /// 悲観スパイラル（負）＝需要不足が悲観を強める1tick後の楽観(0..1)。需要不足が大きいほど
        /// 楽観が下がり（＝悲観が深まり）、それが投資をさらに凍らせる自己強化のループを表す。
        /// demandShortfall(0..1)＝需要の不足度。下げ幅＝needs×pessimismRate×dt。
        /// </summary>
        public static float PessimismSpiral(float optimism, float demandShortfall, float dt, AnimalSpiritsParams p)
        {
            float o = Mathf.Clamp01(optimism);
            float shortfall = Mathf.Clamp01(demandShortfall);
            float step = Mathf.Max(0f, dt);
            float drop = shortfall * p.pessimismRate * step;
            return Mathf.Clamp01(o - drop);
        }

        public static float PessimismSpiral(float optimism, float demandShortfall, float dt)
            => PessimismSpiral(optimism, demandShortfall, dt, AnimalSpiritsParams.Default);

        /// <summary>
        /// 楽観スパイラル（正）＝好況が楽観を強める1tick後の楽観(0..1)。需要が強いほど楽観が高まり、
        /// それがさらに投資を呼ぶ＝バブルの心理（自己強化の正のループ）。
        /// demandStrength(0..1)＝需要の強さ。上げ幅＝strength×optimismRate×dt。
        /// </summary>
        public static float OptimismSpiral(float optimism, float demandStrength, float dt, AnimalSpiritsParams p)
        {
            float o = Mathf.Clamp01(optimism);
            float strength = Mathf.Clamp01(demandStrength);
            float step = Mathf.Max(0f, dt);
            float rise = strength * p.optimismRate * step;
            return Mathf.Clamp01(o + rise);
        }

        public static float OptimismSpiral(float optimism, float demandStrength, float dt)
            => OptimismSpiral(optimism, demandStrength, dt, AnimalSpiritsParams.Default);

        /// <summary>
        /// 美人投票（0..1）＝ケインズの株式市場観。投資判断は自分の予想(ownView)でなく、他人がどう予想するかの
        /// 予想(perceivedConsensus)に引きずられる＝合意へ寄せた実効的な見立て。
        /// 自分の見方を市場のコンセンサスへ半分寄せる（平均＝他人の予想を予想する集団心理）。
        /// </summary>
        public static float BeautyContest(float ownView, float perceivedConsensus)
        {
            float own = Mathf.Clamp01(ownView);
            float consensus = Mathf.Clamp01(perceivedConsensus);
            return Mathf.Clamp01(Mathf.Lerp(own, consensus, 0.5f));
        }

        /// <summary>
        /// 血気の回復（0..1）＝政策の信認回復シグナル(policySignal 0..1)が血気を取り戻す1tick後の信認。
        /// 期待への働きかけ＝政策が市場心理に直接効き、残りの伸びしろ((1−信認))をシグナル比例で埋める。
        /// </summary>
        public static float SpiritRevival(float confidence, float policySignal, AnimalSpiritsParams p)
        {
            float c = Mathf.Clamp01(confidence);
            float signal = Mathf.Clamp01(policySignal);
            float headroom = 1f - c; // 残りの伸びしろ
            float rise = headroom * signal * p.revivalStrength;
            return Mathf.Clamp01(c + rise);
        }

        public static float SpiritRevival(float confidence, float policySignal)
            => SpiritRevival(confidence, policySignal, AnimalSpiritsParams.Default);

        /// <summary>
        /// 信認崩壊（投資の総凍結）判定＝信認が崩壊閾値(collapseThreshold)未満に落ちたか。
        /// 凍結（freezeThreshold）よりさらに深い＝血気が完全に失われ、投資が総凍結する臨界。
        /// </summary>
        public static bool IsConfidenceCollapse(float confidence, AnimalSpiritsParams p)
        {
            return Mathf.Clamp01(confidence) < p.collapseThreshold;
        }

        public static bool IsConfidenceCollapse(float confidence)
            => IsConfidenceCollapse(confidence, AnimalSpiritsParams.Default);
    }
}
