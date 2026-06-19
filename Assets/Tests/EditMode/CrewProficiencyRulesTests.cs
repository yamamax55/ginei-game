using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// CrewProficiencyRules（乗員練度＝個艦の操艦・戦闘習熟）の EditMode テスト。
    /// 既定 Params で期待値を手計算固定。Pow/Sqrt 箇所のみ 1e-3f、他は 1e-4f。
    /// </summary>
    public class CrewProficiencyRulesTests
    {
        private const float Eps = 1e-4f;
        private const float PowEps = 1e-3f;

        [Test]
        public void StationProficiency_満杯訓練で1_補充計算は逓減()
        {
            // 訓練500h＝規模ぴったりで ratio=1, Sqrt(1)=1
            Assert.AreEqual(1f, CrewProficiencyRules.StationProficiency(500f, 0f), PowEps);
            // 訓練200+実戦100×1.6=360, ratio=0.72, Sqrt(0.72)=0.848528
            Assert.AreEqual(0.848528f, CrewProficiencyRules.StationProficiency(200f, 100f), PowEps);
            // 訓練125, ratio=0.25, Sqrt=0.5
            Assert.AreEqual(0.5f, CrewProficiencyRules.StationProficiency(125f, 0f), PowEps);
        }

        [Test]
        public void StationProficiency_負入力はクランプされ0()
        {
            Assert.AreEqual(0f, CrewProficiencyRules.StationProficiency(-100f, -50f), PowEps);
        }

        [Test]
        public void GunneryProficiency_訓練頻度で上乗せ()
        {
            // 0.5*(1+0.3*1)=0.65
            Assert.AreEqual(0.65f, CrewProficiencyRules.GunneryProficiency(0.5f, 1f), Eps);
            // 訓練頻度0なら上乗せなし
            Assert.AreEqual(0.5f, CrewProficiencyRules.GunneryProficiency(0.5f, 0f), Eps);
        }

        [Test]
        public void EngineeringProficiency_機器熟知で上乗せ()
        {
            // 0.8*(1+0.3*0.5)=0.92
            Assert.AreEqual(0.92f, CrewProficiencyRules.EngineeringProficiency(0.8f, 0.5f), Eps);
        }

        [Test]
        public void DamageControlProficiency_訓練実戦度で上乗せ()
        {
            // 0.6*(1+0.3*1)=0.78
            Assert.AreEqual(0.78f, CrewProficiencyRules.DamageControlProficiency(0.6f, 1f), Eps);
        }

        [Test]
        public void CrewCohesion_同乗期間と指揮で合成()
        {
            // 同乗24＝規模ぴったり served=Sqrt(1)=1, v=1*0.6+0.5*0.4=0.8
            Assert.AreEqual(0.8f, CrewProficiencyRules.CrewCohesion(24f, 0.5f), PowEps);
            // 同乗0なら指揮ぶんのみ v=0*0.6+1*0.4=0.4
            Assert.AreEqual(0.4f, CrewProficiencyRules.CrewCohesion(0f, 1f), PowEps);
        }

        [Test]
        public void ProficiencyDilution_補充流入で薄まる()
        {
            // 古参0.8・補充0.5: (1-0.5)+0.5*0.8=0.9
            Assert.AreEqual(0.9f, CrewProficiencyRules.ProficiencyDilution(0.8f, 0.5f), Eps);
            // 補充なしなら希釈なし
            Assert.AreEqual(1f, CrewProficiencyRules.ProficiencyDilution(1f, 0f), Eps);
            // 全員新兵補充で古参比率まで落ちる
            Assert.AreEqual(0.8f, CrewProficiencyRules.ProficiencyDilution(0.8f, 1f), Eps);
        }

        [Test]
        public void OverallProficiency_重み付き平均()
        {
            // 0.65*0.35+0.78*0.25+0.92*0.20+0.8*0.20 = 0.2275+0.195+0.184+0.16 = 0.7665
            Assert.AreEqual(0.7665f, CrewProficiencyRules.OverallProficiency(0.65f, 0.92f, 0.78f, 0.8f), Eps);
        }

        [Test]
        public void PerformanceModifier_練度を倍率へ写す()
        {
            // 練度0.5で1.0、0で0.5、1で1.5
            Assert.AreEqual(1.0f, CrewProficiencyRules.PerformanceModifier(0.5f), Eps);
            Assert.AreEqual(0.5f, CrewProficiencyRules.PerformanceModifier(0f), Eps);
            Assert.AreEqual(1.5f, CrewProficiencyRules.PerformanceModifier(1f), Eps);
            // 0.7665 → Lerp(0.5,1.5,0.7665)=1.2665
            Assert.AreEqual(1.2665f, CrewProficiencyRules.PerformanceModifier(0.7665f), Eps);
        }

        [Test]
        public void 物語_歴戦の乗員は数字以上に強く新兵補充で鈍る()
        {
            // 歴戦艦＝十分な訓練＋実戦・頻繁な砲術・機器熟知・実戦的ダメコン・長い同乗・優れた指揮
            float station = CrewProficiencyRules.StationProficiency(400f, 150f);
            float gun = CrewProficiencyRules.GunneryProficiency(station, 0.9f);
            float eng = CrewProficiencyRules.EngineeringProficiency(station, 0.9f);
            float dc = CrewProficiencyRules.DamageControlProficiency(station, 0.9f);
            float coh = CrewProficiencyRules.CrewCohesion(36f, 0.8f);
            float veteranOverall = CrewProficiencyRules.OverallProficiency(gun, eng, dc, coh);
            float veteranMod = CrewProficiencyRules.PerformanceModifier(veteranOverall);

            // 新兵艦＝訓練浅く実戦なし・訓練頻度低・機器に不慣れ・同乗短い
            float rStation = CrewProficiencyRules.StationProficiency(80f, 0f);
            float rGun = CrewProficiencyRules.GunneryProficiency(rStation, 0.2f);
            float rEng = CrewProficiencyRules.EngineeringProficiency(rStation, 0.2f);
            float rDc = CrewProficiencyRules.DamageControlProficiency(rStation, 0.2f);
            float rCoh = CrewProficiencyRules.CrewCohesion(2f, 0.3f);
            float rookieOverall = CrewProficiencyRules.OverallProficiency(rGun, rEng, rDc, rCoh);
            float rookieMod = CrewProficiencyRules.PerformanceModifier(rookieOverall);

            // 歴戦は新兵より総合練度も性能補正も高い
            Assert.Greater(veteranOverall, rookieOverall);
            Assert.Greater(veteranMod, rookieMod);
            // 歴戦は基準以上(>1)、新兵は基準以下(<1)
            Assert.Greater(veteranMod, 1f);
            Assert.Less(rookieMod, 1f);

            // 大損耗を新兵で補充すると練度が希釈され、性能補正が落ちる
            float diluted = veteranOverall * CrewProficiencyRules.ProficiencyDilution(0.4f, 0.6f);
            float dilutedMod = CrewProficiencyRules.PerformanceModifier(diluted);
            Assert.Less(dilutedMod, veteranMod);
        }
    }
}
