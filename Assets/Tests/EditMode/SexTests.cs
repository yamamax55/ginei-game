using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 性別（<see cref="Sex"/>・人物/POP の属性・<see cref="SexRules"/>）を固定する：人物の既定性別、POP の男女比と人数、
    /// 男女比の偏り→出生係数（均衡で最大・偏ると低下）。性的指向は別軸の検討項目（未実装）。
    /// </summary>
    public class SexTests
    {
        [Test]
        public void Person_DefaultsToMale()
        {
            var p = new Person(1, "提督", Faction.帝国, PersonRole.軍人);
            Assert.AreEqual(Sex.男性, p.sex); // 既定=男性（後方互換）
            p.sex = Sex.女性;
            Assert.AreEqual(Sex.女性, p.sex);
        }

        [Test]
        public void Population_SexRatio_DefaultsBalanced()
        {
            var pop = new Population(20f, 60f, 20f); // Total=100
            Assert.AreEqual(SexRules.BalancedFemaleShare, pop.femaleShare, 1e-4f); // 既定0.5
            Assert.AreEqual(50f, pop.Females, 1e-3f);
            Assert.AreEqual(50f, pop.Males, 1e-3f);

            pop.femaleShare = 0.3f;
            Assert.AreEqual(30f, pop.Females, 1e-3f);
            Assert.AreEqual(70f, pop.Males, 1e-3f);
        }

        [Test]
        public void ShareOf_ReturnsBySex()
        {
            Assert.AreEqual(0.3f, SexRules.ShareOf(Sex.女性, 0.3f), 1e-4f);
            Assert.AreEqual(0.7f, SexRules.ShareOf(Sex.男性, 0.3f), 1e-4f);
        }

        [Test]
        public void BalanceFactor_MaxAtBalance_DropsWithSkew()
        {
            Assert.AreEqual(1f, SexRules.BalanceFactor(0.5f), 1e-4f);                       // 均衡＝最大
            Assert.AreEqual(1f - SexRules.MaxSkewPenalty, SexRules.BalanceFactor(0f), 1e-4f);  // 完全偏り
            Assert.AreEqual(1f - SexRules.MaxSkewPenalty, SexRules.BalanceFactor(1f), 1e-4f);
            // 偏るほど低下（0.6 の方が 0.9 より高い）
            Assert.Greater(SexRules.BalanceFactor(0.6f), SexRules.BalanceFactor(0.9f));
            // 値域クランプ
            Assert.AreEqual(SexRules.BalanceFactor(0f), SexRules.BalanceFactor(-1f), 1e-4f);
        }

        [Test]
        public void EligibleMilitaryFraction_GatesByPolicy()
        {
            // 平等（女性参加100%）＝全員が軍に就ける
            Assert.AreEqual(1f, SexRules.EligibleMilitaryFraction(0.5f, 1f), 1e-4f);
            // 家父長的（女性参加10%）＝男性0.5＋女性0.5×0.1＝0.55（半分の人口を活かせない）
            Assert.AreEqual(0.55f, SexRules.EligibleMilitaryFraction(0.5f, 0.1f), 1e-4f);
            // 女性参加0＝男性のみ
            Assert.AreEqual(0.5f, SexRules.EligibleMilitaryFraction(0.5f, 0f), 1e-4f);
            // 参加率が高いほど徴募源が広い
            Assert.Greater(SexRules.EligibleMilitaryFraction(0.5f, 1f), SexRules.EligibleMilitaryFraction(0.5f, 0.2f));
            // 全員男性なら参加政策によらず全員就ける
            Assert.AreEqual(1f, SexRules.EligibleMilitaryFraction(0f, 0f), 1e-4f);
        }
    }
}
