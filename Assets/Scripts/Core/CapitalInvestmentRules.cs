using UnityEngine;

namespace Ginei
{
    /// <summary>資金調達の出所（#1025）。内部留保＝自前／銀行融資＝負債コスト／増資＝株式希薄化。</summary>
    public enum FinancingSource { 内部留保, 銀行融資, 増資 }

    /// <summary>資本投下の調整値（マジックナンバー禁止＝集約）。</summary>
    public readonly struct CapitalInvestmentParams
    {
        public readonly float riskHurdleSlope;    // リスク1あたりハードルレートに上乗せする要求利回り
        public readonly float overInvestSlack;    // この稼働率までは増設に余裕あり（下回ると遊休リスク）
        public readonly float minConstructionLag; // 建設ラグの下限（年・即時稼働を防ぐ）

        public CapitalInvestmentParams(float riskHurdleSlope, float overInvestSlack, float minConstructionLag)
        {
            this.riskHurdleSlope = Mathf.Max(0f, riskHurdleSlope);
            this.overInvestSlack = Mathf.Clamp01(overInvestSlack);
            this.minConstructionLag = Mathf.Max(0.01f, minConstructionLag);
        }

        /// <summary>既定＝リスク1あたり要求利回り+15%・稼働率0.8まで増設余地・建設ラグ下限1年。</summary>
        public static CapitalInvestmentParams Default => new CapitalInvestmentParams(0.15f, 0.8f, 1.0f);
    }

    /// <summary>
    /// 資本投下・投資判断の純ロジック（#1025）。企業が将来の利益を見込んで設備能力に投資する＝<b>拡大再生産</b>の判断。
    /// 投資はハードルレート（資本コストの壁＝リスクが高いほど高い利回りを要求）を超える利回りでのみ正当化され、
    /// 稼働率が低いのに能力を増やすと<b>過剰投資＝遊休設備</b>を生む。増設は建設ラグで遅れて稼働する。
    /// 分担：<see cref="FirmRules"/>(企業の生産・拡大再生産本体)／<see cref="CapitalRules"/>(ピケティの資本格差・r&gt;g)／
    /// <see cref="BankRules"/>(融資・信用創造)／<see cref="StockMarketRules"/>(増資・株式)とは別＝<b>企業の設備投資判断</b>に特化。
    /// 乱数なし決定論・全入力クランプ。test-first。
    /// </summary>
    public static class CapitalInvestmentRules
    {
        /// <summary>投資利回り＝将来利益の増分／投じた資本（投資1あたり何の利益増を生むか）。コスト0以下は0。</summary>
        public static float ExpectedReturn(float investmentCost, float projectedProfitIncrease)
        {
            if (investmentCost <= 0f) return 0f;
            return Mathf.Max(0f, projectedProfitIncrease) / investmentCost;
        }

        /// <summary>
        /// 投資の可否＝期待利回りが<b>リスク調整ハードルレート</b>を超えるか（資本コストの壁）。
        /// リスクが高いほど要求利回りが上がる＝過剰投資を抑える。
        /// </summary>
        public static bool InvestmentDecision(float expectedReturn, float hurdleRate, float riskLevel, CapitalInvestmentParams p)
        {
            float requiredReturn = RequiredReturn(hurdleRate, riskLevel, p);
            return expectedReturn >= requiredReturn;
        }

        /// <summary>リスク調整後の要求利回り＝ハードルレート＋リスク×係数（リスクが高いほど壁が高い）。</summary>
        public static float RequiredReturn(float hurdleRate, float riskLevel, CapitalInvestmentParams p)
            => Mathf.Max(0f, hurdleRate) + Mathf.Clamp01(riskLevel) * p.riskHurdleSlope;

        /// <summary>回収期間＝投資額／年間キャッシュフロー（何年で元が取れるか）。CF0以下は回収不能＝∞。</summary>
        public static float PaybackPeriod(float investmentCost, float annualCashFlow)
        {
            if (annualCashFlow <= 0f) return float.PositiveInfinity;
            return Mathf.Max(0f, investmentCost) / annualCashFlow;
        }

        /// <summary>
        /// 資金調達の選択。内部留保で足りれば自前＝<see cref="FinancingSource.内部留保"/>。
        /// 不足なら、負債コスト（金利）と株式希薄化を比べ、<b>安い方</b>を選ぶ
        /// （金利が希薄化以下なら融資、希薄化のほうが軽ければ増資）。
        /// </summary>
        public static FinancingSource FinancingChoice(float internalFunds, float investmentCost, float interestRate, float equityDilution)
        {
            if (Mathf.Max(0f, internalFunds) >= Mathf.Max(0f, investmentCost)) return FinancingSource.内部留保;
            return Mathf.Max(0f, interestRate) <= Mathf.Clamp01(equityDilution)
                ? FinancingSource.銀行融資
                : FinancingSource.増資;
        }

        /// <summary>
        /// 過剰投資のリスク 0..1＝稼働率が低いのに能力を増やすほど高い（増えた設備が遊ぶ＝供給過剰）。
        /// 稼働率が <c>overInvestSlack</c> を超えていればリスク0（増設に余地あり）、低いほど計画拡大に比例して上昇。
        /// </summary>
        public static float OverInvestmentRisk(float capacityUtilization, float plannedExpansion, CapitalInvestmentParams p)
        {
            float util = Mathf.Clamp01(capacityUtilization);
            float expansion = Mathf.Max(0f, plannedExpansion);
            if (util >= p.overInvestSlack) return 0f;
            // 余裕(slack)の何割が埋まっていないか × 計画拡大の規模 ＝ 遊休リスク
            float idleShare = (p.overInvestSlack - util) / Mathf.Max(0.0001f, p.overInvestSlack);
            return Mathf.Clamp01(idleShare * expansion);
        }

        /// <summary>
        /// 能力増設の進行＝投資が建設ラグの分だけ遅れて能力に変わる（投資から稼働まで時間がかかる）。
        /// 1tick で <c>investment×dt/lag</c> ぶん能力が増える（ラグが長いほど立ち上がりが遅い）。新しい能力を返す。
        /// </summary>
        public static float CapacityExpansionTick(float capacity, float investment, float constructionLag, float dt, CapitalInvestmentParams p)
        {
            float cap = Mathf.Max(0f, capacity);
            if (dt <= 0f) return cap;
            float lag = Mathf.Max(p.minConstructionLag, constructionLag);
            float added = Mathf.Max(0f, investment) * (dt / lag);
            return cap + added;
        }
    }
}
