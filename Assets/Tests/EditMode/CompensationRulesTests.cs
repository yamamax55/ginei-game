using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>CompensationRules（#996・信賞必罰）の純ロジックを既定Paramsの具体値で固定する。</summary>
    public class CompensationRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>賞/罰の種別判定＝金銭/昇進/授爵/恩賞は賞、叱責/降格/処罰は罰。</summary>
        [Test]
        public void IsReward_賞と罰を種別で分ける()
        {
            Assert.IsTrue(CompensationRules.IsReward(RewardType.金銭));
            Assert.IsTrue(CompensationRules.IsReward(RewardType.授爵));
            Assert.IsTrue(CompensationRules.IsPunishment(RewardType.処罰));
            Assert.IsTrue(CompensationRules.IsPunishment(RewardType.叱責));
        }

        /// <summary>功に見合う賞は士気を上げる＝deservedness=1で magnitude×rewardScale。</summary>
        [Test]
        public void MoraleEffect_妥当な賞は士気を上げる()
        {
            // justice = 2×1-1 = 1 → 0.8×0.3×1 = 0.24
            float m = CompensationRules.MoraleEffect(RewardType.恩賞, 0.8f, 1f);
            Assert.AreEqual(0.24f, m, Eps);
            Assert.Greater(m, 0f);
        }

        /// <summary>不当な賞（功なき過大報酬）は逆効果＝士気を削る（injusticePenalty で増幅）。</summary>
        [Test]
        public void MoraleEffect_不当な賞は逆効果で士気を削る()
        {
            // deservedness=0 → justice=-1 → gain=1×0.3×(-1)=-0.3、×injusticePenalty1.5 = -0.45
            float m = CompensationRules.MoraleEffect(RewardType.授爵, 1f, 0f);
            Assert.AreEqual(-0.45f, m, Eps);
            Assert.Less(m, 0f);
        }

        /// <summary>冤罪の罰は正当な罰よりはるかに士気を削る（信賞必罰の崩壊）。</summary>
        [Test]
        public void MoraleEffect_冤罪の罰は正当な罰より大きく士気を削る()
        {
            // 正当(d=1): justice=1 → loss=-1×0.4 ×(1-1)=0（規律として痛まない）
            float just = CompensationRules.MoraleEffect(RewardType.処罰, 1f, 1f);
            Assert.AreEqual(0f, just, Eps);
            // 冤罪(d=0): justice=-1 → loss=-0.4 ×(1+1×1.5)=-1.0
            float wrongful = CompensationRules.MoraleEffect(RewardType.処罰, 1f, 0f);
            Assert.AreEqual(-1.0f, wrongful, Eps);
            Assert.Less(wrongful, just);
        }

        /// <summary>忠誠効果＝賞は士気と同じ、罰は0.7倍に粘る（正当な罰は忠誠を大きく損なわない）。</summary>
        [Test]
        public void LoyaltyEffect_罰は士気より忠誠への打撃が小さい()
        {
            float morale = CompensationRules.MoraleEffect(RewardType.降格, 1f, 0f);
            float loyalty = CompensationRules.LoyaltyEffect(RewardType.降格, 1f, 0f);
            Assert.AreEqual(morale * 0.7f, loyalty, Eps);
            // 賞は同値。
            float rMorale = CompensationRules.MoraleEffect(RewardType.金銭, 0.5f, 1f);
            float rLoyalty = CompensationRules.LoyaltyEffect(RewardType.金銭, 0.5f, 1f);
            Assert.AreEqual(rMorale, rLoyalty, Eps);
        }

        /// <summary>公平感＝功と報酬が一致で1.0、過大も過小も不満で下がる。</summary>
        [Test]
        public void FairnessPerception_過大も過小も不満()
        {
            Assert.AreEqual(1f, CompensationRules.FairnessPerception(1f, 1f), Eps);
            // 過小（与0.5/相当1.0）→ 誤差0.5 → 0.5
            Assert.AreEqual(0.5f, CompensationRules.FairnessPerception(0.5f, 1f), Eps);
            // 過大（与2.0/相当1.0）→ 誤差1.0 → 0
            Assert.AreEqual(0f, CompensationRules.FairnessPerception(2f, 1f), Eps);
        }

        /// <summary>報酬インフレ＝乱発で価値逓減・希少性で延命（HonorsRules と同型）。</summary>
        [Test]
        public void RewardInflation_乱発で価値が下がり希少性で延命する()
        {
            // 授与0なら満額1.0。
            Assert.AreEqual(1f, CompensationRules.RewardInflation(0, 0f), Eps);
            // 希少性0で半減数20 → 20授与で 1/(1+20/20)=0.5
            Assert.AreEqual(0.5f, CompensationRules.RewardInflation(20, 0f), Eps);
            // 希少性1で実効半減数40 → 同20授与で 1/(1+20/40)=0.6667（インフレに強い）
            float scarce = CompensationRules.RewardInflation(20, 1f);
            Assert.AreEqual(0.6667f, scarce, 0.001f);
            Assert.Greater(scarce, 0.5f);
        }

        /// <summary>罰の抑止力＝見せしめ（可視）の重い処罰ほど他者を律する。賞は抑止力0。</summary>
        [Test]
        public void PunishmentDeterrence_見せしめの処罰は他者を律する()
        {
            // 処罰typeWeight=1, s=1,v=1 → 1×1×1×(1+0.25)=1.25→クランプ1.0
            Assert.AreEqual(1f, CompensationRules.PunishmentDeterrence(RewardType.処罰, 1f, 1f), Eps);
            // 可視性0（陰の処罰）→ 0
            Assert.AreEqual(0f, CompensationRules.PunishmentDeterrence(RewardType.処罰, 1f, 0f), Eps);
            // 賞は抑止力なし。
            Assert.AreEqual(0f, CompensationRules.PunishmentDeterrence(RewardType.恩賞, 1f, 1f), Eps);
        }

        /// <summary>信賞必罰の度合い＝功に賞・過に罰が一貫すれば高く、ちぐはぐで崩れる。</summary>
        [Test]
        public void MeritRewardBalance_功と賞罰の一貫性を測る()
        {
            // 功(+1)に賞(+1)＝完全一致 → 1.0
            Assert.AreEqual(1f, CompensationRules.MeritRewardBalance(1f, 1f), Eps);
            // 功(+1)に罰(-1)＝符号逆 → 1-(1+1)×0.5=0
            Assert.AreEqual(0f, CompensationRules.MeritRewardBalance(-1f, 1f), Eps);
            // 過(-0.8)に罰(-0.8)＝一致 → 1.0
            Assert.AreEqual(1f, CompensationRules.MeritRewardBalance(-0.8f, -0.8f), Eps);
            // 功(+1)に過小な賞(+0.2)＝同符号だが釣り合わず → 1-0.8=0.2
            Assert.AreEqual(0.2f, CompensationRules.MeritRewardBalance(0.2f, 1f), Eps);
        }
    }
}
