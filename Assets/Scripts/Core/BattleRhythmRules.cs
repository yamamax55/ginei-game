using UnityEngine;

namespace Ginei
{
    /// <summary>戦いの拍子（リズム）の型＝宮本武蔵『五輪書』。乗り（勢いに乗る）・崩し（敵の調子を狂わせる）・後の先（敵が動いた直後の隙を打つ）・間（拍子を測る静）。</summary>
    public enum BattleRhythm { 乗り, 崩し, 後の先, 間 }

    /// <summary>拍子と戦機窓の調整係数。ctor で全値をクランプする。</summary>
    public readonly struct BattleRhythmParams
    {
        /// <summary>拍子に合ったときの戦闘効率ボーナスの最大幅（拍子に合えば力少なく勝つ）。</summary>
        public readonly float matchBonusScale;
        /// <summary>拍子を外したときの戦闘効率ペナルティの最大幅（拍子を外せば力あっても負ける）。</summary>
        public readonly float mismatchPenaltyScale;
        /// <summary>後の先＝敵が攻めに出た瞬間の反撃の効きの強さ（敵の踏み込みを衝く倍率幅）。</summary>
        public readonly float goNoSenScale;
        /// <summary>戦いのリズムを掴んだとみなす発火閾値（0..1）。これを超えると拍子に乗る。</summary>
        public readonly float inRhythmThreshold;

        public BattleRhythmParams(float matchBonusScale, float mismatchPenaltyScale,
            float goNoSenScale, float inRhythmThreshold)
        {
            this.matchBonusScale = Mathf.Max(0f, matchBonusScale);
            this.mismatchPenaltyScale = Mathf.Max(0f, mismatchPenaltyScale);
            this.goNoSenScale = Mathf.Max(0f, goNoSenScale);
            this.inRhythmThreshold = Mathf.Clamp01(inRhythmThreshold);
        }

        /// <summary>既定＝合致ボーナス幅0.5・外しペナルティ幅0.5・後の先倍率幅0.6・拍子掴み閾値0.6。</summary>
        public static BattleRhythmParams Default =>
            new BattleRhythmParams(0.5f, 0.5f, 0.6f, 0.6f);
    }

    /// <summary>
    /// 拍子と戦機窓の純ロジック＝宮本武蔵『五輪書』の「拍子（ひょうし）」（GRN-1・#1376）。<b>戦いには
    /// リズム（拍子）があり、敵の拍子を読んで『乗りの拍子（勢いに乗る）』『崩しの拍子（敵の調子を狂わせる）』
    /// 『後の先の拍子（敵が動いた直後の隙を打つ）』を捉える＝拍子に合えば力少なく勝ち、拍子を外せば力あっても
    /// 負ける</b>。敵の拍子を読む力（<see cref="RhythmReading"/>）を起点に、乗り・崩し・後の先の三様の拍子を
    /// 算出し、拍子が状況に合えば戦闘効率が上がり（<see cref="RhythmMatchBonus"/>）、外せば力があっても勝てない
    /// （<see cref="RhythmMismatchPenalty"/>）。<see cref="DecisiveBattleWindowRules"/>（決戦の機会窓＝戦力集結・
    /// 敵の露出など複数条件が揃う「いつ大会戦に踏み切るか」の戦略的好機・生成済み）とは別＝こちらは一合一合の
    /// 「戦いのリズムと戦機」の戦術拍子。<see cref="EscalationRules"/>（望まずとも昇る緊張の梯子）とも別。
    /// <see cref="TimingDoctrineRules"/>（後の先＝同 EPIC GRN・先制と応手の流派）と接続＝<see cref="GoNoSen"/> が
    /// その拍子版。<see cref="FocusRules"/>（三密の集中＝身口意の同期バフ）とも別＝こちらは敵との相対リズム。
    /// 倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。純ロジック（非 MonoBehaviour・
    /// test-first）。
    /// </summary>
    public static class BattleRhythmRules
    {
        /// <summary>
        /// 敵の拍子を読む力（0..1）＝knowing the enemy's rhythm。perception（自分の知覚0..1）×
        /// enemyTelegraphing（敵の動きの読みやすさ0..1＝敵が拍子を露わにしている度）の積＝知覚が鋭く・
        /// 敵の動きが読みやすいほど拍子を読める。どちらか一方でも0なら読めない＝知覚が鈍ければ・敵が拍子を
        /// 隠せば好機を捉えられない。乗り・崩し・後の先すべての起点となる「敵のリズムを読む」力。
        /// </summary>
        public static float RhythmReading(float perception, float enemyTelegraphing)
        {
            float per = Mathf.Clamp01(perception);
            float tel = Mathf.Clamp01(enemyTelegraphing);
            return Mathf.Clamp01(per * tel);
        }

        /// <summary>
        /// 乗りの拍子（0..1）＝流れに乗って力少なく勝つ。momentum（勢い0..1）× rhythmReading（拍子を読む力0..1）
        /// の積＝勢いがあり・敵の拍子を読めるほど流れに乗れる。勢いに乗ると少ない力で勝てる（勢い任せでなく、
        /// 拍子を読んでこそ乗りが効く＝どちらか0なら乗れない）。
        /// </summary>
        public static float RidingRhythm(float momentum, float rhythmReading)
        {
            float mom = Mathf.Clamp01(momentum);
            float rr = Mathf.Clamp01(rhythmReading);
            return Mathf.Clamp01(mom * rr);
        }

        /// <summary>
        /// 崩しの拍子（0..1）＝敵の調子を狂わせる。rhythmReading（拍子を読む力0..1）×（1−enemyComposure）
        /// ＝敵の拍子を読めて・敵の平静（enemyComposure 0..1）が乱れているほど崩せる。敵が落ち着いていれば
        /// （composure→1）崩しは効かず、敵の調子が狂っているほど（composure→0）こちらの崩しが通る＝敵のリズムを乱す。
        /// </summary>
        public static float BreakingRhythm(float rhythmReading, float enemyComposure)
        {
            float rr = Mathf.Clamp01(rhythmReading);
            float comp = Mathf.Clamp01(enemyComposure);
            return Mathf.Clamp01(rr * (1f - comp));
        }

        /// <summary>
        /// 後の先（0..1）＝敵が動いた直後の隙を打つ。rhythmReading（拍子を読む力0..1）× enemyCommitment
        /// （敵の踏み込み0..1＝敵が攻めに出てコミットした度）× goNoSenScale ＝敵の拍子を読めて・敵が深く
        /// 攻めに出るほど、その直後の隙を衝ける。敵が動かなければ（commitment 0）後の先は成立せず、敵が深く
        /// 踏み込むほど反撃の隙が大きい＝<see cref="TimingDoctrineRules"/> の先後の流派の拍子版。
        /// </summary>
        public static float GoNoSen(float rhythmReading, float enemyCommitment, BattleRhythmParams p)
        {
            float rr = Mathf.Clamp01(rhythmReading);
            float commit = Mathf.Clamp01(enemyCommitment);
            return Mathf.Clamp01(rr * commit * p.goNoSenScale);
        }

        /// <summary>既定係数での後の先（0..1）。</summary>
        public static float GoNoSen(float rhythmReading, float enemyCommitment)
            => GoNoSen(rhythmReading, enemyCommitment, BattleRhythmParams.Default);

        /// <summary>
        /// 拍子の一致ボーナス＝拍子が状況に合うと戦闘効率が上がる（拍子に合えば力少なく勝つ）。基準1.0に
        /// matchBonusScale × rhythmType（選んだ拍子の効き0..1）× situationFit（状況適合0..1）を加算した倍率
        /// （≧1.0）。拍子が強く効き・状況に合うほど効率が上がる＝少ない力で勝てる。基準値は変えずローカルに
        /// 倍率を計算する（実効値パターン）。
        /// </summary>
        public static float RhythmMatchBonus(float rhythmType, float situationFit, BattleRhythmParams p)
        {
            float rt = Mathf.Clamp01(rhythmType);
            float fit = Mathf.Clamp01(situationFit);
            return 1f + p.matchBonusScale * rt * fit;
        }

        /// <summary>既定係数での拍子の一致ボーナス（≧1.0）。</summary>
        public static float RhythmMatchBonus(float rhythmType, float situationFit)
            => RhythmMatchBonus(rhythmType, situationFit, BattleRhythmParams.Default);

        /// <summary>
        /// 拍子外しのペナルティ＝拍子を外すと力があっても勝てない（タイミングのずれ）。基準1.0から
        /// mismatchPenaltyScale ×（1−rhythmReading）× ownTiming（自分の動きの強さ0..1）を減算した倍率
        /// （≦1.0）。敵の拍子を読めず（rhythmReading→0）に強く動く（ownTiming→1）ほどタイミングが外れて
        /// 損が大きい＝力あっても拍子を外せば負ける。拍子をよく読めば（rhythmReading→1）ずれは生じない。
        /// </summary>
        public static float RhythmMismatchPenalty(float rhythmReading, float ownTiming, BattleRhythmParams p)
        {
            float rr = Mathf.Clamp01(rhythmReading);
            float own = Mathf.Clamp01(ownTiming);
            return Mathf.Clamp01(1f - p.mismatchPenaltyScale * (1f - rr) * own);
        }

        /// <summary>既定係数での拍子外しのペナルティ（≦1.0）。</summary>
        public static float RhythmMismatchPenalty(float rhythmReading, float ownTiming)
            => RhythmMismatchPenalty(rhythmReading, ownTiming, BattleRhythmParams.Default);

        /// <summary>
        /// テンポ支配（0..1）＝戦いのテンポを支配し自分の拍子に引き込む（主導権）。rhythmReading（拍子を
        /// 読む力0..1）× initiative（主導権0..1＝先んじて仕掛ける度）の積＝敵の拍子を読めて・主導権を握るほど
        /// テンポを支配できる。敵の拍子を読めても受け身（initiative 0）ならテンポは奪えず、主導権があっても
        /// 拍子を読めねば引き込めない＝相手を自分のリズムに乗せる。
        /// </summary>
        public static float TempoControl(float rhythmReading, float initiative)
        {
            float rr = Mathf.Clamp01(rhythmReading);
            float init = Mathf.Clamp01(initiative);
            return Mathf.Clamp01(rr * init);
        }

        /// <summary>
        /// 戦いのリズムを掴んだ判定＝敵の拍子を読み拍子が合致しているか。rhythmReading（拍子を読む力0..1）と
        /// rhythmMatchBonus（一致ボーナス倍率≧1.0）の両方を見て、読む力が閾値を超え、かつ拍子が合致して効率が
        /// 上がっている（matchBonus が 1.0 を超える）とき true ＝拍子を掴んで戦いのリズムに乗っている。
        /// 拍子を読めても合致していなければ（matchBonus≦1.0）リズムは掴めていない。
        /// </summary>
        public static bool IsInRhythm(float rhythmReading, float rhythmMatchBonus, float threshold)
        {
            return Mathf.Clamp01(rhythmReading) >= Mathf.Clamp01(threshold) && rhythmMatchBonus > 1f;
        }

        /// <summary>既定閾値（<see cref="BattleRhythmParams.inRhythmThreshold"/>）でのリズム掴み判定。</summary>
        public static bool IsInRhythm(float rhythmReading, float rhythmMatchBonus)
            => IsInRhythm(rhythmReading, rhythmMatchBonus, BattleRhythmParams.Default.inRhythmThreshold);
    }
}
