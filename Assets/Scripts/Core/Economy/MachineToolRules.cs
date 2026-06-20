using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 工作機械メーカー（マザーマシン）のロジック（#2023・純ロジック・唯一の窓口）。全製造業の上流＝技術基盤：マザーマシン＝
    /// 工作機械の精度が下流製造の品質上限を決める（MTL-1）／受注産業と受注残＝先行指標（MTL-2）／設備投資循環＝最も volatile
    /// （MTL-3）／精度と数値制御＝R&D（MTL-4）／戦略物資・輸出規制＝高精度機は兵器製造を可能にする（MTL-5）。下流の製造（#2016/
    /// #2020/#2022）へ品質上限を、兵器（#2020）へ製造可否を与える（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class MachineToolRules
    {
        /// <summary>工作機械需要の景気増幅率（建機 #2022 の加速度3よりさらに大きい＝最も volatile な資本財）。</summary>
        public const float DefaultToolAmplification = 5f;

        /// <summary>戦略物資（輸出規制対象）とみなす精度の閾値。</summary>
        public const float DefaultStrategicPrecisionThreshold = 0.9f;

        // ===== MTL-1 マザーマシン =====

        /// <summary>下流製造の品質上限＝基準品質×工作機械の精度（マザーマシンの精度以上の品質は作れない）。</summary>
        public static float ManufacturingQualityCeiling(float toolPrecision, float baseQuality)
            => Mathf.Max(0f, baseQuality) * Mathf.Clamp01(toolPrecision);

        /// <summary>工業基盤の自給率（0..1）＝国産工作機械能力/需要（1で自給＝工業的独立、低いと外国依存）。需要0以下は1。</summary>
        public static float IndustrialBaseFactor(float domesticCapacity, float demand)
            => demand <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, domesticCapacity) / demand);

        // ===== MTL-2 受注産業と受注残 =====

        /// <summary>受注後の受注残＝受注残＋新規受注−生産（受注産業のパイプライン）。非負。</summary>
        public static float BacklogAfterOrders(float backlog, float newOrders, float production)
            => Mathf.Max(0f, Mathf.Max(0f, backlog) + Mathf.Max(0f, newOrders) - Mathf.Max(0f, production));

        /// <summary>ブック・ツー・ビル比＝新規受注/出荷（1超で受注拡大＝景気の先行指標）。出荷0以下は0。</summary>
        public static float BookToBillRatio(float newOrders, float shipments)
            => shipments <= 0f ? 0f : Mathf.Max(0f, newOrders) / shipments;

        /// <summary>納期（受注残/生産速度＝あと何期で捌けるか）。生産速度0以下は超長納期。</summary>
        public static float DeliveryLeadTime(float backlog, float productionRate)
            => productionRate <= 0f ? 999999f : Mathf.Max(0f, backlog) / productionRate;

        // ===== MTL-3 設備投資循環 =====

        /// <summary>
        /// 工作機械需要＝基準需要×(1＋設備投資の伸び率×増幅率)。設備投資の<b>先行指標</b>かつ最も volatile（建機より大きく振れる）。非負。
        /// </summary>
        public static float ToolDemand(float capexGrowthRate, float baseDemand, float amplification)
            => Mathf.Max(0f, Mathf.Max(0f, baseDemand) * (1f + capexGrowthRate * Mathf.Max(0f, amplification)));

        // ===== MTL-4 精度と数値制御 =====

        /// <summary>精度水準＝min(上限, 基準精度×(1＋R&D水準×1段あたり寄与))（数値制御・R&Dで精度が上がる・物理上限あり）。</summary>
        public static float PrecisionLevel(float basePrecision, float rdLevel, float gainPerLevel, float ceiling)
            => Mathf.Min(Mathf.Clamp01(ceiling), Mathf.Clamp01(basePrecision) * (1f + Mathf.Max(0f, rdLevel) * Mathf.Max(0f, gainPerLevel)));

        // ===== MTL-5 戦略物資・輸出規制 =====

        /// <summary>戦略物資か＝精度が閾値超（高精度工作機械は兵器製造を可能にするため輸出規制対象）。</summary>
        public static bool IsStrategicGoods(float precision, float threshold) => precision > threshold;

        /// <summary>輸出できるか＝輸出規制下で戦略物資（高精度）を敵対勢力へは売れない（COCOM/東芝機械事件型）。</summary>
        public static bool CanExport(float precision, float threshold, bool targetIsHostile, bool exportControlled)
            => !(exportControlled && IsStrategicGoods(precision, threshold) && targetIsHostile);

        /// <summary>兵器製造を可能にするか＝工作機械の精度が兵器の要求複雑度以上（高精度機が高度兵器 #2020 の製造を解禁する dual-use）。</summary>
        public static bool WeaponsEnablement(float toolPrecision, float weaponComplexity)
            => toolPrecision >= weaponComplexity;
    }
}
