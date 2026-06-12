using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// GDP（国内総生産）のロジック（#1951 GDP・純ロジック・唯一の窓口）。経済の規模を測る単一の物差しを三面等価で定義し、
    /// 物価をはがして実質化し、成長率・需給ギャップ・一人当たりを導き、企業（#1022）の付加価値から集計する：
    /// GDP-1 三面等価（生産＝支出＝分配）／GDP-2 名目vs実質（GDPデフレータ）／GDP-3 成長率・潜在・需給ギャップ／
    /// GDP-4 一人当たり／GDP-5 企業からの集計。財政の課税ベース（#163）・テイラー則の需給ギャップ（#1945 CB-1）・
    /// 生活水準（#181）へ接続（read-only/接続のみ）。実効値パターン。マクロ近似。test-first。
    /// </summary>
    public static class GdpRules
    {
        /// <summary>GDPデフレータの基準値（基準年＝100）。</summary>
        public const float DeflatorBase = 100f;

        // ===== GDP-1 三面等価（生産＝支出＝分配） =====

        /// <summary>純輸出＝輸出−輸入（X−M）。マイナス＝貿易赤字。</summary>
        public static float NetExports(GdpAccounts a)
            => a == null ? 0f : a.exports - a.imports;

        /// <summary>支出面GDP＝C＋I＋G＋(X−M)。需要側から測る（名目・現在価格）。</summary>
        public static float ExpenditureGDP(GdpAccounts a)
            => a == null ? 0f : a.consumption + a.investment + a.government + NetExports(a);

        /// <summary>分配面GDP＝雇用者報酬＋営業余剰（利潤）＋純間接税。所得側から測る（三面等価）。</summary>
        public static float IncomeGDP(float compensation, float operatingSurplus, float netIndirectTaxes)
            => compensation + operatingSurplus + netIndirectTaxes;

        /// <summary>付加価値＝産出額−中間投入（二重計算を避けた純生産）。</summary>
        public static float ValueAdded(float output, float intermediateInput)
            => Mathf.Max(0f, output) - Mathf.Max(0f, intermediateInput);

        /// <summary>生産面GDP＝各生産単位の付加価値の合計（供給側から測る・三面等価）。</summary>
        public static float ProductionGDP(IReadOnlyList<float> valueAdded)
        {
            if (valueAdded == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < valueAdded.Count; i++) sum += valueAdded[i];
            return sum;
        }

        // ===== GDP-2 名目 vs 実質（GDPデフレータ） =====

        /// <summary>名目GDP＝支出面GDP（現在価格で評価した経済規模）。</summary>
        public static float NominalGDP(GdpAccounts a) => ExpenditureGDP(a);

        /// <summary>実質GDP＝名目GDP/物価水準（基準1.0）。物価の水増しをはがした本当の生産量。物価0以下は名目をそのまま。</summary>
        public static float RealGDP(float nominalGdp, float priceLevel)
            => priceLevel <= 0f ? nominalGdp : nominalGdp / priceLevel;

        /// <summary>GDPデフレータ＝名目/実質×100（基準=100）。物価の総合指数。実質0以下は0。</summary>
        public static float Deflator(float nominalGdp, float realGdp)
            => realGdp <= 0f ? 0f : nominalGdp / realGdp * DeflatorBase;

        /// <summary>インフレ率＝デフレータの変化率（#1945 CB-2 の貨幣数量説インフレと整合）。前期0以下は0。</summary>
        public static float InflationRate(float currentDeflator, float priorDeflator)
            => priorDeflator <= 0f ? 0f : (currentDeflator - priorDeflator) / priorDeflator;

        // ===== GDP-3 成長率・潜在GDP・需給ギャップ =====

        /// <summary>成長率＝(今期−前期)/前期。実質GDPで測れば景気（物価をはがした実質成長）。前期0以下は0。</summary>
        public static float GrowthRate(float current, float prior)
            => prior <= 0f ? 0f : (current - prior) / prior;

        /// <summary>
        /// 需給ギャップ（GDPギャップ）＝(実際−潜在)/潜在。プラス＝過熱（インフレ圧力）、マイナス＝不況（デフレ圧力）。
        /// 中央銀行のテイラー則（#1945 <see cref="MonetaryPolicyRules.TaylorRate"/> の outputGap）の入力。潜在0以下は0。
        /// </summary>
        public static float OutputGap(float actualReal, float potential)
            => potential <= 0f ? 0f : (actualReal - potential) / potential;

        /// <summary><see cref="GdpAccounts"/> から需給ギャップを直接（実質GDP vs 潜在GDP）。</summary>
        public static float OutputGap(GdpAccounts a)
            => a == null ? 0f : OutputGap(RealGDP(NominalGDP(a), a.priceLevel), a.potentialOutput);

        /// <summary>景気後退か＝マイナス成長（簡易：単期マイナスで判定）。</summary>
        public static bool IsRecession(float growthRate) => growthRate < 0f;

        // ===== GDP-4 一人当たりGDP =====

        /// <summary>一人当たりGDP＝GDP/人口。豊かさの指標（規模でなく水準）。人口0以下は0。</summary>
        public static float PerCapita(float gdp, float population)
            => population <= 0f ? 0f : gdp / population;

        /// <summary>実質一人当たりGDP＝名目を物価ではがして人口で割った実質的な豊かさ。</summary>
        public static float RealPerCapita(float nominalGdp, float priceLevel, float population)
            => PerCapita(RealGDP(nominalGdp, priceLevel), population);

        /// <summary>
        /// 生活水準係数（0..2 目安）＝一人当たりGDP/基準。1.0で標準、上回れば豊か（支持 #113 / 生活水準 #181 へ）。
        /// 実効値パターン（基準非破壊）。基準0以下は1.0。
        /// </summary>
        public static float LivingStandardFactor(float perCapita, float reference)
            => reference <= 0f ? 1f : Mathf.Max(0f, perCapita) / reference;

        // ===== GDP-5 企業からの集計（付加価値の積み上げ） =====

        /// <summary>企業の付加価値＝賃金（雇用者報酬）＋利潤（営業余剰）＝中間投入を除いた純生産（<see cref="EnterpriseRules"/> 委譲）。</summary>
        public static float ValueAddedOf(Enterprise e, float price)
            => EnterpriseRules.WageBill(e) + EnterpriseRules.Profit(e, price);

        /// <summary>企業群の付加価値合計＝生産面GDP（ボトムアップ集計・#1022 から）。一律 price 評価。</summary>
        public static float AggregateValueAdded(IReadOnlyList<Enterprise> enterprises, float price)
        {
            if (enterprises == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < enterprises.Count; i++)
                if (enterprises[i] != null) sum += ValueAddedOf(enterprises[i], price);
            return sum;
        }
    }
}
