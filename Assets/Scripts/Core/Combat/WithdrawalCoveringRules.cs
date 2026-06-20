using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 退却援護（殿〔しんがり〕戦術）の調整係数。殿部隊が敵を食い止めて本隊の離脱を守る数式の振る舞いを決める。
    /// コンストラクタで全フィールドをクランプ＝不正値が漏れない。<see cref="Default"/> が既定。
    /// </summary>
    public readonly struct WithdrawalCoveringParams
    {
        /// <summary>殿が敵を食い止める基準時間スケール（戦力拮抗で得る足止め時間の上限の素）。</summary>
        public readonly float holdScale;
        /// <summary>足止め時間1あたり本隊が稼ぐ離脱割合（粘った時間がどれだけ離脱に変わるか）。</summary>
        public readonly float escapeRate;
        /// <summary>殿が払う犠牲の最大幅（粘り切ったときの消耗の上限割合）。</summary>
        public readonly float sacrificeScale;
        /// <summary>本隊の練度が秩序ある退却に効く幅（混乱せず下がれる度合いの上乗せ）。</summary>
        public readonly float disciplineScale;
        /// <summary>交互後退（互いに援護）で得る上乗せ幅（一方だけより両者で下がる優位）。</summary>
        public readonly float leapfrogBonus;
        /// <summary>地形の利が援護効率に効く幅（隘路・要害を使った殿の踏ん張り）。</summary>
        public readonly float terrainScale;
        /// <summary>殿の犠牲が本隊救出に見合う損益分岐（救出÷犠牲がこれを超えれば見合う）。</summary>
        public readonly float worthThreshold;
        /// <summary>本隊が無事離脱できたと見なす離脱割合の閾値。</summary>
        public readonly float coveredThreshold;

        public WithdrawalCoveringParams(float holdScale, float escapeRate, float sacrificeScale,
                                        float disciplineScale, float leapfrogBonus, float terrainScale,
                                        float worthThreshold, float coveredThreshold)
        {
            this.holdScale = Mathf.Max(0f, holdScale);
            this.escapeRate = Mathf.Max(0f, escapeRate);
            this.sacrificeScale = Mathf.Clamp01(sacrificeScale);
            this.disciplineScale = Mathf.Clamp01(disciplineScale);
            this.leapfrogBonus = Mathf.Clamp01(leapfrogBonus);
            this.terrainScale = Mathf.Clamp01(terrainScale);
            this.worthThreshold = Mathf.Max(0f, worthThreshold);
            this.coveredThreshold = Mathf.Clamp01(coveredThreshold);
        }

        /// <summary>既定＝足止め1.0・離脱率1.0・犠牲幅0.8・練度幅0.5・交互後退0.25・地形幅0.5・損益分岐1.0・離脱閾値0.6。</summary>
        public static WithdrawalCoveringParams Default =>
            new WithdrawalCoveringParams(1.0f, 1.0f, 0.8f, 0.5f, 0.25f, 0.5f, 1.0f, 0.6f);
    }

    /// <summary>
    /// 退却援護＝殿（しんがり）が本隊の離脱を守る純ロジック（盤面非依存・決定論・test-first）。
    /// 退却する本隊を後衛（殿部隊）が敵を食い止めて守る。殿は危険を引き受け、その戦力・粘りで本隊が
    /// どれだけ無事に離脱できるかが決まる＝殿が粘るほど本隊は逃げるが殿自身は消耗する（トレードオフ）。
    /// 黒田長政・島津の退き口・ナポレオンのネイの後衛戦のような「殿軍」を抽象化する。
    ///
    /// 分担（混同しない）：
    /// ・<see cref="SutegamariRules"/>（島津の捨てがまり）とは別＝あちらは旗艦敗走時に<b>配下艦が個々に</b>身を捨てて殿を務める
    ///   提督↔部下の関係性モデル。本ルールは<b>部隊（艦隊）単位</b>の退却援護＝殿部隊が本隊を逃がす戦力収支。
    /// ・<see cref="RallyRules"/>（再結集）とは別＝あちらは離脱後の<b>立て直し・合流</b>。本ルールは離脱<b>そのものの援護</b>。
    /// ・<see cref="BattleWithdrawalRules"/>（撤退目標）とは別＝あちらは<b>どこへ下がるか</b>。本ルールは<b>どう守って下がるか</b>。
    ///
    /// 戦力は plain な相対量（0以上）、各種度合いは 0..1。乱数なし（必要なら roll を渡す決定論）。
    /// 各メソッドに Params 明示版＋<see cref="WithdrawalCoveringParams.Default"/> 委譲版を備える。実効値パターン（基準値非破壊）。
    /// </summary>
    public static class WithdrawalCoveringRules
    {
        /// <summary>
        /// 殿が敵を食い止める時間（0..holdScale）＝殿戦力 R と追撃戦力 P の比 R/(R+P)×足止めスケール。
        /// 殿が厚いほど長く食い止め、追撃が圧倒的なら短時間で抜かれる（拮抗で半分）。
        /// </summary>
        public static float RearguardHoldTime(float rearguardStrength, float pursuerStrength,
                                              WithdrawalCoveringParams p)
        {
            float r = Mathf.Max(0f, rearguardStrength);
            float pu = Mathf.Max(0f, pursuerStrength);
            float denom = r + pu;
            if (denom <= 0f) return 0f;
            return p.holdScale * (r / denom);
        }

        public static float RearguardHoldTime(float rearguardStrength, float pursuerStrength)
            => RearguardHoldTime(rearguardStrength, pursuerStrength, WithdrawalCoveringParams.Default);

        /// <summary>
        /// 稼いだ時間で本隊が逃げられる割合（0..1）＝足止め時間×離脱率×(0.5＋本隊速度0.5)。
        /// 足止めが長く本隊が速いほど多く逃げられる。speed は 0..1（足の速い軽快な本隊ほど離脱が伸びる）。
        /// </summary>
        public static float MainBodyEscape(float holdTime, float mainBodySpeed, WithdrawalCoveringParams p)
        {
            float t = Mathf.Max(0f, holdTime);
            float speedFactor = 0.5f + 0.5f * Mathf.Clamp01(mainBodySpeed);
            return Mathf.Clamp01(t * p.escapeRate * speedFactor);
        }

        public static float MainBodyEscape(float holdTime, float mainBodySpeed)
            => MainBodyEscape(holdTime, mainBodySpeed, WithdrawalCoveringParams.Default);

        /// <summary>
        /// 殿が払う犠牲の割合（0..sacrificeScale）＝足止め時間×犠牲幅×追撃の重さ(0.5＋追撃圧0.5)。
        /// 長く粘るほど、また敵が大きいほど殿は消耗する（粘り＝死兵化の対価）。pursuerStrength は 0..1 に正規化して用いる。
        /// </summary>
        public static float RearguardSacrifice(float holdTime, float pursuerStrength, WithdrawalCoveringParams p)
        {
            float t = Mathf.Clamp01(holdTime);
            float pressure = 0.5f + 0.5f * Mathf.Clamp01(pursuerStrength);
            return Mathf.Clamp01(t * p.sacrificeScale * pressure);
        }

        public static float RearguardSacrifice(float holdTime, float pursuerStrength)
            => RearguardSacrifice(holdTime, pursuerStrength, WithdrawalCoveringParams.Default);

        /// <summary>
        /// 秩序ある退却の度合い（0..1）＝足止め時間に本隊の練度で上乗せ＝t×(1−練度幅＋練度幅×規律)。
        /// 殿が時間を稼ぎ、本隊の規律(0..1)が高いほど混乱せず整然と下がれる。
        /// </summary>
        public static float OrderlyWithdrawal(float rearguardHoldTime, float mainBodyDiscipline,
                                              WithdrawalCoveringParams p)
        {
            float t = Mathf.Clamp01(rearguardHoldTime);
            float d = Mathf.Clamp01(mainBodyDiscipline);
            float disciplineFactor = (1f - p.disciplineScale) + p.disciplineScale * d;
            return Mathf.Clamp01(t * disciplineFactor);
        }

        public static float OrderlyWithdrawal(float rearguardHoldTime, float mainBodyDiscipline)
            => OrderlyWithdrawal(rearguardHoldTime, mainBodyDiscipline, WithdrawalCoveringParams.Default);

        /// <summary>
        /// 交互後退（互いに援護しながら下がる）の効果（0..1）＝両隊の援護力の平均に交互後退ボーナスを上乗せ。
        /// 二隊が互いに殿を交代するほど、一隊が単独で踏ん張るより安全に下がれる（蛙跳び後退）。unitA/B は 0..1。
        /// </summary>
        public static float LeapfrogCovering(float unitA, float unitB, WithdrawalCoveringParams p)
        {
            float a = Mathf.Clamp01(unitA);
            float b = Mathf.Clamp01(unitB);
            float avg = 0.5f * (a + b);
            // 両隊が揃って噛み合うほど交互後退ボーナスが乗る（積でゲート＝片方が弱いと連携が崩れる）。
            float interlock = a * b;
            return Mathf.Clamp01(avg + p.leapfrogBonus * interlock);
        }

        public static float LeapfrogCovering(float unitA, float unitB)
            => LeapfrogCovering(unitA, unitB, WithdrawalCoveringParams.Default);

        /// <summary>
        /// 地形を使った援護の効率（0..1）＝殿戦力に地形の利を上乗せ＝R×(1＋地形幅×地形優位)で 0..1 にクランプ。
        /// 隘路・要害に拠れば同じ殿戦力でも援護効率が上がる。rearguardStrength・terrainAdvantage は 0..1。
        /// </summary>
        public static float CoverEffectiveness(float rearguardStrength, float terrainAdvantage,
                                               WithdrawalCoveringParams p)
        {
            float r = Mathf.Clamp01(rearguardStrength);
            float terr = Mathf.Clamp01(terrainAdvantage);
            return Mathf.Clamp01(r * (1f + p.terrainScale * terr));
        }

        public static float CoverEffectiveness(float rearguardStrength, float terrainAdvantage)
            => CoverEffectiveness(rearguardStrength, terrainAdvantage, WithdrawalCoveringParams.Default);

        /// <summary>
        /// 殿の犠牲が本隊救出に見合うか（救出÷犠牲・0..∞）＝本隊離脱割合÷殿の犠牲割合。
        /// 損益分岐 worthThreshold を超えれば「殿の犠牲が報われた」＝少ない犠牲で多くを逃がせたほど高い。
        /// 犠牲がほぼ0（無傷で守れた）なら大きな値を返す（最大救出）。
        /// </summary>
        public static float SacrificeWorth(float mainBodyEscape, float rearguardSacrifice,
                                           WithdrawalCoveringParams p)
        {
            float escape = Mathf.Clamp01(mainBodyEscape);
            float sac = Mathf.Clamp01(rearguardSacrifice);
            if (sac <= 0.0001f) return escape / 0.0001f; // 犠牲ほぼ無し＝極めて見合う
            return escape / sac;
        }

        public static float SacrificeWorth(float mainBodyEscape, float rearguardSacrifice)
            => SacrificeWorth(mainBodyEscape, rearguardSacrifice, WithdrawalCoveringParams.Default);

        /// <summary>
        /// 殿の犠牲が見合ったか＝救出÷犠牲が損益分岐を超える（<see cref="SacrificeWorth"/> ≥ worthThreshold）。
        /// </summary>
        public static bool IsSacrificeWorthwhile(float mainBodyEscape, float rearguardSacrifice,
                                                 WithdrawalCoveringParams p)
            => SacrificeWorth(mainBodyEscape, rearguardSacrifice, p) >= p.worthThreshold;

        public static bool IsSacrificeWorthwhile(float mainBodyEscape, float rearguardSacrifice)
            => IsSacrificeWorthwhile(mainBodyEscape, rearguardSacrifice, WithdrawalCoveringParams.Default);

        /// <summary>
        /// 本隊が無事離脱できたか＝本隊離脱割合 mainBodyEscape が閾値 threshold(0..1) 以上。
        /// 既定の損益分岐は <see cref="WithdrawalCoveringParams.coveredThreshold"/>。
        /// </summary>
        public static bool IsWithdrawalCovered(float mainBodyEscape, float threshold)
            => Mathf.Clamp01(mainBodyEscape) >= Mathf.Clamp01(threshold);

        public static bool IsWithdrawalCovered(float mainBodyEscape)
            => IsWithdrawalCovered(mainBodyEscape, WithdrawalCoveringParams.Default.coveredThreshold);
    }
}
