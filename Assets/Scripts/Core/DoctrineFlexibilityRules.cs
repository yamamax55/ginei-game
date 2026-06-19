using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ドクトリンの柔軟性（規律と即応のトレードオフ）の調整値。柔軟性が将校の主導性をどれだけ後押しするか・
    /// 硬直の統一性下限（決まった戦法しか取らない軍でも崩れない最低限の確実性）・柔軟ドクトリンが要する統制コストの
    /// 強さ・最適柔軟性が変動性へ追従する度合い。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct DoctrineFlexibilityParams
    {
        /// <summary>柔軟性が将校の主導性と掛け合わさって適応力を生む重み（高いほど主導性が効く）。</summary>
        public readonly float initiativeWeight;
        /// <summary>硬直ドクトリンの統一性下限（柔軟0でも実行の確実性はこの値を下回らない）。</summary>
        public readonly float rigidityFloor;
        /// <summary>柔軟ドクトリンの統制コスト指数（2.0＝柔軟すぎると散漫リスクが二乗的に増える）。</summary>
        public readonly float coordinationExponent;
        /// <summary>最適柔軟性が状況変動性へ追従する強さ（高いほど変動の大きい戦場で柔軟性を要求）。</summary>
        public readonly float volatilityResponse;

        public DoctrineFlexibilityParams(float initiativeWeight, float rigidityFloor,
            float coordinationExponent, float volatilityResponse)
        {
            this.initiativeWeight = Mathf.Clamp01(initiativeWeight);
            this.rigidityFloor = Mathf.Clamp01(rigidityFloor);
            this.coordinationExponent = Mathf.Clamp(coordinationExponent, 0.5f, 4f);
            this.volatilityResponse = Mathf.Clamp01(volatilityResponse);
        }

        /// <summary>既定：主導性重み0.5・硬直下限0.3・統制コスト指数2.0（二乗）・変動追従0.8。</summary>
        public static DoctrineFlexibilityParams Default => new DoctrineFlexibilityParams(
            DefaultInitiativeWeight, DefaultRigidityFloor,
            DefaultCoordinationExponent, DefaultVolatilityResponse);

        public const float DefaultInitiativeWeight = 0.5f;
        public const float DefaultRigidityFloor = 0.3f;
        public const float DefaultCoordinationExponent = 2.0f;
        public const float DefaultVolatilityResponse = 0.8f;
    }

    /// <summary>
    /// ドクトリンの柔軟性 vs 硬直の純ロジック（唯一の窓口）＝<b>規律と即応のトレードオフ</b>。
    /// 硬直したドクトリン（決まった戦法しかしない軍）は規律・統一性・実行の確実性が高いが、予期せぬ事態に弱い。
    /// 柔軟なドクトリン（状況対応）は不意の事態に強いが、まとめるのに統率を要し、柔軟すぎると組織が散漫になる。
    /// <b>分担</b>：<see cref="FormationDoctrineRules"/>（戦況に応じた陣形の自動切替）とは別＝こちらは
    /// 「ドクトリンの柔軟性」という<b>組織特性</b>そのもの。<see cref="MissionCommandRules"/>（任務戦術＝Auftragstaktik）
    /// が機能する<b>前提</b>となる柔軟性・将校の主導性を扱う（硬直軍では下級の裁量が働かない）。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class DoctrineFlexibilityRules
    {
        /// <summary>既定パラメータで予期せぬ事態への適応力。</summary>
        public static float Adaptability(float doctrineFlexibility, float officerInitiative)
            => Adaptability(doctrineFlexibility, officerInitiative, DoctrineFlexibilityParams.Default);

        /// <summary>
        /// 予期せぬ事態への適応力（0..1）。柔軟なドクトリンが土台で、将校の主導性が掛け合わさって伸びる。
        /// adapt＝柔軟性×(1−重み＋重み×主導性)。主導性が高いほど柔軟性が活きる＝現場が自分で判断して対応する。
        /// 主導性0でも柔軟性ぶんは残る（重み0.5なら柔軟性の半分）が、硬直（柔軟0）なら主導性が高くても0。
        /// </summary>
        public static float Adaptability(float doctrineFlexibility, float officerInitiative, DoctrineFlexibilityParams p)
        {
            float flex = Mathf.Clamp01(doctrineFlexibility);
            float init = Mathf.Clamp01(officerInitiative);
            return Mathf.Clamp01(flex * (1f - p.initiativeWeight + p.initiativeWeight * init));
        }

        /// <summary>既定パラメータで硬直ドクトリンの統一性・実行の確実性。</summary>
        public static float RigidityStrength(float doctrineRigidity)
            => RigidityStrength(doctrineRigidity, DoctrineFlexibilityParams.Default);

        /// <summary>
        /// 硬直ドクトリンの統一性・実行の確実性（0..1）。規律で決まった戦法を寸分違わず実行する強み。
        /// strength＝下限＋(1−下限)×硬直度。硬直0でも下限ぶんの確実性はある（最低限の規律）、硬直1で1.0。
        /// 硬直軍の美徳＝想定どおりの局面では迷いなく強い（半面、想定外には脆い→NovelSituationPenalty）。
        /// </summary>
        public static float RigidityStrength(float doctrineRigidity, DoctrineFlexibilityParams p)
        {
            float rigid = Mathf.Clamp01(doctrineRigidity);
            return Mathf.Clamp01(p.rigidityFloor + (1f - p.rigidityFloor) * rigid);
        }

        /// <summary>
        /// 硬直軍が新奇な状況で機能不全になる度合い（0..1）。硬直しているほど、かつ状況が新奇なほど大きい。
        /// penalty＝硬直度×新奇度。決まった戦法しか持たない軍は前例のない事態でフリーズする（教条主義の弱点）。
        /// 柔軟（硬直0）なら新奇でも機能不全0、見慣れた状況（新奇0）なら硬直軍でも問題なし。
        /// </summary>
        public static float NovelSituationPenalty(float doctrineRigidity, float situationNovelty)
        {
            float rigid = Mathf.Clamp01(doctrineRigidity);
            float novelty = Mathf.Clamp01(situationNovelty);
            return Mathf.Clamp01(rigid * novelty);
        }

        /// <summary>既定パラメータで柔軟ドクトリンが要する統制コスト。</summary>
        public static float CoordinationCost(float doctrineFlexibility)
            => CoordinationCost(doctrineFlexibility, DoctrineFlexibilityParams.Default);

        /// <summary>
        /// 柔軟ドクトリンが要する統制コスト＝散漫リスク（0..1）。柔軟すぎると各隊がバラバラに動き統率が要る。
        /// cost＝pow(柔軟性, 指数)。指数2.0なら柔軟0.5で0.25・柔軟1.0で1.0＝散漫さは加速度的に増す
        /// （ほどほどの柔軟は安いが、振り切れた柔軟は高くつく）。硬直（柔軟0）はコスト0＝統一が取れている。
        /// </summary>
        public static float CoordinationCost(float doctrineFlexibility, DoctrineFlexibilityParams p)
        {
            float flex = Mathf.Clamp01(doctrineFlexibility);
            return Mathf.Clamp01(Mathf.Pow(flex, p.coordinationExponent));
        }

        /// <summary>
        /// ドクトリンと状況の噛み合い（0..1）。種別が一致すれば1.0、外れるほど低い。
        /// 一致＝得意な状況に当たった（理想）、距離が開くほど不向きな局面（噛み合わない）。
        /// match＝1−|種別−状況|/max(種別数,1)。同一で1、最も離れて 1−(N−1)/N。
        /// </summary>
        public static float DoctrineMatch(int doctrineType, int situationType, int typeCount = 4)
        {
            int count = Mathf.Max(1, typeCount);
            int d = Mathf.Clamp(doctrineType, 0, count - 1);
            int s = Mathf.Clamp(situationType, 0, count - 1);
            float dist = Mathf.Abs(d - s);
            return Mathf.Clamp01(1f - dist / count);
        }

        /// <summary>既定パラメータで柔軟な軍が経験から学ぶ速さ。</summary>
        public static float LearningRate(float doctrineFlexibility, float experience)
            => LearningRate(doctrineFlexibility, experience, DoctrineFlexibilityParams.Default);

        /// <summary>
        /// 柔軟な軍ほど経験から学んでドクトリンが進化する速さ（0..1）。柔軟性と経験の積（幾何平均的な相乗）。
        /// rate＝柔軟性×経験。柔軟な軍は失敗から学び戦法を更新する＝硬直軍（柔軟0）は経験を積んでも教義が固まり学ばない。
        /// 柔軟でも経験0なら学びの土台が無い（0）。両方高いとき最も速く進化する。
        /// </summary>
        public static float LearningRate(float doctrineFlexibility, float experience, DoctrineFlexibilityParams p)
        {
            float flex = Mathf.Clamp01(doctrineFlexibility);
            float exp = Mathf.Clamp01(experience);
            return Mathf.Clamp01(flex * exp);
        }

        /// <summary>既定パラメータで状況に応じた最適な柔軟性。</summary>
        public static float OptimalFlexibility(float situationVolatility, float commandQuality)
            => OptimalFlexibility(situationVolatility, commandQuality, DoctrineFlexibilityParams.Default);

        /// <summary>
        /// 状況の変動性と指揮の質に応じた最適な柔軟性（0..1）。変動の大きい戦場ほど柔軟性が要り、
        /// 指揮が高いほど散漫リスク（統制コスト）を負ってでも柔軟性を活かせる。
        /// optimal＝変動性×追従強さ×指揮の質。荒れた戦場×名将は柔軟（高）を志向、静的な戦場や凡将は規律（低）を志向
        /// ＝柔軟は誰でも扱える美徳ではなく、統率に支えられて初めて利益になる。
        /// </summary>
        public static float OptimalFlexibility(float situationVolatility, float commandQuality, DoctrineFlexibilityParams p)
        {
            float vol = Mathf.Clamp01(situationVolatility);
            float cmd = Mathf.Clamp01(commandQuality);
            return Mathf.Clamp01(vol * p.volatilityResponse * cmd);
        }

        /// <summary>
        /// 教条主義的失敗に陥ったか（bool）＝硬直軍が新奇な状況で機能不全のしきい値を超えたか。
        /// 硬直度×新奇度がしきい値（既定0.5）以上で true＝決まった戦法に固執して前例のない事態に対応できず崩れた。
        /// </summary>
        public static bool IsDoctrinaireFailure(float doctrineRigidity, float situationNovelty, float threshold = 0.5f)
        {
            float penalty = NovelSituationPenalty(doctrineRigidity, situationNovelty);
            return penalty >= Mathf.Clamp01(threshold);
        }
    }
}
