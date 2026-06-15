using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙コンテナターミナル（港湾物流・積替ハブ）のロジック（業種細分化・倉庫運輸 #2024 ／海運 #2024 の港湾サブ業種・#2025・純ロジック・唯一の窓口）：
    /// 荷役処理量（TERM-1）／荷役料収入（TERM-2）／積替比率（TERM-3＝ハブ機能）／利益（TERM-4）。
    /// 海運（#2024）の貨物を積み降ろす港湾オペレーター＝クレーン生産性×処理量が命、積替（トランシップ）比率が高いほどハブ港として栄える。星系のハブ機能（宇宙港#2025の貨物版）。マクロ近似。test-first。
    /// </summary>
    public static class ContainerTerminalRules
    {
        /// <summary>荷役処理量＝クレーン数×1基あたり処理回数×稼働時間（クレーン生産性が処理能力を決める）。</summary>
        public static float Throughput(int cranes, float movesPerCrane, float operatingHours)
            => Mathf.Max(0, cranes) * Mathf.Max(0f, movesPerCrane) * Mathf.Max(0f, operatingHours);

        /// <summary>荷役料収入＝処理コンテナ数×1個あたり荷役料。</summary>
        public static float HandlingRevenue(float containers, float feePerContainer)
            => Mathf.Max(0f, containers) * Mathf.Max(0f, feePerContainer);

        /// <summary>積替比率＝積替コンテナ/総処理（高いほど中継ハブ＝立地が呼ぶ貨物で栄える）。総処理0以下は0。</summary>
        public static float TransshipmentShare(float transshipContainers, float totalContainers)
            => totalContainers <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, transshipContainers) / totalContainers);

        /// <summary>ターミナル利益＝荷役料収入−人件費−設備費−固定費。</summary>
        public static float TerminalProfit(float revenue, float laborCost, float equipmentCost, float fixedCost)
            => revenue - Mathf.Max(0f, laborCost) - Mathf.Max(0f, equipmentCost) - Mathf.Max(0f, fixedCost);
    }
}
