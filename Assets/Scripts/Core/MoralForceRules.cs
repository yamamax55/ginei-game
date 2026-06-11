using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 浩然之気（こうぜんのき）の純データ（孟子 MENC-4 #1570）。一貫した善政・義の積み重ねが指導者の内に
    /// 蓄えた道徳的気力。<see cref="qi"/>＝蓄積した気（至大至剛のたね）・<see cref="consistency"/>＝言行一致の一貫性。
    /// すべて 0..1。基準値ではなくこの状態量から係数を導く（実効値パターン）。
    /// </summary>
    public struct MoralForce
    {
        /// <summary>浩然の気 0..1＝義の積み重ねでしか養えない道徳的気力（不義で一気に損なわれる）。</summary>
        public float qi;
        /// <summary>一貫性 0..1＝言行一致の度合い（矛盾で下がる）。カリスマ係数に効く。</summary>
        public float consistency;

        public MoralForce(float qi, float consistency)
        {
            this.qi = Mathf.Clamp01(qi);
            this.consistency = Mathf.Clamp01(consistency);
        }
    }

    /// <summary>浩然之気の調整係数（孟子型・MENC-4 #1570）。</summary>
    public readonly struct MoralForceParams
    {
        /// <summary>義の行い1のとき/秒に養われる気の基礎（蓄積はゆっくり＝日々の正しさの積み重ね）。</summary>
        public readonly float accumulationRate;
        /// <summary>不義1のとき一度に損なわれる気の基礎（蓄積より速い＝非対称の急落）。</summary>
        public readonly float injuryScale;
        /// <summary>助長（無理な育成）1のとき損なう気の基礎（苗を引っ張ると枯れる＝逆効果）。</summary>
        public readonly float forcingScale;
        /// <summary>気が満ちたときの忠誠係数の上乗せ幅（気1で 1.0+この値）。</summary>
        public readonly float loyaltyBonusScale;
        /// <summary>気と一貫性が満ちたときのカリスマ係数の上乗せ幅（気×一貫性1で 1.0+この値）。</summary>
        public readonly float charismaBonusScale;
        /// <summary>気が満ちたときに与える士気の床の最大（至大至剛＝外圧に動じない不動の意志）。</summary>
        public readonly float resolveFloorScale;
        /// <summary>言行一致が続くとき/秒に上がる一貫性（矛盾はこの倍速で下がる）。</summary>
        public readonly float consistencyRate;
        /// <summary>浩然の気が満ちたと判定する既定の閾値。</summary>
        public readonly float floodlikeThreshold;

        public MoralForceParams(float accumulationRate, float injuryScale, float forcingScale,
            float loyaltyBonusScale, float charismaBonusScale, float resolveFloorScale,
            float consistencyRate, float floodlikeThreshold)
        {
            this.accumulationRate = Mathf.Max(0f, accumulationRate);
            this.injuryScale = Mathf.Max(0f, injuryScale);
            this.forcingScale = Mathf.Max(0f, forcingScale);
            this.loyaltyBonusScale = Mathf.Max(0f, loyaltyBonusScale);
            this.charismaBonusScale = Mathf.Max(0f, charismaBonusScale);
            this.resolveFloorScale = Mathf.Clamp01(resolveFloorScale);
            this.consistencyRate = Mathf.Max(0f, consistencyRate);
            this.floodlikeThreshold = Mathf.Clamp01(floodlikeThreshold);
        }

        /// <summary>
        /// 既定＝蓄積0.02/秒・不義急落0.4・助長弊害0.15・忠誠上乗せ0.5・カリスマ上乗せ0.6・
        /// 不動の床0.4・一貫性0.05/秒・浩然閾値0.7。蓄積はゆっくり、不義は一気＝非対称（0.02 ≪ 0.4）。
        /// </summary>
        public static MoralForceParams Default => new MoralForceParams(0.02f, 0.4f, 0.15f, 0.5f, 0.6f, 0.4f, 0.05f, 0.7f);
    }

    /// <summary>
    /// 浩然之気（こうぜんのき）の純ロジック（孟子 MENC-4 #1570）。一貫した善政・義の積み重ねが指導者の内に
    /// <b>道徳的気力（浩然の気）</b>を蓄え、それが忠誠・カリスマ・不動の意志の係数になる。孟子の核は三つ：
    /// ①義を集め積むことでしか養われない（<see cref="AccumulationTick"/>＝日々の正しさの蓄積＝ゆっくり）／
    /// ②一度の不義で一気に損なわれる（<see cref="InjuryFromInjustice"/>＝蓄積より速い非対称）／
    /// ③無理に育てようとすると却って枯れる（<see cref="NoForcingPenalty"/>＝「助長するな」＝苗を引っ張る愚）。
    /// 養われた気は部下の忠誠を高め（<see cref="LoyaltyCoefficient"/>）、一貫性とともにカリスマを高め
    /// （<see cref="CharismaCoefficient"/>）、満ちれば至大至剛＝外圧に動じない士気の床を与える（<see cref="UnshakableResolve"/>）。
    /// 基準値は変えず係数を返す（実効値パターン・≥1.0）。乱数なし・決定論・全入力クランプ。
    /// <see cref="FocusRules"/>（三密＝身口意の一時バフ＝極限集中）／<see cref="ReputationRules"/>（勝敗の武名）／
    /// <c>MoralSproutsRules</c>（四端＝惻隠・羞悪…の道徳の芽・同EPIC MENC）とは分担し、ここは
    /// <b>一貫した善政の積み重ねが生む持続的な道徳的気力（浩然之気）</b>を扱う。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MoralForceRules
    {
        /// <summary>
        /// 義の行いを積んで気を養った新 <see cref="MoralForce"/>（<see cref="AccumulationTick"/>）。
        /// 増分＝義の行い(0..1)×蓄積率×dt＝<b>日々の正しさの蓄積でしか育たない（ゆっくり）</b>。
        /// 義の行いが0なら気は増えない（積まなければ養われない）。気のみ更新し一貫性は据え置く。
        /// </summary>
        public static MoralForce AccumulationTick(MoralForce mf, float righteousAct, float dt, MoralForceParams p)
        {
            float act = Mathf.Clamp01(righteousAct);
            float step = Mathf.Max(0f, dt);
            float qi = Mathf.Clamp01(mf.qi + act * p.accumulationRate * step);
            return new MoralForce(qi, mf.consistency);
        }

        public static MoralForce AccumulationTick(MoralForce mf, float righteousAct, float dt)
            => AccumulationTick(mf, righteousAct, dt, MoralForceParams.Default);

        /// <summary>
        /// 一度の不義が気を一気に損なう（損なわれた後の気 0..1）。損失＝不義の重さ(0..1)×<see cref="MoralForceParams.injuryScale"/>。
        /// dt を取らない＝<b>瞬間の出来事</b>。既定では1回の重い不義で蓄積数十秒ぶんを吹き飛ばす＝
        /// 積むのはゆっくり崩すのは一瞬という<b>非対称</b>。
        /// </summary>
        public static float InjuryFromInjustice(float qi, float injusticeSeverity, MoralForceParams p)
        {
            float q = Mathf.Clamp01(qi);
            float sev = Mathf.Clamp01(injusticeSeverity);
            return Mathf.Clamp01(q - sev * p.injuryScale);
        }

        public static float InjuryFromInjustice(float qi, float injusticeSeverity)
            => InjuryFromInjustice(qi, injusticeSeverity, MoralForceParams.Default);

        /// <summary>
        /// 無理に育てようとすると却って損なう（助長後の気 0..1）。孟子の宋人の譬え＝苗を早く伸ばそうと
        /// 引っ張ると枯らす。損失＝助長の度合い(0..1)×<see cref="MoralForceParams.forcingScale"/>。
        /// <b>気は積むことでしか養えず、急がせれば失う</b>（助長＝0なら損なわない）。
        /// </summary>
        public static float NoForcingPenalty(float qi, float forcedCultivation, MoralForceParams p)
        {
            float q = Mathf.Clamp01(qi);
            float force = Mathf.Clamp01(forcedCultivation);
            return Mathf.Clamp01(q - force * p.forcingScale);
        }

        public static float NoForcingPenalty(float qi, float forcedCultivation)
            => NoForcingPenalty(qi, forcedCultivation, MoralForceParams.Default);

        /// <summary>
        /// 浩然の気が部下の忠誠を高める係数（≥1.0＝基準非破壊の実効値）。1.0 + 気×<see cref="MoralForceParams.loyaltyBonusScale"/>。
        /// 気0で等倍、満ちるほど忠誠が増す（呼び出し側が忠誠値へ掛ける）。
        /// </summary>
        public static float LoyaltyCoefficient(float qi, MoralForceParams p)
            => 1f + Mathf.Clamp01(qi) * p.loyaltyBonusScale;

        public static float LoyaltyCoefficient(float qi)
            => LoyaltyCoefficient(qi, MoralForceParams.Default);

        /// <summary>
        /// 気と一貫性がカリスマを高める係数（≥1.0＝実効値）。1.0 + (気×一貫性)×<see cref="MoralForceParams.charismaBonusScale"/>。
        /// <b>言行一致(consistency)が伴ってはじめて気がカリスマになる</b>＝どちらか低いと積が縮み効きが落ちる
        /// （口先だけ・矛盾した気はカリスマにならない）。
        /// </summary>
        public static float CharismaCoefficient(float qi, float consistency, MoralForceParams p)
        {
            float q = Mathf.Clamp01(qi);
            float c = Mathf.Clamp01(consistency);
            return 1f + q * c * p.charismaBonusScale;
        }

        public static float CharismaCoefficient(float qi, float consistency)
            => CharismaCoefficient(qi, consistency, MoralForceParams.Default);

        /// <summary>
        /// 至大至剛＝気が満ちると外圧に動じない不動の意志（士気の床 0..1）。気×<see cref="MoralForceParams.resolveFloorScale"/>。
        /// 呼び出し側はこの値を士気の下限に使える＝<b>浩然の気が満ちた指揮官の麾下は崩れにくい</b>。
        /// </summary>
        public static float UnshakableResolve(float qi, MoralForceParams p)
            => Mathf.Clamp01(Mathf.Clamp01(qi) * p.resolveFloorScale);

        public static float UnshakableResolve(float qi)
            => UnshakableResolve(qi, MoralForceParams.Default);

        /// <summary>
        /// 言行一致が続くと一貫性が上がり、矛盾で下がる（dt後の一貫性 0..1）。actAligned=true なら
        /// <see cref="MoralForceParams.consistencyRate"/>×dt 増、false なら同率減＝<b>言ったことを行えば積み、
        /// 違えれば崩れる</b>（対称な綱引き）。
        /// </summary>
        public static float ConsistencyTick(float consistency, bool actAligned, float dt, MoralForceParams p)
        {
            float c = Mathf.Clamp01(consistency);
            float step = Mathf.Max(0f, dt);
            float delta = p.consistencyRate * step;
            return Mathf.Clamp01(actAligned ? c + delta : c - delta);
        }

        public static float ConsistencyTick(float consistency, bool actAligned, float dt)
            => ConsistencyTick(consistency, actAligned, dt, MoralForceParams.Default);

        /// <summary>
        /// 浩然の気が満ちた（至大至剛）判定＝気が閾値以上。義の積み重ねが一定に達した指導者は天地に恥じず動じない。
        /// </summary>
        public static bool IsFloodlikeQi(float qi, float threshold)
            => Mathf.Clamp01(qi) >= Mathf.Clamp01(threshold);

        public static bool IsFloodlikeQi(float qi)
            => IsFloodlikeQi(qi, MoralForceParams.Default.floodlikeThreshold);
    }
}
