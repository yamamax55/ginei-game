using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>野戦医療（前線の救護所・治療成績）の純ロジックテスト。既定Paramsの具体値で期待値を固定。</summary>
    public class FieldMedicineRulesTests
    {
        private const float Eps = 1e-4f;
        private const float PowEps = 1e-3f; // Pow/Sqrt を含む箇所

        /// <summary>外科医数が飽和的に技量を上げ、経験が底上げする。</summary>
        [Test]
        public void MedicalStaffSkill_人数と経験で技量が決まる()
        {
            // surgeons=10 → Clamp01(10*0.05)=0.5; exp=0.6 → 1+0.6*0.5=1.3; 0.5*1.3=0.65
            Assert.AreEqual(0.65f, FieldMedicineRules.MedicalStaffSkill(10f, 0.6f), Eps);

            // 大人数＋満経験は1.0で飽和（Clamp01）
            Assert.AreEqual(1f, FieldMedicineRules.MedicalStaffSkill(50f, 1f), Eps);

            // 外科医ゼロは技量ゼロ
            Assert.AreEqual(0f, FieldMedicineRules.MedicalStaffSkill(0f, 1f), Eps);
        }

        /// <summary>医療物資の充足は 物資/(物資+患者) で患者が多いほど薄まる。</summary>
        [Test]
        public void SupplyAdequacy_患者が多いほど物資が薄まる()
        {
            // 物資60, 患者40 → 60/100 = 0.6
            Assert.AreEqual(0.6f, FieldMedicineRules.SupplyAdequacy(60f, 40f), Eps);

            // 物資ゼロは充足ゼロ（0/0 にならず安定）
            Assert.AreEqual(0f, FieldMedicineRules.SupplyAdequacy(0f, 40f), Eps);

            // 患者ゼロ・物資十分なら充足は満杯近く（200/(1+200))
            Assert.Greater(FieldMedicineRules.SupplyAdequacy(200f, 0f), 0.99f);
        }

        /// <summary>応急処置の質は技量×充足＋わずかな下駄。腕も物資も要る。</summary>
        [Test]
        public void FirstAidQuality_技量と充足の積に下駄()
        {
            // skill=0.65, supply=0.6 → core=0.39; +0.1*0.65*(1-0.6)=0.026 → 0.416
            Assert.AreEqual(0.416f, FieldMedicineRules.FirstAidQuality(0.65f, 0.6f), Eps);

            // 充足1.0なら下駄項ゼロ＝core そのまま
            Assert.AreEqual(0.65f, FieldMedicineRules.FirstAidQuality(0.65f, 1f), Eps);
        }

        /// <summary>感染症管理は衛生×抗生物質の加重幾何平均＝どちらか欠けると崩れる。</summary>
        [Test]
        public void InfectionControl_衛生と抗生物質の両方が要る()
        {
            // san=0.8, anti=0.4 → san*anti=0.32; weighted=0.8*0.5+0.4*0.5=0.6; Sqrt(0.32*0.6)=Sqrt(0.192)
            float expected = Mathf.Sqrt(0.192f); // ≈0.43818
            Assert.AreEqual(expected, FieldMedicineRules.InfectionControl(0.8f, 0.4f), PowEps);

            // 抗生物質ゼロなら管理は崩壊（積がゼロ）
            Assert.AreEqual(0f, FieldMedicineRules.InfectionControl(0.9f, 0f), Eps);
        }

        /// <summary>キャパシティ逼迫は患者流入/病床。超過は指数で急悪化、病床ゼロは完全逼迫。</summary>
        [Test]
        public void CapacityStrain_病床超過で急激に逼迫()
        {
            // influx=50, beds=100 → ratio=0.5; Pow(0.5,1.5)
            Assert.AreEqual(Mathf.Pow(0.5f, 1.5f), FieldMedicineRules.CapacityStrain(50f, 100f), PowEps);

            // 病床を超える流入は逼迫1.0で頭打ち
            Assert.AreEqual(1f, FieldMedicineRules.CapacityStrain(200f, 100f), Eps);

            // 病床ゼロで患者あり＝完全逼迫
            Assert.AreEqual(1f, FieldMedicineRules.CapacityStrain(50f, 0f), Eps);
        }

        /// <summary>治療成績は処置×感染管理×(1-逼迫ペナルティ)。床で最低限の救命は残る。</summary>
        [Test]
        public void TreatmentOutcome_処置と感染管理と逼迫の総合()
        {
            // quality=0.416, infection=Sqrt(0.192)≈0.43818, strain=Pow(0.5,1.5)≈0.353553
            // strainFactor = 1 - 0.353553*0.7 = 0.752513
            // raw = 0.416*0.43818*0.752513 ≈ 0.13717
            float infection = Mathf.Sqrt(0.192f);
            float strain = Mathf.Pow(0.5f, 1.5f);
            float expected = 0.416f * infection * (1f - strain * 0.7f);
            Assert.AreEqual(expected, FieldMedicineRules.TreatmentOutcome(0.416f, infection, strain), PowEps);

            // 劣悪でも成績床0.05は割らない
            Assert.AreEqual(0.05f, FieldMedicineRules.TreatmentOutcome(0f, 0f, 1f), Eps);
        }

        /// <summary>医療資源の消耗は患者×治療強度×消耗率（per dt）。</summary>
        [Test]
        public void ResourceDepletion_患者と治療強度で資源が尽きる()
        {
            // 40患者 * 0.5強度 * 0.02率 * 1dt = 0.4
            Assert.AreEqual(0.4f, FieldMedicineRules.ResourceDepletion(40f, 0.5f, 1f), Eps);

            // 患者ゼロ＝消耗ゼロ
            Assert.AreEqual(0f, FieldMedicineRules.ResourceDepletion(0f, 1f, 1f), Eps);
        }

        /// <summary>回復率は治療成績×(1-重症度)。重傷者は救いにくい。</summary>
        [Test]
        public void RecoveryRate_重症度が回復を割り引く()
        {
            // 成績0.8 * (1-0.25) = 0.6
            Assert.AreEqual(0.6f, FieldMedicineRules.RecoveryRate(0.8f, 0.25f), Eps);

            // 致命傷(severity=1)はどんな成績でも回復ゼロ
            Assert.AreEqual(0f, FieldMedicineRules.RecoveryRate(1f, 1f), Eps);
        }

        /// <summary>物語：充実した後送病院は救えるが、物資も病床も尽きた前線救護所は同じ重傷者を救えない。</summary>
        [Test]
        public void 物語_前線崩壊の救護所は同じ傷を救えない()
        {
            // 充実した野戦病院A：外科医多・物資潤沢・衛生良・病床に余裕
            float skillA = FieldMedicineRules.MedicalStaffSkill(20f, 0.8f);
            float supplyA = FieldMedicineRules.SupplyAdequacy(100f, 50f);
            float aidA = FieldMedicineRules.FirstAidQuality(skillA, supplyA);
            float infA = FieldMedicineRules.InfectionControl(0.9f, 0.8f);
            float strainA = FieldMedicineRules.CapacityStrain(40f, 100f);
            float outcomeA = FieldMedicineRules.TreatmentOutcome(aidA, infA, strainA);
            float recoveryA = FieldMedicineRules.RecoveryRate(outcomeA, 0.4f);

            // 崩壊した前線救護所B：人手薄・物資欠乏・不衛生・病床は溢れている
            float skillB = FieldMedicineRules.MedicalStaffSkill(4f, 0.2f);
            float supplyB = FieldMedicineRules.SupplyAdequacy(20f, 120f);
            float aidB = FieldMedicineRules.FirstAidQuality(skillB, supplyB);
            float infB = FieldMedicineRules.InfectionControl(0.3f, 0.2f);
            float strainB = FieldMedicineRules.CapacityStrain(200f, 60f);
            float outcomeB = FieldMedicineRules.TreatmentOutcome(aidB, infB, strainB);
            float recoveryB = FieldMedicineRules.RecoveryRate(outcomeB, 0.4f);

            // 同じ重症度(0.4)でもAの回復率はBを明確に上回る
            Assert.Greater(recoveryA, recoveryB);
            Assert.Greater(recoveryA, 0.2f);
            // 崩壊した救護所は逼迫が満杯＝成績は床近くまで落ち、ほとんど救えない
            Assert.AreEqual(1f, strainB, Eps);
            Assert.Less(recoveryB, 0.1f);

            // 崩壊側の方が資源消耗は激しい（多数の患者を抱える）
            float depleteB = FieldMedicineRules.ResourceDepletion(120f, 1f, 1f);
            float depleteA = FieldMedicineRules.ResourceDepletion(50f, 1f, 1f);
            Assert.Greater(depleteB, depleteA);
        }
    }
}
