using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 現存艦隊（fleet in being）を固定する：潜在脅威＝戦力×(0.6+0.4×機動)、分散強要＝
    /// 潜在脅威/敵総戦力（満点1.0）、行動の自由＝1−分散強要、無活動減衰＝0.1/時、
    /// 出撃再活性＝失われた余地×陽動×0.8の埋め戻し、撃破リスク＝敵集中×(1−自軍戦力)、
    /// 費用対効果＝分散強要/維持費。クランプを担保。
    /// </summary>
    public class FleetInBeingRulesTests
    {
        private static readonly FleetInBeingParams P = FleetInBeingParams.Default;
        // 機動重み0.4/分散満点比1.0/無活動減衰0.1/出撃再活性上限0.8

        [Test]
        public void LatentThreat_StrengthProjectedByMobility()
        {
            Assert.AreEqual(1f, FleetInBeingRules.LatentThreat(1f, 1f, P), 1e-4f);    // 0.6+0.4*1=1.0
            Assert.AreEqual(0.6f, FleetInBeingRules.LatentThreat(1f, 0f, P), 1e-4f);  // 鈍足でも在るだけで0.6
            Assert.AreEqual(0.5f, FleetInBeingRules.LatentThreat(0.5f, 1f, P), 1e-4f); // 半分の戦力
            Assert.AreEqual(0f, FleetInBeingRules.LatentThreat(0f, 1f, P), 1e-4f);    // 戦力ゼロ＝脅威なし
            Assert.AreEqual(1f, FleetInBeingRules.LatentThreat(5f, 5f, P), 1e-4f);    // 入力クランプ
        }

        [Test]
        public void ForcedDispersion_ThreatRelativeToEnemyForce()
        {
            Assert.AreEqual(1f, FleetInBeingRules.ForcedDispersion(0.5f, 0.5f, P), 1e-4f); // 0.5/0.5＝満点で頭打ち
            Assert.AreEqual(0.4f, FleetInBeingRules.ForcedDispersion(0.4f, 1f, P), 1e-4f);
            Assert.AreEqual(0.2f, FleetInBeingRules.ForcedDispersion(0.4f, 2f, P), 1e-4f); // 大敵ほど縛りは薄まる
            Assert.AreEqual(0f, FleetInBeingRules.ForcedDispersion(0.5f, 0f, P), 1e-4f);   // 縛る相手がいない
        }

        [Test]
        public void EnemyFreedomOfAction_InverseOfDispersion()
        {
            Assert.AreEqual(0.7f, FleetInBeingRules.EnemyFreedomOfAction(0.3f), 1e-4f);
            Assert.AreEqual(0f, FleetInBeingRules.EnemyFreedomOfAction(1f), 1e-4f);   // 完全に縛られ動けない
            Assert.AreEqual(1f, FleetInBeingRules.EnemyFreedomOfAction(0f), 1e-4f);   // 縛りなし＝完全な自由
        }

        [Test]
        public void ThreatDecayIfPassive_ErodesOverTime()
        {
            Assert.AreEqual(1f, FleetInBeingRules.ThreatDecayIfPassive(1f, 0f, P), 1e-4f);   // 出たばかりは満額
            Assert.AreEqual(0.5f, FleetInBeingRules.ThreatDecayIfPassive(1f, 5f, P), 1e-4f); // 1-0.1*5=0.5
            Assert.AreEqual(0f, FleetInBeingRules.ThreatDecayIfPassive(0.8f, 10f, P), 1e-4f); // 完全に軽視される
        }

        [Test]
        public void SortieThreatRevival_FeintRefillsLostGround()
        {
            Assert.AreEqual(0.9f, FleetInBeingRules.SortieThreatRevival(0.5f, 1f, P), 1e-4f); // 0.5+0.5*1*0.8
            Assert.AreEqual(0.5f, FleetInBeingRules.SortieThreatRevival(0.5f, 0f, P), 1e-4f); // 動かねば戻らない
            Assert.AreEqual(0.8f, FleetInBeingRules.SortieThreatRevival(0f, 1f, P), 1e-4f);   // 0+1*0.8
        }

        [Test]
        public void RiskOfDestruction_WeakFleetUnderConcentration()
        {
            Assert.AreEqual(0f, FleetInBeingRules.RiskOfDestruction(1f, 1f), 1e-4f);   // 無傷の強艦隊は捕捉撃破されにくい
            Assert.AreEqual(1f, FleetInBeingRules.RiskOfDestruction(0f, 1f), 1e-4f);   // 弱体＋敵全力集中＝撃破必至
            Assert.AreEqual(0.4f, FleetInBeingRules.RiskOfDestruction(0.5f, 0.8f), 1e-4f);
            Assert.AreEqual(0f, FleetInBeingRules.RiskOfDestruction(0.5f, 0f), 1e-4f); // 敵が集中しなければ安全
        }

        [Test]
        public void CostEffectiveness_DispersionPerUpkeep()
        {
            Assert.AreEqual(0.4f, FleetInBeingRules.CostEffectiveness(0.8f, 2f), 1e-4f); // 0.8/2
            Assert.AreEqual(0.5f, FleetInBeingRules.CostEffectiveness(0.5f, 1f), 1e-4f);
            Assert.AreEqual(0f, FleetInBeingRules.CostEffectiveness(0f, 1f), 1e-4f);    // 縛れなければ価値なし
        }

        [Test]
        public void IsEffectiveFleetInBeing_DispersionAboveThreshold()
        {
            Assert.IsTrue(FleetInBeingRules.IsEffectiveFleetInBeing(0.5f, 0.3f));  // 十分縛れている
            Assert.IsFalse(FleetInBeingRules.IsEffectiveFleetInBeing(0.3f, 0.5f)); // 縛りが足りない
            Assert.IsFalse(FleetInBeingRules.IsEffectiveFleetInBeing(0.3f, 0.3f)); // 閾値ちょうど＝不成立（超のみ）
        }

        [Test]
        public void Story_IntactFleetBindsEnemyUntilDestroyedOrIgnored()
        {
            // 無傷の艦隊（戦力1.0・機動1.0）が、自軍と同規模の敵を強く縛る
            float threat = FleetInBeingRules.LatentThreat(1f, 1f, P);              // 1.0
            float dispersion = FleetInBeingRules.ForcedDispersion(threat, 1f, P);  // 1.0
            Assert.IsTrue(FleetInBeingRules.IsEffectiveFleetInBeing(dispersion, 0.5f));
            Assert.AreEqual(0f, FleetInBeingRules.EnemyFreedomOfAction(dispersion), 1e-4f); // 敵は動けない

            // だが艦隊を温存せず放置すると、脅威は時間で軽視され縛りが緩む
            float decayed = FleetInBeingRules.ThreatDecayIfPassive(threat, 7f, P); // 1-0.7=0.3
            float weakDispersion = FleetInBeingRules.ForcedDispersion(decayed, 1f, P); // 0.3
            Assert.IsFalse(FleetInBeingRules.IsEffectiveFleetInBeing(weakDispersion, 0.5f)); // もう縛れない

            // 撃破されれば（戦力0・敵全力集中）全ての縛りが消える＝撃破リスクは最大
            Assert.AreEqual(1f, FleetInBeingRules.RiskOfDestruction(0f, 1f), 1e-4f);
            Assert.AreEqual(0f, FleetInBeingRules.ForcedDispersion(0f, 1f, P), 1e-4f); // 脅威ゼロ＝縛りゼロ
        }
    }
}
