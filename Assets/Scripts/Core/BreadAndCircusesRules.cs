using UnityEngine;

namespace Ginei
{
    /// <summary>パンとサーカス（娯楽と配給による慰撫）の調整係数。</summary>
    public readonly struct BreadAndCircusesParams
    {
        /// <summary>供給最大時に不満の表出を抑えられる率（0..1・根は治さない＝表出だけ）。</summary>
        public readonly float pacifyScale;
        /// <summary>ガス抜き後に残る表出不満がこれを超えるとサーカスでは隠せない（慰撫の限界閾値・0..1）。</summary>
        public readonly float exposureThreshold;
        /// <summary>供給が依存を形成する速度（per provisionLevel per dt）。</summary>
        public readonly float dependencyGrowthRate;
        /// <summary>供給が止まったとき依存（慣れ）が抜ける速度（per dt・形成より遅い）。</summary>
        public readonly float dependencyDecayRate;
        /// <summary>刺激の逓減＝依存最大で必要供給コストに上乗せされる倍率幅（1+これ倍）。</summary>
        public readonly float toleranceScale;
        /// <summary>供給停止の反動の元本係数（緩やかな縮小でも依存×これだけは返ってくる）。</summary>
        public readonly float withdrawalRageBase;
        /// <summary>供給停止の反動の利子係数（急停止 suddenness=1 で元本に上乗せされる分）。</summary>
        public readonly float withdrawalInterestScale;
        /// <summary>政治的無関心の醸成速度（per provisionLevel per duration）。</summary>
        public readonly float apathyRate;

        public BreadAndCircusesParams(float pacifyScale, float exposureThreshold,
                                      float dependencyGrowthRate, float dependencyDecayRate,
                                      float toleranceScale,
                                      float withdrawalRageBase, float withdrawalInterestScale,
                                      float apathyRate)
        {
            this.pacifyScale = Mathf.Clamp01(pacifyScale);
            this.exposureThreshold = Mathf.Clamp01(exposureThreshold);
            this.dependencyGrowthRate = Mathf.Max(0f, dependencyGrowthRate);
            this.dependencyDecayRate = Mathf.Max(0f, dependencyDecayRate);
            this.toleranceScale = Mathf.Max(0f, toleranceScale);
            this.withdrawalRageBase = Mathf.Max(0f, withdrawalRageBase);
            this.withdrawalInterestScale = Mathf.Max(0f, withdrawalInterestScale);
            this.apathyRate = Mathf.Max(0f, apathyRate);
        }

        /// <summary>既定＝ガス抜き0.6/露出閾値0.35/依存形成0.1/依存回復0.02/刺激逓減1/反動元本0.5/反動利子1/無関心0.01。</summary>
        public static BreadAndCircusesParams Default =>
            new BreadAndCircusesParams(0.6f, 0.35f, 0.1f, 0.02f, 1f, 0.5f, 1f, 0.01f);
    }

    /// <summary>
    /// パンとサーカスの純ロジック＝慰撫の代替財。娯楽と配給の供給は政治的不満をガス抜きするが、
    /// パンとサーカスは<b>鎮痛剤</b>＝痛みの原因（本物の不満）は治さず表出だけ抑え、与え続けるほど
    /// 「当然の権利」として依存が形成され（同じ娯楽では足りなくなる＝供給コストの上昇圧）、
    /// 切れた時に倍痛い（依存×急停止＝抑え込んだ不満の元本＋利子が一気に返る）。
    /// 飼い慣らされた市民は投票も反乱もしない＝政治的無関心は専制には好都合・共和制には毒。
    /// <see cref="HopeRules"/> が担う<b>希望の実体</b>（意味・信仰・末人）とは別物＝ここは意味を
    /// 与えない代替財（気晴らし）の供給とその副作用だけを出す。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BreadAndCircusesRules
    {
        /// <summary>
        /// ガス抜き効果（0..1）＝供給水準×ガス抜き率×本物の不満。
        /// 不満の<b>表出</b>をこの量だけ抑える（表出不満＝genuineGrievance−この戻り値）が、
        /// 不満の根（genuineGrievance 自体）は1ミリも治さない＝鎮痛剤。
        /// </summary>
        public static float PacificationEffect(float provisionLevel, float genuineGrievance, BreadAndCircusesParams p)
        {
            return p.pacifyScale * Mathf.Clamp01(provisionLevel) * Mathf.Clamp01(genuineGrievance);
        }

        public static float PacificationEffect(float provisionLevel, float genuineGrievance)
            => PacificationEffect(provisionLevel, genuineGrievance, BreadAndCircusesParams.Default);

        /// <summary>
        /// 慰撫の限界＝ガス抜きしても残る表出不満が露出閾値を超えるか。
        /// 本物の不満が大きすぎるとサーカスでは隠せない（true＝反乱圧として漏れ出す）。
        /// </summary>
        public static bool SubstitutionFailure(float provisionLevel, float genuineGrievance, BreadAndCircusesParams p)
        {
            float expressed = Mathf.Clamp01(genuineGrievance) - PacificationEffect(provisionLevel, genuineGrievance, p);
            return expressed > p.exposureThreshold;
        }

        public static bool SubstitutionFailure(float provisionLevel, float genuineGrievance)
            => SubstitutionFailure(provisionLevel, genuineGrievance, BreadAndCircusesParams.Default);

        /// <summary>
        /// 慰撫依存の1tick後の値（0..1）＝依存＋（形成速度×供給水準×伸び代(1−依存) − 回復速度×依存）×dt。
        /// 与え続けるほど施しが「当然の権利」になり、止めても慣れはゆっくりしか抜けない（形成は速く回復は遅い）。
        /// </summary>
        public static float DependencyTick(float dependency, float provisionLevel, float dt, BreadAndCircusesParams p)
        {
            float d = Mathf.Clamp01(dependency);
            float flow = Mathf.Clamp01(provisionLevel);
            float delta = (p.dependencyGrowthRate * flow * (1f - d) - p.dependencyDecayRate * d) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(d + delta);
        }

        public static float DependencyTick(float dependency, float provisionLevel, float dt)
            => DependencyTick(dependency, provisionLevel, dt, BreadAndCircusesParams.Default);

        /// <summary>
        /// 刺激の逓減＝同じガス抜き効果を得るのに必要な供給コスト倍率（1以上）＝1＋逓減係数×依存度。
        /// 依存が深まるほど同じ娯楽では足りなくなる＝慰撫は続けるほど高くつく（財政への上昇圧）。
        /// </summary>
        public static float ToleranceEscalation(float dependency, BreadAndCircusesParams p)
        {
            return 1f + p.toleranceScale * Mathf.Clamp01(dependency);
        }

        public static float ToleranceEscalation(float dependency)
            => ToleranceEscalation(dependency, BreadAndCircusesParams.Default);

        /// <summary>
        /// 供給停止の反動（0..1）＝依存度×（元本係数＋利子係数×急停止度0..1）。
        /// 抑え込んでいた不満の元本に「裏切られた」利子が付いて一気に返る＝切れた時に倍痛い。
        /// 緩やかな縮小（suddenness=0）でも元本は返り、急停止ほど利子が膨らむ。
        /// </summary>
        public static float WithdrawalRage(float dependency, float suddenness, BreadAndCircusesParams p)
        {
            float d = Mathf.Clamp01(dependency);
            float s = Mathf.Clamp01(suddenness);
            return Mathf.Clamp01(d * (p.withdrawalRageBase + p.withdrawalInterestScale * s));
        }

        public static float WithdrawalRage(float dependency, float suddenness)
            => WithdrawalRage(dependency, suddenness, BreadAndCircusesParams.Default);

        /// <summary>
        /// 政治的無関心の醸成（0..1）＝供給水準×継続期間×醸成速度。
        /// 飼い慣らされた市民は投票も反乱もしない＝専制には好都合・共和制には毒
        /// （参政・動員の係数として符号は消費側が決める）。
        /// </summary>
        public static float PoliticalApathyEffect(float provisionLevel, float duration, BreadAndCircusesParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(provisionLevel) * Mathf.Max(0f, duration) * p.apathyRate);
        }

        public static float PoliticalApathyEffect(float provisionLevel, float duration)
            => PoliticalApathyEffect(provisionLevel, duration, BreadAndCircusesParams.Default);
    }
}
