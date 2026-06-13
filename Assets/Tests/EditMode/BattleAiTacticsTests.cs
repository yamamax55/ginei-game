using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>会戦AIの戦術判断（#2253）：陣形カウンター・特殊指揮選択・難易度ゲート。</summary>
    public class BattleAiTacticsTests
    {
        [Test]
        public void CounterFormation_TriangleInverse()
        {
            Assert.AreEqual(Formation.紡錘陣, BattleAiRules.CounterFormation(Formation.横陣));
            Assert.AreEqual(Formation.横陣, BattleAiRules.CounterFormation(Formation.鶴翼陣));
            Assert.AreEqual(Formation.鶴翼陣, BattleAiRules.CounterFormation(Formation.紡錘陣));
            // 実際にカウンターが有利（相性>1）であることを相性ルールで確認。
            Assert.Greater(FormationMatchupRules.AttackFactor(Formation.紡錘陣, Formation.横陣), 1f);
            // 守勢陣形は据え置き。
            Assert.AreEqual(Formation.円陣, BattleAiRules.CounterFormation(Formation.円陣));
        }

        [Test]
        public void TryChooseCommand_Branches()
        {
            ActiveCommand cmd;
            Assert.IsTrue(BattleAiRules.TryChooseCommand(true, 0.3f, 1.0f, out cmd));   // 低士気
            Assert.AreEqual(ActiveCommand.不退転, cmd);
            Assert.IsTrue(BattleAiRules.TryChooseCommand(true, 0.8f, 1.3f, out cmd));   // 優勢
            Assert.AreEqual(ActiveCommand.突撃, cmd);
            Assert.IsTrue(BattleAiRules.TryChooseCommand(true, 0.8f, 1.0f, out cmd));   // 交戦中
            Assert.AreEqual(ActiveCommand.一斉砲撃, cmd);
            Assert.IsFalse(BattleAiRules.TryChooseCommand(false, 0.8f, 1.0f, out cmd)); // 非交戦・拮抗＝なし
        }

        [Test]
        public void ShouldAct_GatedBySkill()
        {
            Assert.IsTrue(BattleAiRules.ShouldAct(0.8f, 0.5f));  // 有能＝取る
            Assert.IsFalse(BattleAiRules.ShouldAct(0.2f, 0.5f)); // 無能＝取りこぼす
            Assert.IsTrue(BattleAiRules.ShouldAct(1f, 1f));
        }
    }
}
