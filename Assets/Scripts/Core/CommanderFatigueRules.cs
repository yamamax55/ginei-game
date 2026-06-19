using UnityEngine;

namespace Ginei
{
    /// <summary>指揮官個人の疲労・消耗（連戦による判断力低下）の調整係数。</summary>
    public readonly struct CommanderFatigueParams
    {
        /// <summary>作戦強度1.0・1日あたり積む疲労量（体力で割られる前の基準）。</summary>
        public readonly float accumPerDay;
        /// <summary>体力（stamina）が蓄積を緩める効き＝実効除数は 1+stamina×staminaResistance。</summary>
        public readonly float staminaResistance;
        /// <summary>休養1日（quality 1.0）あたり抜く疲労量。</summary>
        public readonly float recoveryPerDay;
        /// <summary>判断力低下の非線形カーブ指数（>1で終盤に急落・Pow近似）。</summary>
        public readonly float impairmentExponent;
        /// <summary>判断力低下の最大幅（疲労最大で実効能力を最大この割合まで削る）。</summary>
        public readonly float impairmentMax;
        /// <summary>集中の途切れの基準率（疲労×複雑さ1.0でこの値）。</summary>
        public readonly float concentrationScale;
        /// <summary>誤判断リスクの基準率（判断低下×局面の重さ1.0でこの値）。</summary>
        public readonly float misjudgmentScale;
        /// <summary>燃え尽き（過労）と見なす、疲労が回復力を超える超過幅の閾値。</summary>
        public readonly float burnoutMargin;
        /// <summary>指揮に耐えると見なす実効能力の既定閾値。</summary>
        public readonly float fitnessThreshold;

        public CommanderFatigueParams(float accumPerDay, float staminaResistance, float recoveryPerDay,
                                      float impairmentExponent, float impairmentMax, float concentrationScale,
                                      float misjudgmentScale, float burnoutMargin, float fitnessThreshold)
        {
            this.accumPerDay = Mathf.Max(0f, accumPerDay);
            this.staminaResistance = Mathf.Max(0f, staminaResistance);
            this.recoveryPerDay = Mathf.Max(0f, recoveryPerDay);
            this.impairmentExponent = Mathf.Max(1f, impairmentExponent);
            this.impairmentMax = Mathf.Clamp01(impairmentMax);
            this.concentrationScale = Mathf.Clamp01(concentrationScale);
            this.misjudgmentScale = Mathf.Clamp01(misjudgmentScale);
            this.burnoutMargin = Mathf.Clamp01(burnoutMargin);
            this.fitnessThreshold = Mathf.Max(0f, fitnessThreshold);
        }

        /// <summary>
        /// 既定＝蓄積0.08/日・体力抵抗1.0・回復0.1/日・低下指数2.0・低下最大0.8・
        /// 集中途切れ0.6・誤判断0.7・燃え尽き超過0.3・適性閾値40。
        /// 体力50（=0.5正規化）で実効除数1.5＝疲労蓄積は体力で緩む。判断低下は二乗カーブで終盤に急落。
        /// </summary>
        public static CommanderFatigueParams Default =>
            new CommanderFatigueParams(0.08f, 1.0f, 0.1f, 2.0f, 0.8f, 0.6f, 0.7f, 0.3f, 40f);
    }

    /// <summary>
    /// 指揮官個人の疲労・消耗（連戦による判断力低下）の純ロジック。
    /// 連戦による蓄積疲労、休養による回復、疲労が判断力・集中力に与える低下、極度の疲労による誤判断、
    /// 休養と即応のトレードオフ（休めば回復するが盤面を離れる）を扱う。
    /// 部隊単位の累積戦闘疲弊 <see cref="CombatFatigueRules"/> とは別＝こちらは「提督個人」の判断力消耗。
    /// 実効能力は基準能力に低下倍率を掛けて出す（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommanderFatigueRules
    {
        /// <summary>能力値（0..100想定）を 0..1 へ正規化。</summary>
        private const float StatNormalizer = 100f;

        /// <summary>
        /// 疲労蓄積（戻り＝0..1）。作戦強度×日数を「体力で緩めた実効除数」で割って積む＝
        /// 連戦（高強度×長期）ほど溜まり、体力が高いほど溜まりにくい。
        /// </summary>
        public static float FatigueAccumulation(float operationIntensity, float durationDays, float stamina, CommanderFatigueParams p)
        {
            float intensity = Mathf.Clamp01(operationIntensity);
            float days = Mathf.Max(0f, durationDays);
            float sta = Mathf.Clamp01(stamina / StatNormalizer);
            float divisor = 1f + sta * p.staminaResistance;
            float add = intensity * days * p.accumPerDay / divisor;
            return Mathf.Clamp01(add);
        }

        public static float FatigueAccumulation(float operationIntensity, float durationDays, float stamina)
            => FatigueAccumulation(operationIntensity, durationDays, stamina, CommanderFatigueParams.Default);

        /// <summary>
        /// 休養による回復（戻り＝残存疲労0..1）。休養日数×質ぶん疲労を減らす＝休めば判断力が戻る。
        /// 0未満には行かない（clamp）。
        /// </summary>
        public static float RestRecovery(float fatigue, float restDays, float restQuality, CommanderFatigueParams p)
        {
            float cur = Mathf.Clamp01(fatigue);
            float days = Mathf.Max(0f, restDays);
            float quality = Mathf.Clamp01(restQuality);
            float recovered = cur - days * quality * p.recoveryPerDay;
            return Mathf.Clamp01(recovered);
        }

        public static float RestRecovery(float fatigue, float restDays, float restQuality)
            => RestRecovery(fatigue, restDays, restQuality, CommanderFatigueParams.Default);

        /// <summary>
        /// 判断力低下（戻り＝0..1・1で完全に判断不能）。疲労が高いほど低下が大きいが非線形＝
        /// 疲労が浅いうちは粘り、終盤で急落する（Pow 近似のカーブ）。最大 impairmentMax まで。
        /// </summary>
        public static float JudgmentImpairment(float fatigue, CommanderFatigueParams p)
        {
            float f = Mathf.Clamp01(fatigue);
            float curve = Mathf.Pow(f, p.impairmentExponent);
            return Mathf.Clamp01(curve * p.impairmentMax);
        }

        public static float JudgmentImpairment(float fatigue)
            => JudgmentImpairment(fatigue, CommanderFatigueParams.Default);

        /// <summary>
        /// 集中の途切れ（戻り＝0..1）。疲労×課題の複雑さで集中が切れる＝
        /// 疲れた頭で複雑な局面を捌くほど見落とす。
        /// </summary>
        public static float ConcentrationLapse(float fatigue, float taskComplexity, CommanderFatigueParams p)
        {
            float f = Mathf.Clamp01(fatigue);
            float complexity = Mathf.Clamp01(taskComplexity);
            return Mathf.Clamp01(f * complexity * p.concentrationScale);
        }

        public static float ConcentrationLapse(float fatigue, float taskComplexity)
            => ConcentrationLapse(fatigue, taskComplexity, CommanderFatigueParams.Default);

        /// <summary>
        /// 誤判断リスク（戻り＝0..1）＝判断力低下×局面の重さ。乱数なし＝確率を返すのみ。
        /// 同じ判断低下でも、重い局面（stakes 大）ほど一手の誤りが致命的になる。
        /// </summary>
        public static float MisjudgmentRisk(float judgmentImpairment, float stakes, CommanderFatigueParams p)
        {
            float impair = Mathf.Clamp01(judgmentImpairment);
            float s = Mathf.Clamp01(stakes);
            return Mathf.Clamp01(impair * s * p.misjudgmentScale);
        }

        public static float MisjudgmentRisk(float judgmentImpairment, float stakes)
            => MisjudgmentRisk(judgmentImpairment, stakes, CommanderFatigueParams.Default);

        /// <summary>
        /// 実効能力（戻り＝基準と同単位）＝基準能力×(1−判断力低下)。基準能力は書き換えない（実効値パターン）。
        /// 疲労で判断力が落ちると、同じ提督でも会戦で出せる力が下がる。
        /// </summary>
        public static float EffectiveCompetence(float baseCompetence, float judgmentImpairment, CommanderFatigueParams p)
        {
            float bc = Mathf.Max(0f, baseCompetence);
            float impair = Mathf.Clamp01(judgmentImpairment);
            return Mathf.Max(0f, bc * (1f - impair));
        }

        public static float EffectiveCompetence(float baseCompetence, float judgmentImpairment)
            => EffectiveCompetence(baseCompetence, judgmentImpairment, CommanderFatigueParams.Default);

        /// <summary>
        /// 燃え尽き（過労）判定。疲労が回復力（resilience 0..100想定）を burnoutMargin 超えたら true＝
        /// 回復力で支えきれないほど疲労が溜まると壊れる。後方へ下げる・休養させる判断材料。
        /// </summary>
        public static bool BurnoutThreshold(float fatigue, float resilience, CommanderFatigueParams p)
        {
            float f = Mathf.Clamp01(fatigue);
            float res = Mathf.Clamp01(resilience / StatNormalizer);
            return f - res >= p.burnoutMargin;
        }

        public static bool BurnoutThreshold(float fatigue, float resilience)
            => BurnoutThreshold(fatigue, resilience, CommanderFatigueParams.Default);

        /// <summary>
        /// 指揮に耐えるか＝実効能力が閾値以上。疲労で実効能力が落ちきると、指揮を任せられない。
        /// </summary>
        public static bool IsFitForCommand(float effectiveCompetence, float threshold)
        {
            return Mathf.Max(0f, effectiveCompetence) >= Mathf.Max(0f, threshold);
        }

        /// <summary>既定閾値（<see cref="CommanderFatigueParams.fitnessThreshold"/>）での適性判定。</summary>
        public static bool IsFitForCommand(float effectiveCompetence, CommanderFatigueParams p)
            => IsFitForCommand(effectiveCompetence, p.fitnessThreshold);

        public static bool IsFitForCommand(float effectiveCompetence)
            => IsFitForCommand(effectiveCompetence, CommanderFatigueParams.Default.fitnessThreshold);
    }
}
