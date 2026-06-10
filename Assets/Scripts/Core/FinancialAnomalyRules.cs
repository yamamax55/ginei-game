using UnityEngine;

namespace Ginei
{
    /// <summary>検出された財務異常の類型（#1016）。<see cref="AnomalyType.なし"/>＝清廉。</summary>
    public enum AnomalyType { なし, 粉飾決算, 横領, 異常支出, 簿外債務 }

    /// <summary>異常検知の調整係数（#1016・マジックナンバー禁止＝集約）。</summary>
    public readonly struct FinancialAnomalyParams
    {
        /// <summary>台帳不整合とみなす貸借差の許容割合（総額比。これ以下は丸め誤差扱い）。</summary>
        public readonly float ledgerTolerance;
        /// <summary>異常支出フラグが立つ外れσ（平均からこのσ以上で外れ値＝怪しい）。</summary>
        public readonly float outlierSigma;
        /// <summary>粉飾（利益平準化）とみなす分散比の上限（報告分散/実分散がこれ以下＝不自然に滑らか）。</summary>
        public readonly float smoothingRatioThreshold;
        /// <summary>簿外債務とみなす債務乖離の割合（実態債務が公表をこの割合超で上回る＝隠れ債務）。</summary>
        public readonly float liabilityGapThreshold;

        public FinancialAnomalyParams(float ledgerTolerance, float outlierSigma, float smoothingRatioThreshold, float liabilityGapThreshold)
        {
            this.ledgerTolerance = Mathf.Max(0f, ledgerTolerance);
            this.outlierSigma = Mathf.Max(0f, outlierSigma);
            this.smoothingRatioThreshold = Mathf.Clamp01(smoothingRatioThreshold);
            this.liabilityGapThreshold = Mathf.Max(0f, liabilityGapThreshold);
        }

        /// <summary>既定＝台帳許容0.1%・外れ3σ・平準化比0.3・債務乖離10%。</summary>
        public static FinancialAnomalyParams Default => new FinancialAnomalyParams(0.001f, 3f, 0.3f, 0.1f);
    }

    /// <summary>
    /// 財務異常検知の純ロジック（#1016）。「数字は嘘をつく＝<b>整合の崩れ</b>と<b>統計的な外れ値</b>が不正の影を映す」を式に出す。
    /// 会計データを read-only で監査し、粉飾決算/横領/異常支出/簿外債務のフラグを立てる。
    /// 監査の分担：<see cref="LedgerRules"/>(複式簿記の記帳整合・#974)／<see cref="BalanceSheetRules"/>(貸借対照表・#975) が
    /// 真の数字を作り、本ルールはその数字の<b>整合崩れと外れ値</b>を検出するだけ（基準値非破壊・read-only）。
    /// 摘発の<b>実行</b>（捜査・粛清）は <see cref="SecurityRules"/>(秘密警察) が担う＝本ルールは影を映すまで（容疑のフラグと監査優先度）。
    /// 乱数なし決定論・入力クランプ・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FinancialAnomalyRules
    {
        /// <summary>
        /// 台帳の不整合度 0..1＝Σ借方とΣ貸方の乖離割合（記帳ミスか改竄＝<see cref="LedgerRules.IsLedgerBalanced"/> の補完）。
        /// 複式簿記では Σ借方=Σ貸方 が根本原理ゆえ、ここがずれること自体が不正の影。総額比で正規化する（規模非依存）。
        /// </summary>
        public static float LedgerInconsistency(float totalDebits, float totalCredits)
        {
            float d = Mathf.Max(0f, totalDebits);
            float c = Mathf.Max(0f, totalCredits);
            float scale = Mathf.Max(d, c);
            if (scale <= 0f) return 0f; // 何も記帳されていない＝整合（空台帳）
            return Mathf.Clamp01(Mathf.Abs(d - c) / scale);
        }

        /// <summary>台帳が不整合か＝乖離割合が許容を超える（true＝改竄/記帳ミスの容疑）。</summary>
        public static bool IsLedgerInconsistent(float totalDebits, float totalCredits, FinancialAnomalyParams p)
        {
            return LedgerInconsistency(totalDebits, totalCredits) > p.ledgerTolerance;
        }

        public static bool IsLedgerInconsistent(float totalDebits, float totalCredits)
            => IsLedgerInconsistent(totalDebits, totalCredits, FinancialAnomalyParams.Default);

        /// <summary>
        /// 異常支出の外れ度＝当期支出が過去平均から何σ外れているか（z スコア・統計的外れ値＝横領/汚職の兆候）。
        /// 標準偏差が0（履歴がほぼ一定）なら、平均と一致で0・乖離があれば極大とみなす（割れない＝無限大を回避）。
        /// </summary>
        public static float ExpenseOutlier(float expense, float historicalMean, float historicalStdDev)
        {
            float sd = Mathf.Max(0f, historicalStdDev);
            float diff = Mathf.Abs(expense - historicalMean);
            if (sd <= 0f) return diff <= 0f ? 0f : float.MaxValue; // 履歴が一定＝少しでもズレれば即異常
            return diff / sd;
        }

        /// <summary>異常支出フラグ＝外れσが閾値以上（true＝横領/水増しの容疑で要監査）。</summary>
        public static bool IsExpenseAnomalous(float expense, float historicalMean, float historicalStdDev, FinancialAnomalyParams p)
        {
            return ExpenseOutlier(expense, historicalMean, historicalStdDev) >= p.outlierSigma;
        }

        public static bool IsExpenseAnomalous(float expense, float historicalMean, float historicalStdDev)
            => IsExpenseAnomalous(expense, historicalMean, historicalStdDev, FinancialAnomalyParams.Default);

        /// <summary>
        /// 粉飾（利益平準化）の兆候 0..1＝報告分散が実態分散より不自然に滑らかなほど大。
        /// 報告分散/実分散の比が小さい＝経営が利益を均している（ビッグバス会計の逆＝悪い年を隠す）。
        /// 比=1（同じ滑らかさ）で0、比→0（報告が完全平坦）で1。実分散が0なら比較不能＝兆候0。
        /// </summary>
        public static float RevenueSmoothing(float reportedVariance, float actualVariance)
        {
            float rep = Mathf.Clamp01(reportedVariance);
            float act = Mathf.Clamp01(actualVariance);
            if (act <= 0f) return 0f; // 実態がそもそも一定＝平準化のしようがない
            float ratio = Mathf.Clamp01(rep / act);
            return 1f - ratio;
        }

        /// <summary>粉飾フラグ＝平準化度が閾値を超える（報告分散比が threshold 未満＝不自然な滑らかさ）。</summary>
        public static bool IsRevenueSmoothed(float reportedVariance, float actualVariance, FinancialAnomalyParams p)
        {
            // RevenueSmoothing = 1 - ratio。ratio < threshold ⇔ smoothing > 1 - threshold。
            return RevenueSmoothing(reportedVariance, actualVariance) > (1f - p.smoothingRatioThreshold);
        }

        public static bool IsRevenueSmoothed(float reportedVariance, float actualVariance)
            => IsRevenueSmoothed(reportedVariance, actualVariance, FinancialAnomalyParams.Default);

        /// <summary>
        /// 簿外債務リスク 0..1＝公表債務と実態（含意）債務の乖離割合（隠れた借金）。
        /// 実態が公表を上回るぶんを実態比で正規化（公表 declared が小さく impled が大きいほど隠蔽が深い）。
        /// 実態が公表以下なら隠れ債務なし＝0。
        /// </summary>
        public static float UnrecordedLiabilityRisk(float declaredDebt, float impliedDebt)
        {
            float dec = Mathf.Max(0f, declaredDebt);
            float imp = Mathf.Max(0f, impliedDebt);
            if (imp <= 0f) return 0f; // 実態債務がない＝隠す対象がない
            if (imp <= dec) return 0f; // 公表が実態以上＝隠していない
            return Mathf.Clamp01((imp - dec) / imp);
        }

        /// <summary>簿外債務フラグ＝乖離割合が閾値を超える（true＝公表外の債務を隠している容疑）。</summary>
        public static bool HasUnrecordedLiability(float declaredDebt, float impliedDebt, FinancialAnomalyParams p)
        {
            return UnrecordedLiabilityRisk(declaredDebt, impliedDebt) > p.liabilityGapThreshold;
        }

        public static bool HasUnrecordedLiability(float declaredDebt, float impliedDebt)
            => HasUnrecordedLiability(declaredDebt, impliedDebt, FinancialAnomalyParams.Default);

        /// <summary>
        /// 総合異常度 0..1＝複数フラグの合成（最も疑わしい兆候が支配する＝最大値合成）。
        /// 台帳不整合・異常支出σ・粉飾平準化・簿外債務の4チャネルを 0..1 へ正規化して束ね、最も濃い影を採る。
        /// 異常支出σは outlierSigma を1.0に対応づけて飽和（閾値到達で満点）。
        /// </summary>
        public static float AnomalyScore(float ledgerInconsistency, float expenseSigma, float revenueSmoothing, float unrecordedLiabilityRisk, FinancialAnomalyParams p)
        {
            float ledger = Mathf.Clamp01(ledgerInconsistency);
            float sigma = p.outlierSigma > 0f ? Mathf.Clamp01(Mathf.Max(0f, expenseSigma) / p.outlierSigma) : 0f;
            float smooth = Mathf.Clamp01(revenueSmoothing);
            float liab = Mathf.Clamp01(unrecordedLiabilityRisk);
            // 影は重なって濃くなるのでなく、最も濃い1点が不正を露わにする＝最大値。
            return Mathf.Max(Mathf.Max(ledger, sigma), Mathf.Max(smooth, liab));
        }

        public static float AnomalyScore(float ledgerInconsistency, float expenseSigma, float revenueSmoothing, float unrecordedLiabilityRisk)
            => AnomalyScore(ledgerInconsistency, expenseSigma, revenueSmoothing, unrecordedLiabilityRisk, FinancialAnomalyParams.Default);

        /// <summary>
        /// 最も濃い影の類型＝4チャネルのうち最大寄与の異常を返す（同点は台帳＞支出＞粉飾＞債務の順で確定）。
        /// すべて閾値未満なら <see cref="AnomalyType.なし"/>（清廉）。容疑の見出しを1つ立てる窓口。
        /// </summary>
        public static AnomalyType DominantAnomaly(float ledgerInconsistency, float expenseSigma, float revenueSmoothing, float unrecordedLiabilityRisk, FinancialAnomalyParams p)
        {
            // 各チャネルが「フラグ条件」を満たすか＝0..1の寄与に直す（満たさなければ0）。
            float ledger = (ledgerInconsistency > p.ledgerTolerance) ? Mathf.Clamp01(ledgerInconsistency) : 0f;
            float sigma = (expenseSigma >= p.outlierSigma && p.outlierSigma > 0f) ? Mathf.Clamp01(expenseSigma / p.outlierSigma) : 0f;
            float smooth = (revenueSmoothing > (1f - p.smoothingRatioThreshold)) ? Mathf.Clamp01(revenueSmoothing) : 0f;
            float liab = (unrecordedLiabilityRisk > p.liabilityGapThreshold) ? Mathf.Clamp01(unrecordedLiabilityRisk) : 0f;

            float best = Mathf.Max(Mathf.Max(ledger, sigma), Mathf.Max(smooth, liab));
            if (best <= 0f) return AnomalyType.なし;
            // 同点優先順位：台帳＞支出（横領）＞粉飾＞簿外債務。
            if (ledger >= best) return AnomalyType.粉飾決算; // 整合崩れは決算の歪み＝粉飾の痕跡
            if (sigma >= best) return AnomalyType.横領;
            if (smooth >= best) return AnomalyType.粉飾決算;
            return AnomalyType.簿外債務;
        }

        public static AnomalyType DominantAnomaly(float ledgerInconsistency, float expenseSigma, float revenueSmoothing, float unrecordedLiabilityRisk)
            => DominantAnomaly(ledgerInconsistency, expenseSigma, revenueSmoothing, unrecordedLiabilityRisk, FinancialAnomalyParams.Default);

        /// <summary>
        /// 監査の優先度＝異常度×勘定規模（怪しく大きい勘定から調べる＝限られた監査資源の配分）。
        /// 小さく怪しい勘定より、大きく怪しい勘定を先に開く（被害額の期待値が高い）。
        /// 規模は対数で逓減させ、巨大勘定が他を完全に呑み込まないようにする（怪しさも残す）。
        /// </summary>
        public static float AuditPriority(float anomalyScore, float accountSize)
        {
            float score = Mathf.Clamp01(anomalyScore);
            float size = Mathf.Max(0f, accountSize);
            // 規模は log(1+size) で逓減＝桁の大きさを効かせつつ飽和。怪しさ0なら規模が大きくても優先度0。
            return score * Mathf.Log(1f + size);
        }
    }
}
