using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 扇動政治家（トリューニヒト型）を固定する：訴求力＝雄弁×(1+恐怖)＋敵作り（平時はただの雄弁家）、
    /// 責任回避＝説明責任の低さ（roll決定論）、逃げ切れば無傷、統治の静かな浸食。境界を担保。
    /// </summary>
    public class DemagogueRulesTests
    {
        private static readonly DemagogueParams P = DemagogueParams.Default;
        // 雄弁0.4/恐怖増幅1.0/敵作り0.2/浸食0.01

        [Test]
        public void Appeal_FearIsFuel()
        {
            // 平時（恐怖0・標的なし）＝ただの雄弁家：0.4
            Assert.AreEqual(0.4f, DemagogueRules.Appeal(1f, 0f, false, P), 1e-5f);
            // 恐怖の時代＝倍化：0.4×2=0.8
            Assert.AreEqual(0.8f, DemagogueRules.Appeal(1f, 1f, false, P), 1e-5f);
            // 敵作りも乗せれば満点
            Assert.AreEqual(1f, DemagogueRules.Appeal(1f, 1f, true, P), 1e-5f);
            // 口下手の扇動は効かない
            Assert.AreEqual(0.2f, DemagogueRules.Appeal(0f, 1f, true, P), 1e-5f);
        }

        [Test]
        public void DodgeChance_AccountabilityIsTheOnlyCage()
        {
            Assert.AreEqual(1f, DemagogueRules.DodgeChance(0f), 1e-5f);   // 説明責任なき体制＝必ず逃げ切る
            Assert.AreEqual(0f, DemagogueRules.DodgeChance(1f), 1e-5f);   // 完全な説明責任＝逃げ場なし
            Assert.IsTrue(DemagogueRules.DodgesResponsibility(0.3f, 0.69f));
            Assert.IsFalse(DemagogueRules.DodgesResponsibility(0.3f, 0.71f));
        }

        [Test]
        public void SupportRetention_EscapeMeansUnharmed()
        {
            // 逃げ切り＝大危機でも無傷（失点は消えた者に付かない）
            Assert.AreEqual(1f, DemagogueRules.SupportRetention(true, 1f), 1e-5f);
            // 捕まれば危機の深さ分だけ削られる
            Assert.AreEqual(0.3f, DemagogueRules.SupportRetention(false, 0.7f), 1e-5f);
            Assert.AreEqual(0f, DemagogueRules.SupportRetention(false, 1f), 1e-5f);
        }

        [Test]
        public void GovernanceErosion_HollowOfficeRots()
        {
            // 要職（重み1）×在任10＝0.1 の統治劣化
            Assert.AreEqual(0.1f, DemagogueRules.GovernanceErosion(1f, 10f, P), 1e-5f);
            // 閑職なら害も小さい
            Assert.AreEqual(0.01f, DemagogueRules.GovernanceErosion(0.1f, 10f, P), 1e-5f);
            Assert.AreEqual(0f, DemagogueRules.GovernanceErosion(0f, 10f, P), 1e-5f);
        }
    }
}
