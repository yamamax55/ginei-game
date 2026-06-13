using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 勢力圏（旗を立てない帝国）を固定する：浸透は三経路（経済0.4/軍事0.35/政治0.25）の
    /// 加重圧力×(1−抵抗力)で蓄積し抵抗力で風化、追従度は浸透の二乗、二大国の取り合いは
    /// 相殺＋代理紛争、浸透しすぎは民族主義反発（閾値0.5超過×誇り）、属国化転換は0.8。
    /// 既定 Params（浸透0.1/s・風化0.05/s）で期待値固定。
    /// </summary>
    public class InfluenceRulesTests
    {
        private static readonly InfluenceParams P = InfluenceParams.Default;
        // 経済0.4・軍事0.35・政治0.25・浸透速度0.1・風化0.05・反発閾値0.5・属国化閾値0.8

        [Test]
        public void PenetrationTick_ThreeChannelsAccumulate()
        {
            // 全経路全開・無抵抗：圧力1.0 → 0 + 0.1*1*1 = 0.1
            Assert.AreEqual(0.1f, InfluenceRules.PenetrationTick(0f, 1f, 1f, 1f, 0f, 1f, P), 1e-4f);
            // 経済のみ：圧力0.4 → 0.04（債務の罠の入口＝経済経路が最重）
            Assert.AreEqual(0.04f, InfluenceRules.PenetrationTick(0f, 1f, 0f, 0f, 0f, 1f, P), 1e-4f);
            // 軍事顧問のみ：圧力0.35 → 0.035
            Assert.AreEqual(0.035f, InfluenceRules.PenetrationTick(0f, 0f, 1f, 0f, 0f, 1f, P), 1e-4f);
        }

        [Test]
        public void PenetrationTick_ResilienceBlocksAndErodes()
        {
            // 抵抗力1：一切浸透せず既存影響が風化 → 0.5 - 0.05*0.5 = 0.475
            Assert.AreEqual(0.475f, InfluenceRules.PenetrationTick(0.5f, 1f, 1f, 1f, 1f, 1f, P), 1e-4f);
            // 抵抗力0.5：蓄積0.1*1*0.5=0.05・風化0.05*0.5*0.5=0.0125 → 0.5375
            Assert.AreEqual(0.5375f, InfluenceRules.PenetrationTick(0.5f, 1f, 1f, 1f, 0.5f, 1f, P), 1e-4f);
            // 働きかけゼロ・抵抗力0：風化もしない（抵抗なき国には影響が居座る）
            Assert.AreEqual(0.5f, InfluenceRules.PenetrationTick(0.5f, 0f, 0f, 0f, 0f, 1f, P), 1e-4f);
            // 上限クランプ＝1.0で飽和
            Assert.AreEqual(1f, InfluenceRules.PenetrationTick(0.99f, 1f, 1f, 1f, 0f, 5f, P), 1e-4f);
        }

        [Test]
        public void PolicyCompliance_DeepensQuadratically()
        {
            Assert.AreEqual(0f, InfluenceRules.PolicyCompliance(0f), 1e-5f);     // 無浸透＝面従腹背すらない
            Assert.AreEqual(0.25f, InfluenceRules.PolicyCompliance(0.5f), 1e-5f); // 半分の浸透でも追従は1/4＝非線形
            Assert.AreEqual(1f, InfluenceRules.PolicyCompliance(1f), 1e-5f);     // 完全浸透＝外交投票も基地も
            Assert.AreEqual(1f, InfluenceRules.PolicyCompliance(1.5f), 1e-5f);   // 入力クランプ
        }

        [Test]
        public void RivalContestation_TugOfWarCancelsAndIgnites()
        {
            // 相殺：純影響は差分のみ
            Assert.AreEqual(0.7f, InfluenceRules.NetInfluence(0.9f, 0.2f), 1e-5f);
            Assert.AreEqual(-0.7f, InfluenceRules.NetInfluence(0.2f, 0.9f), 1e-5f); // 対称
            // 代理紛争：双方0.5ずつ食い込めば確実に火がつく＝2*min
            Assert.AreEqual(1f, InfluenceRules.RivalContestation(0.5f, 0.5f), 1e-5f);
            Assert.AreEqual(0.4f, InfluenceRules.RivalContestation(0.2f, 0.9f), 1e-5f);
            Assert.AreEqual(0f, InfluenceRules.RivalContestation(0f, 1f), 1e-5f); // 一方独占＝綱引き不成立
        }

        [Test]
        public void BacklashRisk_PrideSpikesOverThreshold()
        {
            // 閾値0.5以下の浸透は世論に気づかれない＝リスク0
            Assert.AreEqual(0f, InfluenceRules.BacklashRisk(0.5f, 1f, P), 1e-5f);
            // 閾値超過分の割合×誇り：浸透0.75は超過1/2 → 誇り1.0で0.5
            Assert.AreEqual(0.5f, InfluenceRules.BacklashRisk(0.75f, 1f, P), 1e-4f);
            // 完全浸透×誇り高い国＝確実に急進的離反＝属国扱いへの怒り
            Assert.AreEqual(1f, InfluenceRules.BacklashRisk(1f, 1f, P), 1e-4f);
            // 誇りなき国は属国扱いでも怒らない
            Assert.AreEqual(0f, InfluenceRules.BacklashRisk(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void SoftToHardThreshold_VassalConversionAtDeepPenetration()
        {
            Assert.IsTrue(InfluenceRules.SoftToHardThreshold(0.8f, P));   // 閾値ちょうど＝転換可
            Assert.IsFalse(InfluenceRules.SoftToHardThreshold(0.79f, P)); // 一歩手前＝まだ非公式のまま
            Assert.IsTrue(InfluenceRules.SoftToHardThreshold(1.5f, P));   // 入力クランプ
        }

        [Test]
        public void Params_CtorClampsInputs()
        {
            var p = new InfluenceParams(-1f, 2f, 0.5f, 5f, -0.1f, 1f, 2f);
            Assert.AreEqual(0f, p.economicWeight, 1e-5f);
            Assert.AreEqual(1f, p.militaryWeight, 1e-5f);
            Assert.AreEqual(1f, p.penetrationRate, 1e-5f);
            Assert.AreEqual(0f, p.decayRate, 1e-5f);
            Assert.AreEqual(0.99f, p.backlashThreshold, 1e-5f); // 反発勾配を残すため上限0.99
            Assert.AreEqual(1f, p.vassalThreshold, 1e-5f);
        }
    }
}
