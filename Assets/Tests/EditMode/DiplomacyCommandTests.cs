using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>プレイヤー外交コマンド（#2119 拡張・操作化）：発令可否と適用。</summary>
    public class DiplomacyCommandTests
    {
        [Test]
        public void Commands_GateAndApply()
        {
            var dp = DiplomacyRules.DiplomacyParams.Default;
            var s = new DiplomacyState();

            // 平時：宣戦・同盟・不可侵は可、講和・破棄は不可
            Assert.IsTrue(DiplomacyCommandRules.CanIssue(s, "A", "B", DiplomaticAction.宣戦布告));
            Assert.IsTrue(DiplomacyCommandRules.CanIssue(s, "A", "B", DiplomaticAction.同盟));
            Assert.IsFalse(DiplomacyCommandRules.CanIssue(s, "A", "B", DiplomaticAction.講和));
            Assert.IsFalse(DiplomacyCommandRules.CanIssue(s, "A", "B", DiplomaticAction.破棄));

            // 宣戦布告→交戦
            Assert.IsTrue(DiplomacyCommandRules.Issue(s, "A", "B", DiplomaticAction.宣戦布告, dp));
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.交戦, s.Status("A", "B"));
            // 交戦中は再宣戦不可・講和は可
            Assert.IsFalse(DiplomacyCommandRules.CanIssue(s, "A", "B", DiplomaticAction.宣戦布告));
            Assert.IsTrue(DiplomacyCommandRules.Issue(s, "A", "B", DiplomaticAction.講和, dp));
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.平時, s.Status("A", "B"));

            // 同盟→破棄で平時
            Assert.IsTrue(DiplomacyCommandRules.Issue(s, "A", "B", DiplomaticAction.同盟, dp));
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.同盟, s.Status("A", "B"));
            Assert.IsTrue(DiplomacyCommandRules.CanIssue(s, "A", "B", DiplomaticAction.破棄));
            Assert.IsTrue(DiplomacyCommandRules.Issue(s, "A", "B", DiplomaticAction.破棄, dp));
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.平時, s.Status("A", "B"));

            // 不正：自分自身・null
            Assert.IsFalse(DiplomacyCommandRules.CanIssue(s, "A", "A", DiplomaticAction.宣戦布告));
            Assert.IsFalse(DiplomacyCommandRules.Issue(null, "A", "B", DiplomaticAction.同盟, dp));
        }
    }
}
