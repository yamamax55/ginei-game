using UnityEngine;

namespace Ginei
{
    /// <summary>労働運動（ストライキ）の調整係数。</summary>
    public readonly struct StrikeParams
    {
        /// <summary>人手が余っているとき（不況）の交渉力係数（0..1・替えが効く労働者は弱い）。</summary>
        public readonly float scarcitySlack;
        /// <summary>交渉力ゼロのときの決行圧力係数（0..1・勝ち目が無くても窮すれば立つ分）。</summary>
        public readonly float weakHandFactor;
        /// <summary>決行閾値（0..1・決行圧力がこれ以上でストに突入）。</summary>
        public readonly float strikeTrigger;
        /// <summary>生産損害の速度（per duration・スト参加率1のとき）。</summary>
        public readonly float lossRate;
        /// <summary>スト基金の枯渇速度（per duration）。</summary>
        public readonly float fundDrainRate;
        /// <summary>弾圧がストを物理的に止める効率（0..1）。</summary>
        public readonly float repressionEffect;
        /// <summary>同情される労働者への弾圧が政権支持を削る係数。</summary>
        public readonly float sympathyBackfire;

        public StrikeParams(float scarcitySlack, float weakHandFactor, float strikeTrigger,
                            float lossRate, float fundDrainRate,
                            float repressionEffect, float sympathyBackfire)
        {
            this.scarcitySlack = Mathf.Clamp01(scarcitySlack);
            this.weakHandFactor = Mathf.Clamp01(weakHandFactor);
            this.strikeTrigger = Mathf.Clamp01(strikeTrigger);
            this.lossRate = Mathf.Max(0f, lossRate);
            this.fundDrainRate = Mathf.Max(0f, fundDrainRate);
            this.repressionEffect = Mathf.Clamp01(repressionEffect);
            this.sympathyBackfire = Mathf.Max(0f, sympathyBackfire);
        }

        /// <summary>既定＝不況時交渉力0.3・弱腰決行0.25・決行閾値0.5・損害速度0.2・基金枯渇0.25・弾圧効率0.6・同情逆風0.5。</summary>
        public static StrikeParams Default
            => new StrikeParams(0.3f, 0.25f, 0.5f, 0.2f, 0.25f, 0.6f, 0.5f);
    }

    /// <summary>弾圧の帰結（鎮圧度と政治的代償）。</summary>
    public readonly struct StrikeRepression
    {
        /// <summary>ストの鎮圧度（0..1・参加率をこの割合だけ削る）。</summary>
        public readonly float suppression;
        /// <summary>政権の支持ペナルティ（0..1・同情される労働者を殴った政治的代償）。</summary>
        public readonly float supportPenalty;

        public StrikeRepression(float suppression, float supportPenalty)
        {
            this.suppression = Mathf.Clamp01(suppression);
            this.supportPenalty = Mathf.Clamp01(supportPenalty);
        }
    }

    /// <summary>
    /// 労働運動の純ロジック＝賃金・待遇闘争が生産を止める。政権は弾圧か妥協かを迫られる。
    /// 交渉力は組織率×景気（人手不足）で振れる：替えの効かない労働者ほどストは効く。
    /// 不満の源泉（生活水準）は <see cref="MarketRules"/>（SoL・需給価格）が出所だが、
    /// ここでは plain な grievance 引数で受けるだけ＝MarketRules は read-only の上流。
    /// 弾圧の帰結は <see cref="NonviolenceRules"/> の可視化弾圧と同型＝同情される労働者への
    /// 弾圧は政権の支持を削る（見られて高くつく）。そして核心＝**ストは我慢比べ**：
    /// 生産停止は長引くほど双方が痩せる消耗戦で、スト基金が先に飢えれば労働側が折れ、
    /// 企業体力が先に尽きれば経営側が折れる＝妥結水準は両者の限界の中間に落ちる。
    /// 乱数なしの決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class StrikeRules
    {
        /// <summary>
        /// 交渉力（0..1）＝組織率×景気係数。人手不足（好景気）ほどストは効き、
        /// 人手が余る不況では組織があっても替えが効く＝scarcitySlack まで目減りする。
        /// </summary>
        public static float BargainingPower(float unionization, float laborScarcity, StrikeParams p)
        {
            float boom = Mathf.Lerp(p.scarcitySlack, 1f, Mathf.Clamp01(laborScarcity));
            return Mathf.Clamp01(Mathf.Clamp01(unionization) * boom);
        }

        public static float BargainingPower(float unionization, float laborScarcity)
            => BargainingPower(unionization, laborScarcity, StrikeParams.Default);

        /// <summary>
        /// 決行圧力（0..1）＝不満×手札係数。勝ち目（交渉力）が無ければ不満があっても
        /// 決行しにくい（weakHandFactor まで減衰）。strikeTrigger と比べて決行を判定する。
        /// </summary>
        public static float StrikeThreshold(float grievance, float bargainingPower, StrikeParams p)
        {
            float hand = Mathf.Lerp(p.weakHandFactor, 1f, Mathf.Clamp01(bargainingPower));
            return Mathf.Clamp01(Mathf.Clamp01(grievance) * hand);
        }

        public static float StrikeThreshold(float grievance, float bargainingPower)
            => StrikeThreshold(grievance, bargainingPower, StrikeParams.Default);

        /// <summary>決行判定。決行圧力が閾値 strikeTrigger 以上ならスト突入＝true（決定論）。</summary>
        public static bool StrikeBreaksOut(float grievance, float bargainingPower, StrikeParams p)
        {
            return StrikeThreshold(grievance, bargainingPower, p) >= p.strikeTrigger;
        }

        public static bool StrikeBreaksOut(float grievance, float bargainingPower)
            => StrikeBreaksOut(grievance, bargainingPower, StrikeParams.Default);

        /// <summary>
        /// 生産停止の損害（0..1）＝スト参加率×損害速度×期間。長引くほど双方が痩せる消耗戦＝
        /// 賃金も利潤も同じ生産から出るため、止まった生産の損は労使どちらも逃れられない。
        /// </summary>
        public static float ProductionLoss(float strikingShare, float duration, StrikeParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(strikingShare) * p.lossRate * Mathf.Max(0f, duration));
        }

        public static float ProductionLoss(float strikingShare, float duration)
            => ProductionLoss(strikingShare, duration, StrikeParams.Default);

        /// <summary>
        /// スト基金の枯渇度（0..1）＝期間×枯渇速度−基金規模。基金は持久力＝時間を買う。
        /// 枯渇1で労働側は飢える＝我慢比べの限界（<see cref="SettlementWage(float,float,float,StrikeParams)"/> で折れる側になる）。
        /// </summary>
        public static float StrikeFundDepletion(float duration, float fundSize, StrikeParams p)
        {
            return Mathf.Clamp01(Mathf.Max(0f, duration) * p.fundDrainRate - Mathf.Clamp01(fundSize));
        }

        public static float StrikeFundDepletion(float duration, float fundSize)
            => StrikeFundDepletion(duration, fundSize, StrikeParams.Default);

        /// <summary>
        /// 弾圧の帰結。鎮圧度＝力×弾圧効率（物理的にはストを止められる）。
        /// 支持ペナルティ＝力×世論の同情×逆風係数＝<see cref="NonviolenceRules"/> の可視化弾圧と同型：
        /// 同情される労働者を殴るほど政権の支持が削れる＝弾圧は効くが政治的に高くつく。
        /// </summary>
        public static StrikeRepression RepressionOutcome(float repressionForce, float publicSympathy, StrikeParams p)
        {
            float force = Mathf.Clamp01(repressionForce);
            float suppression = force * p.repressionEffect;
            float penalty = force * Mathf.Clamp01(publicSympathy) * p.sympathyBackfire;
            return new StrikeRepression(suppression, penalty);
        }

        public static StrikeRepression RepressionOutcome(float repressionForce, float publicSympathy)
            => RepressionOutcome(repressionForce, publicSympathy, StrikeParams.Default);

        /// <summary>
        /// 妥結水準（0..1）＝労働側の手札と経営側の弱りの中間。基金の枯渇 fundDepletion は
        /// 交渉力を目減りさせる＝先に飢えた方が折れる（枯渇1で労働側の手札は消える）。
        /// 企業体力 employerReserves が尽きているほど経営側が譲る＝我慢比べの均衡点。
        /// </summary>
        public static float SettlementWage(float bargainingPower, float employerReserves, float fundDepletion, StrikeParams p)
        {
            float laborHand = Mathf.Clamp01(bargainingPower) * (1f - Mathf.Clamp01(fundDepletion));
            float employerWeakness = 1f - Mathf.Clamp01(employerReserves);
            return Mathf.Clamp01((laborHand + employerWeakness) * 0.5f);
        }

        /// <summary>妥結水準（基金枯渇なし）＝交渉力と企業体力の中間。</summary>
        public static float SettlementWage(float bargainingPower, float employerReserves, StrikeParams p)
            => SettlementWage(bargainingPower, employerReserves, 0f, p);

        public static float SettlementWage(float bargainingPower, float employerReserves)
            => SettlementWage(bargainingPower, employerReserves, 0f, StrikeParams.Default);
    }
}
