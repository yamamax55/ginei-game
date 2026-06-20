using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>AllianceChainRules（同盟連鎖・連合形成 DIPLO #2119）の純ロジック検証。</summary>
    public class AllianceChainRulesTests
    {
        private static DiplomacyState WithAlliance(string a, string b)
        {
            var s = new DiplomacyState();
            s.GetEntry(a, b, true).status = DiplomacyState.DiplomaticStatus.同盟;
            return s;
        }

        [Test]
        public void Null_ReturnsZero()
        {
            Assert.AreEqual(0, AllianceChainRules.FormTransitiveAlliances(null, 10f));
        }

        [Test]
        public void ZeroDrift_NoChange()
        {
            var s = WithAlliance("A", "B");
            s.GetEntry("B", "C", true).status = DiplomacyState.DiplomaticStatus.同盟;
            Assert.AreEqual(0, AllianceChainRules.FormTransitiveAlliances(s, 0f));
        }

        [Test]
        public void TransitivePair_DriftsTowardEachOther()
        {
            // A-B 同盟、B-C 同盟 → 共通の同盟相手 B を持つ A,C の関係値が上がる。
            var s = WithAlliance("A", "B");
            s.GetEntry("B", "C", true).status = DiplomacyState.DiplomaticStatus.同盟;
            int changed = AllianceChainRules.FormTransitiveAlliances(s, 10f);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(10f, s.Opinion("A", "C"), 1e-5f);
        }

        [Test]
        public void AlreadyAllied_NotDrifted()
        {
            // A-B, B-C, A-C すべて同盟 → 既に同盟の A,C は触らない。
            var s = WithAlliance("A", "B");
            s.GetEntry("B", "C", true).status = DiplomacyState.DiplomaticStatus.同盟;
            s.GetEntry("A", "C", true).status = DiplomacyState.DiplomaticStatus.同盟;
            int changed = AllianceChainRules.FormTransitiveAlliances(s, 10f);
            Assert.AreEqual(0, changed);
        }

        [Test]
        public void AlreadyGoodOpinion_NotDrifted()
        {
            // A,C が既に良好（>= GoodEnoughOpinion）なら引き上げない。
            var s = WithAlliance("A", "B");
            s.GetEntry("B", "C", true).status = DiplomacyState.DiplomaticStatus.同盟;
            var ac = s.GetEntry("A", "C", true);
            ac.opinion = AllianceChainRules.GoodEnoughOpinion + 5f;
            int changed = AllianceChainRules.FormTransitiveAlliances(s, 10f);
            Assert.AreEqual(0, changed);
            Assert.AreEqual(AllianceChainRules.GoodEnoughOpinion + 5f, s.Opinion("A", "C"), 1e-5f);
        }

        [Test]
        public void NoCommonAlly_NoChange()
        {
            // A-B 同盟のみ。共通相手を持つ第三者なし → 変化なし。
            var s = WithAlliance("A", "B");
            int changed = AllianceChainRules.FormTransitiveAlliances(s, 10f);
            Assert.AreEqual(0, changed);
        }
    }
}
