using UnityEngine;

namespace Ginei
{
    /// <summary>ランチェスター集中効果の調整値（#会戦ダメージ）。火力比の効き（指数）と倍率の上下限。</summary>
    public readonly struct LanchesterParams
    {
        /// <summary>局所火力比に対する効きの指数（0.5＝平方根で穏やか／1.0＝線形で強い）。</summary>
        public readonly float exponent;
        /// <summary>倍率の下限（局所劣勢でこれ以下に下げない）。</summary>
        public readonly float minFactor;
        /// <summary>倍率の上限（局所優勢でこれ以上に上げない＝暴走防止）。</summary>
        public readonly float maxFactor;

        public LanchesterParams(float exponent, float minFactor, float maxFactor)
        {
            this.exponent = exponent;
            this.minFactor = minFactor;
            this.maxFactor = maxFactor;
        }

        /// <summary>既定：指数0.5（平方根）・倍率0.5〜2.0。局所4:1で2倍・1:4で0.5倍。</summary>
        public static LanchesterParams Default => new LanchesterParams(DefaultExponent, DefaultMinFactor, DefaultMaxFactor);

        public const float DefaultExponent = 0.5f;
        public const float DefaultMinFactor = 0.5f;
        public const float DefaultMaxFactor = 2.0f;
    }

    /// <summary>
    /// ランチェスターの法則（二乗則）を会戦のダメージに織り込む純ロジック（#会戦ダメージ）。
    /// <b>単純な総兵力の比較ではなく、一定範囲内の局所火力差</b>でダメージを増減させる＝火力の集中が二乗で効く。
    /// 局所的に火力優勢な側は1発が重く（最大 <see cref="LanchesterParams.maxFactor"/>）、劣勢側は軽い（最小 minFactor）。
    /// 各艦が個別に発砲する既存モデル（数が多いほど総発砲数が増える＝一乗）に、この集中倍率を掛けることで
    /// 「局所優勢が不利を覆す／劣勢が各個撃破される」という二乗則の体感を与える。test-first・実効値パターン（基準値非破壊）。
    /// 局所火力の集計は <see cref="ShipCombat.LocalFirepower"/>（一定範囲内の味方/敵旗艦の火力）。
    /// </summary>
    public static class LanchesterRules
    {
        /// <summary>既定パラメータで局所火力比からダメージ集中倍率を返す。</summary>
        public static float ConcentrationFactor(float localFriendlyPower, float localEnemyPower)
            => ConcentrationFactor(localFriendlyPower, localEnemyPower, LanchesterParams.Default);

        /// <summary>
        /// 局所火力比（味方/敵）からダメージ集中倍率を返す。比が1（拮抗）で1.0、優勢で>1、劣勢で<1。
        /// `factor = clamp(pow(味方/敵, exponent), min, max)`。敵火力0＝最大集中、味方火力0＝最小。
        /// </summary>
        public static float ConcentrationFactor(float localFriendlyPower, float localEnemyPower, LanchesterParams p)
        {
            float friendly = Mathf.Max(0f, localFriendlyPower);
            float enemy = Mathf.Max(0f, localEnemyPower);
            float min = Mathf.Min(p.minFactor, p.maxFactor);
            float max = Mathf.Max(p.minFactor, p.maxFactor);

            if (enemy <= 0f) return Mathf.Max(1f, max); // 敵火力ゼロ＝最大集中（1未満に下げない）
            if (friendly <= 0f) return min;

            float ratio = friendly / enemy;
            float factor = Mathf.Pow(ratio, Mathf.Max(0f, p.exponent));
            return Mathf.Clamp(factor, min, max);
        }
    }
}
