using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 浩然之気（孟子 MENC-4 #1570）の純ロジックのテスト。義の積み重ねでしか育たず、一度の不義で
    /// 一気に損なわれ（非対称）、無理に育てると枯れる（助長の弊害）。気は忠誠・カリスマ・不動の意志の係数になる。
    /// 既定 Params の具体値で期待値を固定する。
    /// </summary>
    public class MoralForceRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>義の行いを積むと気がゆっくり養われる（蓄積率0.02/秒）。</summary>
        [Test]
        public void AccumulationTick_義を積めば気はゆっくり養われる()
        {
            var mf = new MoralForce(0.5f, 0.3f);
            // 0.5 + 1.0×0.02×10 = 0.7
            var r = MoralForceRules.AccumulationTick(mf, 1f, 10f);
            Assert.AreEqual(0.7f, r.qi, Eps);
            Assert.AreEqual(0.3f, r.consistency, Eps, "一貫性は据え置き");

            // 義の行いが0なら積まれない
            var none = MoralForceRules.AccumulationTick(mf, 0f, 10f);
            Assert.AreEqual(0.5f, none.qi, Eps);
        }

        /// <summary>一度の不義は気を一気に損なう＝蓄積より速い非対称（injuryScale0.4）。</summary>
        [Test]
        public void InjuryFromInjustice_一度の不義が気を一気に損なう()
        {
            // 0.8 - 1.0×0.4 = 0.4
            Assert.AreEqual(0.4f, MoralForceRules.InjuryFromInjustice(0.8f, 1f), Eps);
            // 0.8 - 0.5×0.4 = 0.6
            Assert.AreEqual(0.6f, MoralForceRules.InjuryFromInjustice(0.8f, 0.5f), Eps);

            // 非対称：1回の重い不義(0.4減)は蓄積20秒ぶん(0.02×20=0.4)を吹き飛ばす
            float accumPerInjury = MoralForceRules.InjuryFromInjustice(0.8f, 1f); // 0.4
            Assert.Less(accumPerInjury, 0.8f);
        }

        /// <summary>無理に育てようとすると却って枯らす（助長の弊害・forcingScale0.15）。</summary>
        [Test]
        public void NoForcingPenalty_助長すれば却って枯れる()
        {
            // 0.6 - 1.0×0.15 = 0.45
            Assert.AreEqual(0.45f, MoralForceRules.NoForcingPenalty(0.6f, 1f), Eps);
            // 助長0なら損なわない
            Assert.AreEqual(0.6f, MoralForceRules.NoForcingPenalty(0.6f, 0f), Eps);
        }

        /// <summary>浩然の気は忠誠を高める係数になる（≥1.0・気1で1.5）。</summary>
        [Test]
        public void LoyaltyCoefficient_気が忠誠を高める()
        {
            Assert.AreEqual(1f, MoralForceRules.LoyaltyCoefficient(0f), Eps, "気0は等倍");
            Assert.AreEqual(1.5f, MoralForceRules.LoyaltyCoefficient(1f), Eps, "気1で1.0+0.5");
            Assert.AreEqual(1.25f, MoralForceRules.LoyaltyCoefficient(0.5f), Eps);
        }

        /// <summary>気と一貫性がカリスマを高める＝積なのでどちらか低いと効きが落ちる（charismaBonusScale0.6）。</summary>
        [Test]
        public void CharismaCoefficient_気と一貫性の積がカリスマを高める()
        {
            // 1.0 + 1.0×1.0×0.6 = 1.6
            Assert.AreEqual(1.6f, MoralForceRules.CharismaCoefficient(1f, 1f), Eps);
            // 一貫性0なら口先だけ＝等倍
            Assert.AreEqual(1f, MoralForceRules.CharismaCoefficient(1f, 0f), Eps);
            // 1.0 + 0.5×0.5×0.6 = 1.15
            Assert.AreEqual(1.15f, MoralForceRules.CharismaCoefficient(0.5f, 0.5f), Eps);
        }

        /// <summary>至大至剛＝気が満ちると士気の床を与える（resolveFloorScale0.4）。</summary>
        [Test]
        public void UnshakableResolve_満ちた気は不動の床になる()
        {
            Assert.AreEqual(0.4f, MoralForceRules.UnshakableResolve(1f), Eps);
            Assert.AreEqual(0.2f, MoralForceRules.UnshakableResolve(0.5f), Eps);
            Assert.AreEqual(0f, MoralForceRules.UnshakableResolve(0f), Eps);
        }

        /// <summary>言行一致で一貫性が上がり矛盾で下がる（対称・consistencyRate0.05/秒）。</summary>
        [Test]
        public void ConsistencyTick_言行一致で上がり矛盾で下がる()
        {
            // 0.5 + 0.05×4 = 0.7
            Assert.AreEqual(0.7f, MoralForceRules.ConsistencyTick(0.5f, true, 4f), Eps);
            // 0.5 - 0.05×4 = 0.3
            Assert.AreEqual(0.3f, MoralForceRules.ConsistencyTick(0.5f, false, 4f), Eps);
        }

        /// <summary>浩然の気が満ちた判定＝気が既定閾値0.7以上。</summary>
        [Test]
        public void IsFloodlikeQi_閾値以上で浩然の気が満ちる()
        {
            Assert.IsTrue(MoralForceRules.IsFloodlikeQi(0.7f));
            Assert.IsTrue(MoralForceRules.IsFloodlikeQi(0.9f));
            Assert.IsFalse(MoralForceRules.IsFloodlikeQi(0.6999f));
        }
    }
}
