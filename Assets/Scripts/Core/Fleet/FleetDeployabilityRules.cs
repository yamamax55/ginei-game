using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊即応性（deployability）評価の調整係数（MILDEP-1）。
    /// 各係数の重みと出撃可否の閾値だけを持ち、物理量の計算は既存ルールへ委譲する。
    /// </summary>
    public readonly struct FleetDeployabilityParams
    {
        /// <summary>補給レディネス（弾薬/燃料/糧食）の重み。0..1。</summary>
        public readonly float supplyWeight;
        /// <summary>乗員疲労（継戦による効率低下）の重み。0..1。</summary>
        public readonly float fatigueWeight;
        /// <summary>船体整備状態（材料の摩耗・保守状態）の重み。0..1。</summary>
        public readonly float conditionWeight;
        /// <summary>出撃可能とみなす即応性の下限（これ未満は要整備＝出撃見送り）。0..1。</summary>
        public readonly float deployableThreshold;
        /// <summary>総合即応性が下回ると即応性ゼロ扱いにする床（最小律のハードフロア）。0..1。</summary>
        public readonly float criticalFloor;

        public FleetDeployabilityParams(float supplyWeight, float fatigueWeight, float conditionWeight,
                                        float deployableThreshold, float criticalFloor)
        {
            // 重みは非負へ丸める（合計の正規化は計算側で行う）。
            this.supplyWeight = Mathf.Max(0f, supplyWeight);
            this.fatigueWeight = Mathf.Max(0f, fatigueWeight);
            this.conditionWeight = Mathf.Max(0f, conditionWeight);
            this.deployableThreshold = Mathf.Clamp01(deployableThreshold);
            this.criticalFloor = Mathf.Clamp01(criticalFloor);
        }

        /// <summary>既定＝補給0.45・疲労0.30・整備0.25・出撃閾値0.40・危機床0.15。</summary>
        public static FleetDeployabilityParams Default
            => new FleetDeployabilityParams(0.45f, 0.30f, 0.25f, 0.40f, 0.15f);
    }

    /// <summary>
    /// 艦隊即応性評価の結果（各内訳と総合即応性・出撃可否）。
    /// </summary>
    public readonly struct FleetDeployabilityResult
    {
        /// <summary>補給による即応性内訳 0..1（<see cref="MilitaryReadinessRules.OverallReadiness"/> 由来）。</summary>
        public readonly float supplyFactor;
        /// <summary>乗員疲労による即応性内訳 0..1（<see cref="CombatFatigueRules.CombatEffectivenessDecay"/> 由来）。</summary>
        public readonly float crewFactor;
        /// <summary>船体整備状態による即応性内訳 0..1（<see cref="MothballRules.ReactivatedEffectiveness"/> 由来）。</summary>
        public readonly float conditionFactor;
        /// <summary>総合即応性 0..1（最も欠けた要素に律速される重み付き合成）。</summary>
        public readonly float deployability;
        /// <summary>出撃可能か（総合即応性が閾値以上）。</summary>
        public readonly bool isDeployable;

        public FleetDeployabilityResult(float supplyFactor, float crewFactor, float conditionFactor,
                                        float deployability, bool isDeployable)
        {
            this.supplyFactor = supplyFactor;
            this.crewFactor = crewFactor;
            this.conditionFactor = conditionFactor;
            this.deployability = deployability;
            this.isDeployable = isDeployable;
        }
    }

    /// <summary>
    /// 艦隊即応性（deployability）の純ロジック（MILDEP-1）。
    /// これまで別々の窓口に分かれていた三つの即応性トラック＝
    /// <b>補給レディネス</b>（<see cref="MilitaryReadinessRules"/>）／<b>乗員疲労</b>（<see cref="CombatFatigueRules"/>）／
    /// <b>船体整備状態</b>（<see cref="MothballRules"/>）を、ひとつの<b>即応性係数</b>と<b>出撃可否</b>へ集約する単一窓口。
    /// 物理量（補給の最小律・疲労の効率低下・整備状態の実効率）は既存ルールへ委譲し、ここでは合成と出撃判定のみを行う＝
    /// 「補給は満ちていても乗員が疲弊していれば出せない」「整備の済んでいない艦は前線に出せない」を一箇所で表す。
    /// 合成は重み付き相乗（幾何平均型＝一要素が欠けると全体が崩れる）＋危機床のハードフロア。
    /// 戦略Tick（暦境界）が艦隊単位で呼ぶ集約API＝個艦へ降りない（終盤ラグ回避）。
    /// 純ロジック・決定論・null安全・test-first。
    /// </summary>
    public static class FleetDeployabilityRules
    {
        /// <summary>
        /// 補給即応性内訳＝弾薬/燃料/糧食の充足からリービッヒの最小律で 0..1。
        /// <see cref="MilitaryReadinessRules.OverallReadiness"/> へ委譲。
        /// </summary>
        public static float SupplyFactor(float ammo, float fuel, float provision)
            => MilitaryReadinessRules.OverallReadiness(ammo, fuel, provision);

        /// <summary>
        /// 乗員即応性内訳＝疲労による戦闘効率低下を即応性として 0..1。
        /// <see cref="CombatFatigueRules.CombatEffectivenessDecay"/> へ委譲（疲労0で1.0・疲労蓄積で低下）。
        /// </summary>
        public static float CrewFactor(float fatigue)
            => CombatFatigueRules.CombatEffectivenessDecay(fatigue);

        /// <summary>乗員即応性内訳（<see cref="CombatFatigueParams"/> 指定版）。</summary>
        public static float CrewFactor(float fatigue, CombatFatigueParams p)
            => CombatFatigueRules.CombatEffectivenessDecay(fatigue, p);

        /// <summary>
        /// 整備即応性内訳＝船体整備状態（0=朽ち果て/1=万全）から実効率を 0..1。
        /// <see cref="MothballRules.ReactivatedEffectiveness"/> へ委譲（保守不足は下限へ漸近）。
        /// </summary>
        public static float ConditionFactor(float condition)
            => MothballRules.ReactivatedEffectiveness(condition);

        /// <summary>整備即応性内訳（<see cref="MothballParams"/> 指定版）。</summary>
        public static float ConditionFactor(float condition, MothballParams p)
            => MothballRules.ReactivatedEffectiveness(condition, p);

        /// <summary>
        /// 内訳から総合即応性 0..1 を合成する（重み付き相乗＝幾何平均型）。
        /// 一要素が欠けると全体が崩れる（補給満タンでも乗員疲弊で出せない）＝最小律の体感。
        /// 危機床（<see cref="FleetDeployabilityParams.criticalFloor"/>）未満なら 0（出撃不能）。
        /// </summary>
        public static float Compose(float supplyFactor, float crewFactor, float conditionFactor,
                                    FleetDeployabilityParams p)
        {
            float s = Mathf.Clamp01(supplyFactor);
            float c = Mathf.Clamp01(crewFactor);
            float k = Mathf.Clamp01(conditionFactor);

            float wSum = p.supplyWeight + p.fatigueWeight + p.conditionWeight;
            if (wSum <= 0f) return 0f; // 重みゼロは合成不能＝即応性なし

            // 重みで正規化した相乗平均（pow(x, w/Σw) の積）。ゼロ要素は全体を 0 にする＝律速。
            float ws = p.supplyWeight / wSum;
            float wc = p.fatigueWeight / wSum;
            float wk = p.conditionWeight / wSum;

            float deployability = SafePow(s, ws) * SafePow(c, wc) * SafePow(k, wk);
            deployability = Mathf.Clamp01(deployability);

            // 危機床のハードフロア（極端な欠落は出撃不能）。
            if (deployability < p.criticalFloor) return 0f;
            return deployability;
        }

        /// <summary>内訳から総合即応性（既定パラメータ）。</summary>
        public static float Compose(float supplyFactor, float crewFactor, float conditionFactor)
            => Compose(supplyFactor, crewFactor, conditionFactor, FleetDeployabilityParams.Default);

        /// <summary>
        /// 補給充足・乗員疲労・船体整備状態から艦隊の即応性を一括評価する単一窓口。
        /// 各内訳は既存ルールへ委譲し、合成・出撃判定のみここで行う。
        /// </summary>
        public static FleetDeployabilityResult Evaluate(float ammo, float fuel, float provision,
                                                        float fatigue, float condition,
                                                        FleetDeployabilityParams p)
        {
            float s = SupplyFactor(ammo, fuel, provision);
            float c = CrewFactor(fatigue);
            float k = ConditionFactor(condition);
            float deployability = Compose(s, c, k, p);
            bool deployable = deployability >= p.deployableThreshold;
            return new FleetDeployabilityResult(s, c, k, deployability, deployable);
        }

        /// <summary>艦隊の即応性を一括評価（既定パラメータ）。</summary>
        public static FleetDeployabilityResult Evaluate(float ammo, float fuel, float provision,
                                                        float fatigue, float condition)
            => Evaluate(ammo, fuel, provision, fatigue, condition, FleetDeployabilityParams.Default);

        /// <summary>
        /// 即応性係数を会戦戦力へ反映した実効戦力（即応の整わぬ艦隊は数字ほど戦えない）。
        /// 戦力比較・自動解決（<see cref="AutoBattleSim"/>）の前段で raw 戦力を割り引く用途。
        /// </summary>
        public static float EffectiveStrength(float rawStrength, float deployability)
            => Mathf.Max(0f, rawStrength) * Mathf.Clamp01(deployability);

        /// <summary>出撃可能かの単問（即応性を閾値と比較）。</summary>
        public static bool IsDeployable(float deployability, FleetDeployabilityParams p)
            => Mathf.Clamp01(deployability) >= p.deployableThreshold;

        /// <summary>出撃可能かの単問（既定パラメータ）。</summary>
        public static bool IsDeployable(float deployability)
            => IsDeployable(deployability, FleetDeployabilityParams.Default);

        // NaN/負を避けた pow（底は 0..1 を想定）。重みゼロの要素は 1（無視）、ゼロ要素は 0（律速）。
        private static float SafePow(float baseValue, float exponent)
        {
            if (exponent <= 0f) return 1f;          // 重みゼロの要素は合成へ寄与しない
            if (baseValue <= 0f) return 0f;          // ゼロ要素は律速＝全体を 0 へ
            return Mathf.Pow(baseValue, exponent);
        }
    }
}
