using UnityEngine;

namespace Ginei
{
    /// <summary>恩赦の調整係数。</summary>
    public readonly struct AmnestyParams
    {
        /// <summary>全面恩赦が敗者側の統合に返す最大利得。</summary>
        public readonly float reconciliationScale;
        /// <summary>全面恩赦が被害者側に生む不満の最大量（裁かれない加害者を見る痛み）。</summary>
        public readonly float grievanceScale;
        /// <summary>原因未解決のまま許した場合の再発リスクの最大値。</summary>
        public readonly float maxRecidivism;

        public AmnestyParams(float reconciliationScale, float grievanceScale, float maxRecidivism)
        {
            this.reconciliationScale = Mathf.Max(0f, reconciliationScale);
            this.grievanceScale = Mathf.Max(0f, grievanceScale);
            this.maxRecidivism = Mathf.Clamp01(maxRecidivism);
        }

        /// <summary>既定＝和解利得0.4・被害者不満0.3・再発上限0.6。</summary>
        public static AmnestyParams Default => new AmnestyParams(0.4f, 0.3f, 0.6f);
    }

    /// <summary>
    /// 恩赦の純ロジック（内戦・反乱後の集団和解）。恩赦の範囲（scope 0..1＝末端のみ〜首謀者まで）が
    /// 広いほど敗者側の統合は進むが、被害者側の正義の不満が積もる＝**処罰（正義）と統合（実利）の
    /// トレードオフ**。さらに反乱の根本原因が未解決のまま許せば、許された者は同じ理由でまた立つ（再発）。
    /// 個別の捕虜処遇（<see cref="CaptivityRules"/>）・戦犯裁判（バックログ TribunalRules＝裁く側）とは
    /// 別系統＝赦す側の力学。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AmnestyRules
    {
        /// <summary>
        /// 和解の利得（0..reconciliationScale）＝恩赦範囲×敗者人口比 defeatedShare(0..1)。
        /// 広く許すほど・敗者が多いほど統合が進む（全員を敵に回さない）。
        /// </summary>
        public static float ReconciliationGain(float scope, float defeatedShare, AmnestyParams p)
        {
            return Mathf.Clamp01(scope) * Mathf.Clamp01(defeatedShare) * p.reconciliationScale;
        }

        public static float ReconciliationGain(float scope, float defeatedShare)
            => ReconciliationGain(scope, defeatedShare, AmnestyParams.Default);

        /// <summary>
        /// 正義の不満（0..grievanceScale）＝恩赦範囲×被害の深さ victimSuffering(0..1)。
        /// 深く傷ついた社会ほど「裁かれない加害者」を許容できない。
        /// </summary>
        public static float JusticeGrievance(float scope, float victimSuffering, AmnestyParams p)
        {
            return Mathf.Clamp01(scope) * Mathf.Clamp01(victimSuffering) * p.grievanceScale;
        }

        public static float JusticeGrievance(float scope, float victimSuffering)
            => JusticeGrievance(scope, victimSuffering, AmnestyParams.Default);

        /// <summary>
        /// 再発リスク（0..maxRecidivism）＝恩赦範囲×原因の未解決度（1−rootCauseResolved）。
        /// 原因を断ってから許せばリスクなし＝「許す前に直せ」。
        /// </summary>
        public static float RecidivismRisk(float scope, float rootCauseResolved, AmnestyParams p)
        {
            return Mathf.Clamp01(scope) * (1f - Mathf.Clamp01(rootCauseResolved)) * p.maxRecidivism;
        }

        public static float RecidivismRisk(float scope, float rootCauseResolved)
            => RecidivismRisk(scope, rootCauseResolved, AmnestyParams.Default);

        /// <summary>
        /// 安定への純効果＝和解利得−正義の不満−再発リスク。正なら恩赦は引き合う＝
        /// 「どこまで許すか」の損益分岐を scope を振って探せる。
        /// </summary>
        public static float NetStabilityEffect(float scope, float defeatedShare, float victimSuffering, float rootCauseResolved, AmnestyParams p)
        {
            return ReconciliationGain(scope, defeatedShare, p)
                 - JusticeGrievance(scope, victimSuffering, p)
                 - RecidivismRisk(scope, rootCauseResolved, p);
        }

        public static float NetStabilityEffect(float scope, float defeatedShare, float victimSuffering, float rootCauseResolved)
            => NetStabilityEffect(scope, defeatedShare, victimSuffering, rootCauseResolved, AmnestyParams.Default);
    }
}
