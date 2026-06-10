using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 君主の臨御＝親征（#899）を固定する：前線に立つ王はカリスマに応じて将兵の士気を格別に高め戦力へ波及し、
    /// 激戦ほど・護衛が薄いほど戦死リスクを負う。親征して勝てば威信が跳ね負ければ傷つき、後方に留まるだけで
    /// 威信が目減りする（戦わぬ王は侮られる）。君主戦死の継承危機と親征の損得判断（両刃の天秤）を担保。
    /// </summary>
    public class RoyalPresenceRulesTests
    {
        private static readonly RoyalPresenceParams P = RoyalPresenceParams.Default;

        [Test]
        public void MoraleBonus_PresentScalesWithCharisma_AbsentZero()
        {
            // 臨御＋カリスマ1＝基礎0.1＋0.3×1＝0.4（名君ほど格別）
            Assert.AreEqual(0.4f, RoyalPresenceRules.MoraleBonus(1f, true, P), 1e-5f);
            // カリスマ0でも前線に立てば基礎0.1は付く
            Assert.AreEqual(0.1f, RoyalPresenceRules.MoraleBonus(0f, true, P), 1e-5f);
            // 後方に留まる王は士気を上げない＝0
            Assert.AreEqual(0f, RoyalPresenceRules.MoraleBonus(1f, false, P), 1e-5f);
        }

        [Test]
        public void CombatBonus_SpillsFromMorale()
        {
            // 士気0.4×波及0.5＝戦力ボーナス0.2
            Assert.AreEqual(0.2f, RoyalPresenceRules.CombatBonus(0.4f, P), 1e-5f);
            Assert.AreEqual(0f, RoyalPresenceRules.CombatBonus(0f, P), 1e-5f);
        }

        [Test]
        public void MonarchCasualtyRisk_IntensityRaises_GuardReduces_AbsentZero()
        {
            // 臨御×激戦1×護衛0＝(0.02+0.3×1)×(1−0)＝0.32（親征は命懸け）
            Assert.AreEqual(0.32f, RoyalPresenceRules.MonarchCasualtyRisk(1f, 0f, true, P), 1e-5f);
            // 護衛0.5で 0.32×(1−0.8×0.5)＝0.32×0.6＝0.192（厚い護衛が守る）
            Assert.AreEqual(0.192f, RoyalPresenceRules.MonarchCasualtyRisk(1f, 0.5f, true, P), 1e-5f);
            // 後方の王は前線で死なない＝0
            Assert.AreEqual(0f, RoyalPresenceRules.MonarchCasualtyRisk(1f, 0f, false, P), 1e-5f);
        }

        [Test]
        public void MonarchFalls_Deterministic()
        {
            Assert.IsTrue(RoyalPresenceRules.MonarchFalls(0.3f, 0.1f));  // roll<risk＝斃れる
            Assert.IsFalse(RoyalPresenceRules.MonarchFalls(0.3f, 0.5f)); // roll≥risk＝無事
            Assert.IsFalse(RoyalPresenceRules.MonarchFalls(0.3f, 1f));   // roll=1 は決して斃れない
        }

        [Test]
        public void PrestigeFromPresence_WinJumps_LossHurts_AbsenceErodes()
        {
            // 親征して勝てば英雄＝+0.3
            Assert.AreEqual(0.3f, RoyalPresenceRules.PrestigeFromPresence(true, true, P), 1e-5f);
            // 自ら出て負ければ権威が傷つく＝-0.25
            Assert.AreEqual(-0.25f, RoyalPresenceRules.PrestigeFromPresence(true, false, P), 1e-5f);
            // 後方に留まれば勝敗に関わらず威信が目減り＝-0.1（戦わぬ王は侮られる）
            Assert.AreEqual(-0.1f, RoyalPresenceRules.PrestigeFromPresence(false, true, P), 1e-5f);
            Assert.AreEqual(-0.1f, RoyalPresenceRules.PrestigeFromPresence(false, false, P), 1e-5f);
        }

        [Test]
        public void SuccessionCrisisOnDeath_ImportanceRaises_HeirClarityCalms()
        {
            // 要人度1×後継曖昧0＝(0.2+0.8×1)×(1−0)＝1.0（建国の英雄の急逝は国を最大に揺らす）
            Assert.AreEqual(1f, RoyalPresenceRules.SuccessionCrisisOnDeath(1f, 0f, P), 1e-5f);
            // 後継が明確（立太子済み）なら危機は鎮まる＝0
            Assert.AreEqual(0f, RoyalPresenceRules.SuccessionCrisisOnDeath(1f, 1f, P), 1e-5f);
            // 要人度0.5×後継明確さ0.5＝(0.2+0.8×0.5)×0.5＝0.6×0.5＝0.3
            Assert.AreEqual(0.3f, RoyalPresenceRules.SuccessionCrisisOnDeath(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void PresenceDecision_WeighsGainAgainstRiskedStake()
        {
            // 士気利得0.4 − リスク0.2×威信賭金1.0＝0.2（正＝臨御すべき）
            Assert.AreEqual(0.2f, RoyalPresenceRules.PresenceDecision(0.4f, 0.2f, 1f), 1e-5f);
            Assert.IsTrue(RoyalPresenceRules.ShouldTakeField(0.4f, 0.2f, 1f));
            // 利得0.1 − リスク0.5×賭金1.0＝-0.4（負＝退くべき＝両刃の天秤）
            Assert.AreEqual(-0.4f, RoyalPresenceRules.PresenceDecision(0.1f, 0.5f, 1f), 1e-5f);
            Assert.IsFalse(RoyalPresenceRules.ShouldTakeField(0.1f, 0.5f, 1f));
        }
    }
}
