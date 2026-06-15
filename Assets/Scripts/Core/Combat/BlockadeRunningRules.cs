using UnityEngine;

namespace Ginei
{
    /// <summary>封鎖突破（ブロッケードランナー）の調整係数。突破船が包囲網を強行突破して補給/脱出する側の数値モデル。</summary>
    public readonly struct BlockadeRunningParams
    {
        /// <summary>速度優位の効き幅（0.5中立を中心に ±これだけ振れる）。</summary>
        public readonly float speedEdgeScale;
        /// <summary>封鎖の隙を突く際の隠密の重み（0=隙だけ・1=隠密で底上げ大）。</summary>
        public readonly float gapWeight;
        /// <summary>迎撃確率の基礎倍率。</summary>
        public readonly float interceptScale;
        /// <summary>被弾損害の基礎倍率（迎撃確率×火力に乗る）。</summary>
        public readonly float damageScale;
        /// <summary>突破成功度の凹凸（&lt;1で序盤の伸びを早める）。</summary>
        public readonly float breakoutExponent;
        /// <summary>突破を繰り返すと封鎖が締まる度合い（試行1回あたりの締まり）。</summary>
        public readonly float tighteningScale;

        public BlockadeRunningParams(float speedEdgeScale, float gapWeight, float interceptScale,
            float damageScale, float breakoutExponent, float tighteningScale)
        {
            this.speedEdgeScale = Mathf.Clamp(speedEdgeScale, 0f, 0.5f);
            this.gapWeight = Mathf.Clamp01(gapWeight);
            this.interceptScale = Mathf.Clamp(interceptScale, 0f, 2f);
            this.damageScale = Mathf.Clamp(damageScale, 0f, 2f);
            this.breakoutExponent = Mathf.Clamp(breakoutExponent, 0.1f, 2f);
            this.tighteningScale = Mathf.Clamp(tighteningScale, 0f, 1f);
        }

        /// <summary>既定＝速度幅0.5・隠密重み0.5・迎撃1.0・損害0.5・成功凹凸0.5・締まり0.1。</summary>
        public static BlockadeRunningParams Default => new BlockadeRunningParams(0.5f, 0.5f, 1f, 0.5f, 0.5f, 0.1f);
    }

    /// <summary>
    /// 封鎖突破（ブロッケードランナー）の純ロジック。封鎖された星系へ補給を届ける、または包囲から脱出するため、
    /// 封鎖線を強行突破する側の数値モデル。速度・隠密・封鎖の薄い箇所を突くほど成功し、迎撃で損害を受ける。
    /// 突破を繰り返すと封鎖側が穴を塞いで締まる。
    ///
    /// <para>分担：<see cref="BlockadeRules"/>（面の封鎖＝area-denial で補給の通過率を締める側）とは別＝
    /// 本ルールは封鎖を「突破する側」の数値モデル。<see cref="CommerceRaidingRules"/>（通商破壊＝船団を狩る側）とも別＝
    /// 守る/狩る側でなく突破する側。盤面非依存の plain 引数・乱数なし（必要なら roll を渡す）・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。</para>
    /// </summary>
    public static class BlockadeRunningRules
    {
        /// <summary>
        /// 突破側の速度優位（0..1・0.5中立）。突破船が封鎖側より速いほど 0.5 から上振れし、遅いほど下振れする。
        /// 両者0は中立0.5扱い。
        /// </summary>
        public static float RunnerSpeedEdge(float runnerSpeed, float blockaderSpeed, BlockadeRunningParams p)
        {
            float r = Mathf.Max(0f, runnerSpeed);
            float b = Mathf.Max(0f, blockaderSpeed);
            float sum = r + b;
            if (sum <= 0f) return 0.5f;
            float rel = (r - b) / sum;                       // -1..1
            return Mathf.Clamp01(0.5f + p.speedEdgeScale * rel);
        }

        public static float RunnerSpeedEdge(float runnerSpeed, float blockaderSpeed)
            => RunnerSpeedEdge(runnerSpeed, blockaderSpeed, BlockadeRunningParams.Default);

        /// <summary>
        /// 封鎖の薄い箇所を突く度合い（0..1）。封鎖の網羅率 blockadeCoverage(0..1) が低い（穴がある）ほど、
        /// かつ突破船の隠密 runnerStealth(0..1) が高いほど大きい。完全封鎖（coverage=1）では隙ゼロ。
        /// </summary>
        public static float GapExploitation(float blockadeCoverage, float runnerStealth, BlockadeRunningParams p)
        {
            float gap = 1f - Mathf.Clamp01(blockadeCoverage);            // 封鎖の隙
            float stealthTerm = p.gapWeight + (1f - p.gapWeight) * Mathf.Clamp01(runnerStealth);
            return Mathf.Clamp01(gap * stealthTerm);
        }

        public static float GapExploitation(float blockadeCoverage, float runnerStealth)
            => GapExploitation(blockadeCoverage, runnerStealth, BlockadeRunningParams.Default);

        /// <summary>
        /// 迎撃を受ける確率（0..1）。封鎖戦力 blockadeStrength(0..1 規模) が高いほど上がり、
        /// 薄い箇所を突く gapExploitation と速度優位 runnerSpeedEdge が高いほど下がる（すり抜ける）。
        /// </summary>
        public static float InterceptionChance(float blockadeStrength, float gapExploitation, float runnerSpeedEdge, BlockadeRunningParams p)
        {
            float strength = Mathf.Clamp01(blockadeStrength);
            float gap = Mathf.Clamp01(gapExploitation);
            float edge = Mathf.Clamp01(runnerSpeedEdge);
            float chance = p.interceptScale * strength * (1f - gap) * (1f - edge);
            return Mathf.Clamp01(chance);
        }

        public static float InterceptionChance(float blockadeStrength, float gapExploitation, float runnerSpeedEdge)
            => InterceptionChance(blockadeStrength, gapExploitation, runnerSpeedEdge, BlockadeRunningParams.Default);

        /// <summary>
        /// 突破中の被弾損害（兵力/積荷の損耗量）。迎撃確率 interceptionChance × 封鎖側火力 blockaderFirepower に
        /// damageScale を乗じる。迎撃を受けなければ無傷。
        /// </summary>
        public static float RunDamage(float interceptionChance, float blockaderFirepower, BlockadeRunningParams p)
        {
            float ic = Mathf.Clamp01(interceptionChance);
            float fp = Mathf.Max(0f, blockaderFirepower);
            return ic * fp * p.damageScale;
        }

        public static float RunDamage(float interceptionChance, float blockaderFirepower)
            => RunDamage(interceptionChance, blockaderFirepower, BlockadeRunningParams.Default);

        /// <summary>
        /// 届けられた補給量＝積荷 runnerCargo に突破成功度 runSuccess(0..1) を掛けたぶん。
        /// 突破に失敗するほど（成功度が低いほど）届く量が減る。
        /// </summary>
        public static float CargoDelivered(float runnerCargo, float runSuccess)
        {
            return Mathf.Max(0f, runnerCargo) * Mathf.Clamp01(runSuccess);
        }

        /// <summary>
        /// 突破成功度（0..1）。速度優位 runnerSpeedEdge と薄い箇所を突く gapExploitation の平均を
        /// breakoutExponent で凹ませて伸ばし、迎撃確率 interceptionChance のぶんを差し引く。
        /// </summary>
        public static float BreakoutSuccess(float runnerSpeedEdge, float gapExploitation, float interceptionChance, BlockadeRunningParams p)
        {
            float edge = Mathf.Clamp01(runnerSpeedEdge);
            float gap = Mathf.Clamp01(gapExploitation);
            float ic = Mathf.Clamp01(interceptionChance);
            float baseEffort = 0.5f * edge + 0.5f * gap;
            float shaped = Mathf.Pow(Mathf.Clamp01(baseEffort), p.breakoutExponent);
            return Mathf.Clamp01(shaped * (1f - ic));
        }

        public static float BreakoutSuccess(float runnerSpeedEdge, float gapExploitation, float interceptionChance)
            => BreakoutSuccess(runnerSpeedEdge, gapExploitation, interceptionChance, BlockadeRunningParams.Default);

        /// <summary>
        /// 突破を繰り返すと封鎖が締まる（0..1）。封鎖戦力 blockadeStrength を基礎に、これまでの突破試行回数
        /// runnerAttempts に応じて穴が塞がれ網羅率が上がる。
        /// </summary>
        public static float BlockadeTightening(float blockadeStrength, int runnerAttempts, BlockadeRunningParams p)
        {
            float baseCoverage = Mathf.Clamp01(blockadeStrength);
            int attempts = Mathf.Max(0, runnerAttempts);
            return Mathf.Clamp01(baseCoverage + p.tighteningScale * attempts);
        }

        public static float BlockadeTightening(float blockadeStrength, int runnerAttempts)
            => BlockadeTightening(blockadeStrength, runnerAttempts, BlockadeRunningParams.Default);

        /// <summary>突破に成功したか＝突破成功度が閾値 threshold 以上。</summary>
        public static bool IsBlockadeRun(float breakoutSuccess, float threshold)
        {
            return Mathf.Clamp01(breakoutSuccess) >= threshold;
        }
    }
}
