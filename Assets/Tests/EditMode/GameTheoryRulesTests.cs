using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 汎用 2x2 対称ゲーム（#388）の純ロジックを固定する：
    /// 利得表（囚人のジレンマ既定 T&gt;R&gt;P&gt;S）・ナッシュ均衡手・しっぺ返し・ゼロサム判定。
    /// </summary>
    public class GameTheoryRulesTests
    {
        private static readonly GameTheoryParams P = GameTheoryParams.Default; // T5 R3 P1 S0

        // ───────── 利得表 Payoff ─────────

        [Test]
        public void Payoff_FourCells_MatchPrisonersDilemma()
        {
            Assert.AreEqual(3f, GameTheoryRules.Payoff(Move.協調, Move.協調, P), 1e-4f);   // R
            Assert.AreEqual(0f, GameTheoryRules.Payoff(Move.協調, Move.裏切り, P), 1e-4f); // S
            Assert.AreEqual(5f, GameTheoryRules.Payoff(Move.裏切り, Move.協調, P), 1e-4f); // T
            Assert.AreEqual(1f, GameTheoryRules.Payoff(Move.裏切り, Move.裏切り, P), 1e-4f); // P
        }

        [Test]
        public void Payoff_DefaultOverload_EqualsExplicitDefault()
        {
            Assert.AreEqual(GameTheoryRules.Payoff(Move.裏切り, Move.協調, P),
                            GameTheoryRules.Payoff(Move.裏切り, Move.協調), 1e-4f);
        }

        [Test]
        public void Payoff_CustomParams_AreRespected()
        {
            var custom = new GameTheoryParams(10f, 7f, 2f, -1f);
            Assert.AreEqual(7f, GameTheoryRules.Payoff(Move.協調, Move.協調, custom), 1e-4f);
            Assert.AreEqual(-1f, GameTheoryRules.Payoff(Move.協調, Move.裏切り, custom), 1e-4f);
        }

        // ───────── ナッシュ均衡 NashEquilibrium ─────────

        [Test]
        public void Nash_PrisonersDilemma_IsDefect()
        {
            // T>R かつ P>S＝裏切りが支配戦略
            Assert.AreEqual(Move.裏切り, GameTheoryRules.NashEquilibrium(P));
        }

        [Test]
        public void Nash_StagHunt_IsCooperate()
        {
            // 鹿狩り：R>T かつ S>P＝協調が（相手協調時の）最善手
            var stag = new GameTheoryParams(temptation: 3f, reward: 5f, punishment: 1f, sucker: 2f);
            Assert.AreEqual(Move.協調, GameTheoryRules.NashEquilibrium(stag));
        }

        [Test]
        public void Nash_DefaultOverload_IsDefect()
        {
            Assert.AreEqual(Move.裏切り, GameTheoryRules.NashEquilibrium());
        }

        // ───────── しっぺ返し TitForTat ─────────

        [Test]
        public void TitForTat_EchoesOpponent()
        {
            Assert.AreEqual(Move.協調, GameTheoryRules.TitForTat(Move.協調));
            Assert.AreEqual(Move.裏切り, GameTheoryRules.TitForTat(Move.裏切り));
        }

        // ───────── ゼロサム判定 IsZeroSum ─────────

        [Test]
        public void IsZeroSum_PrisonersDilemma_IsFalse()
        {
            Assert.IsFalse(GameTheoryRules.IsZeroSum(P)); // R+R=6≠0
        }

        [Test]
        public void IsZeroSum_TrueZeroSum_IsTrue()
        {
            // R=0,P=0,T+S=0＝全セルで和ゼロ（純粋な敵対）
            var zs = new GameTheoryParams(temptation: 1f, reward: 0f, punishment: 0f, sucker: -1f);
            Assert.IsTrue(GameTheoryRules.IsZeroSum(zs));
        }

        [Test]
        public void IsZeroSum_DefaultOverload_IsFalse()
        {
            Assert.IsFalse(GameTheoryRules.IsZeroSum());
        }
    }
}
