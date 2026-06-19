using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 橋頭堡（敵勢力圏に確保した最初の足場＝回廊出口の星系等）の調整値。
    /// 確保の競り合いの効き・脆さの基準規模・反撃を凌ぐ地形/規模の基準・敵の封じ込めに抗う効き・侵攻起点としての価値の基準規模。
    /// </summary>
    public readonly struct BridgeheadParams
    {
        /// <summary>確保時の防御側戦力の効き（大きいほど守りが堅く確保が難しい）。</summary>
        public readonly float establishContestScale;
        /// <summary>脆さの基準規模（橋頭堡の規模＋後続流入がこの値で脆さ0.5）。</summary>
        public readonly float fragilityRef;
        /// <summary>後続流入が脆さ軽減に寄与する重み。</summary>
        public readonly float fragilityWeight;
        /// <summary>反撃を凌ぐ度合いの基準規模（橋頭堡実効戦力がこの値で抵抗0.5）。</summary>
        public readonly float resistRef;
        /// <summary>防御地形が反撃抵抗を底上げする効き。</summary>
        public readonly float terrainDefenseScale;
        /// <summary>敵の封じ込めの効き（大きいほど押し広げにくい）。</summary>
        public readonly float containmentResistScale;
        /// <summary>侵攻起点としての価値の基準規模（橋頭堡規模がこの値で規模係数0.5）。</summary>
        public readonly float valueRef;

        public BridgeheadParams(float establishContestScale, float fragilityRef, float fragilityWeight,
            float resistRef, float terrainDefenseScale, float containmentResistScale, float valueRef)
        {
            this.establishContestScale = Mathf.Max(0f, establishContestScale);
            this.fragilityRef = Mathf.Max(0.0001f, fragilityRef);
            this.fragilityWeight = Mathf.Max(0f, fragilityWeight);
            this.resistRef = Mathf.Max(0.0001f, resistRef);
            this.terrainDefenseScale = Mathf.Max(0f, terrainDefenseScale);
            this.containmentResistScale = Mathf.Max(0f, containmentResistScale);
            this.valueRef = Mathf.Max(0.0001f, valueRef);
        }

        /// <summary>既定：確保競り合い1.0・脆さ基準100・後続重み1.0・抵抗基準100・地形効き1.0・封じ込め1.0・価値基準100。</summary>
        public static BridgeheadParams Default
            => new BridgeheadParams(DefaultEstablishContestScale, DefaultFragilityRef, DefaultFragilityWeight,
                DefaultResistRef, DefaultTerrainDefenseScale, DefaultContainmentResistScale, DefaultValueRef);

        public const float DefaultEstablishContestScale = 1.0f;
        public const float DefaultFragilityRef = 100f;
        public const float DefaultFragilityWeight = 1.0f;
        public const float DefaultResistRef = 100f;
        public const float DefaultTerrainDefenseScale = 1.0f;
        public const float DefaultContainmentResistScale = 1.0f;
        public const float DefaultValueRef = 100f;
    }

    /// <summary>
    /// 橋頭堡（Bridgehead＝敵地に確保した拠点）の純ロジック。
    /// 敵勢力圏に最初の足場を確保し、そこへ後続戦力を流し込んで拡張する。確保直後は脆く、敵の反撃で押し戻されうる。
    /// 拡張できれば侵攻の起点になる。
    ///
    /// <b>分担</b>：
    /// - <see cref="ColonizationRules"/>（入植・既存）とは別＝こちらは<b>敵地での軍事拠点確保</b>（戦力を流し込む足場）。
    /// - <see cref="PlanetSiegeRules"/>（惑星攻城）とは別＝こちらは攻城そのものでなく<b>侵攻の起点</b>（橋頭堡から戦力を展開する）。
    /// 盤面非依存の plain 引数のみ（艦隊・シーンに触れない）。test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class BridgeheadRules
    {
        /// <summary>既定パラメータで橋頭堡の確保確率を返す。</summary>
        public static float EstablishmentChance(float assaultStrength, float defenderStrength)
            => EstablishmentChance(assaultStrength, defenderStrength, BridgeheadParams.Default);

        /// <summary>
        /// 敵地に橋頭堡を確保できる確率(0..1)。揚陸戦力 vs 守備戦力の競り合い。
        /// 守備不在(0)で確実(1)・拮抗で0.5・守備優勢ほど低い。
        /// </summary>
        public static float EstablishmentChance(float assaultStrength, float defenderStrength, BridgeheadParams p)
        {
            float assault = Mathf.Max(0f, assaultStrength);
            float defender = Mathf.Max(0f, defenderStrength) * p.establishContestScale;
            float total = assault + defender;
            if (total <= 0f) return 0f; // 揚陸戦力ゼロ＝足場を築けない
            return Mathf.Clamp01(assault / total);
        }

        /// <summary>既定パラメータで確保直後の脆さを返す。</summary>
        public static float InitialFragility(float bridgeheadSize, float reinforcementRate)
            => InitialFragility(bridgeheadSize, reinforcementRate, BridgeheadParams.Default);

        /// <summary>
        /// 確保直後の脆さ(0..1)。規模が小さく後続が遅いほど脆い（→1）、規模が大きく後続が速いほど堅い（→0）。
        /// </summary>
        public static float InitialFragility(float bridgeheadSize, float reinforcementRate, BridgeheadParams p)
        {
            float size = Mathf.Max(0f, bridgeheadSize);
            float reinforce = Mathf.Max(0f, reinforcementRate) * p.fragilityWeight;
            float robustness = (size + reinforce) / p.fragilityRef; // 0..∞（堅牢さ）
            return Mathf.Clamp01(1f / (1f + robustness));
        }

        /// <summary>既定パラメータで橋頭堡への戦力流入速度を返す。</summary>
        public static float BuildupRate(float reinforcementRate, float throughputCapacity)
            => BuildupRate(reinforcementRate, throughputCapacity, BridgeheadParams.Default);

        /// <summary>
        /// 橋頭堡へ戦力を流し込む速さ。後続流入量を回廊の搬送容量で律速する（容量を超えては送れない＝兵站の喉首）。
        /// </summary>
        public static float BuildupRate(float reinforcementRate, float throughputCapacity, BridgeheadParams p)
        {
            float reinforce = Mathf.Max(0f, reinforcementRate);
            float capacity = Mathf.Max(0f, throughputCapacity);
            return Mathf.Min(reinforce, capacity);
        }

        /// <summary>既定パラメータで反撃抵抗を返す。</summary>
        public static float CounterattackResistance(float bridgeheadStrength, float defensiveTerrain)
            => CounterattackResistance(bridgeheadStrength, defensiveTerrain, BridgeheadParams.Default);

        /// <summary>
        /// 敵の反撃を凌ぐ度合い(0..1)。橋頭堡の戦力に防御地形の補正を掛けた実効戦力で決まる。
        /// 戦力ゼロ＝0、基準規模で0.5、地形が堅いほど高い。
        /// </summary>
        public static float CounterattackResistance(float bridgeheadStrength, float defensiveTerrain, BridgeheadParams p)
        {
            float strength = Mathf.Max(0f, bridgeheadStrength);
            float terrain = Mathf.Max(0f, defensiveTerrain);
            float effective = strength * (1f + terrain * p.terrainDefenseScale);
            return Mathf.Clamp01(effective / (effective + p.resistRef));
        }

        /// <summary>既定パラメータで橋頭堡の拡張度を返す。</summary>
        public static float Expansion(float bridgeheadStrength, float enemyContainment)
            => Expansion(bridgeheadStrength, enemyContainment, BridgeheadParams.Default);

        /// <summary>
        /// 橋頭堡を押し広げる度合い(0..1)。橋頭堡戦力 vs 敵の封じ込め。
        /// 封じ込め不在で最大(1)・拮抗で0.5・封じ込めが強いほど広げられない。
        /// </summary>
        public static float Expansion(float bridgeheadStrength, float enemyContainment, BridgeheadParams p)
        {
            float strength = Mathf.Max(0f, bridgeheadStrength);
            float containment = Mathf.Max(0f, enemyContainment) * p.containmentResistScale;
            float total = strength + containment;
            if (total <= 0f) return 0f; // 戦力も封じ込めも無い＝広がらない
            return Mathf.Clamp01(strength / total);
        }

        /// <summary>既定パラメータで崩壊リスクを返す。</summary>
        public static float CollapseRisk(float initialFragility, float enemyCounterattack)
            => CollapseRisk(initialFragility, enemyCounterattack, BridgeheadParams.Default);

        /// <summary>
        /// 押し戻されて橋頭堡を失うリスク(0..1)。確保直後の脆さ×敵反撃の強さ。
        /// 脆さゼロor反撃ゼロで0、両方高いほど崩壊しやすい。
        /// </summary>
        public static float CollapseRisk(float initialFragility, float enemyCounterattack, BridgeheadParams p)
        {
            float fragility = Mathf.Clamp01(initialFragility);
            float counterattack = Mathf.Clamp01(enemyCounterattack);
            return Mathf.Clamp01(fragility * counterattack);
        }

        /// <summary>既定パラメータで侵攻起点としての価値を返す。</summary>
        public static float BeachheadValue(float bridgeheadSize, float strategicPosition)
            => BeachheadValue(bridgeheadSize, strategicPosition, BridgeheadParams.Default);

        /// <summary>
        /// 侵攻起点としての価値(0..1)。橋頭堡の規模係数×戦略的位置(0..1)。
        /// 規模が大きく要衝に築くほど侵攻の足場として価値が高い。
        /// </summary>
        public static float BeachheadValue(float bridgeheadSize, float strategicPosition, BridgeheadParams p)
        {
            float size = Mathf.Max(0f, bridgeheadSize);
            float position = Mathf.Clamp01(strategicPosition);
            float sizeFactor = size / (size + p.valueRef); // 0..1
            return Mathf.Clamp01(sizeFactor * position);
        }

        /// <summary>
        /// 橋頭堡が安定したか（橋頭堡戦力 vs 敵反撃の安全度が閾値 threshold 以上で true）。
        /// 反撃に対して十分な戦力を保てれば足場が固まり、侵攻の起点として成立する。
        /// </summary>
        public static bool IsBridgeheadSecure(float bridgeheadStrength, float enemyCounterattack, float threshold)
        {
            float strength = Mathf.Max(0f, bridgeheadStrength);
            float counterattack = Mathf.Max(0f, enemyCounterattack);
            float total = strength + counterattack;
            float security = total <= 0f ? 0f : Mathf.Clamp01(strength / total);
            return security >= threshold;
        }
    }
}
