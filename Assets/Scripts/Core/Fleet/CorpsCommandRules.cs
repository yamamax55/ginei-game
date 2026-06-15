using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍団長（空母打撃群の打撃群指揮官に相当・<b>艦隊を持たず軍団旗艦に乗艦する</b>flag officer）が
    /// 軍団全体の能力・士気に与えるバフ／デバフの純ロジック（test-first）。
    /// 有能な軍団長は軍団全体を底上げ（バフ）、無能な軍団長は足を引っ張る（デバフ）。
    /// 艦の操艦＝艦長（各艦隊の `admiralData`）と、軍団の統御＝軍団長（`corpsCommander`）を分離する（CSG 準拠）。
    /// </summary>
    public static class CorpsCommandRules
    {
        /// <summary>影響幅（±20%）。軍団長の統率50で等倍、100で+20%、0で-20%。</summary>
        public const float MaxInfluence = 0.2f;

        /// <summary>軍団旗艦（軍団長乗艦）を失ったときの指揮空白デバフ（-10%）。</summary>
        public const float LeaderlessFactor = 0.9f;

        /// <summary>軍団全体の能力バフ／デバフ倍率（軍団長の統率基準）。</summary>
        public static float AbilityFactor(float commanderLeadership, float influence = MaxInfluence)
            => InfluenceFactor(commanderLeadership, influence);

        /// <summary>軍団全体の士気バフ／デバフ倍率（軍団長の統率基準＝信望が士気を左右）。</summary>
        public static float MoraleFactor(float commanderLeadership, float influence = MaxInfluence)
            => InfluenceFactor(commanderLeadership, influence);

        /// <summary>能力値(0..100)を中央50基準で ±influence の倍率に写す（クランプ）。</summary>
        private static float InfluenceFactor(float stat, float influence)
        {
            float t = (Mathf.Clamp(stat, 0f, 100f) - 50f) / 50f; // -1..+1
            float inf = Mathf.Max(0f, influence);
            return Mathf.Clamp(1f + t * inf, 1f - inf, 1f + inf);
        }
    }
}
