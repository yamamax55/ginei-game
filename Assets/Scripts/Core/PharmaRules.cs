using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 製薬会社のロジック（東証33業種「医薬品」・#2024・純ロジック・唯一の窓口）。巨額R&D×低成功率×特許の独占利益×特許切れの
    /// 急落＝医薬品ビジネスの賭け：R&D期待価値（PHM-1）／治験の成功（PHM-2・決定論roll）／特許保護の高利益（PHM-3）／パテント
    /// クリフ＝特許切れでジェネリック侵食（PHM-4）。市場（#179）・支持（健康→#113）へ接続（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class PharmaRules
    {
        /// <summary>ブロックバスター（大型薬）とみなす年間売上の閾値。</summary>
        public const float DefaultBlockbusterThreshold = 1000f;

        // ===== PHM-1 R&D期待価値 =====

        /// <summary>R&Dの期待価値＝ピーク売上×開発成功確率（低確率の巨大リターン＝医薬品R&Dの賭け）。</summary>
        public static float ExpectedRdValue(float peakSales, float successProbability)
            => Mathf.Max(0f, peakSales) * Mathf.Clamp01(successProbability);

        // ===== PHM-2 治験 =====

        /// <summary>治験段階の成功か＝roll(0..1)が段階成功率未満（フェーズを1つ突破。多くは失敗する）。</summary>
        public static bool TrialSuccess(float roll, float phaseSuccessRate)
            => roll < Mathf.Clamp01(phaseSuccessRate);

        // ===== PHM-3 特許保護の高利益 =====

        /// <summary>特許利益＝年間売上×特許マージン（特許で競合を排除し高い利益率を享受）。</summary>
        public static float PatentProfit(float annualSales, float patentMargin)
            => Mathf.Max(0f, annualSales) * Mathf.Clamp01(patentMargin);

        // ===== PHM-4 パテントクリフ =====

        /// <summary>特許切れ後の売上＝特許前売上×(1−ジェネリック侵食率)（特許切れで後発薬に一気に奪われる＝パテントクリフ）。</summary>
        public static float PostPatentSales(float prePatentSales, float genericErosion)
            => Mathf.Max(0f, prePatentSales) * (1f - Mathf.Clamp01(genericErosion));

        /// <summary>ブロックバスターか＝年間売上が閾値超（一社の屋台骨＝特許切れの打撃も大きい）。</summary>
        public static bool IsBlockbuster(float annualSales, float threshold) => annualSales > threshold;
    }
}
