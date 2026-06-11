using UnityEngine;

namespace Ginei
{
    /// <summary>改革モード＝漸進的社会工学 vs ユートピア的（全体改造）社会工学（POPR-2 #1514・ポパー型）。</summary>
    public enum ReformMode
    {
        /// <summary>漸進的社会工学＝小さな改革を試し誤りから学ぶ＝リスク分散・修正可能。</summary>
        漸進的,
        /// <summary>ユートピア的（全体改造）社会工学＝理想社会を一挙に全体改造＝全か無か・失敗時壊滅的。</summary>
        全体改造
    }

    /// <summary>漸進的社会工学の調整係数（POPR-2 #1514・ポパー型）。</summary>
    public readonly struct PiecemealEngineeringParams
    {
        /// <summary>漸進改革の期待便益の基礎倍率（小さな改良ぶんの控えめな利得）。</summary>
        public readonly float piecemealBenefitScale;
        /// <summary>全体改造の期待便益の基礎倍率（成功すれば大きい＝大胆な賭けの上振れ）。</summary>
        public readonly float utopianBenefitScale;
        /// <summary>漸進改革の下振れリスクの上限（失敗しても被害は小さく限定的）。</summary>
        public readonly float piecemealDownsideCeiling;
        /// <summary>全体改造の下振れリスクの上限（失敗すれば壊滅的＝全体を巻き込む）。</summary>
        public readonly float utopianDownsideCeiling;
        /// <summary>漸進改革の学習速度の基礎係数（小さく試せるぶん誤りから速く学ぶ）。</summary>
        public readonly float piecemealLearningRate;
        /// <summary>全体改造の学習速度の基礎係数（一挙改造ゆえ学ぶ前に手遅れ＝学習が鈍い）。</summary>
        public readonly float utopianLearningRate;

        public PiecemealEngineeringParams(float piecemealBenefitScale, float utopianBenefitScale,
            float piecemealDownsideCeiling, float utopianDownsideCeiling,
            float piecemealLearningRate, float utopianLearningRate)
        {
            this.piecemealBenefitScale = Mathf.Max(0f, piecemealBenefitScale);
            this.utopianBenefitScale = Mathf.Max(0f, utopianBenefitScale);
            this.piecemealDownsideCeiling = Mathf.Clamp01(piecemealDownsideCeiling);
            this.utopianDownsideCeiling = Mathf.Clamp01(utopianDownsideCeiling);
            this.piecemealLearningRate = Mathf.Clamp01(piecemealLearningRate);
            this.utopianLearningRate = Mathf.Clamp01(utopianLearningRate);
        }

        /// <summary>
        /// 既定＝漸進便益0.4・全体改造便益1.0・漸進下振れ上限0.25・全体改造下振れ上限0.9・
        /// 漸進学習0.8・全体改造学習0.2。漸進は便益も被害も小さく学習が速い、全体改造は便益が大きいが
        /// 失敗が壊滅的で学習が鈍い＝ハイリスク・ハイリターンの一発勝負。
        /// </summary>
        public static PiecemealEngineeringParams Default =>
            new PiecemealEngineeringParams(0.4f, 1.0f, 0.25f, 0.9f, 0.8f, 0.2f);
    }

    /// <summary>
    /// 漸進的社会工学 vs ユートピア的社会工学の純ロジック（POPR-2 #1514・カール・ポパー『開かれた社会と
    /// その敵』）。<b>漸進的工学（piecemeal engineering）＝小さな改革を試し誤りから学ぶ</b>＝リスクを分散し
    /// 取り消し可能で、誤りを発見・修正できる。対して<b>ユートピア的工学（utopian engineering）＝理想社会を
    /// 一挙に全体改造する</b>＝全か無かで、成功すれば大きいが失敗すれば被害が壊滅的かつ不可逆で、しかも
    /// 一挙改造ゆえ学ぶ前に手遅れになる。核＝<b>不確実性が高いほど漸進が賢明</b>（分からない時は小さく試す）。
    /// 改革モードの選択でリスク分布が二様に分かれる＝漸進は低分散の積み重ね、全体改造は高分散の一発勝負。
    /// <see cref="DynastyRules"/>（王朝の制度刷新＝Reform で腐敗を下げ正統性を上げる）／
    /// <see cref="LandReformRules"/>（土地改革＝資産再分配の意欲と効率の交換）とは別＝ここは<b>漸進改革と
    /// ユートピア改造のリスク差（リスク分布の二様）</b>を扱う。同 EPIC POPR では
    /// <see cref="HistoricismTrapRules"/>（歴史主義の罠＝必然論がユートピア硬直と改革麻痺を呼ぶ）／
    /// OpennessRules（開かれた社会＝反証可能性・批判の許容）と分担する。すべて plain な float で受け渡す。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PiecemealEngineeringRules
    {
        /// <summary>
        /// 期待便益（0..）＝改革モードごとの基礎倍率×野心×能力。野心（ambition 0..1）が大きく能力
        /// （competence 0..1）が高いほど便益が伸びる。全体改造は基礎倍率が大きく<b>成功すれば大きい</b>が、
        /// それは野心と能力が揃った場合の上振れ＝同じ野心・能力でも漸進は控えめな便益にとどまる。
        /// </summary>
        public static float ExpectedBenefit(ReformMode mode, float ambition, float competence,
            PiecemealEngineeringParams p)
        {
            float amb = Mathf.Clamp01(ambition);
            float comp = Mathf.Clamp01(competence);
            float scale = mode == ReformMode.全体改造 ? p.utopianBenefitScale : p.piecemealBenefitScale;
            return Mathf.Max(0f, scale * amb * comp);
        }

        public static float ExpectedBenefit(ReformMode mode, float ambition, float competence)
            => ExpectedBenefit(mode, ambition, competence, PiecemealEngineeringParams.Default);

        /// <summary>
        /// 下振れリスク（0..1）＝失敗時の被害。複雑さ（complexity 0..1）と不確実性（uncertainty 0..1）が
        /// 高いほど大きく、モードごとの上限で頭打ち。漸進は<b>被害が小さく限定的</b>（上限が低い＝失敗しても
        /// 一部だけ）、全体改造は<b>壊滅的</b>（上限が高い＝全体を巻き込む）。複雑で不確実なほど全体改造の
        /// 下振れが牙をむく。
        /// </summary>
        public static float DownsideRisk(ReformMode mode, float complexity, float uncertainty,
            PiecemealEngineeringParams p)
        {
            float cx = Mathf.Clamp01(complexity);
            float un = Mathf.Clamp01(uncertainty);
            float ceiling = mode == ReformMode.全体改造 ? p.utopianDownsideCeiling : p.piecemealDownsideCeiling;
            // 複雑さと不確実性の平均ぶんの被害を、モード別の上限でクランプ。
            float severity = (cx + un) * 0.5f;
            return Mathf.Clamp01(ceiling * severity);
        }

        public static float DownsideRisk(ReformMode mode, float complexity, float uncertainty)
            => DownsideRisk(mode, complexity, uncertainty, PiecemealEngineeringParams.Default);

        /// <summary>
        /// 改革の取り消し可能性（0..1）。漸進は<b>高い</b>（小さく試したものは引き返せる）、全体改造は
        /// <b>不可逆</b>（社会全体を作り変えると元に戻せない）。モードのみで決まる定数写像。
        /// </summary>
        public static float Reversibility(ReformMode mode)
            => mode == ReformMode.全体改造 ? 0.1f : 0.9f;

        /// <summary>
        /// 誤りから学ぶ速さ（0..1）＝モード別の基礎学習率×フィードバック速度（feedbackSpeed 0..1）。
        /// 漸進は<b>速い</b>（小さく試せるぶん結果が早く返り誤りを修正できる）、全体改造は<b>学ぶ前に手遅れ</b>
        /// （一挙改造で結果が出る頃には引き返せない＝学習が鈍い）。フィードバックが速いほど両モードとも学べるが、
        /// 基礎率の差で漸進が常に上回る。
        /// </summary>
        public static float LearningRate(ReformMode mode, float feedbackSpeed,
            PiecemealEngineeringParams p)
        {
            float fb = Mathf.Clamp01(feedbackSpeed);
            float baseRate = mode == ReformMode.全体改造 ? p.utopianLearningRate : p.piecemealLearningRate;
            return Mathf.Clamp01(baseRate * fb);
        }

        public static float LearningRate(ReformMode mode, float feedbackSpeed)
            => LearningRate(mode, feedbackSpeed, PiecemealEngineeringParams.Default);

        /// <summary>
        /// リスク分布の分散（0..1）。同じ下振れリスクでも、漸進は<b>低分散</b>（小さく分けて試す＝結果が
        /// ばらつかず安定）、全体改造は<b>高分散の一発勝負</b>（全か無か＝当たれば大・外せば壊滅）。
        /// 全体改造は下振れリスクをそのまま分散とし、漸進はそれを大きく圧縮する（小分けが分散を均す）。
        /// 期待値が同じでも全体改造のほうがばらつきが大きい＝不確実性下では危険、を表す。
        /// </summary>
        public static float RiskDistribution(ReformMode mode, float downsideRisk)
        {
            float dr = Mathf.Clamp01(downsideRisk);
            // 漸進＝小分けで分散を圧縮（0.3倍）、全体改造＝下振れがそのまま分散として暴れる。
            float spreadFactor = mode == ReformMode.全体改造 ? 1f : 0.3f;
            return Mathf.Clamp01(dr * spreadFactor);
        }

        /// <summary>
        /// 最適モードの選択。不確実性（uncertainty 0..1）が高い・賭け金（stakes 0..1）が大きい・取り消し
        /// 可能性の必要（reversibilityNeed 0..1）が高いほど<b>漸進が最適</b>（分からない時・失敗が許されない時
        /// ほど小さく試す）。この三つの平均が閾値（0.5）以上なら漸進、低ければ（＝確実・低リスク・引き返し
        /// 不要なら）全体改造で一気に進めてよい＝<b>不確実性が高いほど漸進</b>を式に出す。
        /// </summary>
        public static ReformMode OptimalMode(float uncertainty, float stakes, float reversibilityNeed)
        {
            float un = Mathf.Clamp01(uncertainty);
            float st = Mathf.Clamp01(stakes);
            float rn = Mathf.Clamp01(reversibilityNeed);
            float caution = (un + st + rn) / 3f;
            return caution >= 0.5f ? ReformMode.漸進的 : ReformMode.全体改造;
        }

        /// <summary>
        /// ユートピアの傲慢（0..1）＝完全な理想を一挙に実現できるという思い上がり。野心（ambition 0..1）が
        /// 大きいほど、かつ知識の限界（knowledgeLimit 0..1＝自分が知らないことの大きさ）を無視するほど
        /// 強まる＝<b>知らないのに全てを設計できると信じる</b>のが傲慢の核。知識の限界が大きい（＝本当は
        /// 分かっていない）のに野心だけ高いと傲慢は最大化する。野心ゼロなら傲慢も無い。
        /// </summary>
        public static float UtopianHubris(float ambition, float knowledgeLimit)
        {
            float amb = Mathf.Clamp01(ambition);
            float limit = Mathf.Clamp01(knowledgeLimit);
            return Mathf.Clamp01(amb * limit);
        }

        /// <summary>
        /// 漸進的改革の積み重ねによる着実な改善（dt後の累積 0..1）。漸進ステップの規模（piecemealSteps 0..1）
        /// ぶんだけ少しずつ確実に積み上がる＝<b>小さな改良の積分が大きな改善になる</b>。劇的ではないが
        /// 後戻りせず安定して伸び、1で頭打ち。全体改造のような一発の跳躍ではなく、漸進の真価＝時間をかけた
        /// 確実な前進を表す。
        /// </summary>
        public static float CumulativeImprovement(float current, float piecemealSteps, float dt,
            PiecemealEngineeringParams p)
        {
            float cur = Mathf.Clamp01(current);
            float steps = Mathf.Clamp01(piecemealSteps);
            float time = Mathf.Max(0f, dt);
            // 漸進の学習率ぶんだけ着実に積む（速いフィードバックが確実な改善を生む）。
            return Mathf.Clamp01(cur + p.piecemealLearningRate * steps * time);
        }

        public static float CumulativeImprovement(float current, float piecemealSteps, float dt)
            => CumulativeImprovement(current, piecemealSteps, dt, PiecemealEngineeringParams.Default);
    }
}
