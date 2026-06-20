using UnityEngine;

namespace Ginei
{
    /// <summary>終末思想運動（黙示録的カルト）の調整係数。終末予言の訴求・危機での信徒急増・過激な備え・千年王国の約束・予言外れの不協和・急進化・集団自決の力学を律する。</summary>
    public readonly struct ApocalypticMovementParams
    {
        /// <summary>予言訴求の鋭さ（大きいほど危機と鮮烈さの相乗が強い）。</summary>
        public readonly float appealSharpness;
        /// <summary>絶望の動員力（社会的絶望が信徒急増へ寄与する強さ）。</summary>
        public readonly float despairPull;
        /// <summary>備えの過激化スケール（信徒×切迫が過激行動へ転じる強さ）。</summary>
        public readonly float preparationScale;
        /// <summary>救済幻のスケール（救済の幻×現世の苦が約束の引力になる強さ）。</summary>
        public readonly float promiseScale;
        /// <summary>不協和の増幅（予言外れ×傾倒が認知的不協和を生む強さ）。</summary>
        public readonly float dissonanceGain;
        /// <summary>カリスマの急進化増幅（不協和をカリスマが信仰深化＝急進化へ転化する強さ）。</summary>
        public readonly float charismaGain;
        /// <summary>孤立の危険増幅（急進化×社会孤立が集団自決の危険を高める強さ）。</summary>
        public readonly float isolationGain;

        public ApocalypticMovementParams(float appealSharpness, float despairPull, float preparationScale,
                                         float promiseScale, float dissonanceGain, float charismaGain,
                                         float isolationGain)
        {
            this.appealSharpness = Mathf.Clamp(appealSharpness, 0.5f, 2f);
            this.despairPull = Mathf.Clamp01(despairPull);
            this.preparationScale = Mathf.Clamp01(preparationScale);
            this.promiseScale = Mathf.Clamp01(promiseScale);
            this.dissonanceGain = Mathf.Clamp01(dissonanceGain);
            this.charismaGain = Mathf.Clamp01(charismaGain);
            this.isolationGain = Mathf.Clamp01(isolationGain);
        }

        /// <summary>既定＝訴求鋭さ1.0・絶望動員0.7・備え0.8・約束0.9・不協和0.8・カリスマ0.7・孤立0.85。</summary>
        public static ApocalypticMovementParams Default =>
            new ApocalypticMovementParams(1f, 0.7f, 0.8f, 0.9f, 0.8f, 0.7f, 0.85f);
    }

    /// <summary>
    /// 終末思想運動（黙示録的カルト）の純ロジック。終末予言の訴求 → 危機の時代の信徒急増 → 終末への備え（過激行動）、
    /// 千年王国の約束（苦しいほど効く）、予言が外れた不協和 → カリスマが信仰を深める逆説の急進化 → 社会からの孤立が
    /// 集団自決の危険を高める。すべて 0..1 の正規化スカラを返す決定論的計算（乱数なし・盤面非依存の plain 引数・実効値パターン）。
    /// <para>
    /// 史実モチーフ：終末論カルト・ミレニアリズム（千年王国）・予言不成就と認知的不協和（フェスティンガー）・人民寺院/天国の門型の集団自決。
    /// </para>
    /// 純ロジック（非 MonoBehaviour・test-first）。LINQ/Log/Exp 不使用。
    /// </summary>
    public static class ApocalypticMovementRules
    {
        /// <summary>
        /// 終末予言の訴求（0..1）＝危機の深刻さ crisisSeverity(0..1)×予言の鮮烈さ prophecyVividness(0..1) を
        /// 訴求鋭さ appealSharpness で増幅（Pow）。危機が深く予言が鮮烈なほど人を引く。
        /// </summary>
        public static float PropheticAppeal(float crisisSeverity, float prophecyVividness, ApocalypticMovementParams p)
        {
            float crisis = Mathf.Clamp01(crisisSeverity);
            float vivid = Mathf.Clamp01(prophecyVividness);
            float raw = crisis * vivid;
            return Mathf.Clamp01(Mathf.Pow(raw, 1f / p.appealSharpness));
        }

        public static float PropheticAppeal(float crisisSeverity, float prophecyVividness)
            => PropheticAppeal(crisisSeverity, prophecyVividness, ApocalypticMovementParams.Default);

        /// <summary>
        /// 信徒の急増（0..1）＝予言訴求 propheticAppeal(0..1) に社会的絶望 socialDespair(0..1) が動員力として乗る
        /// ＝訴求 ×(1 + 絶望×despairPull) を 1 でクランプ。危機と絶望の時代に信徒が増える。
        /// </summary>
        public static float ConvertSurge(float propheticAppeal, float socialDespair, ApocalypticMovementParams p)
        {
            float appeal = Mathf.Clamp01(propheticAppeal);
            float despair = Mathf.Clamp01(socialDespair);
            return Mathf.Clamp01(appeal * (1f + despair * p.despairPull));
        }

        public static float ConvertSurge(float propheticAppeal, float socialDespair)
            => ConvertSurge(propheticAppeal, socialDespair, ApocalypticMovementParams.Default);

        /// <summary>
        /// 終末への備え＝過激行動（0..1）＝信徒の急増 convertSurge(0..1)×教義の切迫 doctrinalUrgency(0..1)×preparationScale。
        /// 信徒が多く「もう時間がない」ほど財産放棄・武装・隔離などの過激な備えに走る。
        /// </summary>
        public static float EndTimePreparation(float convertSurge, float doctrinalUrgency, ApocalypticMovementParams p)
        {
            float surge = Mathf.Clamp01(convertSurge);
            float urgency = Mathf.Clamp01(doctrinalUrgency);
            return Mathf.Clamp01(surge * urgency * p.preparationScale);
        }

        public static float EndTimePreparation(float convertSurge, float doctrinalUrgency)
            => EndTimePreparation(convertSurge, doctrinalUrgency, ApocalypticMovementParams.Default);

        /// <summary>
        /// 千年王国の約束の引力（0..1）＝救済の幻 salvationVision(0..1)×現世の苦 currentSuffering(0..1)×promiseScale。
        /// 救済の絵が鮮やかで現世が苦しいほど約束が効く（苦しいほど千年王国に縋る）。
        /// </summary>
        public static float MillenarianPromise(float salvationVision, float currentSuffering, ApocalypticMovementParams p)
        {
            float vision = Mathf.Clamp01(salvationVision);
            float suffering = Mathf.Clamp01(currentSuffering);
            return Mathf.Clamp01(vision * suffering * p.promiseScale);
        }

        public static float MillenarianPromise(float salvationVision, float currentSuffering)
            => MillenarianPromise(salvationVision, currentSuffering, ApocalypticMovementParams.Default);

        /// <summary>
        /// 予言の不成就度（0..1）＝予言した日 predictedDate と実際に到来した日 dateArrived の隔たり。
        /// 到来日が予言日を過ぎたぶんだけ「世界は終わらなかった」＝外れ。
        /// 隔たり/(隔たり+1) で 0..1 に収める（過ぎるほど外れが深まり 1 に漸近）。dateArrived≤predictedで 0（まだ外れていない）。
        /// </summary>
        public static float ProphecyFailure(float predictedDate, float dateArrived, ApocalypticMovementParams p)
        {
            float overshoot = Mathf.Max(0f, dateArrived - predictedDate);
            return Mathf.Clamp01(overshoot / (overshoot + 1f));
        }

        public static float ProphecyFailure(float predictedDate, float dateArrived)
            => ProphecyFailure(predictedDate, dateArrived, ApocalypticMovementParams.Default);

        /// <summary>
        /// 認知的不協和（0..1）＝予言外れ prophecyFailure(0..1)×それまでの傾倒 priorCommitment(0..1)×dissonanceGain。
        /// 外れが大きく傾倒が深いほど「信じてきたのに外れた」矛盾の痛みが大きい。
        /// </summary>
        public static float CognitiveDissonance(float prophecyFailure, float priorCommitment, ApocalypticMovementParams p)
        {
            float failure = Mathf.Clamp01(prophecyFailure);
            float commitment = Mathf.Clamp01(priorCommitment);
            return Mathf.Clamp01(failure * commitment * p.dissonanceGain);
        }

        public static float CognitiveDissonance(float prophecyFailure, float priorCommitment)
            => CognitiveDissonance(prophecyFailure, priorCommitment, ApocalypticMovementParams.Default);

        /// <summary>
        /// 運動の急進化（0..1）＝認知的不協和 cognitiveDissonance(0..1)×指導者のカリスマ leaderCharisma(0..1)×charismaGain。
        /// 予言が外れても解消すべき不協和をカリスマある指導者が「試練だった」と再解釈し、かえって信仰を深める逆説（外れて急進化）。
        /// </summary>
        public static float Radicalization(float cognitiveDissonance, float leaderCharisma, ApocalypticMovementParams p)
        {
            float dissonance = Mathf.Clamp01(cognitiveDissonance);
            float charisma = Mathf.Clamp01(leaderCharisma);
            return Mathf.Clamp01(dissonance * charisma * p.charismaGain);
        }

        public static float Radicalization(float cognitiveDissonance, float leaderCharisma)
            => Radicalization(cognitiveDissonance, leaderCharisma, ApocalypticMovementParams.Default);

        /// <summary>
        /// 集団自決の危険（0..1）＝急進化 radicalization(0..1)×社会からの孤立 isolationFromSociety(0..1)×isolationGain。
        /// 急進化した運動が外界から孤立し退路を断たれるほど、最終的な集団自決（人民寺院型）の危険が高まる。
        /// </summary>
        public static float CollectiveSuicideRisk(float radicalization, float isolationFromSociety, ApocalypticMovementParams p)
        {
            float radical = Mathf.Clamp01(radicalization);
            float isolation = Mathf.Clamp01(isolationFromSociety);
            return Mathf.Clamp01(radical * isolation * p.isolationGain);
        }

        public static float CollectiveSuicideRisk(float radicalization, float isolationFromSociety)
            => CollectiveSuicideRisk(radicalization, isolationFromSociety, ApocalypticMovementParams.Default);
    }
}
