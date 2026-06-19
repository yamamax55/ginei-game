using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ピーターの法則による官僚機構の能力ドリフトの純ロジック（唯一の窓口・test-first）。
    /// <para>
    /// ピーターの法則＝「階層組織では、各人は<b>無能のレベルに達するまで昇進する</b>」。
    /// 昇進判断は<b>今の職位の成績</b>でなされるが、上の職位は<b>別種の能力</b>を要する。
    /// 故に有能な実務者ほど昇進し、しかし新しい職位で初めて無能を露呈し、そこで昇進が止まる＝
    /// 時間が経つほど各ポストは「その職位で無能になった者」で埋まり、組織全体の実効能力が
    /// 緩やかに劣化していく（席次/年功で昇る組織ほど顕著・実力で罷免できる組織ほど緩い）。
    /// </para>
    /// <para>
    /// <b>分担（並行系を作らない）</b>：
    /// `BureaucracyBloatRules`＝パーキンソン的な<b>人数</b>の自己増殖（定員肥大・管理コスト。質は扱わない）／
    /// `SeniorityRules`＝ある時点の<b>席次 vs 実力</b>の実効序列スナップショット（時間進行を扱わない）／
    /// `BureaucracyCareerRules`/`CivilServiceRules`＝個々人の<b>叙位・銓衡</b>（誰を登用するか）／
    /// `AuthoritarianSelectionRules`＝全体主義の<b>逆淘汰</b>（非情/従順で最悪が頂点に立つ。能力-職位の不整合ではない）／
    /// `MeritPromotionRules`＝戦功→昇進→編成数（功績軸）。
    /// <b>本クラス＝「昇進が能力と職位の不整合を生み、組織の実効能力が世代を経て劣化する」軸のみ</b>を扱う。
    /// 個体粒度（pop/個人台帳）へ降りず、勢力の官僚機構を<b>集約スカラ（実効能力 0..1）</b>として進める
    /// （終盤ラグ回避・暦境界 Tick 想定）。
    /// </para>
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（新しい値を返す）。
    /// 調整値は <see cref="BureauPeterParams"/>（既定 <see cref="BureauPeterParams.Default"/>）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BureauPeterPrincipleRules
    {
        /// <summary>
        /// ピーターの法則の調整値（昇進相関・無能化幅・劣化速度・実力罷免の効き）。ctor で全てクランプ。
        /// </summary>
        public readonly struct BureauPeterParams
        {
            /// <summary>
            /// 昇進の merit 相関（0..1）。1＝完全な実力主義（昇進は今の成績だけで決まる）、
            /// 0＝年功/席次/縁故で昇進が決まり成績と無相関。低いほど無能が上に滞留しやすい。
            /// </summary>
            public readonly float meritCorrelation;
            /// <summary>
            /// 職位間の能力転移率（0..1）。1＝下の職位の能力がそのまま上で通用する（無能化が起きない）、
            /// 0＝上の職位は全く別種の能力を要し下の成績は無意味＝ピーターの法則が最大に効く。
            /// </summary>
            public readonly float skillTransfer;
            /// <summary>無能化1段あたりの実効能力ロスの最大幅（0..1。能力転移0・実力罷免0でのミスマッチ上限）。</summary>
            public readonly float maxIncompetenceDrop;
            /// <summary>実力本位の罷免の効き（0..1）。1＝無能を即座に降格/更迭できる、0＝終身で滞留する。</summary>
            public readonly float demotionEfficacy;
            /// <summary>組織能力の年次ドリフト速度（per year・ミスマッチ圧1のとき実効能力がこの割合で目標へ寄る）。</summary>
            public readonly float driftRate;

            public BureauPeterParams(float meritCorrelation, float skillTransfer, float maxIncompetenceDrop,
                                     float demotionEfficacy, float driftRate)
            {
                this.meritCorrelation = Mathf.Clamp01(meritCorrelation);
                this.skillTransfer = Mathf.Clamp01(skillTransfer);
                this.maxIncompetenceDrop = Mathf.Clamp01(maxIncompetenceDrop);
                this.demotionEfficacy = Mathf.Clamp01(demotionEfficacy);
                this.driftRate = Mathf.Max(0f, driftRate);
            }

            /// <summary>
            /// 既定＝実力相関0.6（おおむね成績で昇進するが縁故も残る）・能力転移0.5（半分は別種の能力）・
            /// 無能化幅0.4・罷免の効き0.3（多くの官僚機構は更迭が利きにくい）・ドリフト速度0.1/年。
            /// </summary>
            public static BureauPeterParams Default
                => new BureauPeterParams(0.6f, 0.5f, 0.4f, 0.3f, 0.1f);
        }

        /// <summary>
        /// 昇進後の能力ミスマッチ（0..1。大きいほど「能力と職位がずれている＝無能が上に立つ」）。
        /// <para>
        /// 機序：昇進は<b>今の成績</b>で決まる（merit 相関が高いほど有能者が選ばれる）が、上の職位は
        /// <b>別種の能力</b>を要する（能力転移 skillTransfer が低いほど下の成績が通用しない）。
        /// さらに実力本位の罷免（demotionEfficacy）が利けば不整合な者を外せる＝ミスマッチが減る。
        /// </para>
        /// 式＝(1−能力転移)×(1−実力相関を含む選抜の質)×(1−罷免の効き)×最大無能化幅。
        /// merit 相関が高いと「成績の良い者を選んでいる」ぶんミスマッチ顕在化が遅れる（昇進直後は優秀に見える）。
        /// </summary>
        public static float IncompetenceMismatch(BureauPeterParams p)
        {
            // 別種の能力を要するほど（転移↓）・罷免が利かないほど（demotion↓）ミスマッチが残る。
            // 実力相関が高いほど選抜段階で優秀者を引いており、初期ミスマッチは小さい。
            float byTransfer = 1f - p.skillTransfer;
            float bySelection = 1f - p.meritCorrelation * 0.5f; // 完全実力でも上の能力は別物ゆえ半分しか効かない
            float byDemotion = 1f - p.demotionEfficacy;
            return Mathf.Clamp01(byTransfer * bySelection * byDemotion * p.maxIncompetenceDrop);
        }

        /// <summary>既定 Params 版。</summary>
        public static float IncompetenceMismatch()
            => IncompetenceMismatch(BureauPeterParams.Default);

        /// <summary>
        /// 組織の実効能力の収束目標（0..1）。投入される人材プールの平均能力 talentPool(0..1) から
        /// ミスマッチ（<see cref="IncompetenceMismatch"/>）ぶんを差し引いた水準＝
        /// 「優秀な人材を採っても、ピーターの法則で各ポストが無能化するため、組織能力は人材プールより下に落ち着く」。
        /// 人材プールが高くてもミスマッチが大きければ低く収束する。
        /// </summary>
        public static float EquilibriumCompetence(float talentPool, BureauPeterParams p)
        {
            float pool = Mathf.Clamp01(talentPool);
            float mismatch = IncompetenceMismatch(p);
            return Mathf.Clamp01(pool * (1f - mismatch));
        }

        /// <summary>既定 Params 版。</summary>
        public static float EquilibriumCompetence(float talentPool)
            => EquilibriumCompetence(talentPool, BureauPeterParams.Default);

        /// <summary>
        /// 組織の実効能力の1年ドリフト（更新後の実効能力 0..1）。現在値を収束目標
        /// （<see cref="EquilibriumCompetence"/>）へ driftRate×dt の割合だけ寄せる（指数収束・上下対称）。
        /// 現在が目標より高ければ無能化で下がり、低ければ採用/淘汰で目標まで戻る。dt は年単位・負は0扱い・割合は飽和。
        /// 新しい実効能力を返す（引数非破壊）。
        /// </summary>
        public static float DriftTick(float currentCompetence, float talentPool, float dt, BureauPeterParams p)
        {
            float cur = Mathf.Clamp01(currentCompetence);
            float target = EquilibriumCompetence(talentPool, p);
            float t = Mathf.Clamp01(p.driftRate * Mathf.Max(0f, dt)); // 1ステップで行き過ぎない
            return Mathf.Clamp01(Mathf.Lerp(cur, target, t));
        }

        /// <summary>既定 Params 版。</summary>
        public static float DriftTick(float currentCompetence, float talentPool, float dt)
            => DriftTick(currentCompetence, talentPool, dt, BureauPeterParams.Default);

        /// <summary>
        /// 行政throughput係数（0..1）＝実効能力をそのまま行政処理能力の倍率として返すヘルパ。
        /// 呼び出し側が `MinistryAdminRules.StaffingEfficiency` 等に掛けて「ポストは埋まっているが
        /// 中身が無能化している」ぶんの目減りを表す想定（基準非破壊・実効値パターン）。
        /// 数値は委譲先（StaffingEfficiency 等）が決め、本ヘルパは恒等の薄いラッパに留める（二重実装回避）。
        /// </summary>
        public static float ThroughputFactor(float effectiveCompetence)
            => Mathf.Clamp01(effectiveCompetence);

        /// <summary>
        /// ピーターの法則が深刻化した判定（true＝実効能力が閾値を下回り組織が機能不全）。
        /// 逆淘汰（<see cref="AuthoritarianSelectionRules.IsWorstOnTop"/>）と同型の終端判定。閾値はクランプ。
        /// </summary>
        public static bool IsHollowedOut(float effectiveCompetence, float threshold)
            => Mathf.Clamp01(effectiveCompetence) < Mathf.Clamp01(threshold);
    }
}
