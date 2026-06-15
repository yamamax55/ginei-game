using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 保険・ロイズのロジック（#1982 INS・純ロジック・唯一の窓口）。大数の法則でリスクを束ね引き受ける：保険料・期待損失と
    /// リスクプール（INS-1）／引受損益とコンバインドレシオ（INS-2）／支払備金とソルベンシー（INS-3）／再保険＝リスク転嫁
    /// （INS-4）／ロイズ＝保険市場のシンジケート共同引受・海上保険（INS-5・#94/#95）／フロート運用（INS-6・#161/#1963）。
    /// ソルベンシーは銀行 BIS（#1976）・証券 net capital（#1963）と同型、再保険連鎖は危機（#1939）と噛み合う。マクロ近似。test-first。
    /// </summary>
    public static class InsuranceRules
    {
        /// <summary>付加保険料率（期待損失への上乗せ＝経費・利潤マージン）。</summary>
        public const float DefaultLoadingFactor = 0.3f;

        /// <summary>支払備金の安全マージン（期待保険金への上乗せ＝保守的に積む）。</summary>
        public const float DefaultPrudenceMargin = 0.1f;

        /// <summary>必要資本の係数（保険料/保険金規模に対する所要自己資本の割合）。</summary>
        public const float DefaultCapitalFactor = 0.5f;

        /// <summary>ソルベンシー比率の最低基準（自己資本/必要資本がこれ以上で健全）。</summary>
        public const float MinSolvencyRatio = 1f;

        // ===== INS-1 保険の基礎（大数の法則） =====

        /// <summary>期待損失＝損害発生確率×損害額（大数の法則で多数集めればこれに収束＝保険料の土台）。</summary>
        public static float ExpectedLoss(float probability, float lossAmount)
            => Mathf.Clamp01(probability) * Mathf.Max(0f, lossAmount);

        /// <summary>適正保険料＝期待損失×(1＋付加率)（期待損失に経費・利潤を上乗せ）。</summary>
        public static float FairPremium(float probability, float lossAmount, float loadingFactor)
            => ExpectedLoss(probability, lossAmount) * (1f + Mathf.Max(0f, loadingFactor));

        /// <summary>リスクプールの期待損失合計（多数契約の期待損失を集計＝損失率が安定する）。</summary>
        public static float PoolExpectedLoss(IReadOnlyList<InsurancePolicy> policies)
        {
            if (policies == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < policies.Count; i++)
                if (policies[i] != null) sum += ExpectedLoss(policies[i].probability, policies[i].lossAmount);
            return sum;
        }

        /// <summary>リスクプールの保険料合計。</summary>
        public static float PoolPremium(IReadOnlyList<InsurancePolicy> policies)
        {
            if (policies == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < policies.Count; i++)
                if (policies[i] != null) sum += Mathf.Max(0f, policies[i].premium);
            return sum;
        }

        /// <summary>損害率＝保険金/保険料（高いほど引受が苦しい）。保険料0以下は0。</summary>
        public static float LossRatio(float claims, float premiums)
            => premiums <= 0f ? 0f : Mathf.Max(0f, claims) / premiums;

        // ===== INS-2 引受とアンダーライティング損益 =====

        /// <summary>引受損益＝保険料−保険金−経費（保険の本業の儲け。負＝引受赤字）。</summary>
        public static float UnderwritingResult(float premiums, float claims, float expenses)
            => premiums - Mathf.Max(0f, claims) - Mathf.Max(0f, expenses);

        /// <summary>引受損益（保険会社データから）。</summary>
        public static float UnderwritingResult(Insurer insurer)
            => insurer == null ? 0f : UnderwritingResult(insurer.premiumsWritten, insurer.claimsPaid, insurer.expenses);

        /// <summary>コンバインドレシオ＝(保険金＋経費)/保険料。1.0未満で引受黒字・超で引受赤字。保険料0以下は大きい値。</summary>
        public static float CombinedRatio(float claims, float expenses, float premiums)
            => premiums <= 0f ? 999f : (Mathf.Max(0f, claims) + Mathf.Max(0f, expenses)) / premiums;

        /// <summary>引受黒字か＝コンバインドレシオが100%未満。</summary>
        public static bool IsUnderwritingProfit(float combinedRatio) => combinedRatio < 1f;

        // ===== INS-3 支払備金とソルベンシー =====

        /// <summary>支払備金＝期待保険金×(1＋安全マージン)（将来の支払に保守的に備える）。</summary>
        public static float RequiredReserve(float expectedClaims, float prudenceMargin)
            => Mathf.Max(0f, expectedClaims) * (1f + Mathf.Max(0f, prudenceMargin));

        /// <summary>必要資本＝max(保険料, 保険金)×係数（引受規模に見合う自己資本）。</summary>
        public static float RequiredCapital(float premiums, float claims, float factor)
            => Mathf.Max(Mathf.Max(0f, premiums), Mathf.Max(0f, claims)) * Mathf.Max(0f, factor);

        /// <summary>ソルベンシー比率＝自己資本/必要資本（規制の指標）。必要資本0以下はリスクなし＝大きい値。</summary>
        public static float SolvencyRatio(float capital, float requiredCapital)
            => requiredCapital <= 0f ? 999f : capital / requiredCapital;

        /// <summary>ソルベンシーを満たすか＝比率が最低基準以上（割れると保険金支払不能リスク＝破綻）。</summary>
        public static bool IsSolvent(float capital, float requiredCapital, float minRatio)
            => SolvencyRatio(capital, requiredCapital) >= minRatio;

        // ===== INS-4 再保険（リスクの転嫁） =====

        /// <summary>出再損失＝損失×出再割合（比例再保険で再保険会社へ渡す損失）。</summary>
        public static float CededLoss(float loss, float cessionRatio)
            => Mathf.Max(0f, loss) * Mathf.Clamp01(cessionRatio);

        /// <summary>保有損失＝損失×(1−出再割合)（自分に残るリスク）。</summary>
        public static float RetainedLoss(float loss, float cessionRatio)
            => Mathf.Max(0f, loss) * (1f - Mathf.Clamp01(cessionRatio));

        /// <summary>超過損害額再保険＝保有限度(retention)を超えた損失を限度額(limit)まで再保険が負担（巨大損失の上澄みを転嫁）。</summary>
        public static float ExcessOfLoss(float loss, float retention, float limit)
            => Mathf.Clamp(Mathf.Max(0f, loss) - Mathf.Max(0f, retention), 0f, Mathf.Max(0f, limit));

        /// <summary>再保険料＝出再ぶんの期待損失×(1＋付加率)（リスクを引き取る再保険会社へ払う）。</summary>
        public static float ReinsurancePremium(float cededExpectedLoss, float loadingFactor)
            => Mathf.Max(0f, cededExpectedLoss) * (1f + Mathf.Max(0f, loadingFactor));

        // ===== INS-5 ロイズ＝保険市場とシンジケート =====

        /// <summary>シンジケートの引受ライン＝min(引受能力, リスク額×引受割合)（取りにいく割合を能力で頭打ち）。</summary>
        public static float SyndicateLine(LloydsSyndicate syndicate, float riskAmount)
        {
            if (syndicate == null) return 0f;
            return Mathf.Min(Mathf.Max(0f, syndicate.capacity), Mathf.Max(0f, riskAmount) * Mathf.Clamp01(syndicate.lineShare));
        }

        /// <summary>共同引受で実際に引き受けられた額＝各シンジケートのラインの合計（リスク額で頭打ち＝超過分は引き受けない）。</summary>
        public static float PlaceRisk(IReadOnlyList<LloydsSyndicate> syndicates, float riskAmount)
        {
            if (syndicates == null) return 0f;
            float placed = 0f;
            for (int i = 0; i < syndicates.Count; i++) placed += SyndicateLine(syndicates[i], riskAmount);
            return Mathf.Min(placed, Mathf.Max(0f, riskAmount));
        }

        /// <summary>リスクが引受能力で満たされたか＝共同引受の合計がリスク額に届く（届かなければ未充足＝引き受け手不足）。</summary>
        public static bool IsFullyPlaced(IReadOnlyList<LloydsSyndicate> syndicates, float riskAmount)
            => PlaceRisk(syndicates, riskAmount) >= Mathf.Max(0f, riskAmount) - 1e-3f;

        /// <summary>海上保険料＝船団価値×襲撃確率×(1＋付加率)（通商破壊 #94/#95 のリスクから＝ロイズ海上保険の起源）。</summary>
        public static float MarinePremium(float convoyValue, float raidProbability, float loadingFactor)
            => FairPremium(raidProbability, convoyValue, loadingFactor);

        // ===== INS-6 フロートの運用 =====

        /// <summary>フロート＝収入保険料−支払保険金（保険料を集めてから払うまでの預かり資金。非負）。</summary>
        public static float Float(float premiums, float paidClaims)
            => Mathf.Max(0f, premiums - Mathf.Max(0f, paidClaims));

        /// <summary>フロート（保険会社データから）。</summary>
        public static float Float(Insurer insurer)
            => insurer == null ? 0f : Float(insurer.premiumsWritten, insurer.claimsPaid);

        /// <summary>投資収益＝フロート×運用利回り（保険会社が機関投資家になる理由＝#161/#1963 で運用）。</summary>
        public static float InvestmentIncome(float floatAmount, float investmentYield)
            => Mathf.Max(0f, floatAmount) * investmentYield;

        /// <summary>総利益＝引受損益＋投資収益（引受赤字でも投資収益で黒字にできる＝バフェット型）。</summary>
        public static float TotalProfit(float underwritingResult, float investmentIncome)
            => underwritingResult + investmentIncome;
    }
}
