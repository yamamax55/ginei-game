using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// バイオ・遺伝子（先端医療）のロジック（業種細分化・医薬品 #2024 の先端サブ業種・#2025・純ロジック・唯一の窓口）：プラットフォーム技術のライセンスアウト収入（BIO-1）／
    /// 規制承認ゲート（BIO-2＝安全性・有効性の両方が閾値を超えて初めて承認）／遺伝子治療の収入（BIO-3＝超高単価）／利益（BIO-4）。
    /// 医薬品（#2024）の先端版＝基盤技術を他社へ貸す（アップフロント＋マイルストン）モデルと、超高単価の遺伝子治療。承認は安全性×有効性の二重ゲート。マクロ近似。test-first。
    /// </summary>
    public static class BiotechRules
    {
        /// <summary>ライセンスアウト収入＝提携数×(一時金+マイルストン額)（自社の基盤技術を製薬大手へ貸す＝開発を他社資本で進める）。</summary>
        public static float PlatformLicensingRevenue(int partnerships, float upfrontFee, float milestoneValue)
            => Mathf.Max(0, partnerships) * (Mathf.Max(0f, upfrontFee) + Mathf.Max(0f, milestoneValue));

        /// <summary>規制承認ゲート＝安全性・有効性の両方が閾値以上で承認（どちらか欠けると不承認＝二重ゲート）。</summary>
        public static bool RegulatoryApprovalGate(float safetyScore, float efficacyScore, float threshold)
            => safetyScore >= threshold && efficacyScore >= threshold;

        /// <summary>遺伝子治療収入＝患者数×1回あたり治療費（一回完結の超高単価）。</summary>
        public static float GeneticTreatmentRevenue(int patients, float pricePerTreatment)
            => Mathf.Max(0, patients) * Mathf.Max(0f, pricePerTreatment);

        /// <summary>バイオ利益＝収入−研究開発費−製造原価−固定費（巨額R&Dが先行する）。</summary>
        public static float BiotechProfit(float revenue, float rdCost, float manufacturingCost, float fixedCost)
            => revenue - Mathf.Max(0f, rdCost) - Mathf.Max(0f, manufacturingCost) - Mathf.Max(0f, fixedCost);
    }
}
