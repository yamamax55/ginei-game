using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 陣営声望の純データ（声望システム KORY-1 #1406・項羽と劉邦参考）。陣営が人材を引き寄せる磁力＝声望を保持する。
    /// prestige＝声望（人望・徳望の総合 0..1）、virtue＝徳望（人を遇する度量 0..1）、momentum＝勢い（趨勢 0..1）。
    /// 解決は <see cref="PrestigeRules"/> が唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public struct PrestigeState
    {
        /// <summary>声望（人望・徳望の総合）0..1。高いほど才人が自ら集まる。</summary>
        public float prestige;
        /// <summary>徳望（人を遇する度量）0..1。人を大切にする度量が声望を生む源。</summary>
        public float virtue;
        /// <summary>勢い（趨勢）0..1。勝ち馬に乗る心理＝磁力を増幅する。</summary>
        public float momentum;

        public PrestigeState(float prestige, float virtue = 0.5f, float momentum = 0.5f)
        {
            this.prestige = Mathf.Clamp01(prestige);
            this.virtue = Mathf.Clamp01(virtue);
            this.momentum = Mathf.Clamp01(momentum);
        }
    }

    /// <summary>声望モデルの調整係数（声望システム KORY-1 #1406）。</summary>
    public readonly struct PrestigeParams
    {
        /// <summary>陣営声望における徳望の重み（人を遇する度量の比重）。</summary>
        public readonly float virtueWeight;
        /// <summary>陣営声望における戦勝の重み（勝利の威光の比重）。</summary>
        public readonly float victoryWeight;
        /// <summary>陣営声望における人材の遇し方の重み（厚遇ぶりの比重）。合計＝1へ正規化する基準。</summary>
        public readonly float treatmentWeight;
        /// <summary>人材流入率の最大幅（声望ある陣営へ才人が集まる per dt の上限）。</summary>
        public readonly float influxRate;
        /// <summary>人材流出率の最大幅（声望が落ち冷遇すると才人が去る per dt の上限）。</summary>
        public readonly float exodusRate;
        /// <summary>厚遇が声望を押し上げる最大幅（人を活かす好循環の上限）。</summary>
        public readonly float treatmentPrestigeGain;
        /// <summary>出来事による声望変動の時定数（この時間で recentEvents へ追従＝大きいほどゆっくり）。</summary>
        public readonly float eventTime;
        /// <summary>勢いが磁力を増幅する最大ボーナス（趨勢満点で 1+これ 倍）。</summary>
        public readonly float momentumBoostMax;
        /// <summary>人材磁石と判定する磁力の既定閾値。</summary>
        public readonly float magnetThreshold;

        public PrestigeParams(float virtueWeight, float victoryWeight, float treatmentWeight,
            float influxRate, float exodusRate, float treatmentPrestigeGain,
            float eventTime, float momentumBoostMax, float magnetThreshold)
        {
            this.virtueWeight = Mathf.Max(0f, virtueWeight);
            this.victoryWeight = Mathf.Max(0f, victoryWeight);
            this.treatmentWeight = Mathf.Max(0f, treatmentWeight);
            this.influxRate = Mathf.Max(0f, influxRate);
            this.exodusRate = Mathf.Max(0f, exodusRate);
            this.treatmentPrestigeGain = Mathf.Max(0f, treatmentPrestigeGain);
            this.eventTime = eventTime;
            this.momentumBoostMax = Mathf.Max(0f, momentumBoostMax);
            this.magnetThreshold = Mathf.Clamp01(magnetThreshold);
        }

        /// <summary>
        /// 既定係数：声望＝徳望0.45/戦勝0.25/遇し方0.3（合計1＝徳望が最も重い）・
        /// 流入率0.3・流出率0.3・厚遇の声望ゲイン0.2・出来事の時定数60（戦略秒）・
        /// 勢いの磁力ブースト最大0.5（趨勢満点で1.5倍）・人材磁石閾値0.6。
        /// </summary>
        public static PrestigeParams Default => new PrestigeParams(
            0.45f, 0.25f, 0.3f, 0.3f, 0.3f, 0.2f, 60f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 声望モデルの純ロジック（声望システム KORY-1 #1406・項羽と劉邦＝「陣営の声望（人望・徳望）が人材を引き寄せる磁力になる。
    /// 劉邦は個人の武勇は項羽に劣ったが、人を遇する度量＝徳望によって韓信・張良・蕭何ら才人を集めた。
    /// 声望が高い陣営には有能な人材が自ら集まり、声望が落ちると人材が離れる（項羽から范増が去る）」）。
    /// 陣営声望(<see cref="FactionPrestige"/>)＝徳望×戦勝×人材の遇し方で生まれ、それが人材を引き寄せる磁力(<see cref="TalentMagnetism"/>)になる。
    /// 声望が高く競合より魅力的なほど人材が流入し(<see cref="TalentInflux"/>)、声望が落ち冷遇すると流出する(<see cref="TalentExodus"/>)。
    /// 人材を厚遇すれば声望が上がる好循環(<see cref="PrestigeFromTreatment"/>)・声望は出来事で上下し(<see cref="PrestigeTick"/>)・
    /// 勢いが磁力を増幅(<see cref="MomentumEffect"/>)・人材磁石の判定(<see cref="IsTalentMagnet"/>)。
    /// すべて決定論・乱数なし・基準値非破壊。
    /// 個人の武名（会戦の勝敗で増減する提督の名声）は <see cref="ReputationRules"/>、役割×役職の適材適所の効果倍率は PersonRules、
    /// 器量（同 EPIC KORY の人物の器の大きさ）は CapacityRules、賢者の能力本位登用が正統性を保つ尚賢は
    /// <see cref="CompetenceLegitimacyRules"/> が担う。本ルールは「陣営の声望が人材を引き寄せる磁力になる」陣営レベルのみを扱い、それらの並行系を作らない。
    /// </summary>
    public static class PrestigeRules
    {
        /// <summary>
        /// 陣営の声望＝徳望(virtue 0..1・人を遇する度量)・戦勝(victories 0..1)・人材の遇し方(treatmentOfTalent 0..1)の加重平均（0..1）。
        /// 重みは Default で徳望0.45/戦勝0.25/遇し方0.3＝武勇よりも人を大切にする度量が声望を生む（劉邦＝徳望で人を集める）。
        /// 合計重みで正規化するので比だけが効く。
        /// </summary>
        public static float FactionPrestige(float virtue, float victories, float treatmentOfTalent, PrestigeParams prm)
        {
            float v = Mathf.Clamp01(virtue);
            float w = Mathf.Clamp01(victories);
            float t = Mathf.Clamp01(treatmentOfTalent);
            float sum = prm.virtueWeight + prm.victoryWeight + prm.treatmentWeight;
            if (sum <= 0f) return 0f;
            float weighted = prm.virtueWeight * v + prm.victoryWeight * w + prm.treatmentWeight * t;
            return Mathf.Clamp01(weighted / sum);
        }

        public static float FactionPrestige(float virtue, float victories, float treatmentOfTalent)
            => FactionPrestige(virtue, victories, treatmentOfTalent, PrestigeParams.Default);

        /// <summary>
        /// 人材を引き寄せる磁力＝声望(factionPrestige 0..1)×活躍の場(opportunityOffered 0..1)（0..1）。
        /// 声望が高くても活躍の場が無ければ才人は来ず、場があっても声望が無ければ来ない（声望×機会の積＝韓信は劉邦の下で大将軍の場を得た）。
        /// </summary>
        public static float TalentMagnetism(float factionPrestige, float opportunityOffered)
        {
            float p = Mathf.Clamp01(factionPrestige);
            float o = Mathf.Clamp01(opportunityOffered);
            return Mathf.Clamp01(p * o);
        }

        /// <summary>
        /// 人材の流入量（per dt）。磁力(talentMagnetism)が高く、競合の声望(rivalPrestige 0..1)より魅力的なほど流入する：
        /// magnetism × (1−rivalPrestige) × influxRate × dt。競合が魅力的（rivalPrestige 高）ほど流入は鈍る（劉邦に人が集まる＝項羽より魅力で勝る分）。0以上。
        /// </summary>
        public static float TalentInflux(float talentMagnetism, float rivalPrestige, float dt, PrestigeParams prm)
        {
            float m = Mathf.Clamp01(talentMagnetism);
            float advantage = 1f - Mathf.Clamp01(rivalPrestige);
            float step = Mathf.Max(0f, dt);
            return Mathf.Max(0f, m * advantage * prm.influxRate * step);
        }

        public static float TalentInflux(float talentMagnetism, float rivalPrestige, float dt)
            => TalentInflux(talentMagnetism, rivalPrestige, dt, PrestigeParams.Default);

        /// <summary>
        /// 人材の流出量（per dt）。声望(factionPrestige 0..1)が低く、人材を冷遇(mistreat 0..1)するほど才人が去る：
        /// (1−factionPrestige) × mistreat × exodusRate × dt（項羽は声望を保てず范増を冷遇して去られた）。0以上。
        /// </summary>
        public static float TalentExodus(float factionPrestige, float mistreat, float dt, PrestigeParams prm)
        {
            float lowPrestige = 1f - Mathf.Clamp01(factionPrestige);
            float mt = Mathf.Clamp01(mistreat);
            float step = Mathf.Max(0f, dt);
            return Mathf.Max(0f, lowPrestige * mt * prm.exodusRate * step);
        }

        public static float TalentExodus(float factionPrestige, float mistreat, float dt)
            => TalentExodus(factionPrestige, mistreat, dt, PrestigeParams.Default);

        /// <summary>
        /// 人材を厚遇すると声望が上がる好循環の増分。人材の定着(talentRetention 0..1)と度量・寛大さ(generosity 0..1)の積に比例：
        /// talentRetention × generosity × treatmentPrestigeGain（人を活かすと評判が立ち、さらに人が集まる）。0以上。
        /// </summary>
        public static float PrestigeFromTreatment(float talentRetention, float generosity, PrestigeParams prm)
        {
            float r = Mathf.Clamp01(talentRetention);
            float g = Mathf.Clamp01(generosity);
            return Mathf.Max(0f, r * g * prm.treatmentPrestigeGain);
        }

        public static float PrestigeFromTreatment(float talentRetention, float generosity)
            => PrestigeFromTreatment(talentRetention, generosity, PrestigeParams.Default);

        /// <summary>
        /// 声望が出来事で上下する（勝利・寛大な処遇は上げ、裏切り・冷遇は下げる）。recentEvents(0..1・0.5が中立)へ
        /// eventTime の時定数で漸近する。recentEvents&gt;0.5 で上昇、&lt;0.5 で下降。返り値は更新後の声望（0..1）。
        /// </summary>
        public static float PrestigeTick(float prestige, float recentEvents, float dt, PrestigeParams prm)
        {
            float p = Mathf.Clamp01(prestige);
            float target = Mathf.Clamp01(recentEvents);
            float step = Mathf.Max(0f, dt);
            if (prm.eventTime <= 0f) return target;
            float t = Mathf.Clamp01(step / prm.eventTime);
            return Mathf.Clamp01(Mathf.Lerp(p, target, t));
        }

        public static float PrestigeTick(float prestige, float recentEvents, float dt)
            => PrestigeTick(prestige, recentEvents, dt, PrestigeParams.Default);

        /// <summary>
        /// 勢いに乗る陣営はさらに人材を集める＝磁力（声望由来）に趨勢ブーストを掛ける（0..1）。
        /// 声望(prestige 0..1)を基礎の磁力として、勢い(momentum 0..1)で 1..(1+momentumBoostMax) 倍へ増幅する（勝ち馬に乗る＝趨勢が磁力を増す）。
        /// </summary>
        public static float MomentumEffect(float momentum, float prestige, PrestigeParams prm)
        {
            float p = Mathf.Clamp01(prestige);
            float mo = Mathf.Clamp01(momentum);
            float boost = 1f + mo * prm.momentumBoostMax;
            return Mathf.Clamp01(p * boost);
        }

        public static float MomentumEffect(float momentum, float prestige)
            => MomentumEffect(momentum, prestige, PrestigeParams.Default);

        /// <summary>
        /// 人材が自ら集まる声望ある陣営か（磁力が閾値以上か）。threshold が負なら既定値で判定。
        /// 才人が自発的に身を寄せる「人材磁石」＝劉邦陣営の判定。
        /// </summary>
        public static bool IsTalentMagnet(float talentMagnetism, float threshold, PrestigeParams prm)
        {
            float m = Mathf.Clamp01(talentMagnetism);
            float th = threshold < 0f ? prm.magnetThreshold : Mathf.Clamp01(threshold);
            return m >= th;
        }

        public static bool IsTalentMagnet(float talentMagnetism, float threshold)
            => IsTalentMagnet(talentMagnetism, threshold, PrestigeParams.Default);
    }
}
