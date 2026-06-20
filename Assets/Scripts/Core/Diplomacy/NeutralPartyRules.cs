using UnityEngine;

namespace Ginei
{
    /// <summary>中立勢力（フェザーン型の通商自治領）の力学を調整する係数。</summary>
    public readonly struct NeutralPartyParams
    {
        /// <summary>均衡の鋭さ（1で線形、大きいほど僅かな偏りでも中立成立度が落ちる）。</summary>
        public readonly float balanceSharpness;
        /// <summary>経済的てこが中立維持に効く重み（0..1）。</summary>
        public readonly float leverageWeight;
        /// <summary>交易繁栄に中立度が効く指数（0で交易量のみ、1で中立度を完全に乗算）。</summary>
        public readonly float neutralityExponent;
        /// <summary>戦略価値に交易繁栄が効く重み（0..1）。残りは要衝性。</summary>
        public readonly float prosperityWeight;
        /// <summary>圧力で中立崩壊リスクが立ち上がる勾配（1/(1+k*x) の k）。</summary>
        public readonly float pressureSlope;
        /// <summary>参戦時に中立価値を失う割合（0..1）。</summary>
        public readonly float neutralityLossShare;

        public NeutralPartyParams(float balanceSharpness, float leverageWeight, float neutralityExponent,
            float prosperityWeight, float pressureSlope, float neutralityLossShare)
        {
            this.balanceSharpness = Mathf.Max(0f, balanceSharpness);
            this.leverageWeight = Mathf.Clamp01(leverageWeight);
            this.neutralityExponent = Mathf.Clamp01(neutralityExponent);
            this.prosperityWeight = Mathf.Clamp01(prosperityWeight);
            this.pressureSlope = Mathf.Max(0f, pressureSlope);
            this.neutralityLossShare = Mathf.Clamp01(neutralityLossShare);
        }

        /// <summary>既定＝均衡鋭さ1.0／てこ重み0.5／中立指数1.0／繁栄重み0.6／圧力勾配1.0／中立喪失0.7。</summary>
        public static NeutralPartyParams Default
            => new NeutralPartyParams(1f, 0.5f, 1f, 0.6f, 1f, 0.7f);
    }

    /// <summary>
    /// 中立勢力（フェザーン型＝二大勢力の狭間に立つ交易自治領）の純ロジック。
    /// 中立の成立度（両陣営の軍事均衡×自国の経済的てこ）→交易による繁栄（中立だからこそ両陣営と商える）
    /// →両陣営への影響力（経済規模×代替不可能性）→戦略的価値（要衝性×繁栄＝狙われやすさ）
    /// →交戦国から受ける圧力→中立崩壊リスク→参戦の期待利得→中立を保つかの判断、を一貫して数値化する。
    /// <see cref="BufferStateRules"/>（緩衝国の生存術＝三体問題の構造）の通商国家版＝交易・影響力・参戦判断に特化。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NeutralPartyRules
    {
        /// <summary>中立を保つ既定の閾値（中立成立度−参戦利得 がこれ以上なら中立維持）。</summary>
        public const float DefaultStayThreshold = 0f;

        /// <summary>
        /// 中立維持の成立度（0..1）＝両陣営の軍事均衡×（経済的てこの底上げ）。
        /// militaryBalance は -1（A優勢）..+1（B優勢）。0（拮抗）で最大、どちらかへ偏るほど均衡が崩れ中立が死ぬ。
        /// 均衡基礎＝1−|balance|^sharpness。これに経済的てこが leverageWeight ぶん下支えする
        /// （てこ＝両陣営が手を出しにくくする自立力）。
        /// </summary>
        public static float NeutralityViability(float militaryBalance, float economicLeverage, NeutralPartyParams p)
        {
            float tilt = Mathf.Abs(Mathf.Clamp(militaryBalance, -1f, 1f));
            float balanceBase = 1f - Mathf.Pow(tilt, Mathf.Max(0.0001f, p.balanceSharpness));
            balanceBase = Mathf.Clamp01(balanceBase);
            float lev = Mathf.Clamp01(economicLeverage);
            // 均衡が骨格・経済てこが下支え：均衡0でも てこ×重み ぶんは生き残る。
            float floor = p.leverageWeight * lev;
            return Mathf.Clamp01(balanceBase + (1f - balanceBase) * floor);
        }

        public static float NeutralityViability(float militaryBalance, float economicLeverage)
            => NeutralityViability(militaryBalance, economicLeverage, NeutralPartyParams.Default);

        /// <summary>
        /// 交易による繁栄（0..1）＝交易量×中立度^指数。中立だからこそ両陣営と商える＝
        /// 中立が崩れるほど（neutrality→0）繁栄は痩せる。neutralityExponent=0なら交易量のみ。
        /// </summary>
        public static float TradeProsperity(float throughput, float neutrality, NeutralPartyParams p)
        {
            float t = Mathf.Clamp01(throughput);
            float n = Mathf.Clamp01(neutrality);
            float factor = Mathf.Pow(n, p.neutralityExponent); // 指数0で1.0＝中立非依存
            return Mathf.Clamp01(t * factor);
        }

        public static float TradeProsperity(float throughput, float neutrality)
            => TradeProsperity(throughput, neutrality, NeutralPartyParams.Default);

        /// <summary>
        /// 両陣営への影響力（0..1）＝経済規模×代替不可能性。大きく、かつ替えが利かないほど
        /// 言葉が両首都に届く。どちらか一方が欠ければ影響力は消える（積）。
        /// </summary>
        public static float Leverage(float economicWeight, float indispensability, NeutralPartyParams p)
        {
            // p は将来拡張用（現状は単純積・他メソッドと署名を揃える）。
            return Mathf.Clamp01(economicWeight) * Mathf.Clamp01(indispensability);
        }

        public static float Leverage(float economicWeight, float indispensability)
            => Leverage(economicWeight, indispensability, NeutralPartyParams.Default);

        /// <summary>
        /// 戦略的価値（0..1）＝要衝性×prosperityWeight補間で繁栄を織り込む。
        /// 要衝（location）であるほど、また繁栄しているほど狙われる価値が高い。
        /// 値＝location×(1−w + w×prosperity)＝繁栄が weight ぶん上乗せ。
        /// </summary>
        public static float StrategicValue(float location, float tradeProsperity, NeutralPartyParams p)
        {
            float loc = Mathf.Clamp01(location);
            float pros = Mathf.Clamp01(tradeProsperity);
            float w = p.prosperityWeight;
            return Mathf.Clamp01(loc * ((1f - w) + w * pros));
        }

        public static float StrategicValue(float location, float tradeProsperity)
            => StrategicValue(location, tradeProsperity, NeutralPartyParams.Default);

        /// <summary>
        /// 交戦国から受ける圧力（0..1）＝戦略価値×交戦国の必要度。価値が高く、かつ交戦国が
        /// それを欲するほど圧力が強い（積）。p は将来拡張用。
        /// </summary>
        public static float PressureFromBelligerent(float strategicValue, float belligerentNeed, NeutralPartyParams p)
        {
            return Mathf.Clamp01(strategicValue) * Mathf.Clamp01(belligerentNeed);
        }

        public static float PressureFromBelligerent(float strategicValue, float belligerentNeed)
            => PressureFromBelligerent(strategicValue, belligerentNeed, NeutralPartyParams.Default);

        /// <summary>
        /// 中立崩壊リスク（0..1）＝圧力が影響力を上回るぶんが立ち上がる。
        /// net＝max(0, pressure−leverage)＝影響力で相殺し切れない圧力。
        /// risk＝1−1/(1+slope×net)＝net=0で0、netが増えるほど飽和しつつ1へ。
        /// </summary>
        public static float NeutralityBreakRisk(float pressure, float leverage, NeutralPartyParams p)
        {
            float net = Mathf.Max(0f, Mathf.Clamp01(pressure) - Mathf.Clamp01(leverage));
            float risk = 1f - 1f / (1f + p.pressureSlope * net);
            return Mathf.Clamp01(risk);
        }

        public static float NeutralityBreakRisk(float pressure, float leverage)
            => NeutralityBreakRisk(pressure, leverage, NeutralPartyParams.Default);

        /// <summary>
        /// 参戦（中立放棄）の期待利得（-1..1）＝勝者側に付く期待利得−中立を捨てる損失。
        /// gain＝winnerProbability×spoilsShare（勝つ見込み×取り分）。
        /// loss＝neutralityLossShare×neutralityValue（中立で得ていた価値の喪失）。
        /// 正なら参戦が得・負なら中立が得。
        /// </summary>
        public static float EntryPayoff(float winnerProbability, float spoilsShare, float neutralityValue, NeutralPartyParams p)
        {
            float gain = Mathf.Clamp01(winnerProbability) * Mathf.Clamp01(spoilsShare);
            float loss = p.neutralityLossShare * Mathf.Clamp01(neutralityValue);
            return Mathf.Clamp(gain - loss, -1f, 1f);
        }

        public static float EntryPayoff(float winnerProbability, float spoilsShare, float neutralityValue)
            => EntryPayoff(winnerProbability, spoilsShare, neutralityValue, NeutralPartyParams.Default);

        /// <summary>
        /// 中立を保つか（true＝中立維持）。中立成立度−参戦利得 が threshold 以上なら中立を保つ。
        /// 参戦利得が中立成立度を threshold ぶん上回って初めて中立を捨てる＝慎重側にバイアス。
        /// </summary>
        public static bool ShouldStayNeutral(float neutralityViability, float entryPayoff, float threshold)
        {
            float v = Mathf.Clamp01(neutralityViability);
            float e = Mathf.Clamp(entryPayoff, -1f, 1f);
            return (v - e) >= threshold;
        }

        public static bool ShouldStayNeutral(float neutralityViability, float entryPayoff)
            => ShouldStayNeutral(neutralityViability, entryPayoff, DefaultStayThreshold);
    }
}
