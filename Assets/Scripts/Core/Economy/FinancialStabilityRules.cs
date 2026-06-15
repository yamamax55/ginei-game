using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 財政自動運営・金融危機の薄いオーケストレータ（純ロジック・唯一の合成窓口）。既存の未配線 Core
    /// （<see cref="FiscalRules"/>＝財政・債務スパイラル／<see cref="DebtDeflationRules"/>＝負債デフレ／
    /// <see cref="FinancialCrisisRules"/>＝システミック危機／<see cref="FinancialContagionRules"/>＝市場間伝染／
    /// <see cref="AutoTreasuryRules"/>＝タッチレス財務）を <b>合成するだけ</b>で、数式は各既存窓口へ委譲する
    /// （二重実装しない）。状態は引数で受け、自身では保持しない。乱数なし・決定論。null 安全。test-first。
    /// </summary>
    public static class FinancialStabilityRules
    {
        // ===== 危機検出（FiscalRules / DebtDeflationRules 委譲） =====

        /// <summary>
        /// 危機度 0..1＝財政（債務スパイラル/財政健全度の悪化）と負債デフレ（重い実質債務負荷）の合成。
        /// 財政健全度が下がるほど・債務スパイラル中・負債デフレ突入で危機度が上がる。すべて既存窓口へ委譲。
        /// fs=null は危機度0（無リスク）。
        /// </summary>
        public static float CrisisSeverity(FiscalState fs, float economy, float priceLevel, float debtLoad, FiscalRules.FiscalParams fp)
        {
            if (fs == null) return 0f;

            // 財政の悪化＝健全度の裏（1で健全→0で危機）。
            float fiscalStress = 1f - FiscalRules.FiscalHealthFactor(fs, economy, fp);
            // 債務スパイラル中は上乗せ（複利膨張＝抜けにくい）。
            if (FiscalRules.IsDebtSpiral(fs, economy, fp)) fiscalStress = Mathf.Max(fiscalStress, 0.5f);

            // 負債デフレ＝物価が基準割れ＆債務負荷が重いと突入。
            float deflationStress = DebtDeflationRules.IsDebtDeflation(priceLevel, debtLoad) ? Mathf.Clamp01(debtLoad) : 0f;

            return Mathf.Clamp01(Mathf.Max(fiscalStress, deflationStress));
        }

        /// <summary><see cref="CrisisSeverity(FiscalState,float,float,float,FiscalRules.FiscalParams)"/> の既定パラメータ版。</summary>
        public static float CrisisSeverity(FiscalState fs, float economy, float priceLevel, float debtLoad)
            => CrisisSeverity(fs, economy, priceLevel, debtLoad, FiscalRules.FiscalParams.Default);

        /// <summary>
        /// 危機検出＝危機度がしきい値（既定0.5）以上か。fs=null は false。数式は <see cref="CrisisSeverity"/> 経由で
        /// すべて既存窓口へ委譲する（独自の判定式を持たない）。
        /// </summary>
        public static bool DetectCrisis(FiscalState fs, float economy, float priceLevel, float debtLoad, float threshold, FiscalRules.FiscalParams fp)
            => CrisisSeverity(fs, economy, priceLevel, debtLoad, fp) >= Mathf.Clamp01(threshold);

        /// <summary>債務負荷なし・物価平常(1.0)前提の簡易版＝財政（債務スパイラル/健全度）だけで危機検出。</summary>
        public static bool DetectCrisis(FiscalState fs, float economy)
            => DetectCrisis(fs, economy, 1f, 0f, 0.5f, FiscalRules.FiscalParams.Default);

        // ===== 伝染（FinancialContagionRules 委譲） =====

        /// <summary>
        /// 震源勢力の危機が、つながり linkage（0..1＝エクスポージャー）を伝って他勢力へ及ぶ伝染度 0..1。
        /// linkage に比例して強まる（つながり0なら伝わらない）＝計算は <see cref="FinancialContagionRules.TransmissionStrength"/> へ委譲。
        /// </summary>
        public static float ContagionTo(float crisisSeverityA, float linkage)
            => FinancialContagionRules.TransmissionStrength(linkage, crisisSeverityA);

        /// <summary>
        /// 防火壁（流動性供給＋資本規制）込みの実効伝染度 0..1。素の伝染度に対し防火壁ぶんを減衰させる
        /// ＝伝染力・防火壁とも <see cref="FinancialContagionRules"/> へ委譲（連鎖を断つのは防火壁だけ）。
        /// </summary>
        public static float ContagionTo(float crisisSeverityA, float linkage, float liquiditySupport, float capitalControl)
        {
            float raw = FinancialContagionRules.TransmissionStrength(linkage, crisisSeverityA);
            float firewall = FinancialContagionRules.FirewallEffectiveness(liquiditySupport, capitalControl);
            // 防火壁が完全(1.0)なら遮断・0なら素通し（FinancialContagionParams.firewallDamping=1 と整合）。
            return Mathf.Clamp01(raw * (1f - firewall));
        }

        // ===== 自動財務運営（AutoTreasuryRules 委譲） =====

        /// <summary>
        /// 財政の自動均衡＝危機なら自動で取る財務行動を選ぶ（赤字→緊縮/増税、準備金割れ→起債/取崩 等）。
        /// 行動の選択は <see cref="AutoTreasuryRules.DecideAction"/> へ委譲し、ここは <see cref="FiscalState"/>/<see cref="FiscalRules"/>
        /// から必要な入力（準備金=見込み黒字の代理、債務比率、金利）を組んで渡すだけ。fs=null/dt&lt;=0 は何もしない。
        /// 推奨行動を返す（状態は破壊的に更新しない＝呼び出し側が適用する）。
        /// </summary>
        public static TreasuryAction TickAutoTreasury(FiscalState fs, float economy, float reserves, float reserveFloor, float debtCeiling, float dt, FiscalRules.FiscalParams fp, AutoTreasuryParams ap)
        {
            if (fs == null || dt <= 0f) return TreasuryAction.何もしない;
            float debtRatio = FiscalRules.DebtRatio(fs, economy);
            float interestRate = FiscalRules.InterestRate(fs, economy, fp);
            return AutoTreasuryRules.DecideAction(reserves, reserveFloor, debtRatio, debtCeiling, interestRate, dt, ap);
        }

        /// <summary><see cref="TickAutoTreasury(FiscalState,float,float,float,float,float,FiscalRules.FiscalParams,AutoTreasuryParams)"/> の既定パラメータ版。</summary>
        public static TreasuryAction TickAutoTreasury(FiscalState fs, float economy, float reserves, float reserveFloor, float debtCeiling, float dt)
            => TickAutoTreasury(fs, economy, reserves, reserveFloor, debtCeiling, dt, FiscalRules.FiscalParams.Default, AutoTreasuryParams.Default);

        /// <summary>
        /// 自動運用の信頼度 0..1＝危機度（変動の代理）が高いほど人手が要る（完全自動の限界）。
        /// <see cref="AutoTreasuryRules.TouchlessConfidence"/> へ委譲。
        /// </summary>
        public static float AutoTreasuryConfidence(float crisisSeverity)
            => AutoTreasuryRules.TouchlessConfidence(crisisSeverity);

        // ===== システミック危機の解決（FinancialCrisisRules 委譲） =====

        /// <summary>
        /// 金融機関群のシステミック危機を不動点まで解決＝破綻数・システム総損失。
        /// <see cref="FinancialCrisisRules.ResolveCrisis"/> へ委譲（institutions の capital を破壊的に更新する点も委譲先のまま）。
        /// institutions=null/空は破綻0・損失0。
        /// </summary>
        public static CrisisResult ResolveSystemicCrisis(IReadOnlyList<FinancialInstitution> institutions, float revealLossRatio, float lgd)
            => FinancialCrisisRules.ResolveCrisis(institutions, revealLossRatio, lgd);

        /// <summary>デフォルト時損失率に <see cref="FinancialCrisisRules.DefaultLGD"/> を使う簡易版。</summary>
        public static CrisisResult ResolveSystemicCrisis(IReadOnlyList<FinancialInstitution> institutions, float revealLossRatio)
            => FinancialCrisisRules.ResolveCrisis(institutions, revealLossRatio, FinancialCrisisRules.DefaultLGD);
    }
}
