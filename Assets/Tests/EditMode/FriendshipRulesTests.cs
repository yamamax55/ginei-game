using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 盟友システム（キルヒアイス/双璧型）を固定する：紐帯は共有体験（修羅場）で速く深まり、共同作戦ボーナスと
    /// 喪失悲嘆はともに紐帯の二乗でスケール（深い紐帯は力であり急所）。悲嘆はゆっくり回復するが床を残せ、
    /// 反目リスクは野心の差×政治圧力の積を深い友情が割り引く（が完全には防げない）。既定Paramsの具体値で担保。
    /// </summary>
    public class FriendshipRulesTests
    {
        private static readonly FriendshipParams P = FriendshipParams.Default;

        [Test]
        public void BondTick_GrowsSlowlyInPeace_FastInTrials()
        {
            // 平時(sharedTrials=0)：0.02/dt
            Assert.AreEqual(0.52f, FriendshipRules.BondTick(0.5f, 0f, 1f, P), 1e-5f);
            // 修羅場(sharedTrials=1)：0.02×(1+4)=0.1/dt
            Assert.AreEqual(0.6f, FriendshipRules.BondTick(0.5f, 1f, 1f, P), 1e-5f);
            // 1 を超えない（MoveTowards）
            Assert.AreEqual(1f, FriendshipRules.BondTick(0.99f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void JointOperationBonus_QuadraticInBond()
        {
            // 二乗スケール：浅い知己はほぼ無益、深い盟友で真価
            Assert.AreEqual(0.3f, FriendshipRules.JointOperationBonus(1f), 1e-5f);    // 1²×0.3
            Assert.AreEqual(0.075f, FriendshipRules.JointOperationBonus(0.5f), 1e-5f); // 0.25×0.3
            Assert.AreEqual(0f, FriendshipRules.JointOperationBonus(0f), 1e-5f);
            // 入力クランプ
            Assert.AreEqual(0.3f, FriendshipRules.JointOperationBonus(2f), 1e-5f);
        }

        [Test]
        public void LossGrief_DeepBondHurtsDisproportionately()
        {
            // キルヒアイス喪失型：紐帯2倍で悲嘆4倍（二乗）＝深い紐帯ほど急所
            float shallow = FriendshipRules.LossGrief(0.5f); // 0.25×0.6=0.15
            float deep = FriendshipRules.LossGrief(1f);      // 1×0.6=0.6
            Assert.AreEqual(0.15f, shallow, 1e-5f);
            Assert.AreEqual(0.6f, deep, 1e-5f);
            Assert.AreEqual(4f, deep / shallow, 1e-4f);
        }

        [Test]
        public void GriefRecoveryTick_SlowAndStopsAtFloor()
        {
            // 回復0.01/dt
            Assert.AreEqual(0.59f, FriendshipRules.GriefRecoveryTick(0.6f, 1f), 1e-5f);
            // GriefFloor(1)=0.6×0.25=0.15＝決して癒えない残滓
            float floor = FriendshipRules.GriefFloor(1f);
            Assert.AreEqual(0.15f, floor, 1e-5f);
            // 床を下回らない
            Assert.AreEqual(0.15f, FriendshipRules.GriefRecoveryTick(0.155f, 1f, P, floor), 1e-5f);
            Assert.AreEqual(0.15f, FriendshipRules.GriefRecoveryTick(0.15f, 10f, P, floor), 1e-5f);
        }

        [Test]
        public void EstrangementRisk_NeedsBothGapAndPressure()
        {
            // 裂け目は積＝どちらか一方だけでは裂けない
            Assert.AreEqual(0f, FriendshipRules.EstrangementRisk(0f, 1f, 0f), 1e-5f);
            Assert.AreEqual(0f, FriendshipRules.EstrangementRisk(0f, 0f, 1f), 1e-5f);
            // 紐帯なし＋両方極大＝リスク最大
            Assert.AreEqual(1f, FriendshipRules.EstrangementRisk(0f, 1f, 1f), 1e-5f);
        }

        [Test]
        public void EstrangementRisk_DeepBondResistsButNotImmune()
        {
            // 双璧の悲劇型：最深の友情(bond=1)でも耐性0.6＝残り0.4は裂けうる
            Assert.AreEqual(0.4f, FriendshipRules.EstrangementRisk(1f, 1f, 1f), 1e-5f);
            // 中程度の紐帯は中程度に抗う：1×(1-0.5×0.6)=0.7
            Assert.AreEqual(0.7f, FriendshipRules.EstrangementRisk(0.5f, 1f, 1f), 1e-5f);
            // 深いほどリスクは単調減少
            Assert.Less(FriendshipRules.EstrangementRisk(1f, 0.8f, 0.8f),
                        FriendshipRules.EstrangementRisk(0.3f, 0.8f, 0.8f));
        }

        [Test]
        public void Params_CtorClampsToValidRange()
        {
            var p = new FriendshipParams(-1f, -2f, -0.5f, -0.1f, -0.01f, 2f, 1.5f);
            Assert.AreEqual(0f, p.bondGrowthBase, 1e-5f);
            Assert.AreEqual(0f, p.trialMultiplier, 1e-5f);
            Assert.AreEqual(0f, p.jointBonusScale, 1e-5f);
            Assert.AreEqual(1f, p.griefResidualRatio, 1e-5f);
            Assert.AreEqual(1f, p.estrangementResist, 1e-5f);
        }
    }
}
