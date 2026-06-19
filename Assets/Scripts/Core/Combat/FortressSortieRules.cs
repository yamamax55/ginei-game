using UnityEngine;

namespace Ginei
{
    /// <summary>要塞出撃（ソーティ）の調整係数。籠城側が打って出る駆け引きの重み。</summary>
    public readonly struct FortressSortieParams
    {
        /// <summary>出撃の好機の利得（0..1基準値に掛ける重み・敵の混乱×即応をどれだけ好機に変換するか）。</summary>
        public readonly float opportunityGain;
        /// <summary>要塞砲の傘が掩護戦闘力を底上げする重み（0..1・砲支援1で何割増しの戦闘力にするか）。</summary>
        public readonly float gunCoverWeight;
        /// <summary>攻囲線の堅固さが打撃を吸収する重み（0..1以上・敵の塹壕がどれだけ打撃を割るか）。</summary>
        public readonly float entrenchmentWeight;
        /// <summary>敵増援が引き上げを早める重み（0..1・増援が迫るほど早く戻る好機に変換）。</summary>
        public readonly float reinforcementWeight;
        /// <summary>退路が安全とみなせる最大距離（要塞からこの距離以内は近い＝退路が通る）。</summary>
        public readonly float maxSafeDistance;
        /// <summary>要塞砲の傘が退路を守る重み（0..1・砲支援が近さ不足をどれだけ補うか）。</summary>
        public readonly float gunSecurityWeight;
        /// <summary>出撃の露出が損害に効く重み（0..1・露出×敵戦力×退路不安をどれだけ損害に変換するか）。</summary>
        public readonly float riskExposureWeight;
        /// <summary>正味価値で攻囲線打撃を評価する重み（0..1）。</summary>
        public readonly float damageWeight;
        /// <summary>正味価値で損害リスクを差し引く重み（0..1）。</summary>
        public readonly float riskWeight;
        /// <summary>出撃可否の既定閾値（好機と正味価値がこれ以上で打って出る）。</summary>
        public readonly float sortieThreshold;

        public FortressSortieParams(float opportunityGain, float gunCoverWeight, float entrenchmentWeight,
            float reinforcementWeight, float maxSafeDistance, float gunSecurityWeight,
            float riskExposureWeight, float damageWeight, float riskWeight, float sortieThreshold)
        {
            this.opportunityGain = Mathf.Clamp01(opportunityGain);
            this.gunCoverWeight = Mathf.Clamp01(gunCoverWeight);
            this.entrenchmentWeight = Mathf.Max(0f, entrenchmentWeight);
            this.reinforcementWeight = Mathf.Clamp01(reinforcementWeight);
            this.maxSafeDistance = Mathf.Max(0.0001f, maxSafeDistance);
            this.gunSecurityWeight = Mathf.Clamp01(gunSecurityWeight);
            this.riskExposureWeight = Mathf.Clamp01(riskExposureWeight);
            this.damageWeight = Mathf.Clamp01(damageWeight);
            this.riskWeight = Mathf.Clamp01(riskWeight);
            this.sortieThreshold = Mathf.Clamp(sortieThreshold, -1f, 1f);
        }

        /// <summary>既定＝好機利得1・砲傘0.5・塹壕重み1・増援0.5・退路安全距離10・砲退路0.5・露出1・打撃重み1・リスク重み1・閾値0.1。</summary>
        public static FortressSortieParams Default =>
            new FortressSortieParams(1f, 0.5f, 1f, 0.5f, 10f, 0.5f, 1f, 1f, 1f, 0.1f);
    }

    /// <summary>
    /// 要塞出撃（ソーティ）の純ロジック（籠城からの打って出る駆け引き・銀英伝の要塞会戦）。
    /// 攻囲を受ける側が要塞砲の傘の下で打って出て、攻囲線へ打撃を与え、深追いせず退路が通るうちに引き上げる。
    /// 出撃の好機（敵の混乱×要塞即応）・掩護戦闘力（出撃部隊×要塞砲）・攻囲線打撃・引き上げの好機・
    /// 退路の安全（要塞への近さ×砲の傘）・損害リスク・正味価値・出撃可否を扱う。
    /// 数値は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論・入力clamp。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FortressSortieRules
    {
        /// <summary>
        /// 出撃の好機（0..1）＝敵の混乱度（0..1）×要塞の即応度（0..1）×好機利得。
        /// 攻囲側が崩れていて、要塞がすぐ動ける時ほど打って出る好機が高い。どちらか低いと好機は萎む。
        /// </summary>
        public static float SortieOpportunity(float attackerDisorganization, float fortressReadiness,
            FortressSortieParams p)
        {
            float dis = Mathf.Clamp01(attackerDisorganization);
            float ready = Mathf.Clamp01(fortressReadiness);
            return Mathf.Clamp01(dis * ready * p.opportunityGain);
        }

        public static float SortieOpportunity(float attackerDisorganization, float fortressReadiness)
            => SortieOpportunity(attackerDisorganization, fortressReadiness, FortressSortieParams.Default);

        /// <summary>
        /// 掩護された戦闘力（0以上）＝出撃部隊（0..1）×(1＋要塞砲支援（0..1）×砲傘重み)。
        /// 要塞砲の射程内で戦う出撃部隊は、砲の傘ぶんだけ実効戦闘力が底上げされる（傘の外より強い）。
        /// </summary>
        public static float CoveredEngagement(float sortieForce, float fortressGunSupport, FortressSortieParams p)
        {
            float force = Mathf.Clamp01(sortieForce);
            float guns = Mathf.Clamp01(fortressGunSupport);
            return Mathf.Max(0f, force * (1f + guns * p.gunCoverWeight));
        }

        public static float CoveredEngagement(float sortieForce, float fortressGunSupport)
            => CoveredEngagement(sortieForce, fortressGunSupport, FortressSortieParams.Default);

        /// <summary>
        /// 攻囲線への打撃（0..1）＝掩護戦闘力/(掩護戦闘力＋敵の堅固さ×塹壕重み)。ランチェスター的な比＝
        /// 掩護戦闘力が攻囲線の塹壕を上回るほど1へ、塹壕が固いほど0へ。両者0なら打撃0。
        /// </summary>
        public static float SiegeLineDamage(float coveredEngagement, float attackerEntrenchment,
            FortressSortieParams p)
        {
            float ce = Mathf.Max(0f, coveredEngagement);
            float entrench = Mathf.Clamp01(attackerEntrenchment) * p.entrenchmentWeight;
            float denom = ce + entrench;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(ce / denom);
        }

        public static float SiegeLineDamage(float coveredEngagement, float attackerEntrenchment)
            => SiegeLineDamage(coveredEngagement, attackerEntrenchment, FortressSortieParams.Default);

        /// <summary>
        /// 引き上げの好機（0..1）＝戦況の進捗（0..1）×(1＋敵増援（0..1）×増援重み)。深追いする前に戻る判断＝
        /// 戦果が出るほど・敵増援が迫るほど引き上げ時。1.0で「今すぐ戻れ」。
        /// </summary>
        public static float WithdrawalTiming(float engagementProgress, float attackerReinforcement,
            FortressSortieParams p)
        {
            float progress = Mathf.Clamp01(engagementProgress);
            float reinforce = Mathf.Clamp01(attackerReinforcement);
            return Mathf.Clamp01(progress * (1f + reinforce * p.reinforcementWeight));
        }

        public static float WithdrawalTiming(float engagementProgress, float attackerReinforcement)
            => WithdrawalTiming(engagementProgress, attackerReinforcement, FortressSortieParams.Default);

        /// <summary>
        /// 退路の安全（0..1）＝要塞への近さ（近いほど高い）を、要塞砲の傘で底上げ。要塞直下なら近さ1、
        /// maxSafeDistance を越えると近さ0。砲支援が高いほど遠くても退路を守れる（傘の下を後退できる）。
        /// </summary>
        public static float RetreatPathSecurity(float distanceToFortress, float fortressGunSupport,
            FortressSortieParams p)
        {
            float dist = Mathf.Max(0f, distanceToFortress);
            float proximity = Mathf.Clamp01(1f - dist / p.maxSafeDistance);
            float guns = Mathf.Clamp01(fortressGunSupport);
            // 砲の傘ぶん近さ不足を補う（傘が無ければ近さそのまま、傘が厚いほど1へ寄せる）
            return Mathf.Clamp01(Mathf.Lerp(proximity, 1f, guns * p.gunSecurityWeight));
        }

        public static float RetreatPathSecurity(float distanceToFortress, float fortressGunSupport)
            => RetreatPathSecurity(distanceToFortress, fortressGunSupport, FortressSortieParams.Default);

        /// <summary>
        /// 出撃の損害リスク（0..1）＝出撃部隊の露出（0..1）×敵戦力（0..1）×(1−退路安全)×露出重み。
        /// 多く出して敵が強く退路が危ういほど損害大。退路が完全に安全（rps=1）ならリスク0（傘の下で安全に戻れる）。
        /// </summary>
        public static float SortieRisk(float sortieForce, float attackerStrength, float retreatPathSecurity,
            FortressSortieParams p)
        {
            float force = Mathf.Clamp01(sortieForce);
            float enemy = Mathf.Clamp01(attackerStrength);
            float exposed = 1f - Mathf.Clamp01(retreatPathSecurity);
            return Mathf.Clamp01(force * enemy * exposed * p.riskExposureWeight);
        }

        public static float SortieRisk(float sortieForce, float attackerStrength, float retreatPathSecurity)
            => SortieRisk(sortieForce, attackerStrength, retreatPathSecurity, FortressSortieParams.Default);

        /// <summary>
        /// 出撃の正味価値（-1..1）＝攻囲線打撃×打撃重み − 損害リスク×リスク重み。打撃が損害を上回れば正＝
        /// 打って出る値打ちあり、損害が勝てば負＝籠城すべし。
        /// </summary>
        public static float NetSortieValue(float siegeLineDamage, float sortieRisk, FortressSortieParams p)
        {
            float dmg = Mathf.Clamp01(siegeLineDamage);
            float risk = Mathf.Clamp01(sortieRisk);
            return Mathf.Clamp(dmg * p.damageWeight - risk * p.riskWeight, -1f, 1f);
        }

        public static float NetSortieValue(float siegeLineDamage, float sortieRisk)
            => NetSortieValue(siegeLineDamage, sortieRisk, FortressSortieParams.Default);

        /// <summary>
        /// 出撃すべきか＝好機が閾値以上 かつ 正味価値が閾値以上。好機（敵の崩れ・要塞即応）が無ければ
        /// たとえ値打ちがあっても打って出ない（門を開けるのは好機が揃った時だけ）。
        /// </summary>
        public static bool ShouldSortie(float sortieOpportunity, float netSortieValue, float threshold)
        {
            float opp = Mathf.Clamp01(sortieOpportunity);
            float net = Mathf.Clamp(netSortieValue, -1f, 1f);
            return opp >= threshold && net >= threshold;
        }

        public static bool ShouldSortie(float sortieOpportunity, float netSortieValue)
            => ShouldSortie(sortieOpportunity, netSortieValue, FortressSortieParams.Default.sortieThreshold);
    }
}
