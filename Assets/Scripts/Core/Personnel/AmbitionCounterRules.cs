using UnityEngine;

namespace Ginei
{
    /// <summary>野心相殺設計の調整係数（FED-2 #1476）。</summary>
    public readonly struct AmbitionCounterParams
    {
        /// <summary>地位への愛着の重み（制度的自己利益のうち、ポストそのものへの執着分）。</summary>
        public readonly float attachmentWeight;

        /// <summary>権力の利害の重み（自己利益のうち、権限の大きさが生む執着分）。</summary>
        public readonly float stakeWeight;

        /// <summary>自己利益の裏打ちが無いとき、紙の制限が保てる効力の下限（紙の防壁の弱さ）。</summary>
        public readonly float parchmentFloor;

        /// <summary>自己執行的均衡とみなす相互牽制力のしきい値。</summary>
        public readonly float selfEnforceThreshold;

        public AmbitionCounterParams(float attachmentWeight, float stakeWeight, float parchmentFloor, float selfEnforceThreshold)
        {
            this.attachmentWeight = Mathf.Clamp01(attachmentWeight);
            this.stakeWeight = Mathf.Clamp01(stakeWeight);
            this.parchmentFloor = Mathf.Clamp01(parchmentFloor);
            this.selfEnforceThreshold = Mathf.Clamp01(selfEnforceThreshold);
        }

        /// <summary>既定＝愛着重み0.5／利害重み0.5／紙の防壁の下限0.2／自己執行しきい値0.5。</summary>
        public static AmbitionCounterParams Default => new AmbitionCounterParams(0.5f, 0.5f, 0.2f, 0.5f);
    }

    /// <summary>
    /// 野心相殺設計の純ロジック（FED-2 #1476・『ザ・フェデラリスト』第51篇マディソン）。
    /// 「野心には野心をもって対抗させよ＝各統治部門の担当者に、自部門の権限を守る個人的動機（自己利益）を
    /// 与えれば、互いに侵害を防ぎ合い権力集中を抑える」を式にする。人が天使でない以上、紙の制限でなく
    /// 動機の対抗（counterveiling force）が抑制均衡を機能させる。各部門の自己利益が能動的に抑制を発動させ、
    /// 相互牽制が権力集中に抵抗するが、利害が同じ方を向くと牽制でなく結託に転ぶ。
    /// <para>分担：三権の抑制均衡そのものの配分は <see cref="SeparationOfPowersRules"/>、弾劾の発動は
    /// <see cref="ImpeachmentRules"/>、複合共和制（重ねた防壁）は CompoundRepublicRules（同EPIC FED）、
    /// 近衛が支配する逆の問題は <see cref="PraetorianRules"/>。本クラスは「自己利益が抑制を能動発動させる設計」に限る。</para>
    /// すべて plain 引数で完結（基準値非破壊・乱数なし決定論）。test-first。
    /// </summary>
    public static class AmbitionCounterRules
    {
        /// <summary>
        /// 制度的自己利益 0..1。担当者が自部門の権限を守ろうとする動機＝地位への愛着（officeAttachment）×
        /// 権力の利害（powerStake）を重み付き合成。愛着も利害も無ければ守る動機が無い＝0。
        /// </summary>
        public static float InstitutionalSelfInterest(float officeAttachment, float powerStake, AmbitionCounterParams p)
        {
            float a = Mathf.Clamp01(officeAttachment);
            float s = Mathf.Clamp01(powerStake);
            float wSum = p.attachmentWeight + p.stakeWeight;
            if (wSum <= 0f) return 0f;
            float interest = (a * p.attachmentWeight + s * p.stakeWeight) / wSum;
            return Mathf.Clamp01(interest);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float InstitutionalSelfInterest(float officeAttachment, float powerStake)
            => InstitutionalSelfInterest(officeAttachment, powerStake, AmbitionCounterParams.Default);

        /// <summary>
        /// 相互牽制力 0..1。二部門の自己利益がぶつかって相互牽制を生む＝野心が野心を相殺する。
        /// 牽制は両者が共に守る動機を持つときに最も強く、どちらかが無関心なら成立しない＝自己利益の積（幾何平均）。
        /// </summary>
        public static float CounterveilingForce(float selfInterestA, float selfInterestB)
        {
            float a = Mathf.Clamp01(selfInterestA);
            float b = Mathf.Clamp01(selfInterestB);
            // 両者が守る動機を持つほど牽制が強い（片方0なら相殺せず0）
            return Mathf.Clamp01(Mathf.Sqrt(a * b));
        }

        /// <summary>
        /// 抑制の発動 0..1。他部門の越権（encroachment）があると自己利益が抑制を能動的に発動させる。
        /// 侵害が無ければ反撃する理由が無い＝発動0。侵害されるほど牽制力に比例して反撃が強まる。
        /// </summary>
        public static float CheckActivation(float counterveilingForce, float encroachment)
        {
            float force = Mathf.Clamp01(counterveilingForce);
            float enc = Mathf.Clamp01(encroachment);
            // 侵害があって初めて自己利益が抑制を能動発動する（侵害×牽制力）
            return Mathf.Clamp01(force * enc);
        }

        /// <summary>
        /// 権力集中への抵抗 0..1。相互牽制が権力集中に抵抗する＝誰も独り占めできない。
        /// 牽制力が強いほど集中を阻む抵抗が直接強まる。
        /// </summary>
        public static float PowerConcentrationResistance(float counterveilingForce)
        {
            return Mathf.Clamp01(counterveilingForce);
        }

        /// <summary>
        /// 紙の防壁の弱さ＝紙の上の制限が実効的に保てる効力 0..1。法的制限（legalLimit）だけでは弱く、
        /// 自己利益の裏打ち（selfInterestBacking）があって初めて機能する（マディソン＝紙の防壁は破られる）。
        /// 裏打ちが無いと <see cref="AmbitionCounterParams.parchmentFloor"/> 止まり、満ちると法的制限がそのまま効く。
        /// </summary>
        public static float ParchmentBarrierWeakness(float legalLimit, float selfInterestBacking, AmbitionCounterParams p)
        {
            float limit = Mathf.Clamp01(legalLimit);
            float backing = Mathf.Clamp01(selfInterestBacking);
            // 裏打ちが無い＝floor、満ちる＝法的制限の額面。裏打ちで floor→limit へ持ち上がる
            float effective = limit * (p.parchmentFloor + (1f - p.parchmentFloor) * backing);
            return Mathf.Clamp01(effective);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float ParchmentBarrierWeakness(float legalLimit, float selfInterestBacking)
            => ParchmentBarrierWeakness(legalLimit, selfInterestBacking, AmbitionCounterParams.Default);

        /// <summary>
        /// 野心の整合 0..1。個人の野心（personalAmbition）と職務上の利害（officeInterest）が結びつくほど
        /// 抑制が強く働く＝地位を守ることが制度を守ることになる。両者が揃って初めて整合する＝積。
        /// </summary>
        public static float AmbitionAlignment(float personalAmbition, float officeInterest)
        {
            float amb = Mathf.Clamp01(personalAmbition);
            float interest = Mathf.Clamp01(officeInterest);
            // 個人の野心が職務の利害と一致するほど、地位を守る＝制度を守るに収束
            return Mathf.Clamp01(amb * interest);
        }

        /// <summary>
        /// 結託リスク 0..1。部門間の利害が一致する（sharedInterest）と牽制でなく結託に転ぶ＝野心が同じ方を向く危険。
        /// 共有利害が大きいほど互いに目をつむり、抑制が成立しなくなる。
        /// </summary>
        public static float CollusionRisk(float sharedInterest)
        {
            return Mathf.Clamp01(sharedInterest);
        }

        /// <summary>
        /// 自己執行的均衡か。相互牽制力（counterveilingForce）が threshold 以上なら、外部の監視に頼らず
        /// 各部門の自己利益で自律的に維持される抑制均衡が成立する＝true。
        /// </summary>
        public static bool IsSelfEnforcingBalance(float counterveilingForce, float threshold)
        {
            return Mathf.Clamp01(counterveilingForce) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定パラメータ版（<see cref="AmbitionCounterParams.selfEnforceThreshold"/> を使う）。</summary>
        public static bool IsSelfEnforcingBalance(float counterveilingForce, AmbitionCounterParams p)
            => IsSelfEnforcingBalance(counterveilingForce, p.selfEnforceThreshold);

        /// <summary>既定パラメータ版。</summary>
        public static bool IsSelfEnforcingBalance(float counterveilingForce)
            => IsSelfEnforcingBalance(counterveilingForce, AmbitionCounterParams.Default.selfEnforceThreshold);
    }
}
