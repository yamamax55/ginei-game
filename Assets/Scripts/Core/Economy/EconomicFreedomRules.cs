using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// EconomicFreedomRules の調整値（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// ctor で全値をクランプ（協力の床・依存倍率・統制逆説率・隷従閾値は 0..1）。
    /// </summary>
    public readonly struct EconomicFreedomParams
    {
        /// <summary>経済統制が最大でも残る最低限の自発的協力（生存的協力・0..1）。</summary>
        public readonly float CooperationFloor;
        /// <summary>経済統制1あたりの国家依存度（パンを握る者が自由を握る・0..1）。</summary>
        public readonly float DependencyFactor;
        /// <summary>過度な経済統制が協力を失わせる逆説の強さ（統制の逆説・1秒あたり・0..1）。</summary>
        public readonly float BackfireRate;
        /// <summary>これを下回る協力係数（かつ高統制）で隷従ドリフトと判定する閾値（0..1）。</summary>
        public readonly float ServitudeThreshold;

        public EconomicFreedomParams(
            float cooperationFloor, float dependencyFactor, float backfireRate, float servitudeThreshold)
        {
            CooperationFloor = Mathf.Clamp01(cooperationFloor);
            DependencyFactor = Mathf.Clamp01(dependencyFactor);
            BackfireRate = Mathf.Clamp01(backfireRate);
            ServitudeThreshold = Mathf.Clamp01(servitudeThreshold);
        }

        /// <summary>
        /// 既定（協力の床0.1／依存倍率0.8／統制逆説率0.2／隷従閾値0.3）。
        /// 依存倍率は大きい＝経済を完全に握れば人々はほぼ国家に依存する。統制逆説率は中程度＝過度な統制が時間をかけて協力を蝕む。
        /// </summary>
        public static EconomicFreedomParams Default => new EconomicFreedomParams(
            cooperationFloor: 0.1f, dependencyFactor: 0.8f, backfireRate: 0.2f, servitudeThreshold: 0.3f);
    }

    /// <summary>
    /// 経済的自由と政治的自由の連動の純ロジック（HAYK-5 #1553・ハイエク『隷属への道』参考・test-first）。
    /// 「経済的自由（私有財産・契約・市場）は政治的自由の前提＝経済を統制すれば政治的自由も失われる」をモデル化する：
    /// 経済統制度が上がると人々は国家に依存し、自発的協力が強制に変わり、結局は安定も損なう
    /// （二つの自由＝経済的自由と政治的自由は補完的で、片方だけでは保てない）。
    /// 乱数は持たない（決定論）。調整値は <see cref="EconomicFreedomParams"/> に集約。
    /// 分担：<see cref="ConsentRules"/>(被治者の協力と統治可能性＝こちらの協力係数の出力先)
    /// ・<see cref="SpontaneousOrderRules"/>(自生的秩序＝同EPIC HAYK・計画なき秩序の形成)
    /// ・<see cref="PropertyRightsRules"/>(私有財産の保護強度＝こちらの入力の一要素)
    /// ・<see cref="PlanningDriftRules"/>(隷属への道＝同EPIC HAYK・計画経済の漸進的滑り)とは別＝
    /// こちらは二つの自由（経済的自由と政治的自由）の連動そのものを扱う。
    /// </summary>
    public static class EconomicFreedomRules
    {
        /// <summary>
        /// 経済的自由の度合い（0..1）＝私有財産×市場の自由×(1−経済統制度)。
        /// 三要素の積＝いずれかが欠ければ経済的自由は崩れる（財産が守られても市場が統制されれば、
        /// あるいは経済全体が国家に統制されれば自由は無に近づく）。
        /// </summary>
        public static float EconomicFreedom(float propertyRights, float marketFreedom, float economicControl)
        {
            float property = Mathf.Clamp01(propertyRights);
            float market = Mathf.Clamp01(marketFreedom);
            float control = Mathf.Clamp01(economicControl);
            return property * market * (1f - control);
        }

        /// <summary>
        /// 政治的自由への連動（0..1）＝経済的自由が政治的自由を支える。
        /// 経済を握られると（経済的自由が低いと）人々は政治的にも従わざるを得ない＝二つの自由の連動。
        /// 経済的自由が政治的自由の前提＝単調写像（経済的自由1で政治的自由を最大に支える）。
        /// </summary>
        public static float PoliticalFreedomLink(float economicFreedom)
            => Mathf.Clamp01(economicFreedom);

        /// <summary>
        /// 協力係数（0..1）＝経済的自由が高いほど自発的協力、統制（強制）が強いほど協力が強制に変わる。
        /// 自発的協力＝経済的自由×(1−強制)＝経済的自由があっても強制が強ければ協力は強制に置き換わる。
        /// 床 <see cref="EconomicFreedomParams.CooperationFloor"/> は残る（<see cref="ConsentRules"/> への入力）。
        /// </summary>
        public static float CooperationCoefficient(float economicFreedom, float coercion, EconomicFreedomParams p)
        {
            float freedom = Mathf.Clamp01(economicFreedom);
            float force = Mathf.Clamp01(coercion);
            float voluntary = freedom * (1f - force);
            return Mathf.Clamp01(p.CooperationFloor + (1f - p.CooperationFloor) * voluntary);
        }

        /// <summary>協力係数（既定パラメータ）。</summary>
        public static float CooperationCoefficient(float economicFreedom, float coercion)
            => CooperationCoefficient(economicFreedom, coercion, EconomicFreedomParams.Default);

        /// <summary>
        /// 国家への依存度（0..1）＝経済統制が人々を国家に依存させる（パンを握る者が自由を握る）。
        /// 経済統制度×依存倍率＝統制が強いほど生活の糧を国家に握られ、自由を主張できなくなる。
        /// </summary>
        public static float DependencyOnState(float economicControl, EconomicFreedomParams p)
        {
            float control = Mathf.Clamp01(economicControl);
            return Mathf.Clamp01(control * p.DependencyFactor);
        }

        /// <summary>国家依存度（既定パラメータ）。</summary>
        public static float DependencyOnState(float economicControl)
            => DependencyOnState(economicControl, EconomicFreedomParams.Default);

        /// <summary>
        /// 自由に基づく安定（0..1）＝自発的協力に基づく安定は強制された安定より頑健。
        /// 協力係数をそのまま安定度に写す＝合意（自発性）に支えられた秩序は崩れにくい。
        /// </summary>
        public static float StabilityFromFreedom(float cooperationCoefficient)
            => Mathf.Clamp01(cooperationCoefficient);

        /// <summary>
        /// 統制の逆説（協力の減少量）＝過度な経済統制がかえって協力を失わせ安定を損なう。
        /// 経済統制度×逆説率×dt ぶん協力が時間をかけて蝕まれる（統制で強制した協力は自発性を失い、結局は安定を損なう）。
        /// 統制が0なら逆説は起きない。返すのは減少量（正値・呼び出し側が協力から差し引く想定）。
        /// </summary>
        public static float ControlBackfire(float economicControl, float dt, EconomicFreedomParams p)
        {
            if (dt <= 0f) return 0f;
            float control = Mathf.Clamp01(economicControl);
            return Mathf.Max(0f, control * p.BackfireRate * dt);
        }

        /// <summary>統制の逆説（既定パラメータ）。</summary>
        public static float ControlBackfire(float economicControl, float dt)
            => ControlBackfire(economicControl, dt, EconomicFreedomParams.Default);

        /// <summary>
        /// 二つの自由の補完性（0..1）＝経済的自由と政治的自由は補完的（片方だけでは保てない）。
        /// 積＝両方が揃ってはじめて自由が成り立つ（経済的自由だけ、政治的自由だけでは持続しない）。
        /// </summary>
        public static float FreedomComplementarity(float economicFreedom, float politicalFreedom)
        {
            float economic = Mathf.Clamp01(economicFreedom);
            float political = Mathf.Clamp01(politicalFreedom);
            return economic * political;
        }

        /// <summary>
        /// 隷従ドリフトの判定＝経済統制で自発性が失われ隷従へ向かうか。
        /// 経済統制が高く（閾値超え）かつ協力係数が低い（自発性が失われた＝強制に置き換わった）ときに true。
        /// 『隷属への道』＝経済を統制すれば自発的協力は強制に変わり、人々は隷従へ滑り落ちる。
        /// </summary>
        public static bool IsServitudeDrift(float economicControl, float cooperationCoefficient, float threshold)
        {
            float control = Mathf.Clamp01(economicControl);
            float cooperation = Mathf.Clamp01(cooperationCoefficient);
            float t = Mathf.Clamp01(threshold);
            return control > (1f - t) && cooperation < t;
        }

        /// <summary>隷従ドリフト判定（既定パラメータの隷従閾値を使用）。</summary>
        public static bool IsServitudeDrift(float economicControl, float cooperationCoefficient, EconomicFreedomParams p)
            => IsServitudeDrift(economicControl, cooperationCoefficient, p.ServitudeThreshold);
    }
}
