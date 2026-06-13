using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>会戦AIの判断ロジック（会戦改善）：陣形ドクトリン＋撤退幾何。</summary>
    public class BattleAiRulesTests
    {
        [Test]
        public void Formation_Doctrine()
        {
            // 敗走＝円陣
            Assert.AreEqual(Formation.円陣, FormationDoctrineRules.RecommendFormation(100, 100, true, null));
            // 劣勢（0.5）＝方陣
            Assert.AreEqual(Formation.方陣, FormationDoctrineRules.RecommendFormation(50, 100, false, null));
            // 優勢（2.0）＝鶴翼陣
            Assert.AreEqual(Formation.鶴翼陣, FormationDoctrineRules.RecommendFormation(200, 100, false, null));
            // 互角・提督なし＝紡錘陣（得意陣形ルートは admiral.hasPreferredFormation で分岐・GameのUpdateで使用）
            Assert.AreEqual(Formation.紡錘陣, FormationDoctrineRules.RecommendFormation(100, 100, false, null));
            // 境界：ちょうど0.6（劣勢側）＝方陣／ちょうど1.5（優勢側）＝鶴翼陣
            Assert.AreEqual(Formation.方陣, FormationDoctrineRules.RecommendFormation(60, 100, false, null));
            Assert.AreEqual(Formation.鶴翼陣, FormationDoctrineRules.RecommendFormation(150, 100, false, null));
        }

        [Test]
        public void Withdrawal_Geometry()
        {
            Assert.AreEqual(Vector2.right, BattleWithdrawalRules.AwayDirection(new Vector2(10, 0), Vector2.zero));
            var target = BattleWithdrawalRules.WithdrawalTarget(new Vector2(10, 0), Vector2.zero, 5f);
            Assert.AreEqual(15f, target.x, 1e-3f);
            Assert.AreEqual(0f, target.y, 1e-3f);

            // 自勢力側の外周（敵=原点、自分は外周50）＝離脱
            Assert.IsTrue(BattleWithdrawalRules.IsAtWithdrawalEdge(new Vector2(50, 0), Vector2.zero, 40f));
            // まだ中央寄り＝離脱しない
            Assert.IsFalse(BattleWithdrawalRules.IsAtWithdrawalEdge(new Vector2(10, 0), Vector2.zero, 40f));
            // 敵側へ突っ込んで外周に達した（敵が外側 60）＝離脱とみなさない
            Assert.IsFalse(BattleWithdrawalRules.IsAtWithdrawalEdge(new Vector2(50, 0), new Vector2(60, 0), 40f));
        }
    }
}
