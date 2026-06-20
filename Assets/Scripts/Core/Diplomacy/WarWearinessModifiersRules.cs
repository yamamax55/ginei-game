using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦争厭戦の修飾（DIPLO・#2119／#192 拡張）。基礎厭戦（<see cref="WarGoalRules.WarWeariness"/>）に、
    /// <b>民心(支持)の低下・兵糧備蓄の枯渇・長期化</b>の修飾を足す＝「国内が疲れると講和へ傾く」。
    /// 基準式は <see cref="WarGoalRules"/> へ委譲（二重実装しない）。決定論・基準非破壊・test-first。
    /// </summary>
    public static class WarWearinessModifiersRules
    {
        /// <summary>修飾の調整値。</summary>
        public readonly struct Mod
        {
            public readonly float lowSupportThreshold; // これ未満の民心で厭戦加算
            public readonly float lowSupportPenalty;
            public readonly float lowFoodThreshold;    // これ未満の兵糧で厭戦加算
            public readonly float lowFoodPenalty;
            public readonly int longWarTurns;          // これを超える長期戦で1ターンあたり加算
            public readonly float longWarPenaltyPerTurn;

            public Mod(float lowSupportThreshold, float lowSupportPenalty, float lowFoodThreshold,
                float lowFoodPenalty, int longWarTurns, float longWarPenaltyPerTurn)
            {
                this.lowSupportThreshold = Mathf.Clamp01(lowSupportThreshold);
                this.lowSupportPenalty = Mathf.Max(0f, lowSupportPenalty);
                this.lowFoodThreshold = Mathf.Clamp01(lowFoodThreshold);
                this.lowFoodPenalty = Mathf.Max(0f, lowFoodPenalty);
                this.longWarTurns = Mathf.Max(0, longWarTurns);
                this.longWarPenaltyPerTurn = Mathf.Max(0f, longWarPenaltyPerTurn);
            }

            /// <summary>既定＝民心0.4未満で+0.10／兵糧0.3未満で+0.15／10ターン超で1ターンあたり+0.05。</summary>
            public static Mod Default => new Mod(0.4f, 0.10f, 0.3f, 0.15f, 10, 0.05f);
        }

        /// <summary>
        /// 修飾済みの厭戦（0..1）。基礎＝<see cref="WarGoalRules.WarWeariness"/>(turnsAtWar,casualties)、
        /// 民心 <paramref name="homeSupport01"/>・兵糧 <paramref name="foodReserve01"/>（各0..1）の低下と長期化で増す。
        /// </summary>
        public static float AdjustedWeariness(WarState w, float homeSupport01, float foodReserve01,
            WarGoalRules.WarGoalParams p, Mod m)
        {
            if (w == null) return 0f;
            float weariness = WarGoalRules.WarWeariness(w.turnsAtWar, w.casualties, p);
            if (Mathf.Clamp01(homeSupport01) < m.lowSupportThreshold) weariness += m.lowSupportPenalty;
            if (Mathf.Clamp01(foodReserve01) < m.lowFoodThreshold) weariness += m.lowFoodPenalty;
            if (w.turnsAtWar > m.longWarTurns)
                weariness += m.longWarPenaltyPerTurn * (w.turnsAtWar - m.longWarTurns);
            return Mathf.Clamp01(weariness);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float AdjustedWeariness(WarState w, float homeSupport01, float foodReserve01)
            => AdjustedWeariness(w, homeSupport01, foodReserve01, WarGoalRules.WarGoalParams.Default, Mod.Default);
    }
}
