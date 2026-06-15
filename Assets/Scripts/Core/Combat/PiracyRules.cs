using UnityEngine;

namespace Ginei
{
    /// <summary>宇宙海賊の調整係数。</summary>
    public readonly struct PiracyParams
    {
        /// <summary>治安の空白に海賊が湧く速度（per dt・治安ゼロ×流量最大のとき）。</summary>
        public readonly float spawnRate;
        /// <summary>戦利品で海賊が肥える成長率（per dt・略奪成功時）。</summary>
        public readonly float lootGrowthRate;
        /// <summary>討伐が海賊勢力を削る速度（per dt・哨戒努力1のとき）。</summary>
        public readonly float suppressionRate;
        /// <summary>交易被害の上限割合（海賊勢力が哨戒を完全に上回ったとき）。</summary>
        public readonly float maxTradeLoss;

        public PiracyParams(float spawnRate, float lootGrowthRate, float suppressionRate, float maxTradeLoss)
        {
            this.spawnRate = Mathf.Max(0f, spawnRate);
            this.lootGrowthRate = Mathf.Max(0f, lootGrowthRate);
            this.suppressionRate = Mathf.Max(0f, suppressionRate);
            this.maxTradeLoss = Mathf.Clamp01(maxTradeLoss);
        }

        /// <summary>既定＝湧き0.05・略奪成長0.03・討伐0.1・被害上限40%。</summary>
        public static PiracyParams Default => new PiracyParams(0.05f, 0.03f, 0.1f, 0.4f);
    }

    /// <summary>
    /// 宇宙海賊の純ロジック（非国家アクター）。治安（security 0..1）の空白と太い交易流量が海賊を湧かせ、
    /// 略奪に成功するほど肥えて増える＝放置は複利で祟る。哨戒（討伐努力）を割けば削れるが、その戦力は
    /// 前線から抜くことになる＝治安と国防の資源配分問題。国家の通商破壊（<see cref="CommerceRaidingRules"/>）
    /// とは別系統＝こちらは外交で止められない無主の暴力。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PiracyRules
    {
        /// <summary>
        /// 海賊勢力の1tick後の値。治安の空白（1−security）×交易流量(0..1) で湧き、略奪成功度に応じて肥え、
        /// 哨戒努力 patrolEffort(0..1) で削れる。下限0。
        /// </summary>
        public static float PiracyTick(float pirateStrength, float security, float tradeVolume,
                                       float patrolEffort, float dt, PiracyParams p)
        {
            float s = Mathf.Max(0f, pirateStrength);
            float d = Mathf.Max(0f, dt);
            float spawn = p.spawnRate * (1f - Mathf.Clamp01(security)) * Mathf.Clamp01(tradeVolume) * d;
            // 略奪の旨味＝哨戒が薄いほど肥える（既存勢力に比例＝複利）
            float feast = p.lootGrowthRate * s * (1f - Mathf.Clamp01(patrolEffort)) * d;
            float cull = p.suppressionRate * Mathf.Clamp01(patrolEffort) * s * d;
            return Mathf.Max(0f, s + spawn + feast - cull);
        }

        public static float PiracyTick(float pirateStrength, float security, float tradeVolume, float patrolEffort, float dt)
            => PiracyTick(pirateStrength, security, tradeVolume, patrolEffort, dt, PiracyParams.Default);

        /// <summary>
        /// 交易被害率（0..maxTradeLoss）。海賊勢力と哨戒戦力の比で決まり、哨戒ゼロなら勢力に応じて上限まで。
        /// 交易収益に（1−被害率）を掛けて使う。
        /// </summary>
        public static float TradeLossRatio(float pirateStrength, float patrolStrength, PiracyParams p)
        {
            float pir = Mathf.Max(0f, pirateStrength);
            if (pir <= 0f) return 0f;
            float pat = Mathf.Max(0f, patrolStrength);
            float dominance = pir / (pir + pat); // 0..1
            return p.maxTradeLoss * dominance;
        }

        public static float TradeLossRatio(float pirateStrength, float patrolStrength)
            => TradeLossRatio(pirateStrength, patrolStrength, PiracyParams.Default);

        /// <summary>海賊が自然消滅へ向かうか＝湧きと略奪より討伐が強い（哨戒努力がこの閾値を超えている）。</summary>
        public static bool IsContained(float security, float patrolEffort, PiracyParams p)
        {
            // 既存勢力の成長(肥え)と討伐の収支：suppression×effort > lootGrowth×(1−effort)
            float effort = Mathf.Clamp01(patrolEffort);
            return p.suppressionRate * effort > p.lootGrowthRate * (1f - effort);
        }

        public static bool IsContained(float security, float patrolEffort)
            => IsContained(security, patrolEffort, PiracyParams.Default);
    }
}
