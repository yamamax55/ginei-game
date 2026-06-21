using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍団会戦フロー（① 正面の撃ち合い → ② 後背への回り込み〔回り込み・敵は対応〕 → ③ 決戦の投入）の
    /// 数値判断を集約する純ロジック（static・test-first）。Game 層（<see cref="BattlefieldCommandManager"/>／
    /// <see cref="FleetAI"/>）は本ルールへ判断を委譲し、配線だけを持つ（数式を各所に重複実装しない）。
    ///
    /// 分担：
    /// - 機動包囲の進捗・利得モデルは <see cref="ManeuverEnvelopmentRules"/>（こちらは「いつ仕掛けるか／どこへ回るか」の軍団判断）。
    /// - 決戦の機会窓は <see cref="DecisiveBattleWindowRules"/>（こちらは軍団の入力をその窓へ橋渡しする）。
    /// 盤面非依存・plain 引数（float/Vector2）・乱数なし（必要なら roll を呼び側から渡す）・実効値パターン。
    /// </summary>
    public static class CorpsBattleFlowRules
    {
        // ── ② 回り込み（包囲）の発意ゲート ──

        /// <summary>軍団長能力（統率＋情報）の正規化。0..1。包囲・決戦の発意ゲートに使う。</summary>
        public const float AbilityNormalizer = 200f;

        /// <summary>有能な軍団長が回り込みを試みる能力しきい値（0..1・既定0.55）。これ未満は正面戦に留まる。</summary>
        public const float DefaultEnvelopmentAbilityThreshold = 0.55f;

        /// <summary>
        /// 軍団が後背への回り込み（包囲）を試みるべきか。条件＝(1) 正面が交戦に入っている（前衛が接敵）、
        /// (2) 回り込みに割ける後衛がいる（rearSubCount&gt;0）、(3) 軍団長が有能（能力≥しきい値）、
        /// (4) 決定論 roll が能力以下（弱い軍団長は取りこぼす）。包囲は有能な指揮官の積極策。
        /// </summary>
        public static bool ShouldAttemptEnvelopment(bool frontEngaged, int rearSubCount,
            float commanderAbility, float roll, float abilityThreshold)
        {
            if (!frontEngaged || rearSubCount <= 0) return false;
            float ability = Mathf.Clamp01(commanderAbility);
            if (ability < Mathf.Clamp01(abilityThreshold)) return false;
            return Mathf.Clamp01(roll) <= ability; // 能力が高いほど踏み切りやすい（決定論 roll は呼び側）
        }

        /// <summary>既定しきい値版。</summary>
        public static bool ShouldAttemptEnvelopment(bool frontEngaged, int rearSubCount,
            float commanderAbility, float roll)
            => ShouldAttemptEnvelopment(frontEngaged, rearSubCount, commanderAbility, roll,
                DefaultEnvelopmentAbilityThreshold);

        /// <summary>提督の実効統率＋情報→0..1 の軍団長能力（提督不在は中庸0.5）。</summary>
        public static float CommanderAbility(int effectiveLeadership, int effectiveIntelligence)
            => Mathf.Clamp01((effectiveLeadership + effectiveIntelligence) / AbilityNormalizer);

        // ── ② 回り込みの目標座標（幾何ヘルパ・純粋） ──

        /// <summary>
        /// 敵軍団の側面へ回り込む広い目標点を返す（回り込み・回頭包囲）。敵の正面（fromAttacker→enemy 方向）に
        /// 直交する横方向へ <paramref name="flankOffset"/> だけ振り、さらに敵の手前 <paramref name="standoff"/> へ寄せる
        /// ＝敵の正面火力を避けつつ側背面へ抜ける軌道。side（+1=右回り/−1=左回り）で回頭方向を選ぶ。
        /// </summary>
        public static Vector2 FlankTarget(Vector2 attackerPos, Vector2 enemyPos, float flankOffset, float standoff, int side)
        {
            Vector2 toEnemy = enemyPos - attackerPos;
            if (toEnemy.sqrMagnitude < 1e-6f) toEnemy = Vector2.up;
            Vector2 dir = toEnemy.normalized;
            // 進行方向に直交する横ベクトル（side で左右を選ぶ）。
            Vector2 perp = new Vector2(-dir.y, dir.x) * (side >= 0 ? 1f : -1f);
            float off = Mathf.Max(0f, flankOffset);
            float so = Mathf.Max(0f, standoff);
            // 敵の手前 standoff まで寄せた点を起点に、横へ off 振る＝側面を狙う広い回り込み点。
            Vector2 approach = enemyPos - dir * so;
            return approach + perp * off;
        }

        /// <summary>
        /// 回り込みの向き（左右）を決める＝包囲側が敵に対してすでに居る側へ回る（最短で側面に着く）。
        /// 包囲側が敵→自分のどちら側に居るかの外積符号。0（真後ろ/真正面）なら右(+1)を既定にする。
        /// </summary>
        public static int ChooseFlankSide(Vector2 attackerPos, Vector2 enemyPos, Vector2 enemyForward)
        {
            Vector2 fwd = enemyForward.sqrMagnitude > 1e-6f ? enemyForward.normalized : Vector2.up;
            Vector2 rel = attackerPos - enemyPos;
            float cross = fwd.x * rel.y - fwd.y * rel.x; // 正＝左／負＝右（CorpsFormation の +Y 規約）
            return cross >= 0f ? 1 : -1;
        }

        // ── ② 敵のカウンター（横腹を脅かされた側の対応） ──

        /// <summary>包囲を察知したとみなす最大角（度・既定100°）。敵正面からこの角を超えて回り込まれたら脅威。</summary>
        public const float DefaultFlankDetectAngleDeg = 100f;

        /// <summary>
        /// ある敵対艦隊が自軍団の側面/後背へ回り込んでいるか（カウンター発動の検出）。threatPos が
        /// 自軍団の正面 corpsForward からなす角が detectAngle を超え、かつ脅威が一定距離 range 内にいれば true
        /// ＝横腹を脅かされたと判断し、後衛を割いて遮蔽（counter-screen）する。
        /// </summary>
        public static bool IsBeingFlanked(Vector2 corpsPos, Vector2 corpsForward, Vector2 threatPos,
            float range, float detectAngleDeg)
        {
            Vector2 toThreat = threatPos - corpsPos;
            float dist = toThreat.magnitude;
            if (dist > Mathf.Max(0f, range) || dist < 1e-4f) return false;
            Vector2 fwd = corpsForward.sqrMagnitude > 1e-6f ? corpsForward.normalized : Vector2.up;
            float ang = Vector2.Angle(fwd, toThreat / dist);
            return ang > Mathf.Clamp(detectAngleDeg, 0f, 180f);
        }

        /// <summary>既定角・range 指定版。</summary>
        public static bool IsBeingFlanked(Vector2 corpsPos, Vector2 corpsForward, Vector2 threatPos, float range)
            => IsBeingFlanked(corpsPos, corpsForward, threatPos, range, DefaultFlankDetectAngleDeg);

        // ── ③ 決戦の投入（commit） ──

        /// <summary>
        /// 軍団の決戦準備度（0..1）＝<see cref="DecisiveBattleWindowRules.Readiness"/> へ橋渡し。
        /// 戦力集結（隊形の整い）・補給充足（会戦中はほぼ1）・士気の高揚 から算出する。
        /// </summary>
        public static float Readiness(float forceConcentration, float supplyAdequacy, float moraleHigh)
            => DecisiveBattleWindowRules.Readiness(forceConcentration, supplyAdequacy, moraleHigh);

        /// <summary>
        /// 敵の脆弱性（0..1）＝<see cref="DecisiveBattleWindowRules.EnemyVulnerability"/> へ橋渡し。
        /// 敵の露出（包囲進捗＝側背面を晒している度）× 敵の疲弊（兵力減＝1−敵兵力比）。
        /// </summary>
        public static float EnemyVulnerability(float envelopmentProgress, float enemyStrengthRatio)
        {
            float exposure = Mathf.Clamp01(envelopmentProgress);
            float fatigue = Mathf.Clamp01(1f - Mathf.Clamp01(enemyStrengthRatio));
            return DecisiveBattleWindowRules.EnemyVulnerability(exposure, fatigue);
        }

        /// <summary>
        /// 決戦の窓の開き（0..1）＝<see cref="DecisiveBattleWindowRules.WindowOpening"/>。
        /// 自軍準備度 × 敵脆弱性。どちらか0なら窓は開かない。
        /// </summary>
        public static float WindowOpening(float readiness, float enemyVulnerability)
            => DecisiveBattleWindowRules.WindowOpening(readiness, enemyVulnerability);

        /// <summary>
        /// 軍団が決戦に踏み切るべきか（③ commit）＝窓の開きが閾値以上で機が熟した。
        /// <see cref="DecisiveBattleWindowRules.IsDecisiveMoment"/> へ委譲（既定閾値0.6）。
        /// </summary>
        public static bool ShouldCommitDecisive(float windowOpening, float threshold)
            => DecisiveBattleWindowRules.IsDecisiveMoment(windowOpening, threshold);

        /// <summary>既定閾値版。</summary>
        public static bool ShouldCommitDecisive(float windowOpening)
            => DecisiveBattleWindowRules.IsDecisiveMoment(windowOpening);

        /// <summary>
        /// 自軍準備度・敵脆弱性から一発で決戦判断する高水準ヘルパ（配線の二重実装回避）。
        /// 隊形の整い・士気・包囲進捗・敵兵力比を渡せば窓を計算し commit 可否を返す（窓値も out で返す）。
        /// </summary>
        public static bool ShouldCommitDecisive(float forceConcentration, float supplyAdequacy, float moraleHigh,
            float envelopmentProgress, float enemyStrengthRatio, out float windowOpening)
        {
            float rdy = Readiness(forceConcentration, supplyAdequacy, moraleHigh);
            float vuln = EnemyVulnerability(envelopmentProgress, enemyStrengthRatio);
            windowOpening = WindowOpening(rdy, vuln);
            return ShouldCommitDecisive(windowOpening);
        }
    }
}
