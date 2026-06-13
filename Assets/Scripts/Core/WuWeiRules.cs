using UnityEngine;

namespace Ginei
{
    /// <summary>無為（むい）ガバナンスの調整係数（老子型・治大国若烹小鮮＝大国を治めるは小魚を煮るが如し）。</summary>
    public readonly struct WuWeiParams
    {
        /// <summary>民の自己組織化が生む自然な安定の最大寄与（自己組織化1のとき）。為さずして治まる効。</summary>
        public readonly float selfOrderScale;
        /// <summary>逆U字の幅＝最適介入点からこの距離だけ離れると効果が0になる目安（小さいほど鋭い山）。</summary>
        public readonly float interventionScale;
        /// <summary>過剰統治のペナルティの強さ（閾値超過1単位あたり）。介入過剰が民の自発性を殺す度合い。</summary>
        public readonly float overGovernScale;
        /// <summary>控えめな統治（為さざる）が民の自発性に与えるボーナスの上限（restraint1のとき）。</summary>
        public readonly float restraintBonusCap;
        /// <summary>政府が手を引いたとき民が自ら秩序を育てる基礎速度/秒（restraint1のとき）。</summary>
        public readonly float selfOrderRate;
        /// <summary>統治しすぎ判定の許容幅＝最適介入点からこの差を超えて介入すると「無為を忘れた」と見なす。</summary>
        public readonly float overGovernThreshold;

        public WuWeiParams(float selfOrderScale, float interventionScale, float overGovernScale,
            float restraintBonusCap, float selfOrderRate, float overGovernThreshold)
        {
            this.selfOrderScale = Mathf.Clamp01(selfOrderScale);
            this.interventionScale = Mathf.Max(0.001f, interventionScale);
            this.overGovernScale = Mathf.Max(0f, overGovernScale);
            this.restraintBonusCap = Mathf.Clamp01(restraintBonusCap);
            this.selfOrderRate = Mathf.Max(0f, selfOrderRate);
            this.overGovernThreshold = Mathf.Clamp01(overGovernThreshold);
        }

        /// <summary>
        /// 既定＝自己組織化寄与0.6・逆U字幅0.5・過統治0.8・控えめボーナス上限0.3・自己秩序化0.1/秒・過統治許容0.25。
        /// 民の自己組織化に任せれば自然な安定が0.6まで積み、介入は最適点±0.5で効果が消え、最適から0.25を超えて
        /// 介入すると統治しすぎ＝逆U字でかえって安定を損なう（小魚をいじりすぎると煮崩れる）。
        /// </summary>
        public static WuWeiParams Default => new WuWeiParams(0.6f, 0.5f, 0.8f, 0.3f, 0.1f, 0.25f);
    }

    /// <summary>
    /// 無為（むい）ガバナンスの純ロジック（LAOZ-1 #1546・老子＝無為の治＝為さざるに似て為さざるは無し）。
    /// 過度に介入せず民の自発性に任せると自然に治まる＝<b>少ない介入は自然な安定を生む</b>。だが介入が
    /// 最適点を超えると<b>逆U字でかえって安定を損なう</b>（治大国若烹小鮮＝大国を治めるは小魚を煮るが如し＝
    /// かき回しすぎると煮崩れる）。介入の最適点と、それを超えたときの逆効果が核。
    /// <see cref="GovernanceRules"/>（安定度の収束計算＝目標値へ MoveTowards）／
    /// <see cref="ReversalRules"/>（反者道之動＝極まれば反転する・同EPIC LAOZ）／
    /// <see cref="ContentmentRules"/>（知足＝足るを知る・同EPIC LAOZ）／
    /// <see cref="WaterDoctrineRules"/>（柔弱＝上善は水の如し・同EPIC LAOZ）とは分担し、ここは
    /// <b>無為の治の逆U字＝介入の最適点を超えると逆効果</b>のみを扱う（係数算出のみ・基準非破壊）。
    /// すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WuWeiRules
    {
        /// <summary>
        /// 民の自己組織化に任せたときの自然な安定（0..1＝無為の効）＝自己組織化×(1−介入)×寄与係数。
        /// 民が自ら組織化するほど（selfOrganization大）、かつ政府が介入を控えるほど（intervention小）自然に治まる
        /// ＝為さずして治まる。介入が最大(1)なら自己組織化の効は完全に消える（手を出しすぎて自発性を奪う）。
        /// </summary>
        public static float NaturalStability(float selfOrganization, float intervention, WuWeiParams p)
        {
            float org = Mathf.Clamp01(selfOrganization);
            float iv = Mathf.Clamp01(intervention);
            return Mathf.Clamp01(org * (1f - iv) * p.selfOrderScale);
        }

        public static float NaturalStability(float selfOrganization, float intervention)
            => NaturalStability(selfOrganization, intervention, WuWeiParams.Default);

        /// <summary>
        /// 介入の効果（−1..1＝逆U字）＝最適点で最大、離れるほど下がり、超えると負に転じる。
        /// 効果 = 1 − ((intervention − optimal)/幅)^2。最適介入点(optimal)で +1、そこから離れると放物線で落ち、
        /// 幅を超えると負＝かえって安定を損なう（小魚をかき回しすぎて煮崩す）。逆U字＝無為の治の核。
        /// </summary>
        public static float InterventionEffect(float intervention, float optimalIntervention, WuWeiParams p)
        {
            float iv = Mathf.Clamp01(intervention);
            float opt = Mathf.Clamp01(optimalIntervention);
            float d = (iv - opt) / p.interventionScale;
            // 逆U字：最適点で1、離れるほど放物線で下がり、幅超過で負（Mathf.Sin 非依存の多項式）。
            return Mathf.Clamp(1f - d * d, -1f, 1f);
        }

        public static float InterventionEffect(float intervention, float optimalIntervention)
            => InterventionEffect(intervention, optimalIntervention, WuWeiParams.Default);

        /// <summary>
        /// 過度な統治のペナルティ（0..1＝民の自発性を殺す損失）＝(介入−閾値)を超過分だけ×過統治係数。
        /// 介入が閾値(threshold)以下なら0（無為＝痛みなし）、超えるほど自発性を奪うペナルティが増える
        /// ＝いじりすぎが自然の治を壊す。閾値はその系がどこまで介入を許すか（脆い系ほど低く設定する想定）。
        /// </summary>
        public static float OverGovernancePenalty(float intervention, float threshold, WuWeiParams p)
        {
            float iv = Mathf.Clamp01(intervention);
            float th = Mathf.Clamp01(threshold);
            float excess = Mathf.Max(0f, iv - th);
            return Mathf.Clamp01(excess * p.overGovernScale);
        }

        public static float OverGovernancePenalty(float intervention, float threshold)
            => OverGovernancePenalty(intervention, threshold, WuWeiParams.Default);

        /// <summary>
        /// 控えめな統治のボーナス（0..1＝為さざるの効）＝抑制×ボーナス上限。政府が手を控えるほど（restraint大）
        /// 民の自発性が活き、安定にボーナスが乗る＝為さずして為す。抑制0（全力介入）ならボーナスなし。
        /// 実効値パターン＝呼び出し側で安定へ加算する想定（基準は変えない）。
        /// </summary>
        public static float MinimalInterventionBonus(float restraint, WuWeiParams p)
        {
            float r = Mathf.Clamp01(restraint);
            return Mathf.Clamp01(r * p.restraintBonusCap);
        }

        public static float MinimalInterventionBonus(float restraint)
            => MinimalInterventionBonus(restraint, WuWeiParams.Default);

        /// <summary>
        /// 自己秩序化の1tick（0..1の新しい秩序）＝政府が手を引くほど民が自ら秩序を育てる（無為自然）。
        /// 増分 = 抑制×自己秩序化率×dt。政府抑制(governmentRestraint)が高いほど民の自発的秩序が育つ＝為さずして秩序が
        /// 生まれる。抑制0（過介入）なら秩序は育たない（自発性を奪う）。現在秩序 order に積んで返す。
        /// </summary>
        public static float SelfOrderingTick(float order, float governmentRestraint, float dt, WuWeiParams p)
        {
            float o = Mathf.Clamp01(order);
            float r = Mathf.Clamp01(governmentRestraint);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(o + r * p.selfOrderRate * step);
        }

        public static float SelfOrderingTick(float order, float governmentRestraint, float dt)
            => SelfOrderingTick(order, governmentRestraint, dt, WuWeiParams.Default);

        /// <summary>
        /// 状況依存の最適介入度（0..1）＝危機時は介入を増やし、平時は介入を控える（最適点は危機で右へ動く）。
        /// 平時(crisisLevel0)は最適介入が小さく無為に近い、危機(crisisLevel1)は最適介入が大きい＝危機には手を打つ。
        /// この値を <see cref="InterventionEffect"/> の optimalIntervention に渡せば、状況に応じた最適点で逆U字が決まる。
        /// </summary>
        public static float OptimalInterventionPoint(float crisisLevel)
        {
            return Mathf.Clamp01(crisisLevel);
        }

        /// <summary>
        /// いじりすぎのダメージ（0..1）＝脆いシステムほど過介入のダメージが大きい（小魚＝煮崩れやすい）。
        /// ダメージ = 介入×脆さ＝介入が強く（intervention大）かつ系が脆い（systemFragility大）ほど大きい
        /// ＝小魚をかき回すほど煮崩れる。頑健な系(fragility小)なら多少いじっても崩れない。
        /// </summary>
        public static float MeddlingDamage(float intervention, float systemFragility)
        {
            float iv = Mathf.Clamp01(intervention);
            float frag = Mathf.Clamp01(systemFragility);
            return Mathf.Clamp01(iv * frag);
        }

        /// <summary>
        /// 統治しすぎ（無為を忘れた）判定（true＝最適点を許容幅 threshold 超えて介入している）。
        /// 介入が最適介入点より threshold を超えて多ければ過統治＝小魚をかき回しすぎ。最適点以下や許容内なら無為の範囲。
        /// </summary>
        public static bool IsOverGoverned(float intervention, float optimalIntervention, float threshold)
        {
            float iv = Mathf.Clamp01(intervention);
            float opt = Mathf.Clamp01(optimalIntervention);
            float th = Mathf.Clamp01(threshold);
            return iv - opt > th;
        }

        public static bool IsOverGoverned(float intervention, float optimalIntervention)
            => IsOverGoverned(intervention, optimalIntervention, WuWeiParams.Default.overGovernThreshold);
    }
}
