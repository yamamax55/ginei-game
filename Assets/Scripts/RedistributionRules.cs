using UnityEngine;

namespace Ginei
{
    /// <summary>財政上の階級区分（#163 #162・税負担と社会保障の主体）。社会階級 #110 の簡約。</summary>
    public enum FiscalClass { 富裕層, 中間層, 貧困層 }

    /// <summary>
    /// 税構造（#163 #162・階級別負担）。各層の税率＋人口シェア＋富シェアを持つ。富裕層税率＞貧困層税率＝累進、
    /// 逆＝逆進。再分配（高税高福祉↔低税低福祉）の政治決断の器。解決は <see cref="RedistributionRules"/>。純データ。
    /// </summary>
    [System.Serializable]
    public class TaxStructure
    {
        [Tooltip("各層の税率（0..1）")]
        public float richRate = 0.4f;
        public float middleRate = 0.25f;
        public float poorRate = 0.1f;

        [Tooltip("各層の人口シェア（合計≒1）")]
        public float richShare = 0.1f;
        public float middleShare = 0.4f;
        public float poorShare = 0.5f;

        [Tooltip("各層の富シェア＝課税ベースの分布（合計≒1）")]
        public float richWealth = 0.5f;
        public float middleWealth = 0.35f;
        public float poorWealth = 0.15f;
    }

    /// <summary>
    /// 再分配の純ロジック（#163 #162・唯一の窓口）。税の<b>階級別負担</b>（累進/逆進）と社会保障で「高税高福祉↔低税低福祉」の
    /// 再分配を政治争点にする：累進は貧困層の支持を上げ富裕層の不満を上げ（逆進は逆）、強い再分配は<b>階級対立</b>を生む。
    /// 税収は <see cref="FiscalRules.TaxRevenue"/> へ実効税率を渡して合流。係数は #106・実効値（基準非破壊）。test-first。
    /// </summary>
    public static class RedistributionRules
    {
        public const float SupportSensitivity = 0.5f; // 累進度→各層支持の感度
        public const float TensionScale = 0.5f;        // 再分配の強さ→階級対立

        public static float RateOf(TaxStructure t, FiscalClass c)
        {
            if (t == null) return 0f;
            switch (c) { case FiscalClass.富裕層: return t.richRate; case FiscalClass.貧困層: return t.poorRate; default: return t.middleRate; }
        }

        public static float WealthOf(TaxStructure t, FiscalClass c)
        {
            if (t == null) return 0f;
            switch (c) { case FiscalClass.富裕層: return t.richWealth; case FiscalClass.貧困層: return t.poorWealth; default: return t.middleWealth; }
        }

        /// <summary>累進度＝富裕層税率−貧困層税率（正＝累進・負＝逆進）。</summary>
        public static float Progressivity(TaxStructure t)
            => t == null ? 0f : t.richRate - t.poorRate;

        /// <summary>実効税率＝富で重み付けした平均税率（課税ベースに掛ける）。`FiscalRules.TaxRevenue` へ渡せる。</summary>
        public static float EffectiveTaxRate(TaxStructure t)
        {
            if (t == null) return 0f;
            return Mathf.Clamp01(
                t.richWealth * Mathf.Clamp01(t.richRate)
                + t.middleWealth * Mathf.Clamp01(t.middleRate)
                + t.poorWealth * Mathf.Clamp01(t.poorRate));
        }

        /// <summary>ある層の税収＝課税ベース×その層の富シェア×税率。</summary>
        public static float ClassTax(TaxStructure t, FiscalClass c, float taxBase)
            => Mathf.Max(0f, taxBase) * Mathf.Max(0f, WealthOf(t, c)) * Mathf.Clamp01(RateOf(t, c));

        /// <summary>総税収＝課税ベース×実効税率（各層税収の合計と一致）。</summary>
        public static float TotalTax(TaxStructure t, float taxBase)
            => Mathf.Max(0f, taxBase) * EffectiveTaxRate(t);

        /// <summary>各層の支持変化（累進＝貧困層↑/富裕層↓・逆進＝逆・中間層は小さく逆相関）。支持#113 へ。</summary>
        public static float ClassSupportDelta(FiscalClass c, float progressivity)
        {
            float p = progressivity * SupportSensitivity;
            switch (c)
            {
                case FiscalClass.富裕層: return Mathf.Clamp(-p, -1f, 1f);
                case FiscalClass.貧困層: return Mathf.Clamp(p, -1f, 1f);
                default: return Mathf.Clamp(-Mathf.Abs(p) * 0.2f, -1f, 1f);
            }
        }

        /// <summary>階級対立度（0..1＝累進/逆進が極端なほど分極＝高い）。内部勢力#113・反乱#109 の火種。</summary>
        public static float ClassTension(float progressivity)
            => Mathf.Clamp01(Mathf.Abs(progressivity) * TensionScale);
    }
}
