using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 産業補助金（純ロジック）。財政支出（補助金）で産業の産出を逓減逓増で押し上げ、
    /// その財政費用とのトレードオフ（純便益）を評価する。
    /// 数式は飽和指数曲線＝補助を増やすほど効果は上限へ漸近する（収穫逓減）。
    /// </summary>
    public static class SubsidyRules
    {
        /// <summary>調整値（補助金効果のチューニング）。</summary>
        public readonly struct Params
        {
            /// <summary>効果係数（大きいほど少額で効く）。</summary>
            public readonly float effectiveness;
            /// <summary>逓減係数（現状は飽和曲線の予備係数として保持）。</summary>
            public readonly float diminishing;
            /// <summary>産出ブースト上限（OutputBoost は最大 1+maxBoost）。</summary>
            public readonly float maxBoost;

            public Params(float effectiveness, float diminishing, float maxBoost)
            {
                this.effectiveness = effectiveness;
                this.diminishing = diminishing;
                this.maxBoost = maxBoost;
            }

            /// <summary>既定値。</summary>
            public static Params Default => new Params(0.01f, 1f, 0.5f);
        }

        /// <summary>
        /// 産出ブースト倍率（>=1）。飽和指数曲線：1 + maxBoost*(1 - exp(-subsidy*effectiveness))。
        /// 補助金は非負へクランプ、結果は [1, 1+maxBoost] に収める（収穫逓減で上限へ漸近）。
        /// </summary>
        public static float OutputBoost(float subsidy, Params p)
        {
            float s = Mathf.Max(0f, subsidy);            // 補助金は非負
            float eff = Mathf.Max(0f, p.effectiveness);
            float maxB = Mathf.Max(0f, p.maxBoost);

            float saturation = 1f - Mathf.Exp(-s * eff); // 0..1 へ漸近
            float boost = 1f + maxB * saturation;
            return Mathf.Clamp(boost, 1f, 1f + maxB);
        }

        /// <summary>既定パラメータでの産出ブースト。</summary>
        public static float OutputBoost(float subsidy)
        {
            return OutputBoost(subsidy, Params.Default);
        }

        /// <summary>
        /// 純便益＝産出増分（outputValue*(boost-1)）から補助金費用を引いた財政トレードオフ。
        /// </summary>
        public static float NetBenefit(float subsidy, float outputValue, Params p)
        {
            float boost = OutputBoost(subsidy, p);
            float gain = outputValue * (boost - 1f);
            return gain - Mathf.Max(0f, subsidy);
        }

        /// <summary>既定パラメータでの純便益。</summary>
        public static float NetBenefit(float subsidy, float outputValue)
        {
            return NetBenefit(subsidy, outputValue, Params.Default);
        }

        /// <summary>補助が割に合うか（純便益が正）。</summary>
        public static bool IsWorthwhile(float subsidy, float outputValue, Params p)
        {
            return NetBenefit(subsidy, outputValue, p) > 0f;
        }

        /// <summary>既定パラメータでの採否判定。</summary>
        public static bool IsWorthwhile(float subsidy, float outputValue)
        {
            return IsWorthwhile(subsidy, outputValue, Params.Default);
        }
    }
}
