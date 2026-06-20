namespace Ginei
{
    /// <summary>
    /// 反乱＝内政の失敗が戦略地図に牙を剥く創発ループの純ロジック（内政↔戦略↔勝敗・test-first・唯一の窓口）。
    /// これまで <see cref="GovernanceRules.RebelPressure"/>（反乱圧）は算出・表示されるだけで帰結が無かった。
    /// ここでは「慢性的な不穏」を年ごとに<b>不穏スコア</b>へ積み（安定すれば回復）、閾値で<b>離反</b>とみなす。
    /// ＝高税/債務/占領直後/補給切れ → 安定度低下 → 反乱 → 星系喪失 → 敗北、という台本なしの因果を作る。
    /// 帰結（誰に離反するか・通知・勝敗波及）は <see cref="GovernanceRules"/> でなく `GalaxyView`(盤面) が決める。
    /// 決定論（乱数なし＝スコア累積）＝「不穏が続けば必ず反乱」で因果が読める。
    /// </summary>
    public static class RebellionRules
    {
        /// <summary>この不穏スコアに達したら離反（不穏の重さ次第で約2〜4年）。</summary>
        public const float RevoltThreshold = 2.0f;

        /// <summary>安定していれば年ごとに減る不穏スコア（鎮静）。</summary>
        public const float RecoverPerYear = 0.5f;

        /// <summary>この割合（×閾値）を超えたら「反乱の兆し」を警告する（プレイヤーが手を打てる猶予）。</summary>
        public const float WarnFraction = 0.6f;

        /// <summary>
        /// 不穏スコアを1年ぶん更新する。反乱リスク域（<see cref="GovernanceRules.IsUnrest"/>）なら
        /// 反乱圧（0..1）ぶん上昇、安定していれば <see cref="RecoverPerYear"/> ぶん回復（0 下限）。
        /// </summary>
        public static float NextScore(float current, Province p)
        {
            if (p != null && GovernanceRules.IsUnrest(p))
                return current + GovernanceRules.RebelPressure(p);
            return UnityEngine.Mathf.Max(0f, current - RecoverPerYear);
        }

        /// <summary>不穏スコアが離反に達したか。</summary>
        public static bool ShouldRevolt(float score) => score >= RevoltThreshold;

        /// <summary>反乱の兆し域か（離反前の警告ライン）。</summary>
        public static bool IsBrewing(float score) => score >= RevoltThreshold * WarnFraction;
    }
}
