using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術的奇襲（不意打ち）の純ロジック。
    /// 敵の予期しない方向・タイミングで攻撃すると、敵は反応が遅れ、
    /// 初撃で大きな打撃を与えられる。奇襲は時間とともに効果が薄れ（敵が態勢を立て直す）、
    /// 偵察に見破られると効果がない。低士気の敵ほど心理的衝撃が大きい。
    ///
    /// 分担（重複実装しない）：
    /// - AmbushRules（伏兵＝地形/配置に潜んで待ち伏せる）とは別＝
    ///   こちらは伏兵に限らない不意打ち一般（方向・タイミングの奇襲も含む）。
    /// - SignalIntelligenceRules / FogOfWarRules（情報・索敵の純ロジック）とは連携するが別＝
    ///   こちらは「奇襲が成立したときの戦術効果」を解く（情報優劣そのものは上記が解く）。
    /// 盤面非依存の plain 引数・乱数なし・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class TacticalSurpriseRules
    {
        /// <summary>戦術的奇襲の係数。トップレベル readonly struct・全 Clamp。</summary>
        public readonly struct TacticalSurpriseParams
        {
            /// <summary>奇襲度の効き（予期の薄さ×隠密の利き）。</summary>
            public readonly float surpriseScale;
            /// <summary>奇襲度→初撃打撃ボーナスの効き（実効値の上乗せ幅）。</summary>
            public readonly float firstStrikeScale;
            /// <summary>奇襲による敵反応遅延の最大秒数。</summary>
            public readonly float maxReactionDelay;
            /// <summary>敵が態勢を立て直すまでの時間（秒）＝奇襲が薄れる時定数。</summary>
            public readonly float recoveryTime;
            /// <summary>反応遅延→好機の窓の換算係数。</summary>
            public readonly float windowScale;
            /// <summary>敵偵察が隠密を打ち消す効き（見破りの利き）。</summary>
            public readonly float reconScale;
            /// <summary>低士気の敵への心理的衝撃の増幅幅。</summary>
            public readonly float moraleShockScale;

            public TacticalSurpriseParams(
                float surpriseScale,
                float firstStrikeScale,
                float maxReactionDelay,
                float recoveryTime,
                float windowScale,
                float reconScale,
                float moraleShockScale)
            {
                this.surpriseScale = Mathf.Max(0f, surpriseScale);
                this.firstStrikeScale = Mathf.Max(0f, firstStrikeScale);
                this.maxReactionDelay = Mathf.Max(0f, maxReactionDelay);
                this.recoveryTime = Mathf.Max(0.0001f, recoveryTime);
                this.windowScale = Mathf.Max(0f, windowScale);
                this.reconScale = Mathf.Max(0f, reconScale);
                this.moraleShockScale = Mathf.Max(0f, moraleShockScale);
            }

            public static TacticalSurpriseParams Default => new TacticalSurpriseParams(
                surpriseScale: 1.0f,
                firstStrikeScale: 1.0f,
                maxReactionDelay: 4.0f,
                recoveryTime: 5.0f,
                windowScale: 1.0f,
                reconScale: 1.0f,
                moraleShockScale: 0.5f);
        }

        /// <summary>
        /// 奇襲度（0..1）。敵の予期の薄さ（1−予期）×接近の隠密×利き。
        /// 敵が身構えていない方向・タイミングで、かつ隠れて寄れたほど高い。
        /// </summary>
        public static float SurpriseLevel(float enemyExpectation, float approachConcealment, TacticalSurpriseParams p)
        {
            float unexpected = 1f - Mathf.Clamp01(enemyExpectation);
            float conceal = Mathf.Clamp01(approachConcealment);
            return Mathf.Clamp01(unexpected * conceal * p.surpriseScale);
        }

        public static float SurpriseLevel(float enemyExpectation, float approachConcealment)
            => SurpriseLevel(enemyExpectation, approachConcealment, TacticalSurpriseParams.Default);

        /// <summary>
        /// 初撃の打撃ボーナス（実効値・基準非破壊。1.0＝補正なし）。
        /// 1 + 奇襲度×利き＝不意を突くほど初撃が痛い。
        /// </summary>
        public static float FirstStrikeBonus(float surpriseLevel, TacticalSurpriseParams p)
        {
            float s = Mathf.Clamp01(surpriseLevel);
            return 1f + s * p.firstStrikeScale;
        }

        public static float FirstStrikeBonus(float surpriseLevel)
            => FirstStrikeBonus(surpriseLevel, TacticalSurpriseParams.Default);

        /// <summary>
        /// 奇襲で敵の反応が遅れる時間（秒）。
        /// 奇襲度×敵の警戒の鈍さ（1−警戒）×最大遅延＝奇襲が強く敵が鈍いほど長く固まる。
        /// </summary>
        public static float EnemyReactionDelay(float surpriseLevel, float enemyAlertness, TacticalSurpriseParams p)
        {
            float s = Mathf.Clamp01(surpriseLevel);
            float slow = 1f - Mathf.Clamp01(enemyAlertness);
            return s * slow * p.maxReactionDelay;
        }

        public static float EnemyReactionDelay(float surpriseLevel, float enemyAlertness)
            => EnemyReactionDelay(surpriseLevel, enemyAlertness, TacticalSurpriseParams.Default);

        /// <summary>
        /// 時間経過で薄れた奇襲度（0..1）。敵が態勢を立て直すほど効果が減衰する。
        /// 奇襲度×(1 − 経過時間/立て直し時間)＝立て直し時間で 0 まで線形に消える。
        /// </summary>
        public static float SurpriseDecay(float surpriseLevel, float timeElapsed, TacticalSurpriseParams p)
        {
            float s = Mathf.Clamp01(surpriseLevel);
            float t = Mathf.Max(0f, timeElapsed);
            float remaining = Mathf.Clamp01(1f - t / p.recoveryTime);
            return s * remaining;
        }

        public static float SurpriseDecay(float surpriseLevel, float timeElapsed)
            => SurpriseDecay(surpriseLevel, timeElapsed, TacticalSurpriseParams.Default);

        /// <summary>
        /// 奇襲が効いている好機の窓（秒）。敵の反応遅延×換算係数。
        /// この窓のあいだに畳みかけられる＝遅延がそのまま戦機になる。
        /// </summary>
        public static float WindowOfOpportunity(float enemyReactionDelay, TacticalSurpriseParams p)
        {
            float d = Mathf.Max(0f, enemyReactionDelay);
            return d * p.windowScale;
        }

        public static float WindowOfOpportunity(float enemyReactionDelay)
            => WindowOfOpportunity(enemyReactionDelay, TacticalSurpriseParams.Default);

        /// <summary>
        /// 偵察に見破られたあとの実効隠密（0..1）。
        /// 隠密 − 敵偵察×利き＝偵察が勝ると隠密が消え、奇襲は成立しない（0 へ）。
        /// </summary>
        public static float DetectionNullification(float approachConcealment, float enemyRecon, TacticalSurpriseParams p)
        {
            float conceal = Mathf.Clamp01(approachConcealment);
            float recon = Mathf.Clamp01(enemyRecon);
            return Mathf.Clamp01(conceal - recon * p.reconScale);
        }

        public static float DetectionNullification(float approachConcealment, float enemyRecon)
            => DetectionNullification(approachConcealment, enemyRecon, TacticalSurpriseParams.Default);

        /// <summary>
        /// 心理的衝撃を加味した複合打撃（実効値・基準非破壊）。
        /// 初撃ボーナス×(1 + 敵の低士気(1−士気)×増幅幅)＝崩れかけの敵ほど不意打ちが利く。
        /// </summary>
        public static float CompoundedShock(float firstStrikeBonus, float enemyMorale, TacticalSurpriseParams p)
        {
            float bonus = Mathf.Max(0f, firstStrikeBonus);
            float fragile = 1f - Mathf.Clamp01(enemyMorale);
            return bonus * (1f + fragile * p.moraleShockScale);
        }

        public static float CompoundedShock(float firstStrikeBonus, float enemyMorale)
            => CompoundedShock(firstStrikeBonus, enemyMorale, TacticalSurpriseParams.Default);

        /// <summary>
        /// 奇襲が成立したか（奇襲度が閾値以上）。見破られて 0 に落ちれば不成立。
        /// </summary>
        public static bool IsSurpriseAchieved(float surpriseLevel, float threshold)
        {
            return Mathf.Clamp01(surpriseLevel) >= Mathf.Clamp01(threshold);
        }
    }
}
