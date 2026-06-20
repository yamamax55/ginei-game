using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 労働市場のスラック（失業）と、その社会的影響（不満）を計算する純ロジック。
    /// 労働供給と需要の差から失業率を求め、失業率から社会不満を導く。
    /// 決定論・クランプ済み・後方互換（既定パラメータ）。
    /// </summary>
    public static class UnemploymentRules
    {
        // ゼロ除算回避用の微小値
        private const float Epsilon = 1e-6f;

        /// <summary>失業計算の調整値。</summary>
        public readonly struct Params
        {
            /// <summary>摩擦的失業の下限（不可避な最低失業率・約0.03）。</summary>
            public readonly float frictionalFloor;
            /// <summary>失業率を社会不満へ写す重み（失業が高いほど不満）。</summary>
            public readonly float discontentWeight;

            public Params(float frictionalFloor, float discontentWeight)
            {
                this.frictionalFloor = frictionalFloor;
                this.discontentWeight = discontentWeight;
            }

            /// <summary>既定パラメータ。</summary>
            public static Params Default => new Params(0.03f, 1.5f);
        }

        /// <summary>
        /// 失業率を求める。供給≤0 なら摩擦的下限を返す。
        /// rate = clamp((供給-需要)/供給, frictionalFloor, 1)。
        /// </summary>
        public static float UnemploymentRate(float laborSupply, float laborDemand, Params p)
        {
            // 供給が無ければ算出不能＝下限で扱う
            if (laborSupply <= 0f) return Mathf.Clamp01(p.frictionalFloor);
            float raw = (laborSupply - laborDemand) / laborSupply;
            // 摩擦的下限〜全失業(1)の範囲へ
            return Mathf.Clamp(raw, Mathf.Clamp01(p.frictionalFloor), 1f);
        }

        /// <summary>失業率（既定パラメータ）。</summary>
        public static float UnemploymentRate(float laborSupply, float laborDemand)
            => UnemploymentRate(laborSupply, laborDemand, Params.Default);

        /// <summary>失業率から社会不満を導く（0..1・高失業ほど不満）。</summary>
        public static float Discontent(float unemploymentRate, Params p)
            => Mathf.Clamp01(unemploymentRate * p.discontentWeight);

        /// <summary>社会不満（既定パラメータ）。</summary>
        public static float Discontent(float unemploymentRate)
            => Discontent(unemploymentRate, Params.Default);

        /// <summary>就業率＝1 - 失業率（補完値・0..1）。</summary>
        public static float EmploymentRate(float unemploymentRate)
            => 1f - Mathf.Clamp01(unemploymentRate);
    }
}
