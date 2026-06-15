using UnityEngine;

namespace Ginei
{
    /// <summary>指揮官個人の運用負担（疲労・判断負荷・睡眠不足）の純データ。回復可能な一時的疲弊。</summary>
    public struct CommanderBurden
    {
        /// <summary>疲労（0..1＝連続作戦で蓄積し休息で回復する一時的消耗）。</summary>
        public float fatigue;
        /// <summary>判断負荷（0..1＝抱え込んだ決断の量。委任できないほど高い）。</summary>
        public float decisionLoad;
        /// <summary>睡眠不足（0..1＝連日の判断で積み上がる睡眠負債）。</summary>
        public float sleepDebt;

        public CommanderBurden(float fatigue, float decisionLoad, float sleepDebt)
        {
            this.fatigue = Mathf.Clamp01(fatigue);
            this.decisionLoad = Mathf.Clamp01(decisionLoad);
            this.sleepDebt = Mathf.Clamp01(sleepDebt);
        }
    }

    /// <summary>指揮官の運用消耗の調整係数。</summary>
    public readonly struct CommanderBurdenParams
    {
        /// <summary>疲労の蓄積率（運用テンポ×責任の重さ1.0あたり per dt）。</summary>
        public readonly float accumulationRate;
        /// <summary>休息の回復率（休息1.0あたり per dt＝恒久的衰えと違い回復できる）。</summary>
        public readonly float recoveryRate;
        /// <summary>判断力低下に対する睡眠不足の寄与（疲労に上乗せされる重み）。</summary>
        public readonly float sleepDebtWeight;
        /// <summary>判断力低下の最大幅（疲労・睡眠不足が満タンでも実効はこのぶんまで落ちる＝下限）。</summary>
        public readonly float maxImpairment;
        /// <summary>抱え込みの罠の加速率（委任しきれない判断負荷が疲労蓄積を増す倍率）。</summary>
        public readonly float micromanageRate;
        /// <summary>燃え尽きの臨界疲労（これを超え長く続くと一時的でなく深刻な消耗に近づく）。</summary>
        public readonly float burnoutThreshold;
        /// <summary>交代要員の回復ボーナス（交代1.0あたり回復率に上乗せ＝ローテーション）。</summary>
        public readonly float rotationReliefRate;

        public CommanderBurdenParams(float accumulationRate, float recoveryRate, float sleepDebtWeight,
                                     float maxImpairment, float micromanageRate, float burnoutThreshold,
                                     float rotationReliefRate)
        {
            this.accumulationRate = Mathf.Max(0f, accumulationRate);
            this.recoveryRate = Mathf.Max(0f, recoveryRate);
            this.sleepDebtWeight = Mathf.Clamp01(sleepDebtWeight);
            this.maxImpairment = Mathf.Clamp01(maxImpairment);
            this.micromanageRate = Mathf.Max(0f, micromanageRate);
            this.burnoutThreshold = Mathf.Clamp01(burnoutThreshold);
            this.rotationReliefRate = Mathf.Max(0f, rotationReliefRate);
        }

        /// <summary>既定＝蓄積率0.05/回復率0.08/睡眠不足重み0.5/最大判断低下0.5/抱え込み加速0.5/燃え尽き臨界0.7/交代回復0.12。</summary>
        public static CommanderBurdenParams Default =>
            new CommanderBurdenParams(0.05f, 0.08f, 0.5f, 0.5f, 0.5f, 0.7f, 0.12f);
    }

    /// <summary>
    /// 長期遊撃戦の指揮官消耗の純ロジック（#1400・スペイン内戦/長期戦型）。長期の連続作戦・遊撃戦で、
    /// 指揮官個人が運用の負担（連日の判断・睡眠不足・責任の重圧）で疲弊し、判断力・能力が一時的に低下する。
    /// ただしこれは休息・交代で回復可能な疲労＝恒久的な加齢の衰えとは違う＝疲れた指揮官は判断を誤りやすいが、
    /// 休ませれば戻る。<see cref="ReadinessRules"/>（部隊の即応疲労＝警戒水準の張り過ぎ）／
    /// <see cref="SenescenceRules"/>（加齢＝恒久的な能力の衰え・戻らない下り坂）／
    /// <see cref="CombatFatigueRules"/>（部隊の累積疲弊）／<see cref="CapacityRules"/>（器量＝委任能力）とは別物で、
    /// 本クラスは「指揮官個人の運用疲弊（回復可能）」を扱い <see cref="CommanderBurden"/> が中核データ。
    /// 倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommanderBurdenRules
    {
        /// <summary>
        /// 疲労の蓄積＝連続作戦の運用テンポ×責任の重さ×accumulationRate を加算した新しい疲労（0..1）。
        /// 連日の判断と責任の重圧が指揮官を消耗させる。戻り値は更新後の疲労。
        /// </summary>
        public static float BurdenAccumulation(float fatigue, float operationalTempo,
                                               float responsibilityWeight, float dt, CommanderBurdenParams p)
        {
            float drive = Mathf.Clamp01(operationalTempo) * Mathf.Clamp01(responsibilityWeight);
            return Mathf.Clamp01(Mathf.Clamp01(fatigue) + drive * p.accumulationRate * Mathf.Max(0f, dt));
        }

        public static float BurdenAccumulation(float fatigue, float operationalTempo,
                                               float responsibilityWeight, float dt)
            => BurdenAccumulation(fatigue, operationalTempo, responsibilityWeight, dt, CommanderBurdenParams.Default);

        /// <summary>
        /// 休息による疲労の回復＝休息の深さ×recoveryRate ぶん疲労を減らした新しい疲労（0..1）。
        /// 恒久的な加齢の衰えと違い、休めば戻るのが運用疲弊の本質。戻り値は更新後の疲労。
        /// </summary>
        public static float RestRecovery(float fatigue, float restDuration, float dt, CommanderBurdenParams p)
        {
            float relief = Mathf.Clamp01(restDuration) * p.recoveryRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(fatigue) - relief);
        }

        public static float RestRecovery(float fatigue, float restDuration, float dt)
            => RestRecovery(fatigue, restDuration, dt, CommanderBurdenParams.Default);

        /// <summary>
        /// 判断力の一時的低下（0..1＝1なら満額、低いほど誤る）。疲労に睡眠不足を sleepDebtWeight で
        /// 上乗せした負担を maxImpairment 幅で効かせる＝疲れて寝ていない指揮官ほど判断を誤りやすい。
        /// 実効値＜1.0（基準は書き換えず、これを能力に掛ける）。
        /// </summary>
        public static float JudgmentImpairment(float fatigue, float sleepDebt, CommanderBurdenParams p)
        {
            float load = Mathf.Clamp01(Mathf.Clamp01(fatigue) + Mathf.Clamp01(sleepDebt) * p.sleepDebtWeight);
            return 1f - load * p.maxImpairment;
        }

        public static float JudgmentImpairment(float fatigue, float sleepDebt)
            => JudgmentImpairment(fatigue, sleepDebt, CommanderBurdenParams.Default);

        /// <summary>
        /// 決断の質（0..1）＝判断力低下が複雑な決断ほど質を落とす。単純な決断（complexity 0）は
        /// 疲れていても質を保てるが、複雑な決断（complexity 1）は判断力低下がそのまま響く。
        /// judgmentImpairment は <see cref="JudgmentImpairment"/> の実効値（0..1）を渡す。
        /// </summary>
        public static float DecisionQualityDecay(float judgmentImpairment, float decisionComplexity)
        {
            float impair = Mathf.Clamp01(judgmentImpairment);
            float c = Mathf.Clamp01(decisionComplexity);
            return Mathf.Lerp(1f, impair, c);
        }

        /// <summary>
        /// 抱え込みの罠＝委任しきれない判断負荷が疲労蓄積を加速する倍率（1以上）。委任能力
        /// （delegationCapacity）が高いほど負荷を任せられ罠は浅い。委任できない指揮官ほど自分を
        /// すり減らす。<see cref="CapacityRules"/>（器量）の委任能力と接続する想定。
        /// </summary>
        public static float MicromanagementTrap(float decisionLoad, float delegationCapacity, CommanderBurdenParams p)
        {
            float unshed = Mathf.Clamp01(decisionLoad) * (1f - Mathf.Clamp01(delegationCapacity));
            return 1f + unshed * p.micromanageRate;
        }

        public static float MicromanagementTrap(float decisionLoad, float delegationCapacity)
            => MicromanagementTrap(decisionLoad, delegationCapacity, CommanderBurdenParams.Default);

        /// <summary>
        /// 交代要員による回復＝交代要員がいれば指揮官を休ませて疲労を回復させられる（ローテーション）。
        /// replacementAvailable×rotationReliefRate ぶん疲労を減らした新しい疲労（0..1）。交代要員が
        /// いなければ（0）休ませられず回復しない。戻り値は更新後の疲労。
        /// </summary>
        public static float RotationRelief(float fatigue, float replacementAvailable, float dt, CommanderBurdenParams p)
        {
            float relief = Mathf.Clamp01(replacementAvailable) * p.rotationReliefRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(fatigue) - relief);
        }

        public static float RotationRelief(float fatigue, float replacementAvailable, float dt)
            => RotationRelief(fatigue, replacementAvailable, dt, CommanderBurdenParams.Default);

        /// <summary>
        /// 燃え尽きリスク（0..1）＝疲労が臨界（burnoutThreshold）を超え、それが長く続くほど、
        /// 一時的でなく深刻な消耗（燃え尽き）に近づく。臨界以下なら0＝休息で戻る範囲。臨界超過分と
        /// 持続時間（sustainedDuration 0..1）の積で立ち上がる。
        /// </summary>
        public static float BurnoutRisk(float fatigue, float sustainedDuration, CommanderBurdenParams p)
        {
            float f = Mathf.Clamp01(fatigue);
            if (f <= p.burnoutThreshold) return 0f;
            float overshoot = (f - p.burnoutThreshold) / Mathf.Max(0.0001f, 1f - p.burnoutThreshold);
            return Mathf.Clamp01(overshoot * Mathf.Clamp01(sustainedDuration));
        }

        public static float BurnoutRisk(float fatigue, float sustainedDuration)
            => BurnoutRisk(fatigue, sustainedDuration, CommanderBurdenParams.Default);

        /// <summary>
        /// 指揮官が疲弊し判断力が落ちたか＝疲労が threshold を超え、かつ判断力低下が（1−threshold）を
        /// 下回った（=判断が threshold ぶん以上鈍った）とき true＝休息・交代が要る合図。
        /// </summary>
        public static bool IsCommandFatigued(float fatigue, float judgmentImpairment, float threshold)
        {
            float t = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(fatigue) > t && Mathf.Clamp01(judgmentImpairment) < 1f - t;
        }
    }
}
