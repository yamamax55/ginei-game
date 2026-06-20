using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 資本投下の仕組み基盤（#917/#269・純ロジック・唯一の窓口）。<b>資本はリターンの高い投資先へ流れ</b>（限界収益で配分）、
    /// <b>投下が生産基盤（資本）を増やす</b>＝蓄積（#269）。資本利潤率 <b>r＝利潤/資本</b>（ピケティ #917 の r）を出す。投資先は操業企業
    /// <see cref="Enterprise"/>（将来は研究/インフラへ一般化）。資本の出所＝国庫（国有 <see cref="PropertyRules"/>）/民間資本/銀行（#186）は接続のみ。
    /// 少数を集約（タイクン化回避＝個別の証券台帳は持たない）。test-first。
    /// </summary>
    public static class CapitalInvestmentRules
    {
        /// <summary>
        /// 資本1単位を投下すると増える売上（限界収益＝雇用×生産性×資本係数×価格）＝<b>投資の魅力度</b>。高いほど資本が集まる。
        /// </summary>
        public static float MarginalReturnOnCapital(Enterprise e, float price)
            => e == null ? 0f
             : Mathf.Max(0f, e.employees) * Mathf.Max(0f, e.productivity) * EnterpriseRules.CapitalOutputWeight * Mathf.Max(0f, price);

        /// <summary>資本利潤率 r＝利潤/資本（ピケティ #917 の r。資本0なら0）。</summary>
        public static float ReturnOnCapital(Enterprise e, float price)
            => e == null || e.capital <= 0f ? 0f : EnterpriseRules.Profit(e, price) / e.capital;

        /// <summary>資本を投下する（生産基盤＝資本を増やす）。投下額を返す。</summary>
        public static float Invest(Enterprise e, float amount)
        {
            if (e == null || amount <= 0f) return 0f;
            e.capital = Mathf.Max(0f, e.capital + amount);
            return amount;
        }

        /// <summary>
        /// 資本プールを限界収益で各投資先へ配分（高リターンへ多く流れる）。全リターン0なら均等配分。戻り値＝各投資先への配分額。
        /// </summary>
        public static float[] AllocateByReturn(float pool, IReadOnlyList<Enterprise> targets, float price)
        {
            int n = targets?.Count ?? 0;
            var alloc = new float[n];
            if (n == 0 || pool <= 0f) return alloc;

            var w = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++) { w[i] = Mathf.Max(0f, MarginalReturnOnCapital(targets[i], price)); total += w[i]; }
            if (total <= 0f) { for (int i = 0; i < n; i++) alloc[i] = pool / n; return alloc; } // 全部0＝均等
            for (int i = 0; i < n; i++) alloc[i] = pool * (w[i] / total);
            return alloc;
        }

        /// <summary>資本プールを配分して実際に投下し、投下総額を返す（資本投下の1ステップ）。</summary>
        public static float DeployAll(float pool, IReadOnlyList<Enterprise> targets, float price)
        {
            float[] alloc = AllocateByReturn(pool, targets, price);
            float deployed = 0f;
            for (int i = 0; i < alloc.Length; i++) deployed += Invest(targets[i], alloc[i]);
            return deployed;
        }
    }
}
