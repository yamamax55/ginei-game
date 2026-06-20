using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 交易ネットワークの収益（純ロジック）。2つの経済中枢間の交易収入を
    /// 重力モデル（規模の積／距離減衰）で算出し、開放度（接続性）で増減する。
    /// FiscalRules の tradeIncome を補完する観点だけを担う（FiscalRules は呼ばない）。
    /// </summary>
    public static class TradeNetworkRules
    {
        /// <summary>調整値（重力モデルのチューニング）。</summary>
        public readonly struct Params
        {
            /// <summary>距離減衰係数（大きいほど遠距離で急減）。</summary>
            public readonly float distanceDecay;
            /// <summary>基準収益倍率（規模積へ掛ける係数）。</summary>
            public readonly float baseYield;
            /// <summary>1経路あたり収益の上限。</summary>
            public readonly float maxIncome;

            public Params(float distanceDecay, float baseYield, float maxIncome)
            {
                this.distanceDecay = distanceDecay;
                this.baseYield = baseYield;
                this.maxIncome = maxIncome;
            }

            /// <summary>既定値。</summary>
            public static Params Default => new Params(0.5f, 1f, 1000f);
        }

        /// <summary>
        /// 1経路の交易収入＝重力式。baseYield * sizeA*sizeB / (1+distance*decay) * openness。
        /// 規模/距離/開放度はすべて非負へクランプし、結果は [0, maxIncome] に収める。
        /// </summary>
        public static float RouteIncome(float sizeA, float sizeB, float distance, float openness01, Params p)
        {
            // 負値はゼロ扱い（経済中枢の規模・距離は非負）。
            float a = Mathf.Max(0f, sizeA);
            float b = Mathf.Max(0f, sizeB);
            float dist = Mathf.Max(0f, distance);
            float openness = Mathf.Clamp01(openness01);
            float decay = Mathf.Max(0f, p.distanceDecay);

            float denom = 1f + dist * decay; // 距離減衰（必ず >=1）
            float income = p.baseYield * a * b / denom * openness;
            return Mathf.Clamp(income, 0f, Mathf.Max(0f, p.maxIncome));
        }

        /// <summary>既定パラメータでの1経路収入。</summary>
        public static float RouteIncome(float sizeA, float sizeB, float distance, float openness01)
        {
            return RouteIncome(sizeA, sizeB, distance, openness01, Params.Default);
        }

        /// <summary>
        /// 経路収入リストの総和。負値（不正値）は無視して加算する。
        /// </summary>
        public static float TotalIncome(System.Collections.Generic.IReadOnlyList<float> routeIncomes)
        {
            if (routeIncomes == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < routeIncomes.Count; i++)
            {
                float v = routeIncomes[i];
                if (v > 0f) sum += v; // 負は無視（決定論）
            }
            return sum;
        }
    }
}
