using UnityEngine;

namespace Ginei
{
    /// <summary>負傷からの回復（療養と戦列復帰）の調整係数。</summary>
    public readonly struct WoundedRecoveryParams
    {
        /// <summary>重症度1.0が治療なしで要する基準治癒期間（healing time の係数）。</summary>
        public readonly float severityTimeScale;
        /// <summary>治療の質が治癒期間を縮める効き（質1.0で 1/(1+effect) に短縮）。</summary>
        public readonly float treatmentTimeEffect;
        /// <summary>施設の質が回復環境に効く重み。</summary>
        public readonly float facilityWeight;
        /// <summary>療養環境（安静・栄養）が回復環境に効く重み。</summary>
        public readonly float restWeight;
        /// <summary>環境ゼロでも残る最低限の回復環境（野戦でも多少は治る床）。</summary>
        public readonly float environmentFloor;
        /// <summary>後遺症（治らぬ傷）が復帰適性を削る重み。</summary>
        public readonly float disabilityPenalty;
        /// <summary>戦列復帰できるとみなす既定の適性閾値。</summary>
        public readonly float fitnessThreshold;

        public WoundedRecoveryParams(float severityTimeScale, float treatmentTimeEffect, float facilityWeight,
                                     float restWeight, float environmentFloor, float disabilityPenalty,
                                     float fitnessThreshold)
        {
            this.severityTimeScale = Mathf.Max(0f, severityTimeScale);
            this.treatmentTimeEffect = Mathf.Max(0f, treatmentTimeEffect);
            this.facilityWeight = Mathf.Clamp01(facilityWeight);
            this.restWeight = Mathf.Clamp01(restWeight);
            this.environmentFloor = Mathf.Clamp01(environmentFloor);
            this.disabilityPenalty = Mathf.Clamp01(disabilityPenalty);
            this.fitnessThreshold = Mathf.Clamp01(fitnessThreshold);
        }

        /// <summary>
        /// 既定＝重症度時間係数1.0/治療短縮効き1.0・施設重み0.6/療養重み0.4・環境床0.2・
        /// 後遺症ペナルティ0.5・復帰閾値0.5。施設と療養の重みは合計1.0。
        /// </summary>
        public static WoundedRecoveryParams Default =>
            new WoundedRecoveryParams(1f, 1f, 0.6f, 0.4f, 0.2f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 負傷兵の療養と戦列復帰の純ロジック。傷の重症度と治癒期間、療養環境の質、後遺症、
    /// 戦列復帰の可否、復帰後の能力低下、長期療養者の戦力外、リハビリを扱う。
    /// 重傷ほど治癒に時間がかかり後遺症が残る＝治療の質と療養環境が時間を縮め後遺症を減らす。
    /// 治癒の進捗が後遺症を上回れば戦列へ復帰できるが、復帰後は後遺症ぶん能力が目減りする。
    /// 治らず進捗の乏しい後遺症者は長期療養者＝戦力外になる。
    /// 倍率・係数は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WoundedRecoveryRules
    {
        /// <summary>
        /// 治癒に要する期間（戻り＝期間・0以上）＝重症度×severityTimeScale ÷(1+治療の質×treatmentTimeEffect)。
        /// 重症ほど長く、治療の質が高いほど短い。
        /// </summary>
        public static float HealingTime(float woundSeverity, float treatmentQuality, WoundedRecoveryParams p)
        {
            float sev = Mathf.Clamp01(woundSeverity);
            float quality = Mathf.Clamp01(treatmentQuality);
            float denom = 1f + quality * p.treatmentTimeEffect;
            return (sev * p.severityTimeScale) / denom;
        }

        public static float HealingTime(float woundSeverity, float treatmentQuality)
            => HealingTime(woundSeverity, treatmentQuality, WoundedRecoveryParams.Default);

        /// <summary>
        /// 回復の環境（0..1）＝施設の質×facilityWeight＋療養環境×restWeight、ただし environmentFloor を下回らない。
        /// 良い病院と安静・栄養が揃うほど治りが速い。
        /// </summary>
        public static float RecoveryEnvironment(float facilityQuality, float restConditions, WoundedRecoveryParams p)
        {
            float env = Mathf.Clamp01(facilityQuality) * p.facilityWeight
                      + Mathf.Clamp01(restConditions) * p.restWeight;
            return Mathf.Clamp01(Mathf.Max(p.environmentFloor, env));
        }

        public static float RecoveryEnvironment(float facilityQuality, float restConditions)
            => RecoveryEnvironment(facilityQuality, restConditions, WoundedRecoveryParams.Default);

        /// <summary>
        /// 治癒の進捗（0..1）＝経過時間×回復の環境÷治癒に要する期間。
        /// 環境が良いほど早く進み、必要期間が長いほど進みは遅い。治癒期間が0なら即完治(1)。
        /// </summary>
        public static float HealingProgress(float healingTime, float recoveryEnvironment, float elapsedTime, WoundedRecoveryParams p)
        {
            float time = Mathf.Max(0f, healingTime);
            float env = Mathf.Clamp01(recoveryEnvironment);
            float elapsed = Mathf.Max(0f, elapsedTime);
            if (time <= 0f) return 1f;
            return Mathf.Clamp01((elapsed * env) / time);
        }

        public static float HealingProgress(float healingTime, float recoveryEnvironment, float elapsedTime)
            => HealingProgress(healingTime, recoveryEnvironment, elapsedTime, WoundedRecoveryParams.Default);

        /// <summary>
        /// 後遺症の度合い（0..1）＝重症度×(1−治療の質)。
        /// 重傷を粗末に扱うほど治らぬ傷が残る＝十分な治療は後遺症を減らす。
        /// </summary>
        public static float PermanentDisability(float woundSeverity, float treatmentQuality, WoundedRecoveryParams p)
        {
            float sev = Mathf.Clamp01(woundSeverity);
            float quality = Mathf.Clamp01(treatmentQuality);
            return Mathf.Clamp01(sev * (1f - quality));
        }

        public static float PermanentDisability(float woundSeverity, float treatmentQuality)
            => PermanentDisability(woundSeverity, treatmentQuality, WoundedRecoveryParams.Default);

        /// <summary>
        /// 戦列復帰の適性（0..1）＝治癒の進捗 − 後遺症×disabilityPenalty。
        /// 傷が治っていても後遺症が重ければ適性は下がる。
        /// </summary>
        public static float ReturnToDutyFitness(float healingProgress, float permanentDisability, WoundedRecoveryParams p)
        {
            float progress = Mathf.Clamp01(healingProgress);
            float disability = Mathf.Clamp01(permanentDisability);
            return Mathf.Clamp01(progress - disability * p.disabilityPenalty);
        }

        public static float ReturnToDutyFitness(float healingProgress, float permanentDisability)
            => ReturnToDutyFitness(healingProgress, permanentDisability, WoundedRecoveryParams.Default);

        /// <summary>
        /// 復帰後の能力（0以上・実効値）＝復帰適性×元の能力。
        /// 適性が満たなければ後遺症ぶん能力が目減りする（基準非破壊・掛けて使う）。
        /// </summary>
        public static float PostRecoveryCapability(float returnToDutyFitness, float originalCapability, WoundedRecoveryParams p)
        {
            float fitness = Mathf.Clamp01(returnToDutyFitness);
            float original = Mathf.Max(0f, originalCapability);
            return fitness * original;
        }

        public static float PostRecoveryCapability(float returnToDutyFitness, float originalCapability)
            => PostRecoveryCapability(returnToDutyFitness, originalCapability, WoundedRecoveryParams.Default);

        /// <summary>
        /// 長期療養者（戦力外）の度合い（0..1）＝後遺症×(1−治癒の進捗)。
        /// 治らず進捗の乏しい重い後遺症ほど高い＝後方送り・戦力外の判断材料。
        /// </summary>
        public static float LongTermInvalid(float permanentDisability, float healingProgress, WoundedRecoveryParams p)
        {
            float disability = Mathf.Clamp01(permanentDisability);
            float progress = Mathf.Clamp01(healingProgress);
            return Mathf.Clamp01(disability * (1f - progress));
        }

        public static float LongTermInvalid(float permanentDisability, float healingProgress)
            => LongTermInvalid(permanentDisability, healingProgress, WoundedRecoveryParams.Default);

        /// <summary>
        /// 戦列復帰できるか（復帰適性が threshold 以上）。リハビリが進み適性が閾値を越えれば前線へ戻せる。
        /// </summary>
        public static bool IsFitForDuty(float returnToDutyFitness, float threshold)
        {
            return Mathf.Clamp01(returnToDutyFitness) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="WoundedRecoveryParams.fitnessThreshold"/>）での復帰判定。</summary>
        public static bool IsFitForDuty(float returnToDutyFitness, WoundedRecoveryParams p)
            => IsFitForDuty(returnToDutyFitness, p.fitnessThreshold);

        public static bool IsFitForDuty(float returnToDutyFitness)
            => IsFitForDuty(returnToDutyFitness, WoundedRecoveryParams.Default.fitnessThreshold);
    }
}
