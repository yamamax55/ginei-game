using UnityEngine;

namespace Ginei
{
    /// <summary>政治的粛清・恐怖統治の調整係数。</summary>
    public readonly struct PoliticalPurgeParams
    {
        /// <summary>認識された脅威が粛清範囲へ寄与する重み。</summary>
        public readonly float threatWeight;
        /// <summary>猜疑心が粛清範囲を増幅する重み（疑い深いほど範囲が膨らむ）。</summary>
        public readonly float paranoiaWeight;
        /// <summary>見せしめの可視性が威圧を増幅する重み。</summary>
        public readonly float visibilityWeight;
        /// <summary>対象の有能さが人材枯渇を増幅する重み（有能な者を粛清する害）。</summary>
        public readonly float competenceWeight;
        /// <summary>密告報奨が密告誘因を増幅する重み。</summary>
        public readonly float rewardWeight;
        /// <summary>威圧→表面的服従の効き（飽和の鋭さ）。</summary>
        public readonly float complianceSharpness;
        /// <summary>過剰な粛清が内心の離反を生む重み（恐怖は面従腹背を生む）。</summary>
        public readonly float defectionWeight;
        /// <summary>冤罪率が自家中毒を増幅する重み（疑心暗鬼の連鎖）。</summary>
        public readonly float falseAccusationWeight;

        public PoliticalPurgeParams(float threatWeight, float paranoiaWeight, float visibilityWeight,
            float competenceWeight, float rewardWeight, float complianceSharpness,
            float defectionWeight, float falseAccusationWeight)
        {
            this.threatWeight = Mathf.Clamp01(threatWeight);
            this.paranoiaWeight = Mathf.Clamp01(paranoiaWeight);
            this.visibilityWeight = Mathf.Clamp01(visibilityWeight);
            this.competenceWeight = Mathf.Clamp01(competenceWeight);
            this.rewardWeight = Mathf.Clamp01(rewardWeight);
            this.complianceSharpness = Mathf.Max(0f, complianceSharpness);
            this.defectionWeight = Mathf.Clamp01(defectionWeight);
            this.falseAccusationWeight = Mathf.Clamp01(falseAccusationWeight);
        }

        /// <summary>既定＝脅威重み0.5・猜疑重み0.5・可視性重み0.8・有能重み0.7・報奨重み0.6・服従鋭さ1.5・離反重み0.8・冤罪重み0.7。</summary>
        public static PoliticalPurgeParams Default =>
            new PoliticalPurgeParams(0.5f, 0.5f, 0.8f, 0.7f, 0.6f, 1.5f, 0.8f, 0.7f);
    }

    /// <summary>
    /// 政治的粛清・恐怖統治の純ロジック（スターリン型／粛清の力学）。政権が政敵を排除し
    /// 恐怖で統べる過程を扱う＝認識された脅威と猜疑心が粛清の範囲を決め、見せしめの可視性が
    /// 威圧を生む。だが有能な者を粛清すれば人材が枯渇し、密告が奨励され、恐怖は表面的服従の
    /// 裏で内心の離反（面従腹背）を育てる。範囲と冤罪が連鎖すれば疑心暗鬼の自家中毒に陥る。
    /// 「恐怖は服従を買えても忠誠は買えない」「粛清は粛清を呼ぶ」を式に出す。
    /// 倍率/指標は各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PoliticalPurgeRules
    {
        /// <summary>
        /// 粛清の範囲（0..1）＝認識された脅威×脅威重み＋猜疑心×猜疑重み。脅威を感じ猜疑が
        /// 深いほど範囲が広がる。疑わしきは皆斬る＝両者が重なると最大化する。
        /// </summary>
        public static float PurgeScope(float perceivedThreat, float paranoia, PoliticalPurgeParams p)
        {
            float threat = Mathf.Clamp01(perceivedThreat);
            float para = Mathf.Clamp01(paranoia);
            float scope = threat * p.threatWeight + para * p.paranoiaWeight;
            return Mathf.Clamp01(scope);
        }

        public static float PurgeScope(float perceivedThreat, float paranoia)
            => PurgeScope(perceivedThreat, paranoia, PoliticalPurgeParams.Default);

        /// <summary>
        /// 威圧効果（0..1）＝粛清範囲×（見せしめの可視性×可視性重み）。粛清が広く、かつ
        /// 公開処刑のように見せつけるほど恐怖が広がる。秘密の粛清は威圧効果が薄い。
        /// </summary>
        public static float Intimidation(float purgeScope, float publicVisibility, PoliticalPurgeParams p)
        {
            float scope = Mathf.Clamp01(purgeScope);
            float vis = Mathf.Clamp01(publicVisibility);
            float visFactor = vis * p.visibilityWeight;
            return Mathf.Clamp01(scope * visFactor);
        }

        public static float Intimidation(float purgeScope, float publicVisibility)
            => Intimidation(purgeScope, publicVisibility, PoliticalPurgeParams.Default);

        /// <summary>
        /// 人材の枯渇（0..1）＝粛清範囲×（対象の有能さ×有能重み）。有能な者ほど粛清すれば
        /// 失われる損失が大きい＝広く粛清し、かつ斬る相手が有能なほど国の頭脳が枯れる。
        /// </summary>
        public static float TalentDrain(float purgeScope, float targetCompetence, PoliticalPurgeParams p)
        {
            float scope = Mathf.Clamp01(purgeScope);
            float comp = Mathf.Clamp01(targetCompetence);
            float compFactor = comp * p.competenceWeight;
            return Mathf.Clamp01(scope * compFactor);
        }

        public static float TalentDrain(float purgeScope, float targetCompetence)
            => TalentDrain(purgeScope, targetCompetence, PoliticalPurgeParams.Default);

        /// <summary>
        /// 密告の誘因（0..1）＝威圧×（密告報奨×報奨重み）。恐怖の下で密告に報いれば、
        /// 保身と利得から人々が互いを告発する＝威圧と報奨が揃うと密告社会化する。
        /// </summary>
        public static float DenunciationIncentive(float intimidation, float rewardForInforming, PoliticalPurgeParams p)
        {
            float intim = Mathf.Clamp01(intimidation);
            float reward = Mathf.Clamp01(rewardForInforming);
            float rewardFactor = reward * p.rewardWeight;
            return Mathf.Clamp01(intim * rewardFactor);
        }

        public static float DenunciationIncentive(float intimidation, float rewardForInforming)
            => DenunciationIncentive(intimidation, rewardForInforming, PoliticalPurgeParams.Default);

        /// <summary>
        /// 表面的服従（0..1）＝威圧に応じて飽和的に高まる。恐怖が強いほど人々は逆らわず従う
        /// （ただし内心は別＝<see cref="InwardDefection"/>）。鋭さが高いほど低威圧でも早く服従へ。
        /// </summary>
        public static float SurfaceCompliance(float intimidation, PoliticalPurgeParams p)
        {
            float intim = Mathf.Clamp01(intimidation);
            // 飽和曲線（Log/Exp 禁止のため冪で代用）：鋭さが高いほど早く1へ近づく
            float exponent = 1f / (1f + p.complianceSharpness);
            float compliance = Mathf.Pow(intim, exponent);
            return Mathf.Clamp01(compliance);
        }

        public static float SurfaceCompliance(float intimidation)
            => SurfaceCompliance(intimidation, PoliticalPurgeParams.Default);

        /// <summary>
        /// 内心の離反（0..1）＝（粛清範囲＋人材枯渇）×離反重み。過剰な粛清と有能な仲間の喪失が
        /// 恨みを募らせる＝恐怖は表面の服従の裏で面従腹背（内心の離反）を育てる。
        /// </summary>
        public static float InwardDefection(float purgeScope, float talentDrain, PoliticalPurgeParams p)
        {
            float scope = Mathf.Clamp01(purgeScope);
            float drain = Mathf.Clamp01(talentDrain);
            // 範囲と枯渇の平均を離反重みで効かせる（過剰なほど離反が深まる）
            float pressure = (scope + drain) * 0.5f;
            return Mathf.Clamp01(pressure * p.defectionWeight);
        }

        public static float InwardDefection(float purgeScope, float talentDrain)
            => InwardDefection(purgeScope, talentDrain, PoliticalPurgeParams.Default);

        /// <summary>
        /// 粛清の自家中毒（0..1）＝粛清範囲×（冤罪率×冤罪重み）。広い粛清に冤罪が混じると
        /// 疑念が疑念を呼び、無実の告発が連鎖して内部崩壊する＝粛清が粛清を呼ぶ自家中毒。
        /// </summary>
        public static float PurgeSelfPoisoning(float purgeScope, float falseAccusationRate, PoliticalPurgeParams p)
        {
            float scope = Mathf.Clamp01(purgeScope);
            float falseRate = Mathf.Clamp01(falseAccusationRate);
            float falseFactor = falseRate * p.falseAccusationWeight;
            return Mathf.Clamp01(scope * falseFactor);
        }

        public static float PurgeSelfPoisoning(float purgeScope, float falseAccusationRate)
            => PurgeSelfPoisoning(purgeScope, falseAccusationRate, PoliticalPurgeParams.Default);

        /// <summary>
        /// 粛清で体制が安定したか（bool）＝表面的服従が内心の離反を閾値ぶん上回るか。
        /// 服従が離反を十分に超えれば（恐怖が勝てば）表面上は安定する。離反が拮抗/上回れば
        /// 面従腹背が体制を蝕み安定とは言えない。
        /// </summary>
        public static bool IsRegimeStabilized(float surfaceCompliance, float inwardDefection, float threshold)
        {
            float compliance = Mathf.Clamp01(surfaceCompliance);
            float defection = Mathf.Clamp01(inwardDefection);
            float margin = Mathf.Clamp01(threshold);
            return compliance - defection >= margin;
        }

        /// <summary>既定閾値0.2＝服従が離反を2割ぶん上回れば安定とみなす。</summary>
        public static bool IsRegimeStabilized(float surfaceCompliance, float inwardDefection)
            => IsRegimeStabilized(surfaceCompliance, inwardDefection, 0.2f);
    }
}
