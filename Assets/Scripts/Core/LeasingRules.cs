using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// リースのロジック（#1989 LEAS・純ロジック・唯一の窓口）。資産を所有せず使う金融＝リース料は値下がり分＋金利（LEAS-1）、
    /// ファイナンス/オペレーティングで残価リスクの所在が変わり（LEAS-2）、残価損益・中途解約（LEAS-3）、借り手の与信・
    /// 資産回収（LEAS-4）、セール＆リースバック（LEAS-5）、そして<b>戦艦リース</b>＝建造せず戦力を `FleetPool`(#148) へ一時供給
    /// （LEAS-6）。リース料は歳出(#163)、調達は銀行(#1976)/債券(#161)、借り手連鎖は危機(#1939)へ接続。マクロ近似。test-first。
    /// </summary>
    public static class LeasingRules
    {
        /// <summary>既定の金利（リース料に乗る資金コスト）。</summary>
        public const float DefaultInterestRate = 0.05f;

        /// <summary>ファイナンスリースと判定するフルペイアウト率（消化率がこれ以上＝実質割賦購入）。</summary>
        public const float FinanceLeaseThreshold = 0.9f;

        // ===== LEAS-1 リースの基礎 =====

        /// <summary>1期あたりリース料＝(取得原価−残価)/期間＋取得原価×金利（値下がり分の回収＋資金コスト）。期間≤0は0。</summary>
        public static float PeriodicPayment(float assetCost, float residualValue, int termPeriods, float interestRate)
        {
            if (termPeriods <= 0) return 0f;
            float depreciation = (Mathf.Max(0f, assetCost) - Mathf.Max(0f, residualValue)) / termPeriods;
            float interest = Mathf.Max(0f, assetCost) * Mathf.Max(0f, interestRate);
            return depreciation + interest;
        }

        /// <summary>総リース料＝1期リース料×期間。</summary>
        public static float TotalLeaseCost(float periodicPayment, int termPeriods)
            => Mathf.Max(0f, periodicPayment) * Mathf.Max(0, termPeriods);

        /// <summary>リースの割高分＝総リース料−取得原価（所有せず使うぶん金利で割高。購入との差）。</summary>
        public static float LeaseVsBuyPremium(float totalLeaseCost, float assetCost)
            => totalLeaseCost - Mathf.Max(0f, assetCost);

        // ===== LEAS-2 ファイナンス vs オペレーティング =====

        /// <summary>フルペイアウト率＝(取得原価−残価)/取得原価（借り手が払う割合。1に近いほどファイナンス）。原価0以下は0。</summary>
        public static float PayoutRatio(float assetCost, float residualValue)
            => assetCost <= 0f ? 0f : (assetCost - Mathf.Max(0f, residualValue)) / assetCost;

        /// <summary>リース種別を分類＝フルペイアウト率が閾値以上ならファイナンス、未満ならオペレーティング。</summary>
        public static LeaseType ClassifyLease(float assetCost, float residualValue, float financeThreshold)
            => PayoutRatio(assetCost, residualValue) >= financeThreshold ? LeaseType.ファイナンス : LeaseType.オペレーティング;

        /// <summary>残価リスクを借り手が負うか＝ファイナンスリースは借り手（ほぼ全額払う）、オペレーティングは貸し手。</summary>
        public static bool LesseeBearsResidualRisk(LeaseType type) => type == LeaseType.ファイナンス;

        // ===== LEAS-3 残価リスクと中途解約 =====

        /// <summary>残価損益＝実際の売却価格−見込み残価（オペレーティングリースで貸し手が負う・下回れば損）。</summary>
        public static float ResidualGainLoss(float expectedResidual, float actualResale)
            => actualResale - expectedResidual;

        /// <summary>残りリース料＝1期リース料×残り期間。</summary>
        public static float RemainingPayments(float periodicPayment, int remainingPeriods)
            => Mathf.Max(0f, periodicPayment) * Mathf.Max(0, remainingPeriods);

        /// <summary>中途解約違約金＝残りリース料×違約金率（貸し手の逸失を補償）。</summary>
        public static float EarlyTerminationFee(float remainingPayments, float penaltyRate)
            => Mathf.Max(0f, remainingPayments) * Mathf.Clamp01(penaltyRate);

        // ===== LEAS-4 与信と債務不履行 =====

        /// <summary>デフォルト損失＝リース債権残高−回収資産価値（現物を引き揚げて回収できるぶん損失が減る＝リースの担保性）。非負。</summary>
        public static float DefaultLoss(float outstandingReceivable, float recoveredAssetValue)
            => Mathf.Max(0f, Mathf.Max(0f, outstandingReceivable) - Mathf.Max(0f, recoveredAssetValue));

        /// <summary>回収率＝回収資産価値/リース債権残高（0..1+）。債権0以下は0。</summary>
        public static float RecoveryRate(float recoveredAssetValue, float outstandingReceivable)
            => outstandingReceivable <= 0f ? 0f : Mathf.Max(0f, recoveredAssetValue) / outstandingReceivable;

        /// <summary>純与信エクスポージャ＝リース債権残高−残価（残価で担保される＝実質のリスク額）。非負。</summary>
        public static float NetExposure(float outstandingReceivable, float residualValue)
            => Mathf.Max(0f, Mathf.Max(0f, outstandingReceivable) - Mathf.Max(0f, residualValue));

        // ===== LEAS-5 セール・アンド・リースバック =====

        /// <summary>売却で得る現金＝資産の市場価値（売って即現金化＝流動性確保）。</summary>
        public static float LeasebackCashRaised(float assetMarketValue)
            => Mathf.Max(0f, assetMarketValue);

        /// <summary>リースバックの金融コスト＝総リース料−売却額（資産担保の借入と同じ＝使い続ける代償）。</summary>
        public static float LeasebackFinancingCost(float saleProceeds, float totalLeasePayments)
            => totalLeasePayments - Mathf.Max(0f, saleProceeds);

        /// <summary>当面の資金需要を賄えるか＝売却で得る現金が必要額以上（流動性危機の回避）。</summary>
        public static bool IsLiquidityPositive(float saleProceeds, float immediateNeed)
            => Mathf.Max(0f, saleProceeds) >= Mathf.Max(0f, immediateNeed);

        // ===== LEAS-6 戦艦リース =====

        /// <summary>軍艦のリース料（LEAS-1 を流用＝艦価−残価/期間＋金利）。建造せず戦力を借りる代償。</summary>
        public static float WarshipLeasePayment(float shipValue, float residualValue, int termPeriods, float interestRate)
            => PeriodicPayment(shipValue, residualValue, termPeriods, interestRate);

        /// <summary>リース戦力を勢力のプール（#148）へ一時供給＝建造せず戦力が乗る。新しい総プールを返す。</summary>
        public static int CommissionLeasedStrength(Faction faction, int strength)
            => FleetPool.Add(faction, Mathf.Max(0, strength));

        /// <summary>リース終了で戦力を返却＝プール（#148）から除く（所有しないので返す）。新しい総プールを返す。</summary>
        public static int ReturnLeasedStrength(Faction faction, int strength)
            => FleetPool.Add(faction, -Mathf.Max(0, strength));

        /// <summary>買取価格＝残価（リース終了時に買い取って自分のものにする＝以後リース料不要）。</summary>
        public static float BuyoutPrice(float residualValue)
            => Mathf.Max(0f, residualValue);

        /// <summary>建艦との初期費用差＝建造コスト−初回リース料（リースは初期費用が小さい＝薄資本でも戦力を即調達）。</summary>
        public static float LeaseVsBuildInitialSaving(float buildCost, float firstPayment)
            => Mathf.Max(0f, buildCost) - Mathf.Max(0f, firstPayment);
    }
}
