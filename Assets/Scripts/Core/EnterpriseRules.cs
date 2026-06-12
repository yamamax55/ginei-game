using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 企業の操業ロジック（#1022・純ロジック・唯一の窓口）。<b>生産＝労働×生産性×資本</b>、<b>利潤＝売上−賃金</b>、
    /// <b>搾取率＝剰余価値/賃金（#271）</b>、<b>蓄積＝利潤の再投資で資本↑（#269）</b>、<b>雇用＝収益性で増減（労働供給に律速）</b>を回す。
    /// 価格は市場（#179）、賃金はPOP（生活水準#181）、利潤は税（#163）・株価（<see cref="Company"/>#185）へ接続する想定（接続のみ）。
    /// マクロ近似（個別会計を持たない＝タイクン化回避）。test-first。
    /// </summary>
    public static class EnterpriseRules
    {
        public const float CapitalOutputWeight = 0.0005f; // 資本が1人あたり産出に寄与する係数
        public const float ReinvestRate = 0.3f;           // 利潤の再投資割合（資本蓄積）
        public const float HireSpeed = 0.3f;              // 雇用が労働需要へ寄る速さ（0..1/tick）
        public const float MinWage = 0.1f;                // 賃金率の下限（ゼロ除算防止）
        public const float MaxLaborSwing = 0.5f;          // 1tickの雇用目標の増減上限（±割合）

        /// <summary>資本集約度（1+資本×係数）＝1人あたり産出の底上げ。</summary>
        public static float CapitalFactor(Enterprise e)
            => e == null ? 1f : 1f + Mathf.Max(0f, e.capital) * CapitalOutputWeight;

        /// <summary>産出＝労働×生産性×資本集約度。</summary>
        public static float Output(Enterprise e)
            => e == null ? 0f : Mathf.Max(0f, e.employees) * Mathf.Max(0f, e.productivity) * CapitalFactor(e);

        /// <summary>売上＝産出×価格（市場 #179）。</summary>
        public static float Revenue(Enterprise e, float price) => Output(e) * Mathf.Max(0f, price);

        /// <summary>賃金総額＝雇用×賃金率（POPへ分配）。</summary>
        public static float WageBill(Enterprise e)
            => e == null ? 0f : Mathf.Max(0f, e.employees) * Mathf.Max(0f, e.wageRate);

        /// <summary>利潤＝売上−賃金（負もありうる）。</summary>
        public static float Profit(Enterprise e, float price) => Revenue(e, price) - WageBill(e);

        /// <summary>搾取率＝剰余価値（利潤）/賃金（#271）。賃金0なら0。</summary>
        public static float ExploitationRate(Enterprise e, float price)
        {
            float wb = WageBill(e);
            return wb <= 0f ? 0f : Profit(e, price) / wb;
        }

        /// <summary>1人雇うと増える売上（限界収益＝生産性×資本集約度×価格）。賃金率を超えれば雇用拡大が儲かる。</summary>
        public static float MarginalRevenuePerWorker(Enterprise e, float price)
            => e == null ? 0f : Mathf.Max(0f, e.productivity) * CapitalFactor(e) * Mathf.Max(0f, price);

        /// <summary>労働需要（望ましい雇用）＝限界収益÷賃金が1超なら増、未満なら減（±<see cref="MaxLaborSwing"/>でクランプ）。</summary>
        public static float LaborDemand(Enterprise e, float price)
        {
            if (e == null) return 0f;
            float wage = Mathf.Max(MinWage, e.wageRate);
            float ratio = MarginalRevenuePerWorker(e, price) / wage;
            float growth = Mathf.Clamp(ratio - 1f, -MaxLaborSwing, MaxLaborSwing);
            return Mathf.Max(0f, e.employees * (1f + growth));
        }

        /// <summary>
        /// 1tick：利潤を再投資して資本↑（蓄積）、雇用を労働需要へ寄せる（<paramref name="availableLabor"/>＝POP工員の供給で雇用増を律速）。
        /// 利潤を返す（税 #163・株価 #185 の素）。<see cref="Enterprise"/> を破壊的に更新。
        /// </summary>
        public static float Tick(Enterprise e, float price, float availableLabor, float dt)
        {
            if (e == null || dt <= 0f) return 0f;
            float profit = Profit(e, price);
            if (profit > 0f) e.capital = Mathf.Max(0f, e.capital + profit * ReinvestRate * dt); // 蓄積

            float demand = LaborDemand(e, price);
            float maxEmployees = e.employees + Mathf.Max(0f, availableLabor); // 採用は供給上限まで
            float target = Mathf.Min(demand, maxEmployees);
            e.employees = Mathf.Max(0f, Mathf.Lerp(e.employees, target, Mathf.Clamp01(HireSpeed * dt)));
            return profit;
        }
    }
}
