using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 鉱山会社（採掘業）のロジック（#2018・純ロジック・唯一の窓口）。有限な鉱床を掘り出す川上 operator：鉱床と埋蔵量＝掘れば
    /// 減る（MIN-1）／採掘と産出＝鉱石×品位＝金属（MIN-2）／品位低下とコスト上昇＝良鉱から掘る・深部化（MIN-3）／探鉱＝リスク
    /// 投資で新規埋蔵量（MIN-4・決定論roll）／収益と枯渇＝価格×産出−コスト・鉱山寿命（MIN-5）。産出資源は <see cref="ResourceStockpile"/>
    /// （#92/#93）・希少資源（#178）・市場（#179）へ接続（read-only/接続のみ）。マクロ近似（坑道 micro は持たない）。test-first。
    /// </summary>
    public static class MiningRules
    {
        /// <summary>既定の品位（鉱石あたり金属含有率＝5%）。</summary>
        public const float DefaultOreGrade = 0.05f;

        // ===== MIN-1 鉱床と埋蔵量 =====

        /// <summary>実際の採掘量＝目標採掘量と埋蔵量の小さい方（埋蔵量を超えては掘れない）。</summary>
        public static float ExtractedOre(float reserves, float targetExtraction)
            => Mathf.Min(Mathf.Max(0f, targetExtraction), Mathf.Max(0f, reserves));

        /// <summary>採掘後の埋蔵量＝埋蔵量−採掘量（非負）。</summary>
        public static float ReservesAfterExtraction(float reserves, float extracted)
            => Mathf.Max(0f, Mathf.Max(0f, reserves) - Mathf.Max(0f, extracted));

        /// <summary>枯渇率＝累積採掘量/初期埋蔵量（1に近いほど掘り尽くした＝品位低下・コスト上昇の進行度）。初期0以下は0。</summary>
        public static float DepletionRatio(float cumulativeExtracted, float initialReserves)
            => initialReserves <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, cumulativeExtracted) / initialReserves);

        /// <summary>枯渇したか＝埋蔵量が尽きた（閉山）。</summary>
        public static bool IsDepleted(float reserves) => reserves <= 0f;

        // ===== MIN-2 採掘と産出 =====

        /// <summary>金属産出＝採掘した鉱石×品位（鉱石のうち金属になる分）。</summary>
        public static float MetalOutput(float oreExtracted, float oreGrade)
            => Mathf.Max(0f, oreExtracted) * Mathf.Clamp01(oreGrade);

        /// <summary>採掘コスト＝採掘量×1単位採掘コスト。</summary>
        public static float ExtractionCost(float oreExtracted, float costPerUnit)
            => Mathf.Max(0f, oreExtracted) * Mathf.Max(0f, costPerUnit);

        // ===== MIN-3 品位低下とコスト上昇 =====

        /// <summary>採掘進行後の品位＝初期品位×(1−枯渇率×低下係数)（良鉱から先に掘るので品位が落ちる）。基準非破壊（新値を返す）。</summary>
        public static float GradeAfterDepletion(float initialGrade, float depletionRatio, float decayFactor)
            => Mathf.Clamp01(initialGrade) * (1f - Mathf.Clamp01(depletionRatio) * Mathf.Clamp01(decayFactor));

        /// <summary>採掘進行後のコスト＝基準コスト×(1＋枯渇率×上昇係数)（深部化・低品位化でコストが上がる）。基準非破壊。</summary>
        public static float CostAfterDepletion(float baseCost, float depletionRatio, float costRiseFactor)
            => Mathf.Max(0f, baseCost) * (1f + Mathf.Clamp01(depletionRatio) * Mathf.Max(0f, costRiseFactor));

        // ===== MIN-4 探鉱 =====

        /// <summary>探鉱成功か＝roll(0..1)が成功確率未満（決定論。当たれば新鉱脈発見）。</summary>
        public static bool ExplorationSuccess(float roll, float successChance)
            => roll < Mathf.Clamp01(successChance);

        /// <summary>発見した埋蔵量＝成功なら探鉱費×1費あたり埋蔵量、失敗なら0（探鉱はリスク投資）。</summary>
        public static float DiscoveredReserves(float explorationSpend, float yieldPerSpend, bool success)
            => success ? Mathf.Max(0f, explorationSpend) * Mathf.Max(0f, yieldPerSpend) : 0f;

        // ===== MIN-5 収益と枯渇 =====

        /// <summary>採掘収益＝金属産出×コモディティ価格（#179）。</summary>
        public static float MiningRevenue(float metalOutput, float commodityPrice)
            => Mathf.Max(0f, metalOutput) * Mathf.Max(0f, commodityPrice);

        /// <summary>採掘利益＝収益−採掘コスト（価格暴落や品位低下で赤字になりうる）。</summary>
        public static float MiningProfit(float metalOutput, float commodityPrice, float oreExtracted, float costPerUnit)
            => MiningRevenue(metalOutput, commodityPrice) - ExtractionCost(oreExtracted, costPerUnit);

        /// <summary>鉱山寿命（年）＝埋蔵量/年間採掘量（あと何年で枯渇するか）。年間採掘0以下は超長寿。</summary>
        public static float MineLifeYears(float reserves, float annualExtraction)
            => annualExtraction <= 0f ? 999999f : Mathf.Max(0f, reserves) / annualExtraction;
    }
}
