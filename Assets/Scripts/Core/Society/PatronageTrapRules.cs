using UnityEngine;

namespace Ginei
{
    /// <summary>後見依存ジレンマの調整係数（CEND-2 #2358）。</summary>
    public readonly struct PatronageParams
    {
        /// <summary>依存度の自然低下/秒（援助が止まれば徐々に自立を取り戻す）。</summary>
        public readonly float decayRate;

        /// <summary>累積援助→依存度の感度（援助量がどれだけ速く依存を生むか）。大きいほど少量で依存。</summary>
        public readonly float aidSensitivity;

        /// <summary>依存が有機成長上限を削る強さ(0..1)。1＝依存1で自力成長ゼロ。</summary>
        public readonly float capStrength;

        /// <summary>短期ブーストの最大上乗せ幅（依存1のとき指標へ加わる倍率増分）。</summary>
        public readonly float shortTermBoostScale;

        /// <summary>この依存度以上で「発育不全（自立不能）」とみなす閾値(0..1)。</summary>
        public readonly float stuntedThreshold;

        public PatronageParams(float decayRate, float aidSensitivity, float capStrength,
            float shortTermBoostScale, float stuntedThreshold)
        {
            this.decayRate = Mathf.Max(0f, decayRate);
            this.aidSensitivity = Mathf.Max(0f, aidSensitivity);
            this.capStrength = Mathf.Clamp01(capStrength);
            this.shortTermBoostScale = Mathf.Max(0f, shortTermBoostScale);
            this.stuntedThreshold = Mathf.Clamp01(stuntedThreshold);
        }

        /// <summary>
        /// 既定＝低下0.05/秒・援助感度0.01・上限削り0.8・短期ブースト0.4・発育不全閾値0.7。
        /// 援助を絶てば約20秒で依存が1段抜ける一方、受け続ければ自力成長は最大8割まで削られる。
        /// </summary>
        public static PatronageParams Default => new PatronageParams(0.05f, 0.01f, 0.8f, 0.4f, 0.7f);
    }

    /// <summary>
    /// 後見依存ジレンマの純ロジック（CEND-2 #2358）。外部の庇護者の「贈り物」は短期の指標
    /// （安定・戦力・生活水準）を底上げするが、受け続けるほど<b>有機的（自律的）成長の上限を下げる</b>＝
    /// 依存の罠。オーバーロードが与えた平和が人類の自力探査能力を奪った、という構造を表す。
    /// <see cref="PatronageTrapRules.OrganicGrowthCap"/> は成長係数の<b>上限倍率</b>を返すだけで、
    /// 実際の成長は呼び出し側（概念上 <c>FactionStateRules.Tick</c> 等）が乗算して適用する
    /// （ここでは他モジュールを直接書き換えない）。`TranscendenceRules`(CEND-1) には
    /// <c>1 - dependencyRatio</c> を渡せる。すべて plain な float で受け渡す。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PatronageTrapRules
    {
        /// <summary>
        /// 援助の受領で依存状態を進める。累積援助＝aidAccumulator + max(0,aidAmount)、
        /// 依存度＝1 - exp(-感度×累積)（累積が増すほど1へ漸近・飽和）。負の援助は無視（負債化しない）。
        /// </summary>
        public static PatronageState AccumulateDependency(PatronageState state, float aidAmount, PatronageParams p)
        {
            float aid = Mathf.Max(0f, aidAmount);
            float acc = Mathf.Max(0f, state.aidAccumulator) + aid;
            float dep = 1f - Mathf.Exp(-p.aidSensitivity * acc);
            return new PatronageState(Mathf.Clamp01(dep), acc);
        }

        public static PatronageState AccumulateDependency(PatronageState state, float aidAmount)
            => AccumulateDependency(state, aidAmount, PatronageParams.Default);

        /// <summary>
        /// 依存度の自然低下（dt後）。援助が止まれば <see cref="PatronageParams.decayRate"/>×dt ずつ
        /// 自立を取り戻す。累積援助カウンタも同じ割合で目減りさせ、依存度と整合させる
        /// （再び援助されたとき過去の蓄積が残り過ぎないように）。
        /// </summary>
        public static PatronageState DependencyDecay(PatronageState state, float dt, PatronageParams p)
        {
            float step = Mathf.Max(0f, dt);
            float drop = p.decayRate * step;
            float dep = Mathf.Clamp01(state.dependencyRatio - drop);
            // 依存度が下がった比率ぶん累積も縮める（依存ゼロなら累積ゼロ）。
            float prev = Mathf.Clamp01(state.dependencyRatio);
            float scale = prev > 0f ? dep / prev : 0f;
            float acc = Mathf.Max(0f, state.aidAccumulator) * scale;
            return new PatronageState(dep, acc);
        }

        public static PatronageState DependencyDecay(PatronageState state, float dt)
            => DependencyDecay(state, dt, PatronageParams.Default);

        /// <summary>
        /// 有機的成長の上限倍率(0..1)＝1 - capStrength×依存度。依存が高いほど自力成長の天井が下がる。
        /// 依存0で1.0（無制限＝従来動作）、依存1で 1-capStrength（既定0.2＝自力成長は2割まで）。
        /// 呼び出し側はこの倍率を成長係数に乗じて頭打ちにする（基準値は破壊しない＝実効値パターン）。
        /// </summary>
        public static float OrganicGrowthCap(float dependencyRatio, PatronageParams p)
        {
            float dep = Mathf.Clamp01(dependencyRatio);
            return Mathf.Clamp01(1f - p.capStrength * dep);
        }

        public static float OrganicGrowthCap(float dependencyRatio)
            => OrganicGrowthCap(dependencyRatio, PatronageParams.Default);

        /// <summary>state 版の有機成長上限。</summary>
        public static float OrganicGrowthCap(PatronageState state, PatronageParams p)
            => OrganicGrowthCap(state.dependencyRatio, p);

        /// <summary>
        /// 短期ブースト倍率(>=1)＝1 + shortTermBoostScale×依存度。援助を受けるほど短期の指標
        /// （安定・戦力・生活水準）が底上げされる＝罠の「甘い側」。依存0で1.0、依存1で 1+scale。
        /// </summary>
        public static float ShortTermBoost(float dependencyRatio, PatronageParams p)
        {
            float dep = Mathf.Clamp01(dependencyRatio);
            return 1f + p.shortTermBoostScale * dep;
        }

        public static float ShortTermBoost(float dependencyRatio)
            => ShortTermBoost(dependencyRatio, PatronageParams.Default);

        /// <summary>state 版の短期ブースト。</summary>
        public static float ShortTermBoost(PatronageState state, PatronageParams p)
            => ShortTermBoost(state.dependencyRatio, p);

        /// <summary>
        /// 庇護者撤退ショック(0..1)＝依存度の二乗。援助が突然絶たれたときの打撃は依存が深いほど
        /// 加速度的に大きい（依存0.5で0.25、依存1で1.0）。呼び出し側が安定/戦力の急落幅に使う。
        /// </summary>
        public static float WithdrawalShock(float dependencyRatio)
        {
            float dep = Mathf.Clamp01(dependencyRatio);
            return dep * dep;
        }

        /// <summary>state 版の撤退ショック。</summary>
        public static float WithdrawalShock(PatronageState state)
            => WithdrawalShock(state.dependencyRatio);

        /// <summary>
        /// 発育不全（自立不能）か＝依存度が <see cref="PatronageParams.stuntedThreshold"/> 以上。
        /// true なら有機的変容（CEND-1）に進めない＝庇護者なしには立ち行かない。
        /// </summary>
        public static bool IsStunted(float dependencyRatio, PatronageParams p)
            => Mathf.Clamp01(dependencyRatio) >= p.stuntedThreshold;

        public static bool IsStunted(float dependencyRatio)
            => IsStunted(dependencyRatio, PatronageParams.Default);

        /// <summary>state 版の発育不全判定。</summary>
        public static bool IsStunted(PatronageState state, PatronageParams p)
            => IsStunted(state.dependencyRatio, p);
    }
}
