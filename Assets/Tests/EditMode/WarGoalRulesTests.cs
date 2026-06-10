using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>WarGoalRules（戦争と講和 DIP-3 #192）の純ロジック検証。決定論・境界・クランプを担保。</summary>
    public class WarGoalRulesTests
    {
        private static WarGoalRules.WarGoalParams P => WarGoalRules.WarGoalParams.Default;

        // ===== WarWeariness =====

        [Test]
        public void WarWeariness_LongerWarAndMoreCasualties_RaisesWeariness()
        {
            float low = WarGoalRules.WarWeariness(1, 0.1f, P);
            float high = WarGoalRules.WarWeariness(10, 0.8f, P);
            Assert.Greater(high, low);
        }

        [Test]
        public void WarWeariness_RepresentativeValue_IsDeterministic()
        {
            // 5ターン*0.02 + 0.5*0.6 = 0.1 + 0.3 = 0.4
            float w = WarGoalRules.WarWeariness(5, 0.5f, P);
            Assert.AreEqual(0.4f, w, 1e-4f);
        }

        [Test]
        public void WarWeariness_ClampsToCap_AndFloorAtZero()
        {
            // 巨大ターン＋満損害は cap(1.0) で頭打ち
            float capped = WarGoalRules.WarWeariness(1000, 1f, P);
            Assert.AreEqual(1f, capped, 1e-4f);
            // 負ターン・負損害は 0 へクランプ
            float floored = WarGoalRules.WarWeariness(-5, -0.5f, P);
            Assert.AreEqual(0f, floored, 1e-4f);
        }

        // ===== GoalLegitimacy =====

        [Test]
        public void GoalLegitimacy_LiberationMoreLegitimateThanConquest()
        {
            float liberation = WarGoalRules.GoalLegitimacy(CasusBelli.解放, 0f, P);
            float conquest = WarGoalRules.GoalLegitimacy(CasusBelli.征服, 0f, P);
            Assert.Greater(liberation, conquest);
        }

        [Test]
        public void GoalLegitimacy_HostileOpinion_RaisesLegitimacy()
        {
            // opinion 負（険悪）ほど正当性が上がる
            float friendly = WarGoalRules.GoalLegitimacy(CasusBelli.懲罰, 100f, P);
            float hostile = WarGoalRules.GoalLegitimacy(CasusBelli.懲罰, -100f, P);
            Assert.Greater(hostile, friendly);
            // 懲罰 base 0.5 + 100/100*0.3 = 0.8（敵対）
            Assert.AreEqual(0.8f, hostile, 1e-4f);
            // 0.5 - 0.3 = 0.2（友好）
            Assert.AreEqual(0.2f, friendly, 1e-4f);
        }

        [Test]
        public void GoalLegitimacy_ClampsTo01_AtExtremes()
        {
            // 解放(0.8) + 強烈な敵対(+0.3) は 1.0 で頭打ち、opinion 範囲外も丸める
            float over = WarGoalRules.GoalLegitimacy(CasusBelli.解放, -1000f, P);
            Assert.AreEqual(1f, over, 1e-4f);
            // 征服(0.2) + 友好(-0.3) は 0 未満 → 0 へクランプ
            float under = WarGoalRules.GoalLegitimacy(CasusBelli.征服, 1000f, P);
            Assert.AreEqual(0f, under, 1e-4f);
        }

        // ===== PeaceAcceptance =====

        [Test]
        public void PeaceAcceptance_LosingAndWeary_HighAcceptance()
        {
            float winning = WarGoalRules.PeaceAcceptance(1f, 0f, P);   // 圧勝・無厭戦
            float losing = WarGoalRules.PeaceAcceptance(-1f, 1f, P);   // 大敗・完全厭戦
            Assert.Greater(losing, winning);
            Assert.AreEqual(0f, winning, 1e-4f); // 劣勢ぶん0 + 厭戦0
            Assert.AreEqual(1f, losing, 1e-4f);  // 劣勢ぶん1 + 厭戦1
        }

        [Test]
        public void PeaceAcceptance_RepresentativeValue_IsDeterministic()
        {
            // warScore=0 → disadvantage 0.5、weariness=0.5、重み0.5/0.5 → (0.5*0.5+0.5*0.5)/1 = 0.5
            float a = WarGoalRules.PeaceAcceptance(0f, 0.5f, P);
            Assert.AreEqual(0.5f, a, 1e-4f);
        }

        [Test]
        public void PeaceAcceptance_ClampsInputs()
        {
            // 範囲外 warScore/weariness を丸めても 0..1 に収まる
            float a = WarGoalRules.PeaceAcceptance(-5f, 5f, P);
            Assert.AreEqual(1f, a, 1e-4f);
            float b = WarGoalRules.PeaceAcceptance(5f, -5f, P);
            Assert.AreEqual(0f, b, 1e-4f);
        }

        // ===== Reparations =====

        [Test]
        public void Reparations_OnlyWhenWinning()
        {
            Assert.AreEqual(0f, WarGoalRules.Reparations(-0.5f), 1e-4f); // 劣勢＝賠償なし
            Assert.AreEqual(0f, WarGoalRules.Reparations(0f), 1e-4f);    // 互角＝賠償なし
            Assert.AreEqual(0.7f, WarGoalRules.Reparations(0.7f), 1e-4f); // 優位ぶん
        }

        [Test]
        public void Reparations_ClampsToRange()
        {
            Assert.AreEqual(1f, WarGoalRules.Reparations(5f), 1e-4f);
            Assert.AreEqual(0f, WarGoalRules.Reparations(-5f), 1e-4f);
        }

        // ===== Default Params / BaseLegitimacy =====

        [Test]
        public void DefaultParams_ClampNegativeInputs()
        {
            var p = new WarGoalRules.WarGoalParams(-1f, -1f, 5f, -1f, -1f, -1f);
            // 負の率はゼロ床、cap は 0..1 クランプ → 全入力ゼロでも weariness は 0
            Assert.AreEqual(0f, WarGoalRules.WarWeariness(10, 1f, p), 1e-4f);
            Assert.AreEqual(1f, p.wearinessCap, 1e-4f);
        }

        [Test]
        public void BaseLegitimacy_OrdersByCasusBelli()
        {
            Assert.Less(WarGoalRules.BaseLegitimacy(CasusBelli.征服), WarGoalRules.BaseLegitimacy(CasusBelli.従属));
            Assert.Less(WarGoalRules.BaseLegitimacy(CasusBelli.従属), WarGoalRules.BaseLegitimacy(CasusBelli.懲罰));
            Assert.Less(WarGoalRules.BaseLegitimacy(CasusBelli.懲罰), WarGoalRules.BaseLegitimacy(CasusBelli.解放));
        }
    }
}
