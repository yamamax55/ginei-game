using UnityEngine;

namespace Ginei
{
    /// <summary>封鎖の調整係数（回廊封鎖＝補給線の扼殺）。</summary>
    public readonly struct BlockadeParams
    {
        /// <summary>封鎖が成立し始める戦力比（封鎖側/護衛側）の閾値。これ未満では素通り。</summary>
        public readonly float blockadeThreshold;
        /// <summary>封鎖が完全（throughput=0）になる戦力比。閾値からこの比までで線形に締まる。</summary>
        public readonly float chokeRatio;
        /// <summary>封鎖突破（ブロッケードランナー）の基礎成功率。</summary>
        public readonly float runnerBase;

        public BlockadeParams(float blockadeThreshold, float chokeRatio, float runnerBase)
        {
            this.blockadeThreshold = Mathf.Max(0f, blockadeThreshold);
            this.chokeRatio = Mathf.Max(blockadeThreshold + 0.01f, chokeRatio);
            this.runnerBase = Mathf.Clamp01(runnerBase);
        }

        /// <summary>既定＝封鎖開始比1.0・完全封鎖比3.0・突破基礎0.3。</summary>
        public static BlockadeParams Default => new BlockadeParams(1f, 3f, 0.3f);
    }

    /// <summary>
    /// 回廊封鎖の純ロジック（補給線の扼殺）。要衝に展開した封鎖側戦力が護衛側を上回るほど、通過する補給の
    /// 通過率（throughput 0..1）を締め上げる。封鎖が需要を下回れば被封鎖側は枯渇していく＝戦わずに干上がらせる。
    /// 通商破壊（<see cref="CommerceRaidingRules"/>＝個別船団の迎撃）とは別系統で、面の area-denial を扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BlockadeRules
    {
        /// <summary>封鎖側/護衛側の戦力比。escortStrength=0 は実質無限大（封鎖側がいれば完全封鎖）。</summary>
        public static float Ratio(float blockaderStrength, float escortStrength)
        {
            float b = Mathf.Max(0f, blockaderStrength);
            float e = Mathf.Max(0f, escortStrength);
            if (b <= 0f) return 0f;
            if (e <= 0f) return float.PositiveInfinity;
            return b / e;
        }

        /// <summary>
        /// 補給の通過率（0..1）。戦力比が blockadeThreshold 以下なら 1（素通り）、chokeRatio 以上なら 0（完全封鎖）、
        /// 間は線形に締まる。
        /// </summary>
        public static float Throughput(float blockaderStrength, float escortStrength, BlockadeParams p)
        {
            float ratio = Ratio(blockaderStrength, escortStrength);
            if (ratio <= p.blockadeThreshold) return 1f;
            if (float.IsPositiveInfinity(ratio) || ratio >= p.chokeRatio) return 0f;
            float t = Mathf.InverseLerp(p.blockadeThreshold, p.chokeRatio, ratio);
            return Mathf.Clamp01(1f - t);
        }

        public static float Throughput(float blockaderStrength, float escortStrength)
            => Throughput(blockaderStrength, escortStrength, BlockadeParams.Default);

        /// <summary>封鎖が成立しているか＝戦力比が blockadeThreshold を超え、通過率が 1 未満。</summary>
        public static bool IsBlockaded(float blockaderStrength, float escortStrength, BlockadeParams p)
        {
            return Ratio(blockaderStrength, escortStrength) > p.blockadeThreshold;
        }

        public static bool IsBlockaded(float blockaderStrength, float escortStrength)
            => IsBlockaded(blockaderStrength, escortStrength, BlockadeParams.Default);

        /// <summary>
        /// 被封鎖側の備蓄の純増減（per dt）。実供給＝通過率×supplyFlow が需要 demand を下回る分だけ枯渇する
        /// （負＝減少）。上回れば余剰で増える。
        /// </summary>
        public static float StockpileDelta(float throughput, float supplyFlow, float demand, float dt)
        {
            float delivered = Mathf.Clamp01(throughput) * Mathf.Max(0f, supplyFlow);
            return (delivered - Mathf.Max(0f, demand)) * Mathf.Max(0f, dt);
        }

        /// <summary>
        /// 封鎖突破（ブロッケードランナー）の成功率（0..1）。封鎖が固いほど（低 throughput）難しく、
        /// ランナーの速度・隠密 runnerEvasion(0..1) が成功率を押し上げる。runnerBase を基礎に通過率で底上げ。
        /// </summary>
        public static float RunnerSuccessChance(float throughput, float runnerEvasion, BlockadeParams p)
        {
            float gap = Mathf.Clamp01(throughput);              // 封鎖が緩いほど通りやすい
            float chance = p.runnerBase + (1f - p.runnerBase) * (0.5f * gap + 0.5f * Mathf.Clamp01(runnerEvasion));
            return Mathf.Clamp01(chance);
        }

        public static float RunnerSuccessChance(float throughput, float runnerEvasion)
            => RunnerSuccessChance(throughput, runnerEvasion, BlockadeParams.Default);
    }
}
