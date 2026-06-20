using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 偽装退却（敗走を装う計略）の調整値（#偽装退却）。演技の効き・釣られた敵の崩れ勾配・反転打撃の伏兵ボーナス・見破りリスクの勾配。
    /// すべて ctor で Clamp し、極端な入力でも収支が破綻しない（実効値パターン＝基準値非破壊）。
    /// </summary>
    public readonly struct FeignedRetreatParams
    {
        /// <summary>退却演技の真に迫る度における「演技の質」の重み（残りは自軍規律の重み）。</summary>
        public readonly float realismWeight;
        /// <summary>釣られた敵が追撃距離1単位あたり隊形を崩す勾配（深追いほど崩れる）。</summary>
        public readonly float pursuitDisorderScale;
        /// <summary>反転攻撃時に伏兵がもたらす打撃の上乗せ係数（伏兵規模1で +この値）。</summary>
        public readonly float ambushBonus;
        /// <summary>偽装を見破られるリスクの基準勾配（演技が拙いほど・敵の情報が鋭いほど上がる）。</summary>
        public readonly float detectionScale;
        /// <summary>見破られた時の逆効果の上限（深入りしていてもこれ以上の損は出さない＝暴走防止）。</summary>
        public readonly float maxBackfire;

        public FeignedRetreatParams(
            float realismWeight,
            float pursuitDisorderScale,
            float ambushBonus,
            float detectionScale,
            float maxBackfire)
        {
            this.realismWeight = Mathf.Clamp01(realismWeight);
            this.pursuitDisorderScale = Mathf.Max(0f, pursuitDisorderScale);
            this.ambushBonus = Mathf.Max(0f, ambushBonus);
            this.detectionScale = Mathf.Max(0f, detectionScale);
            this.maxBackfire = Mathf.Clamp01(maxBackfire);
        }

        /// <summary>
        /// 既定：演技の質の重み0.6（残り0.4が自軍規律）・追撃崩れ0.1/単位・伏兵上乗せ1.0・見破り勾配0.5・逆効果上限0.9。
        /// </summary>
        public static FeignedRetreatParams Default => new FeignedRetreatParams(
            DefaultRealismWeight,
            DefaultPursuitDisorderScale,
            DefaultAmbushBonus,
            DefaultDetectionScale,
            DefaultMaxBackfire);

        public const float DefaultRealismWeight = 0.6f;
        public const float DefaultPursuitDisorderScale = 0.1f;
        public const float DefaultAmbushBonus = 1.0f;
        public const float DefaultDetectionScale = 0.5f;
        public const float DefaultMaxBackfire = 0.9f;
    }

    /// <summary>
    /// 偽装退却＝敗走を装い敵を誘い込む高等戦術（#偽装退却・Core 純ロジック・test-first・盤面非依存）。
    /// モンゴル軍／ハンニバル（カンナエ）型＝わざと退却して見せ、敵が秩序を崩して追撃してきたところを反転して叩く。
    /// 成功には演技の巧みさ（秩序ある偽装＝統率）・敵の油断（攻撃的で規律が低い）・伏兵の配置が要る。
    /// 見破られると本当に不利になる（追撃側でなく逃げる側に回る＝逆効果）。
    /// <b>本物の撤退判断・離脱方向の <see cref="BattleWithdrawalRules"/> とは別＝退却を「装う」計略</b>／
    /// <b>追撃する側の損害収支 <see cref="PursuitBattleRules"/> とは別＝こちらは「誘い込む」側</b>／
    /// <b>伏兵そのものの <see cref="AmbushRules"/> とは連携するが別＝偽装退却に特化</b>。
    /// 各メソッドは Params 明示版と既定 Params 委譲版を持つ。入力はクランプし、乱数は使わない（必要な確率は呼び出し側が roll を渡す）。
    /// </summary>
    public static class FeignedRetreatRules
    {
        // --- 退却演技の真に迫る度 ---

        /// <summary>既定パラメータで演技の真に迫る度を返す。</summary>
        public static float Convincingness(float retreatRealism, float ownDiscipline)
            => Convincingness(retreatRealism, ownDiscipline, FeignedRetreatParams.Default);

        /// <summary>
        /// 退却演技の真に迫る度（0..1）。演技の質（崩れて見えて実は秩序立っている）と自軍規律の加重和。
        /// 統率が高いほど「秩序ある偽装」ができる＝バラけた本物の敗走に見せつつ反転できる。
        /// `conv = clamp01(realism * realismWeight + (discipline/100) * (1 - realismWeight))`。
        /// </summary>
        public static float Convincingness(float retreatRealism, float ownDiscipline, FeignedRetreatParams p)
        {
            float realism = Mathf.Clamp01(retreatRealism);
            float discipline = Mathf.Clamp(ownDiscipline, 0f, 100f) / 100f;
            return Mathf.Clamp01(realism * p.realismWeight + discipline * (1f - p.realismWeight));
        }

        // --- 敵が食いつく度合い ---

        /// <summary>
        /// 敵が偽装退却に食いつく度合い（0..1）。演技の真に迫る度 × 敵の攻撃性 × 敵の規律の低さ。
        /// 攻撃的で規律の低い敵ほど秩序を捨てて追ってくる。規律が高い敵は食いつかない（積＝すべて要る）。
        /// `bait = clamp01(conv * aggression * (1 - enemyDiscipline))`。
        /// </summary>
        public static float EnemyTakesBait(float convincingness, float enemyAggression, float enemyDiscipline)
        {
            float conv = Mathf.Clamp01(convincingness);
            float aggression = Mathf.Clamp01(enemyAggression);
            float discipline = Mathf.Clamp01(enemyDiscipline);
            return Mathf.Clamp01(conv * aggression * (1f - discipline));
        }

        // --- 釣られた敵の隊形崩れ ---

        /// <summary>既定パラメータで追撃側の崩れを返す。</summary>
        public static float PursuerDisorder(float enemyTakesBait, float pursuitDistance)
            => PursuerDisorder(enemyTakesBait, pursuitDistance, FeignedRetreatParams.Default);

        /// <summary>
        /// 釣られた敵が追撃で隊形を崩す度合い（0..1）。食いつき度 × 追撃距離（深追いほど伸びきって崩れる）。
        /// `disorder = clamp01(bait * clamp01(distance * pursuitDisorderScale))`。距離0なら崩れない（追ってこなければ崩れない）。
        /// </summary>
        public static float PursuerDisorder(float enemyTakesBait, float pursuitDistance, FeignedRetreatParams p)
        {
            float bait = Mathf.Clamp01(enemyTakesBait);
            float depth = Mathf.Max(0f, pursuitDistance);
            float depthFactor = Mathf.Clamp01(depth * p.pursuitDisorderScale);
            return Mathf.Clamp01(bait * depthFactor);
        }

        // --- 反転攻撃＋伏兵の打撃 ---

        /// <summary>既定パラメータで反転打撃を返す。</summary>
        public static float ReversalImpact(float pursuerDisorder, float ambushStrength)
            => ReversalImpact(pursuerDisorder, ambushStrength, FeignedRetreatParams.Default);

        /// <summary>
        /// 反転攻撃＋伏兵の打撃（≥0）。崩れた追撃側へ向き直って叩く威力に、配置済み伏兵が上乗せする。
        /// `impact = disorder * (1 + ambushStrength * ambushBonus)`。崩れていなければ（disorder=0）伏兵があっても打撃0。
        /// </summary>
        public static float ReversalImpact(float pursuerDisorder, float ambushStrength, FeignedRetreatParams p)
        {
            float disorder = Mathf.Clamp01(pursuerDisorder);
            float ambush = Mathf.Clamp01(ambushStrength);
            return Mathf.Max(0f, disorder * (1f + ambush * p.ambushBonus));
        }

        // --- 偽装を見破られるリスク ---

        /// <summary>既定パラメータで見破りリスクを返す。</summary>
        public static float DetectionRisk(float retreatRealism, float enemyIntel)
            => DetectionRisk(retreatRealism, enemyIntel, FeignedRetreatParams.Default);

        /// <summary>
        /// 偽装を見破られるリスク（0..1）。演技が拙い（realism が低い）ほど、敵の情報が鋭いほど見抜かれる。
        /// `risk = clamp01((1 - realism) * enemyIntel * detectionScale)`。完璧な演技（realism=1）なら見破られない。
        /// </summary>
        public static float DetectionRisk(float retreatRealism, float enemyIntel, FeignedRetreatParams p)
        {
            float realism = Mathf.Clamp01(retreatRealism);
            float intel = Mathf.Clamp01(enemyIntel);
            return Mathf.Clamp01((1f - realism) * intel * p.detectionScale);
        }

        // --- 見破られた時の逆効果 ---

        /// <summary>既定パラメータで逆効果を返す。</summary>
        public static float BackfireLoss(float detectionRisk, float ownCommitment)
            => BackfireLoss(detectionRisk, ownCommitment, FeignedRetreatParams.Default);

        /// <summary>
        /// 見破られた時の逆効果（0..1）。見破られると「装った退却」が本当の退却扱いになり不利を被る。
        /// 深く退却に踏み込んでいる（commitment が高い）ほど立て直しが利かず損が大きい。
        /// `loss = clamp(detectionRisk * commitment, 0, maxBackfire)`。
        /// </summary>
        public static float BackfireLoss(float detectionRisk, float ownCommitment, FeignedRetreatParams p)
        {
            float risk = Mathf.Clamp01(detectionRisk);
            float commitment = Mathf.Clamp01(ownCommitment);
            return Mathf.Clamp(risk * commitment, 0f, p.maxBackfire);
        }

        // --- 偽装退却の正味価値 ---

        /// <summary>
        /// 偽装退却の正味価値＝反転打撃から見破られた時の逆効果を差し引いた値（負もありうる）。
        /// `net = reversalImpact - backfireLoss`。見破りリスクが高いと正味は負＝仕掛けるべきでない。
        /// </summary>
        public static float FeintNetValue(float reversalImpact, float backfireLoss)
        {
            float impact = Mathf.Max(0f, reversalImpact);
            float loss = Mathf.Clamp01(backfireLoss);
            return impact - loss;
        }

        // --- 罠にかかったか判定 ---

        /// <summary>既定の閾値（0.5）で敵が罠にかかったかを返す。</summary>
        public static bool IsBaitTaken(float enemyTakesBait)
            => IsBaitTaken(enemyTakesBait, DefaultBaitThreshold);

        /// <summary>
        /// 敵が罠にかかったか（true＝食いつき度が閾値超＝反転して叩ける）。食いつきが浅い敵は誘い込めていない。
        /// </summary>
        public static bool IsBaitTaken(float enemyTakesBait, float threshold)
        {
            float bait = Mathf.Clamp01(enemyTakesBait);
            float t = Mathf.Clamp01(threshold);
            return bait > t;
        }

        /// <summary>敵が罠にかかったと見なす既定の食いつき閾値。</summary>
        public const float DefaultBaitThreshold = 0.5f;
    }
}
