using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>武聖（関羽）：限界突破の武勇・一騎打ちの達人・千里行（敵に下らず帰参）・荊州の孤立脆弱。</summary>
    public class WarSaintRulesTests
    {
        [Test]
        public void EffectiveMartial_TranscendsHundred()
        {
            Assert.AreEqual(130, WarSaintRules.EffectiveMartial(100, true)); // 限界突破（上限）
            Assert.AreEqual(104, WarSaintRules.EffectiveMartial(80, true));
            Assert.AreEqual(100, WarSaintRules.EffectiveMartial(100, false)); // 並は100止まり
            Assert.AreEqual(80, WarSaintRules.EffectiveMartial(80, false));
        }

        [Test]
        public void DuelMastery_And_AllianceRejection()
        {
            Assert.AreEqual(1.5f, WarSaintRules.DuelStrengthFactor(true), 1e-4f);  // 華雄/顔良/文醜を斬る
            Assert.AreEqual(1.0f, WarSaintRules.DuelStrengthFactor(false), 1e-4f);

            Assert.IsTrue(WarSaintRules.RejectsAllianceOffer(true));   // 虎の子はやれぬ
            Assert.IsFalse(WarSaintRules.RejectsAllianceOffer(false));
        }

        [Test]
        public void SenriKo_Devotion_And_JingzhouIsolation()
        {
            // 千里行：主君と結ばれる武聖は敵に下らない。
            Assert.IsTrue(WarSaintRules.ResistsEnemyRecruitment(true, true));
            Assert.IsFalse(WarSaintRules.ResistsEnemyRecruitment(true, false));  // 主君との絆なし
            Assert.IsFalse(WarSaintRules.ResistsEnemyRecruitment(false, true));  // 並

            // 荊州：同盟を失い孤立すると背後を突かれ脆くなる。
            Assert.AreEqual(1.4f, WarSaintRules.IsolationVulnerabilityFactor(true, false), 1e-4f); // 孤立
            Assert.AreEqual(1.0f, WarSaintRules.IsolationVulnerabilityFactor(true, true), 1e-4f);  // 同盟あり
            Assert.AreEqual(1.0f, WarSaintRules.IsolationVulnerabilityFactor(false, false), 1e-4f); // 並
        }
    }
}
