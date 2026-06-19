using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦闘トラウマ（兵士の長期的な精神的損傷＝PTSD・戦闘ストレス反応）のパラメータ。
    /// readonly struct・全フィールド ctor でクランプ・盤面非依存の plain データ。
    /// 既存 <see cref="CombatStressParams"/>（連戦の累積疲労＝短期）とは別＝こちらは長期の心的外傷。
    /// </summary>
    public readonly struct CombatTraumaParams
    {
        /// <summary>戦闘曝露がトラウマ蓄積に効く重み。</summary>
        public readonly float exposureWeight;
        /// <summary>惨事（虐殺・凄惨な死）への曝露がトラウマに効く重み。</summary>
        public readonly float atrocityWeight;
        /// <summary>戦友喪失がトラウマに効く重み。</summary>
        public readonly float comradeLossWeight;
        /// <summary>蓄積曲線の指数（>1で多重の惨事ほど跳ね上がる）。</summary>
        public readonly float accumulationExponent;
        /// <summary>戦友の絆が部隊の支えに効く重み。</summary>
        public readonly float bondsWeight;
        /// <summary>指揮官の配慮が部隊の支えに効く重み。</summary>
        public readonly float leadershipWeight;
        /// <summary>慢性化に未治療期間が効く感度（大きいほど早く慢性化）。</summary>
        public readonly float chronificationRate;
        /// <summary>戦闘能力低下の最大カット率（0..1）。</summary>
        public readonly float maxImpairment;
        /// <summary>休養が回復に効く重み。</summary>
        public readonly float restWeight;
        /// <summary>治療アクセスが回復に効く重み。</summary>
        public readonly float treatmentWeight;
        /// <summary>部隊の支えが回復に効く重み。</summary>
        public readonly float supportWeight;
        /// <summary>トラウマ伝播の感度（部隊密度で士気崩壊が伝わる強さ）。</summary>
        public readonly float contagionRate;

        public CombatTraumaParams(
            float exposureWeight,
            float atrocityWeight,
            float comradeLossWeight,
            float accumulationExponent,
            float bondsWeight,
            float leadershipWeight,
            float chronificationRate,
            float maxImpairment,
            float restWeight,
            float treatmentWeight,
            float supportWeight,
            float contagionRate)
        {
            this.exposureWeight = Mathf.Clamp01(exposureWeight);
            this.atrocityWeight = Mathf.Clamp01(atrocityWeight);
            this.comradeLossWeight = Mathf.Clamp01(comradeLossWeight);
            this.accumulationExponent = Mathf.Clamp(accumulationExponent, 0.1f, 8f);
            this.bondsWeight = Mathf.Clamp01(bondsWeight);
            this.leadershipWeight = Mathf.Clamp01(leadershipWeight);
            this.chronificationRate = Mathf.Clamp01(chronificationRate);
            this.maxImpairment = Mathf.Clamp01(maxImpairment);
            this.restWeight = Mathf.Clamp01(restWeight);
            this.treatmentWeight = Mathf.Clamp01(treatmentWeight);
            this.supportWeight = Mathf.Clamp01(supportWeight);
            this.contagionRate = Mathf.Clamp01(contagionRate);
        }

        /// <summary>
        /// 既定値。曝露0.4/惨事0.4/戦友喪失0.3/指数1.5・絆0.6/配慮0.4・慢性化0.5・
        /// 最大低下0.6・休養0.5/治療0.3/支え0.2・伝播0.5。
        /// </summary>
        public static CombatTraumaParams Default => new CombatTraumaParams(
            exposureWeight: 0.4f,
            atrocityWeight: 0.4f,
            comradeLossWeight: 0.3f,
            accumulationExponent: 1.5f,
            bondsWeight: 0.6f,
            leadershipWeight: 0.4f,
            chronificationRate: 0.5f,
            maxImpairment: 0.6f,
            restWeight: 0.5f,
            treatmentWeight: 0.3f,
            supportWeight: 0.2f,
            contagionRate: 0.5f);
    }

    /// <summary>
    /// 戦闘トラウマ（兵士の精神的外傷＝PTSD・戦闘ストレス反応）の純ロジック。すべて static・純関数。
    /// トラウマの蓄積（曝露×惨事×戦友喪失）→部隊の支え/孤立→慢性化→戦闘能力低下、
    /// 休養・治療・支えによる回復、密な部隊での伝播（士気崩壊）、精神的即応性を扱う。
    ///
    /// 責務分担：
    /// - <see cref="CombatStressRules"/>（連戦の短期累積疲労）とは別系統＝本ルールは長期の心的損傷。
    /// - 出力は0..1の係数・確率・進捗だけ＝呼び出し側（士気/能力）が乗算/減算する（実効値パターン・基準値非破壊）。
    /// 盤面非依存の plain 引数のみ・乱数なし・決定論。
    /// </summary>
    public static class CombatTraumaRules
    {
        // --- 蓄積 ---------------------------------------------------------

        /// <summary>
        /// 戦闘曝露×惨事の目撃×戦友喪失でトラウマが蓄積する（0..1）。
        /// 重み付き和を正規化し、指数曲線で多重の惨事ほど跳ね上がる。
        /// </summary>
        public static float TraumaAccumulation(float combatExposure, float atrocityWitnessed, float lossOfComrades, CombatTraumaParams p)
        {
            float exposure = Mathf.Clamp01(combatExposure);
            float atrocity = Mathf.Clamp01(atrocityWitnessed);
            float loss = Mathf.Clamp01(lossOfComrades);

            float weightSum = p.exposureWeight + p.atrocityWeight + p.comradeLossWeight;
            if (weightSum <= 0f) return 0f;

            float weighted = (exposure * p.exposureWeight
                            + atrocity * p.atrocityWeight
                            + loss * p.comradeLossWeight) / weightSum;
            float curved = Mathf.Pow(Mathf.Clamp01(weighted), p.accumulationExponent);
            return Mathf.Clamp01(curved);
        }

        public static float TraumaAccumulation(float combatExposure, float atrocityWitnessed, float lossOfComrades)
            => TraumaAccumulation(combatExposure, atrocityWitnessed, lossOfComrades, CombatTraumaParams.Default);

        // --- 支え・孤立 ---------------------------------------------------

        /// <summary>戦友の絆×指揮官の配慮で部隊の支えが生まれる（0..1）。重み付き和。</summary>
        public static float UnitSupport(float comradeBonds, float leadershipCare, CombatTraumaParams p)
        {
            float bonds = Mathf.Clamp01(comradeBonds);
            float care = Mathf.Clamp01(leadershipCare);

            float weightSum = p.bondsWeight + p.leadershipWeight;
            if (weightSum <= 0f) return 0f;

            float support = (bonds * p.bondsWeight + care * p.leadershipWeight) / weightSum;
            return Mathf.Clamp01(support);
        }

        public static float UnitSupport(float comradeBonds, float leadershipCare)
            => UnitSupport(comradeBonds, leadershipCare, CombatTraumaParams.Default);

        /// <summary>支えがないと孤立する＝(1-支え)で孤立度（0..1）。</summary>
        public static float IsolationFactor(float unitSupport, CombatTraumaParams p)
        {
            float support = Mathf.Clamp01(unitSupport);
            return Mathf.Clamp01(1f - support);
        }

        public static float IsolationFactor(float unitSupport)
            => IsolationFactor(unitSupport, CombatTraumaParams.Default);

        // --- 慢性化 -------------------------------------------------------

        /// <summary>
        /// トラウマ×孤立×未治療期間で慢性化が進む（0..1）。
        /// 未治療期間は chronificationRate で飽和（長引くほど慢性化が確実に近づく）。
        /// </summary>
        public static float Chronification(float traumaAccumulation, float isolationFactor, float untreatedTime, CombatTraumaParams p)
        {
            float trauma = Mathf.Clamp01(traumaAccumulation);
            float isolation = Mathf.Clamp01(isolationFactor);
            float time = Mathf.Max(0f, untreatedTime);

            // 未治療期間を 0..1 へ飽和（rate が大きいほど早く1へ近づく）。
            float timeFactor = Mathf.Clamp01(p.chronificationRate * time);
            float chronic = trauma * isolation * timeFactor;
            return Mathf.Clamp01(chronic);
        }

        public static float Chronification(float traumaAccumulation, float isolationFactor, float untreatedTime)
            => Chronification(traumaAccumulation, isolationFactor, untreatedTime, CombatTraumaParams.Default);

        // --- 戦闘能力への影響 ---------------------------------------------

        /// <summary>
        /// トラウマ×慢性化で戦闘能力が落ちる（0..maxImpairment＝低下幅）。
        /// 慢性化したトラウマほど深く能力を蝕む。呼び出し側が (1-本値) を乗算する想定。
        /// </summary>
        public static float CombatImpairment(float traumaAccumulation, float chronification, CombatTraumaParams p)
        {
            float trauma = Mathf.Clamp01(traumaAccumulation);
            float chronic = Mathf.Clamp01(chronification);

            // 急性ぶん＋慢性化で増幅したぶん。慢性ほど重く効く。
            float severity = trauma * (1f + chronic) * 0.5f;
            return Mathf.Clamp(p.maxImpairment * Mathf.Clamp01(severity), 0f, p.maxImpairment);
        }

        public static float CombatImpairment(float traumaAccumulation, float chronification)
            => CombatImpairment(traumaAccumulation, chronification, CombatTraumaParams.Default);

        // --- 回復 ---------------------------------------------------------

        /// <summary>
        /// 休養×治療アクセス×部隊の支えで回復が進む（0..1の進捗）。重み付き和。
        /// </summary>
        public static float RecoveryProgress(float restPeriod, float treatmentAccess, float unitSupport, CombatTraumaParams p)
        {
            float rest = Mathf.Clamp01(restPeriod);
            float treatment = Mathf.Clamp01(treatmentAccess);
            float support = Mathf.Clamp01(unitSupport);

            float weightSum = p.restWeight + p.treatmentWeight + p.supportWeight;
            if (weightSum <= 0f) return 0f;

            float progress = (rest * p.restWeight
                            + treatment * p.treatmentWeight
                            + support * p.supportWeight) / weightSum;
            return Mathf.Clamp01(progress);
        }

        public static float RecoveryProgress(float restPeriod, float treatmentAccess, float unitSupport)
            => RecoveryProgress(restPeriod, treatmentAccess, unitSupport, CombatTraumaParams.Default);

        // --- 伝播 ---------------------------------------------------------

        /// <summary>
        /// トラウマ×部隊の密度（凝集度）で士気崩壊が伝播する（0..1）。
        /// 密な部隊ほどトラウマが伝わりやすい（兵が互いに影響し合う）。
        /// </summary>
        public static float TraumaContagion(float traumaAccumulation, float unitCohesion, CombatTraumaParams p)
        {
            float trauma = Mathf.Clamp01(traumaAccumulation);
            float cohesion = Mathf.Clamp01(unitCohesion);
            float spread = trauma * cohesion * p.contagionRate;
            return Mathf.Clamp01(spread);
        }

        public static float TraumaContagion(float traumaAccumulation, float unitCohesion)
            => TraumaContagion(traumaAccumulation, unitCohesion, CombatTraumaParams.Default);

        // --- 精神的即応性 -------------------------------------------------

        /// <summary>
        /// (1-トラウマ)＋回復で精神的即応性（0..1）。
        /// トラウマが浅いほど・回復が進むほど高い。回復はトラウマ残量を埋め戻す。
        /// </summary>
        public static float PsychologicalReadiness(float traumaAccumulation, float recoveryProgress, CombatTraumaParams p)
        {
            float trauma = Mathf.Clamp01(traumaAccumulation);
            float recovery = Mathf.Clamp01(recoveryProgress);

            float baseReadiness = 1f - trauma;
            // 回復はトラウマで失った余地（trauma ぶん）を埋め戻す。
            float restored = baseReadiness + recovery * trauma;
            return Mathf.Clamp01(restored);
        }

        public static float PsychologicalReadiness(float traumaAccumulation, float recoveryProgress)
            => PsychologicalReadiness(traumaAccumulation, recoveryProgress, CombatTraumaParams.Default);
    }
}
