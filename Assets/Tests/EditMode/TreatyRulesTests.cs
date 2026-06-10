using NUnit.Framework;
using Ginei;
using TP = Ginei.TreatyRules.TreatyParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 外交条約（EPIC #189・DIP-2 #191）を固定する：条約別 opinion 修正子・外交レバレッジ・違約判定・
    /// Treaty 純データの当事者判定／無期限判定・クランプ。すべて純ロジック・決定論。
    /// </summary>
    public class TreatyRulesTests
    {
        private const string A = "帝国";
        private const string B = "同盟";

        // ===== Treaty 純データ =====

        [Test]
        public void Treaty_Involves_OnlyParties()
        {
            var t = new Treaty(TreatyType.同盟, A, B, 10);
            Assert.IsTrue(t.Involves(A));
            Assert.IsTrue(t.Involves(B));
            Assert.IsFalse(t.Involves("フェザーン"));
        }

        [Test]
        public void Treaty_Perpetual_WhenDurationNonPositive()
        {
            Assert.IsTrue(new Treaty(TreatyType.不可侵, A, B, 0).IsPerpetual);
            Assert.IsTrue(new Treaty(TreatyType.不可侵, A, B, -5).IsPerpetual);
            Assert.IsFalse(new Treaty(TreatyType.不可侵, A, B, 3).IsPerpetual);
        }

        // ===== OpinionEffect =====

        [Test]
        public void OpinionEffect_PerType_MatchesDefault()
        {
            var p = TP.Default;
            Assert.AreEqual(40f, TreatyRules.OpinionEffect(TreatyType.同盟, p), 1e-4f);
            Assert.AreEqual(20f, TreatyRules.OpinionEffect(TreatyType.不可侵, p), 1e-4f);
            Assert.AreEqual(15f, TreatyRules.OpinionEffect(TreatyType.通商, p), 1e-4f);
            Assert.AreEqual(10f, TreatyRules.OpinionEffect(TreatyType.通行, p), 1e-4f);
            Assert.AreEqual(25f, TreatyRules.OpinionEffect(TreatyType.属国, p), 1e-4f);
        }

        [Test]
        public void OpinionEffect_ClampsToRange()
        {
            // 範囲を超える値を与えても [-100,100] に丸まる
            var huge = new TP(500f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            Assert.AreEqual(100f, TreatyRules.OpinionEffect(TreatyType.同盟, huge), 1e-4f);
            var deep = new TP(-500f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            Assert.AreEqual(-100f, TreatyRules.OpinionEffect(TreatyType.同盟, deep), 1e-4f);
        }

        // ===== Leverage =====

        [Test]
        public void Leverage_VassalStrongest_PerType()
        {
            var p = TP.Default;
            Assert.AreEqual(1.0f, TreatyRules.Leverage(TreatyType.属国, p), 1e-4f);
            Assert.AreEqual(0.6f, TreatyRules.Leverage(TreatyType.同盟, p), 1e-4f);
            Assert.AreEqual(0.3f, TreatyRules.Leverage(TreatyType.不可侵, p), 1e-4f);
            Assert.AreEqual(0.4f, TreatyRules.Leverage(TreatyType.通商, p), 1e-4f);
            Assert.AreEqual(0.2f, TreatyRules.Leverage(TreatyType.通行, p), 1e-4f);
            // 簡易窓口（既定 Params）も一致
            Assert.AreEqual(1.0f, TreatyRules.Leverage(TreatyType.属国), 1e-4f);
        }

        [Test]
        public void Leverage_ClampsTo01()
        {
            // 負は Params ctor で 0 に、過大は Leverage で 1 に丸まる
            var weird = new TP(0f, 0f, 0f, 0f, 0f, 5f, -3f, 0f, 0f, 0f);
            Assert.AreEqual(1.0f, TreatyRules.Leverage(TreatyType.同盟, weird), 1e-4f);
            Assert.AreEqual(0f, TreatyRules.Leverage(TreatyType.不可侵, weird), 1e-4f);
        }

        // ===== IsBreach =====

        [Test]
        public void IsBreach_AttackBreaksAllianceNonAggressionVassal()
        {
            Assert.IsTrue(TreatyRules.IsBreach(TreatyType.同盟, TreatyAction.攻撃));
            Assert.IsTrue(TreatyRules.IsBreach(TreatyType.不可侵, TreatyAction.攻撃));
            Assert.IsTrue(TreatyRules.IsBreach(TreatyType.属国, TreatyAction.攻撃));
            // 通商/通行は攻撃で割れない（別の違約条件を持つ）
            Assert.IsFalse(TreatyRules.IsBreach(TreatyType.通商, TreatyAction.攻撃));
            Assert.IsFalse(TreatyRules.IsBreach(TreatyType.通行, TreatyAction.攻撃));
        }

        [Test]
        public void IsBreach_TradeAndPassageHaveOwnConditions()
        {
            Assert.IsTrue(TreatyRules.IsBreach(TreatyType.通商, TreatyAction.交易遮断));
            Assert.IsTrue(TreatyRules.IsBreach(TreatyType.通行, TreatyAction.通行妨害));
            // 通常往来はいかなる条約も割らない
            Assert.IsFalse(TreatyRules.IsBreach(TreatyType.同盟, TreatyAction.通常往来));
            Assert.IsFalse(TreatyRules.IsBreach(TreatyType.通商, TreatyAction.通行妨害));
        }

        // ===== Clamp =====

        [Test]
        public void Clamp_BoundsOpinion()
        {
            Assert.AreEqual(100f, TreatyRules.Clamp(250f), 1e-4f);
            Assert.AreEqual(-100f, TreatyRules.Clamp(-250f), 1e-4f);
            Assert.AreEqual(13f, TreatyRules.Clamp(13f), 1e-4f);
        }
    }
}
