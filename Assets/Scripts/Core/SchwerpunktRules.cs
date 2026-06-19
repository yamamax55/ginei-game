using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 重点（シュヴェルプンクト）の調整値（#重点）＝攻勢で「ここで決める」主攻正面を定め戦力を集中する運用判断のパラメータ。
    /// 重点に集める割合・支作戦の節約効率・突破の二乗的効き・選定ミスの感度・一点集中の過伸張の係数。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct SchwerpunktParams
    {
        /// <summary>支作戦の節約効率（支作戦に割く戦力をどれだけ最小限で済ませられるか。大きいほど節約が効く）。</summary>
        public readonly float economyEfficiency;
        /// <summary>重点突破力の二乗的効き指数（局所優勢比の効き＝Lanchester 的。2.0＝二乗則）。</summary>
        public readonly float breakthroughExponent;
        /// <summary>重点選定ミスの感度（敵主力との戦力差→総崩れリスクの感度）。</summary>
        public readonly float misidentificationScale;
        /// <summary>一点集中の過伸張が立ち始める投入割合の閾値（0..1。集めすぎると他正面が手薄）。</summary>
        public readonly float overextensionThreshold;
        /// <summary>閾値超過分→過伸張リスクの感度。</summary>
        public readonly float overextensionScale;

        public SchwerpunktParams(float economyEfficiency, float breakthroughExponent, float misidentificationScale,
            float overextensionThreshold, float overextensionScale)
        {
            this.economyEfficiency = Mathf.Clamp(economyEfficiency, 0f, 1f);
            this.breakthroughExponent = Mathf.Clamp(breakthroughExponent, 0.5f, 4f);
            this.misidentificationScale = Mathf.Clamp(misidentificationScale, 0f, 10f);
            this.overextensionThreshold = Mathf.Clamp01(overextensionThreshold);
            this.overextensionScale = Mathf.Clamp(overextensionScale, 0f, 10f);
        }

        /// <summary>既定：支作戦節約効率0.5・突破指数2.0（二乗則）・選定ミス感度1.0・過伸張閾値0.7・感度2.0。</summary>
        public static SchwerpunktParams Default => new SchwerpunktParams(
            DefaultEconomyEfficiency, DefaultBreakthroughExponent, DefaultMisidentificationScale,
            DefaultOverextensionThreshold, DefaultOverextensionScale);

        public const float DefaultEconomyEfficiency = 0.5f;
        public const float DefaultBreakthroughExponent = 2.0f;
        public const float DefaultMisidentificationScale = 1.0f;
        public const float DefaultOverextensionThreshold = 0.7f;
        public const float DefaultOverextensionScale = 2.0f;
    }

    /// <summary>
    /// 重点（シュヴェルプンクト）の純ロジック（#重点・唯一の窓口）＝<b>攻勢で「ここで決める」主攻正面を定め戦力・補給・予備を集中する</b>。
    /// 重点に努力を集め（<see cref="FocalPointStrength"/>）他は支作戦＝陽動・経済で最小限に済ませる（<see cref="SupportingEffortEconomy"/>）。
    /// 重点では局所優勢で決定的突破を狙う（<see cref="BreakthroughConcentration"/>／<see cref="DecisiveBreakthrough"/>）が、
    /// 重点選定を誤り敵主力に当たれば総崩れ（<see cref="MisidentificationRisk"/>）、一点集中で他正面が手薄になりすぎる過伸張のリスクもある（<see cref="OverextensionFromNarrowFocus"/>）。
    /// 好機に応じ重点を移す転換にはコストがかかる（<see cref="WeightShift"/>）。
    /// <b>分担</b>：兵力集中そのものの局所優勢/時間/集めすぎは <see cref="ForceConcentrationRules"/> が担う＝こちらは<b>主攻正面の「選定」と支作戦との配分</b>の数値。
    /// 任務戦術の動員・集中率（<see cref="MissionCommandRules"/>）とは別＝こちらは<b>重点の選定と決定打</b>を出す。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・test-first。
    /// </summary>
    public static class SchwerpunktRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        // ── 重点への集中 ──────────────────────────────────────────

        /// <summary>重点に集める戦力＝総戦力×投入割合（主攻正面へ努力を集中する）。</summary>
        public static float FocalPointStrength(float committedFraction, float totalStrength)
            => FocalPointStrength(committedFraction, totalStrength, SchwerpunktParams.Default);

        /// <summary>重点に集める戦力＝clamp01(投入割合)×max(0,総戦力)（非負）。</summary>
        public static float FocalPointStrength(float committedFraction, float totalStrength, SchwerpunktParams p)
        {
            float frac = Mathf.Clamp01(committedFraction);
            float total = Mathf.Max(0f, totalStrength);
            return frac * total;
        }

        // ── 支作戦の節約 ──────────────────────────────────────────

        /// <summary>支作戦は最小限で済ませる節約効果（0..1＝1.0＝支作戦に戦力を要しない）＝支作戦割合を効率で割り引く。</summary>
        public static float SupportingEffortEconomy(float supportingFraction)
            => SupportingEffortEconomy(supportingFraction, SchwerpunktParams.Default);

        /// <summary>支作戦の節約効果（0..1）＝1−支作戦割合×(1−economyEfficiency)。割合0で1.0、効率1で常に1.0。</summary>
        public static float SupportingEffortEconomy(float supportingFraction, SchwerpunktParams p)
        {
            float frac = Mathf.Clamp01(supportingFraction);
            return Mathf.Clamp01(1f - frac * (1f - p.economyEfficiency));
        }

        // ── 重点での突破力 ────────────────────────────────────────

        /// <summary>重点での突破力＝局所優勢比(重点戦力÷敵局所戦力)^breakthroughExponent（数の二乗的効き）。拮抗1.0。</summary>
        public static float BreakthroughConcentration(float focalPointStrength, float enemyAtFocalPoint)
            => BreakthroughConcentration(focalPointStrength, enemyAtFocalPoint, SchwerpunktParams.Default);

        /// <summary>重点での突破力＝(max(0,重点戦力)÷max(eps,敵局所))^breakthroughExponent。敵0は微小値で割る。</summary>
        public static float BreakthroughConcentration(float focalPointStrength, float enemyAtFocalPoint, SchwerpunktParams p)
        {
            float f = Mathf.Max(0f, focalPointStrength);
            float e = Mathf.Max(Epsilon, enemyAtFocalPoint);
            return Mathf.Pow(f / e, p.breakthroughExponent);
        }

        // ── 重点選定ミスのリスク ──────────────────────────────────

        /// <summary>重点選定を誤り敵主力に当たるリスク（0..1）＝重点が敵主力に対し劣勢なほど高い（総崩れ）。</summary>
        public static float MisidentificationRisk(float focalPointStrength, float enemyMainStrength)
            => MisidentificationRisk(focalPointStrength, enemyMainStrength, SchwerpunktParams.Default);

        /// <summary>重点選定ミスのリスク（0..1）＝clamp01((敵主力−重点戦力)÷敵主力)×misidentificationScale。重点が敵主力以上なら0。</summary>
        public static float MisidentificationRisk(float focalPointStrength, float enemyMainStrength, SchwerpunktParams p)
        {
            float f = Mathf.Max(0f, focalPointStrength);
            float e = Mathf.Max(Epsilon, enemyMainStrength);
            float deficit = (e - f) / e;
            if (deficit <= 0f) return 0f;
            return Mathf.Clamp01(deficit * p.misidentificationScale);
        }

        // ── 重点の転換 ────────────────────────────────────────────

        /// <summary>重点を移す価値＝新たな好機の値−現重点の値−転換コスト（正なら移すべき・好機に応じた転換）。</summary>
        public static float WeightShift(float currentFocalPoint, float newOpportunity, float shiftCost)
            => WeightShift(currentFocalPoint, newOpportunity, shiftCost, SchwerpunktParams.Default);

        /// <summary>重点転換の正味価値＝max(0,新好機)−max(0,現重点)−max(0,転換コスト)。正＝転換が得。</summary>
        public static float WeightShift(float currentFocalPoint, float newOpportunity, float shiftCost, SchwerpunktParams p)
        {
            float cur = Mathf.Max(0f, currentFocalPoint);
            float opp = Mathf.Max(0f, newOpportunity);
            float cost = Mathf.Max(0f, shiftCost);
            return opp - cur - cost;
        }

        // ── 一点集中の過伸張 ──────────────────────────────────────

        /// <summary>一点集中で他正面が手薄になりすぎるリスク（0..1）＝投入割合が閾値を超えた分×感度。</summary>
        public static float OverextensionFromNarrowFocus(float committedFraction)
            => OverextensionFromNarrowFocus(committedFraction, SchwerpunktParams.Default);

        /// <summary>過伸張リスク（0..1）。`overextensionThreshold` 超過分×`overextensionScale` をクランプ。閾値以下は0。</summary>
        public static float OverextensionFromNarrowFocus(float committedFraction, SchwerpunktParams p)
        {
            float over = Mathf.Clamp01(committedFraction) - p.overextensionThreshold;
            if (over <= 0f) return 0f;
            return Mathf.Clamp01(over * p.overextensionScale);
        }

        // ── 決定的突破 ────────────────────────────────────────────

        /// <summary>重点突破が決定的になる度合い（0..1）＝突破力の余剰を敵縦深で割り引く（縦深が浅いほど決定的）。</summary>
        public static float DecisiveBreakthrough(float breakthroughConcentration, float enemyDepth)
            => DecisiveBreakthrough(breakthroughConcentration, enemyDepth, SchwerpunktParams.Default);

        /// <summary>決定的突破度（0..1）＝clamp01(突破力−1)÷(1+max(0,敵縦深))。拮抗以下の突破力は0。</summary>
        public static float DecisiveBreakthrough(float breakthroughConcentration, float enemyDepth, SchwerpunktParams p)
        {
            float surplus = Mathf.Clamp01(Mathf.Max(0f, breakthroughConcentration) - 1f);
            float depth = Mathf.Max(0f, enemyDepth);
            return Mathf.Clamp01(surplus / (1f + depth));
        }

        // ── 決定打の判定 ──────────────────────────────────────────

        /// <summary>重点が決定打になったか＝決定的突破度が既定閾値以上（既定 `DecisiveThreshold`=0.5）。</summary>
        public static bool IsSchwerpunktDecisive(float decisiveBreakthrough)
            => IsSchwerpunktDecisive(decisiveBreakthrough, DecisiveThreshold);

        /// <summary>重点が決定打になったか＝決定的突破度が閾値以上（bool）。</summary>
        public static bool IsSchwerpunktDecisive(float decisiveBreakthrough, float threshold)
            => decisiveBreakthrough >= threshold;

        /// <summary>重点が決定打とみなす決定的突破度の既定閾値。</summary>
        public const float DecisiveThreshold = 0.5f;
    }
}
