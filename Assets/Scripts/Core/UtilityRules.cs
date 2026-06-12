using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 公益事業（電気・ガス・水道）のロジック（#2021・純ロジック・唯一の窓口）。自然独占と料金規制＝総括原価方式（UTL-1）／
    /// <b>民営か公営かは政体および法律による</b>（UTL-2・<see cref="PropertyRules"/> 連携＋民営化法）／ユニバーサルサービス＝不採算
    /// 地域も供給（UTL-3）／供給と需要・安定供給＝停電/断水（UTL-4）／インフラ投資と料金転嫁（UTL-5）。内政（#109）・家計（#1969）・
    /// 財政（#163）へ接続（read-only/接続のみ）。マクロ近似（送電網 micro は持たない）。test-first。
    /// </summary>
    public static class UtilityRules
    {
        /// <summary>既定の許容利益率（規制資産への適正報酬）。</summary>
        public const float DefaultAllowedReturn = 0.05f;

        /// <summary>自然独占とみなす固定費の閾値（これを超える高固定費は単一供給が効率的）。</summary>
        public const float DefaultMonopolyThreshold = 1000f;

        // ===== UTL-1 自然独占と料金規制（総括原価方式） =====

        /// <summary>総収入要件＝運営費＋規制資産×許容利益率（総括原価方式＝費用を回収し適正報酬を得る）。</summary>
        public static float RevenueRequirement(float operatingCost, float rateBase, float allowedReturnRate)
            => Mathf.Max(0f, operatingCost) + Mathf.Max(0f, rateBase) * Mathf.Max(0f, allowedReturnRate);

        /// <summary>規制単価＝総収入要件/供給量（規制料金。需要家が払う単価）。供給量0以下は0。</summary>
        public static float RegulatedUnitPrice(float revenueRequirement, float volume)
            => volume <= 0f ? 0f : Mathf.Max(0f, revenueRequirement) / volume;

        /// <summary>自然独占か＝固定費が閾値超（高固定費インフラは単一事業者が効率的＝競争より独占＋規制）。</summary>
        public static bool IsNaturalMonopoly(float fixedCost, float threshold) => fixedCost > threshold;

        // ===== UTL-2 民営/公営の決定（★政体＋法律） =====

        /// <summary>
        /// 所有形態＝政体および法律で決まる。共産（<see cref="PropertyRules"/> が国有）は無条件で国有、資本主義は<b>民営化法が許せば私有</b>・
        /// 許さなければ公営（国有）。＝政体だけでなく法制度が所有を決める二段ゲート。
        /// </summary>
        public static Ownership OwnershipFor(string ideology, bool privatizationAllowedByLaw)
        {
            if (PropertyRules.DefaultFor(ideology) == Ownership.国有) return Ownership.国有; // 共産＝国有
            return privatizationAllowedByLaw ? Ownership.私有 : Ownership.国有;             // 資本主義は法律次第
        }

        /// <summary>民営化できるか＝共産でなく、かつ民営化法が許す（政体＋法律の両方が要る）。</summary>
        public static bool CanPrivatize(string ideology, bool privatizationAllowedByLaw)
            => OwnershipFor(ideology, privatizationAllowedByLaw) == Ownership.私有;

        // ===== UTL-3 ユニバーサルサービス =====

        /// <summary>ユニバーサルサービスの赤字＝不採算需要家数×(1人あたり費用−1人あたり収入)（過疎地も供給する義務の負担）。非負。</summary>
        public static float UniversalServiceCost(float unprofitableUsers, float costPerUser, float revenuePerUser)
            => Mathf.Max(0f, unprofitableUsers) * Mathf.Max(0f, costPerUser - revenuePerUser);

        /// <summary>内部補助＝採算地域の黒字−ユニバーサルサービス赤字（儲かる地域が不採算地域を支える）。</summary>
        public static float CrossSubsidy(float profitableSurplus, float universalServiceLoss)
            => profitableSurplus - Mathf.Max(0f, universalServiceLoss);

        // ===== UTL-4 供給と需要・安定供給 =====

        /// <summary>供給できた需要＝需要と供給能力の小さい方（能力を超えては供給できない）。</summary>
        public static float SuppliedDemand(float demand, float capacity)
            => Mathf.Min(Mathf.Max(0f, demand), Mathf.Max(0f, capacity));

        /// <summary>供給不足率＝(需要−能力)/需要（停電/断水の割合）。需要0以下は0。</summary>
        public static float ShortfallRatio(float demand, float capacity)
            => demand <= 0f ? 0f : Mathf.Max(0f, demand - Mathf.Max(0f, capacity)) / demand;

        /// <summary>停電/断水か＝需要が供給能力を超える（安定供給の破綻＝安定度 #109 を蝕む）。</summary>
        public static bool IsBlackout(float demand, float capacity) => demand > capacity;

        // ===== UTL-5 インフラ投資と料金 =====

        /// <summary>投資後の規制資産＝規制資産＋設備投資（投資は規制資産に入り料金に転嫁される）。</summary>
        public static float RateBaseAfterInvestment(float rateBase, float investment)
            => Mathf.Max(0f, rateBase) + Mathf.Max(0f, investment);

        /// <summary>減価償却費＝規制資産×償却率（インフラの老朽化＝運営費に乗り料金を押し上げる）。</summary>
        public static float DepreciationCost(float rateBase, float depreciationRate)
            => Mathf.Max(0f, rateBase) * Mathf.Clamp01(depreciationRate);
    }
}
