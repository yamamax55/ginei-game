using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 通商保護の調整値（#通商保護）。護衛配分・哨戒制圧・船団有効性・航路安全・保護コスト・途絶打撃・攻防バランス・正味価値のパラメータ。
    /// 符号付きの攻防バランス／正味価値は -1..1。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct CommerceProtectionParams
    {
        /// <summary>護衛配分の参照スケール（この通商路数で護衛戦力÷路数が等倍になる正規化分母）。</summary>
        public readonly float routeScale;
        /// <summary>哨戒網が制圧に寄与する重み（哨戒0でも護衛だけで制圧する余地を残す）。</summary>
        public readonly float patrolWeight;
        /// <summary>船団規律が船団方式の有効性に寄与する重み（規律0でも護衛だけで一定の効果）。</summary>
        public readonly float disciplineWeight;
        /// <summary>哨戒範囲が保護コストに乗る重み（哨戒網の維持費）。</summary>
        public readonly float patrolCostWeight;
        /// <summary>保護コストの正規化スケール（このコストで価値計算上の等倍ペナルティ）。</summary>
        public readonly float costScale;
        /// <summary>通商依存が途絶打撃に乗る指数（1.0＝線形。大きいほど依存度の高い勢力が脆い）。</summary>
        public readonly float dependencyExponent;

        public CommerceProtectionParams(float routeScale, float patrolWeight, float disciplineWeight,
            float patrolCostWeight, float costScale, float dependencyExponent)
        {
            this.routeScale = Mathf.Max(0.0001f, routeScale);
            this.patrolWeight = Mathf.Clamp01(patrolWeight);
            this.disciplineWeight = Mathf.Clamp01(disciplineWeight);
            this.patrolCostWeight = Mathf.Clamp(patrolCostWeight, 0f, 4f);
            this.costScale = Mathf.Max(0.0001f, costScale);
            this.dependencyExponent = Mathf.Clamp(dependencyExponent, 0f, 4f);
        }

        /// <summary>既定：路スケール10・哨戒重み0.5・規律重み0.5・哨戒コスト重み1.0・コストスケール10・依存指数1.0。</summary>
        public static CommerceProtectionParams Default => new CommerceProtectionParams(
            DefaultRouteScale, DefaultPatrolWeight, DefaultDisciplineWeight,
            DefaultPatrolCostWeight, DefaultCostScale, DefaultDependencyExponent);

        public const float DefaultRouteScale = 10f;
        public const float DefaultPatrolWeight = 0.5f;
        public const float DefaultDisciplineWeight = 0.5f;
        public const float DefaultPatrolCostWeight = 1f;
        public const float DefaultCostScale = 10f;
        public const float DefaultDependencyExponent = 1f;
    }

    /// <summary>
    /// 通商保護の純ロジック（#通商保護・唯一の窓口）＝<b>勢力規模の交易路防衛と海上交通維持</b>。
    /// 護衛戦力を通商路へ配分し、哨戒網と船団方式で通商破壊艦を抑え、航路の安全を保つ。保護にはコストが掛かり、
    /// 通商の途絶は経済を打つ（攻防はトレードオフ）。<see cref="ConvoyDefenseRules"/>（個別船団護衛の戦術 #船団護衛）とは別＝
    /// こちらは勢力規模の通商保護<b>戦略</b>。盤面非依存の plain 引数・実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class CommerceProtectionRules
    {
        /// <summary>既定パラメータで1通商路あたりの護衛密度。</summary>
        public static float EscortAllocation(float escortForce, float tradeRouteCount)
            => EscortAllocation(escortForce, tradeRouteCount, CommerceProtectionParams.Default);

        /// <summary>
        /// 護衛戦力÷通商路数で1路あたりの護衛密度（0..1）。路スケールで正規化＝多くの航路へ薄く配分すると密度が下がる。
        /// 通商路0は密度0扱い。密度＝(escort/route)/(escort/route + routeScale)（飽和曲線）。
        /// </summary>
        public static float EscortAllocation(float escortForce, float tradeRouteCount, CommerceProtectionParams p)
        {
            float escort = Mathf.Max(0f, escortForce);
            float routes = Mathf.Max(0f, tradeRouteCount);
            if (routes <= 0f) return 0f;
            float perRoute = escort / routes;
            return Mathf.Clamp01(perRoute / (perRoute + p.routeScale));
        }

        /// <summary>既定パラメータで通商破壊艦の制圧度。</summary>
        public static float RaiderSuppression(float escortAllocation, float patrolCoverage)
            => RaiderSuppression(escortAllocation, patrolCoverage, CommerceProtectionParams.Default);

        /// <summary>
        /// 護衛密度×哨戒網で通商破壊艦の制圧度（0..1）。哨戒は配分された護衛を底上げする＝
        /// suppression＝allocation×(1 + patrolWeight×patrol) を 0..1 へ。哨戒0でも護衛だけで制圧、護衛0なら制圧0。
        /// </summary>
        public static float RaiderSuppression(float escortAllocation, float patrolCoverage, CommerceProtectionParams p)
        {
            float allocation = Mathf.Clamp01(escortAllocation);
            float patrol = Mathf.Clamp01(patrolCoverage);
            return Mathf.Clamp01(allocation * (1f + p.patrolWeight * patrol));
        }

        /// <summary>既定パラメータで船団方式の有効性。</summary>
        public static float ConvoyEffectiveness(float convoyDiscipline, float escortAllocation)
            => ConvoyEffectiveness(convoyDiscipline, escortAllocation, CommerceProtectionParams.Default);

        /// <summary>
        /// 船団規律×護衛で船団方式の有効性（0..1）。規律ある船団に護衛が随伴して初めて方式が機能する＝
        /// effectiveness＝allocation×(1 + disciplineWeight×discipline) を 0..1 へ。護衛0なら規律だけでは無力。
        /// </summary>
        public static float ConvoyEffectiveness(float convoyDiscipline, float escortAllocation, CommerceProtectionParams p)
        {
            float discipline = Mathf.Clamp01(convoyDiscipline);
            float allocation = Mathf.Clamp01(escortAllocation);
            return Mathf.Clamp01(allocation * (1f + p.disciplineWeight * discipline));
        }

        /// <summary>既定パラメータで航路の安全。</summary>
        public static float RouteSafety(float raiderSuppression, float convoyEffectiveness)
            => RouteSafety(raiderSuppression, convoyEffectiveness, CommerceProtectionParams.Default);

        /// <summary>
        /// 通商破壊艦の制圧×船団方式の有効性で航路の安全（0..1）。両輪が揃うほど安全＝
        /// safety＝1-(1-suppression)×(1-effectiveness)（どちらか高ければ安全側へ寄る・両方0で安全0）。
        /// </summary>
        public static float RouteSafety(float raiderSuppression, float convoyEffectiveness, CommerceProtectionParams p)
        {
            float suppression = Mathf.Clamp01(raiderSuppression);
            float effectiveness = Mathf.Clamp01(convoyEffectiveness);
            return Mathf.Clamp01(1f - (1f - suppression) * (1f - effectiveness));
        }

        /// <summary>既定パラメータで通商保護のコスト。</summary>
        public static float ProtectionCost(float escortForce, float patrolCoverage)
            => ProtectionCost(escortForce, patrolCoverage, CommerceProtectionParams.Default);

        /// <summary>
        /// 護衛戦力×哨戒範囲で通商保護のコスト（非負）。護衛艦の維持に哨戒網の運用費が乗る＝
        /// cost＝escort×(1 + patrolCostWeight×patrol)。哨戒を広げるほど高くつく（保護のトレードオフ）。
        /// </summary>
        public static float ProtectionCost(float escortForce, float patrolCoverage, CommerceProtectionParams p)
        {
            float escort = Mathf.Max(0f, escortForce);
            float patrol = Mathf.Clamp01(patrolCoverage);
            return escort * (1f + p.patrolCostWeight * patrol);
        }

        /// <summary>既定パラメータで通商途絶の経済打撃。</summary>
        public static float TradeDisruptionDamage(float routeSafety, float economicDependency)
            => TradeDisruptionDamage(routeSafety, economicDependency, CommerceProtectionParams.Default);

        /// <summary>
        /// (1-航路安全)×経済の通商依存で途絶の経済打撃（0..1）。安全が低いほど・通商依存が高いほど打撃が大きい＝
        /// damage＝(1-safety)×pow(dependency, dependencyExponent)。海上交通を断たれた通商国家ほど脆い。
        /// </summary>
        public static float TradeDisruptionDamage(float routeSafety, float economicDependency, CommerceProtectionParams p)
        {
            float safety = Mathf.Clamp01(routeSafety);
            float dependency = Mathf.Clamp01(economicDependency);
            float damage = (1f - safety) * Mathf.Pow(dependency, p.dependencyExponent);
            return Mathf.Clamp01(damage);
        }

        /// <summary>既定パラメータで攻防バランス（攻者有利か）。</summary>
        public static float AttackDefenseBalance(float raiderStrength, float routeSafety)
            => AttackDefenseBalance(raiderStrength, routeSafety, CommerceProtectionParams.Default);

        /// <summary>
        /// 通商破壊艦×(1-安全)で攻防バランス（-1守者有利／+1攻者有利）。襲撃側が強く航路が危ういほど攻者有利へ＝
        /// balance＝(raider×(1-safety) - safety) を -1..1 へ。安全が高いほど守者有利、襲撃側が強大なら攻者有利。
        /// </summary>
        public static float AttackDefenseBalance(float raiderStrength, float routeSafety, CommerceProtectionParams p)
        {
            float raider = Mathf.Clamp01(raiderStrength);
            float safety = Mathf.Clamp01(routeSafety);
            float value = raider * (1f - safety) - safety;
            return Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>既定パラメータで通商保護の正味価値。</summary>
        public static float CommerceProtectionValue(float routeSafety, float protectionCost, float tradeDisruptionDamage)
            => CommerceProtectionValue(routeSafety, protectionCost, tradeDisruptionDamage, CommerceProtectionParams.Default);

        /// <summary>
        /// 安全−コスト−打撃で通商保護の正味価値（-1..1）。コストは costScale で正規化＝
        /// value＝safety - cost/(cost + costScale) - damage。安全が高くコスト・打撃が小さいほど保護は割に合う。
        /// </summary>
        public static float CommerceProtectionValue(float routeSafety, float protectionCost, float tradeDisruptionDamage, CommerceProtectionParams p)
        {
            float safety = Mathf.Clamp01(routeSafety);
            float cost = Mathf.Max(0f, protectionCost);
            float damage = Mathf.Clamp01(tradeDisruptionDamage);
            float costPenalty = cost / (cost + p.costScale);
            float value = safety - costPenalty - damage;
            return Mathf.Clamp(value, -1f, 1f);
        }
    }
}
