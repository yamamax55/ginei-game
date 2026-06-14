using UnityEngine;

namespace Ginei
{
    /// <summary>金融危機→実経済への打撃の調整係数（決定論・bounded）。</summary>
    public readonly struct CrisisEffectParams
    {
        /// <summary>危機度1.0で国庫から失う割合の上限（0..1）。</summary>
        public readonly float maxTreasuryContraction;
        /// <summary>危機度1.0での産出倍率の下限（収縮しきっても残る生産）。</summary>
        public readonly float minOutputFactor;
        /// <summary>危機度1.0で支持/民心を削る最大量（正値・delta は負で返る）。</summary>
        public readonly float maxSupportDrop;

        public CrisisEffectParams(float maxTreasuryContraction, float minOutputFactor, float maxSupportDrop)
        {
            this.maxTreasuryContraction = Mathf.Clamp01(maxTreasuryContraction);
            this.minOutputFactor = Mathf.Clamp01(minOutputFactor);
            this.maxSupportDrop = Mathf.Max(0f, maxSupportDrop);
        }

        /// <summary>既定＝国庫収縮上限0.4・産出下限0.5・支持低下上限20。</summary>
        public static CrisisEffectParams Default => new CrisisEffectParams(0.4f, 0.5f, 20f);
    }

    /// <summary>
    /// 金融危機を実経済への打撃へ変換する薄い橋渡し（純ロジック・唯一の窓口）。
    /// <see cref="FinancialStabilityRules.CrisisSeverity"/> 等が出す危機度 0..1 を、
    /// 国庫収縮（取崩/逃避）・産出低下（信用収縮の実体経済波及）・支持低下（民心の動揺）の実打撃に写す。
    /// 危機検出/危機度の算定は <see cref="FinancialStabilityRules"/>・<see cref="FinancialCrisisRules"/>・
    /// <see cref="DebtDeflationRules"/> へ委譲し、ここは「危機度→打撃」の線形写像だけを持つ（数式を二重実装しない）。
    /// すべて bounded・実効値パターン（基準値非破壊＝呼び出し側が国庫/産出/支持へ適用）。
    /// severity=0 で打撃なし（収縮0/産出1/支持0）。乱数なし・決定論・null 非依存。test-first。
    /// </summary>
    public static class CrisisEffectRules
    {
        /// <summary>
        /// 国庫から失う割合 0..上限＝危機度×収縮上限（危機が深いほど多く失う）。
        /// severity=0 で 0、severity=1 で <see cref="CrisisEffectParams.maxTreasuryContraction"/>。
        /// </summary>
        public static float TreasuryContraction(float severity, CrisisEffectParams p)
            => Mathf.Clamp01(severity) * p.maxTreasuryContraction;

        public static float TreasuryContraction(float severity)
            => TreasuryContraction(severity, CrisisEffectParams.Default);

        /// <summary>
        /// 産出倍率 1→下限＝Lerp(1, 産出下限, 危機度)（危機が深いほど産出が縮む・実効値パターン）。
        /// severity=0 で 1.0、severity=1 で <see cref="CrisisEffectParams.minOutputFactor"/>。
        /// </summary>
        public static float OutputFactor(float severity, CrisisEffectParams p)
            => Mathf.Lerp(1f, p.minOutputFactor, Mathf.Clamp01(severity));

        public static float OutputFactor(float severity)
            => OutputFactor(severity, CrisisEffectParams.Default);

        /// <summary>
        /// 支持/民心#113 の負の delta＝−危機度×支持低下上限（危機が深いほど民心が冷える）。
        /// severity=0 で 0、severity=1 で −<see cref="CrisisEffectParams.maxSupportDrop"/>。
        /// </summary>
        public static float SupportDelta(float severity, CrisisEffectParams p)
            => -(Mathf.Clamp01(severity) * p.maxSupportDrop);

        public static float SupportDelta(float severity)
            => SupportDelta(severity, CrisisEffectParams.Default);
    }
}
