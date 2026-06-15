using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 財産の自動取引（資産市場）の純ロジック（#2056/#2063/#2070・唯一の窓口）。所有者が<b>財産特性</b>（<see cref="FinancialTrait"/>）に応じて
    /// 目標投資比率へ向けて資産を売買する＝<b>投資型は余剰資金で買い・浪費型は資産を売って消費・貯金型は現金で保有</b>。各owner の
    /// 「いくら買う/売るか」を決めるだけ（実際の売買は <see cref="AssetTradeRules"/>・決済は <see cref="FundsAccess"/>）。
    /// 集約的な財産ストアで動かし個別注文板へ降りない（タイクン化回避）。基準値非破壊（額を返すのみ）。test-first。
    /// </summary>
    public static class AssetMarketRules
    {
        /// <summary>1年で目標との差を埋める割合（急変を避け滑らかに収束＝階段ジャンプ回避）。</summary>
        public const float MaxRebalanceFraction = 0.5f;

        /// <summary>財産特性ごとの目標投資比率（投資資産/総資産・0..1）＝投資型は高く・貯金型は低く・浪費型はゼロ（売って消費）。</summary>
        public static float TargetInvestmentRatio(FinancialTrait trait)
        {
            switch (trait)
            {
                case FinancialTrait.投資: return 0.6f;
                case FinancialTrait.浪費: return 0.0f;
                default: return 0.15f; // 貯金
            }
        }

        /// <summary>
        /// 目標へ近づける売買額＝(+)買い／(−)売り。総資産（現金＋投資）×目標比率を投資の目標とし、差ぶんを動かす。
        /// 買いは手元現金、売りは投資残高で頭打ち（払えない/持たない以上は動かない）。
        /// </summary>
        public static float RebalanceAmount(FinancialTrait trait, float liquidWealth, float investedValue)
        {
            float liquid = Mathf.Max(0f, liquidWealth);
            float invested = Mathf.Max(0f, investedValue);
            float total = liquid + invested;
            float target = total * TargetInvestmentRatio(trait);
            float delta = target - invested;
            if (delta > 0f) return Mathf.Min(delta, liquid);   // 買い＝手元現金まで
            return Mathf.Max(delta, -invested);                // 売り＝投資残高まで
        }

        /// <summary>1年ぶんの売買額（<see cref="RebalanceAmount"/> を <see cref="MaxRebalanceFraction"/> で間引き＝滑らかに収束）。</summary>
        public static float AnnualRebalance(FinancialTrait trait, float liquidWealth, float investedValue)
            => RebalanceAmount(trait, liquidWealth, investedValue) * MaxRebalanceFraction;
    }
}
