using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星のインフラ普及率の純ロジック（#2021 公益事業の惑星層・唯一の窓口）。電気/ガス/水道（<see cref="UtilityRules"/>）が
    /// 住民にどれだけ行き渡っているか＝<b>普及率（0..1）</b>を扱う。普及率は<b>公益事業の供給余力</b>（能力に対する接続需要の余裕）と
    /// <b>安定度</b>から目標へ年々収束し、<b>生活水準#181・生産性#93・安定度#109</b>を底上げする。供給不足（停電/断水）は普及を頭打ちにし生活を蝕む。
    /// 個別配電網へは降りない（惑星×集約＝タイクン化回避）。基準値非破壊（効果は倍率を返す）。test-first。
    /// </summary>
    public static class InfrastructureRules
    {
        /// <summary>普及率を目標へ寄せる年あたりの速度。</summary>
        public const float DefaultPenetrationSpeed = 0.08f;

        /// <summary>接続需要＝人口×1人あたり需要×普及率（普及したぶんだけ需要が立つ＝接続世帯ぶん）。</summary>
        public static float ConnectedDemand(float population, float perCapitaDemand, float penetration)
            => Mathf.Max(0f, population) * Mathf.Max(0f, perCapitaDemand) * Mathf.Clamp01(penetration);

        /// <summary>供給余力＝供給能力/接続需要（>1 で余裕・&lt;1 で不足＝停電/断水）。需要0以下は十分とみなし大きい値。</summary>
        public static float CapacityAdequacy(float capacity, float connectedDemand)
            => connectedDemand <= 0f ? 2f : Mathf.Max(0f, capacity) / connectedDemand;

        /// <summary>
        /// 普及率の目標（0..1）＝供給余力×（治安の重み）。能力に余裕があり安定なほど多くの世帯が接続できる。
        /// 供給不足（余力&lt;1＝停電/断水）は目標を余力で頭打ちにする＝インフラが足りなければ普及は止まる。
        /// </summary>
        public static float PenetrationTarget(float capacityAdequacy, float stability01)
        {
            float adequacy = Mathf.Clamp(capacityAdequacy, 0f, 1f); // 1超は1に飽和（余裕は普及上限を1へ）
            float order = 0.5f + 0.5f * Mathf.Clamp01(stability01);  // 治安が普及を後押し（0.5〜1.0）
            return Mathf.Clamp01(adequacy * order);
        }

        /// <summary>普及率を目標へ年次で寄せる（滑らかに収束・急変なし）。</summary>
        public static float TickPenetration(float current, float target, float speed)
            => Mathf.Clamp01(Mathf.MoveTowards(Mathf.Clamp01(current), Mathf.Clamp01(target), Mathf.Max(0f, speed)));

        /// <summary>生活水準#181 への寄与倍率＝Lerp(min, 1, 普及率)（インフラ未整備は生活が頭打ち）。</summary>
        public static float LivingStandardFactor(float penetration, float minFactor)
            => Mathf.Lerp(Mathf.Clamp01(minFactor), 1f, Mathf.Clamp01(penetration));

        /// <summary>生産性#93 への寄与倍率＝base＋普及率×bonus（電気/水道が産業を支える）。</summary>
        public static float ProductivityFactor(float penetration, float baseFactor, float bonus)
            => Mathf.Max(0f, baseFactor) + Mathf.Clamp01(penetration) * Mathf.Max(0f, bonus);

        /// <summary>安定度#109 への加点（±）＝(普及率−0.5)×scale（高普及で+・低普及で−＝インフラ格差が不満）。</summary>
        public static float StabilityBonus(float penetration, float scale)
            => (Mathf.Clamp01(penetration) - 0.5f) * Mathf.Max(0f, scale);
    }
}
