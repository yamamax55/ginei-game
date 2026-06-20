using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 社会基盤（インフラ）の進行ロジック（内政・経済の素地）。<see cref="InfrastructureState.level"/> は
    /// <b>投資で上昇・無投資で経年劣化</b>し、整備水準が経済産出・補給効率の倍率（<see cref="OutputFactor"/>）になる。
    /// 暦境界Tick（毎フレームでなく日/月年）で回す前提＝集約スカラ・差分進行。決定論・基準クランプ・test-first。
    /// </summary>
    public static class InfrastructureTickRules
    {
        /// <summary>調整値（マジックナンバー集約）。</summary>
        public readonly struct Params
        {
            public readonly float decayPerTick;       // 無投資時の自然劣化（1Tickあたり level 減）
            public readonly float investEfficiency;   // 投資額→level 上昇の効率
            public readonly float minOutputFactor;    // level=0 のときの産出倍率
            public readonly float maxOutputFactor;    // level=1 のときの産出倍率

            public Params(float decayPerTick, float investEfficiency, float minOutputFactor, float maxOutputFactor)
            {
                this.decayPerTick = Mathf.Max(0f, decayPerTick);
                this.investEfficiency = Mathf.Max(0f, investEfficiency);
                this.minOutputFactor = Mathf.Max(0f, minOutputFactor);
                this.maxOutputFactor = Mathf.Max(minOutputFactor, maxOutputFactor);
            }

            /// <summary>既定＝1Tickで0.5%劣化／投資効率0.001（1000で+1.0）／産出倍率 0.7〜1.3。</summary>
            public static Params Default => new Params(0.005f, 0.001f, 0.7f, 1.3f);
        }

        /// <summary>
        /// 1Tickぶん進める。<paramref name="investment"/>（≥0）ぶん整備が上がり、その後 decay ぶん劣化（差分・0..1クランプ）。
        /// 返り値＝更新後の level。
        /// </summary>
        public static float Tick(InfrastructureState s, float investment, Params p)
        {
            if (s == null) return 0f;
            float lvl = s.level;
            if (investment > 0f) lvl += investment * p.investEfficiency;
            lvl -= p.decayPerTick;
            s.level = Mathf.Clamp01(lvl);
            return s.level;
        }

        /// <summary>既定パラメータ版。</summary>
        public static float Tick(InfrastructureState s, float investment)
            => Tick(s, investment, Params.Default);

        /// <summary>整備水準→経済産出/補給効率の倍率（min〜max を level で線形補間）。</summary>
        public static float OutputFactor(float level, Params p)
            => Mathf.Lerp(p.minOutputFactor, p.maxOutputFactor, Mathf.Clamp01(level));

        /// <summary>既定パラメータ版。</summary>
        public static float OutputFactor(float level)
            => OutputFactor(level, Params.Default);
    }
}
