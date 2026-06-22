using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊慰労（私財での労い）の費用/効果パラメータ。</summary>
    public readonly struct FleetReliefParams
    {
        public readonly float costPerStrength; // 慰労費（兵力あたり＝乗員数に比例）
        public readonly int moraleGain;        // 慰労による士気・結束の上昇（記録/将来反映用）

        public FleetReliefParams(float costPerStrength, int moraleGain)
        {
            this.costPerStrength = costPerStrength;
            this.moraleGain = moraleGain;
        }

        /// <summary>既定：兵力あたり 0.5 の私財／士気上昇 10。</summary>
        public static FleetReliefParams Default => new FleetReliefParams(0.5f, 10);
    }

    /// <summary>
    /// 艦隊慰労の純ロジック（決定論）。プレイヤー（主人公）が<b>私有財産の現金</b>を使って自艦隊を労う（慰労）ときの
    /// 費用・支払い可否・士気上昇を算出する唯一の窓口。費用は兵力（乗員数）に比例。財布（<see cref="Person.wealth"/>）の
    /// 増減や通知は呼び出し側（<see cref="PlayerFleetManagementOverlay"/>）が行い、本ルールは金額と可否だけを担う。test-first。
    /// </summary>
    public static class FleetReliefRules
    {
        /// <summary>慰労費＝兵力×係数（非負整数）。</summary>
        public static int Cost(int strength, FleetReliefParams p)
            => Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, strength) * p.costPerStrength));

        /// <summary>私財で慰労費をまかなえるか。</summary>
        public static bool CanAfford(float privateCash, int strength, FleetReliefParams p)
            => privateCash >= Cost(strength, p);

        /// <summary>慰労による士気上昇（非負）。</summary>
        public static int MoraleGain(FleetReliefParams p) => Mathf.Max(0, p.moraleGain);
    }
}
