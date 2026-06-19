using UnityEngine;

namespace Ginei
{
    /// <summary>野戦医療（前線の救護所・野戦病院）の治療成績の調整係数。</summary>
    public readonly struct FieldMedicineParams
    {
        /// <summary>外科医1名あたりが医療スタッフ技量に寄与する量（技量は飽和して0..1）。</summary>
        public readonly float perSurgeonSkill;
        /// <summary>経験（experience 1.0）が技量を底上げする最大割合。</summary>
        public readonly float experienceWeight;
        /// <summary>医療物資の充足計算で 0/0 を避ける加算床（分母の最小値・物資ゼロでも安定）。</summary>
        public readonly float supplyFloor;
        /// <summary>応急処置の質の下駄（技量・充足がゼロでも最低限は手当てできる床への寄与）。</summary>
        public readonly float firstAidBias;
        /// <summary>感染症管理における抗生物質の重み（衛生と抗生物質の配分）。</summary>
        public readonly float antibioticWeight;
        /// <summary>キャパシティ逼迫の効き方（&gt;1で病床超過時に急激に逼迫が悪化＝崩壊）。</summary>
        public readonly float strainExponent;
        /// <summary>治療成績の床（どんなに劣悪でもこれ以上は下がらない＝最低限の救命）。</summary>
        public readonly float outcomeFloor;
        /// <summary>逼迫が治療成績を削る最大割合（逼迫1.0で成績にこの割合のペナルティ）。</summary>
        public readonly float strainPenaltyScale;
        /// <summary>患者1単位・治療強度1.0が1tickあたり消耗する医療資源量（per dt）。</summary>
        public readonly float depletionRate;

        public FieldMedicineParams(float perSurgeonSkill, float experienceWeight, float supplyFloor,
                                   float firstAidBias, float antibioticWeight, float strainExponent,
                                   float outcomeFloor, float strainPenaltyScale, float depletionRate)
        {
            this.perSurgeonSkill = Mathf.Max(0f, perSurgeonSkill);
            this.experienceWeight = Mathf.Clamp01(experienceWeight);
            this.supplyFloor = Mathf.Max(1e-4f, supplyFloor);
            this.firstAidBias = Mathf.Clamp01(firstAidBias);
            this.antibioticWeight = Mathf.Clamp01(antibioticWeight);
            this.strainExponent = Mathf.Max(0.01f, strainExponent);
            this.outcomeFloor = Mathf.Clamp01(outcomeFloor);
            this.strainPenaltyScale = Mathf.Clamp01(strainPenaltyScale);
            this.depletionRate = Mathf.Max(0f, depletionRate);
        }

        /// <summary>
        /// 既定＝外科医寄与0.05/名・経験底上げ0.5・充足床1.0・応急下駄0.1・抗生物質重み0.5・
        /// 逼迫指数1.5・成績床0.05・逼迫ペナルティ0.7・資源消耗0.02/tick。
        /// 物資が患者数に追いつかず、病床を超えた患者流入が成績を急落させる＝前線医療の逼迫を表す。
        /// </summary>
        public static FieldMedicineParams Default =>
            new FieldMedicineParams(0.05f, 0.5f, 1.0f, 0.1f, 0.5f, 1.5f, 0.05f, 0.7f, 0.02f);
    }

    /// <summary>
    /// 野戦医療（前線の野戦病院・救護所）の純ロジック。医療スタッフの技量・医薬品/設備の充足・
    /// 応急処置の質・感染症の管理・医療キャパシティの逼迫から治療成績と回復率を導き、医療資源の消耗を見積もる。
    /// 戦傷者をどれだけ救えるかは、外科医の腕（<paramref/>）だけでなく物資・病床・衛生の総合で決まる＝
    /// どこか一つでも欠ければ救命率は落ちる。倍率・割合は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// <see cref="CombatFatigueRules"/>（兵の持続疲弊）とは別＝負傷者そのものの治療成績に特化。
    /// </summary>
    public static class FieldMedicineRules
    {
        /// <summary>
        /// 医療スタッフの技量（0..1）。外科医数が perSurgeonSkill で飽和的に技量を上げ、
        /// 経験（experience）が experienceWeight まで底上げする＝人数と熟練の積。
        /// </summary>
        public static float MedicalStaffSkill(float surgeons, float experience, FieldMedicineParams p)
        {
            float headcount = Mathf.Clamp01(Mathf.Max(0f, surgeons) * p.perSurgeonSkill);
            float expBoost = 1f + Mathf.Clamp01(experience) * p.experienceWeight;
            return Mathf.Clamp01(headcount * expBoost);
        }

        public static float MedicalStaffSkill(float surgeons, float experience)
            => MedicalStaffSkill(surgeons, experience, FieldMedicineParams.Default);

        /// <summary>
        /// 医療物資の充足（0..1）＝医薬品/(医薬品+患者数)。患者が多いほど一人あたりの物資が薄まる。
        /// 分母に supplyFloor を加えて 0/0 を避ける（物資・患者ともゼロでも安定）。
        /// </summary>
        public static float SupplyAdequacy(float medicalSupplies, float patientLoad, FieldMedicineParams p)
        {
            float supplies = Mathf.Max(0f, medicalSupplies);
            float load = Mathf.Max(0f, patientLoad);
            return Mathf.Clamp01(supplies / Mathf.Max(p.supplyFloor, supplies + load));
        }

        public static float SupplyAdequacy(float medicalSupplies, float patientLoad)
            => SupplyAdequacy(medicalSupplies, patientLoad, FieldMedicineParams.Default);

        /// <summary>
        /// 応急処置の質（0..1）＝技量×充足。腕も物資も要る。
        /// firstAidBias ぶんの下駄を技量に掛けて加える＝最低限の手当ては効く（完全ゼロにしない）。
        /// </summary>
        public static float FirstAidQuality(float staffSkill, float supplyAdequacy, FieldMedicineParams p)
        {
            float skill = Mathf.Clamp01(staffSkill);
            float supply = Mathf.Clamp01(supplyAdequacy);
            float core = skill * supply;
            return Mathf.Clamp01(core + p.firstAidBias * skill * (1f - supply));
        }

        public static float FirstAidQuality(float staffSkill, float supplyAdequacy)
            => FirstAidQuality(staffSkill, supplyAdequacy, FieldMedicineParams.Default);

        /// <summary>
        /// 感染症の管理（0..1）＝衛生×抗生物質。antibioticWeight で抗生物質の比重を配分する
        /// 加重幾何平均＝どちらか一方が欠けても感染が広がる（重病床の衛生は命綱）。
        /// </summary>
        public static float InfectionControl(float sanitation, float antibiotics, FieldMedicineParams p)
        {
            float san = Mathf.Clamp01(sanitation);
            float anti = Mathf.Clamp01(antibiotics);
            float w = p.antibioticWeight;
            float weighted = san * (1f - w) + anti * w;
            // 相乗（どちらかゼロで管理が崩れる）と加重平均の幾何平均で、欠落に弱い管理を表す
            return Mathf.Clamp01(Mathf.Sqrt(Mathf.Clamp01(san * anti) * Mathf.Clamp01(weighted)));
        }

        public static float InfectionControl(float sanitation, float antibiotics)
            => InfectionControl(sanitation, antibiotics, FieldMedicineParams.Default);

        /// <summary>
        /// 医療キャパシティの逼迫（0..1）＝患者流入/病床。病床を超えた流入は strainExponent で急激に逼迫する
        /// ＝定員内は緩やかだが超過すると医療崩壊。bedCapacity が無ければ完全逼迫扱い。
        /// </summary>
        public static float CapacityStrain(float patientInflux, float bedCapacity, FieldMedicineParams p)
        {
            float influx = Mathf.Max(0f, patientInflux);
            float beds = Mathf.Max(0f, bedCapacity);
            if (beds <= 0f) return influx > 0f ? 1f : 0f;
            float ratio = Mathf.Clamp01(influx / beds);
            return Mathf.Clamp01(Mathf.Pow(ratio, p.strainExponent));
        }

        public static float CapacityStrain(float patientInflux, float bedCapacity)
            => CapacityStrain(patientInflux, bedCapacity, FieldMedicineParams.Default);

        /// <summary>
        /// 治療成績（0..1）＝処置×感染管理×(1−逼迫ペナルティ)。応急処置の質と感染管理を掛け、
        /// 逼迫が strainPenaltyScale ぶん成績を削る。outcomeFloor の床で最低限の救命は残る。
        /// </summary>
        public static float TreatmentOutcome(float firstAidQuality, float infectionControl, float capacityStrain, FieldMedicineParams p)
        {
            float quality = Mathf.Clamp01(firstAidQuality);
            float infection = Mathf.Clamp01(infectionControl);
            float strainFactor = Mathf.Clamp01(1f - Mathf.Clamp01(capacityStrain) * p.strainPenaltyScale);
            float raw = quality * infection * strainFactor;
            return Mathf.Clamp01(Mathf.Max(p.outcomeFloor, raw));
        }

        public static float TreatmentOutcome(float firstAidQuality, float infectionControl, float capacityStrain)
            => TreatmentOutcome(firstAidQuality, infectionControl, capacityStrain, FieldMedicineParams.Default);

        /// <summary>
        /// 医療資源の消耗（&gt;=0・per dt）＝患者数×治療強度×depletionRate。
        /// 患者が多く濃密に治療するほど医薬品・血液・包帯が速く尽きる＝備蓄から差し引く想定。
        /// </summary>
        public static float ResourceDepletion(float patientLoad, float treatmentIntensity, float dt, FieldMedicineParams p)
        {
            float load = Mathf.Max(0f, patientLoad);
            float intensity = Mathf.Clamp01(treatmentIntensity);
            return load * intensity * p.depletionRate * Mathf.Max(0f, dt);
        }

        public static float ResourceDepletion(float patientLoad, float treatmentIntensity, float dt)
            => ResourceDepletion(patientLoad, treatmentIntensity, dt, FieldMedicineParams.Default);

        /// <summary>
        /// 回復率（0..1）＝治療成績×(1−重症度)。良い処置でも重傷者は救いにくい＝
        /// 軽傷なら治療成績がそのまま回復に、重傷なら成績を割り引く。
        /// </summary>
        public static float RecoveryRate(float treatmentOutcome, float woundSeverity, FieldMedicineParams p)
        {
            float outcome = Mathf.Clamp01(treatmentOutcome);
            float severity = Mathf.Clamp01(woundSeverity);
            return Mathf.Clamp01(outcome * (1f - severity));
        }

        public static float RecoveryRate(float treatmentOutcome, float woundSeverity)
            => RecoveryRate(treatmentOutcome, woundSeverity, FieldMedicineParams.Default);
    }
}
