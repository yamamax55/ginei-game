using UnityEngine;

namespace Ginei
{
    /// <summary>戦時インフレの調整係数。</summary>
    public readonly struct InflationParams
    {
        /// <summary>増発全開(1.0)が単位時間に物価を押し上げる率（貨幣数量説の簡易係数）。</summary>
        public readonly float printingImpact;
        /// <summary>増発で賄える戦費（見えない税収）の係数（経済規模に掛かる）。</summary>
        public readonly float hiddenTaxScale;
        /// <summary>物価上昇率→不満の非線形指数（1以上。大きいほど急激なインフレが非線形に痛い）。</summary>
        public readonly float discontentExponent;
        /// <summary>物価上昇率→不満のスケール。</summary>
        public readonly float discontentScale;
        /// <summary>これ以上の物価上昇率は制御不能（ハイパーインフレ）とみなす閾値。</summary>
        public readonly float hyperinflationThreshold;

        public InflationParams(float printingImpact, float hiddenTaxScale, float discontentExponent, float discontentScale, float hyperinflationThreshold)
        {
            this.printingImpact = Mathf.Max(0f, printingImpact);
            this.hiddenTaxScale = Mathf.Max(0f, hiddenTaxScale);
            this.discontentExponent = Mathf.Max(1f, discontentExponent);
            this.discontentScale = Mathf.Max(0f, discontentScale);
            this.hyperinflationThreshold = Mathf.Max(0.01f, hyperinflationThreshold);
        }

        /// <summary>既定＝増発影響0.2・見えない税係数0.1・不満指数2・不満スケール4・ハイパー閾値0.5。</summary>
        public static InflationParams Default => new InflationParams(0.2f, 0.1f, 2f, 4f, 0.5f);
    }

    /// <summary>
    /// 戦時インフレの純ロジック（通貨価値の劣化）。通貨増発は今日の戦費を無からひねり出す
    /// 「見えない税」だが、増発が産出成長を超えた分だけ物価が上がり、実質賃金が目減りして
    /// 不満が蓄積する＝刷れば今日は楽になり、明日に痛みが来る。<see cref="FiscalRules"/>
    /// （国債・金利・債務スパイラル＝借金の側）とは別系統で、こちらは貨幣そのものの劣化を扱う。
    /// 倍率は係数として掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InflationRules
    {
        /// <summary>物価水準の下限（デフレでもこれ未満には下がらない）。</summary>
        public const float MinPriceLevel = 0.1f;

        /// <summary>
        /// 物価上昇率（per 単位時間）＝増発(0..1)×影響係数 − 産出成長率(-1..1)。
        /// 増発しても産出がそれ以上に伸びれば物価は上がらない（貨幣数量説の簡易形）。
        /// 負＝デフレもありうる。
        /// </summary>
        public static float InflationRate(float moneyPrinting, float outputGrowth, InflationParams p)
        {
            return Mathf.Clamp01(moneyPrinting) * p.printingImpact - Mathf.Clamp(outputGrowth, -1f, 1f);
        }

        public static float InflationRate(float moneyPrinting, float outputGrowth)
            => InflationRate(moneyPrinting, outputGrowth, InflationParams.Default);

        /// <summary>
        /// 物価水準の1tick後の値＝現在値×(1＋物価上昇率×dt)。下限 <see cref="MinPriceLevel"/> で
        /// クランプ（通貨が無価値でも物価はゼロにならない）。
        /// </summary>
        public static float PriceLevelTick(float priceLevel, float moneyPrinting, float outputGrowth, float dt, InflationParams p)
        {
            float level = Mathf.Max(MinPriceLevel, priceLevel);
            float rate = InflationRate(moneyPrinting, outputGrowth, p);
            return Mathf.Max(MinPriceLevel, level * (1f + rate * Mathf.Max(0f, dt)));
        }

        public static float PriceLevelTick(float priceLevel, float moneyPrinting, float outputGrowth, float dt)
            => PriceLevelTick(priceLevel, moneyPrinting, outputGrowth, dt, InflationParams.Default);

        /// <summary>
        /// 実質賃金係数（0..1）＝名目賃金指数÷物価水準。物価だけが上がれば賃金は実質目減りする。
        /// 1で目減りなし（賃金が物価を上回っても1にクランプ＝目減り係数として使う）。
        /// </summary>
        public static float RealWageFactor(float priceLevel, float nominalWageIndex)
        {
            float level = Mathf.Max(MinPriceLevel, priceLevel);
            float wage = Mathf.Max(0f, nominalWageIndex);
            return Mathf.Clamp01(wage / level);
        }

        /// <summary>
        /// 増発で賄える戦費（見えない税収）＝増発(0..1)×経済規模×係数。
        /// 議会も徴税官も要らない即金＝今日は楽になる側の式。
        /// </summary>
        public static float HiddenTaxRevenue(float moneyPrinting, float economySize, InflationParams p)
        {
            return Mathf.Clamp01(moneyPrinting) * Mathf.Max(0f, economySize) * p.hiddenTaxScale;
        }

        public static float HiddenTaxRevenue(float moneyPrinting, float economySize)
            => HiddenTaxRevenue(moneyPrinting, economySize, InflationParams.Default);

        /// <summary>
        /// 物価上昇率→不満（0..1）＝スケール×上昇率^指数。急激なインフレほど非線形に痛い
        /// （率が2倍なら不満は指数2で4倍）＝明日に来る痛みの側の式。デフレ（負の率）は0。
        /// </summary>
        public static float DiscontentFromInflation(float inflationRate, InflationParams p)
        {
            float rate = Mathf.Max(0f, inflationRate);
            return Mathf.Clamp01(p.discontentScale * Mathf.Pow(rate, p.discontentExponent));
        }

        public static float DiscontentFromInflation(float inflationRate)
            => DiscontentFromInflation(inflationRate, InflationParams.Default);

        /// <summary>ハイパーインフレ（制御不能）か＝物価上昇率が閾値以上。</summary>
        public static bool IsHyperinflation(float inflationRate, float threshold)
        {
            return inflationRate >= Mathf.Max(0.01f, threshold);
        }

        public static bool IsHyperinflation(float inflationRate, InflationParams p)
            => IsHyperinflation(inflationRate, p.hyperinflationThreshold);

        public static bool IsHyperinflation(float inflationRate)
            => IsHyperinflation(inflationRate, InflationParams.Default);
    }
}
