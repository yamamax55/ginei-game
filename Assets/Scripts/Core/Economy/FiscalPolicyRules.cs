using UnityEngine;

namespace Ginei
{
    /// <summary>逸脱の種別（重大度の高い順に優先して対処する）。</summary>
    public enum PolicyBreach
    {
        なし,
        債務超過警戒, // 債務比率が上限を超過＝最優先（破綻リスク）
        準備金不足,   // 準備金が下限を割れ＝資金繰り余裕の喪失
        税率逸脱      // 税率がレンジ外（高すぎは不満・低すぎは歳入不足）
    }

    /// <summary>
    /// 財務ガードレールの方針（純データ＝国策で定める「枠」）。債務上限比率・準備金下限・税率の上下限。
    /// 値は枠そのもの（手すりの位置）であり、財政の実体（債務/準備金/税率の現在値）はここには持たない。
    /// </summary>
    public readonly struct FiscalPolicy
    {
        public readonly float debtCeilingRatio; // 債務比率の上限（FiscalRules.DebtRatio と比較）
        public readonly float reserveFloor;      // 準備金の下限（これを割ると逸脱）
        public readonly float taxRateMin;        // 税率の下限（低すぎ＝歳入不足）
        public readonly float taxRateMax;        // 税率の上限（高すぎ＝不満）

        public FiscalPolicy(float debtCeilingRatio, float reserveFloor, float taxRateMin, float taxRateMax)
        {
            this.debtCeilingRatio = Mathf.Max(0f, debtCeilingRatio);
            this.reserveFloor = Mathf.Max(0f, reserveFloor);
            float lo = Mathf.Clamp01(taxRateMin);
            float hi = Mathf.Clamp01(taxRateMax);
            // min<=max を保証（逆転入力はクランプ後の値で min を下限に揃える）
            this.taxRateMin = lo;
            this.taxRateMax = Mathf.Max(lo, hi);
        }

        /// <summary>既定の手すり位置＝債務比率上限1.0／準備金下限50／税率レンジ0.1〜0.4。</summary>
        public static FiscalPolicy Default => new FiscalPolicy(1.0f, 50f, 0.1f, 0.4f);
    }

    /// <summary>
    /// 財務ガードレールの逸脱判定（#1013・国策チャネル＝財政の手すり）。財政運営の方針（債務上限/準備金下限/税率レンジ）を
    /// <see cref="FiscalPolicy"/> の数値の枠として定め、実際の財政状態が枠を逸脱したかを検知する<b>だけ</b>。
    /// 「ポリシーは財政の手すり＝逸脱を早期に知らせるが財政そのものは別が動かす」＝債務/準備金/税率を増減する財政の実体は
    /// <see cref="FiscalRules"/>（PB・金利・国債・債務スパイラル）が、この逸脱を入力に減税/起債/積み増し等を自動で行うのは
    /// AutoTreasuryRules（自律財務・バックログ）が担う。ここは<b>逸脱検知のみ</b>＝実体も自動行動も動かさない（読み取り専用の警報）。
    /// 全入力クランプ・乱数なし決定論。test-first。
    /// </summary>
    public static class FiscalPolicyRules
    {
        /// <summary>遵守度の重み（各枠の遵守度を合成する際の配分＝合計1）。債務＝最重要。</summary>
        public const float DebtWeight = 0.5f;
        public const float ReserveWeight = 0.3f;
        public const float TaxWeight = 0.2f;

        /// <summary>債務上限の逸脱＝<see cref="FiscalRules.DebtRatio"/> が policy の上限を超えたか。</summary>
        public static bool DebtCeilingBreached(float debtRatio, FiscalPolicy policy)
            => Mathf.Max(0f, debtRatio) > policy.debtCeilingRatio;

        /// <summary>準備金下限の割れ＝現在の準備金が policy の下限を下回ったか。</summary>
        public static bool ReserveFloorBreached(float reserves, FiscalPolicy policy)
            => Mathf.Max(0f, reserves) < policy.reserveFloor;

        /// <summary>税率がレンジ内か＝[min,max] に収まれば true（高すぎ＝不満・低すぎ＝歳入不足のいずれも外れ）。</summary>
        public static bool TaxRateInRange(float taxRate, FiscalPolicy policy)
        {
            float t = Mathf.Clamp01(taxRate);
            return t >= policy.taxRateMin && t <= policy.taxRateMax;
        }

        /// <summary>
        /// 最も重大な逸脱を返す（複数の枠が同時に外れていても、最優先で対処すべき1つ＝enum で返す）。
        /// 優先順＝債務超過警戒 ＞ 準備金不足 ＞ 税率逸脱（破綻リスクの大きい順）。どれも守れていれば なし。
        /// </summary>
        public static PolicyBreach WorstBreach(float debtRatio, float reserves, float taxRate, FiscalPolicy policy)
        {
            if (DebtCeilingBreached(debtRatio, policy)) return PolicyBreach.債務超過警戒;
            if (ReserveFloorBreached(reserves, policy)) return PolicyBreach.準備金不足;
            if (!TaxRateInRange(taxRate, policy)) return PolicyBreach.税率逸脱;
            return PolicyBreach.なし;
        }

        /// <summary>
        /// 財政余裕＝債務上限まであと何ぶん起債できるか（上限比率−現在比率、0未満は上限到達で0）。
        /// 手すりまでの距離＝早期警告の指標。財政の実体は動かさない。
        /// </summary>
        public static float FiscalHeadroom(float debtRatio, FiscalPolicy policy)
            => Mathf.Max(0f, policy.debtCeilingRatio - Mathf.Max(0f, debtRatio));

        /// <summary>
        /// 総合遵守度 0..1＝すべての枠をどれだけ守れているかの加重平均。各枠の遵守度（0..1）を重みで合成する。
        /// 債務＝上限までの余地を上限で正規化（超過で0）／準備金＝下限を満たせば1・割れるほど線形に減（0で0）／
        /// 税率＝レンジ内で1・レンジ幅ぶん外れるごとに線形に減。手すりからの距離の総合スコア（実体は別）。
        /// </summary>
        public static float PolicyComplianceScore(float debtRatio, float reserves, float taxRate, FiscalPolicy policy)
        {
            // 債務：上限までの余地を上限で正規化（上限0なら逸脱有無で0/1）。
            float debtScore;
            if (policy.debtCeilingRatio <= 0f)
                debtScore = DebtCeilingBreached(debtRatio, policy) ? 0f : 1f;
            else
                debtScore = Mathf.Clamp01(FiscalHeadroom(debtRatio, policy) / policy.debtCeilingRatio);

            // 準備金：下限を満たせば1、割れるほど線形に減（下限0なら常に満たす＝1）。
            float r = Mathf.Max(0f, reserves);
            float reserveScore = policy.reserveFloor <= 0f ? 1f : Mathf.Clamp01(r / policy.reserveFloor);

            // 税率：レンジ内なら1、外れた距離をレンジ幅で正規化して減点。
            float taxScore;
            float t = Mathf.Clamp01(taxRate);
            float width = policy.taxRateMax - policy.taxRateMin;
            if (TaxRateInRange(taxRate, policy))
                taxScore = 1f;
            else if (width <= 0f)
                taxScore = 0f; // 幅ゼロのレンジから外れたら遵守なし
            else
            {
                float dist = t < policy.taxRateMin ? (policy.taxRateMin - t) : (t - policy.taxRateMax);
                taxScore = Mathf.Clamp01(1f - dist / width);
            }

            return Mathf.Clamp01(debtScore * DebtWeight + reserveScore * ReserveWeight + taxScore * TaxWeight);
        }
    }
}
