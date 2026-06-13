using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 企業＝生産主体の純データ（#1024・CAP系）。資本・生産能力・現金・負債を持つミクロの経済主体。
    /// 数値ロジック（生産・損益・稼働率・倒産/撤退判断）は <see cref="FirmRules"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class Firm
    {
        /// <summary>自己資本（蓄積した資本ストック）。</summary>
        public float capital;
        /// <summary>生産能力（フル稼働時の最大生産量）。</summary>
        public float capacity;
        /// <summary>稼働率（0..1＝操業度。需要に応じて調整）。</summary>
        public float capacityUtilization;
        /// <summary>手元現金（利払い・固定費の支払い原資）。</summary>
        public float cash;
        /// <summary>負債（借入残高＝<see cref="BankRules"/> の信用と対）。</summary>
        public float debt;

        public Firm() { }

        public Firm(float capital, float capacity, float cash, float debt)
        {
            this.capital = Mathf.Max(0f, capital);
            this.capacity = Mathf.Max(0f, capacity);
            this.cash = Mathf.Max(0f, cash);
            this.debt = Mathf.Max(0f, debt);
            this.capacityUtilization = 1f;
        }
    }

    /// <summary>
    /// 企業の生産・退出を支配するミクロ経済ロジック（#1024・純ロジック test-first・唯一の窓口）。
    /// 「企業は需要に応じて操業し、儲からなければ退出する＝経済を動かすミクロの主体」を式に出す。
    /// <para>分担：<see cref="StockMarketRules"/>(株価・市場での企業評価)／<see cref="MarketRules"/>(財の需給・均衡価格＝企業が直面する需要と価格)
    /// ／<see cref="IncomeStatementRules"/>(#976 P&amp;L＝全社の損益計算書。本クラスの <see cref="OperatingProfit"/> はその営業利益段へ接続)
    /// ／<see cref="CapitalInvestmentRules"/>(設備投資＝生産能力の増減。同Wave並行＝本クラスは所与の能力を運用する側)
    /// ／<see cref="BankRules"/>(信用・取付け＝本クラスの倒産リスク <see cref="SolvencyRisk"/> はその与信判断へ接続)。
    /// 本クラスは「1社が需要を見て稼働を決め、損益を出し、生き残るか退出するか」のミクロ主体に専念する。</para>
    /// 調整値は <see cref="FirmParams"/> に集約（既定 <see cref="FirmParams.Default"/>）。全入力クランプ・乱数なし決定論。
    /// </summary>
    public static class FirmRules
    {
        /// <summary>企業判断の調整値（撤退/倒産の閾値・赤字耐性）。</summary>
        public readonly struct FirmParams
        {
            /// <summary>撤退判断の営業赤字許容率（固定費に対する営業損失がこの倍率を超え続けたら退出）。</summary>
            public readonly float exitLossRatio;
            /// <summary>倒産判断のカバレッジ閾値（現金/利払いがこれ未満＝利払いを賄えず破綻）。</summary>
            public readonly float solvencyCoverage;
            /// <summary>稼働率の下限（需要が枯れても固定費維持のため最低限回す操業度＝0で完全停止可）。</summary>
            public readonly float minUtilization;

            public FirmParams(float exitLossRatio, float solvencyCoverage, float minUtilization)
            {
                this.exitLossRatio = Mathf.Max(0f, exitLossRatio);
                this.solvencyCoverage = Mathf.Max(0f, solvencyCoverage);
                this.minUtilization = Mathf.Clamp01(minUtilization);
            }

            /// <summary>
            /// 既定＝撤退赤字許容率0.5（固定費の半分超の営業損失で退出）・倒産カバレッジ1.0（現金が利払い未満で破綻）・最低稼働率0.1。
            /// </summary>
            public static FirmParams Default => new FirmParams(0.5f, 1f, 0.1f);
        }

        /// <summary>
        /// 生産量を算出する純関数（#1024）＝生産能力×稼働率。負入力はクランプ・稼働率0..1。
        /// 遊休（稼働率&lt;1）は能力を使い切らない＝需要に応じて絞った結果（<see cref="CapacityUtilizationDecision"/>）。
        /// </summary>
        public static float ProductionOutput(float capacity, float capacityUtilization)
        {
            float cap = Mathf.Max(0f, capacity);
            float util = Mathf.Clamp01(capacityUtilization);
            return cap * util;
        }

        /// <summary>
        /// 売上を算出する純関数（#1024）＝生産量×単価。負入力はクランプ。
        /// 単価は <see cref="MarketRules"/> の均衡価格（企業が直面する市場価格）を想定。
        /// </summary>
        public static float Revenue(float output, float unitPrice)
        {
            float o = Mathf.Max(0f, output);
            float price = Mathf.Max(0f, unitPrice);
            return o * price;
        }

        /// <summary>
        /// 営業利益を算出する純関数（#1024）＝売上−変動費−固定費。
        /// <see cref="IncomeStatementRules"/>(#976 P&amp;L) の営業利益段へ接続する（利払い前・税前）。
        /// 売上・費用は非負クランプ、結果は赤字（負）を許容＝退出/倒産判断の入力。
        /// </summary>
        public static float OperatingProfit(float revenue, float variableCost, float fixedCost)
        {
            float rev = Mathf.Max(0f, revenue);
            float vc = Mathf.Max(0f, variableCost);
            float fc = Mathf.Max(0f, fixedCost);
            return rev - vc - fc; // 赤字許容
        }

        /// <summary>
        /// 利益率を算出する純関数（#1024）＝営業利益/売上。売上0は0（評価不能＝中立）。利益（負）は許容。
        /// </summary>
        public static float ProfitMargin(float operatingProfit, float revenue)
        {
            float rev = Mathf.Max(0f, revenue);
            if (rev <= 0f) return 0f; // 売上なし＝評価不能
            return operatingProfit / rev;
        }

        /// <summary>
        /// 稼働率の決定（#1024）＝需要に応じて操業度を調整する純関数。
        /// 市場需要が生産能力を下回れば遊休（需要/能力）、上回れば上限1.0でフル稼働。
        /// 需要が枯れても固定費維持のため <see cref="FirmParams.minUtilization"/> を下限に保つ。
        /// 能力0は0（操業不能）。「企業は売れる分だけ作る＝過剰能力は遊休する」を式に出す。
        /// </summary>
        public static float CapacityUtilizationDecision(float marketDemand, float capacity, FirmParams p)
        {
            float cap = Mathf.Max(0f, capacity);
            if (cap <= 0f) return 0f; // 能力なし＝操業不能
            float demand = Mathf.Max(0f, marketDemand);
            float util = demand / cap; // 需要が能力を下回れば1未満＝遊休
            util = Mathf.Clamp01(util);
            return Mathf.Max(p.minUtilization, util); // 固定費維持の最低稼働
        }

        /// <summary>
        /// 倒産リスク(0..1)を算出する純関数（#1024）：現金が利払い（debtService）を賄えないほど高い。
        /// カバレッジ＝現金/利払い。<see cref="FirmParams.solvencyCoverage"/> 以上なら破綻なし(0)、
        /// それ未満で線形に上昇し、利払いを全く賄えない（現金0）なら1。利払い0は債務不履行なし(0)。
        /// <see cref="BankRules"/> の与信・取付け判断へ接続する。
        /// </summary>
        public static float SolvencyRisk(float cash, float debt, float debtService)
        {
            float ds = Mathf.Max(0f, debtService);
            if (ds <= 0f) return 0f; // 返済義務なし＝破綻なし
            float c = Mathf.Max(0f, cash);
            float coverage = c / ds; // 1以上で利払いを賄える
            if (coverage >= 1f) return 0f; // 余裕＝破綻なし
            return Mathf.Clamp01(1f - coverage); // 賄えない不足分がそのままリスク
        }

        /// <summary>
        /// 撤退判断（#1024）＝営業赤字が固定費に対して許容率を超えたら市場退出する純関数。
        /// 営業利益が <c>-fixedCost × exitLossRatio</c> を下回れば true（黒字・小幅赤字は残留）。
        /// ミクロ経済の退出条件＝「儲からない企業は市場から去る」を式に出す。固定費0は赤字（負）で退出。
        /// </summary>
        public static bool MarketExitDecision(float operatingProfit, float fixedCost, FirmParams p)
        {
            float fc = Mathf.Max(0f, fixedCost);
            float threshold = -(fc * p.exitLossRatio); // この水準を下回る赤字で退出
            return operatingProfit < threshold;
        }
    }
}
