using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 希望と末人（ロンドン派）（フロストパンク #852〜#856）を固定する：希望が尽きると末人が立ち、
    /// 信仰（希望回復）で鎮静／秩序（力の鎮圧）でも鎮まるが進めすぎると専制（虚構の暴走）。
    /// </summary>
    public class HopeRulesTests
    {
        private static readonly HopeParams P = HopeParams.Default; // dissent 0.25 / suppress 0.5 / tyranny 0.8

        [Test]
        public void HopeCollapse_RaisesDissent()
        {
            var c = new Community(1, hope: 1f);
            HopeRules.Shift(c, -0.85f); // 破局イベント（唯一の希望の喪失 等）
            Assert.AreEqual(0.15f, c.hope, 1e-4f);
            Assert.IsTrue(HopeRules.UpdateDissent(c, P)); // ロンドン派が立つ
            Assert.IsTrue(c.dissent);
        }

        [Test]
        public void Faith_RestoresHope_CalmsDissent()
        {
            var c = new Community(1, hope: 0.15f);
            HopeRules.UpdateDissent(c, P);
            Assert.IsTrue(c.dissent);

            HopeRules.Faith(c, 0.5f); // 意味を捏造して希望UP
            Assert.AreEqual(0.65f, c.hope, 1e-4f);
            Assert.IsFalse(HopeRules.UpdateDissent(c, P)); // 鎮静
        }

        [Test]
        public void Order_SuppressesDissent_ByForce_WithoutHope()
        {
            var c = new Community(1, hope: 0.15f);
            HopeRules.UpdateDissent(c, P);
            Assert.IsTrue(c.dissent);

            HopeRules.Order(c, 0.6f); // 力で鎮圧（希望は上がらない）
            Assert.AreEqual(0.6f, c.repression, 1e-4f);
            Assert.AreEqual(0.15f, c.hope, 1e-4f);            // 希望は低いまま
            Assert.IsFalse(HopeRules.UpdateDissent(c, P));    // だが抑圧で鎮まる
        }

        [Test]
        public void Order_TooMuch_BecomesTyranny()
        {
            var c = new Community(1, hope: 0.1f);
            HopeRules.Order(c, 0.5f);
            Assert.IsFalse(HopeRules.IsTyranny(c, P)); // 0.5 < 0.8
            HopeRules.Order(c, 0.4f);                  // 0.9
            Assert.IsTrue(HopeRules.IsTyranny(c, P));  // 専制＝虚構の暴走
        }

        [Test]
        public void Hope_And_Repression_Clamp()
        {
            var c = new Community(1, hope: 0.9f, repression: 0.9f);
            HopeRules.Faith(c, 1f);
            Assert.AreEqual(1f, c.hope, 1e-4f);
            HopeRules.Order(c, 1f);
            Assert.AreEqual(1f, c.repression, 1e-4f);
        }
    }
}
