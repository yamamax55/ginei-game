using UnityEngine;

namespace Ginei
{
    /// <summary>軍事根拠地（軍港・補給基地）の調整係数。ctor で全値をクランプする。</summary>
    public readonly struct NavalBaseParams
    {
        /// <summary>位置価値における戦略的位置の重み。</summary>
        public readonly float locationWeight;
        /// <summary>位置価値における前線近接の重み。</summary>
        public readonly float frontierWeight;
        /// <summary>行動半径の最低保証（維持能力が低くても残る半径）。</summary>
        public readonly float radiusFloor;
        /// <summary>要塞化における防御施設の重み。</summary>
        public readonly float worksWeight;
        /// <summary>要塞化における守備隊の重み。</summary>
        public readonly float garrisonWeight;
        /// <summary>戦略価値で喪失リスクを差し引く強さ。</summary>
        public readonly float lossPenaltyWeight;

        public NavalBaseParams(float locationWeight, float frontierWeight, float radiusFloor,
                               float worksWeight, float garrisonWeight, float lossPenaltyWeight)
        {
            this.locationWeight = Mathf.Max(0f, locationWeight);
            this.frontierWeight = Mathf.Max(0f, frontierWeight);
            this.radiusFloor = Mathf.Clamp01(radiusFloor);
            this.worksWeight = Mathf.Max(0f, worksWeight);
            this.garrisonWeight = Mathf.Max(0f, garrisonWeight);
            this.lossPenaltyWeight = Mathf.Clamp01(lossPenaltyWeight);
        }

        /// <summary>既定＝位置0.6・前線0.4／行動半径下限0.1／防御0.5・守備0.5／喪失ペナルティ0.5。</summary>
        public static NavalBaseParams Default => new NavalBaseParams(0.6f, 0.4f, 0.1f, 0.5f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 軍事根拠地（イゼルローン要塞・ガイエスブルク要塞型の母港・補給拠点）の純ロジック＝
    /// 根拠地の戦略価値を点数化する <b>AI・自動配備の判断材料</b>。
    /// 位置価値（戦略的位置×前線への近さ）→艦隊維持能力（整備×補給）→行動半径（維持×燃料）→
    /// 前進基地の価値、根拠地への依存度、要塞化の防御、喪失の打撃を合成し、
    /// 0..1 の総合戦略価値（前進価値×要塞化−喪失リスク）まで導く。
    /// 盤面（<see cref="StarSystem"/>/<see cref="StrategicFleet"/> 等）には依存せず plain 引数で受ける。
    /// 乱数なし・決定論。実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NavalBaseRules
    {
        /// <summary>
        /// 位置価値（0..1）＝戦略的位置×重み＋前線への近さ×重み（重み合計で正規化）。
        /// 回廊の要・前線直結ほど高い。重み合計0なら0。
        /// </summary>
        public static float BasePositionValue(float strategicLocation, float frontierProximity, NavalBaseParams p)
        {
            float loc = Mathf.Clamp01(strategicLocation);
            float front = Mathf.Clamp01(frontierProximity);
            float weightSum = p.locationWeight + p.frontierWeight;
            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01((p.locationWeight * loc + p.frontierWeight * front) / weightSum);
        }

        /// <summary>既定係数での位置価値（0..1）。</summary>
        public static float BasePositionValue(float strategicLocation, float frontierProximity)
            => BasePositionValue(strategicLocation, frontierProximity, NavalBaseParams.Default);

        /// <summary>
        /// 艦隊維持能力（0..1）＝整備設備×補給備蓄の相乗（幾何平均）。
        /// どちらか欠けると維持できない（修理だけ・補給だけでは艦隊を支えきれない）。
        /// </summary>
        public static float FleetSustainmentCapacity(float repairFacilities, float supplyStockpiles, NavalBaseParams p)
        {
            float repair = Mathf.Clamp01(repairFacilities);
            float supply = Mathf.Clamp01(supplyStockpiles);
            return Mathf.Clamp01(Mathf.Sqrt(repair * supply));
        }

        /// <summary>既定係数での艦隊維持能力（0..1）。</summary>
        public static float FleetSustainmentCapacity(float repairFacilities, float supplyStockpiles)
            => FleetSustainmentCapacity(repairFacilities, supplyStockpiles, NavalBaseParams.Default);

        /// <summary>
        /// 行動半径（0..1）＝維持能力×燃料兵站。維持・燃料が揃うほど遠征できる。
        /// 下限 <see cref="NavalBaseParams.radiusFloor"/> を保証（最低限の哨戒は残る）。
        /// </summary>
        public static float OperationalRadius(float fleetSustainmentCapacity, float fuelLogistics, NavalBaseParams p)
        {
            float sustain = Mathf.Clamp01(fleetSustainmentCapacity);
            float fuel = Mathf.Clamp01(fuelLogistics);
            float radius = sustain * fuel;
            return Mathf.Clamp01(Mathf.Max(p.radiusFloor, radius));
        }

        /// <summary>既定係数での行動半径（0..1）。</summary>
        public static float OperationalRadius(float fleetSustainmentCapacity, float fuelLogistics)
            => OperationalRadius(fleetSustainmentCapacity, fuelLogistics, NavalBaseParams.Default);

        /// <summary>
        /// 前進基地の価値（0..1）＝位置価値×行動半径の相乗（幾何平均）。
        /// 好位置でも届かなければ前進拠点にならず、遠征できても無価値な位置なら意味がない。
        /// </summary>
        public static float ForwardBaseValue(float basePositionValue, float operationalRadius, NavalBaseParams p)
        {
            float pos = Mathf.Clamp01(basePositionValue);
            float radius = Mathf.Clamp01(operationalRadius);
            return Mathf.Clamp01(Mathf.Sqrt(pos * radius));
        }

        /// <summary>既定係数での前進基地の価値（0..1）。</summary>
        public static float ForwardBaseValue(float basePositionValue, float operationalRadius)
            => ForwardBaseValue(basePositionValue, operationalRadius, NavalBaseParams.Default);

        /// <summary>
        /// 根拠地への依存度（0..1）＝(1-行動半径)×基地からの距離。行動半径が狭く・基地から遠いほど、
        /// 補給線が伸びて根拠地への依存が深まる（突出＝補給途絶リスク）。
        /// </summary>
        public static float BaseDependency(float operationalRadius, float distanceFromBase, NavalBaseParams p)
        {
            float radius = Mathf.Clamp01(operationalRadius);
            float dist = Mathf.Clamp01(distanceFromBase);
            return Mathf.Clamp01((1f - radius) * dist);
        }

        /// <summary>既定係数での根拠地依存度（0..1）。</summary>
        public static float BaseDependency(float operationalRadius, float distanceFromBase)
            => BaseDependency(operationalRadius, distanceFromBase, NavalBaseParams.Default);

        /// <summary>
        /// 要塞化の防御（0..1）＝防御施設×重み＋守備隊×重み（重み合計で正規化）。
        /// 要塞砲・装甲などの施設と守備隊の双方で堅さが決まる。重み合計0なら0。
        /// </summary>
        public static float Fortification(float defensiveWorks, float garrisonStrength, NavalBaseParams p)
        {
            float works = Mathf.Clamp01(defensiveWorks);
            float garrison = Mathf.Clamp01(garrisonStrength);
            float weightSum = p.worksWeight + p.garrisonWeight;
            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01((p.worksWeight * works + p.garrisonWeight * garrison) / weightSum);
        }

        /// <summary>既定係数での要塞化の防御（0..1）。</summary>
        public static float Fortification(float defensiveWorks, float garrisonStrength)
            => Fortification(defensiveWorks, garrisonStrength, NavalBaseParams.Default);

        /// <summary>
        /// 根拠地喪失の打撃（0..1）＝艦隊維持能力×根拠地依存度。維持を多く支え・依存が深い根拠地ほど、
        /// 失ったときの戦略的打撃が大きい（ヤンのイゼルローン放棄・ガイエスブルク喪失の重み）。
        /// </summary>
        public static float BaseLossImpact(float fleetSustainmentCapacity, float baseDependency, NavalBaseParams p)
        {
            float sustain = Mathf.Clamp01(fleetSustainmentCapacity);
            float dep = Mathf.Clamp01(baseDependency);
            return Mathf.Clamp01(sustain * dep);
        }

        /// <summary>既定係数での根拠地喪失の打撃（0..1）。</summary>
        public static float BaseLossImpact(float fleetSustainmentCapacity, float baseDependency)
            => BaseLossImpact(fleetSustainmentCapacity, baseDependency, NavalBaseParams.Default);

        /// <summary>
        /// 根拠地の戦略価値（0..1）＝前進基地の価値を要塞化が底上げし（堅い前進拠点ほど価値が高い）、
        /// 喪失リスク（喪失の打撃×ペナルティ重み）を差し引く。AIの拠点防衛・攻略優先度の総合キー。
        /// </summary>
        public static float StrategicBaseValue(float forwardBaseValue, float fortification, float baseLossImpact, NavalBaseParams p)
        {
            float forward = Mathf.Clamp01(forwardBaseValue);
            float fort = Mathf.Clamp01(fortification);
            float loss = Mathf.Clamp01(baseLossImpact);
            // 前進価値を要塞化で 0.5〜1.0 倍に底上げ（堅い拠点ほど価値が残る）。
            float fortified = forward * Mathf.Lerp(0.5f, 1f, fort);
            float value = fortified - p.lossPenaltyWeight * loss;
            return Mathf.Clamp01(value);
        }

        /// <summary>既定係数での根拠地の戦略価値（0..1）。</summary>
        public static float StrategicBaseValue(float forwardBaseValue, float fortification, float baseLossImpact)
            => StrategicBaseValue(forwardBaseValue, fortification, baseLossImpact, NavalBaseParams.Default);
    }
}
