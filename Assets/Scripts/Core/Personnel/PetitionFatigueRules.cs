using UnityEngine;

namespace Ginei
{
    /// <summary>具申の食傷（乱発ペナルティ）パラメータ。</summary>
    public readonly struct PetitionFatigueParams
    {
        public readonly int freePending;     // この件数までは未決でもペナルティなし
        public readonly float decayPerExtra; // 超過1件あたり採用率に掛ける減衰（0..1）

        public PetitionFatigueParams(int freePending, float decayPerExtra)
        {
            this.freePending = freePending;
            this.decayPerExtra = decayPerExtra;
        }

        /// <summary>既定：2件まで無罰／超過1件ごとに採用率×0.8（乱発するほど上官が食傷）。</summary>
        public static PetitionFatigueParams Default => new PetitionFatigueParams(2, 0.8f);
    }

    /// <summary>
    /// 具申の乱発ペナルティ（純・決定論）。未決の具申が積み上がるほど上官が食傷し、採用率が下がる＝「信用コスト」。
    /// 基準採用率に、未決件数の超過ぶんだけ減衰を掛けて実効採用率を返す唯一の窓口。<see cref="ProtagonistCareerDirector"/>
    /// の採用判定と採用見込み表示が共用する（判定と表示がズレない）。test-first。
    /// </summary>
    public static class PetitionFatigueRules
    {
        /// <summary>無罰枠を超えた未決件数（0以上）。</summary>
        public static int ExcessPending(int pendingCount, PetitionFatigueParams p)
            => Mathf.Max(0, pendingCount - Mathf.Max(0, p.freePending));

        /// <summary>食傷による採用率の倍率（超過なし＝1.0／超過1件ごとに decay を累乗）。</summary>
        public static float FatigueFactor(int pendingCount, PetitionFatigueParams p)
        {
            int extra = ExcessPending(pendingCount, p);
            float f = 1f;
            float decay = Mathf.Clamp01(p.decayPerExtra);
            for (int i = 0; i < extra; i++) f *= decay;
            return f;
        }

        /// <summary>実効採用率＝基準採用率 × 食傷倍率（0..1 クランプ）。</summary>
        public static float EffectiveAdoptChance(float baseChance, int pendingCount, PetitionFatigueParams p)
            => Mathf.Clamp01(Mathf.Clamp01(baseChance) * FatigueFactor(pendingCount, p));
    }
}
