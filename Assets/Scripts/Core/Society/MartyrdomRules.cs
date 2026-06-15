using UnityEngine;

namespace Ginei
{
    /// <summary>殉教の政治の調整係数。</summary>
    public readonly struct MartyrdomParams
    {
        /// <summary>劇的な死の増幅（deathDrama=1 のとき名声がこの割合だけ上乗せ＝死者が生者を超える源泉）。</summary>
        public readonly float dramaBoost;
        /// <summary>殉教強度1あたりの動員力上乗せ（MobilizationBonus=1+これ×強度）。</summary>
        public readonly float mobilizationScale;
        /// <summary>正統性主張における「故人との近さ」の重み。</summary>
        public readonly float proximityWeight;
        /// <summary>正統性主張における「語りの独占度」の重み（近さより重い＝解釈を握る者が勝つ）。</summary>
        public readonly float controlWeight;
        /// <summary>殉教強度の風化速度（単位時間あたりの減少量。ゆっくり＝死者の力は長く残る）。</summary>
        public readonly float decayRate;
        /// <summary>後継正統性ボーナスのスケール（主張の強さ×殉教強度×これ）。</summary>
        public readonly float legitimacyScale;

        public MartyrdomParams(float dramaBoost, float mobilizationScale, float proximityWeight, float controlWeight, float decayRate, float legitimacyScale)
        {
            this.dramaBoost = Mathf.Max(0f, dramaBoost);
            this.mobilizationScale = Mathf.Max(0f, mobilizationScale);
            this.proximityWeight = Mathf.Clamp01(proximityWeight);
            this.controlWeight = Mathf.Clamp01(controlWeight);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.legitimacyScale = Mathf.Max(0f, legitimacyScale);
        }

        /// <summary>既定＝劇的増幅1.0・動員スケール0.5・近さ重み0.4・独占重み0.6・風化0.01/時間・正統性スケール0.3。</summary>
        public static MartyrdomParams Default => new MartyrdomParams(1f, 0.5f, 0.4f, 0.6f, 0.01f, 0.3f);
    }

    /// <summary>
    /// 殉教の政治の純ロジック（ハイネセン/キルヒアイス型＝死者の力）。英雄の死は生前より強い動員力を持ち
    /// （劇的な死ほど名声が増幅され、生前の名声を超える）、遺志を「誰がどれだけ独占して語るか」が
    /// 後継者の正統性を決める＝故人との近さより語りの独占が重い。殉教の強度は風化するがゆっくり＝
    /// 死者は長く政治を縛る。生者の名声（ReputationRules＝別系統）とは分担し、ここは死後の力だけを扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MartyrdomRules
    {
        /// <summary>
        /// 殉教の強度（0..1+dramaBoost）＝死時の名声(0..1)×(1＋劇的増幅×死の劇的さ(0..1))。
        /// 劇的な死（deathDrama&gt;0）なら生前の名声を超える＝死者が生者より強い。
        /// </summary>
        public static float MartyrIntensity(float renownAtDeath, float deathDrama, MartyrdomParams p)
        {
            float renown = Mathf.Clamp01(renownAtDeath);
            float drama = Mathf.Clamp01(deathDrama);
            return renown * (1f + p.dramaBoost * drama);
        }

        public static float MartyrIntensity(float renownAtDeath, float deathDrama)
            => MartyrIntensity(renownAtDeath, deathDrama, MartyrdomParams.Default);

        /// <summary>
        /// 殉教の動員力（倍率≥1）＝1＋動員スケール×強度。生者の動員が「1＋スケール×名声」相当なら、
        /// 強度＞名声（劇的な死）の殉教者は生前より多くを動かす。
        /// </summary>
        public static float MobilizationBonus(float intensity, MartyrdomParams p)
        {
            return 1f + p.mobilizationScale * Mathf.Max(0f, intensity);
        }

        public static float MobilizationBonus(float intensity) => MobilizationBonus(intensity, MartyrdomParams.Default);

        /// <summary>
        /// 遺志の継承者としての主張の強さ（0..1）＝近さ×近さ重み＋独占度×独占重み。
        /// 独占重み＞近さ重み（既定0.6対0.4）＝故人に近かった者より「語りを独占した者」が正統を取る。
        /// </summary>
        public static float LegacyClaimStrength(float proximity, float interpretationControl, MartyrdomParams p)
        {
            float prox = Mathf.Clamp01(proximity);
            float control = Mathf.Clamp01(interpretationControl);
            return Mathf.Clamp01(prox * p.proximityWeight + control * p.controlWeight);
        }

        public static float LegacyClaimStrength(float proximity, float interpretationControl)
            => LegacyClaimStrength(proximity, interpretationControl, MartyrdomParams.Default);

        /// <summary>
        /// 後継者の正統性ボーナス＝主張の強さ×殉教強度×正統性スケール。
        /// 偉大な殉教者（高強度）の遺志を独占するほど大きい＝死者の威光を借りる政治。
        /// </summary>
        public static float SuccessionLegitimacyBonus(float intensity, float proximity, float interpretationControl, MartyrdomParams p)
        {
            return LegacyClaimStrength(proximity, interpretationControl, p) * Mathf.Max(0f, intensity) * p.legitimacyScale;
        }

        public static float SuccessionLegitimacyBonus(float intensity, float proximity, float interpretationControl)
            => SuccessionLegitimacyBonus(intensity, proximity, interpretationControl, MartyrdomParams.Default);

        /// <summary>殉教強度の風化（dt 経過後の強度）。線形にゆっくり減衰し 0 を下回らない＝死者の力は長く残る。</summary>
        public static float IntensityTick(float intensity, float dt, MartyrdomParams p)
        {
            return Mathf.MoveTowards(Mathf.Max(0f, intensity), 0f, p.decayRate * Mathf.Max(0f, dt));
        }

        public static float IntensityTick(float intensity, float dt) => IntensityTick(intensity, dt, MartyrdomParams.Default);

        /// <summary>カルト的象徴か＝殉教強度が閾値以上（祭り上げられて政治を縛る存在）。</summary>
        public static bool IsCultFigure(float intensity, float threshold)
        {
            return Mathf.Max(0f, intensity) >= Mathf.Max(0f, threshold);
        }
    }
}
