using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 商社＝総合商社のロジック（FRM-5 #1027 / 事業投資 FRM-7 #1029・純ロジック・唯一の窓口）。仲介と裁定で margin を取り
    /// （TRAD-1）、資源権益で供給を確保し（TRAD-2・#178）、川上に事業投資して束ね（TRAD-3・#1022/#917）、取引先へ与信し
    /// （TRAD-4・#186）、多角化で価格/為替リスクを分散する（TRAD-5）。市場価格（#179）・資源備蓄（<see cref="ResourceStockpile"/>
    /// #92/#93）へ接続（read-only/接続のみ）。在庫・与信・為替リスクを負う独立エージェント。マクロ近似。test-first。
    /// </summary>
    public static class TradingHouseRules
    {
        /// <summary>既定の口銭率（仲介手数料＝取引額の3%）。</summary>
        public const float DefaultCommissionRate = 0.03f;

        /// <summary>既定の権益/事業投資の利回り。</summary>
        public const float DefaultStakeReturnRate = 0.10f;

        /// <summary>既定のトレードファイナンスの利鞘。</summary>
        public const float DefaultTradeCreditSpread = 0.02f;

        // ===== TRAD-1 貿易仲介（裁定＋口銭） =====

        /// <summary>裁定マージン＝(売値−買値)×数量（安く買い高く売る＝自己勘定の儲け。負＝逆ザヤ）。</summary>
        public static float ArbitrageMargin(float buyPrice, float sellPrice, float volume)
            => (sellPrice - buyPrice) * Mathf.Max(0f, volume);

        /// <summary>口銭＝取引額×口銭率（取り次ぎの仲介手数料）。</summary>
        public static float Commission(float tradeValue, float commissionRate)
            => Mathf.Max(0f, tradeValue) * Mathf.Max(0f, commissionRate);

        /// <summary>取引の総利益＝裁定マージン＋口銭（取引額＝売値×数量に口銭率を掛ける）。</summary>
        public static float TradeProfit(float buyPrice, float sellPrice, float volume, float commissionRate)
            => ArbitrageMargin(buyPrice, sellPrice, volume)
             + Commission(sellPrice * Mathf.Max(0f, volume), commissionRate);

        // ===== TRAD-2 資源権益（川上開発・#178） =====

        /// <summary>資源権益のリターン＝出資額×利回り（権益からの取り分・配当）。</summary>
        public static float ResourceStakeReturn(float stakeAmount, float returnRate)
            => Mathf.Max(0f, stakeAmount) * returnRate;

        /// <summary>権益が確保する供給量＝出資額×単位あたり供給（安定調達を握る）。</summary>
        public static float SecuredSupply(float stakeAmount, float supplyPerStake)
            => Mathf.Max(0f, stakeAmount) * Mathf.Max(0f, supplyPerStake);

        /// <summary>確保した供給を勢力の備蓄へ納入（資源権益が <see cref="ResourceStockpile"/> #92/#93 を満たす）。</summary>
        public static void DeliverSecuredSupply(ResourceStockpile into, ResourceType type,
            float stakeAmount, float supplyPerStake, float dt)
        {
            if (into == null || dt <= 0f) return;
            into.Add(type, SecuredSupply(stakeAmount, supplyPerStake) * dt);
        }

        // ===== TRAD-3 事業投資・オーガナイザー（#1022/#917） =====

        /// <summary>事業投資のリターン＝出資額×出資先の資本利潤率（川上企業へ出資して配当を得る）。</summary>
        public static float BusinessStakeReturn(float stakeAmount, float returnOnCapital)
            => Mathf.Max(0f, stakeAmount) * returnOnCapital;

        /// <summary>オーガナイザー価値＝束ねたサプライチェーンの結節数×結節あたりシナジー（川上〜川下を通す価値）。</summary>
        public static float SupplyChainSynergy(int linkedSegments, float synergyPerSegment)
            => Mathf.Max(0, linkedSegments) * Mathf.Max(0f, synergyPerSegment);

        // ===== TRAD-4 トレードファイナンス（#186） =====

        /// <summary>与信収益＝与信残高×利鞘（取引先への信用供与で稼ぐ）。</summary>
        public static float TradeFinanceIncome(float creditOutstanding, float spread)
            => Mathf.Max(0f, creditOutstanding) * Mathf.Max(0f, spread);

        /// <summary>取引先の焦げ付き損失＝与信残高×デフォルト率（与信は信用リスクを伴う）。</summary>
        public static float CounterpartyLoss(float creditOutstanding, float defaultRate)
            => Mathf.Max(0f, creditOutstanding) * Mathf.Clamp01(defaultRate);

        // ===== TRAD-5 ポートフォリオとリスク =====

        /// <summary>在庫評価損益＝在庫×価格変化率（相場・為替が動くと評価損益＝商社の負うリスク）。</summary>
        public static float InventoryPnL(float inventory, float priceChangeRatio)
            => Mathf.Max(0f, inventory) * priceChangeRatio;

        /// <summary>在庫の価格ショックを自己資本へ反映（評価損益を capital に加算）。損益を返す（house を破壊的更新）。</summary>
        public static float ApplyPriceShock(TradingHouse house, float priceChangeRatio)
        {
            if (house == null) return 0f;
            float pnl = InventoryPnL(house.inventory, priceChangeRatio);
            house.capital += pnl;
            return pnl;
        }

        /// <summary>多角化指数（0..1）＝1−ハーフィンダル指数（収益シェアの二乗和）。多くの分野に分散するほど高い＝価格ショックに強い。</summary>
        public static float DiversificationIndex(IReadOnlyList<float> segmentRevenues)
        {
            float total = TotalRevenue(segmentRevenues);
            if (total <= 0f) return 0f;
            float hhi = 0f;
            for (int i = 0; i < segmentRevenues.Count; i++)
            {
                float share = Mathf.Max(0f, segmentRevenues[i]) / total;
                hhi += share * share;
            }
            return Mathf.Clamp01(1f - hhi);
        }

        /// <summary>分野別収益の合計（多角化ポートフォリオの総収益）。</summary>
        public static float TotalRevenue(IReadOnlyList<float> segmentRevenues)
        {
            if (segmentRevenues == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < segmentRevenues.Count; i++) sum += Mathf.Max(0f, segmentRevenues[i]);
            return sum;
        }
    }
}
