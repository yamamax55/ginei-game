using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 労働力供給の単一窓口（POPLAB-1・#2026・#110/#153 連携・純ロジック）。
    /// 生産年齢人口（<see cref="Population.working"/>）×労働参加率＝労働力人口。参加率に人口局面（#153）・男女比・保育の労働参加を織り込む。
    /// 既存 <see cref="OccupationRules.WorkingAge"/>／<see cref="LaborRules"/>（#1957 国集計）の下位＝惑星の供給を一本化。集約・後方互換。test-first。
    /// </summary>
    public static class LaborSupplyRules
    {
        public const float DefaultBaseParticipation = 0.65f; // 既定の基礎労働参加率

        /// <summary>労働力人口＝生産年齢人口×労働参加率（0..1にクランプ）。</summary>
        public static float LaborForce(float workingAgePopulation, float participationRate)
            => Mathf.Max(0f, workingAgePopulation) * Mathf.Clamp01(participationRate);

        /// <summary>男女比調整＝女性の労働参加が低いと供給が細る＝基礎率×(1−女性比×(1−女性参加比))。</summary>
        public static float GenderAdjustedRate(float baseRate, float femaleShare, float femaleParticipationRatio)
            => Mathf.Clamp01(Mathf.Max(0f, baseRate) * (1f - Mathf.Clamp01(femaleShare) * (1f - Mathf.Clamp01(femaleParticipationRatio))));

        /// <summary>人口局面調整＝人口ボーナス/オーナス（#153 OutputFactor 0.8..1.2）を参加率へ乗算。</summary>
        public static float PhaseAdjustedRate(float rate, float demographicOutputFactor)
            => Mathf.Clamp01(Mathf.Max(0f, rate) * Mathf.Max(0f, demographicOutputFactor));

        /// <summary>保育調整＝保育整備で働く親が増える（#nursery LaborParticipationFactor 1.0..1.15）。</summary>
        public static float NurseryAdjustedRate(float rate, float nurseryLaborFactor)
            => Mathf.Clamp01(Mathf.Max(0f, rate) * Mathf.Max(0f, nurseryLaborFactor));

        /// <summary>実効参加率＝男女比×人口局面×保育を合成（単一窓口）。</summary>
        public static float EffectiveParticipation(float baseRate, float femaleShare, float femaleParticipationRatio,
            float demographicOutputFactor, float nurseryLaborFactor)
        {
            float r = GenderAdjustedRate(baseRate, femaleShare, femaleParticipationRatio);
            r = PhaseAdjustedRate(r, demographicOutputFactor);
            r = NurseryAdjustedRate(r, nurseryLaborFactor);
            return r;
        }

        /// <summary>実効労働力人口＝生産年齢×実効参加率（一本化した供給）。</summary>
        public static float EffectiveLaborForce(float workingAgePopulation, float baseRate, float femaleShare,
            float femaleParticipationRatio, float demographicOutputFactor, float nurseryLaborFactor)
            => LaborForce(workingAgePopulation,
                EffectiveParticipation(baseRate, femaleShare, femaleParticipationRatio, demographicOutputFactor, nurseryLaborFactor));
    }
}
