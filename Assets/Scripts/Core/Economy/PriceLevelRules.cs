using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 貨幣数量説（MV=PY）を単純化したインフレ／物価水準の純ロジック。
    /// 貨幣供給×流通速度を実質産出で割って均衡物価を求め、現在物価をそこへ収束させる。
    /// 決定論・クランプ済み・後方互換（既定パラメータ）。
    /// </summary>
    public static class PriceLevelRules
    {
        // ゼロ除算回避用の微小値
        private const float Epsilon = 1e-6f;

        /// <summary>物価計算の調整値。</summary>
        public readonly struct Params
        {
            /// <summary>貨幣の流通速度 V。</summary>
            public readonly float velocity;
            /// <summary>均衡への収束速度（1秒あたりの追従割合）。</summary>
            public readonly float adjustSpeed;
            /// <summary>物価の下限。</summary>
            public readonly float minPrice;
            /// <summary>物価の上限。</summary>
            public readonly float maxPrice;

            public Params(float velocity, float adjustSpeed, float minPrice, float maxPrice)
            {
                this.velocity = velocity;
                this.adjustSpeed = adjustSpeed;
                this.minPrice = minPrice;
                this.maxPrice = maxPrice;
            }

            /// <summary>既定パラメータ。</summary>
            public static Params Default => new Params(1f, 0.5f, 0.1f, 100f);
        }

        /// <summary>
        /// 均衡物価 P = (M×V)/Y を求める（実質産出は微小値で下支え）。下限〜上限へクランプ。
        /// </summary>
        public static float EquilibriumPrice(float moneySupply, float realOutput, Params p)
        {
            float y = Mathf.Max(Epsilon, realOutput);
            float price = (moneySupply * p.velocity) / y;
            return Mathf.Clamp(price, p.minPrice, p.maxPrice);
        }

        /// <summary>均衡物価（既定パラメータ）。</summary>
        public static float EquilibriumPrice(float moneySupply, float realOutput)
            => EquilibriumPrice(moneySupply, realOutput, Params.Default);

        /// <summary>
        /// 現在物価を均衡物価へ adjustSpeed×dt ぶん収束させる（Lerp・1ステップで行き過ぎない）。下限〜上限へクランプ。
        /// </summary>
        public static float Tick(float currentPrice, float equilibriumPrice, float dt, Params p)
        {
            float t = Mathf.Clamp01(p.adjustSpeed * dt);
            float next = Mathf.Lerp(currentPrice, equilibriumPrice, t);
            return Mathf.Clamp(next, p.minPrice, p.maxPrice);
        }

        /// <summary>収束計算（既定パラメータ）。</summary>
        public static float Tick(float currentPrice, float equilibriumPrice, float dt)
            => Tick(currentPrice, equilibriumPrice, dt, Params.Default);

        /// <summary>インフレ率＝(新-旧)/旧（旧物価は微小値で下支え）。</summary>
        public static float InflationRate(float oldPrice, float newPrice)
            => (newPrice - oldPrice) / Mathf.Max(Epsilon, oldPrice);
    }
}
