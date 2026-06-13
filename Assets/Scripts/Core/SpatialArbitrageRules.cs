using UnityEngine;

namespace Ginei
{
    /// <summary>空間裁定の調整係数（狼と香辛料型の遠隔地交易）。</summary>
    public readonly struct SpatialArbitrageParams
    {
        /// <summary>裁定取引が1tickで価格差を縮める速さ（0..1、取引量で増幅）。</summary>
        public readonly float convergenceRate;
        /// <summary>市場統合度を1から差し引く際に価格差を正規化する基準（この差で統合度0）。</summary>
        public readonly float integrationReference;

        public SpatialArbitrageParams(float convergenceRate, float integrationReference)
        {
            this.convergenceRate = Mathf.Clamp01(convergenceRate);
            this.integrationReference = Mathf.Max(1e-4f, integrationReference);
        }

        /// <summary>既定＝収束速度0.1/tick・統合度の基準価格差100。</summary>
        public static SpatialArbitrageParams Default => new SpatialArbitrageParams(0.1f, 100f);
    }

    /// <summary>
    /// 空間裁定の純ロジック（#1075・狼と香辛料型の遠隔地交易）。ある星系で安く買い別の星系で高く売る＝
    /// 価格差が裁定取引を呼び、取引が安い方を押し上げ高い方を押し下げる＝市場は一物一価へ向かう。
    /// ただし輸送費を超える価格差がなければ裁定は成立せず（遠隔地交易の条件）、収束しても輸送費ぶんの差は残る
    /// （完全には一致しない）。交易の利得分配（<see cref="TradeRules"/>）・単一市場の需給均衡
    /// （<see cref="MarketRules"/>）・輸送の物理的遮断（<see cref="BlockadeRules"/>）とは別系統＝
    /// 星系間の価格差を裁定が埋める動学。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SpatialArbitrageRules
    {
        /// <summary>二星系の価格差＝高い側−安い側（裁定の源泉）。負（逆転）は0へクランプ。</summary>
        public static float PriceGap(float buyPrice, float sellPrice)
        {
            return Mathf.Max(0f, sellPrice - buyPrice);
        }

        /// <summary>
        /// 裁定の利得＝（価格差−輸送費）×数量。価格差が輸送費を超えてはじめて儲かる（遠隔地交易の条件）。
        /// 輸送費に満たない差は損になるため0でクランプ＝割に合わない取引はしない。
        /// </summary>
        public static float ArbitrageProfit(float priceGap, float transportCost, float quantity)
        {
            float margin = Mathf.Max(0f, priceGap) - Mathf.Max(0f, transportCost);
            return Mathf.Max(0f, margin) * Mathf.Max(0f, quantity);
        }

        /// <summary>
        /// 裁定が成立するか＝輸送費を超える価格差があるか。等しいときは利幅ゼロ＝成立しない
        /// （厳密に超えて初めて儲かる）。
        /// </summary>
        public static bool IsArbitrageViable(float priceGap, float transportCost)
        {
            return Mathf.Max(0f, priceGap) > Mathf.Max(0f, transportCost);
        }

        /// <summary>
        /// 価格収束（1tick）＝裁定取引が安い方を押し上げ高い方を押し下げる（市場は一物一価へ向かう）。
        /// 縮める量は現在の価格差×収束速度×取引量×dt を上限に、両側へ半分ずつ寄せる。
        /// 価格差ゼロ・逆転（buy>=sell）なら変化なし。out で更新後の二価格を返す。
        /// </summary>
        public static void ConvergenceTick(
            float buyPrice, float sellPrice, float tradeVolume, float dt,
            out float newBuyPrice, out float newSellPrice, SpatialArbitrageParams p)
        {
            newBuyPrice = buyPrice;
            newSellPrice = sellPrice;
            float gap = sellPrice - buyPrice;
            if (gap <= 0f) return; // 価格差なし／逆転＝裁定の駆動力なし

            float vol = Mathf.Max(0f, tradeVolume);
            float step = Mathf.Max(0f, dt);
            // 縮める総量＝価格差×速度×取引量×dt（価格差を超えて寄せない＝オーバーシュート防止）
            float close = Mathf.Min(gap, gap * p.convergenceRate * vol * step);
            float half = close * 0.5f;
            newBuyPrice = buyPrice + half;   // 安い方を押し上げる
            newSellPrice = sellPrice - half; // 高い方を押し下げる
        }

        /// <summary>既定パラメータ版の価格収束。</summary>
        public static void ConvergenceTick(
            float buyPrice, float sellPrice, float tradeVolume, float dt,
            out float newBuyPrice, out float newSellPrice)
            => ConvergenceTick(buyPrice, sellPrice, tradeVolume, dt, out newBuyPrice, out newSellPrice, SpatialArbitrageParams.Default);

        /// <summary>
        /// 収束先の価格差＝輸送費ぶん（一物一価へ向かっても輸送費を割る差は裁定が消えるので残る）。
        /// 完全には一致しない遠隔地交易の最終形。
        /// </summary>
        public static float EquilibriumGap(float transportCost)
        {
            return Mathf.Max(0f, transportCost);
        }

        /// <summary>
        /// 市場統合度（0..1）＝星系間の価格差が小さいほど統合された経済圏（交易網の発達）。
        /// 各ペアの「収束先（輸送費）を超える超過価格差」を基準で正規化し、その平均を1から引く。
        /// 超過がゼロ（裁定が出尽くした）なら統合度1。配列長不一致は短い方に合わせる。空は1。
        /// </summary>
        public static float MarketIntegration(float[] priceGaps, float[] transportCosts, SpatialArbitrageParams p)
        {
            if (priceGaps == null || transportCosts == null) return 1f;
            int n = Mathf.Min(priceGaps.Length, transportCosts.Length);
            if (n <= 0) return 1f;

            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                // 輸送費を超える残存価格差だけが未統合の証（輸送費ぶんは正常）
                float excess = Mathf.Max(0f, Mathf.Max(0f, priceGaps[i]) - EquilibriumGap(transportCosts[i]));
                sum += Mathf.Clamp01(excess / p.integrationReference);
            }
            float meanDisparity = sum / n;
            return Mathf.Clamp01(1f - meanDisparity);
        }

        /// <summary>既定パラメータ版の市場統合度。</summary>
        public static float MarketIntegration(float[] priceGaps, float[] transportCosts)
            => MarketIntegration(priceGaps, transportCosts, SpatialArbitrageParams.Default);
    }
}
