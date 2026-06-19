using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 市街戦パラメータ（都市での近接戦闘）。readonly struct・トップレベル。
    /// ctor で全値をクランプ。Default で標準値。
    /// </summary>
    public readonly struct UrbanWarfareParams
    {
        /// <summary>建物密度が防御優位へ効く重み。</summary>
        public readonly float densityWeight;
        /// <summary>準備陣地（塹壕・トーチカ）が防御優位へ効く重み。</summary>
        public readonly float preparedWeight;
        /// <summary>攻者三倍の原則の係数（防御優位1で攻撃コストがこの倍率まで膨らむ）。</summary>
        public readonly float attackerCostScale;
        /// <summary>近接戦の混沌の最大値（指揮統制の崩壊度の上限0..1）。</summary>
        public readonly float chaosMax;
        /// <summary>民間人による攻撃制約の最大値（交戦規則による掣肘の上限0..1）。</summary>
        public readonly float impedanceMax;
        /// <summary>瓦礫が機動を削る重み（破壊度が機動へ効く感度）。</summary>
        public readonly float rubbleWeight;
        /// <summary>ビル制圧速度の基準スケール。</summary>
        public readonly float clearingScale;
        /// <summary>立体戦（高層×地下）の複雑さの重み。</summary>
        public readonly float verticalWeight;

        public UrbanWarfareParams(
            float densityWeight,
            float preparedWeight,
            float attackerCostScale,
            float chaosMax,
            float impedanceMax,
            float rubbleWeight,
            float clearingScale,
            float verticalWeight)
        {
            this.densityWeight = Mathf.Clamp01(densityWeight);
            this.preparedWeight = Mathf.Clamp01(preparedWeight);
            this.attackerCostScale = Mathf.Clamp(attackerCostScale, 0f, 4f);
            this.chaosMax = Mathf.Clamp01(chaosMax);
            this.impedanceMax = Mathf.Clamp01(impedanceMax);
            this.rubbleWeight = Mathf.Clamp01(rubbleWeight);
            this.clearingScale = Mathf.Clamp(clearingScale, 0.01f, 4f);
            this.verticalWeight = Mathf.Clamp01(verticalWeight);
        }

        public static UrbanWarfareParams Default => new UrbanWarfareParams(
            densityWeight: 0.6f,
            preparedWeight: 0.4f,
            attackerCostScale: 2f,
            chaosMax: 1f,
            impedanceMax: 0.8f,
            rubbleWeight: 1f,
            clearingScale: 1f,
            verticalWeight: 1f);
    }

    /// <summary>
    /// 市街戦ロジック＝惑星地上戦の「都市での近接戦闘」の純数値モデル。
    /// 建物による遮蔽と防御優位、近接戦の混沌、攻者三倍の原則による攻撃側損耗、
    /// 民間人の存在による掣肘、瓦礫による機動阻害、ビル単位の争奪、地下・高層の立体戦を扱う。
    ///
    /// 分担：惑星全体の攻城/制空権は <see cref="PlanetSiegeRules"/>、地上侵攻全般は
    /// <see cref="GroundInvasionRules"/>、地形の係数は <see cref="TerrainRules"/>、
    /// ゲリラ戦は <see cref="GuerrillaDoctrineRules"/> が担う。本ルールはそれらとは別＝
    /// 都市内部の戦術的な「市街戦」の係数モデル（盤面非依存・plain 引数・実効値パターン）。
    /// </summary>
    public static class UrbanWarfareRules
    {
        /// <summary>
        /// 防御側優位 0..1。建物密度×重み＋準備陣地×重みの加重和。
        /// 密集した市街と築城された陣地ほど守りやすい。
        /// </summary>
        public static float DefenderAdvantage(float buildingDensity, float preparedPositions, UrbanWarfareParams p)
        {
            float density = Mathf.Clamp01(buildingDensity);
            float prepared = Mathf.Clamp01(preparedPositions);
            return Mathf.Clamp01(density * p.densityWeight + prepared * p.preparedWeight);
        }

        public static float DefenderAdvantage(float buildingDensity, float preparedPositions)
            => DefenderAdvantage(buildingDensity, preparedPositions, UrbanWarfareParams.Default);

        /// <summary>
        /// 攻撃側コスト倍率（1.0〜1+attackerCostScale）。攻者三倍の原則＝
        /// 防御優位が高いほど攻撃側は多くの戦力を費やす。defenderAdvantage(0..1)に比例。
        /// </summary>
        public static float AttackerCostMultiplier(float defenderAdvantage, UrbanWarfareParams p)
        {
            float adv = Mathf.Clamp01(defenderAdvantage);
            return 1f + adv * p.attackerCostScale;
        }

        public static float AttackerCostMultiplier(float defenderAdvantage)
            => AttackerCostMultiplier(defenderAdvantage, UrbanWarfareParams.Default);

        /// <summary>
        /// 近接戦の混沌 0..1。交戦密度×(1-視界)＝至近で視界が利かないほど指揮統制が崩れる。
        /// </summary>
        public static float CloseQuartersChaos(float engagementDensity, float visibility, UrbanWarfareParams p)
        {
            float density = Mathf.Clamp01(engagementDensity);
            float vis = Mathf.Clamp01(visibility);
            return Mathf.Clamp01(density * (1f - vis) * p.chaosMax);
        }

        public static float CloseQuartersChaos(float engagementDensity, float visibility)
            => CloseQuartersChaos(engagementDensity, visibility, UrbanWarfareParams.Default);

        /// <summary>
        /// 民間人による攻撃の制約 0..1。民間人の存在×交戦規則の厳しさ＝
        /// 巻き添えを避ける交戦規則ほど攻撃が掣肘される。
        /// </summary>
        public static float CivilianImpedance(float civilianPresence, float ruleOfEngagement, UrbanWarfareParams p)
        {
            float civ = Mathf.Clamp01(civilianPresence);
            float roe = Mathf.Clamp01(ruleOfEngagement);
            return Mathf.Clamp01(civ * roe * p.impedanceMax);
        }

        public static float CivilianImpedance(float civilianPresence, float ruleOfEngagement)
            => CivilianImpedance(civilianPresence, ruleOfEngagement, UrbanWarfareParams.Default);

        /// <summary>
        /// 瓦礫下の機動 0..1。(1-破壊度)×(1-車両依存)＝瓦礫が多いほど、車両に頼るほど動けない。
        /// 1.0=瓦礫無し・徒歩で自在、0=瓦礫だらけで車両頼みの足止め。
        /// </summary>
        public static float RubbleMobility(float destruction, float vehicleReliance, UrbanWarfareParams p)
        {
            float destroyed = Mathf.Clamp01(destruction);
            float reliance = Mathf.Clamp01(vehicleReliance);
            // 破壊度を重みで圧縮した実効瓦礫量で機動を削る。
            float rubble = Mathf.Clamp01(destroyed * p.rubbleWeight);
            return Mathf.Clamp01((1f - rubble) * (1f - reliance));
        }

        public static float RubbleMobility(float destruction, float vehicleReliance)
            => RubbleMobility(destruction, vehicleReliance, UrbanWarfareParams.Default);

        /// <summary>
        /// ビル制圧速度 0..1。歩兵戦力 ÷ 攻撃コスト倍率＝コストが膨らむほど制圧は遅い。
        /// clearingScale で基準速度を調整。攻撃コストが歩兵戦力を食い潰すと0へ。
        /// </summary>
        public static float BuildingClearing(float infantryStrength, float attackerCostMultiplier, UrbanWarfareParams p)
        {
            float infantry = Mathf.Clamp01(infantryStrength);
            float cost = Mathf.Max(1f, attackerCostMultiplier);
            return Mathf.Clamp01(infantry * p.clearingScale / cost);
        }

        public static float BuildingClearing(float infantryStrength, float attackerCostMultiplier)
            => BuildingClearing(infantryStrength, attackerCostMultiplier, UrbanWarfareParams.Default);

        /// <summary>
        /// 立体戦の複雑さ 0..1。高層×地下＝高低両方向に広がるほど制圧が難しい。
        /// 高層ビル群と地下構造の積で効く（片方だけなら緩い）。
        /// </summary>
        public static float VerticalComplexity(float highRise, float subterranean, UrbanWarfareParams p)
        {
            float high = Mathf.Clamp01(highRise);
            float sub = Mathf.Clamp01(subterranean);
            return Mathf.Clamp01(high * sub * p.verticalWeight);
        }

        public static float VerticalComplexity(float highRise, float subterranean)
            => VerticalComplexity(highRise, subterranean, UrbanWarfareParams.Default);

        /// <summary>
        /// 市街戦の進捗 -1..1。制圧速度 − 民間人制約 − 過剰な攻撃コスト＝
        /// ビルを取れば進み、交戦規則と損耗で停滞・後退する。正＝前進、負＝損耗で押し戻される。
        /// </summary>
        public static float UrbanCombatProgress(
            float buildingClearing,
            float civilianImpedance,
            float attackerCostMultiplier,
            UrbanWarfareParams p)
        {
            float clearing = Mathf.Clamp01(buildingClearing);
            float impedance = Mathf.Clamp01(civilianImpedance);
            float cost = Mathf.Max(1f, attackerCostMultiplier);
            // コスト超過ぶん（1超過＝攻者三倍の負担）を 0..1 の損耗圧へ。
            float costDrag = Mathf.Clamp01((cost - 1f) / 3f);
            return Mathf.Clamp(clearing - impedance - costDrag, -1f, 1f);
        }

        public static float UrbanCombatProgress(
            float buildingClearing,
            float civilianImpedance,
            float attackerCostMultiplier)
            => UrbanCombatProgress(buildingClearing, civilianImpedance, attackerCostMultiplier, UrbanWarfareParams.Default);
    }
}
