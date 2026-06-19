using UnityEngine;

namespace Ginei
{
    /// <summary>戦場昇進（武勲による抜擢・論功行賞）の調整係数。</summary>
    public readonly struct BattlefieldPromotionParams
    {
        /// <summary>武勲評価の難度の重み（難しい戦功ほど評価が伸びる寄与）。</summary>
        public readonly float difficultyWeight;
        /// <summary>武勲評価の独断の功の重み（命令外の独断専行で挙げた功への加点）。</summary>
        public readonly float initiativeWeight;
        /// <summary>抜擢の効きの強さ（武勲を現階級で割った抜擢妥当性の倍率）。</summary>
        public readonly float promotionScale;
        /// <summary>年功の主張の伸び（勤続年数1.0あたりの年功スコア。逓減はSqrtで効かせる）。</summary>
        public readonly float seniorityScale;
        /// <summary>士気高揚の最大幅（昇進×顕彰が満タンでもこのぶんまで）。</summary>
        public readonly float moraleBoostMax;
        /// <summary>忠誠の最大幅（抜擢×恩義が満タンでもこのぶんまで）。</summary>
        public readonly float loyaltyMax;
        /// <summary>不満の最大幅（功績と処遇の乖離が満タンでもこのぶんまで）。</summary>
        public readonly float resentmentMax;
        /// <summary>昇進に値する既定閾値（昇進度がこれを超えれば昇進相当）。</summary>
        public readonly float deserveThreshold;

        public BattlefieldPromotionParams(float difficultyWeight, float initiativeWeight, float promotionScale,
                                          float seniorityScale, float moraleBoostMax, float loyaltyMax,
                                          float resentmentMax, float deserveThreshold)
        {
            this.difficultyWeight = Mathf.Clamp01(difficultyWeight);
            this.initiativeWeight = Mathf.Clamp01(initiativeWeight);
            this.promotionScale = Mathf.Max(0f, promotionScale);
            this.seniorityScale = Mathf.Max(0f, seniorityScale);
            this.moraleBoostMax = Mathf.Clamp01(moraleBoostMax);
            this.loyaltyMax = Mathf.Clamp01(loyaltyMax);
            this.resentmentMax = Mathf.Clamp01(resentmentMax);
            this.deserveThreshold = Mathf.Clamp01(deserveThreshold);
        }

        /// <summary>既定＝難度重み0.5/独断重み0.3/抜擢倍率3.0/年功伸び0.4/士気高揚最大0.5/忠誠最大0.6/不満最大0.7/昇進閾値0.5。</summary>
        public static BattlefieldPromotionParams Default =>
            new BattlefieldPromotionParams(0.5f, 0.3f, 3.0f, 0.4f, 0.5f, 0.6f, 0.7f, 0.5f);
    }

    /// <summary>
    /// 戦場昇進（武勲による抜擢と論功行賞）の純ロジック。戦功の評価、抜擢昇進の妥当性、年功と実力の
    /// バランス、論功行賞の公平さ、昇進が士気と忠誠に与える効果、不当な人事への不満を扱う。
    /// ラインハルトの抜擢人事（年功を飛び越え武勲ある下位者を引き上げ、引き立てが忠誠を生む。一方で
    /// 武勲があるのに報われなければ不満が芽生える）を想定。倍率/スコアは基準値に掛けて使う（実効値
    /// パターン・基準非破壊）。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BattlefieldPromotionRules
    {
        /// <summary>
        /// 武勲評価（0..1）＝戦功×難度×独断の功。基礎の戦功（battleContribution）を、難度（difficulty）と
        /// 独断の功（initiative＝命令外の独断専行）が difficultyWeight/initiativeWeight ぶん押し上げる。
        /// 難しい戦を独断で勝った功ほど高く評価される。
        /// </summary>
        public static float MeritScore(float battleContribution, float difficulty, float initiative,
                                       BattlefieldPromotionParams p)
        {
            float baseMerit = Mathf.Clamp01(battleContribution);
            float bonus = 1f + Mathf.Clamp01(difficulty) * p.difficultyWeight
                             + Mathf.Clamp01(initiative) * p.initiativeWeight;
            return Mathf.Clamp01(baseMerit * bonus);
        }

        public static float MeritScore(float battleContribution, float difficulty, float initiative)
            => MeritScore(battleContribution, difficulty, initiative, BattlefieldPromotionParams.Default);

        /// <summary>
        /// 抜擢の妥当性（0..1）＝武勲/(1+現階級)で、下位者ほど同じ武勲で上がりやすい。currentRankTier は
        /// 現階級の序列（大きいほど上位＝伸びしろが小さい）。下士官の大功は破格の抜擢、将官の同功は
        /// 当然視される。promotionScale で倍率を効かせ 0..1 にクランプ。
        /// </summary>
        public static float PromotionMerit(float meritScore, float currentRankTier, BattlefieldPromotionParams p)
        {
            float m = Mathf.Clamp01(meritScore);
            float tier = Mathf.Max(0f, currentRankTier);
            return Mathf.Clamp01(m / (1f + tier) * p.promotionScale);
        }

        public static float PromotionMerit(float meritScore, float currentRankTier)
            => PromotionMerit(meritScore, currentRankTier, BattlefieldPromotionParams.Default);

        /// <summary>
        /// 年功の主張（0..1）＝勤続年数から積み上がる年功。Sqrt で逓減させ（古参ほど伸びが鈍る）、
        /// seniorityScale で効かせる。実力主義の対極＝実績でなく在籍年数だけで昇進を求める力。
        /// </summary>
        public static float SeniorityClaim(float yearsOfService, BattlefieldPromotionParams p)
        {
            float years = Mathf.Max(0f, yearsOfService);
            return Mathf.Clamp01(Mathf.Sqrt(years) * p.seniorityScale);
        }

        public static float SeniorityClaim(float yearsOfService)
            => SeniorityClaim(yearsOfService, BattlefieldPromotionParams.Default);

        /// <summary>
        /// 昇進度（0..1）＝抜擢妥当性（実力）と年功の主張を、実力主義度（meritocracyBias 0..1）で混合。
        /// 実力主義度1なら武勲のみ、0なら年功のみで決まる。ラインハルト型の組織は高め＝武勲が物を言う。
        /// </summary>
        public static float PromotionDecision(float promotionMerit, float seniorityClaim, float meritocracyBias,
                                              BattlefieldPromotionParams p)
        {
            float merit = Mathf.Clamp01(promotionMerit);
            float seniority = Mathf.Clamp01(seniorityClaim);
            float bias = Mathf.Clamp01(meritocracyBias);
            return Mathf.Clamp01(Mathf.Lerp(seniority, merit, bias));
        }

        public static float PromotionDecision(float promotionMerit, float seniorityClaim, float meritocracyBias)
            => PromotionDecision(promotionMerit, seniorityClaim, meritocracyBias, BattlefieldPromotionParams.Default);

        /// <summary>
        /// 顕彰による士気高揚（0..1）＝昇進度×顕彰の可視性。公の場で武勲が称えられ昇進が示されるほど
        /// 士気が上がる。陰の論功は効きが薄い（recognitionVisibility が低い）。moraleBoostMax 幅で効かせる。
        /// </summary>
        public static float MoraleBoostFromRecognition(float promotionDecision, float recognitionVisibility,
                                                       BattlefieldPromotionParams p)
        {
            float decision = Mathf.Clamp01(promotionDecision);
            float visibility = Mathf.Clamp01(recognitionVisibility);
            return Mathf.Clamp01(decision * visibility * p.moraleBoostMax);
        }

        public static float MoraleBoostFromRecognition(float promotionDecision, float recognitionVisibility)
            => MoraleBoostFromRecognition(promotionDecision, recognitionVisibility, BattlefieldPromotionParams.Default);

        /// <summary>
        /// 引き立てへの忠誠（0..1）＝抜擢（昇進度）×恩義（patronGratitude）。自分を引き上げてくれた上官への
        /// 恩義が忠誠を生む（ラインハルト×子飼いの将）。恩義を感じない者（patronGratitude 低）は抜擢しても
        /// 忠誠に変わりにくい。loyaltyMax 幅で効かせる。
        /// </summary>
        public static float LoyaltyFromPatronage(float promotionDecision, float patronGratitude,
                                                 BattlefieldPromotionParams p)
        {
            float decision = Mathf.Clamp01(promotionDecision);
            float gratitude = Mathf.Clamp01(patronGratitude);
            return Mathf.Clamp01(decision * gratitude * p.loyaltyMax);
        }

        public static float LoyaltyFromPatronage(float promotionDecision, float patronGratitude)
            => LoyaltyFromPatronage(promotionDecision, patronGratitude, BattlefieldPromotionParams.Default);

        /// <summary>
        /// 不満（0..1）＝武勲があるのに昇進しない乖離（功績と処遇のギャップ）。武勲評価が昇進度を上回る
        /// ぶんだけ立ち上がる＝功を立てたのに報われない不公平への憤り。昇進度が武勲に見合っていれば0。
        /// resentmentMax 幅で効かせる。
        /// </summary>
        public static float Resentment(float meritScore, float promotionDecision, BattlefieldPromotionParams p)
        {
            float merit = Mathf.Clamp01(meritScore);
            float decision = Mathf.Clamp01(promotionDecision);
            float gap = Mathf.Max(0f, merit - decision);
            return Mathf.Clamp01(gap * p.resentmentMax);
        }

        public static float Resentment(float meritScore, float promotionDecision)
            => Resentment(meritScore, promotionDecision, BattlefieldPromotionParams.Default);

        /// <summary>昇進に値するか＝昇進度が threshold を超えれば true。</summary>
        public static bool DeservesPromotion(float promotionDecision, float threshold)
            => Mathf.Clamp01(promotionDecision) > Mathf.Clamp01(threshold);

        /// <summary>昇進に値するか（既定閾値 deserveThreshold 委譲版）。</summary>
        public static bool DeservesPromotion(float promotionDecision)
            => DeservesPromotion(promotionDecision, BattlefieldPromotionParams.Default.deserveThreshold);
    }
}
