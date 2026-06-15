using UnityEngine;

namespace Ginei
{
    /// <summary>包囲の調整係数（包囲殲滅戦）。</summary>
    public readonly struct EncirclementParams
    {
        /// <summary>完全包囲とみなす包囲度の閾値。</summary>
        public readonly float fullSurroundThreshold;
        /// <summary>包囲が士気を削る速度（per dt・完全包囲時）。</summary>
        public readonly float moraleDrainRate;
        /// <summary>完全包囲かつ士気ゼロでの降伏率の上限。</summary>
        public readonly float maxSurrenderChance;
        /// <summary>突囲（包囲突破）の基礎損害率。</summary>
        public readonly float breakoutCasualtyBase;

        public EncirclementParams(float fullSurroundThreshold, float moraleDrainRate,
                                  float maxSurrenderChance, float breakoutCasualtyBase)
        {
            this.fullSurroundThreshold = Mathf.Clamp01(fullSurroundThreshold);
            this.moraleDrainRate = Mathf.Max(0f, moraleDrainRate);
            this.maxSurrenderChance = Mathf.Clamp01(maxSurrenderChance);
            this.breakoutCasualtyBase = Mathf.Clamp01(breakoutCasualtyBase);
        }

        /// <summary>既定＝完全包囲0.9・士気減0.05・降伏上限0.8・突囲基礎損害0.3。</summary>
        public static EncirclementParams Default => new EncirclementParams(0.9f, 0.05f, 0.8f, 0.3f);
    }

    /// <summary>
    /// 包囲の純ロジック（包囲殲滅戦）。包囲度（退路の遮断割合 0..1）が上がるほど被包囲側の士気が痩せ、
    /// 完全包囲＋低士気で降伏が現実になる。突囲（脱出）は包囲が固いほど高くつき、薄い一角を破るのが定石。
    /// 個人の捕虜化は <see cref="CaptivityRules"/>（降伏後の処遇へ委譲）、ZOC遮断は Game層
    /// （`ZoneOfControl`）が担い、ここは部隊規模の包囲解決のみ。乱数は roll で決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EncirclementRules
    {
        /// <summary>包囲度（0..1）＝遮断された退路方向の割合。総方向0は包囲なし＝0。</summary>
        public static float Coverage(int blockedDirections, int totalDirections)
        {
            if (totalDirections <= 0) return 0f;
            return Mathf.Clamp01(Mathf.Max(0, blockedDirections) / (float)totalDirections);
        }

        /// <summary>完全包囲か＝包囲度が閾値以上。</summary>
        public static bool IsFullySurrounded(float coverage, EncirclementParams p)
        {
            return Mathf.Clamp01(coverage) >= p.fullSurroundThreshold;
        }

        public static bool IsFullySurrounded(float coverage)
            => IsFullySurrounded(coverage, EncirclementParams.Default);

        /// <summary>包囲の士気減（per dt）＝包囲度×減衰率×dt（袋の口が閉じるほど心が折れる）。</summary>
        public static float MoraleDrain(float coverage, float dt, EncirclementParams p)
        {
            return Mathf.Clamp01(coverage) * p.moraleDrainRate * Mathf.Max(0f, dt);
        }

        public static float MoraleDrain(float coverage, float dt)
            => MoraleDrain(coverage, dt, EncirclementParams.Default);

        /// <summary>
        /// 降伏確率（0..maxSurrenderChance）。完全包囲でなければ0（逃げ道がある限り戦う）。
        /// 完全包囲下では士気の低さに比例して上がる。
        /// </summary>
        public static float SurrenderChance(float coverage, float morale, EncirclementParams p)
        {
            if (!IsFullySurrounded(coverage, p)) return 0f;
            return (1f - Mathf.Clamp01(morale)) * p.maxSurrenderChance;
        }

        public static float SurrenderChance(float coverage, float morale)
            => SurrenderChance(coverage, morale, EncirclementParams.Default);

        /// <summary>降伏判定。roll∈[0,1) が降伏率未満なら降伏＝true（決定論）。</summary>
        public static bool Surrenders(float coverage, float morale, float roll, EncirclementParams p)
        {
            return roll < SurrenderChance(coverage, morale, p);
        }

        public static bool Surrenders(float coverage, float morale, float roll)
            => Surrenders(coverage, morale, roll, EncirclementParams.Default);

        /// <summary>
        /// 突囲の損害率（0..1）＝基礎損害×包囲度×（包囲側/被包囲側の戦力比、上限2倍）。
        /// 薄い包囲・弱い包囲側なら安く抜けられる。
        /// </summary>
        public static float BreakoutCasualtyRatio(float coverage, float encirclerStrength, float trappedStrength, EncirclementParams p)
        {
            float trapped = Mathf.Max(0.0001f, trappedStrength);
            float ratio = Mathf.Min(2f, Mathf.Max(0f, encirclerStrength) / trapped);
            return Mathf.Clamp01(p.breakoutCasualtyBase * Mathf.Clamp01(coverage) * ratio);
        }

        public static float BreakoutCasualtyRatio(float coverage, float encirclerStrength, float trappedStrength)
            => BreakoutCasualtyRatio(coverage, encirclerStrength, trappedStrength, EncirclementParams.Default);
    }
}
