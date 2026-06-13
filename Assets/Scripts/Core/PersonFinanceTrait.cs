using UnityEngine;

namespace Ginei
{
    /// <summary>人物の財産行動特性（PFIN-1・#2056）。貯金型＝堅実に貯める／投資型＝増やすがリスク／浪費型＝使い切るが人望。各々メリット/デメリット。</summary>
    public enum FinancialTrait { 貯金, 投資, 浪費 }

    /// <summary>
    /// 人物の財産行動特性のパラメータ（PFIN-1・#2056・純ロジック・唯一の窓口）。
    /// 可処分所得の配分率（貯蓄/投資/浪費・合計1）＋リスク（投資の変動#185）＋気前（浪費の人望#113）＋破産傾向。各特性のトレードオフを定義。test-first。
    /// </summary>
    public static class FinanceTraitRules
    {
        /// <summary>貯蓄率（貯金型が高い）。</summary>
        public static float SaveRate(FinancialTrait t)
        {
            switch (t) { case FinancialTrait.貯金: return 0.60f; case FinancialTrait.投資: return 0.20f; default: return 0.05f; }
        }

        /// <summary>投資率（投資型が高い）。</summary>
        public static float InvestRate(FinancialTrait t)
        {
            switch (t) { case FinancialTrait.貯金: return 0.10f; case FinancialTrait.投資: return 0.60f; default: return 0.05f; }
        }

        /// <summary>浪費率（浪費型が高い＝可処分の大半を使う）。SaveRate+InvestRate+SpendRate=1。</summary>
        public static float SpendRate(FinancialTrait t)
        {
            switch (t) { case FinancialTrait.貯金: return 0.30f; case FinancialTrait.投資: return 0.20f; default: return 0.90f; }
        }

        /// <summary>投資リスク（投資型が高い＝暴落#185 で財産が毀損しうる）。</summary>
        public static float RiskExposure(FinancialTrait t)
        {
            switch (t) { case FinancialTrait.貯金: return 0.10f; case FinancialTrait.投資: return 0.80f; default: return 0.30f; }
        }

        /// <summary>気前（浪費型が高い＝部下/民へ振る舞い人望#113・貯金型は低い＝ケチ）。</summary>
        public static float Generosity(FinancialTrait t)
        {
            switch (t) { case FinancialTrait.貯金: return 0.20f; case FinancialTrait.投資: return 0.50f; default: return 1.00f; }
        }

        /// <summary>破産傾向（浪費型が高い＝財産が薄く困窮で破産・投資型は暴落で・貯金型は堅実）。</summary>
        public static float RuinPropensity(FinancialTrait t)
        {
            switch (t) { case FinancialTrait.貯金: return 0.10f; case FinancialTrait.投資: return 0.50f; default: return 1.00f; }
        }
    }
}
