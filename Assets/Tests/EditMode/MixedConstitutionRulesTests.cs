using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 混合政体の安定指数（POLY-2 #1445・ポリュビオス）の純ロジックテスト。
    /// 三成分の混合バランス・腐落抵抗・相互牽制・政体循環の停止・支配的成分・混合の崩れ・
    /// 混合度・バランスの取れた混合政体判定を既定 Params 具体値で固定する。
    /// </summary>
    public class MixedConstitutionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>三成分が均等なら混合バランスは最大1・一成分に集中すると0・偏ると中間（ローマ型は均等）。</summary>
        [Test]
        public void MixedBalance_均等で最大_集中で0()
        {
            Assert.AreEqual(1f, MixedConstitutionRules.MixedBalance(1f, 1f, 1f), Eps);
            Assert.AreEqual(0f, MixedConstitutionRules.MixedBalance(1f, 0f, 0f), Eps);
            // (2,1,1)＝シェア(.5,.25,.25) 最大偏差.1667/(2/3)=.25 → 1−.25=0.75
            Assert.AreEqual(0.75f, MixedConstitutionRules.MixedBalance(2f, 1f, 1f), Eps);
            // 全成分0は測れず0
            Assert.AreEqual(0f, MixedConstitutionRules.MixedBalance(0f, 0f, 0f), Eps);
        }

        /// <summary>シャノン的混合度＝均等で最大1・単一成分で0（多項式エントロピー近似）。</summary>
        [Test]
        public void ShannonMixedness_均等で最大_単一で0()
        {
            Assert.AreEqual(1f, MixedConstitutionRules.ShannonMixedness(1f, 1f, 1f), Eps);
            Assert.AreEqual(0f, MixedConstitutionRules.ShannonMixedness(1f, 0f, 0f), Eps);
            // 偏った混合(2,1,1)は中間値（均等未満・単一超）
            float mid = MixedConstitutionRules.ShannonMixedness(2f, 1f, 1f);
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(1f));
        }

        /// <summary>腐落抵抗＝混合バランスに比例し上限0.9（完璧な混合でも腐落を完全には止められない）。</summary>
        [Test]
        public void CorruptionResistance_バランス比例で上限0_9()
        {
            // 既定 maxCorruptionResistance=0.9
            Assert.AreEqual(0.9f, MixedConstitutionRules.CorruptionResistance(1f), Eps);
            Assert.AreEqual(0.45f, MixedConstitutionRules.CorruptionResistance(0.5f), Eps);
            Assert.AreEqual(0f, MixedConstitutionRules.CorruptionResistance(0f), Eps);
        }

        /// <summary>相互牽制＝均等なら他成分が突出を抑えて最大・一成分に完全集中すると抑える側が無く0。</summary>
        [Test]
        public void MutualCheck_均等で牽制_集中で0()
        {
            // 均等＝突出シェア1/3・他2/3・バランス1 → 0.6667
            Assert.AreEqual(2f / 3f, MixedConstitutionRules.MutualCheck(1f, 1f, 1f), Eps);
            Assert.AreEqual(0f, MixedConstitutionRules.MutualCheck(1f, 0f, 0f), Eps);
            Assert.AreEqual(0f, MixedConstitutionRules.MutualCheck(0f, 0f, 0f), Eps);
        }

        /// <summary>政体循環の停止＝腐落抵抗×(1−循環圧力)。圧力が満ちれば止められず0。</summary>
        [Test]
        public void CycleArrest_圧力で循環を止められなくなる()
        {
            Assert.AreEqual(0.9f, MixedConstitutionRules.CycleArrest(0.9f, 0f), Eps);
            Assert.AreEqual(0.45f, MixedConstitutionRules.CycleArrest(0.9f, 0.5f), Eps);
            Assert.AreEqual(0f, MixedConstitutionRules.CycleArrest(0.9f, 1f), Eps);
        }

        /// <summary>支配的成分＝最大の成分を返す（偏りの方向・同値は王政＞貴族政＞民主政の先勝ち）。</summary>
        [Test]
        public void DominantComponent_最大成分を返す()
        {
            Assert.AreEqual(ConstitutionComponent.王政, MixedConstitutionRules.DominantComponent(3f, 1f, 1f));
            Assert.AreEqual(ConstitutionComponent.貴族政, MixedConstitutionRules.DominantComponent(1f, 3f, 1f));
            Assert.AreEqual(ConstitutionComponent.民主政, MixedConstitutionRules.DominantComponent(1f, 1f, 3f));
            // 同値は王政が先勝ち
            Assert.AreEqual(ConstitutionComponent.王政, MixedConstitutionRules.DominantComponent(1f, 1f, 1f));
        }

        /// <summary>混合の崩れ＝均等(balance=1)なら崩れず0・偏り×支配×dt で堕落リスクが増す。</summary>
        [Test]
        public void Degeneration_偏りと支配で混合が崩れる()
        {
            // 均等なら退化なし
            Assert.AreEqual(0f, MixedConstitutionRules.Degeneration(1f, 1f, 1f), Eps);
            // 完全な偏り(0)×支配1×係数0.5×dt1 = 0.5
            Assert.AreEqual(0.5f, MixedConstitutionRules.Degeneration(0f, 1f, 1f), Eps);
            // balance0.5×支配1×0.5×dt2 = 0.5
            Assert.AreEqual(0.5f, MixedConstitutionRules.Degeneration(0.5f, 1f, 2f), Eps);
            // dt<=0 は進まない
            Assert.AreEqual(0f, MixedConstitutionRules.Degeneration(0f, 1f, 0f), Eps);
        }

        /// <summary>バランスの取れた混合政体判定＝既定しきい値0.6以上でローマ型（腐落に強い）。</summary>
        [Test]
        public void IsBalancedMixedConstitution_しきい値0_6で判定()
        {
            Assert.IsTrue(MixedConstitutionRules.IsBalancedMixedConstitution(0.75f));
            Assert.IsTrue(MixedConstitutionRules.IsBalancedMixedConstitution(0.6f));
            Assert.IsFalse(MixedConstitutionRules.IsBalancedMixedConstitution(0.5f));
        }
    }
}
