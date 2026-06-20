using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 汚職（腐敗）が公金をかすめ取り、行政効率を損なう純ロジック（唯一の窓口）。
    /// 腐敗が高いほど歳入から中抜き（skim）され、監督（oversight）でそれを抑える。
    /// 自己完結＝float/int のみで他ゲーム型に依存しない。決定論・clamp 済み・test-first。
    /// </summary>
    public static class CorruptionRules
    {
        /// <summary>汚職モデルの調整値。</summary>
        public readonly struct Params
        {
            /// <summary>中抜きの上限割合（0..1）。</summary>
            public readonly float maxSkim;
            /// <summary>腐敗が行政効率を削る強さ（0..1）。</summary>
            public readonly float efficiencyPenalty;
            /// <summary>監督が腐敗を相殺する効き目（0..1）。</summary>
            public readonly float oversightEffect;

            public Params(float maxSkim, float efficiencyPenalty, float oversightEffect)
            {
                this.maxSkim = maxSkim;
                this.efficiencyPenalty = efficiencyPenalty;
                this.oversightEffect = oversightEffect;
            }

            /// <summary>既定値（最大中抜き40%・効率ペナルティ0.5・監督効き目0.8）。</summary>
            public static Params Default => new Params(0.4f, 0.5f, 0.8f);
        }

        /// <summary>
        /// 中抜き率＝maxSkim × clamp01(腐敗 − 監督×oversightEffect)。最終的に [0, maxSkim] へ clamp。
        /// 腐敗が上がるほど増え、監督が上がるほど減る。
        /// </summary>
        public static float SkimRate(float corruption01, float oversight01, Params p)
        {
            // 監督ぶんを差し引いた実効腐敗（0..1）
            float effective = Mathf.Clamp01(corruption01 - oversight01 * p.oversightEffect);
            float skim = p.maxSkim * effective;
            return Mathf.Clamp(skim, 0f, p.maxSkim);
        }

        /// <summary>中抜き率（既定値）。</summary>
        public static float SkimRate(float corruption01, float oversight01)
            => SkimRate(corruption01, oversight01, Params.Default);

        /// <summary>純歳入＝総歳入 × (1 − 中抜き率)。</summary>
        public static float NetRevenue(float grossRevenue, float corruption01, float oversight01, Params p)
            => grossRevenue * (1f - SkimRate(corruption01, oversight01, p));

        /// <summary>純歳入（既定値）。</summary>
        public static float NetRevenue(float grossRevenue, float corruption01, float oversight01)
            => NetRevenue(grossRevenue, corruption01, oversight01, Params.Default);

        /// <summary>行政効率＝clamp01(1 − 腐敗×efficiencyPenalty)。腐敗が上がるほど下がる。</summary>
        public static float AdminEfficiency(float corruption01, Params p)
            => Mathf.Clamp01(1f - corruption01 * p.efficiencyPenalty);

        /// <summary>行政効率（既定値）。</summary>
        public static float AdminEfficiency(float corruption01)
            => AdminEfficiency(corruption01, Params.Default);
    }
}
