using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 対偵察（カウンターリコン）の調整係数。
    /// 敵の偵察艦・偵察衛星を能動的に狩り、敵を「盲目」にして自軍の動きを隠すレイヤーの数式パラメータ。
    /// </summary>
    public readonly struct CounterReconParams
    {
        /// <summary>敵偵察の回避力の重み（大きいほど回避が効いて狩りにくい）。</summary>
        public readonly float evasionWeight;
        /// <summary>敵を盲目にできる度合いの上限（1.0でも完全な暗闇にはしない）。</summary>
        public readonly float blindCeiling;
        /// <summary>情報陳腐化の速さ（盲目1あたり・経過時間1あたりに古びる係数）。</summary>
        public readonly float stalenessRate;
        /// <summary>自軍の動きが隠れる最低保証（敵が盲目でも完全には隠れない床）。</summary>
        public readonly float concealFloor;
        /// <summary>偵察と対偵察の削り合いの係数（敵偵察比1あたりに対偵察が失う割合）。</summary>
        public readonly float attritionRate;
        /// <summary>情報優位における敵盲目の寄与重み（自軍可視と敵盲目の合成比）。</summary>
        public readonly float dominanceWeight;

        public CounterReconParams(float evasionWeight, float blindCeiling, float stalenessRate,
            float concealFloor, float attritionRate, float dominanceWeight)
        {
            this.evasionWeight = Mathf.Max(0f, evasionWeight);
            this.blindCeiling = Mathf.Clamp01(blindCeiling);
            this.stalenessRate = Mathf.Max(0f, stalenessRate);
            this.concealFloor = Mathf.Clamp01(concealFloor);
            this.attritionRate = Mathf.Max(0f, attritionRate);
            this.dominanceWeight = Mathf.Clamp01(dominanceWeight);
        }

        /// <summary>既定＝回避重み1.0・盲目上限0.9・陳腐化0.05/秒・隠蔽床0.1・削り係数0.5・優位重み0.5。</summary>
        public static CounterReconParams Default =>
            new CounterReconParams(1f, 0.9f, 0.05f, 0.1f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 対偵察の純ロジック＝敵の偵察を排除し情報を遮断して敵を盲目にする。
    /// <para>分担：偵察そのもの（敵を見る・霧）＝ <see cref="ReconRules"/> の「対（つい）」の概念で、こちらは敵偵察の能動的な排除。
    /// 前衛が本隊を隠す受動的な偵察幕＝ ScreeningRules、固定の哨戒線＝ SensorPicketRules とは別＝
    /// 本ルールは「敵の偵察戦力を狩り、敵を盲目にする」能動的な対偵察に特化する。</para>
    /// 盤面非依存の plain 引数のみ。乱数を持たず（必要なら roll を外から与える）、入力はクランプ。
    /// 実効値パターン（基準戦力は非破壊・倍率/割合を返す）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CounterReconRules
    {
        /// <summary>
        /// 敵偵察を狩る効率 0..1。投入した対偵察戦力 counterReconStrength が、
        /// 敵偵察の回避 enemyScoutEvasion(0..1)×evasionWeight ぶん割り引かれた「実効的な狩り力」の飽和比。
        /// 対偵察ゼロで 0、回避が高いほど効率は落ちる。
        /// </summary>
        public static float ScoutHuntingEffectiveness(float counterReconStrength, float enemyScoutEvasion, CounterReconParams p)
        {
            float cr = Mathf.Max(0f, counterReconStrength);
            float evade = Mathf.Clamp01(enemyScoutEvasion);
            // 回避が高いほど分母が増え、効率が下がる（飽和比）。
            float denom = cr + 1f + p.evasionWeight * evade * (cr + 1f);
            return Mathf.Clamp01(cr / denom);
        }

        public static float ScoutHuntingEffectiveness(float counterReconStrength, float enemyScoutEvasion)
            => ScoutHuntingEffectiveness(counterReconStrength, enemyScoutEvasion, CounterReconParams.Default);

        /// <summary>
        /// 敵を盲目にする度合い 0..blindCeiling。敵偵察を狩る効率に比例して情報を遮断する。
        /// 上限 blindCeiling まで（完全な暗闇にはしない）。
        /// </summary>
        public static float EnemyBlinding(float scoutHuntingEffectiveness, CounterReconParams p)
        {
            float eff = Mathf.Clamp01(scoutHuntingEffectiveness);
            return p.blindCeiling * eff;
        }

        public static float EnemyBlinding(float scoutHuntingEffectiveness)
            => EnemyBlinding(scoutHuntingEffectiveness, CounterReconParams.Default);

        /// <summary>
        /// 敵情報の陳腐化 0..1＝敵は古い情報で戦うしかなくなる度合い。
        /// 盲目度 enemyBlinding が高いほど・最後に偵察できてからの経過 timeSinceLastIntel(秒) が長いほど古びる。
        /// </summary>
        public static float EnemyIntelStaleness(float enemyBlinding, float timeSinceLastIntel, CounterReconParams p)
        {
            float blind = Mathf.Clamp01(enemyBlinding);
            float t = Mathf.Max(0f, timeSinceLastIntel);
            return Mathf.Clamp01(blind * p.stalenessRate * t);
        }

        public static float EnemyIntelStaleness(float enemyBlinding, float timeSinceLastIntel)
            => EnemyIntelStaleness(enemyBlinding, timeSinceLastIntel, CounterReconParams.Default);

        /// <summary>
        /// 自軍の動きが隠れる度合い 0..1。敵を盲目にする（enemyBlinding）ほど隠れ、
        /// さらに自軍の電波管制 ownEmcon(0..1)で上積みする。concealFloor を下限に保証。
        /// </summary>
        public static float OwnMovementConcealment(float enemyBlinding, float ownEmcon, CounterReconParams p)
        {
            float blind = Mathf.Clamp01(enemyBlinding);
            float emcon = Mathf.Clamp01(ownEmcon);
            // 盲目と電波管制の独立な「見えなさ」を合成（1-(1-a)(1-b)）。
            float concealed = 1f - (1f - blind) * (1f - emcon);
            return Mathf.Clamp01(Mathf.Max(p.concealFloor, concealed));
        }

        public static float OwnMovementConcealment(float enemyBlinding, float ownEmcon)
            => OwnMovementConcealment(enemyBlinding, ownEmcon, CounterReconParams.Default);

        /// <summary>
        /// 対偵察に割く戦力の機会費用 0..1＝主力から抜いた割合。
        /// counterReconStrength / totalStrength。総戦力0なら0。
        /// </summary>
        public static float CounterReconCost(float counterReconStrength, float totalStrength)
        {
            float cr = Mathf.Max(0f, counterReconStrength);
            float total = Mathf.Max(0f, totalStrength);
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(cr / total);
        }

        /// <summary>
        /// 偵察と対偵察の削り合いで、対偵察側が1ティックに失う戦力の割合 0..1。
        /// 敵偵察戦力 enemyScoutStrength が対偵察 counterReconStrength に対して相対的に多いほど削られる。
        /// </summary>
        public static float ReconScreenAttrition(float counterReconStrength, float enemyScoutStrength, CounterReconParams p)
        {
            float cr = Mathf.Max(0f, counterReconStrength);
            float es = Mathf.Max(0f, enemyScoutStrength);
            float ratio = es / (cr + 1f); // +1 でゼロ割回避（対偵察1単位を最小防壁とみなす）。
            return Mathf.Clamp01(p.attritionRate * ratio);
        }

        public static float ReconScreenAttrition(float counterReconStrength, float enemyScoutStrength)
            => ReconScreenAttrition(counterReconStrength, enemyScoutStrength, CounterReconParams.Default);

        /// <summary>
        /// 情報優位 0..1＝こちらは見え（ownRecon 0..1）敵は盲目（enemyBlinding 0..1）という非対称。
        /// dominanceWeight で自軍可視と敵盲目を合成。両方高いと1に近づく。
        /// </summary>
        public static float InformationDominance(float ownRecon, float enemyBlinding, CounterReconParams p)
        {
            float own = Mathf.Clamp01(ownRecon);
            float blind = Mathf.Clamp01(enemyBlinding);
            float w = p.dominanceWeight;
            return Mathf.Clamp01(w * blind + (1f - w) * own);
        }

        public static float InformationDominance(float ownRecon, float enemyBlinding)
            => InformationDominance(ownRecon, enemyBlinding, CounterReconParams.Default);

        /// <summary>敵を盲目にできたか＝盲目度が閾値以上なら true。</summary>
        public static bool IsEnemyBlinded(float enemyBlinding, float threshold)
        {
            return Mathf.Clamp01(enemyBlinding) >= Mathf.Clamp01(threshold);
        }
    }
}
