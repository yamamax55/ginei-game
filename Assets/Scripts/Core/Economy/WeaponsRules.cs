using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 兵器メーカー（防衛 contractor）のロジック（#2020・純ロジック・唯一の窓口）。兵器を作って軍に納める operate：兵器開発と
    /// 性能（WPN-1）／防衛調達＝コストプラス vs 競争入札（WPN-2）／特需と平時遊休（WPN-3・平和が経営問題に #204）／武器輸出＝
    /// 両陣営商法（WPN-4・#160）／戦力供給＝<see cref="FleetPool"/>(#148)へ（WPN-5）。汎用製造は <see cref="ManufacturerRules"/>(#2016)、
    /// 軍産の政治は <see cref="MilitaryIndustrialRules"/>(#1389/#204)、建艦は #884 へ接続（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class WeaponsRules
    {
        /// <summary>コストプラス契約の利益率（原価への上乗せ）。</summary>
        public const float DefaultCostPlusMargin = 0.15f;

        /// <summary>戦時の需要急増倍率（最大）。</summary>
        public const float DefaultWartimeSurge = 3f;

        // ===== WPN-1 兵器開発と性能 =====

        /// <summary>兵器性能＝基準性能×(1＋R&D水準×1段あたり寄与)（研究開発で火力/装甲が上がる）。</summary>
        public static float WeaponPerformance(float baseSpec, float rdLevel, float gainPerLevel)
            => Mathf.Max(0f, baseSpec) * (1f + Mathf.Max(0f, rdLevel) * Mathf.Max(0f, gainPerLevel));

        /// <summary>開発コスト超過＝見積×(1＋複雑度×超過係数)（高度兵器ほど予算/工期を超過する）。</summary>
        public static float DevelopmentOverrun(float estimatedCost, float complexity, float overrunFactor)
            => Mathf.Max(0f, estimatedCost) * (1f + Mathf.Clamp01(complexity) * Mathf.Max(0f, overrunFactor));

        // ===== WPN-2 防衛調達 =====

        /// <summary>コストプラス価格＝原価×(1＋利益率)（利益が保証され原価削減の動機が弱い＝割高調達の温床）。</summary>
        public static float CostPlusPrice(float productionCost, float margin)
            => Mathf.Max(0f, productionCost) * (1f + Mathf.Max(0f, margin));

        /// <summary>競争入札価格＝原価×(1＋利益率×(1−競争度))（競争が強いほど利益が圧縮される）。</summary>
        public static float CompetitiveBidPrice(float productionCost, float margin, float competition)
            => Mathf.Max(0f, productionCost) * (1f + Mathf.Max(0f, margin) * (1f - Mathf.Clamp01(competition)));

        /// <summary>調達売上＝納入数×単価。</summary>
        public static float ProcurementRevenue(float units, float unitPrice)
            => Mathf.Max(0f, units) * Mathf.Max(0f, unitPrice);

        // ===== WPN-3 特需と平時 =====

        /// <summary>戦時需要＝平時需要×(1＋戦争強度×(急増倍率−1))（戦時に需要が跳ね上がる＝特需）。</summary>
        public static float WartimeDemand(float peacetimeDemand, float warIntensity, float surgeMultiplier)
            => Mathf.Max(0f, peacetimeDemand) * (1f + Mathf.Clamp01(warIntensity) * (Mathf.Max(1f, surgeMultiplier) - 1f));

        /// <summary>平時の遊休コスト＝生産能力×(1−稼働率)×1単位固定費（平時は受注が減り設備が遊ぶ＝平和が経営問題に #204）。</summary>
        public static float PeacetimeIdleCost(float capacity, float utilization, float fixedCostPerUnit)
            => Mathf.Max(0f, capacity) * (1f - Mathf.Clamp01(utilization)) * Mathf.Max(0f, fixedCostPerUnit);

        // ===== WPN-4 武器輸出 =====

        /// <summary>輸出売上＝輸出数×輸出価格。</summary>
        public static float ExportRevenue(float units, float exportPrice)
            => Mathf.Max(0f, units) * Mathf.Max(0f, exportPrice);

        /// <summary>両陣営商法の売上＝(陣営Aへ＋陣営Bへ)×輸出価格（敵味方双方に売る＝フェザーン #160 の商法）。</summary>
        public static float DualUseSales(float unitsToA, float unitsToB, float exportPrice)
            => (Mathf.Max(0f, unitsToA) + Mathf.Max(0f, unitsToB)) * Mathf.Max(0f, exportPrice);

        /// <summary>輸出できるか＝輸出規制下で敵対勢力へは売れない（規制なし or 非敵対なら可）。</summary>
        public static bool CanExport(bool exportRestricted, bool targetIsHostile)
            => !(exportRestricted && targetIsHostile);

        // ===== WPN-5 戦力供給 =====

        /// <summary>兵器の戦力換算＝納入数×(兵器性能/基準性能)（高性能兵器ほど1機あたりの戦力が高い）。基準0以下は0。</summary>
        public static float WeaponStrengthYield(float units, float weaponPerformance, float referencePerformance)
            => referencePerformance <= 0f ? 0f : Mathf.Max(0f, units) * (Mathf.Max(0f, weaponPerformance) / referencePerformance);

        /// <summary>兵器を勢力の戦力プール（#148）へ供給＝建艦と並ぶ戦力調達経路。新しい総プールを返す。</summary>
        public static int CommissionWeapons(Faction faction, int strength)
            => FleetPool.Add(faction, Mathf.Max(0, strength));
    }
}
