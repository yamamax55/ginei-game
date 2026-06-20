using UnityEngine;

namespace Ginei
{
    /// <summary>地上強襲（軌道からの降下強襲作戦）の調整係数。</summary>
    public readonly struct GroundAssaultParams
    {
        /// <summary>制圧計算で軌道火力支援を展開戦力に換算する重み。</summary>
        public readonly float fireSupportWeight;
        /// <summary>地形の影響の強さ＝地形遮蔽×防御側有利に掛ける係数。</summary>
        public readonly float terrainSeverity;
        /// <summary>増援が突進の勢いを底上げする度合い。</summary>
        public readonly float reinforcementGain;
        /// <summary>地形が強襲の消耗を増す度合い。</summary>
        public readonly float attritionTerrainGain;

        public GroundAssaultParams(float fireSupportWeight, float terrainSeverity,
                                   float reinforcementGain, float attritionTerrainGain)
        {
            this.fireSupportWeight = Mathf.Max(0f, fireSupportWeight);
            this.terrainSeverity = Mathf.Clamp(terrainSeverity, 0f, 4f);
            this.reinforcementGain = Mathf.Clamp(reinforcementGain, 0f, 4f);
            this.attritionTerrainGain = Mathf.Clamp(attritionTerrainGain, 0f, 4f);
        }

        /// <summary>既定＝火力支援重み1.0・地形強度1.0・増援寄与0.5・地形消耗0.5。</summary>
        public static GroundAssaultParams Default => new GroundAssaultParams(1f, 1f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 地上強襲（軌道から惑星地表への降下強襲作戦）の純ロジック。
    /// 軌道上の艦隊が惑星地表へ降下部隊を投入し、軌道砲撃の火力支援を受けつつ地表防御を制圧、
    /// 地形の影響を踏まえて橋頭堡を確保し、増援を流し込んで突進、敵抵抗と地形で消耗しながら地上を支配する。
    /// 段階＝展開戦力→火力支援→防御制圧→地形の影響→橋頭堡確保→突進の勢い→強襲の消耗→地上支配の獲得。
    /// 占領そのものの解決（侵略値の蓄積→閾値）は <see cref="PlanetSiegeRules"/>、地上消耗の二者解決は
    /// <see cref="GroundInvasionRules"/> が担い、本ルールは降下強襲の各段階係数のみを供給する（重複実装しない）。
    /// 決定論・乱数なし。純ロジック（非 MonoBehaviour・test-first）。実効値パターン（入力非破壊）。
    /// </summary>
    public static class GroundAssaultRules
    {
        /// <summary>
        /// 展開戦力（≧0）＝降下兵×降下精度。精度が低いと降下が散らばり実効戦力が痩せる。
        /// </summary>
        public static float LandingForce(float troopsDropped, float dropAccuracy, GroundAssaultParams p)
            => Mathf.Max(0f, troopsDropped) * Mathf.Clamp01(dropAccuracy);

        public static float LandingForce(float troopsDropped, float dropAccuracy)
            => LandingForce(troopsDropped, dropAccuracy, GroundAssaultParams.Default);

        /// <summary>
        /// 軌道火力支援（≧0）＝利用可能な軌道砲撃×照準精度。照準が甘いと地表へ届く有効火力が減る。
        /// </summary>
        public static float OrbitalFireSupport(float bombardmentAvailable, float targetingAccuracy, GroundAssaultParams p)
            => Mathf.Max(0f, bombardmentAvailable) * Mathf.Clamp01(targetingAccuracy);

        public static float OrbitalFireSupport(float bombardmentAvailable, float targetingAccuracy)
            => OrbitalFireSupport(bombardmentAvailable, targetingAccuracy, GroundAssaultParams.Default);

        /// <summary>
        /// 地表防御の制圧（0..1）＝(展開＋重み×火力)/(展開＋重み×火力＋敵防御)。攻撃側の総圧力が敵防御を上回るほど1へ近づく。
        /// 攻防いずれもゼロなら0（制圧の根拠なし）。ゼロ割安全。
        /// </summary>
        public static float DefenseSuppression(float landingForce, float orbitalFireSupport, float enemyFortification, GroundAssaultParams p)
        {
            float attack = Mathf.Max(0f, landingForce) + p.fireSupportWeight * Mathf.Max(0f, orbitalFireSupport);
            float denom = attack + Mathf.Max(0f, enemyFortification);
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(attack / denom);
        }

        public static float DefenseSuppression(float landingForce, float orbitalFireSupport, float enemyFortification)
            => DefenseSuppression(landingForce, orbitalFireSupport, enemyFortification, GroundAssaultParams.Default);

        /// <summary>
        /// 地形の影響（0..1）＝地形遮蔽×防御側有利×地形強度。遮蔽の濃い地形で防御側が有利なほど攻撃側の不利が増す。
        /// </summary>
        public static float TerrainEffect(float terrainCover, float defenderAdvantage, GroundAssaultParams p)
            => Mathf.Clamp01(Mathf.Clamp01(terrainCover) * Mathf.Clamp01(defenderAdvantage) * p.terrainSeverity);

        public static float TerrainEffect(float terrainCover, float defenderAdvantage)
            => TerrainEffect(terrainCover, defenderAdvantage, GroundAssaultParams.Default);

        /// <summary>
        /// 橋頭堡の確保（0..1）＝制圧×(1-地形の影響)。地表防御を抑えても地形が不利なら橋頭堡は痩せる。
        /// </summary>
        public static float BeachheadSecure(float defenseSuppression, float terrainEffect, GroundAssaultParams p)
            => Mathf.Clamp01(Mathf.Clamp01(defenseSuppression) * (1f - Mathf.Clamp01(terrainEffect)));

        public static float BeachheadSecure(float defenseSuppression, float terrainEffect)
            => BeachheadSecure(defenseSuppression, terrainEffect, GroundAssaultParams.Default);

        /// <summary>
        /// 突進の勢い（0..1）＝橋頭堡×(1+増援寄与×増援流入)。固めた橋頭堡へ増援が流れるほど勢いが乗る。
        /// </summary>
        public static float AssaultMomentum(float beachheadSecure, float reinforcementFlow, GroundAssaultParams p)
            => Mathf.Clamp01(Mathf.Clamp01(beachheadSecure) * (1f + p.reinforcementGain * Mathf.Clamp01(reinforcementFlow)));

        public static float AssaultMomentum(float beachheadSecure, float reinforcementFlow)
            => AssaultMomentum(beachheadSecure, reinforcementFlow, GroundAssaultParams.Default);

        /// <summary>
        /// 強襲の消耗（0..1）＝敵抵抗×(1+地形消耗×地形の影響)。粘る敵と不利な地形が攻撃側を削る。
        /// </summary>
        public static float AssaultAttrition(float enemyResistance, float terrainEffect, GroundAssaultParams p)
            => Mathf.Clamp01(Mathf.Clamp01(enemyResistance) * (1f + p.attritionTerrainGain * Mathf.Clamp01(terrainEffect)));

        public static float AssaultAttrition(float enemyResistance, float terrainEffect)
            => AssaultAttrition(enemyResistance, terrainEffect, GroundAssaultParams.Default);

        /// <summary>
        /// 地上支配の獲得（-1..1）＝突進の勢い−強襲の消耗。正なら地上を押し進め、負なら押し戻されて損耗が勝る。
        /// </summary>
        public static float GroundControlGained(float assaultMomentum, float assaultAttrition, GroundAssaultParams p)
            => Mathf.Clamp(Mathf.Clamp01(assaultMomentum) - Mathf.Clamp01(assaultAttrition), -1f, 1f);

        public static float GroundControlGained(float assaultMomentum, float assaultAttrition)
            => GroundControlGained(assaultMomentum, assaultAttrition, GroundAssaultParams.Default);
    }
}
