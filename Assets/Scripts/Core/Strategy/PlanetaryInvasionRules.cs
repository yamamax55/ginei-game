using UnityEngine;

namespace Ginei
{
    /// <summary>惑星侵攻（惑星規模の戦略的制圧作戦）の調整係数。</summary>
    public readonly struct PlanetaryInvasionParams
    {
        /// <summary>軌道からの補給がテンポを底上げする下限（補給ゼロでもこの割合は拡大が効く）。</summary>
        public readonly float supplyFloor;
        /// <summary>住民抵抗の強さ＝敵意×士気に掛ける係数。</summary>
        public readonly float resistanceSeverity;
        /// <summary>住民抵抗が侵攻テンポを削る度合い。</summary>
        public readonly float resistanceDrag;

        public PlanetaryInvasionParams(float supplyFloor, float resistanceSeverity, float resistanceDrag)
        {
            this.supplyFloor = Mathf.Clamp01(supplyFloor);
            this.resistanceSeverity = Mathf.Clamp(resistanceSeverity, 0f, 4f);
            this.resistanceDrag = Mathf.Clamp(resistanceDrag, 0f, 4f);
        }

        /// <summary>既定＝補給下限0.5・抵抗強度1.0・抵抗ドラッグ1.0。</summary>
        public static PlanetaryInvasionParams Default => new PlanetaryInvasionParams(0.5f, 1f, 1f);
    }

    /// <summary>
    /// 惑星侵攻（惑星全体を制圧する戦略的侵攻作戦）の純ロジック。
    /// 段階＝軌道制圧→上陸作戦の成立→地上戦力比→占領地の拡大／住民の抵抗／軌道からの補給→侵攻のテンポ→制圧の完了。
    /// 宇宙艦隊で軌道を制圧し、その下で上陸を成立させ、惑星防衛軍との戦力比で占領地を広げる一方、住民の抵抗が
    /// テンポを削り、軌道からの補給が拡大を支える。拡大が抵抗を十分上回れば惑星を制圧できる。
    /// 1tick の占領解決（侵略値の蓄積→閾値フリップ）は <see cref="PlanetSiegeRules"/>、軌道→地表の降下強襲戦術は
    /// <see cref="GroundAssaultRules"/> が担い、本ルールは惑星規模の戦略的侵攻の各段階係数のみを供給する（重複実装しない）。
    /// 決定論・乱数なし。純ロジック（非 MonoBehaviour・test-first）。実効値パターン（入力非破壊）。
    /// </summary>
    public static class PlanetaryInvasionRules
    {
        /// <summary>制圧完了とみなす既定閾値（占領地の拡大−住民抵抗 がこれ以上で制圧）。</summary>
        public const float DefaultSubjugationThreshold = 0.4f;

        /// <summary>
        /// 軌道制圧（0..1）＝宇宙艦隊/(艦隊＋惑星防衛)。艦隊が惑星防衛火力を上回るほど1へ近づく。
        /// 双方ゼロなら0（制圧の根拠なし）。ゼロ割安全。
        /// </summary>
        public static float OrbitalSupremacy(float spaceFleetStrength, float planetaryDefense, PlanetaryInvasionParams p)
        {
            float fleet = Mathf.Max(0f, spaceFleetStrength);
            float def = Mathf.Max(0f, planetaryDefense);
            float denom = fleet + def;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(fleet / denom);
        }

        public static float OrbitalSupremacy(float spaceFleetStrength, float planetaryDefense)
            => OrbitalSupremacy(spaceFleetStrength, planetaryDefense, PlanetaryInvasionParams.Default);

        /// <summary>
        /// 上陸作戦の成立（0..1）＝軌道制圧×上陸戦力の準備度。軌道を制してこそ上陸が成り立つ。
        /// </summary>
        public static float LandingViability(float orbitalSupremacy, float landingForce, PlanetaryInvasionParams p)
            => Mathf.Clamp01(Mathf.Clamp01(orbitalSupremacy) * Mathf.Clamp01(landingForce));

        public static float LandingViability(float orbitalSupremacy, float landingForce)
            => LandingViability(orbitalSupremacy, landingForce, PlanetaryInvasionParams.Default);

        /// <summary>
        /// 地上戦力比（0..1）＝侵攻地上軍/(侵攻＋防衛)。侵攻側が惑星防衛軍を上回るほど1へ近づく。
        /// 双方ゼロなら0。ゼロ割安全。
        /// </summary>
        public static float ForceRatio(float invaderGroundForce, float defenderGroundForce, PlanetaryInvasionParams p)
        {
            float inv = Mathf.Max(0f, invaderGroundForce);
            float def = Mathf.Max(0f, defenderGroundForce);
            float denom = inv + def;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(inv / denom);
        }

        public static float ForceRatio(float invaderGroundForce, float defenderGroundForce)
            => ForceRatio(invaderGroundForce, defenderGroundForce, PlanetaryInvasionParams.Default);

        /// <summary>
        /// 占領地の拡大（0..1）＝地上戦力比×上陸成立。戦力で優位でも上陸が成り立たねば広がらない。
        /// </summary>
        public static float TerritoryExpansion(float forceRatio, float landingViability, PlanetaryInvasionParams p)
            => Mathf.Clamp01(Mathf.Clamp01(forceRatio) * Mathf.Clamp01(landingViability));

        public static float TerritoryExpansion(float forceRatio, float landingViability)
            => TerritoryExpansion(forceRatio, landingViability, PlanetaryInvasionParams.Default);

        /// <summary>
        /// 住民抵抗（0..1）＝占領への敵意×防衛側士気×抵抗強度。敵意と士気が高いほど占領が荒れる。
        /// </summary>
        public static float PopularResistance(float occupierHostility, float defenderMorale, PlanetaryInvasionParams p)
            => Mathf.Clamp01(Mathf.Clamp01(occupierHostility) * Mathf.Clamp01(defenderMorale) * p.resistanceSeverity);

        public static float PopularResistance(float occupierHostility, float defenderMorale)
            => PopularResistance(occupierHostility, defenderMorale, PlanetaryInvasionParams.Default);

        /// <summary>
        /// 軌道からの補給（0..1）＝軌道制圧×兵站能力。軌道を握ってこそ地表へ補給が降りる。
        /// </summary>
        public static float SupplyFromOrbit(float orbitalSupremacy, float logisticsCapacity, PlanetaryInvasionParams p)
            => Mathf.Clamp01(Mathf.Clamp01(orbitalSupremacy) * Mathf.Clamp01(logisticsCapacity));

        public static float SupplyFromOrbit(float orbitalSupremacy, float logisticsCapacity)
            => SupplyFromOrbit(orbitalSupremacy, logisticsCapacity, PlanetaryInvasionParams.Default);

        /// <summary>
        /// 侵攻のテンポ（-1..1）＝占領地の拡大×補給の効き−抵抗ドラッグ×住民抵抗。
        /// 補給は下限 supplyFloor から満額1.0へ拡大を底上げし、住民抵抗がテンポを差し引く。
        /// 正なら侵攻が進み、負なら抵抗と補給難で押し戻される。
        /// </summary>
        public static float InvasionTempo(float territoryExpansion, float supplyFromOrbit, float popularResistance, PlanetaryInvasionParams p)
        {
            float expansion = Mathf.Clamp01(territoryExpansion);
            float supplyFactor = Mathf.Lerp(p.supplyFloor, 1f, Mathf.Clamp01(supplyFromOrbit));
            float drag = p.resistanceDrag * Mathf.Clamp01(popularResistance);
            return Mathf.Clamp(expansion * supplyFactor - drag, -1f, 1f);
        }

        public static float InvasionTempo(float territoryExpansion, float supplyFromOrbit, float popularResistance)
            => InvasionTempo(territoryExpansion, supplyFromOrbit, popularResistance, PlanetaryInvasionParams.Default);

        /// <summary>
        /// 惑星を制圧できたか＝占領地の拡大−住民抵抗 が閾値以上。抵抗を十分上回って広げてこそ制圧。
        /// </summary>
        public static bool IsPlanetSubjugated(float territoryExpansion, float popularResistance, float threshold)
            => Mathf.Clamp01(territoryExpansion) - Mathf.Clamp01(popularResistance) >= Mathf.Clamp01(threshold);

        public static bool IsPlanetSubjugated(float territoryExpansion, float popularResistance)
            => IsPlanetSubjugated(territoryExpansion, popularResistance, DefaultSubjugationThreshold);
    }
}
