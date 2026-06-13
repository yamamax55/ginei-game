using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 夜戦パラメータ（視界制限戦闘の係数）。基準値非破壊・全項目 Clamp。
    /// </summary>
    public readonly struct NightBattleParams
    {
        /// <summary>暗さが視界へ効く重み。</summary>
        public readonly float darknessWeight;
        /// <summary>索敵装備が闇を補う重み。</summary>
        public readonly float sensorWeight;
        /// <summary>視界低下→命中低下の係数。</summary>
        public readonly float accuracyImpact;
        /// <summary>混戦×低視界→同士討ちの基礎係数。</summary>
        public readonly float friendlyFireBase;
        /// <summary>視界低下→指揮混乱の係数。</summary>
        public readonly float confusionImpact;
        /// <summary>奇襲有利の係数。</summary>
        public readonly float surpriseImpact;
        /// <summary>練度が闇の不利を補う係数。</summary>
        public readonly float veterancyImpact;
        /// <summary>視界低下→交戦距離縮小の係数。</summary>
        public readonly float rangeShrinkImpact;
        /// <summary>夜戦混乱と判定する視界低下のしきい値。</summary>
        public readonly float chaosThreshold;
        /// <summary>指揮混乱の基準艦隊規模（これで割って 0..1 正規化）。</summary>
        public readonly float maxFleetSize;

        public NightBattleParams(
            float darknessWeight,
            float sensorWeight,
            float accuracyImpact,
            float friendlyFireBase,
            float confusionImpact,
            float surpriseImpact,
            float veterancyImpact,
            float rangeShrinkImpact,
            float chaosThreshold,
            float maxFleetSize)
        {
            this.darknessWeight = Mathf.Clamp01(darknessWeight);
            this.sensorWeight = Mathf.Clamp01(sensorWeight);
            this.accuracyImpact = Mathf.Clamp01(accuracyImpact);
            this.friendlyFireBase = Mathf.Clamp01(friendlyFireBase);
            this.confusionImpact = Mathf.Clamp01(confusionImpact);
            this.surpriseImpact = Mathf.Clamp01(surpriseImpact);
            this.veterancyImpact = Mathf.Clamp01(veterancyImpact);
            this.rangeShrinkImpact = Mathf.Clamp01(rangeShrinkImpact);
            this.chaosThreshold = Mathf.Clamp01(chaosThreshold);
            this.maxFleetSize = Mathf.Max(1f, maxFleetSize);
        }

        public static NightBattleParams Default =>
            new NightBattleParams(
                darknessWeight: 0.7f,
                sensorWeight: 0.6f,
                accuracyImpact: 0.8f,
                friendlyFireBase: 0.5f,
                confusionImpact: 0.6f,
                surpriseImpact: 0.9f,
                veterancyImpact: 0.7f,
                rangeShrinkImpact: 0.7f,
                chaosThreshold: 0.5f,
                maxFleetSize: 100f);
    }

    /// <summary>
    /// 夜戦／視界制限戦闘（暗黒・星雲内＝混乱と奇襲）の純ロジック（盤面非依存・plain引数）。
    /// 視界が制限されると命中が落ち・指揮が混乱し・同士討ちが増えるが、闇は奇襲の好機でもあり、
    /// 練度と索敵装備が闇の不利を補う。
    /// 分担：<see cref="NightBattleRules"/> は「視界制限が命中/指揮/同士討ちに効く戦闘レイヤー」。
    /// 「敵戦力の推定誤差」を扱う FogOfWarRules（情報の不確実性）とは別物、
    /// 地形そのものを扱う TerrainRules（地形効果）とも別物。ここは戦闘の解像度低下を担う。
    /// 実効値パターン（基準値を破壊せず倍率/減衰を返す）・乱数なし（必要なら呼び出し側が roll を渡す）。
    /// </summary>
    public static class NightBattleRules
    {
        /// <summary>
        /// 視界低下度（0..1）。暗さで視界が落ち、索敵装備（センサー品質）が闇を補って取り戻す。
        /// = darkness × darknessWeight ×（1 − sensorQuality × sensorWeight）。
        /// </summary>
        public static float VisibilityPenalty(float darkness, float sensorQuality, NightBattleParams p)
        {
            float d = Mathf.Clamp01(darkness);
            float s = Mathf.Clamp01(sensorQuality);
            float raw = d * p.darknessWeight * (1f - s * p.sensorWeight);
            return Mathf.Clamp01(raw);
        }

        public static float VisibilityPenalty(float darkness, float sensorQuality) =>
            VisibilityPenalty(darkness, sensorQuality, NightBattleParams.Default);

        /// <summary>
        /// 命中率の低下量（0..1）。視界が落ちるほど当たらなくなる。
        /// </summary>
        public static float AccuracyReduction(float visibilityPenalty, NightBattleParams p)
        {
            float vp = Mathf.Clamp01(visibilityPenalty);
            return Mathf.Clamp01(vp * p.accuracyImpact);
        }

        public static float AccuracyReduction(float visibilityPenalty) =>
            AccuracyReduction(visibilityPenalty, NightBattleParams.Default);

        /// <summary>
        /// 同士討ちのリスク（0..1）。低視界 × 混戦（陣形密度）で味方を撃つ。
        /// </summary>
        public static float FriendlyFireRisk(float visibilityPenalty, float formationDensity, NightBattleParams p)
        {
            float vp = Mathf.Clamp01(visibilityPenalty);
            float density = Mathf.Clamp01(formationDensity);
            return Mathf.Clamp01(vp * density * p.friendlyFireBase);
        }

        public static float FriendlyFireRisk(float visibilityPenalty, float formationDensity) =>
            FriendlyFireRisk(visibilityPenalty, formationDensity, NightBattleParams.Default);

        /// <summary>
        /// 指揮混乱度（0..1）。大艦隊ほど闇で統制が崩れる（規模を maxFleetSize で正規化）。
        /// </summary>
        public static float CommandConfusion(float visibilityPenalty, float fleetSize, NightBattleParams p)
        {
            float vp = Mathf.Clamp01(visibilityPenalty);
            float sizeFactor = Mathf.Clamp01(Mathf.Max(0f, fleetSize) / p.maxFleetSize);
            return Mathf.Clamp01(vp * p.confusionImpact * sizeFactor);
        }

        public static float CommandConfusion(float visibilityPenalty, float fleetSize) =>
            CommandConfusion(visibilityPenalty, fleetSize, NightBattleParams.Default);

        /// <summary>
        /// 夜戦奇襲ボーナス（0..1）。闇に紛れた隠密接近で、敵の視界が低いほど有利になる。
        /// </summary>
        public static float NightSurpriseBonus(float stealthApproach, float enemyVisibilityPenalty, NightBattleParams p)
        {
            float stealth = Mathf.Clamp01(stealthApproach);
            float enemyVp = Mathf.Clamp01(enemyVisibilityPenalty);
            return Mathf.Clamp01(stealth * enemyVp * p.surpriseImpact);
        }

        public static float NightSurpriseBonus(float stealthApproach, float enemyVisibilityPenalty) =>
            NightSurpriseBonus(stealthApproach, enemyVisibilityPenalty, NightBattleParams.Default);

        /// <summary>
        /// 練度補償後の実効視界低下（0..1）。練度が高いほど闇の不利を打ち消す（基準 vp は非破壊）。
        /// = visibilityPenalty ×（1 − veterancy × veterancyImpact）。
        /// </summary>
        public static float VeterancyCompensation(float veterancy, float visibilityPenalty, NightBattleParams p)
        {
            float vet = Mathf.Clamp01(veterancy);
            float vp = Mathf.Clamp01(visibilityPenalty);
            return Mathf.Clamp01(vp * (1f - vet * p.veterancyImpact));
        }

        public static float VeterancyCompensation(float veterancy, float visibilityPenalty) =>
            VeterancyCompensation(veterancy, visibilityPenalty, NightBattleParams.Default);

        /// <summary>
        /// 実効交戦距離。視界が落ちると敵を遠くで捉えられず近接遭遇戦になる（baseRange は非破壊）。
        /// = baseRange ×（1 − visibilityPenalty × rangeShrinkImpact）。
        /// </summary>
        public static float EngagementRangeShrink(float visibilityPenalty, float baseRange, NightBattleParams p)
        {
            float vp = Mathf.Clamp01(visibilityPenalty);
            float br = Mathf.Max(0f, baseRange);
            return br * (1f - vp * p.rangeShrinkImpact);
        }

        public static float EngagementRangeShrink(float visibilityPenalty, float baseRange) =>
            EngagementRangeShrink(visibilityPenalty, baseRange, NightBattleParams.Default);

        /// <summary>
        /// 夜戦の混乱状態か。視界低下がしきい値以上なら混戦（近接遭遇戦化）と判定。
        /// </summary>
        public static bool IsNightChaos(float visibilityPenalty, NightBattleParams p)
        {
            float vp = Mathf.Clamp01(visibilityPenalty);
            return vp >= p.chaosThreshold;
        }

        public static bool IsNightChaos(float visibilityPenalty) =>
            IsNightChaos(visibilityPenalty, NightBattleParams.Default);
    }
}
