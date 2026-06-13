using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 信託（trust）の調整係数（LOCK-2 #1450・ロック『統治二論』）。既定は <see cref="Default"/>。
    /// </summary>
    public readonly struct TrustMandateParams
    {
        /// <summary>信託の健全さで合意（consentBasis）が占める重み（残りは権利保護）。</summary>
        public readonly float consentWeight;
        /// <summary>侵犯の蓄積がこれを超えると信託が解消される既定閾値（もはや正統な政府でない）。</summary>
        public readonly float dissolutionThreshold;
        /// <summary>侵犯1あたりの信託侵食/秒の効き（蓄積が信託を蝕む速さ）。</summary>
        public readonly float erosionWeight;

        public TrustMandateParams(float consentWeight, float dissolutionThreshold, float erosionWeight)
        {
            this.consentWeight = consentWeight;
            this.dissolutionThreshold = dissolutionThreshold;
            this.erosionWeight = erosionWeight;
        }

        /// <summary>既定（合意の重み0.5／信託解消閾値0.6／侵食の重み0.8）。</summary>
        public static TrustMandateParams Default => new TrustMandateParams(0.5f, 0.6f, 0.8f);
    }

    /// <summary>
    /// 信託解消連鎖＝ロックの「政府は人民からの信託（trust）」の純ロジック（LOCK-2 #1450・参考『統治二論』）。
    /// 人民は自然権の一部を政府に託すが、その目的は<b>生命・自由・財産の保護</b>。政府がこの信託に違反して
    /// 人民の権利を侵害（侵犯）すると、侵犯が蓄積して信託が解消され、人民は政府を解体し作り直す<b>抵抗権</b>を
    /// 持つ＝ホッブズと違い服従は無条件でない（侵犯蓄積→信託解消→反乱正当化）。乱数なし・決定論。
    /// <para>
    /// 分担：<see cref="ConsentRules"/>(被治者の協力＝合意撤回 <see cref="ConsentRules.Withdraw"/> ＝再構成の<b>転送先</b>)／
    /// <see cref="CovenantRules"/>(ホッブズの保護と服従＝生成済み・<b>対比</b>。ホッブズは保護失敗でのみ服従消滅で抵抗権なし、ロックは信託違反で抵抗権が発動)／
    /// <see cref="MagnaCartaRules"/>(抵抗権＝契約による王権制約)／
    /// <see cref="PropertyOriginRules"/>(同EPIC LOCK＝労働による所有の起源＝守るべき財産権の発生源)。本クラスは
    /// 「政府は信託で権利保護が目的だが侵犯が蓄積して信託に違反すると信託は解消され人民は政府を作り直す抵抗権を持つ」を式に出す。
    /// </para>
    /// </summary>
    public static class TrustMandateRules
    {
        /// <summary>
        /// 政府への信託（trust・0..1）＝政府が権利を保護し合意に基づくほど信託が健全（人民が託す正統性）。
        /// 権利保護と合意の加重和。どちらかが欠けても信託は不全（守らない／同意のない政府は託すに値しない）。
        /// </summary>
        public static float MandateTrust(float rightsProtection, float consentBasis, TrustMandateParams p)
        {
            float rights = Mathf.Clamp01(rightsProtection);
            float consent = Mathf.Clamp01(consentBasis);
            float w = Mathf.Clamp01(p.consentWeight);
            return Mathf.Clamp01((1f - w) * rights + w * consent);
        }

        /// <summary>政府への信託（既定パラメータ）。</summary>
        public static float MandateTrust(float rightsProtection, float consentBasis)
            => MandateTrust(rightsProtection, consentBasis, TrustMandateParams.Default);

        /// <summary>
        /// 権利侵害（侵犯）の蓄積（0..1）＝政府の生命・自由・財産への侵犯が時間で積み重なる（一度でなく蓄積）。
        /// 既存の蓄積に新たな侵犯×dt を加算。ロックの「a long train of abuses（長く続く濫用の連鎖）」。
        /// </summary>
        public static float ViolationAccumulation(float currentViolations, float newViolation, float dt)
        {
            float current = Mathf.Clamp01(currentViolations);
            float added = Mathf.Clamp01(newViolation);
            float step = dt <= 0f ? 0f : added * dt;
            return Mathf.Clamp01(current + step);
        }

        /// <summary>
        /// 信託の侵食（0..1）＝侵犯の蓄積が信託を蝕む（保護の約束が裏切られる）。
        /// 健全な信託でも侵犯の蓄積に比例して削られる＝侵食＝信託×蓄積（蓄積が無ければ侵食なし）。
        /// </summary>
        public static float TrustErosion(float mandateTrust, float violationAccumulation, TrustMandateParams p)
        {
            float trust = Mathf.Clamp01(mandateTrust);
            float accum = Mathf.Clamp01(violationAccumulation);
            float w = Mathf.Clamp01(p.erosionWeight);
            return Mathf.Clamp01(trust * accum * w);
        }

        /// <summary>信託の侵食（既定パラメータ）。</summary>
        public static float TrustErosion(float mandateTrust, float violationAccumulation)
            => TrustErosion(mandateTrust, violationAccumulation, TrustMandateParams.Default);

        /// <summary>
        /// 信託の解消度（0..1）＝侵犯の蓄積が閾値を超えると信託が解消される（もはや正統な政府でない）。
        /// 閾値手前では0、閾値超で蓄積に比例して解消が進む（ロック＝信託違反で政府は解体されうる）。
        /// </summary>
        public static float DissolutionThreshold(float violationAccumulation, float threshold)
        {
            float accum = Mathf.Clamp01(violationAccumulation);
            float th = Mathf.Clamp01(threshold);
            if (accum <= th) return 0f;
            float span = Mathf.Max(0.0001f, 1f - th);
            return Mathf.Clamp01((accum - th) / span);
        }

        /// <summary>信託の解消度（既定閾値）。</summary>
        public static float DissolutionThreshold(float violationAccumulation)
            => DissolutionThreshold(violationAccumulation, TrustMandateParams.Default.dissolutionThreshold);

        /// <summary>
        /// 反乱の正当化（0..1）＝信託違反（侵食）が著しく、かつ平和的救済が尽きると反乱が正当化される（ロックの抵抗権）。
        /// 平和的救済が残るうちは正当性は割り引かれる（exhausted=1で侵食ぶん満額＝抵抗が正当な最後の手段）。
        /// </summary>
        public static float RebellionJustification(float trustErosion, float peacefulRemedyExhausted)
        {
            float erosion = Mathf.Clamp01(trustErosion);
            float exhausted = Mathf.Clamp01(peacefulRemedyExhausted);
            return Mathf.Clamp01(erosion * exhausted);
        }

        /// <summary>
        /// 政府を作り直す権利（0..1）＝信託解消後、人民が政府を解体し作り直す権利（合意の撤回＋新主権の樹立）。
        /// 解消が進むほど再構成の権利は強い（<see cref="ConsentRules.Withdraw"/> へ転送＝協力を引き上げ新政府を立てる）。
        /// </summary>
        public static float RightToReconstitute(float dissolution)
            => Mathf.Clamp01(dissolution);

        /// <summary>
        /// 天への訴え（0..1）＝地上の裁定者がいない究極の場合の抵抗（ロックの「appeal to heaven」＝実力での抵抗）。
        /// 侵害が著しく、かつ訴える上位の裁定者がいないほど、人民は実力で抵抗するほかない（共通の裁き手の不在）。
        /// </summary>
        public static float AppealToHeaven(float violationSeverity, float noEarthlyJudge)
        {
            float severity = Mathf.Clamp01(violationSeverity);
            float noJudge = Mathf.Clamp01(noEarthlyJudge);
            return Mathf.Clamp01(severity * noJudge);
        }

        /// <summary>
        /// 信託が破れ政府が正統性を失った判定＝信託の侵食が閾値を超えたとき true（信託違反＝政府は信託を失う）。
        /// </summary>
        public static bool IsTrustBroken(float trustErosion, float threshold)
            => Mathf.Clamp01(trustErosion) > Mathf.Clamp01(threshold);

        /// <summary>信託破綻判定（既定閾値）。</summary>
        public static bool IsTrustBroken(float trustErosion)
            => IsTrustBroken(trustErosion, TrustMandateParams.Default.dissolutionThreshold);
    }
}
