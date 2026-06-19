using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 兵力集中の調整値（#兵力集中）＝決定的な一点へ戦力を集める運用判断のパラメータ。
    /// 集めるのに要る時間・他方面の手薄リスク・集めすぎの脆弱性・質的効果（二乗）の係数。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct ForceConcentrationParams
    {
        /// <summary>戦力を集めるのに要る時間の係数（集結速度の逆数＝大きいほど集結に時間がかかる）。</summary>
        public readonly float gatherTimeRate;
        /// <summary>各個撃破の効きの指数（局所優勢比→各個撃破潜在の感度。1=線形）。</summary>
        public readonly float defeatInDetailExponent;
        /// <summary>集中の質的効果の指数（局所優勢比の二乗的効き＝Lanchester 的。2.0＝二乗則）。</summary>
        public readonly float massExponent;
        /// <summary>集めすぎリスクが立ち始める集中割合の閾値（0..1。卵を一つの籠に）。</summary>
        public readonly float overconcentrationThreshold;
        /// <summary>閾値超過分→集めすぎリスクの感度。</summary>
        public readonly float overconcentrationScale;

        public ForceConcentrationParams(float gatherTimeRate, float defeatInDetailExponent, float massExponent,
            float overconcentrationThreshold, float overconcentrationScale)
        {
            this.gatherTimeRate = Mathf.Clamp(gatherTimeRate, 0.01f, 100f);
            this.defeatInDetailExponent = Mathf.Clamp(defeatInDetailExponent, 0.1f, 4f);
            this.massExponent = Mathf.Clamp(massExponent, 0.5f, 4f);
            this.overconcentrationThreshold = Mathf.Clamp01(overconcentrationThreshold);
            this.overconcentrationScale = Mathf.Clamp(overconcentrationScale, 0f, 10f);
        }

        /// <summary>既定：集結時間係数1.0・各個撃破指数1.0（線形）・質的効果指数2.0（二乗則）・集めすぎ閾値0.7・感度2.0。</summary>
        public static ForceConcentrationParams Default => new ForceConcentrationParams(
            DefaultGatherTimeRate, DefaultDefeatInDetailExponent, DefaultMassExponent,
            DefaultOverconcentrationThreshold, DefaultOverconcentrationScale);

        public const float DefaultGatherTimeRate = 1.0f;
        public const float DefaultDefeatInDetailExponent = 1.0f;
        public const float DefaultMassExponent = 2.0f;
        public const float DefaultOverconcentrationThreshold = 0.7f;
        public const float DefaultOverconcentrationScale = 2.0f;
    }

    /// <summary>
    /// 兵力集中の純ロジック（#兵力集中・唯一の窓口）＝<b>戦力を分散させず決定的な一点に集める運用判断</b>。
    /// 局所優勢で分散した敵を各個撃破できる（孫子・クラウゼヴィッツ）。だが集中には時間がかかり（<see cref="ConcentrationTime"/>）、
    /// 集める間は他方面が手薄になり（<see cref="OtherFrontExposure"/>）、集めすぎれば一撃で大損害を被る（<see cref="OverconcentrationRisk"/>）。
    /// <b>分担</b>：局所火力の二乗則そのもの（会戦ダメージ側）は <see cref="LanchesterRules"/> が担う＝こちらは<b>戦力を集める運用判断</b>の数値。
    /// 任務戦術の集中率（<see cref="MissionCommandRules"/>）とは別＝こちらは<b>兵力集中そのもの</b>の局所優勢/時間/リスクを出す。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・test-first。
    /// </summary>
    public static class ForceConcentrationRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        // ── 局所優勢 ──────────────────────────────────────────────

        /// <summary>集中による局所優勢比＝集中戦力÷敵の局所戦力（拮抗1.0／2倍2.0）。敵0は微小値で割る。</summary>
        public static float LocalSuperiority(float concentratedStrength, float enemyLocalStrength)
            => LocalSuperiority(concentratedStrength, enemyLocalStrength, ForceConcentrationParams.Default);

        /// <summary>集中による局所優勢比＝集中戦力÷敵の局所戦力。負入力は0扱い。</summary>
        public static float LocalSuperiority(float concentratedStrength, float enemyLocalStrength, ForceConcentrationParams p)
        {
            float c = Mathf.Max(0f, concentratedStrength);
            float e = Mathf.Max(Epsilon, enemyLocalStrength);
            return c / e;
        }

        // ── 集結時間 ──────────────────────────────────────────────

        /// <summary>戦力を集めるのに要る時間＝集める戦力量×分散距離×係数（集中には時間がかかる）。</summary>
        public static float ConcentrationTime(float forcesToGather, float dispersionDistance)
            => ConcentrationTime(forcesToGather, dispersionDistance, ForceConcentrationParams.Default);

        /// <summary>戦力を集めるのに要る時間＝forces×distance×gatherTimeRate（負入力は0扱い・非負）。</summary>
        public static float ConcentrationTime(float forcesToGather, float dispersionDistance, ForceConcentrationParams p)
        {
            float f = Mathf.Max(0f, forcesToGather);
            float d = Mathf.Max(0f, dispersionDistance);
            return f * d * p.gatherTimeRate;
        }

        // ── 他方面の手薄リスク ────────────────────────────────────

        /// <summary>集中で他方面が手薄になる脆弱性（0..1）＝集中割合×他方面の比率((総数−1)/総数)。</summary>
        public static float OtherFrontExposure(float concentratedFraction, int totalFronts)
            => OtherFrontExposure(concentratedFraction, totalFronts, ForceConcentrationParams.Default);

        /// <summary>集中で他方面が手薄になる脆弱性（0..1）。前線が1以下なら他方面なし＝0。</summary>
        public static float OtherFrontExposure(float concentratedFraction, int totalFronts, ForceConcentrationParams p)
        {
            if (totalFronts <= 1) return 0f;
            float frac = Mathf.Clamp01(concentratedFraction);
            float otherRatio = (totalFronts - 1f) / totalFronts;
            return Mathf.Clamp01(frac * otherRatio);
        }

        // ── 各個撃破 ──────────────────────────────────────────────

        /// <summary>集中側が分散した敵を各個撃破できる潜在（0..1）＝局所優勢の余剰×敵の分散度。</summary>
        public static float DefeatInDetailPotential(float localSuperiority, float enemyDispersion)
            => DefeatInDetailPotential(localSuperiority, enemyDispersion, ForceConcentrationParams.Default);

        /// <summary>各個撃破潜在（0..1）＝clamp01(優勢比−1)^指数 × clamp01(敵分散度)。拮抗以下は0。</summary>
        public static float DefeatInDetailPotential(float localSuperiority, float enemyDispersion, ForceConcentrationParams p)
        {
            float advantage = Mathf.Clamp01(Mathf.Max(0f, localSuperiority) - 1f);
            float dispersion = Mathf.Clamp01(enemyDispersion);
            return Mathf.Clamp01(Mathf.Pow(advantage, p.defeatInDetailExponent) * dispersion);
        }

        // ── 質的効果（二乗則） ────────────────────────────────────

        /// <summary>集中の質的効果＝局所優勢比^massExponent（数の二乗的効き＝Lanchester 的）。拮抗1.0。</summary>
        public static float MassEffect(float localSuperiority)
            => MassEffect(localSuperiority, ForceConcentrationParams.Default);

        /// <summary>集中の質的効果＝clamp(優勢比,0..)^massExponent。負は0。</summary>
        public static float MassEffect(float localSuperiority, ForceConcentrationParams p)
            => Mathf.Pow(Mathf.Max(0f, localSuperiority), p.massExponent);

        // ── 集めすぎリスク ────────────────────────────────────────

        /// <summary>集めすぎて一撃で大損害を受けるリスク（0..1・卵を一つの籠に）＝閾値超過分×感度。</summary>
        public static float OverconcentrationRisk(float concentratedFraction)
            => OverconcentrationRisk(concentratedFraction, ForceConcentrationParams.Default);

        /// <summary>集めすぎリスク（0..1）。`overconcentrationThreshold` 超過分×`overconcentrationScale` をクランプ。閾値以下は0。</summary>
        public static float OverconcentrationRisk(float concentratedFraction, ForceConcentrationParams p)
        {
            float over = Mathf.Clamp01(concentratedFraction) - p.overconcentrationThreshold;
            if (over <= 0f) return 0f;
            return Mathf.Clamp01(over * p.overconcentrationScale);
        }

        // ── 好機の窓 ──────────────────────────────────────────────

        /// <summary>集中が好機の窓に間に合うか（0..1・1=完全に間に合う）＝窓÷集結時間をクランプ。</summary>
        public static float TimingVsConcentration(float concentrationTime, float opportunityWindow)
            => TimingVsConcentration(concentrationTime, opportunityWindow, ForceConcentrationParams.Default);

        /// <summary>集中の間に合い度（0..1）。集結時間0は即時＝1.0、窓0は0.0。</summary>
        public static float TimingVsConcentration(float concentrationTime, float opportunityWindow, ForceConcentrationParams p)
        {
            float t = Mathf.Max(0f, concentrationTime);
            float w = Mathf.Max(0f, opportunityWindow);
            if (t <= Epsilon) return 1f;
            return Mathf.Clamp01(w / t);
        }

        // ── 決定的集中の判定 ──────────────────────────────────────

        /// <summary>決定的に集中できたか＝局所優勢比が閾値以上（既定 `DecisiveThreshold`=2.0）。</summary>
        public static bool IsDecisivelyConcentrated(float localSuperiority)
            => IsDecisivelyConcentrated(localSuperiority, DecisiveThreshold);

        /// <summary>決定的に集中できたか＝局所優勢比が閾値以上（bool）。</summary>
        public static bool IsDecisivelyConcentrated(float localSuperiority, float threshold)
            => localSuperiority >= threshold;

        /// <summary>決定的集中とみなす局所優勢比の既定閾値（2:1＝決戦の一点）。</summary>
        public const float DecisiveThreshold = 2.0f;
    }
}
