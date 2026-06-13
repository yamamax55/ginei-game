using UnityEngine;

namespace Ginei
{
    /// <summary>挟撃（両翼/前後同期攻撃）の調整係数。</summary>
    public readonly struct PincerAttackParams
    {
        /// <summary>タイミング差をこの幅まで許容する（0..1・この差までは完全同期扱い）。</summary>
        public readonly float mismatchTolerance;
        /// <summary>十字砲火ボーナスの最大幅（同期×包囲度がフルのときの加算上限）。</summary>
        public readonly float crossfireScale;
        /// <summary>挟まれた敵が火力を二方向へ割く最大度合い（0..1）。</summary>
        public readonly float splitScale;
        /// <summary>退路遮断の最大度合い（同期がフルのときの上限・0..1）。</summary>
        public readonly float denialScale;
        /// <summary>タイミングずれペナルティの強さ（差が大きいほど各個撃破されやすい）。</summary>
        public readonly float mistimingScale;
        /// <summary>片翼孤立リスクの強さ（劣勢×ずれが大きいほど各個撃破される）。</summary>
        public readonly float isolationScale;
        /// <summary>挟撃成立とみなす同期度のしきい値（0..1）。</summary>
        public readonly float closedThreshold;

        public PincerAttackParams(float mismatchTolerance, float crossfireScale, float splitScale,
                                  float denialScale, float mistimingScale, float isolationScale, float closedThreshold)
        {
            this.mismatchTolerance = Mathf.Clamp01(mismatchTolerance);
            this.crossfireScale = Mathf.Clamp01(crossfireScale);
            this.splitScale = Mathf.Clamp01(splitScale);
            this.denialScale = Mathf.Clamp01(denialScale);
            this.mistimingScale = Mathf.Max(0f, mistimingScale);
            this.isolationScale = Mathf.Max(0f, isolationScale);
            this.closedThreshold = Mathf.Clamp01(closedThreshold);
        }

        /// <summary>既定＝許容0.15・十字砲火0.5・火力分散0.6・退路遮断0.7・ずれ1.0・孤立0.6・成立しきい値0.6。</summary>
        public static PincerAttackParams Default =>
            new PincerAttackParams(0.15f, 0.5f, 0.6f, 0.7f, 1.0f, 0.6f, 0.6f);
    }

    /// <summary>
    /// 挟撃（ピンサー＝両翼/前後から挟んで叩く）の純ロジック。敵を二方向から挟むと、敵は火力を二方向へ
    /// 分散させられ、退路を断たれて崩れやすい。だが挟撃は**両翼の連携（到達タイミングの同期）**が要り、
    /// 各個に分かれた自軍は片方を各個撃破されるリスクを負う＝同期すれば十字砲火で崩すが、ずれれば各個撃破。
    /// 同期度（<see cref="PincerCoordination"/>）を軸に、十字砲火ボーナス・火力分散・退路遮断という利と、
    /// ずれペナルティ・片翼孤立リスクという害を出し、正味価値（<see cref="PincerNetValue"/>）で判断する。
    ///
    /// 分担：<see cref="ManeuverEnvelopmentRules"/>（機動包囲＝迂回して側背面を奪う単翼の機動）とは別＝
    /// 本ルールは**両翼の同期挟撃**（タイミングの噛み合い）に特化する。<see cref="EncirclementRules"/>
    /// （全周包囲＝退路完全遮断）とも別＝挟撃は二方向の挟み込みであって全周ではない。
    /// 盤面非依存の plain 引数（タイミング・兵力・包囲率は呼び出し側が観測して渡す）。実効値パターン
    /// （基準値非破壊・倍率/加算を返すだけ）。乱数なし・入力クランプ・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PincerAttackRules
    {
        /// <summary>
        /// 両翼の連携度（0..1）。左翼/右翼の到達タイミング（0..1の正規化時刻）の差が小さいほど高い。
        /// 許容幅 mismatchTolerance 以内なら完全同期(1)、差が広がるほど線形に低下し最大差(1)で0。
        /// </summary>
        public static float PincerCoordination(float leftArmTiming, float rightArmTiming, PincerAttackParams p)
        {
            float gap = Mathf.Abs(Mathf.Clamp01(leftArmTiming) - Mathf.Clamp01(rightArmTiming));
            float over = Mathf.Max(0f, gap - p.mismatchTolerance);
            float span = Mathf.Max(1e-4f, 1f - p.mismatchTolerance);
            return Mathf.Clamp01(1f - over / span);
        }

        public static float PincerCoordination(float leftArmTiming, float rightArmTiming)
            => PincerCoordination(leftArmTiming, rightArmTiming, PincerAttackParams.Default);

        /// <summary>
        /// 十字砲火ボーナス（与ダメ加算倍率・0..crossfireScale）。同期度×包囲率（二方向から撃てている割合）
        /// に比例。同期して敵を二方向から撃つほど火力が集中する。戻り値は「+◯◯倍」の加算ぶん（0=ボーナス無し）。
        /// </summary>
        public static float CrossfireBonus(float pincerCoordination, float encircledFraction, PincerAttackParams p)
        {
            float coord = Mathf.Clamp01(pincerCoordination);
            float enc = Mathf.Clamp01(encircledFraction);
            return Mathf.Clamp01(coord * enc) * p.crossfireScale;
        }

        public static float CrossfireBonus(float pincerCoordination, float encircledFraction)
            => CrossfireBonus(pincerCoordination, encircledFraction, PincerAttackParams.Default);

        /// <summary>
        /// 挟まれた敵の火力分散度（0..1）。敵火力が大きいほど、二方向への割り当てで前方へ向けられる火力が
        /// 削られる。戻り値は「敵が分散させられる割合」＝大きいほど敵の実効火力が落ちる（呼び出し側で 1-split を掛ける等）。
        /// </summary>
        public static float FirepowerSplit(float enemyFirepower, PincerAttackParams p)
        {
            // 敵火力を 0..1 の規模感に圧縮（firepower/(firepower+1)）＝大火力ほど分散の影響を受けるが上限あり。
            float ef = Mathf.Max(0f, enemyFirepower);
            float scaleBound = ef / (ef + 1f);
            return Mathf.Clamp01(scaleBound * p.splitScale);
        }

        public static float FirepowerSplit(float enemyFirepower)
            => FirepowerSplit(enemyFirepower, PincerAttackParams.Default);

        /// <summary>
        /// 退路遮断度（0..1）。両翼が同期して挟むほど敵の逃げ場が消える＝同期度に比例。
        /// 戻り値は「退路を断てている割合」（敵の離脱/撤退成功率を削る入力に使う）。
        /// </summary>
        public static float RetreatDenial(float pincerCoordination, PincerAttackParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(pincerCoordination) * p.denialScale);
        }

        public static float RetreatDenial(float pincerCoordination)
            => RetreatDenial(pincerCoordination, PincerAttackParams.Default);

        /// <summary>
        /// タイミングずれペナルティ（0..1）。timingGap（両翼の到達差・0..1）が許容幅を超えるほど、
        /// 早着/遅着した片翼が単独で晒され各個撃破されやすい＝被害倍率の入力。許容内は0、最大差で mistimingScale 上限。
        /// </summary>
        public static float MistimingPenalty(float timingGap, PincerAttackParams p)
        {
            float gap = Mathf.Clamp01(timingGap);
            float over = Mathf.Max(0f, gap - p.mismatchTolerance);
            float span = Mathf.Max(1e-4f, 1f - p.mismatchTolerance);
            return Mathf.Clamp01(over / span) * Mathf.Clamp01(p.mistimingScale);
        }

        public static float MistimingPenalty(float timingGap)
            => MistimingPenalty(timingGap, PincerAttackParams.Default);

        /// <summary>
        /// 片翼孤立リスク（0..1）。分かれた片翼 armStrength が敵 enemyStrength に対し劣勢であるほど、
        /// またタイミングがずれている（合流前に晒される）ほど、その片翼が各個撃破される危険が高い。
        /// 劣勢度（敵比）×ずれ×係数。armStrength≥enemyStrength（互角以上）なら劣勢度0でリスクは小さい。
        /// </summary>
        public static float ArmIsolationRisk(float armStrength, float enemyStrength, float timingGap, PincerAttackParams p)
        {
            float arm = Mathf.Max(0f, armStrength);
            float enemy = Mathf.Max(0f, enemyStrength);
            float total = arm + enemy;
            // 劣勢度＝敵が占める火力比のうち自分を上回るぶん（0.5で互角→0、敵が圧倒→1へ）。
            float enemyShare = total > 1e-4f ? enemy / total : 0f;
            float disadvantage = Mathf.Clamp01((enemyShare - 0.5f) * 2f); // 0.5→0, 1.0→1
            float gap = Mathf.Clamp01(timingGap);
            return Mathf.Clamp01(disadvantage * gap * p.isolationScale);
        }

        public static float ArmIsolationRisk(float armStrength, float enemyStrength, float timingGap)
            => ArmIsolationRisk(armStrength, enemyStrength, timingGap, PincerAttackParams.Default);

        /// <summary>
        /// 挟撃の正味価値（-1..+1 近傍）。十字砲火の利から片翼孤立の害を差し引く＝同期した挟撃は正、
        /// ずれて孤立リスクが勝てば負（仕掛けるべきでない）。AI/評価が「挟むか各個撃破を避けて集結するか」を判断する。
        /// </summary>
        public static float PincerNetValue(float crossfireBonus, float armIsolationRisk)
        {
            return Mathf.Clamp(crossfireBonus - Mathf.Clamp01(armIsolationRisk), -1f, 1f);
        }

        /// <summary>挟撃が成立したか＝同期度がしきい値以上（両翼が噛み合って挟み込めた）。</summary>
        public static bool IsPincerClosed(float pincerCoordination, float threshold)
        {
            return Mathf.Clamp01(pincerCoordination) >= Mathf.Clamp01(threshold);
        }

        public static bool IsPincerClosed(float pincerCoordination)
            => IsPincerClosed(pincerCoordination, PincerAttackParams.Default.closedThreshold);
    }
}
