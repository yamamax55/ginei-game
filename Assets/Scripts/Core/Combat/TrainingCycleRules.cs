using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 演習→実戦能力移転の純ロジック（ENDR-1 #2347）。<see cref="AutoBattleSim"/>（TIME-4 自動解決器）と
    /// <see cref="GrowthRules"/>（Wave1 #537 成長器）の**橋**＝会戦結果（演習・実会戦）を経験値に変換し
    /// <see cref="GrowthRules.GainExperience"/> へ供給するコネクタ。「自動解決の蓄積が実戦能力を育てる」
    /// という演習の地続き性（演習と実戦の連続体）を表す。乱数は引かない（経験は決定論）。test-first。
    /// <para>
    /// 数式は本ファイルに集約し、成長そのもの（飽和カーブ・天井）は <see cref="GrowthRules"/> へ委譲して
    /// 二重実装しない（基準能力は変えない＝実効値パターン）。
    /// </para>
    /// </summary>
    public static class TrainingCycleRules
    {
        /// <summary>演習1回あたりの基準経験収率（消耗・所要時間の係数を掛ける前の素地）。</summary>
        public const float BaseYield = 1f;

        /// <summary>実会戦の収率倍率（演習より高収率＝生死を賭けた1戦は重い）。</summary>
        public const float RealBattleMultiplier = 2f;

        /// <summary>演習（非実戦）の収率倍率（基準＝1.0）。</summary>
        public const float DrillMultiplier = 1f;

        /// <summary>所要時間→収率の正規化スケール（秒）。この秒数の会戦で時間項が約0.5に飽和。</summary>
        public const float DurationScaleSeconds = 60f;

        /// <summary>時間項の重み（接近戦＝長丁場ほど学びが多いが頭打ち）。</summary>
        public const float DurationWeight = 0.5f;

        /// <summary>反復の逓減スケール（この回数で疲労係数が約0.5へ）。同じ訓練の飽和を表す。</summary>
        public const float FatigueScale = 10f;

        /// <summary>反復疲労係数の下限（飽和しても完全には0にしない＝積み上げは続く）。</summary>
        public const float MinFatigue = 0.1f;

        /// <summary>逆境ボーナスの最小倍率（難度0＝楽な訓練は伸びが鈍い）。</summary>
        public const float MinAdversarialBonus = 0.5f;

        /// <summary>逆境ボーナスの最大倍率（難度1＝苛烈な逆境ほど成長係数が上がる）。</summary>
        public const float MaxAdversarialBonus = 2f;

        /// <summary>
        /// 反復による逓減係数（同じ訓練の飽和）を返す。<paramref name="repetitions"/>=0で1.0、
        /// 回数が増えるほど 0 へ漸近するが <see cref="MinFatigue"/> を下限とする（積み上げは止まらない）。
        /// 単調非増加・(0,1] に収まる。負の入力は0扱い。
        /// </summary>
        public static float TrainingFatigue(int repetitions)
        {
            int r = Mathf.Max(0, repetitions);
            // 1/(1+r/scale)：r=0で1.0、r=scale で0.5、∞で0へ漸近。下限でクランプ。
            float raw = FatigueScale / (FatigueScale + r);
            return Mathf.Clamp(raw, MinFatigue, 1f);
        }

        /// <summary>
        /// 訓練難度（0..1）→成長係数の倍率を返す。逆境下（高難度）の訓練ほど伸びる。
        /// 0で <see cref="MinAdversarialBonus"/>、1で <see cref="MaxAdversarialBonus"/> へ線形補間。
        /// 範囲外の入力は0..1へクランプ＝単調非減少。
        /// </summary>
        public static float AdversarialBonus(float trainingDifficulty)
        {
            float t = Mathf.Clamp01(trainingDifficulty);
            return Mathf.Lerp(MinAdversarialBonus, MaxAdversarialBonus, t);
        }

        /// <summary>
        /// 会戦結果1戦ぶんの「戦闘強度」係数（0以上）を返す。所要時間が長い接戦ほど学びが多い
        /// （長丁場＝拮抗）が、<see cref="DurationScaleSeconds"/> で飽和して頭打ちにする。
        /// 時間が0/負でも基準分（DurationWeight 抜き）は得られる。
        /// </summary>
        public static float Intensity(AutoBattleResult simResult)
        {
            // 所要時間→0..1 の飽和項（長いほど1へ漸近）。負/0は0。
            double dur = simResult.durationSeconds > 0.0 ? simResult.durationSeconds : 0.0;
            float scale = Mathf.Max(0.0001f, DurationScaleSeconds);
            float timeTerm = (float)(dur / (dur + scale)); // 0..1
            // 基準1.0＋時間項×重み（接戦の長丁場ほど上がるが頭打ち）。
            return 1f + DurationWeight * timeTerm;
        }

        /// <summary>
        /// 会戦結果（演習・実会戦）を経験収率（&gt;=0）に変換する純関数。
        /// ＝基準収率 × 戦闘強度（所要時間）× 実戦/演習倍率 × 反復疲労（逓減）。
        /// <paramref name="isRealBattle"/>=true は演習より高収率（<see cref="RealBattleMultiplier"/>）。
        /// <paramref name="repetitions"/> が増えるほど同訓練の飽和で逓減（diminishing returns）。
        /// </summary>
        public static float ExperienceYield(AutoBattleResult simResult, int repetitions, bool isRealBattle)
        {
            float modeMul = isRealBattle ? RealBattleMultiplier : DrillMultiplier;
            float yield = BaseYield * Intensity(simResult) * modeMul * TrainingFatigue(repetitions);
            return Mathf.Max(0f, yield);
        }

        /// <summary>
        /// 訓練難度（逆境ボーナス）込みの経験収率（&gt;=0）。
        /// ＝<see cref="ExperienceYield(AutoBattleResult,int,bool)"/> × <see cref="AdversarialBonus"/>。
        /// </summary>
        public static float ExperienceYield(AutoBattleResult simResult, int repetitions, bool isRealBattle, float trainingDifficulty)
        {
            float baseYield = ExperienceYield(simResult, repetitions, isRealBattle);
            return Mathf.Max(0f, baseYield * AdversarialBonus(trainingDifficulty));
        }

        /// <summary>
        /// 会戦結果を経験へ変換して <see cref="GrowthRules.GainExperience"/> へ供給する配線用ヘルパ。
        /// 成長そのもの（飽和カーブ・天井）は <see cref="GrowthRules"/> へ委譲し二重実装しない。
        /// <paramref name="growth"/> が null なら何もしない（null安全）。実際に加算した収率を返す（0以上）。
        /// dt はここでは収率を1回ぶん丸ごと渡す意図で 1.0 固定（<see cref="GrowthRules.GainExperience"/> 側で
        /// amount×speed×dt として積算される）。
        /// </summary>
        public static float ApplyTraining(Growth growth, AutoBattleResult simResult, int repetitions, bool isRealBattle, float trainingDifficulty)
        {
            float yield = ExperienceYield(simResult, repetitions, isRealBattle, trainingDifficulty);
            if (growth == null) return 0f;
            GrowthRules.GainExperience(growth, yield, OneCycleDt);
            return yield;
        }

        /// <summary>1サイクルぶんの時間係数（収率を丸ごと渡す＝逓減は repetitions 側で表現済み）。</summary>
        private const float OneCycleDt = 1f;
    }
}
