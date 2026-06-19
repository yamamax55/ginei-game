using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 反撃（防御から攻勢への切り返し）の調整値。敵が攻勢限界に達したと見なす侵攻深度・補給逼迫の寄与、
    /// 反撃の好機が立つしきい値、早すぎる反撃の損失係数、受け続けすぎる遅延損失係数。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct CounterAttackParams
    {
        /// <summary>敵の侵攻深度が攻勢限界へ効く寄与（深く伸びるほど限界へ近づく）。</summary>
        public readonly float advanceWeight;
        /// <summary>敵の補給逼迫が攻勢限界へ効く寄与（補給が伸びきるほど限界へ近づく）。</summary>
        public readonly float supplyWeight;
        /// <summary>早すぎる反撃の損失係数（敵がまだ強い間に出ると損が大きい）。</summary>
        public readonly float prematurePenalty;
        /// <summary>受け続けすぎる遅延損失係数（守りが磨り減って崩れる）。</summary>
        public readonly float latePenalty;

        public CounterAttackParams(float advanceWeight, float supplyWeight, float prematurePenalty, float latePenalty)
        {
            this.advanceWeight = Mathf.Clamp01(advanceWeight);
            this.supplyWeight = Mathf.Clamp01(supplyWeight);
            this.prematurePenalty = Mathf.Clamp01(prematurePenalty);
            this.latePenalty = Mathf.Clamp01(latePenalty);
        }

        /// <summary>既定：侵攻寄与0.5・補給寄与0.5（合計1）・早すぎ損失0.8・遅すぎ損失0.6。</summary>
        public static CounterAttackParams Default => new CounterAttackParams(
            DefaultAdvanceWeight, DefaultSupplyWeight, DefaultPrematurePenalty, DefaultLatePenalty);

        public const float DefaultAdvanceWeight = 0.5f;
        public const float DefaultSupplyWeight = 0.5f;
        public const float DefaultPrematurePenalty = 0.8f;
        public const float DefaultLatePenalty = 0.6f;
    }

    /// <summary>
    /// 反撃の純ロジック（唯一の窓口）＝<b>攻勢限界点を過ぎて伸びきった敵を受け止め、勢いを失った好機に切り返す</b>。
    /// 攻めてくる敵をいったん防御で受け、敵が攻勢限界（補給が伸びきり勢いを失う点）に達したところで攻勢へ転じる。
    /// 切り返しのタイミングが肝＝早すぎれば敵はまだ強く損を出し、遅すぎれば守りが崩れる。
    /// <b>分担</b>：<c>CulminatingPointRules</c>（攻勢終末点）があればその<b>敵版</b>を <see cref="EnemyCulmination"/> の入力に取り、
    /// <c>ReserveDeploymentRules</c>（予備の投入規模）とは別＝こちらは<b>防御から攻勢への切り返し判断</b>（いつ転じるか）。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class CounterAttackRules
    {
        /// <summary>既定パラメータで敵の攻勢限界到達度。</summary>
        public static float EnemyCulmination(float enemyAdvanceDepth, float enemySupplyStrain)
            => EnemyCulmination(enemyAdvanceDepth, enemySupplyStrain, CounterAttackParams.Default);

        /// <summary>
        /// 敵が攻勢限界に達した度合い（0..1）。侵攻が深いほど、補給が逼迫するほど高い＝伸びきって勢いを失う。
        /// culm＝clamp01(侵攻深度×advanceWeight＋補給逼迫×supplyWeight)。0＝まだ余裕／1＝攻勢終末点。
        /// </summary>
        public static float EnemyCulmination(float enemyAdvanceDepth, float enemySupplyStrain, CounterAttackParams p)
        {
            float depth = Mathf.Clamp01(enemyAdvanceDepth);
            float strain = Mathf.Clamp01(enemySupplyStrain);
            return Mathf.Clamp01(depth * p.advanceWeight + strain * p.supplyWeight);
        }

        /// <summary>
        /// 反撃の好機（0..1）。敵が攻勢限界に達し、かつ自軍の予備が整っているほど好機が立つ。
        /// timing＝敵限界×自予備の整い。どちらか欠ければ好機は立たない（積＝両立条件）。
        /// </summary>
        public static float CounterTiming(float enemyCulmination, float ownReserveReadiness)
        {
            float culm = Mathf.Clamp01(enemyCulmination);
            float ready = Mathf.Clamp01(ownReserveReadiness);
            return Mathf.Clamp01(culm * ready);
        }

        /// <summary>
        /// 反撃の打撃（0..1）。好機が立っているほど、予備兵力が厚いほど、敵が疲れているほど大きい＝疲れた敵を叩く。
        /// impact＝好機×予備兵力×敵疲弊。三者の積＝どれかが欠ければ打撃は出ない。
        /// </summary>
        public static float CounterImpact(float counterTiming, float reserveStrength, float enemyExhaustion)
        {
            float timing = Mathf.Clamp01(counterTiming);
            float reserve = Mathf.Clamp01(reserveStrength);
            float exhaust = Mathf.Clamp01(enemyExhaustion);
            return Mathf.Clamp01(timing * reserve * exhaust);
        }

        /// <summary>既定パラメータで早すぎる反撃の損失。</summary>
        public static float PrematureCounterLoss(float enemyCulmination)
            => PrematureCounterLoss(enemyCulmination, CounterAttackParams.Default);

        /// <summary>
        /// 早すぎる反撃の損失（0..1）。敵がまだ攻勢限界に達していない（culm が低い）ほど損が大きい＝強い敵に逆撃して跳ね返される。
        /// loss＝(1−敵限界)×prematurePenalty。敵が完全に伸びきっていれば（culm=1）損は0、まだ強ければ（culm=0）損は最大。
        /// </summary>
        public static float PrematureCounterLoss(float enemyCulmination, CounterAttackParams p)
        {
            float culm = Mathf.Clamp01(enemyCulmination);
            return Mathf.Clamp01((1f - culm) * p.prematurePenalty);
        }

        /// <summary>既定パラメータで受け続けすぎる遅延損失。</summary>
        public static float TooLateLoss(float defenseAttrition)
            => TooLateLoss(defenseAttrition, CounterAttackParams.Default);

        /// <summary>
        /// 受け続けすぎて守りが崩れる損失（0..1）。防御の消耗が進むほど損が大きい＝好機を逃して受け身を続けると守りが磨り減る。
        /// loss＝防御消耗×latePenalty。消耗0で損なし、消耗1で最大。<see cref="PrematureCounterLoss"/>と対の遅延側リスク。
        /// </summary>
        public static float TooLateLoss(float defenseAttrition, CounterAttackParams p)
        {
            float attrition = Mathf.Clamp01(defenseAttrition);
            return Mathf.Clamp01(attrition * p.latePenalty);
        }

        /// <summary>
        /// 反撃で攻守が逆転する度合い（0..1）。反撃の打撃が大きいほど、受けに回っていた攻守が入れ替わる。
        /// reversal＝反撃打撃。打撃をそのまま逆転度に写す（クランプのみ）＝弱い反撃では逆転せず、強い反撃で攻守が入れ替わる。
        /// </summary>
        public static float MomentumReversal(float counterImpact)
        {
            return Mathf.Clamp01(counterImpact);
        }

        /// <summary>
        /// 反撃成功から追撃への移行（0..1）。攻守逆転が進み、かつ敵が崩れ（潰走し）始めているほど追撃に移れる。
        /// transition＝逆転度×敵潰走。逆転していても敵が崩れていなければ追撃には移れない（積＝両立条件）。
        /// </summary>
        public static float PursuitTransition(float momentumReversal, float enemyRout)
        {
            float reversal = Mathf.Clamp01(momentumReversal);
            float rout = Mathf.Clamp01(enemyRout);
            return Mathf.Clamp01(reversal * rout);
        }

        /// <summary>
        /// 今、防御から攻勢へ転じるべきか（bool）＝反撃の好機がしきい値以上。僅かな好機では切り返さず受けを続ける（既定0.5）。
        /// </summary>
        public static bool ShouldCounterattack(float counterTiming, float threshold = 0.5f)
        {
            float timing = Mathf.Clamp01(counterTiming);
            return timing >= Mathf.Clamp01(threshold);
        }
    }
}
