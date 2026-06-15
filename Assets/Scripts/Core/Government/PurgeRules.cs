using UnityEngine;

namespace Ginei
{
    /// <summary>粛清の調整係数（リップシュタット後／スターリン型）。</summary>
    public readonly struct PurgeParams
    {
        /// <summary>全面粛清が統制に返す最大利得（敵対派閥の物理的消滅）。</summary>
        public readonly float controlGainScale;
        /// <summary>全面粛清が人材プールを毀損する最大割合。</summary>
        public readonly float talentLossScale;
        /// <summary>恐怖の萎縮が組織のイニシアチブを削る最大幅。</summary>
        public readonly float paralysisScale;
        /// <summary>証拠の質ゼロでの冤罪率（粛清が雑なほど無実が消える）。</summary>
        public readonly float maxFalsePositive;

        public PurgeParams(float controlGainScale, float talentLossScale, float paralysisScale, float maxFalsePositive)
        {
            this.controlGainScale = Mathf.Max(0f, controlGainScale);
            this.talentLossScale = Mathf.Clamp01(talentLossScale);
            this.paralysisScale = Mathf.Clamp01(paralysisScale);
            this.maxFalsePositive = Mathf.Clamp01(maxFalsePositive);
        }

        /// <summary>既定＝統制利得0.5・人材毀損0.4・萎縮0.5・冤罪上限0.7。</summary>
        public static PurgeParams Default => new PurgeParams(0.5f, 0.4f, 0.5f, 0.7f);
    }

    /// <summary>
    /// 粛清の純ロジック（政策としての大規模排除）。敵対派閥の一掃は統制を即座に固めるが、
    /// ①人材プールの毀損（有能も網にかかる）、②恐怖の萎縮（誰も決断しなくなる＝イニシアチブ消滅）、
    /// ③冤罪（証拠の質が低いほど無実が消え、残った者の面従腹背を生む）の三重の代償を払う。
    /// 表面の忠誠は上がるが本音は見えなくなる＝選好偽装（バックログ PreferenceFalsificationRules）への接続点。
    /// クーデターの結末の粛清（<see cref="CoupRules"/>）とは別系統＝平時の政策としての粛清。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PurgeRules
    {
        /// <summary>統制の利得（0..controlGainScale）＝粛清規模×敵対派閥の残存度 oppositionStrength(0..1)。</summary>
        public static float ControlGain(float scale, float oppositionStrength, PurgeParams p)
        {
            return Mathf.Clamp01(scale) * Mathf.Clamp01(oppositionStrength) * p.controlGainScale;
        }

        public static float ControlGain(float scale, float oppositionStrength)
            => ControlGain(scale, oppositionStrength, PurgeParams.Default);

        /// <summary>人材プールの毀損率（0..talentLossScale）＝粛清規模に比例。能力ある者ほど網にかかる。</summary>
        public static float TalentLoss(float scale, PurgeParams p)
        {
            return Mathf.Clamp01(scale) * p.talentLossScale;
        }

        public static float TalentLoss(float scale) => TalentLoss(scale, PurgeParams.Default);

        /// <summary>
        /// 恐怖の萎縮（0..paralysisScale）＝粛清規模に比例して組織のイニシアチブが死ぬ
        /// （間違えたら消されるなら、誰も判断しない）。意思決定速度・自律性の倍率として 1−これ を掛ける。
        /// </summary>
        public static float FearParalysis(float scale, PurgeParams p)
        {
            return Mathf.Clamp01(scale) * p.paralysisScale;
        }

        public static float FearParalysis(float scale) => FearParalysis(scale, PurgeParams.Default);

        /// <summary>
        /// 冤罪率（0..maxFalsePositive）＝（1−証拠の質 evidenceQuality(0..1)）×規模。
        /// 丁寧な摘発（証拠の質1）なら冤罪なし、雑な大粛清は無実を大量に消す。
        /// </summary>
        public static float FalsePositiveRatio(float scale, float evidenceQuality, PurgeParams p)
        {
            return (1f - Mathf.Clamp01(evidenceQuality)) * Mathf.Clamp01(scale) * p.maxFalsePositive;
        }

        public static float FalsePositiveRatio(float scale, float evidenceQuality)
            => FalsePositiveRatio(scale, evidenceQuality, PurgeParams.Default);

        /// <summary>
        /// 生存者の表面忠誠（0..1）＝恐怖で上がる見かけの忠誠。だが本音との乖離＝冤罪率×規模で広がる
        /// （無実が消えるのを見た者は面従腹背になる）。out で乖離（選好偽装の入力）も返す。
        /// </summary>
        public static float SurvivorProfessedLoyalty(float scale, float evidenceQuality, PurgeParams p, out float falsification)
        {
            float professed = Mathf.Clamp01(0.5f + Mathf.Clamp01(scale) * 0.5f); // 恐怖は口を揃えさせる
            falsification = FalsePositiveRatio(scale, evidenceQuality, p);        // 乖離＝冤罪を見た分
            return professed;
        }

        /// <summary>
        /// 粛清の純効果＝統制利得−人材毀損−萎縮（冤罪は乖離として別勘定）。
        /// 標的が明確で証拠が固い小規模粛清だけが引き合う、を数値で出す。
        /// </summary>
        public static float NetEffect(float scale, float oppositionStrength, PurgeParams p)
        {
            return ControlGain(scale, oppositionStrength, p) - TalentLoss(scale, p) - FearParalysis(scale, p);
        }

        public static float NetEffect(float scale, float oppositionStrength)
            => NetEffect(scale, oppositionStrength, PurgeParams.Default);
    }
}
