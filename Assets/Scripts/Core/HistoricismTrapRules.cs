using UnityEngine;

namespace Ginei
{
    /// <summary>歴史主義の罠の調整係数（POPR-5 #1521・ポパー型）。</summary>
    public readonly struct HistoricismTrapParams
    {
        /// <summary>必然論の確信が適応拒否へ寄与する強さ（信じるほど反証を無視する）。</summary>
        public readonly float refusalScale;
        /// <summary>必然論の確信が誤り否認へ寄与する強さ（教義を守り失敗を逸脱と片付ける）。</summary>
        public readonly float denialScale;
        /// <summary>脆性の蓄積/秒の基礎係数（修正されない誤りが溜まる速さ）。</summary>
        public readonly float brittlenessRate;
        /// <summary>予言の過信の上限（歴史法則で未来を読めるという僭称の最大）。</summary>
        public readonly float overconfidenceCeiling;
        /// <summary>歴史主義崩壊の既定閾値（脆性×衝撃がこれ以上で非線形崩壊）。</summary>
        public readonly float collapseThreshold;

        public HistoricismTrapParams(float refusalScale, float denialScale, float brittlenessRate,
            float overconfidenceCeiling, float collapseThreshold)
        {
            this.refusalScale = Mathf.Clamp01(refusalScale);
            this.denialScale = Mathf.Clamp01(denialScale);
            this.brittlenessRate = Mathf.Max(0f, brittlenessRate);
            this.overconfidenceCeiling = Mathf.Clamp01(overconfidenceCeiling);
            this.collapseThreshold = Mathf.Max(0f, collapseThreshold);
        }

        /// <summary>
        /// 既定＝適応拒否寄与0.8・誤り否認寄与0.7・脆性蓄積0.1/秒・予言過信上限0.9・崩壊閾値0.5。
        /// 必然を強く信じるほど反証を無視し（0.8）誤りを認めず（0.7）、脆性が溜まって衝撃で崩れる。
        /// </summary>
        public static HistoricismTrapParams Default =>
            new HistoricismTrapParams(0.8f, 0.7f, 0.1f, 0.9f, 0.5f);
    }

    /// <summary>
    /// 歴史主義の罠の純ロジック（POPR-5 #1521・カール・ポパー『歴史主義の貧困』『開かれた社会とその敵』）。
    /// <b>歴史主義（historicism）＝歴史には不可避の法則・必然の目的地があるという信念</b>（マルクス主義や
    /// ヘーゲル的歴史哲学への批判）。必然を信じ込むほど現実の反証を無視して<b>適応を拒否</b>し（『歴史の
    /// 必然だから』と現実を無視）、失敗を『一時的逸脱』と片付けて<b>誤りを認めず</b>、修正されない誤りが
    /// <b>脆性として蓄積</b>する。やがて衝撃で非線形に崩壊する＝必然論が現実に裏切られる。歴史法則で未来を
    /// 予言できるという過信（科学の僭称）と、理想の終着点（ユートピア）へ社会を型にはめる硬直、『大きな
    /// 歴史の流れ』の前で漸進改革を無力と見なす改革麻痺を伴う。
    /// <see cref="DynastyRules"/>（天命と王朝サイクル＝腐敗の制度疲労）／<see cref="HopeRules"/>（希望と末人）
    /// とは別＝ここは<b>歴史法則信仰（必然論）が適応拒否と硬直を呼ぶ脆性</b>を扱う。同 EPIC POPR では
    /// OpennessRules（開かれた社会＝反証可能性・批判の許容）／PiecemealEngineeringRules（漸進的社会工学＝
    /// 部分改良の積み重ね＝本罠 ReformParalysis の対極）／InstitutionalCorrectionRules（制度的な誤りの蓄積と
    /// 修正）と分担する。すべて plain な float で受け渡す。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HistoricismTrapRules
    {
        /// <summary>
        /// 歴史主義の確信（0..1）＝法則信仰×イデオロギーの硬さ。歴史の必然を信じ込む度合いは、必然論への
        /// 確信とイデオロギーの硬直の<b>両方</b>が要る＝どちらかが緩めば確信も緩む（積で表す）。
        /// </summary>
        public static float HistoricistConviction(float determinismBelief, float ideologicalRigidity)
        {
            float belief = Mathf.Clamp01(determinismBelief);
            float rigidity = Mathf.Clamp01(ideologicalRigidity);
            return Mathf.Clamp01(belief * rigidity);
        }

        /// <summary>
        /// 適応拒否（0..1）＝確信×反証の強さ×寄与係数。必然を信じるほど、目の前の反証が強いほど現実を無視して
        /// 適応を拒む（『歴史の必然だから』）＝<b>反証が強いのに適応しない</b>のが罠の核。確信ゼロなら反証に
        /// 素直に適応する（拒否0）。
        /// </summary>
        public static float AdaptationRefusal(float historicistConviction, float contradictingEvidence,
            HistoricismTrapParams p)
        {
            float conv = Mathf.Clamp01(historicistConviction);
            float evidence = Mathf.Clamp01(contradictingEvidence);
            return Mathf.Clamp01(conv * evidence * p.refusalScale);
        }

        public static float AdaptationRefusal(float historicistConviction, float contradictingEvidence)
            => AdaptationRefusal(historicistConviction, contradictingEvidence, HistoricismTrapParams.Default);

        /// <summary>
        /// 誤り否認（0..1）＝確信×誤りの可視性×寄与係数。失敗が見えているほど・確信が強いほど教義を守って
        /// 誤りを『一時的逸脱』と片付ける＝<b>見えている失敗ほど否認が強まる</b>（教義を守るための否認）。
        /// 確信ゼロなら可視の誤りを率直に認める（否認0）。
        /// </summary>
        public static float ErrorDenial(float historicistConviction, float errorVisibility,
            HistoricismTrapParams p)
        {
            float conv = Mathf.Clamp01(historicistConviction);
            float visibility = Mathf.Clamp01(errorVisibility);
            return Mathf.Clamp01(conv * visibility * p.denialScale);
        }

        public static float ErrorDenial(float historicistConviction, float errorVisibility)
            => ErrorDenial(historicistConviction, errorVisibility, HistoricismTrapParams.Default);

        /// <summary>
        /// 脆性の蓄積（dt後の brittleness 0..1）＝適応拒否と誤り否認の平均ぶんだけ脆性が溜まる。適応を拒み
        /// 誤りを認めないほど、修正されない誤りが脆性として積み上がる＝<b>硬直は脆さを蓄える</b>。両方ゼロなら
        /// 蓄積しない。1で頭打ち（自然回復はしない＝罠は能動的な刷新でしか抜けられない）。
        /// </summary>
        public static float BrittlenessAccumulation(float adaptationRefusal, float errorDenial, float brittleness,
            float dt, HistoricismTrapParams p)
        {
            float b = Mathf.Clamp01(brittleness);
            float refusal = Mathf.Clamp01(adaptationRefusal);
            float denial = Mathf.Clamp01(errorDenial);
            float step = Mathf.Max(0f, dt);
            float drift = (refusal + denial) * 0.5f;
            return Mathf.Clamp01(b + p.brittlenessRate * drift * step);
        }

        public static float BrittlenessAccumulation(float adaptationRefusal, float errorDenial, float brittleness, float dt)
            => BrittlenessAccumulation(adaptationRefusal, errorDenial, brittleness, dt, HistoricismTrapParams.Default);

        /// <summary>
        /// 予言の過信（0..1）＝歴史法則で未来を予言できるという過信（科学を僭称する）。確信に比例し、上限
        /// <see cref="HistoricismTrapParams.overconfidenceCeiling"/>で頭打ち＝どれほど信じても確実な予言には
        /// ならない（だが本人はそう思い込む）。
        /// </summary>
        public static float PredictionOverconfidence(float historicistConviction, HistoricismTrapParams p)
        {
            float conv = Mathf.Clamp01(historicistConviction);
            return Mathf.Clamp01(conv * p.overconfidenceCeiling);
        }

        public static float PredictionOverconfidence(float historicistConviction)
            => PredictionOverconfidence(historicistConviction, HistoricismTrapParams.Default);

        /// <summary>
        /// ユートピア硬直（0..1）＝確信×終着点ビジョンの明確さ。理想の終着点（ユートピア）を鮮明に描くほど、
        /// その型へ社会を無理にはめ込もうとする硬直が強まる＝<b>終着点を信じるほど現在を犠牲にする</b>。
        /// ビジョンが曖昧なら（または確信が無ければ）硬直しない。
        /// </summary>
        public static float UtopianRigidity(float historicistConviction, float endStateVision)
        {
            float conv = Mathf.Clamp01(historicistConviction);
            float vision = Mathf.Clamp01(endStateVision);
            return Mathf.Clamp01(conv * vision);
        }

        /// <summary>
        /// 改革麻痺（0..1）＝適応拒否そのもの。『大きな歴史の流れ』の前で漸進改革（PiecemealEngineering）を
        /// 無力と見なし放棄する度合い＝適応を拒むほど部分改良も諦める。恒等写像として置き、呼び出し側が
        /// 改革実行率（1−これ）へ写す＝<b>必然論は手を動かす改革を麻痺させる</b>。
        /// </summary>
        public static float ReformParalysis(float adaptationRefusal) => Mathf.Clamp01(adaptationRefusal);

        /// <summary>
        /// 歴史主義崩壊の判定（true＝蓄積した脆性が衝撃で崩壊）。脆性×衝撃が閾値以上で非線形に崩れる＝
        /// 必然論が現実に裏切られる瞬間。脆性が低ければ多少の衝撃は耐え、脆性が高いほど小さな衝撃でも崩れる
        /// （硬直した必然論ほど脆い）。
        /// </summary>
        public static bool IsHistoricistCollapse(float brittlenessAccumulation, float shock, float threshold)
        {
            float b = Mathf.Clamp01(brittlenessAccumulation);
            float s = Mathf.Clamp01(shock);
            float th = Mathf.Max(0f, threshold);
            return b * s >= th;
        }

        public static bool IsHistoricistCollapse(float brittlenessAccumulation, float shock)
            => IsHistoricistCollapse(brittlenessAccumulation, shock, HistoricismTrapParams.Default.collapseThreshold);
    }
}
