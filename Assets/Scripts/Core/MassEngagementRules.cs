using UnityEngine;

namespace Ginei
{
    /// <summary>大規模会戦の規模限界（規模摩擦）の調整係数。</summary>
    public readonly struct MassEngagementParams
    {
        /// <summary>総兵員数1あたりの規模摩擦の伸び（数が増えるほど摩擦が乗算的に膨らむ速さ）。</summary>
        public readonly float frictionPerTroop;
        /// <summary>指揮統制の範囲（commandSpan）が規模摩擦をさらに増幅する重み（広い指揮幅ほど摩擦増）。</summary>
        public readonly float spanAmplification;
        /// <summary>規模摩擦の乗数の上限（これ以上は膨らまない＝統御不能の天井）。</summary>
        public readonly float maxFrictionMultiplier;
        /// <summary>指揮統制の負担の非線形度（兵員数の何乗で負担が増すか＝1超で大軍ほど急増）。</summary>
        public readonly float strainExponent;
        /// <summary>連携破綻の非線形度（兵員数の何乗で連携が崩れるか＝1超で大軍ほど急増）。</summary>
        public readonly float coordinationExponent;
        /// <summary>補給負担の非線形度（兵員数の何乗で補給が重くなるか＝1超で大軍ほど急増）。</summary>
        public readonly float logisticsExponent;
        /// <summary>統御不能な大軍とみなす規模摩擦乗数の既定閾値（これ超で持て余す）。</summary>
        public readonly float unwieldyThreshold;

        public MassEngagementParams(float frictionPerTroop, float spanAmplification, float maxFrictionMultiplier,
            float strainExponent, float coordinationExponent, float logisticsExponent, float unwieldyThreshold)
        {
            this.frictionPerTroop = Mathf.Max(0f, frictionPerTroop);
            this.spanAmplification = Mathf.Max(0f, spanAmplification);
            this.maxFrictionMultiplier = Mathf.Max(1f, maxFrictionMultiplier);
            this.strainExponent = Mathf.Max(0f, strainExponent);
            this.coordinationExponent = Mathf.Max(0f, coordinationExponent);
            this.logisticsExponent = Mathf.Max(0f, logisticsExponent);
            this.unwieldyThreshold = Mathf.Max(1f, unwieldyThreshold);
        }

        /// <summary>既定＝摩擦伸び1.0/兵員・指揮幅増幅0.5・摩擦乗数上限3.0・統制負担指数1.5・連携破綻指数1.5・補給負担指数1.3・統御不能閾値2.0。</summary>
        public static MassEngagementParams Default => new MassEngagementParams(1f, 0.5f, 3f, 1.5f, 1.5f, 1.3f, 2f);
    }

    /// <summary>
    /// 大規模会戦の規模限界＝革命戦争型（国民軍の大量動員）の純ロジック（WAP-3 #1417）。
    /// 総兵員数が膨大になるほどクラウゼヴィッツの「摩擦」が乗算的に増大する＝大軍は指揮統制・補給・
    /// 連携の摩擦が規模とともに非線形に膨らみ、数が多ければ強いとは限らず規模には統御の限界がある。
    /// 中核 <see cref="ScaleFrictionMultiplier"/> は ≥1.0 の係数で、<see cref="FrictionRules"/>（作戦摩擦＝
    /// 命令深度×補給×士気・生成済み）の摩擦に掛ける「規模による乗算拡張」を担う＝本クラスは規模そのものが
    /// 摩擦を増す側、FrictionRules は計画が摩擦で削られる側。分担：<see cref="CommunicationsRules"/>（命令の
    /// 到達遅延）／<see cref="FleetCapRules"/>（提督1人あたりの指揮容量）／<see cref="LogisticsRules"/>（版図の
    /// 物理連結）とは別＝こちらは「総兵員規模が摩擦を非線形に膨らませる規模の限界」の写像のみ。
    /// 全入力クランプ・乱数なし・決定論・基準値非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MassEngagementRules
    {
        /// <summary>
        /// 規模摩擦の乗数（≥1.0）＝総兵員数が大きく指揮統制の範囲が広いほど摩擦が乗算的に増す。
        /// =1＋troopCount×(frictionPerTroop＋commandSpan×spanAmplification)、上限 maxFrictionMultiplier。
        /// <see cref="FrictionRules.FrictionLevel"/> 等の摩擦値に掛けて使う規模拡張係数（大軍ほど膨らむ）。
        /// </summary>
        public static float ScaleFrictionMultiplier(float troopCount, float commandSpan, MassEngagementParams p)
        {
            float troops = Mathf.Clamp01(troopCount);
            float span = Mathf.Clamp01(commandSpan);
            float growth = troops * (p.frictionPerTroop + span * p.spanAmplification);
            return Mathf.Clamp(1f + growth, 1f, p.maxFrictionMultiplier);
        }

        /// <summary>規模摩擦の乗数（既定Params版）。</summary>
        public static float ScaleFrictionMultiplier(float troopCount, float commandSpan)
            => ScaleFrictionMultiplier(troopCount, commandSpan, MassEngagementParams.Default);

        /// <summary>
        /// 指揮統制の負担（0..1）＝兵員が多いほど指揮統制が困難になる（命令が末端まで届かない）。
        /// =troopCount^strainExponent×(1−commandStructure)＝兵員数で非線形に増し、堅い指揮機構が緩和する。
        /// </summary>
        public static float CommandControlStrain(float troopCount, float commandStructure, MassEngagementParams p)
        {
            float troops = Mathf.Clamp01(troopCount);
            float structure = Mathf.Clamp01(commandStructure);
            float scaled = Mathf.Pow(troops, p.strainExponent);
            return Mathf.Clamp01(scaled * (1f - structure));
        }

        /// <summary>指揮統制の負担（既定Params版）。</summary>
        public static float CommandControlStrain(float troopCount, float commandStructure)
            => CommandControlStrain(troopCount, commandStructure, MassEngagementParams.Default);

        /// <summary>
        /// 連携の破綻度（0..1）＝大軍は部隊間の連携が破綻しやすい（左翼と右翼が連動しない）。
        /// =troopCount^coordinationExponent×(1−communicationQuality)＝規模で非線形、良い通信が連携を保つ。
        /// </summary>
        public static float CoordinationBreakdown(float troopCount, float communicationQuality, MassEngagementParams p)
        {
            float troops = Mathf.Clamp01(troopCount);
            float comms = Mathf.Clamp01(communicationQuality);
            float scaled = Mathf.Pow(troops, p.coordinationExponent);
            return Mathf.Clamp01(scaled * (1f - comms));
        }

        /// <summary>連携の破綻度（既定Params版）。</summary>
        public static float CoordinationBreakdown(float troopCount, float communicationQuality)
            => CoordinationBreakdown(troopCount, communicationQuality, MassEngagementParams.Default);

        /// <summary>
        /// 補給のスケール負担（0..1）＝大軍ほど補給の負担が重い（食わせるだけで国が傾く）。
        /// =troopCount^logisticsExponent×(1−supplyCapacity)＝規模で非線形、補給能力が高いほど和らぐ。
        /// </summary>
        public static float LogisticalScaleStrain(float troopCount, float supplyCapacity, MassEngagementParams p)
        {
            float troops = Mathf.Clamp01(troopCount);
            float capacity = Mathf.Clamp01(supplyCapacity);
            float scaled = Mathf.Pow(troops, p.logisticsExponent);
            return Mathf.Clamp01(scaled * (1f - capacity));
        }

        /// <summary>補給のスケール負担（既定Params版）。</summary>
        public static float LogisticalScaleStrain(float troopCount, float supplyCapacity)
            => LogisticalScaleStrain(troopCount, supplyCapacity, MassEngagementParams.Default);

        /// <summary>
        /// 実効戦闘力（0..1）＝生の戦力が規模の摩擦で目減りした値＝数がそのまま力にならない。
        /// =rawStrength÷scaleFrictionMultiplier（乗数≥1で割る＝大軍ほど実効戦力が削られる）。
        /// </summary>
        public static float EffectiveCombatPower(float rawStrength, float scaleFrictionMultiplier)
        {
            float raw = Mathf.Clamp01(rawStrength);
            float mul = Mathf.Max(1f, scaleFrictionMultiplier);
            return Mathf.Clamp01(raw / mul);
        }

        /// <summary>
        /// 統御できる最適な兵力規模（0..1）＝これを超えると規模の摩擦で逆に弱くなる。
        /// =commandCapacity×terrainConstraint＝指揮容量が大きく地形が許すほど大軍を活かせる。
        /// </summary>
        public static float OptimalForceSize(float commandCapacity, float terrainConstraint, MassEngagementParams p)
        {
            float cap = Mathf.Clamp01(commandCapacity);
            float terrain = Mathf.Clamp01(terrainConstraint);
            return Mathf.Clamp01(cap * terrain);
        }

        /// <summary>統御できる最適な兵力規模（既定Params版）。</summary>
        public static float OptimalForceSize(float commandCapacity, float terrainConstraint)
            => OptimalForceSize(commandCapacity, terrainConstraint, MassEngagementParams.Default);

        /// <summary>
        /// 混乱リスク（0..1）＝大軍が戦場の霧の中で同士討ち・混乱する危険＝troopCount×fogOfWar。
        /// 兵員も霧も無ければ混乱しない（どちらか欠ければ混乱は起きにくい）。
        /// </summary>
        public static float MassConfusionRisk(float troopCount, float fogOfWar, MassEngagementParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(troopCount) * Mathf.Clamp01(fogOfWar));
        }

        /// <summary>混乱リスク（既定Params版）。</summary>
        public static float MassConfusionRisk(float troopCount, float fogOfWar)
            => MassConfusionRisk(troopCount, fogOfWar, MassEngagementParams.Default);

        /// <summary>
        /// 統御不能なほど膨れ上がった大軍か＝規模摩擦乗数が閾値を超える（数だけ多くて持て余す）。
        /// </summary>
        public static bool IsUnwieldyHost(float scaleFrictionMultiplier, float threshold)
        {
            return Mathf.Max(1f, scaleFrictionMultiplier) > Mathf.Max(1f, threshold);
        }

        /// <summary>統御不能な大軍か（既定閾値版）。</summary>
        public static bool IsUnwieldyHost(float scaleFrictionMultiplier)
            => IsUnwieldyHost(scaleFrictionMultiplier, MassEngagementParams.Default.unwieldyThreshold);
    }
}
