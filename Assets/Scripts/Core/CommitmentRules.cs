using UnityEngine;

namespace Ginei
{
    /// <summary>背水の陣＝韓信型の決死のコミットメント（#1414）の調整係数。</summary>
    public readonly struct CommitmentParams
    {
        /// <summary>退路遮断の重み（retreatBlocked=1 で決死の覚悟がこの幅まで立ち上がる）。</summary>
        public readonly float retreatBlockedWeight;
        /// <summary>生存本能の重み（survivalInstinct=1 で決死の覚悟がこの幅まで上乗せ＝退けば死ぬという認識）。</summary>
        public readonly float survivalInstinctWeight;
        /// <summary>戦闘力ブーストの最大幅（noRetreatResolve=1 で戦闘力がこの幅まで跳ねる＝背水の陣の決死の力）。</summary>
        public readonly float combatBoostScale;
        /// <summary>敗北時の壊滅率の上限（退路なし＝逃げ場がないので全滅しうる）。</summary>
        public readonly float catastropheScale;
        /// <summary>背水の士気の高揚幅（決死の覚悟が初動の士気を押し上げる）。</summary>
        public readonly float moraleSurgeScale;
        /// <summary>膠着で士気が恐慌へ転じる速度（per dt・高揚は続かず袋小路で崩れる）。</summary>
        public readonly float panicDrainRate;
        /// <summary>心理的限界の閾値（決死の覚悟がこれを超えると逆に崩壊しうる＝追い詰めすぎ）。</summary>
        public readonly float breakingThreshold;
        /// <summary>敵の油断につけ込む反撃の最大幅（敵が背水を侮ると決死の反撃を食らう＝韓信の罠）。</summary>
        public readonly float overconfidenceScale;

        public CommitmentParams(float retreatBlockedWeight, float survivalInstinctWeight, float combatBoostScale,
                                float catastropheScale, float moraleSurgeScale, float panicDrainRate,
                                float breakingThreshold, float overconfidenceScale)
        {
            this.retreatBlockedWeight = Mathf.Clamp01(retreatBlockedWeight);
            this.survivalInstinctWeight = Mathf.Clamp01(survivalInstinctWeight);
            this.combatBoostScale = Mathf.Max(0f, combatBoostScale);
            this.catastropheScale = Mathf.Clamp01(catastropheScale);
            this.moraleSurgeScale = Mathf.Max(0f, moraleSurgeScale);
            this.panicDrainRate = Mathf.Max(0f, panicDrainRate);
            this.breakingThreshold = Mathf.Clamp01(breakingThreshold);
            this.overconfidenceScale = Mathf.Max(0f, overconfidenceScale);
        }

        /// <summary>
        /// 既定＝退路遮断重み0.6・生存本能重み0.4・戦闘力ブースト幅0.5・敗北壊滅上限0.9・士気高揚幅0.4・
        /// 恐慌転化率0.2・心理的限界0.85・敵油断反撃幅0.5。
        /// </summary>
        public static CommitmentParams Default => new CommitmentParams(
            0.6f, 0.4f, 0.5f, 0.9f, 0.4f, 0.2f, 0.85f, 0.5f);
    }

    /// <summary>
    /// 背水の陣＝韓信型の決死のコミットメント（#1414・項羽と劉邦）の純ロジック。あえて川を背にして退路を断つと
    /// （破釜沈船＝釜を破り船を沈める）、兵は「退けば死ぬ」と覚悟し（<see cref="NoRetreatResolve"/>）、決死の覚悟が
    /// 戦闘力を最大化する（<see cref="CombatPowerBoost"/>＝背水の陣のボーナス）。だが敗れれば退路がないので壊滅する
    /// （<see cref="DefeatCatastrophe"/>＝逃げ場がない全滅リスク）＝諸刃のコミットメント。退路を断った士気は初め高揚するが
    /// 膠着すると恐慌に転じうる（<see cref="MoraleUnderCommitment"/>）。勝算が薄く決死しかない時にのみ有効で、余力がある時に
    /// 敷くのは愚策（<see cref="CommitmentTiming"/>）。決死の覚悟も限界を超えると逆に崩壊し（<see cref="PsychologicalThreshold"/>
    /// ＝追い詰めすぎると逃散）、敵が背水を「袋の鼠」と侮るとかえって決死の反撃を食らう（<see cref="EnemyOverconfidence"/>＝韓信の罠）。
    /// 分担：<see cref="ForcedMarchRules"/>（強行軍＝疲労と速度）／<see cref="DeterrenceRules"/>（退路を焼くコミットメント＝戦略レベルの抑止）／
    /// <see cref="NationalDeterminationRules"/>（#1433・背水の決意＝劣勢国の国家意志）／<see cref="EncirclementRules"/>（包囲＝敵を袋小路へ）
    /// とは別＝本クラスは「韓信型の背水の陣＝戦術レベルの決死の戦闘力と、敗北すれば壊滅する諸刃のコミットメント」。
    /// 乱数なし・全入力クランプ・決定論・基準非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommitmentRules
    {
        /// <summary>
        /// 決死の覚悟（0..1）＝「退けば死ぬ」という認識。退路を断つ度合い（retreatBlocked 0..1）×retreatBlockedWeight と
        /// 生存本能（survivalInstinct 0..1）×survivalInstinctWeight の和＝退路が断たれ、かつ生への執着が強いほど高い。
        /// 既定重み（0.6/0.4）の和は1.0＝両者最大で決死の覚悟1.0。
        /// </summary>
        public static float NoRetreatResolve(float retreatBlocked, float survivalInstinct, CommitmentParams p)
        {
            float blocked = Mathf.Clamp01(retreatBlocked);
            float instinct = Mathf.Clamp01(survivalInstinct);
            return Mathf.Clamp01(p.retreatBlockedWeight * blocked + p.survivalInstinctWeight * instinct);
        }

        public static float NoRetreatResolve(float retreatBlocked, float survivalInstinct)
            => NoRetreatResolve(retreatBlocked, survivalInstinct, CommitmentParams.Default);

        /// <summary>
        /// 戦闘力ブースト（≥1.0）。決死の覚悟（noRetreatResolve 0..1）が高いほど戦闘力が最大化する＝背水の陣の決死の力。
        /// 1 + noRetreatResolve×combatBoostScale で、覚悟ゼロでは1.0（ボーナスなし＝退路があれば決死にならない）。
        /// 実効値（基準非破壊）。
        /// </summary>
        public static float CombatPowerBoost(float noRetreatResolve, CommitmentParams p)
        {
            float resolve = Mathf.Clamp01(noRetreatResolve);
            return 1f + resolve * p.combatBoostScale;
        }

        public static float CombatPowerBoost(float noRetreatResolve)
            => CombatPowerBoost(noRetreatResolve, CommitmentParams.Default);

        /// <summary>
        /// 敗北の壊滅率（0..catastropheScale）。退路がない状態（retreatBlocked 0..1）で敗れる（battleLost）と、逃げ場が
        /// ないので壊滅する＝退路を断つほど全滅リスクが高い。勝てば（battleLost=false）壊滅なし＝0＝コミットメントの諸刃の片刃。
        /// retreatBlocked×catastropheScale を返す。
        /// </summary>
        public static float DefeatCatastrophe(float retreatBlocked, bool battleLost, CommitmentParams p)
        {
            if (!battleLost) return 0f; // 勝てば退路の有無は問われない
            return Mathf.Clamp01(retreatBlocked) * p.catastropheScale;
        }

        public static float DefeatCatastrophe(float retreatBlocked, bool battleLost)
            => DefeatCatastrophe(retreatBlocked, battleLost, CommitmentParams.Default);

        /// <summary>
        /// 退路を断った士気（0..1）。決死の覚悟（noRetreatResolve 0..1）が初動の士気を高揚させる（1 を基準に
        /// +noRetreatResolve×moraleSurgeScale）が、膠着が長引く（dt の蓄積）と恐慌に転じうる（−panicDrainRate×dt）。
        /// 短期決戦なら高揚が勝り、長期の袋小路では恐慌が勝つ＝背水は速戦でこそ活きる。下限0・上限1。
        /// </summary>
        public static float MoraleUnderCommitment(float noRetreatResolve, float dt, CommitmentParams p)
        {
            float resolve = Mathf.Clamp01(noRetreatResolve);
            float t = Mathf.Max(0f, dt);
            float surge = resolve * p.moraleSurgeScale;
            float panic = resolve * p.panicDrainRate * t; // 覚悟が深いほど袋小路の恐慌も深い
            return Mathf.Clamp01(1f + surge - panic);
        }

        public static float MoraleUnderCommitment(float noRetreatResolve, float dt)
            => MoraleUnderCommitment(noRetreatResolve, dt, CommitmentParams.Default);

        /// <summary>
        /// 背水の陣を敷くべきタイミング（0..1）。敵が強大（enemyStrength 0..1）で、かつ自軍が決死しかない（ownDesperation 0..1＝
        /// 余力のなさ）ほど有効＝勝算が薄く決死に賭けるしかない時に1へ近づく。余力がある（ownDesperation→0）なら敵が強くても
        /// 低い＝退路を残せる時に断つのは愚策。enemyStrength×ownDesperation を返す。
        /// </summary>
        public static float CommitmentTiming(float enemyStrength, float ownDesperation, CommitmentParams p)
        {
            float enemy = Mathf.Clamp01(enemyStrength);
            float desperation = Mathf.Clamp01(ownDesperation);
            return Mathf.Clamp01(enemy * desperation);
        }

        public static float CommitmentTiming(float enemyStrength, float ownDesperation)
            => CommitmentTiming(enemyStrength, ownDesperation, CommitmentParams.Default);

        /// <summary>
        /// 心理的限界による崩壊度（0..1）。決死の覚悟（noRetreatResolve 0..1）が breakingPoint（0..1＝兵が耐えうる限界）を
        /// 超えると、追い詰めすぎてかえって崩壊する＝逃散を図る（諸刃）。breakingPoint を実効限界として
        /// breakingThreshold と min を取り、それを超えた超過分を崩壊度として返す。限界内なら0（覚悟が力に転じる）。
        /// </summary>
        public static float PsychologicalThreshold(float noRetreatResolve, float breakingPoint, CommitmentParams p)
        {
            float resolve = Mathf.Clamp01(noRetreatResolve);
            float limit = Mathf.Min(p.breakingThreshold, Mathf.Clamp01(breakingPoint));
            if (resolve <= limit) return 0f; // 限界内＝決死が戦闘力に転じる
            // 限界超過分を [limit,1] → [0,1] へ写像＝追い詰めすぎた崩壊
            float span = Mathf.Max(0.0001f, 1f - limit);
            return Mathf.Clamp01((resolve - limit) / span);
        }

        public static float PsychologicalThreshold(float noRetreatResolve, float breakingPoint)
            => PsychologicalThreshold(noRetreatResolve, breakingPoint, CommitmentParams.Default);

        /// <summary>
        /// 敵の油断につけ込む決死の反撃（≥1.0）。背水が敵に見えており（commitmentVisible 0..1）、敵が「袋の鼠」と
        /// 油断する（enemyComplacency 0..1）ほど、こちらの決死の反撃が効く＝韓信の罠（侮った敵が決死の側に食われる）。
        /// 1 + commitmentVisible×enemyComplacency×overconfidenceScale。敵が侮らなければ（complacency=0）1.0＝罠は成立しない。
        /// 実効値（基準非破壊）。
        /// </summary>
        public static float EnemyOverconfidence(float commitmentVisible, float enemyComplacency, CommitmentParams p)
        {
            float visible = Mathf.Clamp01(commitmentVisible);
            float complacency = Mathf.Clamp01(enemyComplacency);
            return 1f + visible * complacency * p.overconfidenceScale;
        }

        public static float EnemyOverconfidence(float commitmentVisible, float enemyComplacency)
            => EnemyOverconfidence(commitmentVisible, enemyComplacency, CommitmentParams.Default);

        /// <summary>
        /// 背水の決死の陣に入ったか＝決死の覚悟（noRetreatResolve 0..1）が threshold 以上＝退路を断ち、兵が決死で
        /// 戦う状態の判定。<see cref="IsBackToTheWall(float)"/> は既定閾値0.5。
        /// </summary>
        public static bool IsBackToTheWall(float noRetreatResolve, float threshold)
        {
            return Mathf.Clamp01(noRetreatResolve) >= Mathf.Clamp01(threshold);
        }

        public static bool IsBackToTheWall(float noRetreatResolve) => IsBackToTheWall(noRetreatResolve, 0.5f);
    }
}
