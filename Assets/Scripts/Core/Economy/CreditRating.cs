namespace Ginei
{
    /// <summary>
    /// 信用格付け（#185/#161 格付け会社・上ほど安全）。AAA〜BBB＝投資適格／BB以下＝投機的（ジャンク）／D＝デフォルト。
    /// 並び順＝信用度の高い順（AAA=0 .. D=7）。発行体（国/企業）の信用力を表し、債券のスプレッド/利回りを決める。解決は <see cref="CreditRatingRules"/>。
    /// </summary>
    public enum CreditRating { AAA, AA, A, BBB, BB, B, CCC, D }

    /// <summary>
    /// 格付け会社のロジック（#185/#161・純ロジック・唯一の窓口）。発行体の<b>信用力</b>（財政健全度#163/デフォルトリスク）を
    /// <b>格付け</b>に変換し、格付け→<b>信用スプレッド</b>（債券 <see cref="BondMarketRules"/> の利回り上乗せ）・<b>想定デフォルトリスク</b>へ写す。
    /// 格下げ＝借入コスト↑（利回り↑・価格↓）、格上げ＝↓。投資適格/ジャンクの線引き。少数の格（タイクン化回避）。test-first。
    /// </summary>
    public static class CreditRatingRules
    {
        // 格付け別の信用スプレッド（利回り上乗せ・AAAは0・Dは最大）。並び＝CreditRating の整数。
        private static readonly float[] spreadTable = { 0.00f, 0.005f, 0.01f, 0.02f, 0.04f, 0.07f, 0.12f, 0.20f };

        // 格付け別の想定デフォルトリスク（0..1・債券#185 の defaultRisk へ写す）。
        private static readonly float[] defaultRiskTable = { 0.00f, 0.01f, 0.03f, 0.07f, 0.15f, 0.30f, 0.50f, 1.00f };

        /// <summary>投資適格の下限（BBB以上＝投資適格、BB以下＝投機的/ジャンク）。</summary>
        public const CreditRating InvestmentGradeFloor = CreditRating.BBB;

        /// <summary>信用力（0..1・1＝最良）→格付け。財政健全度#163 や (1−デフォルトリスク) を渡す。</summary>
        public static CreditRating Rate(float creditworthiness)
        {
            float c = UnityEngine.Mathf.Clamp01(creditworthiness);
            if (c >= 0.95f) return CreditRating.AAA;
            if (c >= 0.85f) return CreditRating.AA;
            if (c >= 0.75f) return CreditRating.A;
            if (c >= 0.60f) return CreditRating.BBB;
            if (c >= 0.45f) return CreditRating.BB;
            if (c >= 0.30f) return CreditRating.B;
            if (c >= 0.15f) return CreditRating.CCC;
            return CreditRating.D;
        }

        /// <summary>デフォルトリスク（0..1）→格付け（リスクが低いほど高格付け）。</summary>
        public static CreditRating RatingFromDefaultRisk(float defaultRisk) => Rate(1f - defaultRisk);

        /// <summary>格付け→信用スプレッド（利回り上乗せ）。債券 <see cref="BondMarketRules.RequiredYield"/> に効く。</summary>
        public static float Spread(CreditRating rating) => spreadTable[(int)rating];

        /// <summary>格付け→想定デフォルトリスク（0..1）。債券の <see cref="Bond.defaultRisk"/> へ写せる。</summary>
        public static float DefaultRiskOf(CreditRating rating) => defaultRiskTable[(int)rating];

        /// <summary>投資適格か（BBB以上）。BB以下はジャンク＝機関投資家が買えない/プレミアム要求。</summary>
        public static bool IsInvestmentGrade(CreditRating rating) => (int)rating <= (int)InvestmentGradeFloor;

        /// <summary>格下げ（信用悪化）＝steps ノッチ悪化（Dで頭打ち）。</summary>
        public static CreditRating Downgrade(CreditRating rating, int steps = 1)
            => (CreditRating)UnityEngine.Mathf.Clamp((int)rating + UnityEngine.Mathf.Max(0, steps), 0, (int)CreditRating.D);

        /// <summary>格上げ（信用改善）＝steps ノッチ改善（AAAで頭打ち）。</summary>
        public static CreditRating Upgrade(CreditRating rating, int steps = 1)
            => (CreditRating)UnityEngine.Mathf.Clamp((int)rating - UnityEngine.Mathf.Max(0, steps), 0, (int)CreditRating.D);
    }
}
