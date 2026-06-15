using UnityEngine;

namespace Ginei
{
    /// <summary>虐殺・無差別攻撃の調整係数（ヴェスターラント型）。</summary>
    public readonly struct AtrocityParams
    {
        /// <summary>規模最大の苛烈行為が対象地の抵抗を即時に削る最大量（恐怖の即効）。</summary>
        public readonly float immediateSuppression;
        /// <summary>実行者が負う正統性の汚点の最大幅（露出度最大のとき）。</summary>
        public readonly float maxLegitimacyStain;
        /// <summary>敵に渡る宣伝素材の最大価値（露出度最大のとき）。</summary>
        public readonly float maxPropagandaValue;
        /// <summary>「防げたのに黙認した」側が負う隠れた汚点の係数（実行者の汚点に対する比）。</summary>
        public readonly float condonerStainRatio;
        /// <summary>汚点の自然減衰率（per dt・忘却は遅い）。</summary>
        public readonly float stainDecayRate;

        public AtrocityParams(float immediateSuppression, float maxLegitimacyStain, float maxPropagandaValue,
                              float condonerStainRatio, float stainDecayRate)
        {
            this.immediateSuppression = Mathf.Clamp01(immediateSuppression);
            this.maxLegitimacyStain = Mathf.Max(0f, maxLegitimacyStain);
            this.maxPropagandaValue = Mathf.Max(0f, maxPropagandaValue);
            this.condonerStainRatio = Mathf.Clamp01(condonerStainRatio);
            this.stainDecayRate = Mathf.Max(0f, stainDecayRate);
        }

        /// <summary>既定＝即時鎮圧0.5・汚点幅0.4・宣伝価値0.5・黙認比0.5・減衰0.005。</summary>
        public static AtrocityParams Default => new AtrocityParams(0.5f, 0.4f, 0.5f, 0.5f, 0.005f);
    }

    /// <summary>
    /// 虐殺・無差別攻撃の純ロジック（ヴェスターラント型）。苛烈行為は対象地の抵抗を恐怖で即座に黙らせるが、
    /// 露出した瞬間に実行者の正統性を蝕み、敵には極上の宣伝素材を渡す＝短期の戦果と引き換えの長期の汚点。
    /// 「防げたのに黙認した」者（敵の蛮行を意図的に見過ごして敵の自滅を待つ選択）も隠れた汚点を負い、
    /// 秘密の発覚で表面化する＝黙認は安いが無料ではない。世論への波及は <see cref="PropagandaRules"/>
    /// を read-only で参照する想定（宣伝価値はその素材の強さ）。乱数は roll で決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AtrocityRules
    {
        /// <summary>恐怖の即効＝対象地の抵抗低下量（0..immediateSuppression）＝規模 scale(0..1) に比例。</summary>
        public static float ImmediateSuppression(float scale, AtrocityParams p)
        {
            return Mathf.Clamp01(scale) * p.immediateSuppression;
        }

        public static float ImmediateSuppression(float scale) => ImmediateSuppression(scale, AtrocityParams.Default);

        /// <summary>実行者の正統性汚点（0..maxLegitimacyStain）＝規模×露出度 exposure(0..1)。隠し通せた蛮行は（当面）無傷。</summary>
        public static float PerpetratorStain(float scale, float exposure, AtrocityParams p)
        {
            return Mathf.Clamp01(scale) * Mathf.Clamp01(exposure) * p.maxLegitimacyStain;
        }

        public static float PerpetratorStain(float scale, float exposure)
            => PerpetratorStain(scale, exposure, AtrocityParams.Default);

        /// <summary>敵に渡る宣伝素材の価値（0..maxPropagandaValue）＝規模×露出度。敵の `PropagandaRules` の主張強度の入力。</summary>
        public static float PropagandaValue(float scale, float exposure, AtrocityParams p)
        {
            return Mathf.Clamp01(scale) * Mathf.Clamp01(exposure) * p.maxPropagandaValue;
        }

        public static float PropagandaValue(float scale, float exposure)
            => PropagandaValue(scale, exposure, AtrocityParams.Default);

        /// <summary>
        /// 黙認者の隠れた汚点＝実行者の汚点（露出度1換算）×黙認比。発覚するまで表面化しない
        /// （発覚判定は <see cref="CondonementExposed"/>）。
        /// </summary>
        public static float CondonerHiddenStain(float scale, AtrocityParams p)
        {
            return PerpetratorStain(scale, 1f, p) * p.condonerStainRatio;
        }

        public static float CondonerHiddenStain(float scale) => CondonerHiddenStain(scale, AtrocityParams.Default);

        /// <summary>
        /// 黙認の発覚判定。秘密を知る者の数 witnesses が多いほど漏れる（発覚率＝1−0.9^witnesses）。
        /// roll∈[0,1) が発覚率未満なら露見＝隠れた汚点が表面化する（決定論）。
        /// </summary>
        public static bool CondonementExposed(int witnesses, float roll)
        {
            float leakChance = 1f - Mathf.Pow(0.9f, Mathf.Max(0, witnesses));
            return roll < leakChance;
        }

        /// <summary>汚点の1tick後の値。ゆっくり風化する（stainDecayRate×dt）が、ゼロになるのは遠い。</summary>
        public static float StainTick(float stain, float dt, AtrocityParams p)
        {
            return Mathf.Max(0f, stain - p.stainDecayRate * Mathf.Max(0f, dt));
        }

        public static float StainTick(float stain, float dt) => StainTick(stain, dt, AtrocityParams.Default);
    }
}
