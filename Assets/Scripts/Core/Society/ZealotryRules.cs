using UnityEngine;

namespace Ginei
{
    /// <summary>狂信（信仰的熱狂）の戦力化と暴走の調整係数。信仰・洗脳・迫害が狂信を煽り、戦意と制御不能の両刃を生む。</summary>
    public readonly struct ZealotryParams
    {
        /// <summary>洗脳の増幅率（洗脳1で信仰の深さをどれだけ煽るか）。</summary>
        public readonly float indoctrinationGain;
        /// <summary>迫害の増幅率（迫害1で狂信をどれだけ煽るか＝弾圧が殉教熱を生む）。</summary>
        public readonly float persecutionGain;
        /// <summary>戦意スケール（狂信1のときの死を恐れぬ戦意の最大値）。</summary>
        public readonly float fervorScale;
        /// <summary>殉教志願スケール（狂信×来世報酬1のときの自爆志願の最大値）。</summary>
        public readonly float martyrdomScale;
        /// <summary>合理性喪失スケール（狂信1で失う合理的判断の最大幅）。</summary>
        public readonly float rationalityLossScale;
        /// <summary>自爆威力スケール（殉教志願×標的の隙1のときの自爆威力の最大値）。</summary>
        public readonly float suicidePowerScale;
        /// <summary>伝染スケール（狂信×結束1のときの伝染強度の最大値）。</summary>
        public readonly float contagionScale;
        /// <summary>制御不能スケール（狂信×判断喪失1のときの暴走度の最大値）。</summary>
        public readonly float uncontrolScale;

        public ZealotryParams(float indoctrinationGain, float persecutionGain, float fervorScale,
                              float martyrdomScale, float rationalityLossScale, float suicidePowerScale,
                              float contagionScale, float uncontrolScale)
        {
            this.indoctrinationGain = Mathf.Max(0f, indoctrinationGain);
            this.persecutionGain = Mathf.Max(0f, persecutionGain);
            this.fervorScale = Mathf.Clamp01(fervorScale);
            this.martyrdomScale = Mathf.Clamp01(martyrdomScale);
            this.rationalityLossScale = Mathf.Clamp01(rationalityLossScale);
            this.suicidePowerScale = Mathf.Clamp01(suicidePowerScale);
            this.contagionScale = Mathf.Clamp01(contagionScale);
            this.uncontrolScale = Mathf.Clamp01(uncontrolScale);
        }

        /// <summary>既定＝洗脳増幅0.5・迫害増幅0.4・戦意1.0・殉教0.9・合理性喪失0.8・自爆威力1.0・伝染0.7・制御不能0.9。</summary>
        public static ZealotryParams Default =>
            new ZealotryParams(0.5f, 0.4f, 1.0f, 0.9f, 0.8f, 1.0f, 0.7f, 0.9f);
    }

    /// <summary>
    /// 狂信（ゼロット＝信仰的熱狂）の純ロジック。信仰の深さ・洗脳・迫害が狂信の強度を煽り、狂信は死を恐れぬ戦意と
    /// 殉教（自爆）志願を生む一方、合理的判断を失わせ、集団で伝染し、ついには指揮に従わぬ暴走（制御不能）へ至る。
    /// 狂信兵は<b>両刃</b>＝高い戦意は戦力だが、制御不能はそれを蝕む（正味戦力価値は-1..1で振れる）。
    /// <para>
    /// 分担：<c>MoraleContagionRules</c>（勝敗の士気伝播）とは別＝こちらは<b>信仰・イデオロギーへの狂信</b>に特化。
    /// 盤面非依存の plain 引数・実効値パターン。乱数は持たない（決定論）。純ロジック（非 MonoBehaviour・test-first）。
    /// </para>
    /// </summary>
    public static class ZealotryRules
    {
        /// <summary>
        /// 狂信の強度（0..1）＝信仰の深さ faithDepth(0..1) を洗脳 indoctrination(0..1) と迫害 persecution(0..1) が煽る。
        /// amp = 1 + 洗脳×増幅 + 迫害×増幅。弾圧が殉教熱を生む＝迫害も狂信を高める。
        /// </summary>
        public static float ZealotryIntensity(float faithDepth, float indoctrination, float persecution, ZealotryParams p)
        {
            float faith = Mathf.Clamp01(faithDepth);
            float ind = Mathf.Clamp01(indoctrination);
            float per = Mathf.Clamp01(persecution);
            float amp = 1f + ind * p.indoctrinationGain + per * p.persecutionGain;
            return Mathf.Clamp01(faith * amp);
        }

        public static float ZealotryIntensity(float faithDepth, float indoctrination, float persecution)
            => ZealotryIntensity(faithDepth, indoctrination, persecution, ZealotryParams.Default);

        /// <summary>
        /// 戦意（0..1）＝狂信に応じた死を恐れぬ突撃。狂信が高いほど高揚するが逓減（平方根）＝
        /// 弱い信仰でも一定の戦意は出る。fervorScale で頭打ち。
        /// </summary>
        public static float CombatFervor(float zealotryIntensity, ZealotryParams p)
        {
            float z = Mathf.Clamp01(zealotryIntensity);
            return Mathf.Clamp01(Mathf.Sqrt(z) * p.fervorScale);
        }

        public static float CombatFervor(float zealotryIntensity)
            => CombatFervor(zealotryIntensity, ZealotryParams.Default);

        /// <summary>
        /// 殉教（自爆）志願（0..1）＝狂信 zealotryIntensity と来世の報酬 afterlifeReward(0..1) の積×係数。
        /// 来世の報酬を強く信じるほど死を厭わない＝両方高いとき志願が跳ね上がる。
        /// </summary>
        public static float MartyrdomWillingness(float zealotryIntensity, float afterlifeReward, ZealotryParams p)
        {
            float z = Mathf.Clamp01(zealotryIntensity);
            float reward = Mathf.Clamp01(afterlifeReward);
            return Mathf.Clamp01(z * reward * p.martyrdomScale);
        }

        public static float MartyrdomWillingness(float zealotryIntensity, float afterlifeReward)
            => MartyrdomWillingness(zealotryIntensity, afterlifeReward, ZealotryParams.Default);

        /// <summary>
        /// 合理性の喪失（0..1）＝狂信が高いほど合理的判断を失う。zealotryIntensity×係数の線形。
        /// 損得・退却・降伏といった理性的選択ができなくなる度合い。
        /// </summary>
        public static float RationalityLoss(float zealotryIntensity, ZealotryParams p)
        {
            float z = Mathf.Clamp01(zealotryIntensity);
            return Mathf.Clamp01(z * p.rationalityLossScale);
        }

        public static float RationalityLoss(float zealotryIntensity)
            => RationalityLoss(zealotryIntensity, ZealotryParams.Default);

        /// <summary>
        /// 自爆攻撃の威力（0..1）＝殉教志願 martyrdomWillingness(0..1) ×標的の隙 targetVulnerability(0..1) ×係数。
        /// 志願者がいても標的に隙が無ければ効かず、隙があるほど一撃が大きい。
        /// </summary>
        public static float SuicideAttackPower(float martyrdomWillingness, float targetVulnerability, ZealotryParams p)
        {
            float willing = Mathf.Clamp01(martyrdomWillingness);
            float vuln = Mathf.Clamp01(targetVulnerability);
            return Mathf.Clamp01(willing * vuln * p.suicidePowerScale);
        }

        public static float SuicideAttackPower(float martyrdomWillingness, float targetVulnerability)
            => SuicideAttackPower(martyrdomWillingness, targetVulnerability, ZealotryParams.Default);

        /// <summary>
        /// 狂信の伝染（0..1）＝狂信 zealotryIntensity ×集団の結束 groupCohesion(0..1) ×係数。
        /// 結束した集団ほど狂信が燃え広がる（同調圧力で熱狂が伝播する）。
        /// </summary>
        public static float ZealotryContagion(float zealotryIntensity, float groupCohesion, ZealotryParams p)
        {
            float z = Mathf.Clamp01(zealotryIntensity);
            float cohesion = Mathf.Clamp01(groupCohesion);
            return Mathf.Clamp01(z * cohesion * p.contagionScale);
        }

        public static float ZealotryContagion(float zealotryIntensity, float groupCohesion)
            => ZealotryContagion(zealotryIntensity, groupCohesion, ZealotryParams.Default);

        /// <summary>
        /// 制御不能（0..1）＝狂信 zealotryIntensity ×合理性喪失 rationalityLoss(0..1) ×係数。
        /// 理性を失った狂信ほど指揮に従わず暴走する（独断専行・命令無視の度合い）。
        /// </summary>
        public static float Uncontrollability(float zealotryIntensity, float rationalityLoss, ZealotryParams p)
        {
            float z = Mathf.Clamp01(zealotryIntensity);
            float loss = Mathf.Clamp01(rationalityLoss);
            return Mathf.Clamp01(z * loss * p.uncontrolScale);
        }

        public static float Uncontrollability(float zealotryIntensity, float rationalityLoss)
            => Uncontrollability(zealotryIntensity, rationalityLoss, ZealotryParams.Default);

        /// <summary>
        /// 狂信兵の正味戦力価値（-1..1・両刃）＝戦意 combatFervor − 制御不能 uncontrollability。
        /// 高い戦意は戦力（正）だが、制御不能はそれを相殺し下回れば負（味方の足を引っ張る暴走集団）。
        /// </summary>
        public static float ZealotMilitaryValue(float combatFervor, float uncontrollability, ZealotryParams p)
        {
            float fervor = Mathf.Clamp01(combatFervor);
            float uncontrol = Mathf.Clamp01(uncontrollability);
            return Mathf.Clamp(fervor - uncontrol, -1f, 1f);
        }

        public static float ZealotMilitaryValue(float combatFervor, float uncontrollability)
            => ZealotMilitaryValue(combatFervor, uncontrollability, ZealotryParams.Default);
    }
}
