using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// シェルショック（戦闘消耗＝苛烈な戦闘・砲爆撃による即時的な戦闘ストレス反応）のパラメータ。
    /// readonly struct・全フィールド ctor でクランプ・盤面非依存の plain データ。
    /// 既存 <see cref="CombatTraumaParams"/>（長期PTSD）とは別＝こちらは戦闘中の即時的崩壊。
    /// </summary>
    public readonly struct ShellShockParams
    {
        /// <summary>砲爆撃の激しさが心理的衝撃に効く重み。</summary>
        public readonly float bombardmentWeight;
        /// <summary>砲爆撃の持続が心理的衝撃に効く重み。</summary>
        public readonly float durationWeight;
        /// <summary>限界突破曲線の指数（>1で限界を超えると急に崩れる）。</summary>
        public readonly float breakingExponent;
        /// <summary>機能停止（戦闘不能）の最大度（0..1）。</summary>
        public readonly float maxShutdown;
        /// <summary>パニック伝染の感度（部隊の密集で崩壊が伝わる強さ）。</summary>
        public readonly float contagionRate;
        /// <summary>戦闘経験がベテランの耐性に効く重み。</summary>
        public readonly float experienceWeight;
        /// <summary>精神的鍛錬がベテランの耐性に効く重み。</summary>
        public readonly float conditioningWeight;
        /// <summary>戦闘離脱が短期回復に効く重み。</summary>
        public readonly float removalWeight;
        /// <summary>休息が短期回復に効く重み。</summary>
        public readonly float restWeight;
        /// <summary>慢性化（長期トラウマへの分岐）の感度。</summary>
        public readonly float chronificationRate;

        public ShellShockParams(
            float bombardmentWeight,
            float durationWeight,
            float breakingExponent,
            float maxShutdown,
            float contagionRate,
            float experienceWeight,
            float conditioningWeight,
            float removalWeight,
            float restWeight,
            float chronificationRate)
        {
            this.bombardmentWeight = Mathf.Clamp01(bombardmentWeight);
            this.durationWeight = Mathf.Clamp01(durationWeight);
            this.breakingExponent = Mathf.Clamp(breakingExponent, 0.1f, 8f);
            this.maxShutdown = Mathf.Clamp01(maxShutdown);
            this.contagionRate = Mathf.Clamp01(contagionRate);
            this.experienceWeight = Mathf.Clamp01(experienceWeight);
            this.conditioningWeight = Mathf.Clamp01(conditioningWeight);
            this.removalWeight = Mathf.Clamp01(removalWeight);
            this.restWeight = Mathf.Clamp01(restWeight);
            this.chronificationRate = Mathf.Clamp01(chronificationRate);
        }

        /// <summary>
        /// 既定値。砲爆撃0.6/持続0.4・限界指数1.5・最大停止0.8・伝染0.5・
        /// 経験0.6/鍛錬0.4・離脱0.6/休息0.4・慢性化0.5。
        /// </summary>
        public static ShellShockParams Default => new ShellShockParams(
            bombardmentWeight: 0.6f,
            durationWeight: 0.4f,
            breakingExponent: 1.5f,
            maxShutdown: 0.8f,
            contagionRate: 0.5f,
            experienceWeight: 0.6f,
            conditioningWeight: 0.4f,
            removalWeight: 0.6f,
            restWeight: 0.4f,
            chronificationRate: 0.5f);
    }

    /// <summary>
    /// シェルショック（戦闘消耗＝苛烈な戦闘による即時的な戦闘不能）の純ロジック。すべて static・純関数。
    /// 砲爆撃の心理的衝撃→限界突破→機能停止（戦闘不能）→パニックの伝染、
    /// ベテランの耐性、戦闘離脱・休息による短期回復、慢性化（長期トラウマ）への分岐を扱う。
    ///
    /// 責務分担：
    /// - <see cref="CombatTraumaRules"/>（長期PTSD・慢性化した心的損傷）とは別系統＝本ルールは戦闘中の即時的崩壊。
    /// - 慢性化リスクは長期トラウマ系へ橋渡しする確率を返すだけ＝本ルールでは数式を二重実装しない。
    /// - 出力は0..1の係数・確率・度合いだけ＝呼び出し側（士気/戦闘継続）が乗算/減算する（実効値パターン・基準値非破壊）。
    /// 盤面非依存の plain 引数のみ・乱数なし・決定論。
    /// </summary>
    public static class ShellShockRules
    {
        // --- 砲爆撃の心理的衝撃 -------------------------------------------

        /// <summary>
        /// 砲爆撃の激しさ×持続で心理的衝撃が生まれる（0..1）。重み付き和。
        /// 激しく長い砲爆撃ほど大きな衝撃。
        /// </summary>
        public static float BombardmentShock(float bombardmentIntensity, float duration, ShellShockParams p)
        {
            float intensity = Mathf.Clamp01(bombardmentIntensity);
            float dur = Mathf.Clamp01(duration);

            float weightSum = p.bombardmentWeight + p.durationWeight;
            if (weightSum <= 0f) return 0f;

            float shock = (intensity * p.bombardmentWeight + dur * p.durationWeight) / weightSum;
            return Mathf.Clamp01(shock);
        }

        public static float BombardmentShock(float bombardmentIntensity, float duration)
            => BombardmentShock(bombardmentIntensity, duration, ShellShockParams.Default);

        // --- 限界突破 -----------------------------------------------------

        /// <summary>
        /// ストレス負荷/(1+耐性) で限界突破の度合い（0..1）。
        /// 個人の耐性が高いほどストレスを受け流す。指数曲線で限界を超えると急に崩れる。
        /// </summary>
        public static float BreakingPoint(float stressLoad, float individualResilience, ShellShockParams p)
        {
            float load = Mathf.Clamp01(stressLoad);
            float resilience = Mathf.Clamp01(individualResilience);

            float effective = load / (1f + resilience);
            float curved = Mathf.Pow(Mathf.Clamp01(effective), p.breakingExponent);
            return Mathf.Clamp01(curved);
        }

        public static float BreakingPoint(float stressLoad, float individualResilience)
            => BreakingPoint(stressLoad, individualResilience, ShellShockParams.Default);

        // --- 機能停止（戦闘不能）-----------------------------------------

        /// <summary>
        /// 心理的衝撃×限界突破で機能停止（戦闘不能）が生じる（0..maxShutdown）。
        /// 衝撃を受け限界を超えた兵が動けなくなる。呼び出し側が (1-本値) を乗算する想定。
        /// </summary>
        public static float FunctionalShutdown(float bombardmentShock, float breakingPoint, ShellShockParams p)
        {
            float shock = Mathf.Clamp01(bombardmentShock);
            float breaking = Mathf.Clamp01(breakingPoint);

            float shutdown = shock * breaking;
            return Mathf.Clamp(p.maxShutdown * shutdown, 0f, p.maxShutdown);
        }

        public static float FunctionalShutdown(float bombardmentShock, float breakingPoint)
            => FunctionalShutdown(bombardmentShock, breakingPoint, ShellShockParams.Default);

        // --- パニックの伝染 -----------------------------------------------

        /// <summary>
        /// 機能停止×部隊の密集でパニックが伝染する（0..1）。
        /// 密集した部隊ほど崩壊が伝わりやすい（兵が互いに恐怖を煽る）。
        /// </summary>
        public static float PanicContagion(float functionalShutdown, float unitProximity, ShellShockParams p)
        {
            float shutdown = Mathf.Clamp01(functionalShutdown);
            float proximity = Mathf.Clamp01(unitProximity);

            float spread = shutdown * proximity * p.contagionRate;
            return Mathf.Clamp01(spread);
        }

        public static float PanicContagion(float functionalShutdown, float unitProximity)
            => PanicContagion(functionalShutdown, unitProximity, ShellShockParams.Default);

        // --- ベテランの耐性 -----------------------------------------------

        /// <summary>
        /// 戦闘経験×精神的鍛錬でベテランの耐性が生まれる（0..1）。重み付き和。
        /// 場数を踏み鍛えられた兵ほど衝撃に耐える（BreakingPoint の resilience に入れる想定）。
        /// </summary>
        public static float VeteranImmunity(float combatExperience, float mentalConditioning, ShellShockParams p)
        {
            float experience = Mathf.Clamp01(combatExperience);
            float conditioning = Mathf.Clamp01(mentalConditioning);

            float weightSum = p.experienceWeight + p.conditioningWeight;
            if (weightSum <= 0f) return 0f;

            float immunity = (experience * p.experienceWeight + conditioning * p.conditioningWeight) / weightSum;
            return Mathf.Clamp01(immunity);
        }

        public static float VeteranImmunity(float combatExperience, float mentalConditioning)
            => VeteranImmunity(combatExperience, mentalConditioning, ShellShockParams.Default);

        // --- 短期回復 -----------------------------------------------------

        /// <summary>
        /// 戦闘からの離脱×休息で短期回復が進む（0..1の進捗）。重み付き和。
        /// 前線から退き休めば即時的なシェルショックは比較的早く回復する。
        /// </summary>
        public static float ShortTermRecovery(float removalFromCombat, float restTime, ShellShockParams p)
        {
            float removal = Mathf.Clamp01(removalFromCombat);
            float rest = Mathf.Clamp01(restTime);

            float weightSum = p.removalWeight + p.restWeight;
            if (weightSum <= 0f) return 0f;

            float recovery = (removal * p.removalWeight + rest * p.restWeight) / weightSum;
            return Mathf.Clamp01(recovery);
        }

        public static float ShortTermRecovery(float removalFromCombat, float restTime)
            => ShortTermRecovery(removalFromCombat, restTime, ShellShockParams.Default);

        // --- 慢性化リスク（長期トラウマへの分岐）-------------------------

        /// <summary>
        /// 機能停止×反復曝露で慢性化（長期トラウマへ移行）のリスク（0..1）。
        /// 戦闘不能を繰り返し受けるほど即時的崩壊が長期化する。
        /// CombatTraumaRules（長期PTSD）へ橋渡しする確率を返すだけ＝数式は二重実装しない。
        /// </summary>
        public static float ChronificationRisk(float functionalShutdown, float repeatedExposure, ShellShockParams p)
        {
            float shutdown = Mathf.Clamp01(functionalShutdown);
            float exposure = Mathf.Clamp01(repeatedExposure);

            float risk = shutdown * exposure * p.chronificationRate;
            return Mathf.Clamp01(risk);
        }

        public static float ChronificationRisk(float functionalShutdown, float repeatedExposure)
            => ChronificationRisk(functionalShutdown, repeatedExposure, ShellShockParams.Default);

        // --- 部隊の戦闘継続力 ---------------------------------------------

        /// <summary>
        /// (1-機能停止-パニック)＋耐性で部隊の戦闘継続力（0..1）。
        /// 機能停止とパニック伝染で削られ、ベテランの耐性が残量を底上げする。
        /// 呼び出し側が士気/火力に乗算する想定。
        /// </summary>
        public static float UnitCombatCapacity(float functionalShutdown, float panicContagion, float veteranImmunity, ShellShockParams p)
        {
            float shutdown = Mathf.Clamp01(functionalShutdown);
            float panic = Mathf.Clamp01(panicContagion);
            float immunity = Mathf.Clamp01(veteranImmunity);

            float baseCapacity = Mathf.Clamp01(1f - shutdown - panic);
            // 耐性は崩壊で失った余地（1-baseCapacity ぶん）を底上げする。
            float restored = baseCapacity + immunity * (1f - baseCapacity);
            return Mathf.Clamp01(restored);
        }

        public static float UnitCombatCapacity(float functionalShutdown, float panicContagion, float veteranImmunity)
            => UnitCombatCapacity(functionalShutdown, panicContagion, veteranImmunity, ShellShockParams.Default);
    }
}
