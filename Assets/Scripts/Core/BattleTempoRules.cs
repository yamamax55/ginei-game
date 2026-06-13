using UnityEngine;

namespace Ginei
{
    /// <summary>会戦テンポ（勢いの振り戻し/減衰）の調整係数。ctor で全値をクランプする。</summary>
    public readonly struct BattleTempoParams
    {
        /// <summary>勢いが時間で中庸へ戻る速さ（攻め疲れ＝1秒あたりの減衰率の係数）。大きいほど早く冷める。</summary>
        public readonly float decayRate;
        /// <summary>劣勢側の踏ん張りによる振り戻しの強さ（背水の意地。0=戻らない/1=拮抗まで押し返す）。</summary>
        public readonly float swingStrength;
        /// <summary>戦力比が極端でも損害が一気に振れすぎない減衰の効き（指数。0.5＝平方根で穏やか）。</summary>
        public readonly float dampingExponent;
        /// <summary>これ以下の勢いを「拮抗」とみなす膠着判定の閾値（0..1）。</summary>
        public readonly float stalemateThreshold;
        /// <summary>拮抗が続いて膠着とみなす最短時間（秒）。</summary>
        public readonly float stalemateDuration;
        /// <summary>勢いがこれを超えた一瞬を「決定機」とみなす閾値（0..1）。</summary>
        public readonly float decisiveThreshold;

        public BattleTempoParams(float decayRate, float swingStrength, float dampingExponent,
            float stalemateThreshold, float stalemateDuration, float decisiveThreshold)
        {
            this.decayRate = Mathf.Max(0f, decayRate);
            this.swingStrength = Mathf.Clamp01(swingStrength);
            this.dampingExponent = Mathf.Max(0f, dampingExponent);
            this.stalemateThreshold = Mathf.Clamp01(stalemateThreshold);
            this.stalemateDuration = Mathf.Max(0f, stalemateDuration);
            this.decisiveThreshold = Mathf.Clamp01(decisiveThreshold);
        }

        /// <summary>既定＝減衰率0.5・振り戻し0.6・減衰指数0.5・膠着閾値0.15・膠着時間8秒・決定機閾値0.6。</summary>
        public static BattleTempoParams Default =>
            new BattleTempoParams(0.5f, 0.6f, 0.5f, 0.15f, 8f, 0.6f);

        /// <summary>局面の不感帯＝この幅の勢いは「拮抗」(0)とみなす（<see cref="BattleTempoRules.TempoPhase"/>）。</summary>
        public const float PhaseDeadband = 0.1f;
    }

    /// <summary>
    /// 会戦テンポの純ロジック＝<b>一方的な雪崩を抑える勢いの振り戻し/減衰</b>（緊張を保つ演出レイヤー）。
    /// 攻め疲れ（<see cref="MomentumDecay"/>）・予備投入や士気の揺り戻し（<see cref="SwingBack"/>）・極端な戦力比の
    /// 損害減衰（<see cref="AvalancheDamping"/>）で戦況が往復し、押し/拮抗/押されの局面（<see cref="TempoPhase"/>）が
    /// 入れ替わる。<b>勝敗そのものではなくテンポ/緊張の演出</b>＝<see cref="LanchesterRules"/>（局所火力差の二乗則で
    /// 実ダメージを決める勝敗レイヤー）とは別系統。<see cref="BattleMomentumRules"/>（両軍戦力から0..1の優勢度を
    /// 読み取る読みやすさレイヤー）・<see cref="BattleRhythmRules"/>（敵の拍子を読む武蔵の戦闘効率倍率）とも責務を
    /// 分け、本ルールは<b>符号付き勢い(-1..1)の振り戻し・減衰・局面転換</b>に特化する（既存と数式を重複させない）。
    /// 勢いは「基準side視点」の符号付き値＝正=押している/負=押されている。盤面非依存の plain 引数・乱数なし・決定論。
    /// 入力はクランプ・各メソッドに Params 明示版＋Default 委譲版。実効値パターン（基準値非破壊）。test-first。
    /// </summary>
    public static class BattleTempoRules
    {
        /// <summary>
        /// 直近の戦果差から勢い(-1..1)を出す。ownGain（自軍が与えた戦果）と enemyGain（敵が与えた戦果）の
        /// 相対差＝(own-enemy)/(own+enemy)。両方0なら拮抗0（符号なし）。自軍が一方的に削れば+1へ、削られれば-1へ。
        /// </summary>
        public static float Momentum(float ownGain, float enemyGain)
        {
            float own = Mathf.Max(0f, ownGain);
            float enemy = Mathf.Max(0f, enemyGain);
            float sum = own + enemy;
            if (sum <= 0f) return 0f;
            return Mathf.Clamp((own - enemy) / sum, -1f, 1f);
        }

        /// <summary>
        /// 勢いが時間で中庸（0）へ戻る＝攻め疲れの減衰。momentum ×（1−decayRate×dt）。係数は0未満に落とさない
        /// （行き過ぎて符号反転しない）。dt が大きい/decayRate が高いほど早く冷める＝押し続けても勢いは持続しない。
        /// </summary>
        public static float MomentumDecay(float momentum, float dt, BattleTempoParams p)
        {
            float m = Mathf.Clamp(momentum, -1f, 1f);
            float factor = Mathf.Max(0f, 1f - p.decayRate * Mathf.Max(0f, dt));
            return Mathf.Clamp(m * factor, -1f, 1f);
        }

        /// <summary>既定係数での勢いの減衰(-1..1)。</summary>
        public static float MomentumDecay(float momentum, float dt)
            => MomentumDecay(momentum, dt, BattleTempoParams.Default);

        /// <summary>
        /// 劣勢側の踏ん張りで勢いを拮抗側へ押し返す＝背水の意地（予備投入/士気の揺り戻し）。momentum の絶対値を
        /// underdogResolve（劣勢側の覚悟0..1）× swingStrength ぶん削る＝m×(1−resolve×swingStrength)。resolve=0で
        /// 不変、最大で拮抗(0)近くまで押し返す。符号は保つ（押されが押しに逆転はしない＝あくまで振り戻し）。
        /// </summary>
        public static float SwingBack(float momentum, float underdogResolve, BattleTempoParams p)
        {
            float m = Mathf.Clamp(momentum, -1f, 1f);
            float resolve = Mathf.Clamp01(underdogResolve);
            float pull = 1f - resolve * p.swingStrength;
            return Mathf.Clamp(m * pull, -1f, 1f);
        }

        /// <summary>既定係数での振り戻し(-1..1)。</summary>
        public static float SwingBack(float momentum, float underdogResolve)
            => SwingBack(momentum, underdogResolve, BattleTempoParams.Default);

        /// <summary>
        /// 勢いから局面を返す＝押し(+1)/拮抗(0)/押され(-1)。不感帯 <see cref="BattleTempoParams.PhaseDeadband"/> の
        /// 範囲内は拮抗とみなす（微小な揺れで局面が暴れない）。
        /// </summary>
        public static int TempoPhase(float momentum)
        {
            float m = Mathf.Clamp(momentum, -1f, 1f);
            if (m > BattleTempoParams.PhaseDeadband) return 1;
            if (m < -BattleTempoParams.PhaseDeadband) return -1;
            return 0;
        }

        /// <summary>
        /// 戦力比が極端でも損害が一気に振れすぎない減衰倍率(0..1)。strengthRatio は強者/弱者（1以上で扱う＝
        /// 1未満は1へクランプ）。比が1（拮抗）で1.0、比が開くほど 1/比^dampingExponent で下がる＝圧倒的でも
        /// 損害が一拍で雪崩れない。基準損害に掛けて使う（実効値パターン）。
        /// </summary>
        public static float AvalancheDamping(float strengthRatio, BattleTempoParams p)
        {
            float ratio = Mathf.Max(1f, strengthRatio);
            float damping = Mathf.Pow(1f / ratio, p.dampingExponent);
            return Mathf.Clamp01(damping);
        }

        /// <summary>既定係数での雪崩減衰倍率(0..1)。</summary>
        public static float AvalancheDamping(float strengthRatio)
            => AvalancheDamping(strengthRatio, BattleTempoParams.Default);

        /// <summary>
        /// 勢いの揺れの大きさから山場の緊張(0..1)を返す。momentumVolatility（勢いの変動量0..1）が大きいほど高い。
        /// volatility×(2−volatility) の凹カーブ＝小さな揺れより大きな振り戻しがより緊張を高める。
        /// </summary>
        public static float ClimaxIntensity(float momentumVolatility)
        {
            float v = Mathf.Clamp01(momentumVolatility);
            return Mathf.Clamp01(v * (2f - v));
        }

        /// <summary>
        /// 勢いが閾値を超えた一瞬を決定機とみなす(bool)。|momentum| ≧ threshold で true＝押し/押されのどちらでも
        /// 決定的に傾いた瞬間（一気に畳みかける/踏みとどまる好機）。
        /// </summary>
        public static bool DecisiveWindow(float momentum, float threshold)
        {
            return Mathf.Abs(Mathf.Clamp(momentum, -1f, 1f)) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="BattleTempoParams.decisiveThreshold"/>）での決定機判定。</summary>
        public static bool DecisiveWindow(float momentum)
            => DecisiveWindow(momentum, BattleTempoParams.Default.decisiveThreshold);

        /// <summary>
        /// 勢いが拮抗し続けると膠着とみなす(bool)。|momentum| ≦ stalemateThreshold かつ duration ≧ stalemateDuration
        /// で true＝どちらも決め手を欠いて時間だけ過ぎる睨み合い。
        /// </summary>
        public static bool IsStalemate(float momentum, float duration, BattleTempoParams p)
        {
            return Mathf.Abs(Mathf.Clamp(momentum, -1f, 1f)) <= p.stalemateThreshold
                && Mathf.Max(0f, duration) >= p.stalemateDuration;
        }

        /// <summary>既定係数での膠着判定。</summary>
        public static bool IsStalemate(float momentum, float duration)
            => IsStalemate(momentum, duration, BattleTempoParams.Default);
    }
}
