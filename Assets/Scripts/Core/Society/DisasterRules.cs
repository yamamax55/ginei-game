using UnityEngine;

namespace Ginei
{
    /// <summary>災害の種類。被害の出方が違う（値駆動＝倍率の組で表す）。</summary>
    public enum DisasterKind
    {
        疫病,   // 人口を直接削る・時間で広がる
        飢饉,   // 人口と安定度を削る・備蓄で緩和
        天災    // 一撃型＝インフラ（産出）と安定度を削る
    }

    /// <summary>災害の調整係数。</summary>
    public readonly struct DisasterParams
    {
        /// <summary>深刻度最大・救援ゼロのときの人口喪失の最大割合。</summary>
        public readonly float maxPopulationLoss;
        /// <summary>深刻度最大・救援ゼロのときの安定度低下の最大量。</summary>
        public readonly float maxStabilityHit;
        /// <summary>救援が被害を削る最大割合（救援努力最大で被害がこれだけ軽くなる）。</summary>
        public readonly float reliefMitigation;
        /// <summary>迅速な救援が正統性に返す最大ボーナス。</summary>
        public readonly float reliefLegitimacyBonus;
        /// <summary>放置が正統性を削る最大ペナルティ。</summary>
        public readonly float neglectLegitimacyHit;

        public DisasterParams(float maxPopulationLoss, float maxStabilityHit, float reliefMitigation,
                              float reliefLegitimacyBonus, float neglectLegitimacyHit)
        {
            this.maxPopulationLoss = Mathf.Clamp01(maxPopulationLoss);
            this.maxStabilityHit = Mathf.Max(0f, maxStabilityHit);
            this.reliefMitigation = Mathf.Clamp01(reliefMitigation);
            this.reliefLegitimacyBonus = Mathf.Max(0f, reliefLegitimacyBonus);
            this.neglectLegitimacyHit = Mathf.Max(0f, neglectLegitimacyHit);
        }

        /// <summary>既定＝人口喪失10%・安定度30・救援緩和80%・救援正統性+0.1・放置−0.2。</summary>
        public static DisasterParams Default => new DisasterParams(0.1f, 30f, 0.8f, 0.1f, 0.2f);
    }

    /// <summary>
    /// 災害・疫病・飢饉の純ロジック。災害は人口と安定度を削るが、救援努力（reliefEffort 0..1）が
    /// 被害を大きく緩和し、対応の巧拙が正統性を試す＝迅速な救援は支持を買い、放置は天命を疑わせる。
    /// 疫病は時間で広がる（指数増殖→飽和）。平時の人口動態は <see cref="DemographicsRules"/>、
    /// 安定度の収束は <see cref="GovernanceRules"/> が担い、ここは災害ショックの量だけを出す。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DisasterRules
    {
        /// <summary>救援による被害倍率（1−緩和×救援努力）。被害量に掛ける。</summary>
        public static float ReliefFactor(float reliefEffort, DisasterParams p)
        {
            return 1f - p.reliefMitigation * Mathf.Clamp01(reliefEffort);
        }

        public static float ReliefFactor(float reliefEffort) => ReliefFactor(reliefEffort, DisasterParams.Default);

        /// <summary>人口喪失＝人口×最大喪失率×深刻度(0..1)×救援倍率。</summary>
        public static float PopulationLoss(float population, float severity, float reliefEffort, DisasterParams p)
        {
            return Mathf.Max(0f, population) * p.maxPopulationLoss * Mathf.Clamp01(severity) * ReliefFactor(reliefEffort, p);
        }

        public static float PopulationLoss(float population, float severity, float reliefEffort)
            => PopulationLoss(population, severity, reliefEffort, DisasterParams.Default);

        /// <summary>安定度低下量＝最大低下×深刻度×救援倍率（`Province.stability` 0..100 スケール）。</summary>
        public static float StabilityHit(float severity, float reliefEffort, DisasterParams p)
        {
            return p.maxStabilityHit * Mathf.Clamp01(severity) * ReliefFactor(reliefEffort, p);
        }

        public static float StabilityHit(float severity, float reliefEffort)
            => StabilityHit(severity, reliefEffort, DisasterParams.Default);

        /// <summary>
        /// 対応の正統性増減＝救援努力0.5を中立に、上回れば+（×深刻度×ボーナス幅）、下回れば−（×深刻度×ペナルティ幅）。
        /// 深刻な災害ほど対応が問われる（小さな災害の放置は咎められにくい）。
        /// </summary>
        public static float LegitimacyDelta(float severity, float reliefEffort, DisasterParams p)
        {
            float sev = Mathf.Clamp01(severity);
            float effort = Mathf.Clamp01(reliefEffort);
            const float Neutral = 0.5f;
            if (effort >= Neutral)
                return (effort - Neutral) * 2f * p.reliefLegitimacyBonus * sev;
            return -(Neutral - effort) * 2f * p.neglectLegitimacyHit * sev;
        }

        public static float LegitimacyDelta(float severity, float reliefEffort)
            => LegitimacyDelta(severity, reliefEffort, DisasterParams.Default);

        /// <summary>
        /// 疫病の1tick後の深刻度（0..1）。ロジスティック増殖＝広がる余地（1−深刻度）がある間は
        /// spreadRate×dt で増え、封じ込め containment(0..1) がそのぶん増殖を削る（封じ込め1で増殖停止）。
        /// </summary>
        public static float EpidemicTick(float severity, float spreadRate, float containment, float dt)
        {
            float s = Mathf.Clamp01(severity);
            float growth = Mathf.Max(0f, spreadRate) * (1f - Mathf.Clamp01(containment)) * s * (1f - s) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(s + growth);
        }
    }
}
