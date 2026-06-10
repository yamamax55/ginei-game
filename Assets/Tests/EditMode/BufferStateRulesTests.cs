using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 緩衝国（フェザーン型）の生存術を固定する：均衡度は弱÷強、生存余地は均衡×不可欠性、
    /// 肩入れは等距離からのズレ、併呑リスクは「均衡崩壊・用済み・偏りすぎ」の三死因で跳ね、
    /// 仲介影響力は均衡×依存。既定 Params（基礎0.05/崩壊0.6/偏り0.35）で期待値固定。
    /// </summary>
    public class BufferStateRulesTests
    {
        private static readonly BufferStateParams P = BufferStateParams.Default;
        // 基礎リスク5%・均衡崩壊の重み0.6・偏りの重み0.35

        [Test]
        public void PowerBalance_WeakOverStrong()
        {
            Assert.AreEqual(1f, BufferStateRules.PowerBalance(100f, 100f), 1e-5f);  // 対等＝1
            Assert.AreEqual(0.5f, BufferStateRules.PowerBalance(50f, 100f), 1e-5f); // 半分＝0.5
            Assert.AreEqual(0f, BufferStateRules.PowerBalance(0f, 100f), 1e-5f);    // 一強＝0
            Assert.AreEqual(1f, BufferStateRules.PowerBalance(0f, 0f), 1e-5f);      // 大国不在＝均衡扱い
            Assert.AreEqual(0.5f, BufferStateRules.PowerBalance(100f, 50f), 1e-5f); // 無向＝対称
        }

        [Test]
        public void SurvivalSpace_BalanceTimesIndispensability()
        {
            Assert.AreEqual(1f, BufferStateRules.SurvivalSpace(1f, 1f), 1e-5f);      // 均衡×不可欠＝最大の余地
            Assert.AreEqual(0.4f, BufferStateRules.SurvivalSpace(0.5f, 0.8f), 1e-5f);
            Assert.AreEqual(0f, BufferStateRules.SurvivalSpace(0f, 1f), 1e-5f);      // 均衡崩壊＝余地なし
            Assert.AreEqual(0f, BufferStateRules.SurvivalSpace(1f, 0f), 1e-5f);      // 用済み＝余地なし
            Assert.AreEqual(1f, BufferStateRules.SurvivalSpace(2f, 5f), 1e-5f);      // 入力クランプ
        }

        [Test]
        public void TiltPenalty_DistanceFromNeutrality()
        {
            Assert.AreEqual(0f, BufferStateRules.TiltPenalty(0f), 1e-5f);    // 等距離＝危険なし
            Assert.AreEqual(0.5f, BufferStateRules.TiltPenalty(0.5f), 1e-5f);
            Assert.AreEqual(1f, BufferStateRules.TiltPenalty(-1f), 1e-5f);   // A完全従属もBと同罪＝対称
            Assert.AreEqual(1f, BufferStateRules.TiltPenalty(2f), 1e-5f);    // クランプ
        }

        [Test]
        public void AnnexationRisk_PerfectBufferStateHasOnlyBaseRisk()
        {
            // 均衡・不可欠・等距離が揃えば基礎リスク5%のみ＝弱さの外交の完成形
            Assert.AreEqual(0.05f, BufferStateRules.AnnexationRisk(1f, 1f, 0f, P), 1e-4f);
        }

        [Test]
        public void AnnexationRisk_BalanceCollapseSpikes()
        {
            // 死因①均衡崩壊：一強になれば不可欠でも 0.05+0.6=0.65 へ跳ねる
            Assert.AreEqual(0.65f, BufferStateRules.AnnexationRisk(0f, 1f, 0f, P), 1e-4f);
            // 部分的な傾き（balance0.5×不可欠0.8＝余地0.4）でも 0.05+0.6*0.6=0.41
            Assert.AreEqual(0.41f, BufferStateRules.AnnexationRisk(0.5f, 0.8f, 0f, P), 1e-4f);
        }

        [Test]
        public void AnnexationRisk_ObsolescenceAndTilt()
        {
            // 死因②用済み：均衡が保たれても不可欠性0なら 0.05+0.6=0.65
            Assert.AreEqual(0.65f, BufferStateRules.AnnexationRisk(1f, 0f, 0f, P), 1e-4f);
            // 死因③偏りすぎ：完全肩入れは 0.05+0.35=0.40（どちらへ寄っても同じ）
            Assert.AreEqual(0.4f, BufferStateRules.AnnexationRisk(1f, 1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.4f, BufferStateRules.AnnexationRisk(1f, 1f, -1f, P), 1e-4f);
            // 三死因が揃えば 0.05+0.6+0.35=1.0＝確実に併呑
            Assert.AreEqual(1f, BufferStateRules.AnnexationRisk(0f, 0f, 1f, P), 1e-4f);
        }

        [Test]
        public void BrokerLeverage_BalanceTimesDependence()
        {
            Assert.AreEqual(1f, BufferStateRules.BrokerLeverage(1f, 1f), 1e-5f);      // 拮抗×全依存＝最大の発言力
            Assert.AreEqual(0.3f, BufferStateRules.BrokerLeverage(0.5f, 0.6f), 1e-5f);
            Assert.AreEqual(0f, BufferStateRules.BrokerLeverage(0f, 1f), 1e-5f);      // 一強＝仲介の意味喪失
        }
    }
}
