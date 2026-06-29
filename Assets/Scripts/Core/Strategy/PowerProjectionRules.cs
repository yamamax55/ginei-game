using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦力投射（パワープロジェクション）の調整値。距離減衰の効き・補給線負荷の上限・各効果の重みを束ねる。
    /// 全フィールドは ctor で Clamp 済み（負値・暴走を避ける）。
    /// </summary>
    public readonly struct PowerProjectionParams
    {
        /// <summary>距離による戦力減衰の係数（大きいほど遠方で急減衰）。</summary>
        public readonly float distanceFalloff;
        /// <summary>補給線負荷の上限（1.0＝完全途絶で到達戦力ゼロ）。</summary>
        public readonly float maxStrain;
        /// <summary>投射持続時間の基準スケール（持続戦力×本国支援に掛ける）。</summary>
        public readonly float durationScale;
        /// <summary>抑止効果の重み（持続戦力×相手の認識に掛ける）。</summary>
        public readonly float deterrentWeight;
        /// <summary>過剰投射リスクの重み（補給負荷×本国の手薄に掛ける）。</summary>
        public readonly float overextensionWeight;

        public PowerProjectionParams(float distanceFalloff, float maxStrain, float durationScale, float deterrentWeight, float overextensionWeight)
        {
            this.distanceFalloff = Mathf.Max(0f, distanceFalloff);
            this.maxStrain = Mathf.Clamp01(maxStrain);
            this.durationScale = Mathf.Max(0f, durationScale);
            this.deterrentWeight = Mathf.Clamp01(deterrentWeight);
            this.overextensionWeight = Mathf.Clamp01(overextensionWeight);
        }

        /// <summary>既定：距離係数0.5・補給負荷上限1.0・持続スケール1.0・抑止重み1.0・過剰投射重み1.0。</summary>
        public static PowerProjectionParams Default =>
            new PowerProjectionParams(DefaultDistanceFalloff, DefaultMaxStrain, DefaultDurationScale, DefaultDeterrentWeight, DefaultOverextensionWeight);

        public const float DefaultDistanceFalloff = 0.5f;
        public const float DefaultMaxStrain = 1.0f;
        public const float DefaultDurationScale = 1.0f;
        public const float DefaultDeterrentWeight = 1.0f;
        public const float DefaultOverextensionWeight = 1.0f;
    }

    /// <summary>
    /// 戦力投射（遠方への軍事力の到達と維持）の純ロジック。
    /// 投射可能な戦力の規模 → 距離による減衰 → 補給線の延伸 → 現地での戦力維持 → 持続時間・抑止・過剰投射リスク
    /// → 総合的な投射の価値（-1..1）までを段階的に算出する。決定論・実効値パターン・入力 Clamp。
    /// 個体粒度へ降りず勢力/版図の集約スカラで扱う（終盤ラグ回避）。test-first。
    /// </summary>
    public static class PowerProjectionRules
    {
        /// <summary>投射可能戦力＝総戦力×遠征即応（0..1）。</summary>
        public static float ProjectableForce(float totalForce, float expeditionaryReadiness)
            => ProjectableForce(totalForce, expeditionaryReadiness, PowerProjectionParams.Default);

        /// <summary>投射可能戦力＝総戦力×遠征即応。即応が低いほど派遣できる戦力が減る。</summary>
        public static float ProjectableForce(float totalForce, float expeditionaryReadiness, PowerProjectionParams p)
        {
            float force = Mathf.Max(0f, totalForce);
            float readiness = Mathf.Clamp01(expeditionaryReadiness);
            return force * readiness;
        }

        /// <summary>距離減衰後の到達戦力＝投射戦力/(1+距離×距離係数)。</summary>
        public static float DistanceAttenuation(float projectableForce, float projectionDistance)
            => DistanceAttenuation(projectableForce, projectionDistance, PowerProjectionParams.Default);

        /// <summary>距離減衰後の到達戦力。遠いほど目的地に届く戦力が薄まる（双曲減衰）。</summary>
        public static float DistanceAttenuation(float projectableForce, float projectionDistance, PowerProjectionParams p)
        {
            float force = Mathf.Max(0f, projectableForce);
            float distance = Mathf.Max(0f, projectionDistance);
            float denom = 1f + distance * p.distanceFalloff;
            return force / denom;
        }

        /// <summary>補給線の延伸負荷（0..maxStrain）＝距離/兵站能力。兵站が細いと遠征で逼迫する。</summary>
        public static float SupplyLineStrain(float projectionDistance, float logisticsCapacity)
            => SupplyLineStrain(projectionDistance, logisticsCapacity, PowerProjectionParams.Default);

        /// <summary>補給線の延伸負荷。兵站能力ゼロは最大負荷とみなす（補給線が成立しない）。</summary>
        public static float SupplyLineStrain(float projectionDistance, float logisticsCapacity, PowerProjectionParams p)
        {
            float distance = Mathf.Max(0f, projectionDistance);
            float capacity = Mathf.Max(0f, logisticsCapacity);
            if (capacity <= 0f) return p.maxStrain; // 兵站皆無＝補給線崩壊
            float strain = distance / capacity;
            return Mathf.Clamp(strain, 0f, p.maxStrain);
        }

        /// <summary>現地での持続的戦力＝到達戦力×(1-補給負荷)。補給が逼迫するほど維持できる戦力が削られる。</summary>
        public static float SustainedPresence(float distanceAttenuation, float supplyLineStrain)
            => SustainedPresence(distanceAttenuation, supplyLineStrain, PowerProjectionParams.Default);

        /// <summary>現地での持続的戦力。負荷1.0で維持戦力ゼロ（補給途絶で前線が干上がる）。</summary>
        public static float SustainedPresence(float distanceAttenuation, float supplyLineStrain, PowerProjectionParams p)
        {
            float arrived = Mathf.Max(0f, distanceAttenuation);
            float strain = Mathf.Clamp01(supplyLineStrain);
            return arrived * (1f - strain);
        }

        /// <summary>投射の持続時間＝持続戦力×本国支援×持続スケール。本国が支えるほど長く居座れる。</summary>
        public static float ProjectionDuration(float sustainedPresence, float homelandSupport)
            => ProjectionDuration(sustainedPresence, homelandSupport, PowerProjectionParams.Default);

        /// <summary>投射の持続時間。本国支援が尽きると持続戦力があっても短命に終わる。</summary>
        public static float ProjectionDuration(float sustainedPresence, float homelandSupport, PowerProjectionParams p)
        {
            float presence = Mathf.Max(0f, sustainedPresence);
            float support = Mathf.Clamp01(homelandSupport);
            return presence * support * p.durationScale;
        }

        /// <summary>抑止効果＝持続戦力×相手の認識×抑止重み。相手が脅威を認識して初めて抑止が効く。</summary>
        public static float DeterrentEffect(float sustainedPresence, float adversaryPerception)
            => DeterrentEffect(sustainedPresence, adversaryPerception, PowerProjectionParams.Default);

        /// <summary>抑止効果。実戦力がいくら高くても相手の認識が薄ければ抑止にならない。</summary>
        public static float DeterrentEffect(float sustainedPresence, float adversaryPerception, PowerProjectionParams p)
        {
            float presence = Mathf.Max(0f, sustainedPresence);
            float perception = Mathf.Clamp01(adversaryPerception);
            return presence * perception * p.deterrentWeight;
        }

        /// <summary>過剰投射リスク（0..1）＝補給負荷×本国の手薄×過剰投射重み。延びきった補給線と空いた本国が危険。</summary>
        public static float OverextensionRisk(float supplyLineStrain, float homelandVulnerability)
            => OverextensionRisk(supplyLineStrain, homelandVulnerability, PowerProjectionParams.Default);

        /// <summary>過剰投射リスク。補給逼迫と本国の手薄が重なるほど大きい（0..1）。</summary>
        public static float OverextensionRisk(float supplyLineStrain, float homelandVulnerability, PowerProjectionParams p)
        {
            float strain = Mathf.Clamp01(supplyLineStrain);
            float vulnerability = Mathf.Clamp01(homelandVulnerability);
            return Mathf.Clamp01(strain * vulnerability * p.overextensionWeight);
        }

        /// <summary>戦力投射の総合価値（-1..1）＝持続戦力×抑止−過剰投射リスク。</summary>
        public static float ProjectionValue(float sustainedPresence, float deterrentEffect, float overextensionRisk)
            => ProjectionValue(sustainedPresence, deterrentEffect, overextensionRisk, PowerProjectionParams.Default);

        /// <summary>
        /// 戦力投射の総合価値（-1..1）。持続戦力(0..1相当に丸めた効き)×抑止の便益から過剰投射リスクを引く。
        /// 便益が薄く危険が大きいと負値＝投射すべきでない（撤収/縮小の合図）。
        /// </summary>
        public static float ProjectionValue(float sustainedPresence, float deterrentEffect, float overextensionRisk, PowerProjectionParams p)
        {
            float presence = Mathf.Clamp01(sustainedPresence);
            float deterrent = Mathf.Clamp01(deterrentEffect);
            float risk = Mathf.Clamp01(overextensionRisk);
            float benefit = presence * deterrent;
            return Mathf.Clamp(benefit - risk, -1f, 1f);
        }
    }
}
