using UnityEngine;

namespace Ginei
{
    /// <summary>組織学習能力の調整係数（SHP-2 #1375・『失敗の本質』型）。</summary>
    public readonly struct OrganizationalLearningParams
    {
        /// <summary>失敗分析能力の基礎係数（自己批判×率直さから直視できる能力の濃さ）。</summary>
        public readonly float analysisScale;
        /// <summary>敗北からの学習の基礎係数（痛い敗北を分析できれば学びが大きい）。</summary>
        public readonly float learningScale;
        /// <summary>ドクトリン改善/秒の基礎係数（教訓が作戦・組織を改善する速さ＝米軍型）。</summary>
        public readonly float adaptationRate;
        /// <summary>失敗の神話化/秒の基礎係数（精神論で糊塗し前提を温存する速さ＝日本軍型）。</summary>
        public readonly float mythologizeRate;
        /// <summary>ダブルループ学習が前提を問い直して得る根本学習の最大上乗せ。</summary>
        public readonly float doubleLoopScale;
        /// <summary>長期戦で学習する組織が得る適応の優位の最大値（適応する側が勝つ）。</summary>
        public readonly float adaptiveScale;
        /// <summary>学習する組織と判定する既定閾値。</summary>
        public readonly float learningThreshold;

        public OrganizationalLearningParams(float analysisScale, float learningScale, float adaptationRate,
            float mythologizeRate, float doubleLoopScale, float adaptiveScale, float learningThreshold)
        {
            this.analysisScale = Mathf.Max(0f, analysisScale);
            this.learningScale = Mathf.Max(0f, learningScale);
            this.adaptationRate = Mathf.Max(0f, adaptationRate);
            this.mythologizeRate = Mathf.Max(0f, mythologizeRate);
            this.doubleLoopScale = Mathf.Max(0f, doubleLoopScale);
            this.adaptiveScale = Mathf.Max(0f, adaptiveScale);
            this.learningThreshold = Mathf.Clamp01(learningThreshold);
        }

        /// <summary>
        /// 既定＝分析1.0・学習1.0・改善0.1/秒・神話化0.08/秒・ダブルループ上乗せ0.4・適応優位上限0.5・学習閾値0.5。
        /// 自己批判を許し原因を直視すれば失敗を分析でき（分析1.0）、痛い敗北を分析できれば学びが深く（学習1.0）、
        /// 教訓は作戦・組織を時間で改善し（0.1/秒＝米軍型）、精神論で糊塗すれば前提が温存され（0.08/秒＝日本軍型）、
        /// 前提を問い直せば根本学習が上乗せされ（0.4）、長期戦ほど学習する側が適応で優位に立つ（最大0.5）。
        /// </summary>
        public static OrganizationalLearningParams Default =>
            new OrganizationalLearningParams(1f, 1f, 0.1f, 0.08f, 0.4f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 組織学習能力の純ロジック（SHP-2 #1375・『失敗の本質』）。
    /// <b>敗北・失敗から学んで能力を改善できる組織か</b>＝米軍は敗北を分析して教訓を制度化し改善したが、
    /// 日本軍は精神論で失敗を糊塗し同じ過ちを繰り返した。組織が失敗をフィードバックして学習する能力が、
    /// 長期の戦争での適応力を決める。<b>自己批判を許し原因を直視する</b>組織は敗北を分析でき（失敗分析能力）、
    /// 痛い敗北を分析できれば教訓が深く（敗北からの学習）、教訓を制度化すれば作戦・組織が時間で改善する
    /// （ドクトリン改善＝米軍型）。逆に<b>失敗を精神論・神話で糊塗</b>する組織は自己批判を避けて前提を温存し
    /// （失敗の神話化＝日本軍型）、学習できない硬直した組織は同じ過ちを繰り返す（反復失敗リスク）。表面的修正
    /// （シングルループ）に留まらず<b>前提を問い直す</b>と根本学習（ダブルループ）になり、長期戦ほど学習する側が
    /// 適応で優位に立つ（適応する側が勝つ）。
    /// <see cref="InstitutionalMemoryRules"/>（危機学習→制度知の蓄積＝歴史の教訓を制度的記憶へ昇華）／
    /// <see cref="VeterancyRules"/>（個人・部隊の練度＝経験値で上がる戦闘倍率）／
    /// <see cref="AtmosphereRules"/>（空気＝同調圧力で異論が消える・同EPIC SHP）／
    /// <see cref="InstitutionalCorrectionRules"/>（誤りの蓄積と脆性崩壊＝自己修正できるか否か）とは別＝ここは
    /// <b>敗北・失敗を分析して学べる組織か（組織学習能力）</b>を扱う。すべて plain な float で受け渡す。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OrganizationalLearningRules
    {
        /// <summary>
        /// 失敗分析能力（0..1）＝失敗を率直に分析する能力＝自己批判（selfCriticism）×率直さ（openness）。
        /// 自己批判を許し原因を直視するほど高く、精神論で糊塗するほど低い＝<b>どちらか欠ければ失敗は直視
        /// されない</b>（積で表す）。
        /// </summary>
        public static float FailureAnalysisCapacity(float selfCriticism, float openness,
            OrganizationalLearningParams p)
        {
            float crit = Mathf.Clamp01(selfCriticism);
            float open = Mathf.Clamp01(openness);
            return Mathf.Clamp01(crit * open * p.analysisScale);
        }

        public static float FailureAnalysisCapacity(float selfCriticism, float openness)
            => FailureAnalysisCapacity(selfCriticism, openness, OrganizationalLearningParams.Default);

        /// <summary>
        /// 敗北からの学習（0..1）＝敗北の深刻さ（defeatSeverity）×失敗を分析する能力（failureAnalysisCapacity）。
        /// 痛い敗北を分析できれば学びが大きく、分析できなければ無駄に終わる＝<b>どちらか欠ければ教訓は得られ
        /// ない</b>（積で表す）。
        /// </summary>
        public static float LearningFromDefeat(float defeatSeverity, float failureAnalysisCapacity,
            OrganizationalLearningParams p)
        {
            float severity = Mathf.Clamp01(defeatSeverity);
            float analysis = Mathf.Clamp01(failureAnalysisCapacity);
            return Mathf.Clamp01(severity * analysis * p.learningScale);
        }

        public static float LearningFromDefeat(float defeatSeverity, float failureAnalysisCapacity)
            => LearningFromDefeat(defeatSeverity, failureAnalysisCapacity, OrganizationalLearningParams.Default);

        /// <summary>
        /// ドクトリン改善（dt後の doctrineEffectiveness 0..1）＝学習が作戦・組織を時間で改善する（教訓を制度化＝
        /// 米軍型）。learning×adaptationRate×dt ぶん上積みする＝<b>学べば学ぶほどドクトリンが洗練される</b>。
        /// 1で頭打ち。
        /// </summary>
        public static float DoctrineAdaptationTick(float doctrineEffectiveness, float learning,
            float dt, OrganizationalLearningParams p)
        {
            float effectiveness = Mathf.Clamp01(doctrineEffectiveness);
            float learn = Mathf.Clamp01(learning);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(effectiveness + learn * p.adaptationRate * step);
        }

        public static float DoctrineAdaptationTick(float doctrineEffectiveness, float learning, float dt)
            => DoctrineAdaptationTick(doctrineEffectiveness, learning, dt, OrganizationalLearningParams.Default);

        /// <summary>
        /// 失敗の神話化（0..1）＝失敗を精神論・神話で糊塗する度合い＝自己批判を避けるほど（1−failureAnalysisCapacity）
        /// ×体面維持の圧力（faceSavingPressure）。分析能力が低く体面を守りたいほど失敗を神話で覆い隠し、前提を
        /// 問い直さず同じ過ちへ向かう（日本軍型）＝<b>糊塗できる組織は学ばない</b>。
        /// </summary>
        public static float MythologizeFailure(float failureAnalysisCapacity, float faceSavingPressure)
        {
            float analysis = Mathf.Clamp01(failureAnalysisCapacity);
            float face = Mathf.Clamp01(faceSavingPressure);
            return Mathf.Clamp01((1f - analysis) * face);
        }

        /// <summary>
        /// 反復失敗リスク（0..1）＝学習できない硬直した組織が同じ失敗を繰り返すリスク＝ドクトリンの硬直
        /// （doctrineRigidity）×学習できなさ（1−learningCapacity）。硬直して学べないほど高く、柔軟に学べば下がる
        /// ＝<b>学習する組織は同じ過ちを避ける</b>。学習能力が満ちていればリスクは0。
        /// </summary>
        public static float RepeatedFailureRisk(float doctrineRigidity, float learningCapacity)
        {
            float rigidity = Mathf.Clamp01(doctrineRigidity);
            float capacity = Mathf.Clamp01(learningCapacity);
            return Mathf.Clamp01(rigidity * (1f - capacity));
        }

        /// <summary>
        /// シングルループ vs ダブルループ学習（実効的な学習の深さ 0..1）＝表面的修正（シングルループ）に、前提を
        /// 問い直す根本学習（ダブルループ）ぶんを上乗せする。基準の学習（learning）に、前提を問う度合い
        /// （assumptionQuestioning）×doubleLoopScale を足す＝<b>前提を問い直す組織ほど深く学ぶ</b>
        /// （基準値は非破壊＝実効値パターン）。1で頭打ち。
        /// </summary>
        public static float SingleLoopVsDoubleLoop(float learning, float assumptionQuestioning,
            OrganizationalLearningParams p)
        {
            float learn = Mathf.Clamp01(learning);
            float questioning = Mathf.Clamp01(assumptionQuestioning);
            return Mathf.Clamp01(learn + questioning * p.doubleLoopScale);
        }

        public static float SingleLoopVsDoubleLoop(float learning, float assumptionQuestioning)
            => SingleLoopVsDoubleLoop(learning, assumptionQuestioning, OrganizationalLearningParams.Default);

        /// <summary>
        /// 適応の優位（0..adaptiveScale）＝長期戦ほど学習する組織が優位になる（適応する側が勝つ）。学習能力
        /// （learningCapacity）×戦争の長さ（warDuration）×adaptiveScale＝<b>短期決戦では差が出ず、長引くほど
        /// 学習する側が積み重ねた改善で勝つ</b>。
        /// </summary>
        public static float AdaptiveAdvantage(float learningCapacity, float warDuration,
            OrganizationalLearningParams p)
        {
            float capacity = Mathf.Clamp01(learningCapacity);
            float duration = Mathf.Clamp01(warDuration);
            return capacity * duration * p.adaptiveScale;
        }

        public static float AdaptiveAdvantage(float learningCapacity, float warDuration)
            => AdaptiveAdvantage(learningCapacity, warDuration, OrganizationalLearningParams.Default);

        /// <summary>
        /// 学習する組織の判定（true＝敗北から学び改善する組織）＝失敗分析能力（failureAnalysisCapacity）と
        /// ドクトリン改善（doctrineAdaptation）の双方が閾値以上なら、敗北・失敗を分析して教訓を制度化し改善する
        /// 組織とみなす＝<b>分析できかつ改善できているか</b>。どちらか欠ければ精神論で糊塗して同じ過ちを繰り返す
        /// 組織（false）。
        /// </summary>
        public static bool IsLearningMilitary(float failureAnalysisCapacity, float doctrineAdaptation,
            float threshold)
        {
            float analysis = Mathf.Clamp01(failureAnalysisCapacity);
            float adaptation = Mathf.Clamp01(doctrineAdaptation);
            float th = Mathf.Clamp01(threshold);
            return analysis >= th && adaptation >= th;
        }

        public static bool IsLearningMilitary(float failureAnalysisCapacity, float doctrineAdaptation)
            => IsLearningMilitary(failureAnalysisCapacity, doctrineAdaptation,
                OrganizationalLearningParams.Default.learningThreshold);
    }
}
