using UnityEngine;

namespace Ginei
{
    /// <summary>艦の退役・除籍判断の調整係数。</summary>
    public readonly struct ShipDecommissionParams
    {
        /// <summary>旧式化が満点(1.0)に達する艦齢の基準（これより若ければ艦齢寄与は線形）。</summary>
        public readonly float ageObsolescenceSpan;
        /// <summary>世代差1あたりの旧式化寄与（古い世代ほど効く）。</summary>
        public readonly float generationWeight;
        /// <summary>維持コスト負担に効く船体老朽の重み（1-船体状態への掛け）。</summary>
        public readonly float conditionBurdenWeight;
        /// <summary>維持コスト負担の艦齢正規化基準（これで艦齢を割って0..1へ）。</summary>
        public readonly float ageBurdenSpan;
        /// <summary>近代化妥当性が無限大に飛ばないための改修費下限（ゼロ割回避）。</summary>
        public readonly float minUpgradeCost;
        /// <summary>退役切迫度の分母下限（残存価値ゼロでも割れるよう）。</summary>
        public readonly float minServiceValue;

        public ShipDecommissionParams(float ageObsolescenceSpan, float generationWeight, float conditionBurdenWeight,
                                      float ageBurdenSpan, float minUpgradeCost, float minServiceValue)
        {
            this.ageObsolescenceSpan = Mathf.Max(1f, ageObsolescenceSpan);
            this.generationWeight = Mathf.Clamp01(generationWeight);
            this.conditionBurdenWeight = Mathf.Clamp01(conditionBurdenWeight);
            this.ageBurdenSpan = Mathf.Max(1f, ageBurdenSpan);
            this.minUpgradeCost = Mathf.Max(0.01f, minUpgradeCost);
            this.minServiceValue = Mathf.Max(0.01f, minServiceValue);
        }

        /// <summary>既定＝艦齢旧式化基準40・世代重み0.25・老朽重み0.5・艦齢負担基準40・改修費下限1・残存価値下限1。</summary>
        public static ShipDecommissionParams Default => new ShipDecommissionParams(40f, 0.25f, 0.5f, 40f, 1f, 1f);
    }

    /// <summary>
    /// 艦の退役・除籍の純ロジック。艦齢と世代差で旧式化し（<see cref="Obsolescence"/>）、旧式化が戦力価値を削る
    /// （<see cref="RemainingServiceValue"/>）。老朽艦は維持コストが高く（<see cref="MaintenanceBurden"/>）、
    /// 残存価値と改修費を天秤にかけて近代化改修するか（<see cref="ModernizationViability"/>）、予備役で眠らせるか
    /// （<see cref="MothballValue"/>＝<see cref="MothballRules"/> の保管とは別観点の「眠らせる価値」）、解体してリサイクルするか
    /// （<see cref="ScrapRecovery"/>）を判断する。維持負担が残存価値を上回るほど退役は切迫し（<see cref="RetirementUrgency"/>）、
    /// 改修の妥当性より退役の切迫度が勝てば退役する（<see cref="ShouldDecommission"/>）。
    /// 乱数なし・決定論。実効値パターン（基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ShipDecommissionRules
    {
        /// <summary>
        /// 旧式化(0..1)＝艦齢寄与（艦齢/基準・1で頭打ち）と世代差寄与（(現行世代-艦世代)×世代重み）の合算。
        /// 古く・世代遅れなほど1へ近づく。
        /// </summary>
        public static float Obsolescence(float shipAge, float techGeneration, float currentGeneration, ShipDecommissionParams p)
        {
            float ageTerm = Mathf.Clamp01(Mathf.Max(0f, shipAge) / p.ageObsolescenceSpan);
            float genGap = Mathf.Max(0f, currentGeneration - techGeneration);
            float genTerm = genGap * p.generationWeight;
            return Mathf.Clamp01(ageTerm + genTerm);
        }

        public static float Obsolescence(float shipAge, float techGeneration, float currentGeneration)
            => Obsolescence(shipAge, techGeneration, currentGeneration, ShipDecommissionParams.Default);

        /// <summary>残存戦力価値＝戦力×(1-旧式化)。旧式なほど価値が目減りする。</summary>
        public static float RemainingServiceValue(float combatValue, float obsolescence, ShipDecommissionParams p)
        {
            return Mathf.Max(0f, combatValue) * (1f - Mathf.Clamp01(obsolescence));
        }

        public static float RemainingServiceValue(float combatValue, float obsolescence)
            => RemainingServiceValue(combatValue, obsolescence, ShipDecommissionParams.Default);

        /// <summary>
        /// 維持コスト負担＝(艦齢/基準・1で頭打ち)×(1+老朽重み×(1-船体状態))。老朽・劣化ほど高くつく。
        /// </summary>
        public static float MaintenanceBurden(float shipAge, float hullCondition, ShipDecommissionParams p)
        {
            float ageFactor = Mathf.Clamp01(Mathf.Max(0f, shipAge) / p.ageBurdenSpan);
            float wear = 1f - Mathf.Clamp01(hullCondition);
            return ageFactor * (1f + p.conditionBurdenWeight * wear);
        }

        public static float MaintenanceBurden(float shipAge, float hullCondition)
            => MaintenanceBurden(shipAge, hullCondition, ShipDecommissionParams.Default);

        /// <summary>
        /// 近代化改修の妥当性＝船体状態×残存価値/改修費。状態の良い・価値の高い艦を安く直せるほど妥当。
        /// ボロ船（状態低い）や残存価値の薄い艦を高く直すのは妥当でない。
        /// </summary>
        public static float ModernizationViability(float hullCondition, float upgradeCost, float remainingServiceValue, ShipDecommissionParams p)
        {
            float cost = Mathf.Max(p.minUpgradeCost, upgradeCost);
            return Mathf.Clamp01(hullCondition) * Mathf.Max(0f, remainingServiceValue) / cost;
        }

        public static float ModernizationViability(float hullCondition, float upgradeCost, float remainingServiceValue)
            => ModernizationViability(hullCondition, upgradeCost, remainingServiceValue, ShipDecommissionParams.Default);

        /// <summary>予備役保管の価値＝残存価値−保管コスト。眠らせて温存する価値（負なら眠らせる意味なし）。</summary>
        public static float MothballValue(float remainingServiceValue, float storageCost, ShipDecommissionParams p)
        {
            return Mathf.Max(0f, remainingServiceValue) - Mathf.Max(0f, storageCost);
        }

        public static float MothballValue(float remainingServiceValue, float storageCost)
            => MothballValue(remainingServiceValue, storageCost, ShipDecommissionParams.Default);

        /// <summary>解体リサイクルの回収＝船体質量×資材価値。大艦ほど・資材価値が高いほど解体で取り戻せる。</summary>
        public static float ScrapRecovery(float hullMass, float materialValue, ShipDecommissionParams p)
        {
            return Mathf.Max(0f, hullMass) * Mathf.Max(0f, materialValue);
        }

        public static float ScrapRecovery(float hullMass, float materialValue)
            => ScrapRecovery(hullMass, materialValue, ShipDecommissionParams.Default);

        /// <summary>
        /// 退役の切迫度(0..1)＝維持負担/(残存価値の正規化)。維持に金がかかるのに価値が薄い艦ほど切迫する。
        /// 残存価値は下限でクランプしてゼロ割を避ける。
        /// </summary>
        public static float RetirementUrgency(float maintenanceBurden, float remainingServiceValue, ShipDecommissionParams p)
        {
            float value = Mathf.Max(p.minServiceValue, remainingServiceValue);
            return Mathf.Clamp01(Mathf.Max(0f, maintenanceBurden) / value);
        }

        public static float RetirementUrgency(float maintenanceBurden, float remainingServiceValue)
            => RetirementUrgency(maintenanceBurden, remainingServiceValue, ShipDecommissionParams.Default);

        /// <summary>
        /// 退役すべきか＝退役切迫度が（近代化妥当性＋閾値）を上回るか。改修して延命するより退役が勝るとき true。
        /// 閾値は改修側に下駄を履かせる余裕（高いほど退役しにくい＝艦を温存する保守的判断）。
        /// </summary>
        public static bool ShouldDecommission(float retirementUrgency, float modernizationViability, float threshold)
        {
            return Mathf.Clamp01(retirementUrgency) > Mathf.Max(0f, modernizationViability) + Mathf.Max(0f, threshold);
        }

        /// <summary>既定閾値＝0.1（改修側にわずかな下駄＝迷ったら温存）。</summary>
        public static bool ShouldDecommission(float retirementUrgency, float modernizationViability)
            => ShouldDecommission(retirementUrgency, modernizationViability, 0.1f);
    }
}
