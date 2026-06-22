using UnityEngine;

namespace Ginei
{
    /// <summary>社会的立場（人望/武名/民望/悪名）の合成の調整値（PER-PROPS B5/C9）。</summary>
    public readonly struct StandingParams
    {
        /// <summary>武名(fame)の寄与。</summary>
        public readonly float fameWeight;
        /// <summary>民望(popularRenown)の寄与。</summary>
        public readonly float popularWeight;
        /// <summary>人望(charisma)の寄与。</summary>
        public readonly float charismaWeight;
        /// <summary>悪名(infamy)の減点（0..1）。</summary>
        public readonly float infamyWeight;
        /// <summary>悪名→占領地反発の最大（0..1）。</summary>
        public readonly float unrestScale;

        public StandingParams(float fameWeight, float popularWeight, float charismaWeight, float infamyWeight, float unrestScale)
        {
            this.fameWeight = fameWeight;
            this.popularWeight = popularWeight;
            this.charismaWeight = charismaWeight;
            this.infamyWeight = infamyWeight;
            this.unrestScale = unrestScale;
        }

        /// <summary>既定＝武名0.4／民望0.3／人望0.3／悪名0.6／反発0.5。</summary>
        public static StandingParams Default => new StandingParams(0.4f, 0.3f, 0.3f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 人物の社会的立場（人望 charisma／武名 fame／民望 popularRenown／悪名 infamy）の純ロジック（PER-PROPS B5/C9）。
    /// 戦績由来の名声は既存 <see cref="ReputationRules"/>、武名→威圧/登用は <see cref="RenownRules"/> が窓口。
    /// ここはそれらと別軸の<b>民望・悪名・人望</b>を束ねた総合的な立場 0..1 と、悪名による占領地反発を返す（再実装しない）。
    /// 決定論・test-first。
    /// </summary>
    public static class StandingRules
    {
        /// <summary>総合的な立場 0..1＝(武名×w＋民望×w＋人望×w) を重みで正規化し、悪名×w を引いてクランプ。各入力0..100。</summary>
        public static float NetStanding(int fame, int popularRenown, int charisma, int infamy, StandingParams p)
        {
            float pos = Mathf.Clamp01(fame / 100f) * p.fameWeight
                      + Mathf.Clamp01(popularRenown / 100f) * p.popularWeight
                      + Mathf.Clamp01(charisma / 100f) * p.charismaWeight;
            float wsum = Mathf.Max(0.0001f, p.fameWeight + p.popularWeight + p.charismaWeight);
            float neg = Mathf.Clamp01(infamy / 100f) * Mathf.Max(0f, p.infamyWeight);
            return Mathf.Clamp01(pos / wsum - neg);
        }

        public static float NetStanding(int fame, int popularRenown, int charisma, int infamy)
            => NetStanding(fame, popularRenown, charisma, infamy, StandingParams.Default);

        /// <summary>悪名による占領地の反発 0..1（infamy 比例）。LoyaltyRules/民心へ橋渡し。</summary>
        public static float UnrestFromInfamy(int infamy, StandingParams p)
            => Mathf.Clamp01(infamy / 100f) * Mathf.Clamp01(p.unrestScale);

        public static float UnrestFromInfamy(int infamy) => UnrestFromInfamy(infamy, StandingParams.Default);

        /// <summary>登用での求心力 0..1（人望と民望の平均）。RenownRules.RecruitmentPull(武名) を補完する別軸。</summary>
        public static float RecruitmentAppeal(int charisma, int popularRenown)
            => Mathf.Clamp01((Mathf.Clamp01(charisma / 100f) + Mathf.Clamp01(popularRenown / 100f)) * 0.5f);
    }
}
