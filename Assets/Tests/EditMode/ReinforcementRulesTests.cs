using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>増援：到着判定（game-time）と出現端の座標。</summary>
    public class ReinforcementRulesTests
    {
        [Test]
        public void IsDue_AtOrAfterDelay()
        {
            Assert.IsFalse(ReinforcementRules.IsDue(30f, 10f));
            Assert.IsTrue(ReinforcementRules.IsDue(30f, 30f));
            Assert.IsTrue(ReinforcementRules.IsDue(30f, 45f));
            Assert.IsTrue(ReinforcementRules.IsDue(0f, 0f)); // 遅延0は即時
        }

        [Test]
        public void EdgePosition_FactionSide()
        {
            Vector2 emp = ReinforcementRules.EdgePosition(Faction.帝国, 5f, 45f);
            Assert.AreEqual(-45f, emp.x, 1e-4f); // 帝国＝左端
            Assert.AreEqual(5f, emp.y, 1e-4f);
            Vector2  all = ReinforcementRules.EdgePosition(Faction.同盟, -3f, 45f);
            Assert.AreEqual(45f, all.x, 1e-4f);  // 同盟＝右端
        }
    }
}
