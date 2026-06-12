using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// サブプライム証券化と格付けインフレのロジック（#185・純ロジック・唯一の窓口）。住宅ローン（プライム/サブプライム）を束ねた
    /// 証券化商品（<see cref="MortgageBundle"/>）の<b>真の格付け</b>（中身の加重デフォルトリスク）と<b>表示格付け</b>を分ける：
    /// <b>規制（SOX法）制定前は中身にジャンク（BB以下）が紛れ込んでいても AAA と表示</b>（利益相反・甘い監査＝格付けインフレ）／
    /// SOX法で規制されると<b>真の格付けに是正</b>され、隠れたリスク（AAA↔真の格差）が露呈＝損失（暴落#185の引き金）。test-first。
    /// </summary>
    public static class SubprimeRules
    {
        /// <summary>プライム借り手の代表デフォルトリスク（低い）。</summary>
        public const float PrimeDefaultRisk = 0.02f;

        /// <summary>サブプライム借り手の代表デフォルトリスク（高い＝本来ジャンク）。</summary>
        public const float SubprimeDefaultRisk = 0.50f;

        /// <summary>種別→代表デフォルトリスク。</summary>
        public static float DefaultRiskFor(LoanType type)
            => type == LoanType.サブプライム ? SubprimeDefaultRisk : PrimeDefaultRisk;

        /// <summary>束の総元本。</summary>
        public static float TotalPrincipal(MortgageBundle b)
        {
            if (b == null || b.loans == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < b.loans.Count; i++) if (b.loans[i] != null) sum += Mathf.Max(0f, b.loans[i].principal);
            return sum;
        }

        /// <summary>サブプライムの割合（元本比・0..1）。高いほど本来は危ない。</summary>
        public static float SubprimeShare(MortgageBundle b)
        {
            float total = TotalPrincipal(b);
            if (total <= 0f) return 0f;
            float sub = 0f;
            for (int i = 0; i < b.loans.Count; i++)
                if (b.loans[i] != null && b.loans[i].type == LoanType.サブプライム) sub += Mathf.Max(0f, b.loans[i].principal);
            return sub / total;
        }

        /// <summary>束の加重平均デフォルトリスク（元本加重）。サブプライムが多いほど高い。</summary>
        public static float PoolDefaultRisk(MortgageBundle b)
        {
            float total = TotalPrincipal(b);
            if (total <= 0f) return 0f;
            float weighted = 0f;
            for (int i = 0; i < b.loans.Count; i++)
                if (b.loans[i] != null) weighted += Mathf.Max(0f, b.loans[i].principal) * Mathf.Clamp01(b.loans[i].defaultRisk);
            return weighted / total;
        }

        /// <summary>真の格付け＝中身の加重リスクから算定（<see cref="CreditRatingRules"/>）。サブプライム過多はジャンク。</summary>
        public static CreditRating TrueRating(MortgageBundle b)
            => CreditRatingRules.RatingFromDefaultRisk(PoolDefaultRisk(b));

        /// <summary>
        /// 表示格付け＝<b>SOX法制定前は AAA（中身にジャンクが混ざっていても）</b>、制定後は真の格付けに是正される。
        /// </summary>
        public static CreditRating StatedRating(MortgageBundle b, bool soxEnacted)
            => soxEnacted ? TrueRating(b) : CreditRating.AAA;

        /// <summary>格付けインフレか＝規制前で、真の格付けが投資適格でない（ジャンクが AAA に紛れ込む）。</summary>
        public static bool IsRatingInflated(MortgageBundle b, bool soxEnacted)
            => !soxEnacted && !CreditRatingRules.IsInvestmentGrade(TrueRating(b));

        /// <summary>隠れたリスク＝真の格付けの想定リスク−表示格付けの想定リスク（規制前は AAA に隠される）。0..1。</summary>
        public static float HiddenRisk(MortgageBundle b, bool soxEnacted)
            => Mathf.Max(0f, CreditRatingRules.DefaultRiskOf(TrueRating(b)) - CreditRatingRules.DefaultRiskOf(StatedRating(b, soxEnacted)));

        /// <summary>
        /// 真実が露呈したときの損失＝隠れたリスク×総元本（SOX法制定/デフォルト顕在化で AAA が真の格付けに是正された瞬間の評価損）。
        /// 規制後は表示＝真なので隠れた損失は0。暴落（#185）の引き金。
        /// </summary>
        public static float RevealLoss(MortgageBundle b, bool soxEnacted)
            => HiddenRisk(b, soxEnacted) * TotalPrincipal(b);
    }
}
