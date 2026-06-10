using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 予測会計・キャッシュフロー予測の純ロジック（#1015・唯一の窓口）。<b>「あとNヶ月で債務超過になる」早期警告</b>が核：
    /// 現在の収支トレンド（毎期の純CF）を将来へ単純投影し、準備金がいつ尽きるか＝<b>債務超過までの残期間</b>を逆算する
    /// （赤字が続けば必ず尽きる日が来る＝燃焼率で割れば残り時間が出る）。黒字トレンドなら尽きない＝無限大。
    /// 分担：<see cref="FiscalRules"/> が財政の<b>実体</b>（PB・国債・金利・債務スパイラル）を動かし、本クラスはその実体を
    /// <b>動かさず将来へ投影する予測のみ</b>（read-only な早期警告器）。<c>AutoTreasuryRules</c>（自律財務）はこの警告段階を
    /// 入力に減債/緊縮を起動し、<c>FiscalPolicyRules</c> は税率/歳出の政策レバーを引く。本クラスは「いつ尽きるか」を告げるだけ。
    /// 全入力クランプ（純CFは赤字＝負を許容）・乱数なし決定論・test-first。
    /// </summary>
    /// <summary>予測会計の調整値（マジックナンバー禁止＝集約）。</summary>
    public readonly struct CashFlowForecastParams
    {
        public readonly float cautionPeriods;   // この残期間以下で「注意」（余裕→注意の境界）
        public readonly float alertPeriods;     // この残期間以下で「警戒」
        public readonly float crisisPeriods;    // この残期間以下で「危機」＝即対応
        public readonly float minConfidence;    // 予測信頼度の下限（激動期でもこれは残す）

        public CashFlowForecastParams(float cautionPeriods, float alertPeriods, float crisisPeriods, float minConfidence)
        {
            // 危機 ≤ 警戒 ≤ 注意 の不変条件を保つ（クランプ後の値を基準に各段を積む）。
            this.crisisPeriods = Mathf.Max(0f, crisisPeriods);
            this.alertPeriods = Mathf.Max(this.crisisPeriods, alertPeriods);
            this.cautionPeriods = Mathf.Max(this.alertPeriods, cautionPeriods);
            this.minConfidence = Mathf.Clamp01(minConfidence);
        }

        /// <summary>既定＝注意12期/警戒6期/危機3期（ヶ月想定）・信頼度下限0.1。</summary>
        public static CashFlowForecastParams Default => new CashFlowForecastParams(12f, 6f, 3f, 0.1f);
    }

    /// <summary>警告段階（残期間が短いほど赤信号）。</summary>
    public enum CashFlowWarning
    {
        余裕,   // 黒字 or 残期間 > 注意閾値
        注意,
        警戒,
        危機,   // あとわずかで債務超過＝即対応
    }

    public static class CashFlowForecastRules
    {

        // 黒字（純CF≥0）は尽きない＝残期間は無限大の番兵。
        public const float Infinite = float.PositiveInfinity;

        /// <summary>N期後の予測残高＝現準備金＋純CF×期数（赤字トレンドなら減り続ける＝トレンドの単純投影）。</summary>
        public static float ProjectedBalance(float currentReserves, float netCashFlowPerPeriod, float periods)
        {
            float n = Mathf.Max(0f, periods);
            return currentReserves + netCashFlowPerPeriod * n;
        }

        /// <summary>資金燃焼率＝毎期どれだけ溶けているか（赤字の大きさ＝純CFが負のときの絶対値・黒字は0）。</summary>
        public static float BurnRate(float netCashFlow)
            => netCashFlow < 0f ? -netCashFlow : 0f;

        /// <summary>
        /// 債務超過までの残期間＝早期警告の核。赤字トレンド（純CF&lt;0）なら 準備金÷燃焼率 で「あと何期で尽きるか」。
        /// 既に債務超過（準備金≤0）の赤字は0期、黒字 or 横ばい（純CF≥0）は尽きない＝<see cref="Infinite"/>。
        /// </summary>
        public static float PeriodsUntilInsolvency(float reserves, float netCashFlow)
        {
            float burn = BurnRate(netCashFlow);
            if (burn <= 0f) return Infinite;            // 黒字/横ばいは尽きない
            if (reserves <= 0f) return 0f;              // 既に底＝即危機
            return reserves / burn;                     // 残準備金 ÷ 毎期の燃焼＝残り時間
        }

        /// <summary>警告段階＝残期間が短いほど赤信号（無限大＝余裕／危機閾値以下＝危機）。</summary>
        public static CashFlowWarning WarningLevel(float periodsUntilInsolvency, CashFlowForecastParams p)
        {
            if (float.IsPositiveInfinity(periodsUntilInsolvency) || periodsUntilInsolvency > p.cautionPeriods)
                return CashFlowWarning.余裕;
            if (periodsUntilInsolvency > p.alertPeriods) return CashFlowWarning.注意;
            if (periodsUntilInsolvency > p.crisisPeriods) return CashFlowWarning.警戒;
            return CashFlowWarning.危機;
        }

        /// <summary>
        /// 必要な改善幅＝目標期間まで準備金を持たせるには収支をいくら改善すべきか。
        /// 目標期間ぶん尽きない最小の純CF＝−準備金/目標期間。それを現状純CFが下回る不足分を返す（足りていれば0）。
        /// </summary>
        public static float RequiredCorrection(float reserves, float netCashFlow, float targetPeriods)
        {
            float n = Mathf.Max(0f, targetPeriods);
            if (n <= 0f) return 0f;                          // 目標期間0＝制約なし
            float requiredNet = -Mathf.Max(0f, reserves) / n; // この純CFなら目標期間で丁度尽きる＝下限
            float gap = requiredNet - netCashFlow;            // 現状がそれを下回る不足
            return Mathf.Max(0f, gap);
        }

        /// <summary>
        /// 予測の信頼度 0..1＝収支が安定しているほど予測が当たる。履歴の分散(0..1)が大きい激動期ほど信頼度が落ちる
        /// （ただし下限 minConfidence は残す）。早期警告を鵜呑みにしてよいかの目安。
        /// </summary>
        public static float TrendConfidence(float historicalVariance, CashFlowForecastParams p)
        {
            float v = Mathf.Clamp01(historicalVariance);
            return Mathf.Max(p.minConfidence, 1f - v);
        }
    }
}
