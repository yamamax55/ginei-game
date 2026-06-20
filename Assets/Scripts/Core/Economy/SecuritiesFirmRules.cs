using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 証券会社（投資銀行・ブローカー）のロジック（#1963 SEC・純ロジック・唯一の窓口）。発行体と投資家をつなぐ仲介の3業務と
    /// 規制・健全性を回す：SEC-1 ブローカー（委託手数料）／SEC-2 引受（引受料＋売れ残り在庫リスク）／SEC-3 自己売買・
    /// マーケットメイク（スプレッド収益＋在庫評価損益）／SEC-4 自己資本規制（ネット・キャピタル・ルール）／SEC-5 収益と
    /// 金融機関 <see cref="FinancialInstitution"/>(#1939 LEHM)への写像（危機で倒れる投資銀行）。株式市場 #185・債券市場 #161 へ
    /// 接続（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class SecuritiesFirmRules
    {
        /// <summary>既定の委託売買手数料率（取引額の1%）。</summary>
        public const float DefaultCommissionRate = 0.01f;

        /// <summary>既定の引受手数料率（発行額の5%）。</summary>
        public const float DefaultUnderwritingFeeRate = 0.05f;

        /// <summary>既定のビッド・アスク・スプレッド（0.5%）。</summary>
        public const float DefaultBidAskSpread = 0.005f;

        /// <summary>ネット・キャピタル・ルールの最低自己資本比率（在庫リスクに対する自己資本の最低割合＝8%）。</summary>
        public const float MinNetCapitalRatio = 0.08f;

        /// <summary>買取引受の既定消化率（投資家へ売れる割合＝残りは在庫リスク）。</summary>
        public const float DefaultPlacementRatio = 0.9f;

        // ===== SEC-1 ブローカー業務（委託売買手数料） =====

        /// <summary>委託売買手数料＝取引額×手数料率（注文の取り次ぎで稼ぐ）。</summary>
        public static float BrokerageCommission(float tradeVolume, float commissionRate)
            => Mathf.Max(0f, tradeVolume) * Mathf.Max(0f, commissionRate);

        /// <summary>顧客の売買からの手数料収入＝預かり資産×回転率（売買頻度）×手数料率（薄利多売の安定収益）。</summary>
        public static float CommissionFromClients(float clientAssets, float turnoverRate, float commissionRate)
            => Mathf.Max(0f, clientAssets) * Mathf.Max(0f, turnoverRate) * Mathf.Max(0f, commissionRate);

        // ===== SEC-2 引受業務（アンダーライティング） =====

        /// <summary>引受手数料＝発行額×引受料率（IPO/増資 #185・起債 #161 を引き受けて稼ぐ）。</summary>
        public static float UnderwritingFee(float issueAmount, float feeRate)
            => Mathf.Max(0f, issueAmount) * Mathf.Max(0f, feeRate);

        /// <summary>売れ残り在庫＝発行額×(1−消化率)。投資家に売り切れず自社で抱えるぶん＝在庫リスク。</summary>
        public static float UnplacedInventory(float issueAmount, float placementRatio)
            => Mathf.Max(0f, issueAmount) * (1f - Mathf.Clamp01(placementRatio));

        /// <summary>
        /// 引受を実行：引受手数料を自己資本へ加え、売れ残りを在庫へ積む（買取引受の在庫リスク）。引受手数料を返す（firm を破壊的更新）。
        /// </summary>
        public static float Underwrite(SecuritiesFirm firm, float issueAmount, float placementRatio)
        {
            if (firm == null) return 0f;
            float fee = UnderwritingFee(issueAmount, firm.underwritingFeeRate);
            float unplaced = UnplacedInventory(issueAmount, placementRatio);
            firm.capital += fee;
            firm.inventory += unplaced;
            return fee;
        }

        // ===== SEC-3 自己売買・マーケットメイク（ディーラー） =====

        /// <summary>マーケットメイク収益＝売買高×スプレッド（流動性を供給して値差で稼ぐ）。</summary>
        public static float MarketMakingRevenue(float volume, float spread)
            => Mathf.Max(0f, volume) * Mathf.Max(0f, spread);

        /// <summary>在庫評価損益＝在庫×価格変化率（相場が上がれば益・崩れれば損＝投資銀行の急所）。</summary>
        public static float InventoryPnL(float inventory, float priceChangeRatio)
            => Mathf.Max(0f, inventory) * priceChangeRatio;

        /// <summary>在庫の価格ショックを自己資本へ反映（評価損益を capital に加算）。損益を返す（firm を破壊的更新）。</summary>
        public static float ApplyInventoryShock(SecuritiesFirm firm, float priceChangeRatio)
        {
            if (firm == null) return 0f;
            float pnl = InventoryPnL(firm.inventory, priceChangeRatio);
            firm.capital += pnl;
            return pnl;
        }

        // ===== SEC-4 自己資本規制（ネット・キャピタル・ルール） =====

        /// <summary>リスク資産＝在庫（自己売買＋引受の売れ残り＝相場変動にさらされる額）。</summary>
        public static float RiskExposure(SecuritiesFirm firm)
            => firm == null ? 0f : Mathf.Max(0f, firm.inventory);

        /// <summary>所要自己資本＝リスク資産×最低比率（ネット・キャピタル・ルール）。</summary>
        public static float RequiredNetCapital(SecuritiesFirm firm, float minRatio)
            => RiskExposure(firm) * Mathf.Max(0f, minRatio);

        /// <summary>規制を満たすか＝自己資本が所要自己資本以上。</summary>
        public static bool MeetsNetCapital(SecuritiesFirm firm, float minRatio)
            => firm != null && firm.capital >= RequiredNetCapital(firm, minRatio);

        /// <summary>過小資本か＝規制割れ（在庫膨張×薄い自己資本）。</summary>
        public static bool IsUndercapitalized(SecuritiesFirm firm, float minRatio)
            => !MeetsNetCapital(firm, minRatio);

        // ===== SEC-5 収益と健全性（金融機関への橋渡し） =====

        /// <summary>総収益＝委託手数料＋引受料＋マーケットメイク収益（3業務の合算）。</summary>
        public static float Revenue(SecuritiesFirm firm, float brokerVolume, float underwriteAmount, float marketMakeVolume)
        {
            if (firm == null) return 0f;
            return BrokerageCommission(brokerVolume, firm.commissionRate)
                 + UnderwritingFee(underwriteAmount, firm.underwritingFeeRate)
                 + MarketMakingRevenue(marketMakeVolume, firm.bidAskSpread);
        }

        /// <summary>
        /// 証券会社を金融機関（<see cref="FinancialInstitution"/> #1939）へ写像：自己資本＝capital／総資産＝自己資本＋在庫／
        /// 証券化エクスポージャ相当＝在庫（相場ショックで毀損するリスク資産）。投資銀行としてシステミック危機の連鎖に組み込む。
        /// </summary>
        public static FinancialInstitution AsFinancialInstitution(SecuritiesFirm firm)
        {
            if (firm == null) return null;
            float inv = Mathf.Max(0f, firm.inventory);
            return new FinancialInstitution(firm.name, firm.capital, firm.capital + inv, inv,
                interbankLinkage: 0.3f, tooBigToFail: false, faction: firm.faction);
        }
    }
}
