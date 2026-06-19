using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊演習（NavalDrill）の調整値。演習の強度・実戦度・技能向上・連携・消耗・過訓練疲弊の各係数。
    /// すべて 0..1 系の倍率／寄与で、ctor で安全側に Clamp する（負値や暴走を持たない）。
    /// </summary>
    public readonly struct NavalDrillParams
    {
        /// <summary>演習規模が強度へ寄与する重み。</summary>
        public readonly float scaleWeight;
        /// <summary>演習頻度が強度へ寄与する重み。</summary>
        public readonly float frequencyWeight;
        /// <summary>想定の複雑さが実戦度へ寄与する重み。</summary>
        public readonly float complexityWeight;
        /// <summary>対抗部隊（敵役）の質が実戦度へ寄与する重み。</summary>
        public readonly float opposingWeight;
        /// <summary>技能向上の基準ゲイン（強度×実戦度に掛ける）。</summary>
        public readonly float skillGainRate;
        /// <summary>連携習熟の編成規模ペナルティ係数（大きいほど大編成で習熟が鈍る）。</summary>
        public readonly float coordinationCrowding;
        /// <summary>訓練強度に対する演習消耗（燃料・部品）の基準率。</summary>
        public readonly float attritionRate;
        /// <summary>実戦的演習で弱点が露呈する基準度合い。</summary>
        public readonly float exposureRate;
        /// <summary>過訓練疲弊の基準率（頻度×(1-休養)に掛ける）。</summary>
        public readonly float fatigueRate;

        public NavalDrillParams(
            float scaleWeight, float frequencyWeight,
            float complexityWeight, float opposingWeight,
            float skillGainRate, float coordinationCrowding,
            float attritionRate, float exposureRate, float fatigueRate)
        {
            this.scaleWeight = Mathf.Clamp01(scaleWeight);
            this.frequencyWeight = Mathf.Clamp01(frequencyWeight);
            this.complexityWeight = Mathf.Clamp01(complexityWeight);
            this.opposingWeight = Mathf.Clamp01(opposingWeight);
            this.skillGainRate = Mathf.Clamp01(skillGainRate);
            this.coordinationCrowding = Mathf.Clamp(coordinationCrowding, 0f, 10f);
            this.attritionRate = Mathf.Clamp01(attritionRate);
            this.exposureRate = Mathf.Clamp01(exposureRate);
            this.fatigueRate = Mathf.Clamp01(fatigueRate);
        }

        public const float DefaultScaleWeight = 0.5f;
        public const float DefaultFrequencyWeight = 0.5f;
        public const float DefaultComplexityWeight = 0.5f;
        public const float DefaultOpposingWeight = 0.5f;
        public const float DefaultSkillGainRate = 0.5f;
        public const float DefaultCoordinationCrowding = 0.25f;
        public const float DefaultAttritionRate = 0.4f;
        public const float DefaultExposureRate = 0.6f;
        public const float DefaultFatigueRate = 0.5f;

        /// <summary>既定：規模/頻度・複雑さ/対抗が各0.5、技能ゲイン0.5、混雑0.25、消耗0.4、露呈0.6、疲弊0.5。</summary>
        public static NavalDrillParams Default => new NavalDrillParams(
            DefaultScaleWeight, DefaultFrequencyWeight,
            DefaultComplexityWeight, DefaultOpposingWeight,
            DefaultSkillGainRate, DefaultCoordinationCrowding,
            DefaultAttritionRate, DefaultExposureRate, DefaultFatigueRate);
    }

    /// <summary>
    /// 艦隊演習・訓練による錬成の純ロジック（NavalDrill・test-first・唯一の窓口）。
    /// 演習の規模と質（<see cref="DrillIntensity"/>/<see cref="RealismFactor"/>）から
    /// 技能向上（<see cref="SkillGain"/>・高練度ほど伸び鈍化）と艦隊連携の習熟（<see cref="FleetCoordination"/>・大編成ほど難しい）を出し、
    /// 演習の代償＝消耗（<see cref="DrillAttrition"/>＝燃料・部品）・過訓練の疲弊（<see cref="OvertrainingFatigue"/>）と
    /// 弱点の露呈（<see cref="WeaknessExposure"/>＝改善の機会）を見て、最終的に演習の正味価値（<see cref="NetTrainingValue"/>・-1..1）を返す。
    /// <b>個体粒度へ降りない＝艦隊×集約</b>（艦1隻ずつのシミュはしない＝スケーラビリティ規律）。決定論・状態は変えない（read-only）。
    /// 中核トレードオフ＝<b>錬成 vs 消耗・疲弊</b>：強く頻繁に演習すれば練度は上がるが、燃料を食い艦と乗員が疲弊する。
    /// </summary>
    public static class NavalDrillRules
    {
        // ---- DrillIntensity（演習規模×頻度＝訓練の強度） ----

        /// <summary>演習規模(0..1)×頻度(0..1)を重み付き合成して訓練強度 0..1 を返す。</summary>
        public static float DrillIntensity(float drillScale, float drillFrequency, NavalDrillParams p)
        {
            float scale = Mathf.Clamp01(drillScale);
            float freq = Mathf.Clamp01(drillFrequency);
            float wsum = p.scaleWeight + p.frequencyWeight;
            if (wsum <= 0f) return 0f;
            float v = (p.scaleWeight * scale + p.frequencyWeight * freq) / wsum;
            return Mathf.Clamp01(v);
        }

        public static float DrillIntensity(float drillScale, float drillFrequency)
            => DrillIntensity(drillScale, drillFrequency, NavalDrillParams.Default);

        // ---- RealismFactor（想定の複雑さ×対抗部隊＝実戦的度合い） ----

        /// <summary>想定の複雑さ(0..1)×対抗部隊の質(0..1)を重み付き合成して実戦度 0..1 を返す。</summary>
        public static float RealismFactor(float scenarioComplexity, float opposingForce, NavalDrillParams p)
        {
            float comp = Mathf.Clamp01(scenarioComplexity);
            float opp = Mathf.Clamp01(opposingForce);
            float wsum = p.complexityWeight + p.opposingWeight;
            if (wsum <= 0f) return 0f;
            float v = (p.complexityWeight * comp + p.opposingWeight * opp) / wsum;
            return Mathf.Clamp01(v);
        }

        public static float RealismFactor(float scenarioComplexity, float opposingForce)
            => RealismFactor(scenarioComplexity, opposingForce, NavalDrillParams.Default);

        // ---- SkillGain（強度×実戦度×(1-現練度)＝技能向上・高練度ほど鈍化） ----

        /// <summary>
        /// 技能向上 0..1＝強度×実戦度×(1-現練度)×ゲイン率。
        /// 現練度が高いほど (1-現練度) で伸びが鈍る（収穫逓減）。
        /// </summary>
        public static float SkillGain(float drillIntensity, float realismFactor, float currentSkill, NavalDrillParams p)
        {
            float intensity = Mathf.Clamp01(drillIntensity);
            float realism = Mathf.Clamp01(realismFactor);
            float cur = Mathf.Clamp01(currentSkill);
            float gain = intensity * realism * (1f - cur) * p.skillGainRate;
            return Mathf.Clamp01(gain);
        }

        public static float SkillGain(float drillIntensity, float realismFactor, float currentSkill)
            => SkillGain(drillIntensity, realismFactor, currentSkill, NavalDrillParams.Default);

        // ---- FleetCoordination（強度/(1+部隊数)＝連携習熟・大編成ほど難しい） ----

        /// <summary>
        /// 艦隊連携の習熟 0..1＝強度/(1+混雑係数×部隊数)。
        /// 部隊数が増えるほど分母が大きくなり習熟が鈍る（大編成ほど連携が難しい）。
        /// </summary>
        public static float FleetCoordination(float drillIntensity, int unitCount, NavalDrillParams p)
        {
            float intensity = Mathf.Clamp01(drillIntensity);
            float units = Mathf.Max(0, unitCount);
            float denom = 1f + p.coordinationCrowding * units;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(intensity / denom);
        }

        public static float FleetCoordination(float drillIntensity, int unitCount)
            => FleetCoordination(drillIntensity, unitCount, NavalDrillParams.Default);

        // ---- DrillAttrition（訓練強度に応じた演習消耗＝燃料・部品） ----

        /// <summary>演習消耗 0..1（燃料・部品）＝訓練強度×消耗率。強く演習するほど資源を食う。</summary>
        public static float DrillAttrition(float drillIntensity, NavalDrillParams p)
        {
            float intensity = Mathf.Clamp01(drillIntensity);
            return Mathf.Clamp01(intensity * p.attritionRate);
        }

        public static float DrillAttrition(float drillIntensity)
            => DrillAttrition(drillIntensity, NavalDrillParams.Default);

        // ---- WeaknessExposure（実戦的演習ほど弱点が露呈＝改善の機会） ----

        /// <summary>弱点の露呈度 0..1＝実戦度×露呈率。実戦的な演習ほど欠点が顕在化する（＝改善の機会）。</summary>
        public static float WeaknessExposure(float realismFactor, NavalDrillParams p)
        {
            float realism = Mathf.Clamp01(realismFactor);
            return Mathf.Clamp01(realism * p.exposureRate);
        }

        public static float WeaknessExposure(float realismFactor)
            => WeaknessExposure(realismFactor, NavalDrillParams.Default);

        // ---- OvertrainingFatigue（頻度×(1-休養)＝過訓練の疲弊） ----

        /// <summary>過訓練の疲弊 0..1＝頻度×(1-休養)×疲弊率。休養が足りないまま頻繁に演習すると蓄積する。</summary>
        public static float OvertrainingFatigue(float drillFrequency, float restPeriods, NavalDrillParams p)
        {
            float freq = Mathf.Clamp01(drillFrequency);
            float rest = Mathf.Clamp01(restPeriods);
            float fatigue = freq * (1f - rest) * p.fatigueRate;
            return Mathf.Clamp01(fatigue);
        }

        public static float OvertrainingFatigue(float drillFrequency, float restPeriods)
            => OvertrainingFatigue(drillFrequency, restPeriods, NavalDrillParams.Default);

        // ---- NetTrainingValue（技能向上−消耗−疲弊＝演習の正味価値・-1..1） ----

        /// <summary>
        /// 演習の正味価値 -1..1＝技能向上−消耗−疲弊。
        /// 得るもの（練度）が代償（資源消耗・疲弊）を上回れば正、下回れば負（やりすぎ＝逆効果）。
        /// </summary>
        public static float NetTrainingValue(float skillGain, float drillAttrition, float overtrainingFatigue, NavalDrillParams p)
        {
            float gain = Mathf.Clamp01(skillGain);
            float attrition = Mathf.Clamp01(drillAttrition);
            float fatigue = Mathf.Clamp01(overtrainingFatigue);
            float net = gain - attrition - fatigue;
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float NetTrainingValue(float skillGain, float drillAttrition, float overtrainingFatigue)
            => NetTrainingValue(skillGain, drillAttrition, overtrainingFatigue, NavalDrillParams.Default);
    }
}
