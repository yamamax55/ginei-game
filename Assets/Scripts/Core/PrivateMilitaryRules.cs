using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 民間軍事会社（PMC・傭兵）のロジック（業種細分化・サービス #2024 の軍事請負サブ業種・#2025・純ロジック・唯一の窓口）：契約収入（PMC-1）／
    /// 戦力供給（PMC-2＝`FleetPool` #148へ傭兵戦力を一時供給＝戦艦リース LEAS-6と同系統）／戦死補償（PMC-3＝リスクコスト）／利益（PMC-4）。
    /// 戦力を金で調達するフェザーン#160的な傭兵業＝薄資本の勢力でも契約で戦力を借りられるが、戦死補償と評判（再契約）がコスト。マクロ近似。test-first。
    /// </summary>
    public static class PrivateMilitaryRules
    {
        /// <summary>契約収入＝展開戦力×日額×契約日数（戦力を貸す期間で課金）。</summary>
        public static float ContractRevenue(float deployedForces, float dailyRate, int days)
            => Mathf.Max(0f, deployedForces) * Mathf.Max(0f, dailyRate) * Mathf.Max(0, days);

        /// <summary>供給戦力＝契約数×1契約あたり戦力（`FleetPool` #148へ傭兵艦隊を一時供給＝戦艦リース LEAS-6と同系統）。</summary>
        public static float MercenaryStrengthSupplied(int contracts, float strengthPerContract)
            => Mathf.Max(0, contracts) * Mathf.Max(0f, strengthPerContract);

        /// <summary>戦死補償＝戦死者数×1人あたり補償（高リスク任務ほど補償が嵩む＝傭兵のコスト）。</summary>
        public static float CasualtyCompensation(int casualties, float compensationPerCasualty)
            => Mathf.Max(0, casualties) * Mathf.Max(0f, compensationPerCasualty);

        /// <summary>PMC利益＝契約収入−人件費−戦死補償−固定費。</summary>
        public static float PmcProfit(float revenue, float personnelCost, float compensation, float fixedCost)
            => revenue - Mathf.Max(0f, personnelCost) - Mathf.Max(0f, compensation) - Mathf.Max(0f, fixedCost);
    }
}
