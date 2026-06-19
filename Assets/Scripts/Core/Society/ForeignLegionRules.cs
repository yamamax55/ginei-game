using UnityEngine;

namespace Ginei
{
    /// <summary>外人部隊（外国人志願兵の精鋭部隊）の調整係数。</summary>
    public readonly struct ForeignLegionParams
    {
        /// <summary>結束における共通目的の重み（残りは苦楽を共にへ）。</summary>
        public readonly float purposeWeight;
        /// <summary>精鋭化における過酷訓練の重み（残りは選抜の厳しさへ）。幾何平均の偏り。</summary>
        public readonly float harshnessWeight;
        /// <summary>消耗品的投入における任務の危険度の重み（残りは精鋭度そのもの）。</summary>
        public readonly float riskWeight;
        /// <summary>募集源における窮状の重み（残りは難民の規模）。</summary>
        public readonly float desperationWeight;
        /// <summary>名誉における戦歴の重み（残りは団結精神）。</summary>
        public readonly float combatWeight;
        /// <summary>総合価値で耐性を効かせる強さ（耐性のフロア＝最低保証）。</summary>
        public readonly float resilienceFloor;

        public ForeignLegionParams(float purposeWeight, float harshnessWeight, float riskWeight,
            float desperationWeight, float combatWeight, float resilienceFloor)
        {
            this.purposeWeight = Mathf.Clamp01(purposeWeight);
            this.harshnessWeight = Mathf.Clamp01(harshnessWeight);
            this.riskWeight = Mathf.Clamp01(riskWeight);
            this.desperationWeight = Mathf.Clamp01(desperationWeight);
            this.combatWeight = Mathf.Clamp01(combatWeight);
            this.resilienceFloor = Mathf.Clamp01(resilienceFloor);
        }

        /// <summary>
        /// 既定＝目的重み0.6・過酷重み0.5・危険重み0.5・窮状重み0.5・戦歴重み0.5・耐性フロア0.25。
        /// 結束は共通目的をやや重く、精鋭化は過酷さと選抜を半々（幾何平均）、名誉は戦歴と団結を半々。
        /// </summary>
        public static ForeignLegionParams Default => new ForeignLegionParams(0.6f, 0.5f, 0.5f, 0.5f, 0.5f, 0.25f);
    }

    /// <summary>
    /// 外人部隊（外国人志願兵で構成される常備の精鋭部隊）の純ロジック。出自も言語も異なる多国籍兵を、
    /// <b>共通の目的と苦楽を共にすること</b>で束ね、<b>過酷な訓練と厳しい選抜</b>で精鋭へ練成する。祖国を
    /// 捨てた者ゆえ<b>他に居場所がないほど部隊への帰属（新たな忠誠）が深まり</b>、その精鋭は<b>消耗品（捨て駒）
    /// として危険な任務へ投入できる</b>。亡命者・無国籍者・難民が窮状ゆえに志願する＝<b>受け皿としての募集源</b>。
    /// 戦歴と団結精神が<b>部隊の名誉</b>を育て、帰属と名誉が<b>損耗への耐性（部隊のために死ねる）</b>を生む。
    /// すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ForeignLegionRules
    {
        /// <summary>
        /// 多国籍兵の結束（0..1）＝共通の目的×重み ＋ 苦楽を共に×(1−重み)。出自がばらばらでも、共通の目的を
        /// 掲げ苦楽を共にするほど一つの部隊になる。どちらか欠ければ結束は伸びない（加重和）。
        /// </summary>
        public static float MultinationalCohesion(float commonPurpose, float sharedHardship, ForeignLegionParams p)
        {
            float purpose = Mathf.Clamp01(commonPurpose);
            float hardship = Mathf.Clamp01(sharedHardship);
            return Mathf.Clamp01(purpose * p.purposeWeight + hardship * (1f - p.purposeWeight));
        }

        public static float MultinationalCohesion(float commonPurpose, float sharedHardship)
            => MultinationalCohesion(commonPurpose, sharedHardship, ForeignLegionParams.Default);

        /// <summary>
        /// 精鋭化（0..1）＝訓練の過酷さ×選抜の厳しさの幾何平均（重みで偏らせる）。過酷な訓練と厳しい選抜の
        /// <b>両方</b>が揃って初めて精鋭になる＝どちらか0なら精鋭化は0（鍛えるだけでも選ぶだけでも足りない）。
        /// </summary>
        public static float EliteForging(float trainingHarshness, float selectionRigor, ForeignLegionParams p)
        {
            float harsh = Mathf.Clamp01(trainingHarshness);
            float rigor = Mathf.Clamp01(selectionRigor);
            // 重み付き幾何平均＝harsh^w × rigor^(1-w)
            float forged = Mathf.Pow(harsh, p.harshnessWeight) * Mathf.Pow(rigor, 1f - p.harshnessWeight);
            return Mathf.Clamp01(forged);
        }

        public static float EliteForging(float trainingHarshness, float selectionRigor)
            => EliteForging(trainingHarshness, selectionRigor, ForeignLegionParams.Default);

        /// <summary>
        /// 部隊への帰属（0..1）＝結束×(1−他の居場所)。祖国を捨てた者ほど他に帰る所がなく、部隊そのものが
        /// 新たな祖国になる＝<b>居場所のなさが忠誠を深める</b>。帰る家がある者（alternativeHome=1）は帰属0。
        /// </summary>
        public static float UnitBelonging(float multinationalCohesion, float alternativeHome, ForeignLegionParams p)
        {
            float cohesion = Mathf.Clamp01(multinationalCohesion);
            float home = Mathf.Clamp01(alternativeHome);
            return Mathf.Clamp01(cohesion * (1f - home));
        }

        public static float UnitBelonging(float multinationalCohesion, float alternativeHome)
            => UnitBelonging(multinationalCohesion, alternativeHome, ForeignLegionParams.Default);

        /// <summary>
        /// 消耗品的投入の適性（0..1）＝精鋭度×重み ＋ 任務の危険度×重み の加重和。精鋭であるほど、また
        /// 危険な任務であるほど<b>捨て駒として投入する価値</b>が高い（高い練度の部隊を最も危ない局面へ送れる）。
        /// </summary>
        public static float ExpendableDeployment(float eliteForging, float missionRisk, ForeignLegionParams p)
        {
            float elite = Mathf.Clamp01(eliteForging);
            float risk = Mathf.Clamp01(missionRisk);
            return Mathf.Clamp01(elite * (1f - p.riskWeight) + risk * p.riskWeight);
        }

        public static float ExpendableDeployment(float eliteForging, float missionRisk)
            => ExpendableDeployment(eliteForging, missionRisk, ForeignLegionParams.Default);

        /// <summary>
        /// 亡命者の募集源（0..1）＝難民の規模×重み ＋ 窮状×重み の加重和。流民が多く、かつ窮しているほど
        /// <b>外人部隊への志願者の池</b>が大きい＝祖国を失った者の受け皿。どちらも欠ければ募集源は痩せる。
        /// </summary>
        public static float RefugeeRecruitPool(float displacedPopulation, float desperation, ForeignLegionParams p)
        {
            float displaced = Mathf.Clamp01(displacedPopulation);
            float desp = Mathf.Clamp01(desperation);
            return Mathf.Clamp01(displaced * (1f - p.desperationWeight) + desp * p.desperationWeight);
        }

        public static float RefugeeRecruitPool(float displacedPopulation, float desperation)
            => RefugeeRecruitPool(displacedPopulation, desperation, ForeignLegionParams.Default);

        /// <summary>
        /// 部隊の名誉（0..1）＝戦歴×重み ＋ 団結精神×重み の加重和。輝かしい戦歴と強い団結精神（エスプリ・
        /// ド・コール）が<b>部隊の誇り</b>を育てる＝外人部隊が祖国の代わりに掲げる旗。
        /// </summary>
        public static float UnitHonor(float combatRecord, float espritDeCorps, ForeignLegionParams p)
        {
            float record = Mathf.Clamp01(combatRecord);
            float esprit = Mathf.Clamp01(espritDeCorps);
            return Mathf.Clamp01(record * p.combatWeight + esprit * (1f - p.combatWeight));
        }

        public static float UnitHonor(float combatRecord, float espritDeCorps)
            => UnitHonor(combatRecord, espritDeCorps, ForeignLegionParams.Default);

        /// <summary>
        /// 損耗への耐性（0..1）＝帰属×名誉の幾何平均。部隊に帰属し、部隊の名誉を背負う者は<b>部隊のために
        /// 死ねる</b>＝大損害でも崩れない。どちらか欠ければ（帰属だけ・名誉だけ）耐性は崩れる（幾何平均）。
        /// </summary>
        public static float AttritionResilience(float unitBelonging, float unitHonor, ForeignLegionParams p)
        {
            float belonging = Mathf.Clamp01(unitBelonging);
            float honor = Mathf.Clamp01(unitHonor);
            return Mathf.Clamp01(Mathf.Sqrt(belonging * honor));
        }

        public static float AttritionResilience(float unitBelonging, float unitHonor)
            => AttritionResilience(unitBelonging, unitHonor, ForeignLegionParams.Default);

        /// <summary>
        /// 外人部隊の総合価値（0..1）＝精鋭度×帰属×耐性。精鋭で、部隊に帰属し、損耗に耐える＝三拍子が
        /// 揃って初めて価値ある常備精鋭になる。耐性はフロア（最低保証）を効かせ、新設で耐性が浅くても
        /// 精鋭度と帰属がゼロにしない＝<b>練成と忠誠が先に立つ</b>。三つの積（どれか0なら価値も0に近づく）。
        /// </summary>
        public static float LegionValue(float eliteForging, float unitBelonging, float attritionResilience,
            ForeignLegionParams p)
        {
            float elite = Mathf.Clamp01(eliteForging);
            float belonging = Mathf.Clamp01(unitBelonging);
            float resil = Mathf.Clamp01(attritionResilience);
            // 耐性はフロアで底上げ＝浅い耐性でも価値を潰しすぎない
            float effResil = Mathf.Lerp(p.resilienceFloor, 1f, resil);
            return Mathf.Clamp01(elite * belonging * effResil);
        }

        public static float LegionValue(float eliteForging, float unitBelonging, float attritionResilience)
            => LegionValue(eliteForging, unitBelonging, attritionResilience, ForeignLegionParams.Default);
    }
}
