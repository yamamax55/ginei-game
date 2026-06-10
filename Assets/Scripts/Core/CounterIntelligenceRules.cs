using UnityEngine;

namespace Ginei
{
    /// <summary>防諜の調整係数。</summary>
    public readonly struct CounterIntelParams
    {
        /// <summary>摘発が敵浸透度を削る速度（per dt・防諜努力1のとき）。</summary>
        public readonly float sweepRate;
        /// <summary>摘発したスパイを二重スパイ化できる基礎成功率。</summary>
        public readonly float turnBaseChance;
        /// <summary>二重スパイ1人が敵の意思決定に注ぐ偽情報バイアスの強さ。</summary>
        public readonly float disinfoPerAgent;
        /// <summary>偽情報バイアスの上限（敵を完全には盲目にできない）。</summary>
        public readonly float maxDisinfoBias;

        public CounterIntelParams(float sweepRate, float turnBaseChance, float disinfoPerAgent, float maxDisinfoBias)
        {
            this.sweepRate = Mathf.Max(0f, sweepRate);
            this.turnBaseChance = Mathf.Clamp01(turnBaseChance);
            this.disinfoPerAgent = Mathf.Max(0f, disinfoPerAgent);
            this.maxDisinfoBias = Mathf.Clamp01(maxDisinfoBias);
        }

        /// <summary>既定＝摘発0.1・転向基礎0.3・偽情報0.15/人・バイアス上限0.6。</summary>
        public static CounterIntelParams Default => new CounterIntelParams(0.1f, 0.3f, 0.15f, 0.6f);
    }

    /// <summary>
    /// 防諜の純ロジック（守りと毒）。敵スパイ網の浸透度を摘発で削り、捕えたスパイは処断でなく
    /// 転向させて二重スパイにできる＝敵の諜報網を逆に偽情報の注入路に変える（敵の推定にバイアスを盛る）。
    /// 自分の諜報網の運用は <see cref="EspionageRules"/>（攻め）が担い、ここは受けと毒。
    /// 乱数は roll で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CounterIntelligenceRules
    {
        /// <summary>
        /// 摘発の1tick後の敵浸透度（0..1）。防諜努力 effort(0..1)×摘発速度×dt で削れる。
        /// 努力ゼロなら浸透は放置される（増殖は敵 `EspionageRules` 側の仕事）。
        /// </summary>
        public static float SweepTick(float penetration, float effort, float dt, CounterIntelParams p)
        {
            float cut = p.sweepRate * Mathf.Clamp01(effort) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(penetration) - cut);
        }

        public static float SweepTick(float penetration, float effort, float dt)
            => SweepTick(penetration, effort, dt, CounterIntelParams.Default);

        /// <summary>
        /// 摘発成功率（0..1）＝防諜努力×敵浸透度。深く食い込んだ網ほど尻尾を出す（接触点が多い）。
        /// roll∈[0,1) 未満で1人摘発。
        /// </summary>
        public static float CatchChance(float penetration, float effort)
        {
            return Mathf.Clamp01(Mathf.Clamp01(effort) * Mathf.Clamp01(penetration));
        }

        /// <summary>摘発判定（決定論）。</summary>
        public static bool CatchesSpy(float penetration, float effort, float roll)
        {
            return roll < CatchChance(penetration, effort);
        }

        /// <summary>
        /// 転向（二重スパイ化）の成功率＝基礎×（1−スパイの忠誠 spyLoyalty(0..1)）。
        /// 忠誠で死ぬスパイは転ばない。roll 未満で転向成立。
        /// </summary>
        public static float TurnChance(float spyLoyalty, CounterIntelParams p)
        {
            return p.turnBaseChance * (1f - Mathf.Clamp01(spyLoyalty));
        }

        public static float TurnChance(float spyLoyalty) => TurnChance(spyLoyalty, CounterIntelParams.Default);

        /// <summary>転向判定（決定論）。</summary>
        public static bool TurnsSpy(float spyLoyalty, float roll, CounterIntelParams p)
        {
            return roll < TurnChance(spyLoyalty, p);
        }

        public static bool TurnsSpy(float spyLoyalty, float roll) => TurnsSpy(spyLoyalty, roll, CounterIntelParams.Default);

        /// <summary>
        /// 偽情報バイアス（0..maxDisinfoBias）＝二重スパイ数×1人あたり強度（上限あり）。
        /// 敵の戦力推定（`ReconRules.EstimateStrength` の roll バイアス等）に注ぐ毒の量。
        /// </summary>
        public static float DisinformationBias(int doubleAgents, CounterIntelParams p)
        {
            return Mathf.Min(p.maxDisinfoBias, Mathf.Max(0, doubleAgents) * p.disinfoPerAgent);
        }

        public static float DisinformationBias(int doubleAgents)
            => DisinformationBias(doubleAgents, CounterIntelParams.Default);
    }
}
