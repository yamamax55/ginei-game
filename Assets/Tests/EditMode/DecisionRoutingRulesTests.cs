using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>決裁デスクの絞り込み：自分の決裁箱宛てのみ通す／一般決裁は常に表示。</summary>
    public class DecisionRoutingRulesTests
    {
        [Test]
        public void PlayerDecisionBox_IsKingByDefault()
        {
            Assert.AreEqual(BoxKind.国王, DecisionRoutingRules.PlayerDecisionBox());
        }

        [Test]
        public void BoxRouted_ShownOnlyWhenMatchingPlayerBox()
        {
            Assert.IsTrue(DecisionRoutingRules.IsForPlayer(true, BoxKind.国王, BoxKind.国王));   // 自分の箱＝表示
            Assert.IsFalse(DecisionRoutingRules.IsForPlayer(true, BoxKind.政治家, BoxKind.国王)); // 他箱＝非表示
            Assert.IsFalse(DecisionRoutingRules.IsForPlayer(true, BoxKind.地方, BoxKind.国王));
        }

        [Test]
        public void NonBoxRouted_AlwaysShown()
        {
            // 箱に紐づかない一般決裁（イベント/システム）は箱に関係なく表示。
            Assert.IsTrue(DecisionRoutingRules.IsForPlayer(false, BoxKind.政治家, BoxKind.国王));
            Assert.IsTrue(DecisionRoutingRules.IsForPlayer(false, BoxKind.国王, BoxKind.国王));
        }
    }
}
