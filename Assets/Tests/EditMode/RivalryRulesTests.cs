using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 宿敵（ヤン vs ラインハルト型）を固定する：強度＝対戦回数×互角度（1で飽和）、一方的な相手は
    /// 宿敵にならない、成長倍率1..1.5、集中ボーナスは対面会戦のみ、喪失の空虚は強度比例で深く
    /// 時間で緩やかに回復。境界とパラメータクランプを担保。
    /// </summary>
    public class RivalryRulesTests
    {
        private static readonly RivalryParams P = RivalryParams.Default;
        // 対戦寄与0.1/成長幅0.5/集中幅0.2/空虚深度0.8/回復0.05

        [Test]
        public void RivalryIntensity_GrowsWithEncountersAndCloseness()
        {
            Assert.AreEqual(0f, RivalryRules.RivalryIntensity(0, 1f, P), 1e-5f);     // 未対戦＝宿敵なし
            Assert.AreEqual(0.5f, RivalryRules.RivalryIntensity(5, 1f, P), 1e-5f);   // 5戦×互角＝半ば
            Assert.AreEqual(1f, RivalryRules.RivalryIntensity(10, 1f, P), 1e-5f);    // 10戦で飽和
            Assert.AreEqual(1f, RivalryRules.RivalryIntensity(100, 1f, P), 1e-5f);   // 回数は1で頭打ち
            Assert.AreEqual(0.25f, RivalryRules.RivalryIntensity(5, 0.5f, P), 1e-5f); // 半端な互角度は半減
        }

        [Test]
        public void RivalryIntensity_OneSidedOpponentNeverBecomesRival()
        {
            // 「互角の敵だけが人を磨く」＝一方的な相手（互角度0）は何度戦っても宿敵にならない。
            Assert.AreEqual(0f, RivalryRules.RivalryIntensity(100, 0f, P), 1e-5f);
            Assert.AreEqual(0f, RivalryRules.RivalryIntensity(100, -1f, P), 1e-5f);  // 負入力もクランプ
        }

        [Test]
        public void GrowthMultiplier_ScalesFromOneToOnePointFive()
        {
            Assert.AreEqual(1f, RivalryRules.GrowthMultiplier(0f, P), 1e-5f);     // 宿敵なし＝等倍（基準非破壊）
            Assert.AreEqual(1.25f, RivalryRules.GrowthMultiplier(0.5f, P), 1e-5f);
            Assert.AreEqual(1.5f, RivalryRules.GrowthMultiplier(1f, P), 1e-5f);   // 最大の好敵手＝1.5倍
            Assert.AreEqual(1.5f, RivalryRules.GrowthMultiplier(2f, P), 1e-5f);   // 過大入力はクランプ
            Assert.AreEqual(1f, RivalryRules.GrowthMultiplier(-1f, P), 1e-5f);    // 負入力はクランプ
        }

        [Test]
        public void FocusBonus_OnlyWhenFacingRival()
        {
            Assert.AreEqual(0.2f, RivalryRules.FocusBonus(1f, true, P), 1e-5f);   // 宿敵対面＝最大集中
            Assert.AreEqual(0.1f, RivalryRules.FocusBonus(0.5f, true, P), 1e-5f);
            Assert.AreEqual(0f, RivalryRules.FocusBonus(1f, false, P), 1e-5f);    // 不在の会戦は燃えない
        }

        [Test]
        public void VoidAfterDeath_DeeperForStrongerRival()
        {
            // 「宿敵の死は勝者から目標を奪う」＝強い宿敵ほど空虚が深い。
            Assert.AreEqual(0.8f, RivalryRules.VoidAfterDeath(1f, P), 1e-5f);
            Assert.AreEqual(0.4f, RivalryRules.VoidAfterDeath(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, RivalryRules.VoidAfterDeath(0f, P), 1e-5f);       // 宿敵でない敵の死は何も奪わない
        }

        [Test]
        public void VoidRecoveryTick_HealsSlowlyTowardZero()
        {
            Assert.AreEqual(0.75f, RivalryRules.VoidRecoveryTick(0.8f, 1f, P), 1e-5f);  // 0.05/dt で漸減
            Assert.AreEqual(0.55f, RivalryRules.VoidRecoveryTick(0.8f, 5f, P), 1e-5f);
            Assert.AreEqual(0f, RivalryRules.VoidRecoveryTick(0.8f, 100f, P), 1e-5f);   // いつかは癒える（0で頭打ち）
            Assert.AreEqual(0.8f, RivalryRules.VoidRecoveryTick(0.8f, -1f, P), 1e-5f);  // 負の dt は進まない
        }

        [Test]
        public void Params_CtorClampsNegatives()
        {
            var p = new RivalryParams(-1f, -1f, -1f, -1f, -1f);
            Assert.AreEqual(0f, p.intensityPerEncounter, 1e-5f);
            Assert.AreEqual(0f, p.growthScale, 1e-5f);
            Assert.AreEqual(0f, p.focusScale, 1e-5f);
            Assert.AreEqual(0f, p.voidScale, 1e-5f);
            Assert.AreEqual(0f, p.voidRecoveryRate, 1e-5f);
            // すべて0なら宿敵は育たず・倍率等倍・空虚も生じない（安全側）。
            Assert.AreEqual(0f, RivalryRules.RivalryIntensity(10, 1f, p), 1e-5f);
            Assert.AreEqual(1f, RivalryRules.GrowthMultiplier(1f, p), 1e-5f);
            Assert.AreEqual(0f, RivalryRules.VoidAfterDeath(1f, p), 1e-5f);
        }
    }
}
