using UnityEngine;

namespace Ginei
{
    /// <summary>歴史の教訓と制度的記憶の調整係数（POLY-5 #1454・ポリュビオス型）。</summary>
    public readonly struct InstitutionalMemoryParams
    {
        /// <summary>危機学習の基礎係数（危機×省察から得られる教訓の濃さ）。</summary>
        public readonly float learningScale;
        /// <summary>制度知識の蓄積/秒の基礎係数（教訓が制度知になる速さ）。</summary>
        public readonly float accumulationRate;
        /// <summary>記録なし時の忘却/秒の基礎係数（記録を怠ると記憶が薄れる速さ）。</summary>
        public readonly float decayRate;
        /// <summary>備えボーナスの最大値（蓄積知が類似危機への備えを高める上限）。</summary>
        public readonly float preparednessScale;
        /// <summary>歴史の知恵が意思決定の質を高める最大上乗せ（実践的教師の効き）。</summary>
        public readonly float wisdomScale;
        /// <summary>成文化が記憶を永続させる重み（暗黙知→形式知の固定力）。</summary>
        public readonly float codificationWeight;
        /// <summary>学習する制度と判定する制度知識の既定閾値。</summary>
        public readonly float learningThreshold;

        public InstitutionalMemoryParams(float learningScale, float accumulationRate, float decayRate,
            float preparednessScale, float wisdomScale, float codificationWeight, float learningThreshold)
        {
            this.learningScale = Mathf.Max(0f, learningScale);
            this.accumulationRate = Mathf.Max(0f, accumulationRate);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.preparednessScale = Mathf.Max(0f, preparednessScale);
            this.wisdomScale = Mathf.Max(0f, wisdomScale);
            this.codificationWeight = Mathf.Clamp01(codificationWeight);
            this.learningThreshold = Mathf.Clamp01(learningThreshold);
        }

        /// <summary>
        /// 既定＝学習1.0・蓄積0.1/秒・忘却0.05/秒・備え上限0.6・知恵上限0.5・成文化重み0.7・学習制度閾値0.5。
        /// 痛い危機を省察すれば教訓が深く（学習1.0）、教訓は制度知へ積もり（0.1/秒）、記録を怠れば世代で薄れ
        /// （0.05/秒）、蓄積知は類似危機への備え（最大0.6）と意思決定の質（最大0.5）を高め、成文化（0.7）で永続する。
        /// </summary>
        public static InstitutionalMemoryParams Default =>
            new InstitutionalMemoryParams(1f, 0.1f, 0.05f, 0.6f, 0.5f, 0.7f, 0.5f);
    }

    /// <summary>
    /// 歴史の教訓と制度的記憶の純ロジック（POLY-5 #1454・ポリュビオス『歴史』）。
    /// <b>歴史は政治家の実践的教師</b>＝過去の危機とその対処を記録・記憶することで、国家は同じ過ちを
    /// 繰り返さず賢く対処できる。危機を経験し<b>省察</b>した制度は教訓を制度知識として蓄積し（経験が
    /// 制度知になる）、その知が類似危機への備えと意思決定の質を高める。逆に<b>記録を怠り忘却</b>した制度は
    /// 制度的記憶が世代で薄れ、同じ災厄を繰り返す（歴史を忘れた者は繰り返す）。教訓を文書・制度へ<b>成文化</b>
    /// すれば暗黙知が形式知となり記憶が永続する。
    /// <see cref="GenerationalMemoryRules"/>（戦争記憶の世代減衰＝半減期で薄れる集合記憶と開戦閾値）／
    /// <see cref="InstitutionalCorrectionRules"/>（制度的な誤りの蓄積と脆性崩壊＝自己修正できるか否か）／
    /// <see cref="EducationRules"/>（教育＝学校の質と人材の世代遅延）／<see cref="HistoriographyRules"/>
    /// （歴史叙述＝政権整合で歪む公式評価）とは別＝ここは<b>危機から学んで制度知が蓄積する（歴史の教訓→
    /// 制度的記憶）</b>を扱う。すべて plain な float で受け渡す。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InstitutionalMemoryRules
    {
        /// <summary>
        /// 危機からの学習（0..1）＝危機の深刻さ×省察する能力。痛い危機ほど学びが深く（crisisSeverity）、
        /// 省察しなければ学ばない（reflectionCapacity）＝<b>どちらか欠ければ教訓は得られない</b>（積で表す）。
        /// </summary>
        public static float CrisisLearning(float crisisSeverity, float reflectionCapacity,
            InstitutionalMemoryParams p)
        {
            float severity = Mathf.Clamp01(crisisSeverity);
            float reflection = Mathf.Clamp01(reflectionCapacity);
            return Mathf.Clamp01(severity * reflection * p.learningScale);
        }

        public static float CrisisLearning(float crisisSeverity, float reflectionCapacity)
            => CrisisLearning(crisisSeverity, reflectionCapacity, InstitutionalMemoryParams.Default);

        /// <summary>
        /// 制度知識の蓄積（dt後の institutionalKnowledge 0..1）＝危機の教訓が制度知として積もる
        /// （経験が制度知になる）。crisisLearning×accumulationRate×dt ぶん上積みする＝<b>学べば学ぶほど
        /// 制度知が厚くなる</b>。1で頭打ち。
        /// </summary>
        public static float MemoryAccumulationTick(float institutionalKnowledge, float crisisLearning,
            float dt, InstitutionalMemoryParams p)
        {
            float knowledge = Mathf.Clamp01(institutionalKnowledge);
            float learning = Mathf.Clamp01(crisisLearning);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(knowledge + learning * p.accumulationRate * step);
        }

        public static float MemoryAccumulationTick(float institutionalKnowledge, float crisisLearning, float dt)
            => MemoryAccumulationTick(institutionalKnowledge, crisisLearning, dt, InstitutionalMemoryParams.Default);

        /// <summary>
        /// 制度的記憶の忘却（dt後の institutionalKnowledge 0..1）＝記録を怠ると記憶が薄れる。記録の手当て
        /// （recordKeeping）が薄いほど忘却が速い＝(1−recordKeeping)×decayRate×dt ぶん減る＝<b>記録なしは
        /// 世代で消える</b>。記録が完璧なら薄れない。
        /// </summary>
        public static float MemoryDecay(float institutionalKnowledge, float recordKeeping,
            float dt, InstitutionalMemoryParams p)
        {
            float knowledge = Mathf.Clamp01(institutionalKnowledge);
            float record = Mathf.Clamp01(recordKeeping);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(knowledge - (1f - record) * p.decayRate * step);
        }

        public static float MemoryDecay(float institutionalKnowledge, float recordKeeping, float dt)
            => MemoryDecay(institutionalKnowledge, recordKeeping, dt, InstitutionalMemoryParams.Default);

        /// <summary>
        /// 過ちを繰り返すリスク（0..1）＝制度的記憶が薄いほど、似た危機が来たとき同じ過ちを繰り返す
        /// （歴史を忘れた者は繰り返す）＝(1−institutionalKnowledge)×similarCrisis＝<b>記憶が厚いほど反復を
        /// 避けられる</b>。記憶が満ちていれば似た危機でもリスクは0。
        /// </summary>
        public static float RepeatedMistakeRisk(float institutionalKnowledge, float similarCrisis)
        {
            float knowledge = Mathf.Clamp01(institutionalKnowledge);
            float similar = Mathf.Clamp01(similarCrisis);
            return Mathf.Clamp01((1f - knowledge) * similar);
        }

        /// <summary>
        /// 備えボーナス（0..preparednessScale）＝蓄積された制度知が、類似度の高い危機への備えを高める
        /// （過去に学んだ対処）。institutionalKnowledge×crisisType（似ているほど効く）×preparednessScale＝
        /// <b>過去に学んだ危機にはよく備えられる</b>。
        /// </summary>
        public static float PreparednessBonus(float institutionalKnowledge, float crisisType,
            InstitutionalMemoryParams p)
        {
            float knowledge = Mathf.Clamp01(institutionalKnowledge);
            float similarity = Mathf.Clamp01(crisisType);
            return knowledge * similarity * p.preparednessScale;
        }

        public static float PreparednessBonus(float institutionalKnowledge, float crisisType)
            => PreparednessBonus(institutionalKnowledge, crisisType, InstitutionalMemoryParams.Default);

        /// <summary>
        /// 成文化の価値（0..1）＝教訓を文書・制度へ成文化すると記憶が永続する（暗黙知→形式知）。学んだ教訓
        /// （crisisLearning）を文書化（documentation）した分だけ永続する固定値＝crisisLearning×documentation×
        /// codificationWeight＝<b>書き留めねば教訓は消える</b>（どちらか欠ければ残らない＝積で表す）。
        /// </summary>
        public static float CodificationValue(float crisisLearning, float documentation,
            InstitutionalMemoryParams p)
        {
            float learning = Mathf.Clamp01(crisisLearning);
            float doc = Mathf.Clamp01(documentation);
            return Mathf.Clamp01(learning * doc * p.codificationWeight);
        }

        public static float CodificationValue(float crisisLearning, float documentation)
            => CodificationValue(crisisLearning, documentation, InstitutionalMemoryParams.Default);

        /// <summary>
        /// 歴史の知恵（実効的な意思決定の質 0..1）＝歴史の教訓が意思決定の質を高める（歴史は実践的教師）。
        /// 基準の決定の質（decisionQuality）に、蓄積知ぶんの上乗せ（institutionalKnowledge×wisdomScale）を
        /// 足す＝<b>歴史に学んだ制度ほど賢く決める</b>（基準値は非破壊＝実効値パターン）。1で頭打ち。
        /// </summary>
        public static float WisdomFromHistory(float institutionalKnowledge, float decisionQuality,
            InstitutionalMemoryParams p)
        {
            float knowledge = Mathf.Clamp01(institutionalKnowledge);
            float quality = Mathf.Clamp01(decisionQuality);
            return Mathf.Clamp01(quality + knowledge * p.wisdomScale);
        }

        public static float WisdomFromHistory(float institutionalKnowledge, float decisionQuality)
            => WisdomFromHistory(institutionalKnowledge, decisionQuality, InstitutionalMemoryParams.Default);

        /// <summary>
        /// 学習する制度の判定（true＝危機から学び知を蓄積する学習する制度）＝制度知識が閾値以上なら、
        /// 危機を経験し省察して知を積み同じ過ちを避ける制度とみなす＝<b>歴史を制度知へ昇華できているか</b>。
        /// 知が薄ければ忘却して同じ災厄を繰り返す制度（false）。
        /// </summary>
        public static bool IsLearningOrganization(float institutionalKnowledge, float threshold)
        {
            float knowledge = Mathf.Clamp01(institutionalKnowledge);
            float th = Mathf.Clamp01(threshold);
            return knowledge >= th;
        }

        public static bool IsLearningOrganization(float institutionalKnowledge)
            => IsLearningOrganization(institutionalKnowledge, InstitutionalMemoryParams.Default.learningThreshold);
    }
}
