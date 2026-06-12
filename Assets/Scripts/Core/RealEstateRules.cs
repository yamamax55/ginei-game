using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 不動産会社のロジック（#2019・純ロジック・唯一の窓口）。土地・建物を取得し賃貸・売買・開発する：土地と私有可否（RE-1・
    /// <b>政体で土地が私有財産にできるか変わる</b>＝<see cref="PropertyRules"/> 連携）／賃貸収入と利回り（RE-2）／売買と地価（RE-3）／
    /// 開発＝建てて価値を上げる（RE-4）／不動産バブル＝割高→崩壊（RE-5・#1939）。市場（#179）・銀行担保（#186）・家計支出（#1969）へ
    /// 接続（read-only/接続のみ）。マクロ近似（個別物件 micro は持たない）。test-first。
    /// </summary>
    public static class RealEstateRules
    {
        /// <summary>既定の空室率。</summary>
        public const float DefaultVacancyRate = 0.1f;

        /// <summary>バブル判定の価格/賃料倍率の閾値（賃料に比してこれを超える価格は割高＝バブル）。</summary>
        public const float DefaultBubbleThreshold = 25f;

        // ===== RE-1 土地と私有可否（政体） =====

        /// <summary>土地の所有形態＝政体の既定（共産＝国有・他＝私有。<see cref="PropertyRules.DefaultFor"/>）。</summary>
        public static Ownership LandOwnership(string ideology) => PropertyRules.DefaultFor(ideology);

        /// <summary>土地を私有財産にできるか＝政体が私有を許すか（共産は不可＝国有のみ）。</summary>
        public static bool CanPrivatizeLand(string ideology) => LandOwnership(ideology) == Ownership.私有;

        /// <summary>売買可能な私有地の評価額＝私有可なら地価そのまま、共産（国有）では0（私有地として取引できない）。</summary>
        public static float TradableLand(float landValue, string ideology)
            => CanPrivatizeLand(ideology) ? Mathf.Max(0f, landValue) : 0f;

        // ===== RE-2 賃貸収入と利回り =====

        /// <summary>実効賃料＝総賃料×(1−空室率)（空室は賃料を生まない）。</summary>
        public static float EffectiveRent(float grossRent, float vacancyRate)
            => Mathf.Max(0f, grossRent) * (1f - Mathf.Clamp01(vacancyRate));

        /// <summary>純営業収益（NOI）＝実効賃料−運営費（管理・修繕・税）。</summary>
        public static float NetOperatingIncome(float effectiveRent, float operatingExpense)
            => effectiveRent - Mathf.Max(0f, operatingExpense);

        /// <summary>還元利回り（キャップレート）＝NOI/物件価値（高いほど高利回り）。物件価値0以下は0。</summary>
        public static float CapRate(float noi, float propertyValue)
            => propertyValue <= 0f ? 0f : noi / propertyValue;

        /// <summary>収益還元法の物件価値＝NOI/キャップレート（賃料が生む価値）。キャップレート0以下は0。</summary>
        public static float PropertyValueFromCap(float noi, float capRate)
            => capRate <= 0f ? 0f : Mathf.Max(0f, noi) / capRate;

        // ===== RE-3 売買と地価 =====

        /// <summary>売買差益（キャピタルゲイン）＝売却額−取得原価（負＝売却損）。</summary>
        public static float CapitalGain(float saleProceeds, float acquisitionCost)
            => saleProceeds - acquisitionCost;

        /// <summary>地価変動後の評価額＝地価×(1＋上昇率)（負の率で下落）。非負。</summary>
        public static float LandValueAfterAppreciation(float landValue, float appreciationRate)
            => Mathf.Max(0f, Mathf.Max(0f, landValue) * (1f + appreciationRate));

        // ===== RE-4 開発 =====

        /// <summary>開発後の価値＝(地価＋開発費)×(1＋付加価値率)（建物を建てて投じた以上の価値を生む）。</summary>
        public static float DevelopedValue(float landValue, float developmentCost, float uplift)
            => (Mathf.Max(0f, landValue) + Mathf.Max(0f, developmentCost)) * (1f + Mathf.Max(0f, uplift));

        /// <summary>開発利益＝開発後の価値−地価−開発費（付加価値ぶんが儲け）。</summary>
        public static float DevelopmentProfit(float developedValue, float landValue, float developmentCost)
            => developedValue - Mathf.Max(0f, landValue) - Mathf.Max(0f, developmentCost);

        // ===== RE-5 不動産バブル =====

        /// <summary>価格/賃料倍率＝物件価値/年間賃料（高いほど賃料に比して割高＝バブル指標）。賃料0以下は超大。</summary>
        public static float PriceToRentRatio(float propertyValue, float annualRent)
            => annualRent <= 0f ? 999999f : Mathf.Max(0f, propertyValue) / annualRent;

        /// <summary>バブルか＝価格/賃料倍率が閾値超（賃料の裏付けを超えた価格）。</summary>
        public static bool IsBubble(float priceToRentRatio, float threshold) => priceToRentRatio > threshold;

        /// <summary>バブル崩壊の評価損＝物件価値×下落率（地価暴落。銀行担保 #186 を毀損し #1939 の引き金）。非負。</summary>
        public static float BubbleBurstLoss(float propertyValue, float correctionRatio)
            => Mathf.Max(0f, propertyValue) * Mathf.Clamp01(correctionRatio);
    }
}
