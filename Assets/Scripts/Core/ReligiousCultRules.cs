using UnityEngine;

namespace Ginei
{
    /// <summary>地球教（Church of Terra）型カルトの調整係数。すべて ctor でクランプ。</summary>
    public readonly struct ReligiousCultParams
    {
        /// <summary>教義魅力における救済の約束の重み（0..1）。</summary>
        public readonly float promiseWeight;
        /// <summary>教義魅力における社会不安の下駄（0..1。不安0でもこの割合だけ魅力が残る＝不安1で満額）。</summary>
        public readonly float anxietyFloor;
        /// <summary>信徒獲得のスケール（魅力×布教努力の幾何平均にこれを掛ける）。</summary>
        public readonly float conversionScale;
        /// <summary>信徒1人あたりの献金（献金収入＝信徒数×信仰×これ。0..∞）。</summary>
        public readonly float tithePerConvert;
        /// <summary>献金を工作余力へ換算する係数（資金1あたりの工作潜在力。小さい＝資金は嵩む）。</summary>
        public readonly float titheToInfluence;
        /// <summary>秘密工作における工作員の下駄（0..1。工作員0でもこの割合だけ工作が通る）。</summary>
        public readonly float agentFloor;
        /// <summary>隠然たる影響力における工作の重み（0..1。残りが動員の重み）。</summary>
        public readonly float infiltrationWeight;
        /// <summary>世俗対立における当局警戒の下駄（0..1。警戒0でもこの割合だけ摩擦が出る）。</summary>
        public readonly float vigilanceFloor;
        /// <summary>実効権力における対立コスト係数（影響力から対立×これを引く）。</summary>
        public readonly float conflictCost;

        public ReligiousCultParams(float promiseWeight, float anxietyFloor, float conversionScale,
            float tithePerConvert, float titheToInfluence, float agentFloor,
            float infiltrationWeight, float vigilanceFloor, float conflictCost)
        {
            this.promiseWeight = Mathf.Clamp01(promiseWeight);
            this.anxietyFloor = Mathf.Clamp01(anxietyFloor);
            this.conversionScale = Mathf.Max(0f, conversionScale);
            this.tithePerConvert = Mathf.Max(0f, tithePerConvert);
            this.titheToInfluence = Mathf.Max(0f, titheToInfluence);
            this.agentFloor = Mathf.Clamp01(agentFloor);
            this.infiltrationWeight = Mathf.Clamp01(infiltrationWeight);
            this.vigilanceFloor = Mathf.Clamp01(vigilanceFloor);
            this.conflictCost = Mathf.Max(0f, conflictCost);
        }

        /// <summary>既定＝約束重み0.6・不安下駄0.5・獲得スケール1.0・献金1.0・資金換算0.01・工作員下駄0.3・工作重み0.6・警戒下駄0.2・対立コスト0.8。</summary>
        public static ReligiousCultParams Default
            => new ReligiousCultParams(0.6f, 0.5f, 1f, 1f, 0.01f, 0.3f, 0.6f, 0.2f, 0.8f);
    }

    /// <summary>
    /// 地球教（Church of Terra）型カルト宗教の純ロジック（秘密教団の信徒獲得と政治介入）。
    /// 救済の約束×社会不安で教義が魅力を持ち（不安な時代ほど効く）、魅力×布教で信徒を獲得し、
    /// 信徒数×信仰で献金が集まり、資金×工作員で政治へ秘密工作を仕掛け、信仰×過激さで狂信者を動員し、
    /// 工作×動員で隠然たる影響力を持つが、影響力は当局の警戒を招いて世俗権力と対立し、その対立コストが
    /// 実効権力を削る（影響力−対立＝-1..1）。乱数なし・決定論・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReligiousCultRules
    {
        /// <summary>
        /// 教義の魅力（0..1）＝救済の約束(0..1)×約束重み×不安の下駄。社会不安が高いほど魅力が満額に近づく
        /// （不安な時代ほど効く）。不安0でも anxietyFloor ぶんは残る。
        /// </summary>
        public static float DoctrineAppeal(float spiritualPromise, float socialAnxiety, ReligiousCultParams p)
        {
            float promise = Mathf.Clamp01(spiritualPromise);
            float anxiety = Mathf.Clamp01(socialAnxiety);
            float anxietyFactor = p.anxietyFloor + (1f - p.anxietyFloor) * anxiety;
            return Mathf.Clamp01(promise * p.promiseWeight * anxietyFactor);
        }

        public static float DoctrineAppeal(float spiritualPromise, float socialAnxiety)
            => DoctrineAppeal(spiritualPromise, socialAnxiety, ReligiousCultParams.Default);

        /// <summary>
        /// 信徒の獲得（0..1+）＝魅力×布教努力の幾何平均×獲得スケール。魅力か努力のどちらかが薄いと崩れる
        /// （両方そろって初めて広まる）。
        /// </summary>
        public static float ConvertAcquisition(float doctrineAppeal, float missionaryEffort, ReligiousCultParams p)
        {
            float appeal = Mathf.Clamp01(doctrineAppeal);
            float effort = Mathf.Clamp01(missionaryEffort);
            return Mathf.Sqrt(appeal * effort) * p.conversionScale;
        }

        public static float ConvertAcquisition(float doctrineAppeal, float missionaryEffort)
            => ConvertAcquisition(doctrineAppeal, missionaryEffort, ReligiousCultParams.Default);

        /// <summary>
        /// 献金収入（0..∞）＝信徒数×信仰の篤さ(0..1)×1人あたり献金。篤い信者が多いほど資金が太る。
        /// </summary>
        public static float TitheRevenue(float convertCount, float devotionLevel, ReligiousCultParams p)
        {
            float converts = Mathf.Max(0f, convertCount);
            float devotion = Mathf.Clamp01(devotionLevel);
            return converts * devotion * p.tithePerConvert;
        }

        public static float TitheRevenue(float convertCount, float devotionLevel)
            => TitheRevenue(convertCount, devotionLevel, ReligiousCultParams.Default);

        /// <summary>
        /// 政治への秘密工作（0..1）＝資金力(資金×換算で0..1へ飽和)×工作員の下駄。
        /// 資金と工作員の両輪で要人へ浸透する（資金だけ・人だけでは通らない）。
        /// </summary>
        public static float PoliticalInfiltration(float titheRevenue, float secretAgents, ReligiousCultParams p)
        {
            float funds = Mathf.Clamp01(Mathf.Max(0f, titheRevenue) * p.titheToInfluence);
            float agents = Mathf.Clamp01(secretAgents);
            float agentFactor = p.agentFloor + (1f - p.agentFloor) * agents;
            return Mathf.Clamp01(funds * agentFactor);
        }

        public static float PoliticalInfiltration(float titheRevenue, float secretAgents)
            => PoliticalInfiltration(titheRevenue, secretAgents, ReligiousCultParams.Default);

        /// <summary>
        /// 狂信者の動員（0..1）＝信仰の篤さ×教義の過激さの幾何平均。篤く・過激なほど身を捧げる手駒になる。
        /// </summary>
        public static float FanaticMobilization(float devotionLevel, float doctrinalRadicalism, ReligiousCultParams p)
        {
            float devotion = Mathf.Clamp01(devotionLevel);
            float radicalism = Mathf.Clamp01(doctrinalRadicalism);
            return Mathf.Sqrt(devotion * radicalism);
        }

        public static float FanaticMobilization(float devotionLevel, float doctrinalRadicalism)
            => FanaticMobilization(devotionLevel, doctrinalRadicalism, ReligiousCultParams.Default);

        /// <summary>
        /// 隠然たる影響力（0..1）＝秘密工作×工作重み＋狂信者動員×(1-工作重み)。
        /// 上層への浸透と下からの動員を併せ持つほど社会を隠然と動かす。
        /// </summary>
        public static float HiddenInfluence(float politicalInfiltration, float fanaticMobilization, ReligiousCultParams p)
        {
            float infiltration = Mathf.Clamp01(politicalInfiltration);
            float mobilization = Mathf.Clamp01(fanaticMobilization);
            return Mathf.Clamp01(infiltration * p.infiltrationWeight + mobilization * (1f - p.infiltrationWeight));
        }

        public static float HiddenInfluence(float politicalInfiltration, float fanaticMobilization)
            => HiddenInfluence(politicalInfiltration, fanaticMobilization, ReligiousCultParams.Default);

        /// <summary>
        /// 世俗権力との対立（0..1）＝隠然たる影響力×当局の警戒の下駄。影響力が露見し当局が警戒するほど摩擦が増す
        /// （警戒0でも vigilanceFloor ぶんは敵視される）。
        /// </summary>
        public static float SecularConflict(float hiddenInfluence, float stateVigilance, ReligiousCultParams p)
        {
            float influence = Mathf.Clamp01(hiddenInfluence);
            float vigilance = Mathf.Clamp01(stateVigilance);
            float vigilanceFactor = p.vigilanceFloor + (1f - p.vigilanceFloor) * vigilance;
            return Mathf.Clamp01(influence * vigilanceFactor);
        }

        public static float SecularConflict(float hiddenInfluence, float stateVigilance)
            => SecularConflict(hiddenInfluence, stateVigilance, ReligiousCultParams.Default);

        /// <summary>
        /// 教団の実効権力（-1..1）＝隠然たる影響力−対立コスト×世俗対立。
        /// 影響力が大きくても当局と激突すれば実効権力は目減りし、対立が嵩めば負（弾圧されて潰える）。
        /// </summary>
        public static float CultPower(float hiddenInfluence, float secularConflict, ReligiousCultParams p)
        {
            float influence = Mathf.Clamp01(hiddenInfluence);
            float conflict = Mathf.Clamp01(secularConflict);
            return Mathf.Clamp(influence - p.conflictCost * conflict, -1f, 1f);
        }

        public static float CultPower(float hiddenInfluence, float secularConflict)
            => CultPower(hiddenInfluence, secularConflict, ReligiousCultParams.Default);
    }
}
