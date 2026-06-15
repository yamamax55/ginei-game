using UnityEngine;

namespace Ginei
{
    /// <summary>エスカレーション（戦争の梯子）の調整係数。</summary>
    public readonly struct EscalationParams
    {
        /// <summary>死者が出た事件の重さ加算（段が跳ねる）。</summary>
        public readonly float casualtyJump;
        /// <summary>事件固有の昇り圧の割合（0..1）。世論もタカ派もゼロでも、事件の重さのこの割合は圧力になる＝誰も望まなくても梯子は昇る。</summary>
        public readonly float inherentMomentum;
        /// <summary>圧力1.0が梯子を昇らせる速度（per dt）。</summary>
        public readonly float riseRate;
        /// <summary>譲歩1.0が梯子を降ろす速度（per dt）。</summary>
        public readonly float concessionRate;
        /// <summary>引き返し不能点の段（これ以上は宣戦が合理化される）。</summary>
        public readonly float noReturnRung;
        /// <summary>仲介者がいる時の降りコスト倍率（0..1・小さいほど安く降りられる＝面子の出口）。</summary>
        public readonly float mediatorRelief;

        public EscalationParams(float casualtyJump, float inherentMomentum, float riseRate, float concessionRate, float noReturnRung, float mediatorRelief)
        {
            this.casualtyJump = Mathf.Clamp01(casualtyJump);
            this.inherentMomentum = Mathf.Clamp01(inherentMomentum);
            this.riseRate = Mathf.Max(0f, riseRate);
            this.concessionRate = Mathf.Max(0f, concessionRate);
            this.noReturnRung = Mathf.Clamp01(noReturnRung);
            this.mediatorRelief = Mathf.Clamp01(mediatorRelief);
        }

        /// <summary>既定＝死者跳ね0.4・固有圧0.5・昇り速度0.5・降り速度0.4・不能点0.8・仲介割引0.5。</summary>
        public static EscalationParams Default => new EscalationParams(0.4f, 0.5f, 0.5f, 0.4f, 0.8f, 0.5f);
    }

    /// <summary>
    /// エスカレーション管理の純ロジック＝戦争の梯子の力学。偶発的な国境事件が段（rung 0..1）を昇り、
    /// 世論の怒り×タカ派が圧力を増幅し、高い段ほど・威信を重んじる国ほど降りられない＝
    /// 「誰も望まない戦争に梯子だけで至る」を式に出す。仲介者がいれば面子を保って安く降りられる。
    /// ここは梯子の力学のみを扱い、引き返し不能点を超えた後の実際の開戦＝状態遷移は
    /// <see cref="DiplomacyRules"/>.DeclareWar へ委譲する（<see cref="DiplomacyState"/> は編集しない）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EscalationRules
    {
        /// <summary>威信ゼロの国でも負う降りコストの割合（面子はゼロにならない）。</summary>
        private const float BasePrestigeCost = 0.5f;

        /// <summary>
        /// 事件の重さ（0..1）。規模 scale が基準で、死者が出ると casualtyJump ぶん段が跳ねる
        /// （血は事件を別物に変える）。
        /// </summary>
        public static float IncidentSeverity(float scale, bool casualties, EscalationParams p)
        {
            float s = Mathf.Clamp01(scale);
            if (casualties) s += p.casualtyJump;
            return Mathf.Clamp01(s);
        }

        public static float IncidentSeverity(float scale, bool casualties)
            => IncidentSeverity(scale, casualties, EscalationParams.Default);

        /// <summary>
        /// 梯子を昇る圧力（0..1）＝事件の重さ×（固有圧＋世論の怒り×タカ派度の増幅）。
        /// 怒りもタカ派もゼロでも固有圧ぶんは昇る＝誰も望まなくても梯子は昇る。
        /// 増幅は怒り×タカ派の積＝どちらか一方だけでは火がつかない。
        /// </summary>
        public static float EscalationPressure(float severity, float publicAnger, float hawkishness, EscalationParams p)
        {
            float amplify = Mathf.Clamp01(publicAnger) * Mathf.Clamp01(hawkishness);
            return Mathf.Clamp01(severity) * (p.inherentMomentum + (1f - p.inherentMomentum) * amplify);
        }

        public static float EscalationPressure(float severity, float publicAnger, float hawkishness)
            => EscalationPressure(severity, publicAnger, hawkishness, EscalationParams.Default);

        /// <summary>
        /// 梯子を降りる威信コスト（0..1）＝現在の段×威信係数。高い段からほど・威信を重んじる国ほど
        /// 降りられない。威信ゼロでも BasePrestigeCost ぶんは払う（面子はゼロにならない）。
        /// </summary>
        public static float DeescalationCost(float currentRung, float prestige, EscalationParams p)
        {
            float factor = Mathf.Lerp(BasePrestigeCost, 1f, Mathf.Clamp01(prestige));
            return Mathf.Clamp01(currentRung) * factor;
        }

        public static float DeescalationCost(float currentRung, float prestige)
            => DeescalationCost(currentRung, prestige, EscalationParams.Default);

        /// <summary>
        /// 面子の出口＝降りコストの実効値。仲介者がいれば mediatorRelief 倍に割り引かれる
        /// （第三者の顔を立てる形なら安く降りられる）。いなければ素の威信コストのまま。
        /// </summary>
        public static float FaceSavingExit(float currentRung, float prestige, bool mediatorPresence, EscalationParams p)
        {
            float cost = DeescalationCost(currentRung, prestige, p);
            return mediatorPresence ? cost * p.mediatorRelief : cost;
        }

        public static float FaceSavingExit(float currentRung, float prestige, bool mediatorPresence)
            => FaceSavingExit(currentRung, prestige, mediatorPresence, EscalationParams.Default);

        /// <summary>
        /// 梯子の1tick後の段（0..1）＝圧力で昇り（riseRate）・譲歩で降りる（concessionRate）。
        /// 圧力と譲歩の綱引きの差分を dt で積分する。
        /// </summary>
        public static float RungTick(float rung, float pressure, float concession, float dt, EscalationParams p)
        {
            float up = Mathf.Clamp01(pressure) * p.riseRate;
            float down = Mathf.Clamp01(concession) * p.concessionRate;
            return Mathf.Clamp01(Mathf.Clamp01(rung) + (up - down) * Mathf.Max(0f, dt));
        }

        public static float RungTick(float rung, float pressure, float concession, float dt)
            => RungTick(rung, pressure, concession, dt, EscalationParams.Default);

        /// <summary>
        /// 引き返し不能点＝段が noReturnRung 以上。これ以上は宣戦が合理化される＝
        /// 呼び出し側は <see cref="DiplomacyRules"/>.DeclareWar で状態遷移させる（ここでは遷移しない）。
        /// </summary>
        public static bool PointOfNoReturn(float rung, EscalationParams p)
        {
            return Mathf.Clamp01(rung) >= p.noReturnRung;
        }

        public static bool PointOfNoReturn(float rung) => PointOfNoReturn(rung, EscalationParams.Default);
    }
}
