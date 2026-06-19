using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// RefugeeCrisisRules（難民危機＝戦災・占領による住民流出と受入負荷）の純ロジック検証。
    /// 既定 Params の期待値は手計算で固定（Pow/Sqrt 箇所のみ 1e-3f）。
    /// </summary>
    public class RefugeeCrisisRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        // base=0.8*1.0*0.5=0.4; amp=0.4*(1+0.5*0.6)=0.52
        [Test]
        public void DisplacementVolume_WarAndHarshOccupation()
        {
            float v = RefugeeCrisisRules.DisplacementVolume(0.8f, 0.5f, 1.0f);
            Assert.AreEqual(0.52f, v, Eps);
        }

        // 戦災0なら発生0（戦災×人口の積）。
        [Test]
        public void DisplacementVolume_NoWar_NoDisplacement()
        {
            float v = RefugeeCrisisRules.DisplacementVolume(0f, 0.9f, 1.0f);
            Assert.AreEqual(0f, v, Eps);
        }

        // ratio=0.52/0.4=1.3→clamp01=1; pow(1,2)=1
        [Test]
        public void InfluxPressure_OverCapacity_Saturates()
        {
            float v = RefugeeCrisisRules.InfluxPressure(0.52f, 0.4f);
            Assert.AreEqual(1f, v, PowEps);
        }

        // ratio=0.3/0.6=0.5; pow(0.5,2)=0.25
        [Test]
        public void InfluxPressure_UnderCapacity()
        {
            float v = RefugeeCrisisRules.InfluxPressure(0.3f, 0.6f);
            Assert.AreEqual(0.25f, v, PowEps);
        }

        // relief=1-0.4*0.5=0.8; 0.25*0.8=0.2
        [Test]
        public void AbsorptionStrain_StableHostAbsorbs()
        {
            float v = RefugeeCrisisRules.AbsorptionStrain(0.25f, 0.4f);
            Assert.AreEqual(0.2f, v, Eps);
        }

        // orgFactor=0.5+0.6*0.5=0.8; 0.8*0.8=0.64
        [Test]
        public void HumanitarianResponse_AidTimesOrganization()
        {
            float v = RefugeeCrisisRules.HumanitarianResponse(0.8f, 0.6f);
            Assert.AreEqual(0.64f, v, Eps);
        }

        // 物資0なら対応0
        [Test]
        public void HumanitarianResponse_NoAid_NoResponse()
        {
            float v = RefugeeCrisisRules.HumanitarianResponse(0f, 1.0f);
            Assert.AreEqual(0f, v, Eps);
        }

        // 0.6/(1+0.64*1)=0.6/1.64=0.365853...
        [Test]
        public void CampDestitution_ResponseRelievesPressure()
        {
            float v = RefugeeCrisisRules.CampDestitution(0.6f, 0.64f);
            Assert.AreEqual(0.6f / 1.64f, v, Eps);
        }

        // combined=0.5*0.4=0.2; pow(0.2,2)=0.04
        [Test]
        public void SecurityRisk_DestitutionTimesStrain()
        {
            float v = RefugeeCrisisRules.SecurityRisk(0.5f, 0.4f);
            Assert.AreEqual(0.04f, v, PowEps);
        }

        // 困窮0ならリスク0（積）
        [Test]
        public void SecurityRisk_NoDestitution_NoRisk()
        {
            float v = RefugeeCrisisRules.SecurityRisk(0f, 1.0f);
            Assert.AreEqual(0f, v, PowEps);
        }

        // wSum=1; readiness=(0.8*0.6+0.5*0.4)=0.68; damped=0.68*(1-0.4*0.5)=0.544
        [Test]
        public void ReturnViability_SafeHomelandAndReconstruction()
        {
            float v = RefugeeCrisisRules.ReturnViability(0.8f, 0.5f, 0.4f);
            Assert.AreEqual(0.544f, v, Eps);
        }

        // 対応0.7-リスク0.2=0.5≧0.1→管理可能
        [Test]
        public void IsCrisisManageable_ResponseBeatsRisk()
        {
            Assert.IsTrue(RefugeeCrisisRules.IsCrisisManageable(0.7f, 0.2f));
        }

        // 対応0.2-リスク0.8=-0.6<0.1→管理不能
        [Test]
        public void IsCrisisManageable_RiskOverwhelms()
        {
            Assert.IsFalse(RefugeeCrisisRules.IsCrisisManageable(0.2f, 0.8f));
        }

        // 入力 clamp（範囲外でも例外なく内側へ丸まる）
        [Test]
        public void Inputs_ClampedSafely()
        {
            float v = RefugeeCrisisRules.DisplacementVolume(5f, -3f, 9f);
            Assert.AreEqual(0.5f, v, Eps); // damage=1,harsh=0,pop=1: 1*1*0.5*(1+0)=0.5
            Assert.GreaterOrEqual(v, 0f);
            Assert.LessOrEqual(v, 1f);
        }

        // 物語テスト：苛烈な占領で大量の難民が発生→受入能力を超え逼迫→支援不足でキャンプ困窮→
        // 治安リスクが高まり危機は管理不能。やがて故地が安定し復興すれば帰還の見込みが立つ。
        [Test]
        public void Narrative_OccupiedWorldRefugeesThenReturn()
        {
            // 1) 戦災＋苛烈な占領で住民が大量に流出
            float displaced = RefugeeCrisisRules.DisplacementVolume(0.9f, 0.8f, 1.0f);
            Assert.Greater(displaced, 0.5f);

            // 2) 受入能力の乏しい辺境へ流れ込み逼迫
            float influx = RefugeeCrisisRules.InfluxPressure(displaced, 0.3f);
            Assert.Greater(influx, 0.7f);

            // 3) 受入先も不安定で社会負荷が重い
            float strain = RefugeeCrisisRules.AbsorptionStrain(influx, 0.2f);
            Assert.Greater(strain, 0.5f);

            // 4) 支援も運営も乏しく人道対応は低水準
            float response = RefugeeCrisisRules.HumanitarianResponse(0.2f, 0.3f);
            Assert.Less(response, 0.3f);

            // 5) キャンプは困窮する
            float destitution = RefugeeCrisisRules.CampDestitution(influx, response);
            Assert.Greater(destitution, 0.5f);

            // 6) 困窮と負荷で治安リスクが高まり、危機は管理不能
            float risk = RefugeeCrisisRules.SecurityRisk(destitution, strain);
            Assert.Greater(risk, 0.1f);
            Assert.IsFalse(RefugeeCrisisRules.IsCrisisManageable(response, risk));

            // 7) のちに故地が安定・復興すれば帰還の見込みが立つ
            float ret = RefugeeCrisisRules.ReturnViability(0.9f, 0.8f, displaced);
            Assert.Greater(ret, 0.3f);
        }
    }
}
