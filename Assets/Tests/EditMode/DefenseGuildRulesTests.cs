using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 守城専門集団（墨家型）を固定する：技術が防御を底上げし要塞と相乗、寡兵でも専門技術で守る攻城耐性、
    /// 大義重視で金だけでは動かない請負判断、規律ゆえ攻撃を常に拒否、守り抜くと名声、籠城消耗、技術伝承を担保。
    /// </summary>
    public class DefenseGuildRulesTests
    {
        private static readonly DefenseGuildParams P = DefenseGuildParams.Default; // 技術+150%/要塞相乗0.5/最低大義0.4/報酬重み0.25/消耗0.1/伝承0.2

        [Test]
        public void DefensiveBonus_TechAndFortressSynergy()
        {
            var g = new DefenseGuild(1f, 1f, 0.5f); // 技術満点
            // 要塞なし＝1+1.5×1
            Assert.AreEqual(2.5f, DefenseGuildRules.DefensiveBonus(g, 0f, P), 1e-5f);
            // 要塞満＝1+1.5×1+0.5×1×1
            Assert.AreEqual(3.0f, DefenseGuildRules.DefensiveBonus(g, 1f, P), 1e-5f);
            // 技術0＝素の1.0（要塞があっても相乗ゼロ）
            Assert.AreEqual(1.0f, DefenseGuildRules.DefensiveBonus(new DefenseGuild(0f), 1f, P), 1e-5f);
        }

        [Test]
        public void SiegeResistance_FewButExpert()
        {
            // 寡兵（size=0.1）でも高技術・高規律なら守り抜く＝攻撃と互角でも耐性は高め
            var expert = new DefenseGuild(1f, 1f, 0.1f);
            // skill=0.7×1+0.3×0.1=0.73、defense=0.73×Lerp(0.5,1,1)=0.73、attack=0.73 → 0.5
            Assert.AreEqual(0.5f, DefenseGuildRules.SiegeResistance(expert, 0.73f, P), 1e-4f);
            // 攻撃が弱ければ耐性は1へ近づく
            Assert.Greater(DefenseGuildRules.SiegeResistance(expert, 0.1f, P), 0.8f);
            // 同規模でも素人（低技術・低規律）は専門家に劣る
            var amateur = new DefenseGuild(0.2f, 0.2f, 0.1f);
            Assert.Less(DefenseGuildRules.SiegeResistance(amateur, 0.5f, P),
                        DefenseGuildRules.SiegeResistance(expert, 0.5f, P));
        }

        [Test]
        public void WillDefend_CauseOverPayment()
        {
            var g = new DefenseGuild(0.8f);
            // 大義が最低基準未満＝金を積まれても守らない
            Assert.IsFalse(DefenseGuildRules.WillDefend(g, 0.3f, 1f, P));
            // 高い大義は無報酬でも請け負う：appeal=0.75×0.8=0.6≥0.5
            Assert.IsTrue(DefenseGuildRules.WillDefend(g, 0.8f, 0f, P));
            // ぎりぎりの大義(0.4)は満額報酬でも足りない：0.75×0.4+0.25×1=0.55... 実は足りる→0.55≥0.5
            Assert.IsTrue(DefenseGuildRules.WillDefend(g, 0.4f, 1f, P));
            // ぎりぎりの大義(0.4)＋無報酬は不足：0.75×0.4=0.3<0.5
            Assert.IsFalse(DefenseGuildRules.WillDefend(g, 0.4f, 0f, P));
        }

        [Test]
        public void RefusesOffense_AlwaysTrue()
        {
            // 守城専門は攻撃に加担しない＝常に拒否
            Assert.IsTrue(DefenseGuildRules.RefusesOffense(new DefenseGuild(1f, 1f, 1f)));
            Assert.IsTrue(DefenseGuildRules.RefusesOffense(new DefenseGuild(0f, 0f, 0f)));
        }

        [Test]
        public void ReputationGain_RisesOnSuccessfulDefense()
        {
            var g = new DefenseGuild(0.8f, 0.6f, 0.5f); // merit=0.5×0.8+0.5×0.6=0.7
            Assert.AreEqual(0.7f, DefenseGuildRules.ReputationGain(g, true, P), 1e-5f);
            Assert.AreEqual(-0.7f, DefenseGuildRules.ReputationGain(g, false, P), 1e-5f);
        }

        [Test]
        public void AttritionUnderSiege_DepletesOverTime()
        {
            var g = new DefenseGuild(1f, 0f, 0.5f); // 規律0＝消耗緩和なし(resist=1)
            // loss=1×2×0.1×1=0.2 → 0.5−0.2=0.3
            Assert.AreEqual(0.3f, DefenseGuildRules.AttritionUnderSiege(g, 1f, 2f, P), 1e-4f);
            // 規律が高いと消耗が半減（resist=0.5）：loss=0.1 → 0.4
            var disciplined = new DefenseGuild(1f, 1f, 0.5f);
            Assert.AreEqual(0.4f, DefenseGuildRules.AttritionUnderSiege(disciplined, 1f, 2f, P), 1e-4f);
            // 0未満には割り込まない
            Assert.AreEqual(0f, DefenseGuildRules.AttritionUnderSiege(g, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void KnowledgeTransfer_LeavesDefenseBehind()
        {
            var g = new DefenseGuild(0.9f); // 技術上限0.9
            // 現地0.3 → MoveTowards(0.3,0.9,0.2)=0.5
            Assert.AreEqual(0.5f, DefenseGuildRules.KnowledgeTransfer(g, 0.3f, P), 1e-4f);
            // 集団技術を超えては伝わらない（現地が既に高い＝減らさない）
            Assert.AreEqual(0.95f, DefenseGuildRules.KnowledgeTransfer(g, 0.95f, P), 1e-4f);
        }

        [Test]
        public void IsImpregnableDefense_AboveThreshold()
        {
            Assert.IsTrue(DefenseGuildRules.IsImpregnableDefense(3.0f, 2.5f));
            Assert.IsFalse(DefenseGuildRules.IsImpregnableDefense(2.0f, 2.5f));
        }
    }
}
