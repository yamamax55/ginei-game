using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>システミック危機の解決結果（破綻数とシステム総損失）。</summary>
    public readonly struct CrisisResult
    {
        public readonly int failedCount;
        public readonly float totalLoss;
        public CrisisResult(int failedCount, float totalLoss) { this.failedCount = failedCount; this.totalLoss = totalLoss; }
    }

    /// <summary>
    /// リーマンショック＝システミック金融危機のロジック（#1939 LEHM・純ロジック・唯一の窓口）。証券化バブルの崩壊が
    /// レバレッジで増幅され、金融機関の破綻→信用収縮→連鎖伝染→救済へ波及する連鎖を回す：
    /// LEHM-1 評価損／LEHM-2 レバレッジと破綻／LEHM-3 信用収縮／LEHM-4 連鎖伝染（不動点 cascade）／LEHM-5 救済。
    /// サブプライム <see cref="SubprimeRules.RevealLoss"/>・銀行 #186・株式暴落 #185・財政 #163 へ接続。マクロ近似。test-first。
    /// </summary>
    public static class FinancialCrisisRules
    {
        /// <summary>デフォルト時損失率（伝染で破綻先への与信が失われる割合）。</summary>
        public const float DefaultLGD = 0.5f;

        /// <summary>cascade の反復上限（不動点ガード）。</summary>
        public const int MaxCascadeRounds = 12;

        // ===== LEHM-1 評価損 =====

        /// <summary>保有証券化商品の評価損＝MBSエクスポージャ×露呈率（<see cref="SubprimeRules.RevealLoss"/> 由来の損失率0..1）。</summary>
        public static float MarkToMarketLoss(FinancialInstitution fi, float revealLossRatio)
            => fi == null ? 0f : Mathf.Max(0f, fi.mbsExposure) * Mathf.Clamp01(revealLossRatio);

        // ===== LEHM-2 レバレッジと破綻 =====

        /// <summary>レバレッジ＝総資産/自己資本（薄いほど小さな損失で吹き飛ぶ）。自己資本0以下は超過大。</summary>
        public static float Leverage(FinancialInstitution fi)
        {
            if (fi == null) return 0f;
            return fi.capital <= 0f ? 999f : Mathf.Max(0f, fi.assets) / fi.capital;
        }

        /// <summary>損失後の自己資本＝資本−損失。</summary>
        public static float EquityAfterLoss(FinancialInstitution fi, float loss)
            => (fi == null ? 0f : fi.capital) - Mathf.Max(0f, loss);

        /// <summary>破綻＝損失後の自己資本がマイナス（債務超過）。</summary>
        public static bool IsBankrupt(FinancialInstitution fi, float loss) => EquityAfterLoss(fi, loss) < 0f;

        // ===== LEHM-3 信用収縮 =====

        /// <summary>信用収縮率（0..1）＝損失/自己資本のストレスで貸出が縮む割合（貸し渋り）。自己資本0以下は完全収縮。</summary>
        public static float CreditCrunchFactor(FinancialInstitution fi, float loss)
        {
            if (fi == null || fi.capital <= 0f) return 1f;
            return Mathf.Clamp01(Mathf.Max(0f, loss) / fi.capital);
        }

        // ===== LEHM-4 連鎖伝染 =====

        /// <summary>伝染損失＝破綻先への与信×デフォルト時損失率（カウンターパーティリスク）。</summary>
        public static float ContagionLoss(float exposureToFailed, float lossGivenDefault)
            => Mathf.Max(0f, exposureToFailed) * Mathf.Clamp01(lossGivenDefault);

        /// <summary>
        /// 危機を不動点まで解決する：①直接の評価損で自己資本を毀損→②債務超過は破綻→③破綻割合に応じて生存行のインターバンク与信が
        /// 損失化（伝染）→再び破綻…を新たな破綻が止まるまで。システム総損失（直接損より大きくなりうる＝システミック増幅）と破綻数を返す。
        /// institutions の capital を破壊的に更新する。
        /// </summary>
        public static CrisisResult ResolveCrisis(IReadOnlyList<FinancialInstitution> institutions, float revealLossRatio, float lgd)
        {
            int n = institutions?.Count ?? 0;
            if (n == 0) return new CrisisResult(0, 0f);

            var failed = new bool[n];
            float totalLoss = 0f;

            // ① 直接の評価損
            for (int i = 0; i < n; i++)
            {
                var fi = institutions[i];
                if (fi == null) continue;
                float l = MarkToMarketLoss(fi, revealLossRatio);
                fi.capital -= l;
                totalLoss += l;
            }

            // ②③ cascade（破綻→伝染→再破綻を不動点まで）
            float prevFrac = 0f;
            for (int round = 0; round < MaxCascadeRounds; round++)
            {
                for (int i = 0; i < n; i++)
                {
                    var fi = institutions[i];
                    if (fi == null || failed[i]) continue;
                    if (fi.capital < 0f) failed[i] = true;
                }
                int fc = 0; for (int i = 0; i < n; i++) if (failed[i]) fc++;
                float frac = (float)fc / n;
                float deltaFrac = frac - prevFrac;
                if (deltaFrac <= 1e-6f) break; // 新たな破綻なし＝収束

                // 伝染：新たな破綻ぶんだけ生存行のインターバンク与信が失われる
                for (int i = 0; i < n; i++)
                {
                    var fi = institutions[i];
                    if (fi == null || failed[i]) continue;
                    float interbankExposure = Mathf.Max(0f, fi.assets) * Mathf.Clamp01(fi.interbankLinkage);
                    float cl = ContagionLoss(interbankExposure * deltaFrac, lgd);
                    fi.capital -= cl;
                    totalLoss += cl;
                }
                prevFrac = frac;
            }

            int failedCount = 0; for (int i = 0; i < n; i++) if (failed[i]) failedCount++;
            return new CrisisResult(failedCount, totalLoss);
        }

        // ===== LEHM-5 公的救済 =====

        /// <summary>救済コスト＝債務超過の穴埋め（損失が自己資本を超えたぶん＝国庫 #163 が注入する資本）。</summary>
        public static float BailoutCost(FinancialInstitution fi, float loss)
            => Mathf.Max(0f, Mathf.Max(0f, loss) - (fi == null ? 0f : fi.capital));

        /// <summary>救済すべきか＝大きすぎて潰せない（システム上重要）。</summary>
        public static bool ShouldBailout(FinancialInstitution fi) => fi != null && fi.tooBigToFail;

        /// <summary>システミックリスク指数（0..1）＝総MBS/(総資本+総MBS)×平均相互接続度。証券化過多＋高接続で脆弱。</summary>
        public static float SystemicRisk(IReadOnlyList<FinancialInstitution> institutions)
        {
            int n = institutions?.Count ?? 0;
            if (n == 0) return 0f;
            float totalMbs = 0f, totalCap = 0f, sumLink = 0f; int m = 0;
            for (int i = 0; i < n; i++)
            {
                var fi = institutions[i];
                if (fi == null) continue;
                totalMbs += Mathf.Max(0f, fi.mbsExposure);
                totalCap += Mathf.Max(0f, fi.capital);
                sumLink += Mathf.Clamp01(fi.interbankLinkage);
                m++;
            }
            if (m == 0) return 0f;
            float mbsToCapital = totalMbs / (totalCap + totalMbs + 1e-6f); // 0..1
            float avgLink = sumLink / m;
            return Mathf.Clamp01(mbsToCapital * avgLink);
        }
    }
}
