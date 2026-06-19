using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 兵力節用（エコノミー・オブ・フォース）の調整値（#節用）＝二次正面を必要最小限で保持し、浮いた戦力を主攻（重点）へ回す運用判断のパラメータ。
    /// 最小所要を決める脅威係数・節約しすぎの崩壊感度・主攻強化の効率・牽制効果の係数。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct EconomyOfForceParams
    {
        /// <summary>二次正面を保持するのに要る最小戦力＝脅威×この係数（大きいほど守りに戦力が要る）。</summary>
        public readonly float minimumForceFactor;
        /// <summary>節約しすぎ（最小所要を割り込む不足）→二次正面崩れリスクの感度。</summary>
        public readonly float shortfallRiskScale;
        /// <summary>浮いた戦力が主攻を強化する効率（0..1。1.0＝浮き分がそのまま主攻の充足に効く）。</summary>
        public readonly float reinforcementEfficiency;
        /// <summary>牽制・拘束の効果係数（少数で敵を釘付けにする効き）。</summary>
        public readonly float holdingEffectScale;

        public EconomyOfForceParams(float minimumForceFactor, float shortfallRiskScale,
            float reinforcementEfficiency, float holdingEffectScale)
        {
            this.minimumForceFactor = Mathf.Clamp(minimumForceFactor, 0f, 10f);
            this.shortfallRiskScale = Mathf.Clamp(shortfallRiskScale, 0f, 10f);
            this.reinforcementEfficiency = Mathf.Clamp01(reinforcementEfficiency);
            this.holdingEffectScale = Mathf.Clamp(holdingEffectScale, 0f, 10f);
        }

        /// <summary>既定：最小所要係数0.6・崩壊感度1.0・主攻強化効率0.8・牽制効果1.0。</summary>
        public static EconomyOfForceParams Default => new EconomyOfForceParams(
            DefaultMinimumForceFactor, DefaultShortfallRiskScale,
            DefaultReinforcementEfficiency, DefaultHoldingEffectScale);

        public const float DefaultMinimumForceFactor = 0.6f;
        public const float DefaultShortfallRiskScale = 1.0f;
        public const float DefaultReinforcementEfficiency = 0.8f;
        public const float DefaultHoldingEffectScale = 1.0f;
    }

    /// <summary>
    /// 兵力節用（エコノミー・オブ・フォース）の純ロジック（#節用・唯一の窓口）＝<b>決定的でない二次正面（防御・牽制）は必要最小限で保持し、浮いた戦力を主攻（重点）へ集中する</b>。
    /// 二次正面を保つ最小所要（<see cref="MinimumViableForce"/>）を見積もり、それを超えた分が主攻へ回せる浮き（<see cref="EconomizedSurplus"/>）。
    /// 節約しすぎれば二次正面が崩れ（<see cref="SecondarySectorRisk"/>／<see cref="OvereconomyCollapse"/>）、適度に浮かせれば主攻を強化できる（<see cref="MainEffortReinforcement"/>）。
    /// 牽制は少数で敵を釘付けにする（<see cref="HoldingActionEffectiveness"/>）。節約と主攻強化の綱引きをバランスで見て（<see cref="ForceAllocationBalance"/>）、うまく節用できているか判定する（<see cref="IsEconomicallyAllocated"/>）。
    /// <b>分担</b>：兵力集中そのもの（局所優勢/集めすぎ）は <see cref="ForceConcentrationRules"/>、主攻正面の「選定」と支作戦配分は <see cref="SchwerpunktRules"/> が担う＝こちらは<b>二次正面の「節約」という対の原則</b>の数値。
    /// 戦域全体のための予備兵力（<see cref="StrategicReserveRules"/>）とも別＝こちらは<b>すでに正面へ割いた戦力の節約配分</b>。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・test-first。
    /// </summary>
    public static class EconomyOfForceRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        // ── 二次正面の最小所要 ────────────────────────────────────

        /// <summary>二次正面を保持するのに要る最小戦力＝脅威×最小所要係数（守りに最低限要る兵力）。</summary>
        public static float MinimumViableForce(float sectorThreat)
            => MinimumViableForce(sectorThreat, EconomyOfForceParams.Default);

        /// <summary>最小所要戦力＝max(0,脅威)×minimumForceFactor（非負）。</summary>
        public static float MinimumViableForce(float sectorThreat, EconomyOfForceParams p)
        {
            float threat = Mathf.Max(0f, sectorThreat);
            return threat * p.minimumForceFactor;
        }

        // ── 浮いた戦力（主攻へ回せる） ─────────────────────────────

        /// <summary>最小限を超えて浮いた（主攻へ回せる）戦力＝割当−最小所要（不足なら0）。</summary>
        public static float EconomizedSurplus(float allocatedForce, float minimumViableForce)
            => EconomizedSurplus(allocatedForce, minimumViableForce, EconomyOfForceParams.Default);

        /// <summary>浮いた戦力＝max(0, max(0,割当)−max(0,最小所要))。最小未満は浮き0。</summary>
        public static float EconomizedSurplus(float allocatedForce, float minimumViableForce, EconomyOfForceParams p)
        {
            float alloc = Mathf.Max(0f, allocatedForce);
            float min = Mathf.Max(0f, minimumViableForce);
            return Mathf.Max(0f, alloc - min);
        }

        // ── 節約しすぎのリスク ────────────────────────────────────

        /// <summary>節約しすぎて二次正面が崩れるリスク（0..1）＝最小所要に対する不足割合×感度。最小以上は0。</summary>
        public static float SecondarySectorRisk(float allocatedForce, float minimumViableForce)
            => SecondarySectorRisk(allocatedForce, minimumViableForce, EconomyOfForceParams.Default);

        /// <summary>二次正面崩れリスク（0..1）＝clamp01((最小所要−割当)÷最小所要)×shortfallRiskScale。割当が最小以上なら0。</summary>
        public static float SecondarySectorRisk(float allocatedForce, float minimumViableForce, EconomyOfForceParams p)
        {
            float alloc = Mathf.Max(0f, allocatedForce);
            float min = Mathf.Max(Epsilon, minimumViableForce);
            float shortfall = (min - alloc) / min;
            if (shortfall <= 0f) return 0f;
            return Mathf.Clamp01(shortfall * p.shortfallRiskScale);
        }

        // ── 主攻の強化 ────────────────────────────────────────────

        /// <summary>浮いた戦力が主攻をどれだけ強化するか（0..1）＝浮き×効率を主攻所要で割った充足度。所要0は満額1。</summary>
        public static float MainEffortReinforcement(float economizedSurplus, float mainEffortNeed)
            => MainEffortReinforcement(economizedSurplus, mainEffortNeed, EconomyOfForceParams.Default);

        /// <summary>主攻強化度（0..1）＝clamp01(max(0,浮き)×reinforcementEfficiency ÷ max(0,主攻所要))。所要≤0なら1.0。</summary>
        public static float MainEffortReinforcement(float economizedSurplus, float mainEffortNeed, EconomyOfForceParams p)
        {
            float surplus = Mathf.Max(0f, economizedSurplus);
            float need = Mathf.Max(0f, mainEffortNeed);
            if (need <= 0f) return 1f;
            return Mathf.Clamp01(surplus * p.reinforcementEfficiency / need);
        }

        // ── 牽制・拘束の効果 ──────────────────────────────────────

        /// <summary>牽制・拘束の効果（0..1）＝少数の牽制戦力で敵をどれだけ釘付けにできるか（敵圧力に対する相対）。</summary>
        public static float HoldingActionEffectiveness(float holdingForce, float enemyPressure)
            => HoldingActionEffectiveness(holdingForce, enemyPressure, EconomyOfForceParams.Default);

        /// <summary>牽制効果（0..1）＝clamp01(max(0,牽制戦力)×holdingEffectScale ÷ max(eps,敵圧力))。敵圧力0は微小値で割る。</summary>
        public static float HoldingActionEffectiveness(float holdingForce, float enemyPressure, EconomyOfForceParams p)
        {
            float hold = Mathf.Max(0f, holdingForce);
            float pressure = Mathf.Max(Epsilon, enemyPressure);
            return Mathf.Clamp01(hold * p.holdingEffectScale / pressure);
        }

        // ── 節約しすぎの崩壊判定 ──────────────────────────────────

        /// <summary>節約しすぎで二次正面が崩壊したか＝割当が最小所要×既定割合を下回る（既定 `CollapseThreshold`=0.5）。</summary>
        public static bool OvereconomyCollapse(float allocatedForce, float minimumViableForce)
            => OvereconomyCollapse(allocatedForce, minimumViableForce, CollapseThreshold);

        /// <summary>二次正面の崩壊（bool）＝max(0,割当) &lt; max(0,最小所要)×clamp01(threshold)。最小所要0は崩壊しない。</summary>
        public static bool OvereconomyCollapse(float allocatedForce, float minimumViableForce, float threshold)
        {
            float alloc = Mathf.Max(0f, allocatedForce);
            float min = Mathf.Max(0f, minimumViableForce);
            if (min <= 0f) return false;
            return alloc < min * Mathf.Clamp01(threshold);
        }

        /// <summary>二次正面が崩壊とみなす最小所要に対する割当比の既定閾値。</summary>
        public const float CollapseThreshold = 0.5f;

        // ── 節約と主攻強化のバランス ──────────────────────────────

        /// <summary>節約と主攻強化のバランス（-1..1）＝主攻強化−二次正面リスク（-1=節約過小で主攻弱る／+1=適正）。</summary>
        public static float ForceAllocationBalance(float secondaryRisk, float mainEffortGain)
            => ForceAllocationBalance(secondaryRisk, mainEffortGain, EconomyOfForceParams.Default);

        /// <summary>配分バランス（-1..1）＝clamp01(主攻強化)−clamp01(二次正面リスク)。正＝うまく節約して主攻が強い、負＝崩れリスクが勝つ。</summary>
        public static float ForceAllocationBalance(float secondaryRisk, float mainEffortGain, EconomyOfForceParams p)
        {
            float risk = Mathf.Clamp01(secondaryRisk);
            float gain = Mathf.Clamp01(mainEffortGain);
            return Mathf.Clamp(gain - risk, -1f, 1f);
        }

        // ── 節用できているかの判定 ────────────────────────────────

        /// <summary>うまく節用できているか＝主攻へ回せる浮きがあり、かつ二次正面リスクが既定閾値以下（既定 `WellEconomizedRiskThreshold`=0.3）。</summary>
        public static bool IsEconomicallyAllocated(float economizedSurplus, float secondarySectorRisk)
            => IsEconomicallyAllocated(economizedSurplus, secondarySectorRisk, WellEconomizedRiskThreshold);

        /// <summary>節用できているか（bool）＝max(0,浮き)&gt;0 かつ clamp01(二次正面リスク)≤threshold。浮きが無い／崩れリスクが高いと false。</summary>
        public static bool IsEconomicallyAllocated(float economizedSurplus, float secondarySectorRisk, float threshold)
        {
            float surplus = Mathf.Max(0f, economizedSurplus);
            float risk = Mathf.Clamp01(secondarySectorRisk);
            return surplus > 0f && risk <= threshold;
        }

        /// <summary>節用できているとみなす二次正面リスクの既定上限。</summary>
        public const float WellEconomizedRiskThreshold = 0.3f;
    }
}
