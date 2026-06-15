using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 経済・民心 → 内政（安定度）の橋（創発ループの配線・純ロジック・test-first）。
    /// 反乱（<see cref="RebellionRules"/>）は <see cref="Province.stability"/> の低下で発火するが、これまで安定度は
    /// 統合度/思想/戦争/補給/宰相しか見ておらず、<b>高税・過大債務・低民心が安定度に効いていなかった</b>。
    /// ここで国家状態（<see cref="FactionState"/>）から安定度への±補正を導き、`GalaxyView` が `GovernanceRules` の
    /// adminBonus 経由で加える＝「高税/債務スパイラル/民心崩壊 → 安定度低下 → 反乱 → 領土喪失」という因果を閉じる。
    /// 既存の支持/財政の数式（hope/taxRate/debt）は読むだけ＝二重実装しない。基準非破壊（安定度目標への加点）。
    /// </summary>
    public static class GovernanceEconomyRules
    {
        /// <summary>経済→安定度の調整値。</summary>
        public readonly struct EconomyStabilityParams
        {
            /// <summary>民心(0..1)の安定度への重み。0.5を中立として ±(hope-0.5)×weight。</summary>
            public readonly float hopeWeight;
            /// <summary>この税率を超えたぶんが不満（高税の重み）。</summary>
            public readonly float taxFree;
            public readonly float taxWeight;
            /// <summary>この債務を超えると不満（債務スパイラルの芽）。</summary>
            public readonly float debtTolerance;
            public readonly float debtPenalty;

            public EconomyStabilityParams(float hopeWeight, float taxFree, float taxWeight, float debtTolerance, float debtPenalty)
            {
                this.hopeWeight = hopeWeight;
                this.taxFree = taxFree;
                this.taxWeight = taxWeight;
                this.debtTolerance = debtTolerance;
                this.debtPenalty = debtPenalty;
            }

            /// <summary>既定：民心±10／税0.3超で減（0.8で約-10）／債務500超で-10。BaseStability50・RebelThreshold25 に効く強さ。</summary>
            public static EconomyStabilityParams Default => new EconomyStabilityParams(20f, 0.3f, 20f, 500f, 10f);
        }

        /// <summary>
        /// 国家状態から安定度への±補正（安定度目標へ加点）。民心が高ければ＋、低ければ−。高税・過大債務は−。
        /// null/未設定は中立寄り（hope 既定0.5＝0）。
        /// </summary>
        public static float StabilityModifier(FactionState fs, EconomyStabilityParams prm)
        {
            if (fs == null) return 0f;
            float hope = fs.community != null ? Mathf.Clamp01(fs.community.hope) : 0.5f;
            float mod = (hope - 0.5f) * prm.hopeWeight;                       // 民心（±）
            mod -= Mathf.Max(0f, fs.taxRate - prm.taxFree) * prm.taxWeight;   // 高税の不満（−）
            if (fs.fiscal != null && fs.fiscal.debt > prm.debtTolerance)
                mod -= prm.debtPenalty;                                       // 過大債務（−）
            return mod;
        }

        /// <summary>既定パラメータ版。</summary>
        public static float StabilityModifier(FactionState fs)
            => StabilityModifier(fs, EconomyStabilityParams.Default);
    }
}
