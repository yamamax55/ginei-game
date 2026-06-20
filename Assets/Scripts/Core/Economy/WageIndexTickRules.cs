using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 賃金指数の進行ロジック（内政・労働 #2026/#2034 系）。生産性と労働需給の逼迫で<b>目標賃金</b>が決まり、
    /// 現在の賃金指数がそこへ収束する（差分進行）。賃金は<b>下方硬直</b>（下がりにくい）を持つ＝失業より名目維持が起きやすい。
    /// `Province.wageIndex`（観測層 X が読む）を暦境界Tickで進める前提の集約スカラ。決定論・クランプ・test-first。
    /// </summary>
    public static class WageIndexTickRules
    {
        /// <summary>調整値（マジックナンバー集約）。</summary>
        public readonly struct Params
        {
            public readonly float tightnessWeight; // 需給逼迫(demand-supply)が目標賃金へ寄与する重み
            public readonly float riseSpeed;       // 上昇方向の収束速度（1/Tick基準・×dt）
            public readonly float fallSpeed;       // 下落方向の収束速度（下方硬直＝rise より小）
            public readonly float minIndex;        // 賃金指数の下限
            public readonly float maxIndex;        // 賃金指数の上限

            public Params(float tightnessWeight, float riseSpeed, float fallSpeed, float minIndex, float maxIndex)
            {
                this.tightnessWeight = Mathf.Max(0f, tightnessWeight);
                this.riseSpeed = Mathf.Clamp01(riseSpeed);
                this.fallSpeed = Mathf.Clamp01(fallSpeed);
                this.minIndex = Mathf.Max(0f, minIndex);
                this.maxIndex = Mathf.Max(minIndex, maxIndex);
            }

            /// <summary>既定＝逼迫重み0.5・上昇速度0.3/下落速度0.1（下方硬直）・指数 0.2〜3.0。</summary>
            public static Params Default => new Params(0.5f, 0.3f, 0.1f, 0.2f, 3.0f);
        }

        /// <summary>
        /// 目標賃金指数。生産性 <paramref name="productivity"/>（1.0=標準）を基準に、需給逼迫（需要−供給, 各0..1）で上下。
        /// 需要過多（demand&gt;supply）で目標↑、供給過多で目標↓。
        /// </summary>
        public static float TargetIndex(float productivity, float demand01, float supply01, Params p)
        {
            float tightness = Mathf.Clamp01(demand01) - Mathf.Clamp01(supply01); // -1..1
            float target = Mathf.Max(0f, productivity) * (1f + tightness * p.tightnessWeight);
            return Mathf.Clamp(target, p.minIndex, p.maxIndex);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float TargetIndex(float productivity, float demand01, float supply01)
            => TargetIndex(productivity, demand01, supply01, Params.Default);

        /// <summary>
        /// 賃金指数を目標へ1Tickぶん近づける。上昇は riseSpeed、下落は fallSpeed（下方硬直）で収束。
        /// 返り値＝更新後の指数（min〜max クランプ）。
        /// </summary>
        public static float Tick(float current, float target, float dt, Params p)
        {
            float tgt = Mathf.Clamp(target, p.minIndex, p.maxIndex);
            float speed = (tgt >= current) ? p.riseSpeed : p.fallSpeed;
            float next = Mathf.Lerp(current, tgt, Mathf.Clamp01(speed * Mathf.Max(0f, dt)));
            return Mathf.Clamp(next, p.minIndex, p.maxIndex);
        }

        /// <summary>既定パラメータ版（dt=1Tick）。</summary>
        public static float Tick(float current, float target)
            => Tick(current, target, 1f, Params.Default);
    }
}
