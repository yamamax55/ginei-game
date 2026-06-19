using UnityEngine;

namespace Ginei
{
    /// <summary>異端弾圧の調整係数。</summary>
    public readonly struct HereticSuppressionParams
    {
        /// <summary>異端認定における教義逸脱の重み。</summary>
        public readonly float deviationWeight;
        /// <summary>異端認定における異端審問の警戒の重み。</summary>
        public readonly float vigilanceWeight;
        /// <summary>宗教裁判の苛烈さで正統派の熱狂が効く強さ。</summary>
        public readonly float zealWeight;
        /// <summary>見せしめの威圧で公開処刑が効く強さ。</summary>
        public readonly float executionWeight;
        /// <summary>殉教効果で異端者の信念が信仰を固める強さ。</summary>
        public readonly float convictionWeight;
        /// <summary>地下信仰への移行で威圧が押し込む強さ。</summary>
        public readonly float intimidationPush;
        /// <summary>地下信仰への移行で殉教効果が押し込む強さ。</summary>
        public readonly float martyrPush;
        /// <summary>弾圧の正統性で正統派の合意が効く重み。</summary>
        public readonly float consensusWeight;
        /// <summary>弾圧の正統性で異端の脅威が効く重み。</summary>
        public readonly float threatWeight;
        /// <summary>寛容の利益で社会の調和が効く重み。</summary>
        public readonly float harmonyWeight;
        /// <summary>寛容の利益で思想の自由が効く重み。</summary>
        public readonly float freedomWeight;

        public HereticSuppressionParams(float deviationWeight, float vigilanceWeight,
                                        float zealWeight, float executionWeight,
                                        float convictionWeight, float intimidationPush,
                                        float martyrPush, float consensusWeight,
                                        float threatWeight, float harmonyWeight,
                                        float freedomWeight)
        {
            this.deviationWeight = Mathf.Clamp01(deviationWeight);
            this.vigilanceWeight = Mathf.Clamp01(vigilanceWeight);
            this.zealWeight = Mathf.Clamp01(zealWeight);
            this.executionWeight = Mathf.Clamp01(executionWeight);
            this.convictionWeight = Mathf.Clamp01(convictionWeight);
            this.intimidationPush = Mathf.Clamp01(intimidationPush);
            this.martyrPush = Mathf.Clamp01(martyrPush);
            this.consensusWeight = Mathf.Clamp01(consensusWeight);
            this.threatWeight = Mathf.Clamp01(threatWeight);
            this.harmonyWeight = Mathf.Clamp01(harmonyWeight);
            this.freedomWeight = Mathf.Clamp01(freedomWeight);
        }

        /// <summary>
        /// 既定＝認定:逸脱0.6/警戒0.4・苛烈:熱狂0.5・威圧:処刑0.5・殉教:信念0.6・
        /// 地下化:威圧0.5/殉教0.5・正統性:合意0.6/脅威0.4・寛容:調和0.5/自由0.5。
        /// </summary>
        public static HereticSuppressionParams Default
            => new HereticSuppressionParams(0.6f, 0.4f, 0.5f, 0.5f, 0.6f, 0.5f, 0.5f,
                                            0.6f, 0.4f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 異端弾圧の純ロジック＝正統教義からの逸脱（異端）の取り締まり。
    /// 教義逸脱を異端審問の警戒で認定（<see cref="HeresyDetection"/>）し、正統派の熱狂で
    /// 宗教裁判の苛烈さ（<see cref="InquisitionSeverity"/>）が決まる。苛烈さは公開処刑で
    /// 見せしめの威圧（<see cref="Intimidation"/>）になる一方、異端者の信念が強ければ
    /// 殉教が信仰を固める（<see cref="MartyrdomEffect"/>＝弾圧の逆説）。威圧と殉教の双方が
    /// 信仰を地下へ追いやる（<see cref="UndergroundFaith"/>）。弾圧の正統性は正統派の合意と
    /// 異端の脅威で支えられ（<see cref="SuppressionLegitimacy"/>）、対する寛容の選択は社会の調和と
    /// 思想の自由から利益を得る（<see cref="ToleranceBenefit"/>）。核心＝
    /// **弾圧の正味効果**（<see cref="NetSuppressionValue"/>）は威圧から殉教と地下化を差し引いた
    /// もので、苛烈すぎる弾圧は信仰を固め地下へ潜らせて逆効果（-1..1）になりうる。
    /// 純ロジック（非 MonoBehaviour・test-first・決定論）。
    /// </summary>
    public static class HereticSuppressionRules
    {
        /// <summary>
        /// 異端の認定（0..1）＝教義逸脱 doctrinalDeviation×逸脱重みと、異端審問の警戒
        /// inquisitionVigilance×警戒重みの加重和。逸脱があっても審問が緩ければ見逃され、
        /// 審問が鋭くても逸脱が無ければ認定されない。
        /// </summary>
        public static float HeresyDetection(float doctrinalDeviation, float inquisitionVigilance, HereticSuppressionParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(doctrinalDeviation) * p.deviationWeight
                                 + Mathf.Clamp01(inquisitionVigilance) * p.vigilanceWeight);
        }

        public static float HeresyDetection(float doctrinalDeviation, float inquisitionVigilance)
            => HeresyDetection(doctrinalDeviation, inquisitionVigilance, HereticSuppressionParams.Default);

        /// <summary>
        /// 宗教裁判の苛烈さ（0..1）＝異端の認定×（1+正統派の熱狂×熱狂重み）。
        /// 認定が無ければ苛烈さは生じず、正統派が熱狂するほど裁判は厳しくなる。
        /// </summary>
        public static float InquisitionSeverity(float heresyDetection, float orthodoxZeal, HereticSuppressionParams p)
        {
            float det = Mathf.Clamp01(heresyDetection);
            return Mathf.Clamp01(det * (1f + Mathf.Clamp01(orthodoxZeal) * p.zealWeight));
        }

        public static float InquisitionSeverity(float heresyDetection, float orthodoxZeal)
            => InquisitionSeverity(heresyDetection, orthodoxZeal, HereticSuppressionParams.Default);

        /// <summary>
        /// 見せしめの威圧（0..1）＝裁判の苛烈さ×（1+公開処刑×処刑重み）。
        /// 苛烈な裁きを公開で行うほど、見る者を萎縮させる威圧が増す。
        /// </summary>
        public static float Intimidation(float inquisitionSeverity, float publicExecution, HereticSuppressionParams p)
        {
            float sev = Mathf.Clamp01(inquisitionSeverity);
            return Mathf.Clamp01(sev * (1f + Mathf.Clamp01(publicExecution) * p.executionWeight));
        }

        public static float Intimidation(float inquisitionSeverity, float publicExecution)
            => Intimidation(inquisitionSeverity, publicExecution, HereticSuppressionParams.Default);

        /// <summary>
        /// 殉教の効果（0..1）＝裁判の苛烈さ×異端者の信念×信念重み。弾圧の逆説＝
        /// 信念の堅い者を苛烈に処すほど、その死が殉教となって残る信徒の信仰を固める。
        /// 信念が無ければ（迎合・棄教すれば）殉教は生じない。
        /// </summary>
        public static float MartyrdomEffect(float inquisitionSeverity, float hereticConviction, HereticSuppressionParams p)
        {
            float sev = Mathf.Clamp01(inquisitionSeverity);
            return Mathf.Clamp01(sev * Mathf.Clamp01(hereticConviction) * p.convictionWeight);
        }

        public static float MartyrdomEffect(float inquisitionSeverity, float hereticConviction)
            => MartyrdomEffect(inquisitionSeverity, hereticConviction, HereticSuppressionParams.Default);

        /// <summary>
        /// 地下信仰への移行（0..1）＝威圧×押し込み＋殉教効果×押し込み。
        /// 表で信仰できぬ威圧と、殉教で固まった信仰の双方が、信仰を地下へ潜らせる。
        /// </summary>
        public static float UndergroundFaith(float intimidation, float martyrdomEffect, HereticSuppressionParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(intimidation) * p.intimidationPush
                                 + Mathf.Clamp01(martyrdomEffect) * p.martyrPush);
        }

        public static float UndergroundFaith(float intimidation, float martyrdomEffect)
            => UndergroundFaith(intimidation, martyrdomEffect, HereticSuppressionParams.Default);

        /// <summary>
        /// 弾圧の正統性（0..1）＝正統派の合意×合意重み＋異端の脅威×脅威重み。
        /// 教団内の合意が厚く、異端が秩序への現実の脅威であるほど、弾圧は正当と見なされる。
        /// </summary>
        public static float SuppressionLegitimacy(float orthodoxConsensus, float hereticThreat, HereticSuppressionParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(orthodoxConsensus) * p.consensusWeight
                                 + Mathf.Clamp01(hereticThreat) * p.threatWeight);
        }

        public static float SuppressionLegitimacy(float orthodoxConsensus, float hereticThreat)
            => SuppressionLegitimacy(orthodoxConsensus, hereticThreat, HereticSuppressionParams.Default);

        /// <summary>
        /// 寛容の利益（0..1）＝社会の調和×調和重み＋思想の自由×自由重み。
        /// 弾圧しない選択の便益＝多様な信仰が共存する社会の調和と、思想の自由がもたらす活力。
        /// </summary>
        public static float ToleranceBenefit(float socialHarmony, float intellectualFreedom, HereticSuppressionParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(socialHarmony) * p.harmonyWeight
                                 + Mathf.Clamp01(intellectualFreedom) * p.freedomWeight);
        }

        public static float ToleranceBenefit(float socialHarmony, float intellectualFreedom)
            => ToleranceBenefit(socialHarmony, intellectualFreedom, HereticSuppressionParams.Default);

        /// <summary>
        /// 弾圧の正味効果（-1..1）＝威圧−殉教効果−地下化。威圧は弾圧の成果（信仰を萎縮させる）だが、
        /// 殉教（信仰の硬化）と地下化（信仰の潜伏）はその裏目。苛烈な弾圧は後二者を膨らませ、
        /// 正味は負＝逆効果になりうる（弾圧が信仰を強めてしまう）。p は対称合成のため未使用だが
        /// API 統一のため受ける。
        /// </summary>
        public static float NetSuppressionValue(float intimidation, float martyrdomEffect, float undergroundFaith, HereticSuppressionParams p)
        {
            float net = Mathf.Clamp01(intimidation)
                        - Mathf.Clamp01(martyrdomEffect)
                        - Mathf.Clamp01(undergroundFaith);
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float NetSuppressionValue(float intimidation, float martyrdomEffect, float undergroundFaith)
            => NetSuppressionValue(intimidation, martyrdomEffect, undergroundFaith, HereticSuppressionParams.Default);
    }
}
