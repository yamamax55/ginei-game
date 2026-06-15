using UnityEngine;

namespace Ginei
{
    /// <summary>制度的な誤りの蓄積と自己修正の調整係数（POPR-3 #1517・ポパー型）。</summary>
    public readonly struct InstitutionalCorrectionParams
    {
        /// <summary>誤りの蓄積/秒の基礎係数（新たな誤りが積もる速さ）。</summary>
        public readonly float accumulationRate;
        /// <summary>批判の自由が修正能力へ寄与する重み（開かれた社会の主因）。</summary>
        public readonly float criticismWeight;
        /// <summary>フィードバック回路が修正能力へ寄与する重み（誤りを拾い直す回路）。</summary>
        public readonly float feedbackWeight;
        /// <summary>脆性の誤り感応度（蓄積した誤りが脆さへ変わる強さ）。</summary>
        public readonly float brittlenessScale;
        /// <summary>崩壊が起こる誤り蓄積の既定臨界点（これ以上で衝撃が非線形に効く）。</summary>
        public readonly float criticalThreshold;
        /// <summary>試行錯誤の学習/秒の基礎係数（漸進的工学で制度が改善する速さ）。</summary>
        public readonly float learningRate;
        /// <summary>抑圧による隠蔽蓄積/秒の基礎係数（閉じた社会で誤りが見えないまま溜まる速さ）。</summary>
        public readonly float concealmentRate;

        public InstitutionalCorrectionParams(float accumulationRate, float criticismWeight, float feedbackWeight,
            float brittlenessScale, float criticalThreshold, float learningRate, float concealmentRate)
        {
            this.accumulationRate = Mathf.Max(0f, accumulationRate);
            this.criticismWeight = Mathf.Clamp01(criticismWeight);
            this.feedbackWeight = Mathf.Clamp01(feedbackWeight);
            this.brittlenessScale = Mathf.Max(0f, brittlenessScale);
            this.criticalThreshold = Mathf.Clamp01(criticalThreshold);
            this.learningRate = Mathf.Max(0f, learningRate);
            this.concealmentRate = Mathf.Max(0f, concealmentRate);
        }

        /// <summary>
        /// 既定＝蓄積0.1/秒・批判の重み0.6・フィードバック重み0.4・脆性感応1.0・崩壊臨界0.6・
        /// 学習0.05/秒・隠蔽蓄積0.08/秒。批判の自由（0.6）がフィードバック回路（0.4）より修正能力に効く＝
        /// 誤りを口に出せる社会ほど自己修正でき、誤り蓄積が臨界0.6を超えると衝撃で非線形に崩れる。
        /// </summary>
        public static InstitutionalCorrectionParams Default =>
            new InstitutionalCorrectionParams(0.1f, 0.6f, 0.4f, 1f, 0.6f, 0.05f, 0.08f);
    }

    /// <summary>
    /// 制度的な誤りの蓄積と脆性崩壊の純ロジック（POPR-3 #1517・カール・ポパー『開かれた社会とその敵』）。
    /// <b>開かれた社会＝試行錯誤で誤りを修正できる社会</b>。誤りを認め修正する仕組み（批判の自由・フィードバック
    /// 回路）があれば、新たな誤りが積もっても修正能力がそれを削り、誤り（errorStock）は溜まらない。逆に
    /// 自己修正できない閉じた社会では、誤りが<b>蓄積して脆性を高め</b>、臨界点を超えると衝撃で<b>非線形に
    /// 崩壊</b>する。批判を抑圧すると誤りが見えないまま溜まり（隠蔽の罠）、透明性と批判が誤りの早期発見を促し、
    /// 試行錯誤（漸進的工学）が制度を学習させて改善する。
    /// <see cref="DynastyRules"/>（天命と王朝サイクル＝腐敗が制度疲労で進む）／<see cref="HistoricismTrapRules"/>
    /// （歴史主義の罠＝必然論が誤りを否認する）とは別＝ここは<b>自己修正できるか否かが誤り蓄積と脆性崩壊を
    /// 分ける（試行錯誤の有無）</b>を扱う。同 EPIC POPR では OpennessRules（開放度＝反証可能性・批判の許容）と
    /// 分担する（開放度が高いほど本ルールの修正能力が高い）。すべて plain な float で受け渡す。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InstitutionalCorrectionRules
    {
        /// <summary>
        /// 修正能力（0..1）＝批判の自由×重み＋フィードバック回路×重み。批判を口に出せて（criticism）、
        /// 誤りを拾い直す回路（feedbackLoops）があるほど誤りを自己修正できる＝<b>開かれた社会ほど高い</b>。
        /// 既定では批判の自由がフィードバックより効く（criticismWeight 0.6 > feedbackWeight 0.4）。
        /// </summary>
        public static float CorrectionCapacity(float criticismFreedom, float feedbackLoops,
            InstitutionalCorrectionParams p)
        {
            float crit = Mathf.Clamp01(criticismFreedom);
            float feed = Mathf.Clamp01(feedbackLoops);
            return Mathf.Clamp01(crit * p.criticismWeight + feed * p.feedbackWeight);
        }

        public static float CorrectionCapacity(float criticismFreedom, float feedbackLoops)
            => CorrectionCapacity(criticismFreedom, feedbackLoops, InstitutionalCorrectionParams.Default);

        /// <summary>
        /// 誤りの蓄積（dt後の errorStock 0..1）＝新たな誤りが積もり、修正能力がそれを削る。1tick の正味変化＝
        /// (newErrors − correctionCapacity)×accumulationRate×dt ぶん。修正能力が新たな誤りを上回れば誤りは
        /// 減り（自己修正できれば溜まらない）、下回れば溜まる＝<b>自己修正できる制度は誤りを溜めない</b>。
        /// </summary>
        public static float ErrorAccumulationTick(float errorStock, float newErrors, float correctionCapacity,
            float dt, InstitutionalCorrectionParams p)
        {
            float stock = Mathf.Clamp01(errorStock);
            float incoming = Mathf.Clamp01(newErrors);
            float cap = Mathf.Clamp01(correctionCapacity);
            float step = Mathf.Max(0f, dt);
            float delta = (incoming - cap) * p.accumulationRate * step;
            return Mathf.Clamp01(stock + delta);
        }

        public static float ErrorAccumulationTick(float errorStock, float newErrors, float correctionCapacity, float dt)
            => ErrorAccumulationTick(errorStock, newErrors, correctionCapacity, dt, InstitutionalCorrectionParams.Default);

        /// <summary>
        /// 脆性（0..1）＝蓄積した誤りが制度の脆さを高める。修正されない歪みが溜まるほど脆くなる＝
        /// errorStock×brittlenessScale で写す（既定 1.0 ＝誤り蓄積がそのまま脆性）＝<b>溜めた誤りは脆さに変わる</b>。
        /// </summary>
        public static float BrittlenessFromErrors(float errorStock, InstitutionalCorrectionParams p)
        {
            float stock = Mathf.Clamp01(errorStock);
            return Mathf.Clamp01(stock * p.brittlenessScale);
        }

        public static float BrittlenessFromErrors(float errorStock)
            => BrittlenessFromErrors(errorStock, InstitutionalCorrectionParams.Default);

        /// <summary>
        /// 崩壊確率（0..1）＝誤り蓄積が臨界を超えると衝撃で非線形に崩壊する。臨界未満は衝撃が来ても崩れず
        /// （確率0）、臨界超過ぶん（超過率）と衝撃の<b>積</b>で確率が立ち上がる＝<b>溜めた誤りが多いほど小さな
        /// 衝撃でも崩れる</b>非線形。臨界点1なら決して崩れない。
        /// </summary>
        public static float CollapseProbability(float errorStock, float shock, float criticalThreshold)
        {
            float stock = Mathf.Clamp01(errorStock);
            float s = Mathf.Clamp01(shock);
            float th = Mathf.Clamp01(criticalThreshold);
            if (th >= 1f) return 0f;
            if (stock <= th) return 0f;
            // 臨界超過ぶんを残り幅で正規化した「超過率」×衝撃＝非線形に崩壊が立ち上がる。
            float excess = (stock - th) / (1f - th);
            return Mathf.Clamp01(excess * s);
        }

        public static float CollapseProbability(float errorStock, float shock)
            => CollapseProbability(errorStock, shock, InstitutionalCorrectionParams.Default.criticalThreshold);

        /// <summary>
        /// 誤りの発見度（0..1）＝透明性×批判で誤りがどれだけ早く発見されるか。透明で（transparency）批判が
        /// 許される（criticism）ほど誤りが表に出る＝<b>隠蔽されると溜まる</b>（どちらか欠ければ発見されにくい
        /// ＝積で表す）。
        /// </summary>
        public static float ErrorDetection(float transparency, float criticism)
        {
            float trans = Mathf.Clamp01(transparency);
            float crit = Mathf.Clamp01(criticism);
            return Mathf.Clamp01(trans * crit);
        }

        /// <summary>
        /// 試行錯誤の学習（dt後の correctionCapacity 0..1）＝修正能力×実験度ぶんだけ制度が学習し改善する
        /// （漸進的工学）。修正能力が高く実験を重ねるほど能力自体が伸びる＝<b>試して直すほど直せる力が育つ</b>。
        /// どちらかゼロなら据え置き（積で表す）。1で頭打ち。
        /// </summary>
        public static float TrialAndErrorLearning(float correctionCapacity, float experimentation, float dt,
            InstitutionalCorrectionParams p)
        {
            float cap = Mathf.Clamp01(correctionCapacity);
            float exp = Mathf.Clamp01(experimentation);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(cap + p.learningRate * cap * exp * step);
        }

        public static float TrialAndErrorLearning(float correctionCapacity, float experimentation, float dt)
            => TrialAndErrorLearning(correctionCapacity, experimentation, dt, InstitutionalCorrectionParams.Default);

        /// <summary>
        /// 隠蔽の蓄積（dt後の errorStock 0..1）＝批判を抑圧すると誤りが見えないまま溜まる（閉じた社会の罠）。
        /// 抑圧（suppression）が強いほど、既存の誤りが多いほど隠蔽が積み増す＝<b>抑圧は誤りを覆い隠して
        /// 増幅する</b>（suppression×errorStock で既存の歪みが見えないまま膨らむ）。抑圧ゼロなら据え置き。
        /// </summary>
        public static float ConcealmentBacklog(float suppression, float errorStock, float dt,
            InstitutionalCorrectionParams p)
        {
            float supp = Mathf.Clamp01(suppression);
            float stock = Mathf.Clamp01(errorStock);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(stock + p.concealmentRate * supp * stock * step);
        }

        public static float ConcealmentBacklog(float suppression, float errorStock, float dt)
            => ConcealmentBacklog(suppression, errorStock, dt, InstitutionalCorrectionParams.Default);

        /// <summary>
        /// 崩壊へ向かう判定（true＝誤りが修正能力を上回り崩壊へ向かう）。蓄積した誤りが閾値を超え、かつ
        /// その誤りが修正能力を上回るとき＝<b>修正が追いつかず誤りが膨らむ閉じた社会</b>。誤りが少なくても
        /// 修正能力が十分高ければ向かわない（自己修正で間に合う）。
        /// </summary>
        public static bool IsHeadedForCollapse(float errorStock, float correctionCapacity, float threshold)
        {
            float stock = Mathf.Clamp01(errorStock);
            float cap = Mathf.Clamp01(correctionCapacity);
            float th = Mathf.Clamp01(threshold);
            return stock >= th && stock > cap;
        }

        public static bool IsHeadedForCollapse(float errorStock, float correctionCapacity)
            => IsHeadedForCollapse(errorStock, correctionCapacity, InstitutionalCorrectionParams.Default.criticalThreshold);
    }
}
