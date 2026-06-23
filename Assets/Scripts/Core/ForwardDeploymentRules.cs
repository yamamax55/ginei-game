using UnityEngine;

namespace Ginei
{
    /// <summary>前方展開の調整係数。</summary>
    public readonly struct ForwardDeploymentParams
    {
        /// <summary>前方配置戦力が即応性へ寄与する重み。</summary>
        public readonly float readinessWeight;
        /// <summary>抑止プレゼンスが相手への可視に依存する度合い（見えない戦力は抑止しない）。</summary>
        public readonly float visibilityWeight;
        /// <summary>仕掛け線（トリップワイヤ）効果の最大値（0..1）。</summary>
        public readonly float maxTripwire;
        /// <summary>増援距離が孤立リスクを増幅する重み（遠いほど突出が孤立する）。</summary>
        public readonly float isolationDistanceWeight;
        /// <summary>維持距離が展開コストを増幅する重み（遠いほど補給維持が高くつく）。</summary>
        public readonly float sustainmentDistanceWeight;
        /// <summary>相手の反応が緊張へ寄与する重み（前方プレゼンスへの過敏反応で緊張が高まる）。</summary>
        public readonly float reactionWeight;
        /// <summary>前方への傾注が緊張を増幅する非線形度（冪指数・1以上）。</summary>
        public readonly float escalationExponent;
        /// <summary>孤立リスクが前方展開の価値を削る重み。</summary>
        public readonly float isolationPenaltyWeight;
        /// <summary>後方の手薄が前方展開の価値を削る重み。</summary>
        public readonly float rearPenaltyWeight;

        public ForwardDeploymentParams(float readinessWeight, float visibilityWeight, float maxTripwire,
            float isolationDistanceWeight, float sustainmentDistanceWeight, float reactionWeight,
            float escalationExponent, float isolationPenaltyWeight, float rearPenaltyWeight)
        {
            this.readinessWeight = Mathf.Max(0f, readinessWeight);
            this.visibilityWeight = Mathf.Clamp01(visibilityWeight);
            this.maxTripwire = Mathf.Clamp01(maxTripwire);
            this.isolationDistanceWeight = Mathf.Max(0f, isolationDistanceWeight);
            this.sustainmentDistanceWeight = Mathf.Max(0f, sustainmentDistanceWeight);
            this.reactionWeight = Mathf.Max(0f, reactionWeight);
            this.escalationExponent = Mathf.Max(1f, escalationExponent);
            this.isolationPenaltyWeight = Mathf.Max(0f, isolationPenaltyWeight);
            this.rearPenaltyWeight = Mathf.Max(0f, rearPenaltyWeight);
        }

        /// <summary>既定＝即応重み1・可視重み0.7・仕掛け線上限0.9・孤立距離重み1・維持距離重み0.5・反応重み0.8・緊張冪2・孤立罰0.6・後方罰0.5。</summary>
        public static ForwardDeploymentParams Default =>
            new ForwardDeploymentParams(1f, 0.7f, 0.9f, 1f, 0.5f, 0.8f, 2f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 前方展開の純ロジック（前線・国境への戦力の常時配置）。前方プレゼンスは即応性を高め、相手に見える
    /// ことで抑止になり、攻撃＝全面戦争の引き金となる「仕掛け線（トリップワイヤ）」を張る。だが突出した
    /// 前方部隊は本国の増援から遠く孤立リスクを負い、維持距離ぶんコストがかさみ、相手の反応次第で緊張が
    /// 高まり、前方へ傾注するほど後方が手薄になる。展開の価値＝抑止−孤立−後方手薄（-1..1）でその綱引きを出す。
    /// 過剰拡張（<see cref="OverextensionRules"/>＝国家規模の恒常的な過伸張）とは別系統＝こちらは前線への
    /// 部隊配置という作戦・戦域レベルの判断。倍率は実効値パターン（基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ForwardDeploymentRules
    {
        /// <summary>
        /// 前方即応性（0以上）＝前方配置戦力×警戒態勢×即応重み。前線に置いた戦力が高い警戒態勢にあるほど
        /// すぐ動ける。配置ゼロ・態勢ゼロなら即応性なし。
        /// </summary>
        public static float ForwardReadiness(float deployedForce, float alertPosture, ForwardDeploymentParams p)
        {
            float force = Mathf.Max(0f, deployedForce);
            float alert = Mathf.Clamp01(alertPosture);
            return force * alert * p.readinessWeight;
        }

        public static float ForwardReadiness(float deployedForce, float alertPosture)
            => ForwardReadiness(deployedForce, alertPosture, ForwardDeploymentParams.Default);

        /// <summary>
        /// 抑止プレゼンス（0以上）＝即応性×相手への可視。隠れた即応性は抑止にならない＝可視がゼロなら
        /// 可視重みぶん抑止が消える（可視1で全効果、可視0で readiness×(1−visibilityWeight) の最低限）。
        /// </summary>
        public static float DeterrentPresence(float forwardReadiness, float visibilityToAdversary, ForwardDeploymentParams p)
        {
            float r = Mathf.Max(0f, forwardReadiness);
            float vis = Mathf.Clamp01(visibilityToAdversary);
            float visFactor = Mathf.Lerp(1f - p.visibilityWeight, 1f, vis);
            return r * visFactor;
        }

        public static float DeterrentPresence(float forwardReadiness, float visibilityToAdversary)
            => DeterrentPresence(forwardReadiness, visibilityToAdversary, ForwardDeploymentParams.Default);

        /// <summary>
        /// 仕掛け線効果（0..maxTripwire）＝前方戦力×関与の意思。少数でも前線に置けば「ここを攻撃＝全面戦争」
        /// の引き金になる＝関与の意思が強いほど効く。戦力ゼロor意思ゼロなら効果なし。飽和して上限で頭打ち。
        /// </summary>
        public static float TripwireEffect(float forwardForce, float commitmentSignal, ForwardDeploymentParams p)
        {
            float force = Mathf.Clamp01(forwardForce);
            float signal = Mathf.Clamp01(commitmentSignal);
            return Mathf.Min(p.maxTripwire, force * signal);
        }

        public static float TripwireEffect(float forwardForce, float commitmentSignal)
            => TripwireEffect(forwardForce, commitmentSignal, ForwardDeploymentParams.Default);

        /// <summary>
        /// 孤立リスク（0..1）＝前方戦力の突出×増援距離。前線へ突出するほど、そして本国の増援が遠いほど、
        /// 各個撃破され孤立する危険が増す。突出ゼロ・増援距離ゼロ（隣接）なら孤立リスクなし。
        /// </summary>
        public static float IsolationRisk(float forwardForce, float reinforcementDistance, ForwardDeploymentParams p)
        {
            float force = Mathf.Clamp01(forwardForce);
            float dist = Mathf.Clamp01(reinforcementDistance);
            return Mathf.Clamp01(force * dist * p.isolationDistanceWeight);
        }

        public static float IsolationRisk(float forwardForce, float reinforcementDistance)
            => IsolationRisk(forwardForce, reinforcementDistance, ForwardDeploymentParams.Default);

        /// <summary>
        /// 展開コスト（0以上）＝前方戦力×（1＋維持距離×維持距離重み）。前線に多く置くほど、そして補給線が
        /// 遠いほど常時維持の負担が増す。本国近傍（維持距離0）でも戦力ぶんの基礎コストはかかる。
        /// </summary>
        public static float DeploymentCost(float forwardForce, float sustainmentDistance, ForwardDeploymentParams p)
        {
            float force = Mathf.Max(0f, forwardForce);
            float dist = Mathf.Clamp01(sustainmentDistance);
            return force * (1f + dist * p.sustainmentDistanceWeight);
        }

        public static float DeploymentCost(float forwardForce, float sustainmentDistance)
            => DeploymentCost(forwardForce, sustainmentDistance, ForwardDeploymentParams.Default);

        /// <summary>
        /// 緊張の高まり（0..1）＝抑止プレゼンス×相手の反応。前方プレゼンスへ相手が過敏に反応するほど緊張が
        /// 非線形に膨らむ（安全のジレンマ）。プレゼンスゼロor反応ゼロなら緊張なし。
        /// </summary>
        public static float EscalationTension(float deterrentPresence, float adversaryReaction, ForwardDeploymentParams p)
        {
            float presence = Mathf.Clamp01(deterrentPresence);
            float reaction = Mathf.Clamp01(adversaryReaction);
            float raw = presence * reaction * p.reactionWeight;
            return Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(raw), p.escalationExponent));
        }

        public static float EscalationTension(float deterrentPresence, float adversaryReaction)
            => EscalationTension(deterrentPresence, adversaryReaction, ForwardDeploymentParams.Default);

        /// <summary>
        /// 後方の手薄（0..1）＝前方への傾注×(1−後方予備)。戦力を前線へ傾けるほど、そして後方予備が薄いほど
        /// 本国・後方が手薄になる。前方傾注ゼロ、または後方予備が満ちていれば（1.0）手薄にならない。
        /// </summary>
        public static float RearVulnerability(float forwardCommitment, float homelandReserves, ForwardDeploymentParams p)
        {
            float commit = Mathf.Clamp01(forwardCommitment);
            float reserves = Mathf.Clamp01(homelandReserves);
            return Mathf.Clamp01(commit * (1f - reserves));
        }

        public static float RearVulnerability(float forwardCommitment, float homelandReserves)
            => RearVulnerability(forwardCommitment, homelandReserves, ForwardDeploymentParams.Default);

        /// <summary>
        /// 前方展開の価値（-1..1）＝抑止プレゼンス−孤立リスク×孤立罰−後方手薄×後方罰。抑止という便益と
        /// 孤立・後方手薄という代償の綱引き。正なら前方展開が割に合い、負なら割に合わない（撤収を促す）。
        /// </summary>
        public static float ForwardDeploymentValue(float deterrentPresence, float isolationRisk,
            float rearVulnerability, ForwardDeploymentParams p)
        {
            float presence = Mathf.Clamp01(deterrentPresence);
            float isolation = Mathf.Clamp01(isolationRisk);
            float rear = Mathf.Clamp01(rearVulnerability);
            float value = presence - isolation * p.isolationPenaltyWeight - rear * p.rearPenaltyWeight;
            return Mathf.Clamp(value, -1f, 1f);
        }

        public static float ForwardDeploymentValue(float deterrentPresence, float isolationRisk, float rearVulnerability)
            => ForwardDeploymentValue(deterrentPresence, isolationRisk, rearVulnerability, ForwardDeploymentParams.Default);
    }
}
