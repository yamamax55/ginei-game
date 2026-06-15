using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 功績昇進制の調整係数（#1064・Almagest）。戦功で貯まる功績値・昇進閾値の逓増・
    /// 階級別の最大編成数・平時減衰をまとめる。既定 <see cref="Default"/>。
    /// 基準値は非破壊（実効値パターン＝功績はローカル算出）。
    /// </summary>
    public readonly struct MeritPromotionParams
    {
        /// <summary>1回の会戦で得られる功績の基準値（勝利×貢献の満点で得る量）。</summary>
        public readonly float meritPerBattle;
        /// <summary>敗北時の功績獲得倍率（負けても貢献ぶんは僅かに残る）。</summary>
        public readonly float defeatMeritRatio;
        /// <summary>格上撃破ボーナスの最大倍率（敵戦力比が大きいほど功績が跳ねる）。</summary>
        public readonly float upsetBonusMax;
        /// <summary>昇進閾値の基準（tier1ぶんに必要な功績）。</summary>
        public readonly float baseRequiredMerit;
        /// <summary>昇進閾値の逓増係数（上の階級ほど次の昇進に多くの功績が要る＝1超で逓増）。</summary>
        public readonly float promotionEscalation;
        /// <summary>最大編成数の基準（最下級が率いる部隊数）。</summary>
        public readonly int baseFormationSize;
        /// <summary>1階級ごとに増える最大編成数の倍率（階級が上がるほど大きな部隊＝指数的に増える）。</summary>
        public readonly float formationGrowth;
        /// <summary>編成数の上限（軍集団規模＝青天井にしない）。</summary>
        public readonly int maxFormationSize;
        /// <summary>功績の平時減衰の時定数（この時間で過去の戦功が古びる＝大きいほどゆっくり）。</summary>
        public readonly float decayTime;

        public MeritPromotionParams(float meritPerBattle, float defeatMeritRatio, float upsetBonusMax,
            float baseRequiredMerit, float promotionEscalation, int baseFormationSize,
            float formationGrowth, int maxFormationSize, float decayTime)
        {
            this.meritPerBattle = meritPerBattle;
            this.defeatMeritRatio = defeatMeritRatio;
            this.upsetBonusMax = upsetBonusMax;
            this.baseRequiredMerit = baseRequiredMerit;
            this.promotionEscalation = promotionEscalation;
            this.baseFormationSize = baseFormationSize;
            this.formationGrowth = formationGrowth;
            this.maxFormationSize = maxFormationSize;
            this.decayTime = decayTime;
        }

        /// <summary>
        /// 既定係数：会戦功績10・敗北倍率0.25・格上ボーナス最大2倍・昇進基準20功績・
        /// 逓増1.6（階級ごとに必要功績が1.6倍）・基準編成1・編成成長1.7倍/級・編成上限27・
        /// 減衰時定数120（戦略秒）。
        /// </summary>
        public static MeritPromotionParams Default => new MeritPromotionParams(
            10f, 0.25f, 2f, 20f, 1.6f, 1, 1.7f, 27, 120f);
    }

    /// <summary>
    /// 功績→昇進→最大編成数の数値解決（#1064・Almagest・純ロジック test-first）。
    /// 戦功で貯まる功績値が一定値で昇進を生み、昇進が指揮できる部隊数（最大編成数）を増やす：
    /// 功績獲得(<see cref="MeritGain"/>)・昇進閾値(<see cref="MeritForNextPromotion"/>・逓増)・
    /// 昇進可否(<see cref="PromotionReady"/>)・最大編成数(<see cref="MaxFormationSize"/>)・
    /// 平時減衰(<see cref="MeritDecay"/>)・昇進頭打ち(<see cref="PromotionStagnation"/>)。
    /// すべて決定論・乱数なし・基準値非破壊。
    /// 軍功爵位（功績→爵位/士気）は <see cref="MeritRankRules"/>、階級序列は <see cref="RankSystem"/>、
    /// 指揮容量（同Wave並行）は FleetCapRules、名声（勝敗→名声）は <see cref="ReputationRules"/> が担う。
    /// 本ルールは「功績が指揮できる部隊数を増やす」軸のみを扱い、それらの並行系を作らない。
    /// </summary>
    public static class MeritPromotionRules
    {
        /// <summary>
        /// 戦功による功績の獲得（<see cref="ReputationRules"/> と同型）。勝利×貢献×格上撃破で貯まる。
        /// battleContribution(0..1)＝会戦での貢献度、enemyStrengthRatio＝敵戦力/自戦力（1超で格上）、
        /// victory＝勝利か。勝利は満額、敗北は <see cref="MeritPromotionParams.defeatMeritRatio"/> 倍。
        /// 格上撃破は <see cref="MeritPromotionParams.upsetBonusMax"/> までボーナス（劣勢ほど跳ねる）。
        /// </summary>
        public static float MeritGain(float battleContribution, float enemyStrengthRatio, bool victory,
            MeritPromotionParams prm)
        {
            float contrib = Mathf.Clamp01(battleContribution);
            float ratio = Mathf.Max(0f, enemyStrengthRatio);
            // 格上（ratio>1）ほど 1..upsetBonusMax へ近づくボーナス。同格以下は1倍。
            float upset = 1f + Mathf.Clamp01(ratio - 1f) * (prm.upsetBonusMax - 1f);
            float outcome = victory ? 1f : Mathf.Clamp01(prm.defeatMeritRatio);
            return prm.meritPerBattle * contrib * upset * outcome;
        }

        /// <summary>
        /// 次の昇進（currentRankTier → +1）に必要な累積功績。上の階級ほど多くの功績が要る＝
        /// 昇進は逓増的に難しい：baseRequiredMerit × promotionEscalation^currentRankTier。
        /// currentRankTier は0以上にクランプ。
        /// </summary>
        public static float MeritForNextPromotion(int currentRankTier, MeritPromotionParams prm)
        {
            int tier = Mathf.Max(0, currentRankTier);
            float esc = Mathf.Max(1f, prm.promotionEscalation);
            return prm.baseRequiredMerit * Mathf.Pow(esc, tier);
        }

        /// <summary>
        /// 昇進可能か（累積功績が次の昇進閾値に達したか）。
        /// </summary>
        public static bool PromotionReady(float accumulatedMerit, int currentRankTier, MeritPromotionParams prm)
        {
            float merit = Mathf.Max(0f, accumulatedMerit);
            return merit >= MeritForNextPromotion(currentRankTier, prm);
        }

        /// <summary>
        /// 階級別の最大編成数（指揮できる部隊数）。階級が上がるほど大きな部隊を率いる
        /// （中将＝艦隊・元帥＝軍集団）：baseFormationSize × formationGrowth^rankTier を
        /// <see cref="MeritPromotionParams.maxFormationSize"/> でクランプ。rankTier は0以上。
        /// </summary>
        public static int MaxFormationSize(int rankTier, MeritPromotionParams prm)
        {
            int tier = Mathf.Max(0, rankTier);
            float growth = Mathf.Max(1f, prm.formationGrowth);
            float baseSize = Mathf.Max(1, prm.baseFormationSize);
            int size = Mathf.FloorToInt(baseSize * Mathf.Pow(growth, tier));
            return Mathf.Clamp(size, prm.baseFormationSize, prm.maxFormationSize);
        }

        /// <summary>
        /// 功績の平時減衰（戦功は古びる＝過去の栄光だけでは昇進し続けられない）。
        /// peacetimeDuration（連続平時の長さ）に応じ指数減衰し、dt ぶん功績を削る。
        /// 戦時に近い（peacetimeDuration小）ほど減衰は緩く、長い平時ほど速く古びる。
        /// 返り値は減衰後の功績（0未満にならない）。
        /// </summary>
        public static float MeritDecay(float merit, float peacetimeDuration, float dt, MeritPromotionParams prm)
        {
            float m = Mathf.Max(0f, merit);
            float dur = Mathf.Max(0f, peacetimeDuration);
            float step = Mathf.Max(0f, dt);
            if (prm.decayTime <= 0f) return m;
            // 平時が長いほど減衰率が増す（dur/decayTime を係数に取る）。
            float rate = (dur / prm.decayTime) / prm.decayTime;
            float decayed = m - m * rate * step;
            return Mathf.Max(0f, decayed);
        }

        /// <summary>
        /// 昇進の頭打ち（能力や定員の天井で功績があっても昇れない＝アップオアアウトへ）。
        /// merit が requiredMerit を満たしていても、currentTier が ceilingTier 以上なら昇進不可。
        /// 「閾値到達かつ天井未満」のときだけ true（＝実際に昇進できる）を返す。
        /// 停滞＝閾値到達済みなのに天井で昇れない状況は false（RetirementRules のアップオアアウトの入力）。
        /// </summary>
        public static bool PromotionStagnation(float merit, float requiredMerit, int ceilingTier, int currentTier)
        {
            float m = Mathf.Max(0f, merit);
            float req = Mathf.Max(0f, requiredMerit);
            if (m < req) return false;          // そもそも閾値未達
            if (currentTier >= ceilingTier) return false; // 天井で頭打ち（停滞）
            return true;                         // 閾値到達かつ天井未満＝昇進できる
        }
    }
}
