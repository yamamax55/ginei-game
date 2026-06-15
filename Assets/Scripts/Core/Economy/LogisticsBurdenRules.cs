using UnityEngine;

namespace Ginei
{
    /// <summary>超線形兵站消費（尾高比＝tail-to-tooth スケーリング）の調整係数。</summary>
    public readonly struct LogisticsBurdenParams
    {
        /// <summary>艦隊規模に対する兵站負担の冪指数（1超で超線形＝大軍ほど爆発的に増える・既定1.3）。</summary>
        public readonly float sizeExponent;
        /// <summary>距離に対する兵站負担の冪指数（1未満なら逓増が緩い＝距離は規模より穏やか・既定0.7）。</summary>
        public readonly float distanceExponent;
        /// <summary>兵站負担の上限（これ以上は飽和＝供給能力の物理的天井）。</summary>
        public readonly float maxBurden;
        /// <summary>戦闘部隊（tooth）に対する後方兵站（tail）の基準比（戦闘1あたり何倍の兵站が要るか・既定3）。</summary>
        public readonly float baseTailRatio;
        /// <summary>距離による補給効率低下の急峻さ（距離の冪・1以上＝遠いほど加速して途中で消費される）。</summary>
        public readonly float supplyDecayExponent;
        /// <summary>補給効率の下限（0..1・どれだけ遠くても最低限は届く＝現地調達など）。</summary>
        public readonly float minSupplyEfficiency;
        /// <summary>過伸張＝兵站破綻とみなす規模×距離の既定閾値（これ超で補給が破綻する）。</summary>
        public readonly float overstretchThreshold;
        /// <summary>持続不能とみなす兵站負担の既定閾値（供給能力比でこれ超なら持続不能）。</summary>
        public readonly float unsustainableThreshold;

        public LogisticsBurdenParams(float sizeExponent, float distanceExponent, float maxBurden,
            float baseTailRatio, float supplyDecayExponent, float minSupplyEfficiency,
            float overstretchThreshold, float unsustainableThreshold)
        {
            this.sizeExponent = Mathf.Max(0f, sizeExponent);
            this.distanceExponent = Mathf.Max(0f, distanceExponent);
            this.maxBurden = Mathf.Max(0.0001f, maxBurden);
            this.baseTailRatio = Mathf.Max(0f, baseTailRatio);
            this.supplyDecayExponent = Mathf.Max(1f, supplyDecayExponent);
            this.minSupplyEfficiency = Mathf.Clamp01(minSupplyEfficiency);
            this.overstretchThreshold = Mathf.Max(0.0001f, overstretchThreshold);
            this.unsustainableThreshold = Mathf.Max(0.0001f, unsustainableThreshold);
        }

        /// <summary>既定＝規模冪1.3・距離冪0.7・負担上限4.0・基準尾高比3.0・補給減衰冪1.5・補給効率下限0.1・過伸張閾値0.5・持続不能閾値1.0。</summary>
        public static LogisticsBurdenParams Default =>
            new LogisticsBurdenParams(1.3f, 0.7f, 4f, 3f, 1.5f, 0.1f, 0.5f, 1f);
    }

    /// <summary>
    /// 超線形兵站消費＝尾高比（tail-to-tooth）スケーリングの純ロジック（CRV-2 #1365・兵站）。
    /// 兵站の消費は艦隊規模と距離に対して超線形（非線形）にスケールする＝規模^1.3×距離^0.7の尾高比的
    /// スケーリングで、大軍を遠くで戦わせるほど補給負担は単純な比例を超えて爆発的に増える＝戦闘部隊（tooth）
    /// 1に対し後方の兵站（tail）が何倍も要る。中核 <see cref="SupplyBurden"/> は fleetSize^sizeExponent ×
    /// distance^distanceExponent の超線形写像。分担：<see cref="MassEngagementRules"/>（総兵員規模が摩擦を
    /// 非線形に膨らませる規模の限界・生成済み）とは別＝こちらは兵站「消費」の超線形スケーリング。
    /// <see cref="OverextensionRules"/>（版図と国力の比＝国家規模の恒常的な過伸張）とも別。
    /// <see cref="CulminatingPointRules"/>（攻勢終末点＝1作戦の距離による戦力減衰）とは過伸張で整合させるが
    /// こちらは兵站消費量そのものの写像。<see cref="SupplyRules"/>（補給線が回廊で繋がるか＝面の到達）とも別。
    /// 全入力クランプ・乱数なし・決定論・基準値非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class LogisticsBurdenRules
    {
        /// <summary>
        /// 兵站負担（0..maxBurden）＝艦隊規模^sizeExponent × 距離^distanceExponent の超線形スケーリング。
        /// 規模も距離も増すと爆発的に負担が増える（規模冪1.3で大軍ほど急増・距離冪0.7で遠征が乗る）。
        /// 規模0または距離0なら0（戦わせなければ／遠征しなければ負担なし）。超線形の核。
        /// </summary>
        public static float SupplyBurden(float fleetSize, float distance, LogisticsBurdenParams p)
        {
            float size = Mathf.Clamp01(fleetSize);
            float dist = Mathf.Clamp01(distance);
            if (size <= 0f || dist <= 0f) return 0f;
            float burden = Mathf.Pow(size, p.sizeExponent) * Mathf.Pow(dist, p.distanceExponent);
            return Mathf.Min(p.maxBurden, burden);
        }

        /// <summary>兵站負担（既定Params版）。</summary>
        public static float SupplyBurden(float fleetSize, float distance)
            => SupplyBurden(fleetSize, distance, LogisticsBurdenParams.Default);

        /// <summary>
        /// 尾高比（tail-to-tooth ratio・0以上）＝戦闘部隊（tooth）に対する後方兵站（tail）の比。
        /// =baseTailRatio×supplyBurden÷combatForce＝兵站負担が重く戦闘部隊が少ないほど tail が膨らむ＝
        /// 大軍遠征ほど「実際に戦う1に対し後方で支える兵站が何倍も要る」。戦闘部隊0なら無限（支えるだけの軍）。
        /// </summary>
        public static float TailToToothRatio(float supplyBurden, float combatForce, LogisticsBurdenParams p)
        {
            float burden = Mathf.Max(0f, supplyBurden);
            float tooth = Mathf.Clamp01(combatForce);
            if (tooth <= 0f) return float.PositiveInfinity;
            return p.baseTailRatio * burden / tooth;
        }

        /// <summary>尾高比（既定Params版）。</summary>
        public static float TailToToothRatio(float supplyBurden, float combatForce)
            => TailToToothRatio(supplyBurden, combatForce, LogisticsBurdenParams.Default);

        /// <summary>
        /// 超線形ペナルティ（0以上）＝超線形の兵站負担が線形予測を超えた分＝単純比例で見積もると足りない量。
        /// 線形予測＝fleetSize×distance（比例の想定）に対し、実際の超線形負担との差を返す＝超過分。
        /// 規模冪1.3・距離冪0.7が掛かるため、規模が大きいほど線形予測を上回る（過小見積もりの罠）。
        /// </summary>
        public static float SuperlinearPenalty(float fleetSize, float distance, LogisticsBurdenParams p)
        {
            float size = Mathf.Clamp01(fleetSize);
            float dist = Mathf.Clamp01(distance);
            float actual = SupplyBurden(size, dist, p);
            float linear = size * dist; // 単純比例で見積もった兵站（過小評価）
            return Mathf.Max(0f, actual - linear);
        }

        /// <summary>超線形ペナルティ（既定Params版）。</summary>
        public static float SuperlinearPenalty(float fleetSize, float distance)
            => SuperlinearPenalty(fleetSize, distance, LogisticsBurdenParams.Default);

        /// <summary>
        /// ある距離で持続可能な最大艦隊規模（0..1）＝SupplyBurden(size,distance)=supplyCapacity となる size を逆算。
        /// 距離が遠いほど小さくなる（同じ補給能力でも遠征では大軍を養えない＝補給の制約）。
        /// 距離0なら制約なし（1.0）、供給能力0なら0（何も養えない）。
        /// </summary>
        public static float SustainableForceAtDistance(float distance, float supplyCapacity, LogisticsBurdenParams p)
        {
            float dist = Mathf.Clamp01(distance);
            float cap = Mathf.Clamp01(supplyCapacity);
            if (cap <= 0f) return 0f;
            if (dist <= 0f) return 1f;
            // burden = size^se × dist^de = cap → size = (cap / dist^de)^(1/se)
            float distTerm = Mathf.Pow(dist, p.distanceExponent);
            if (distTerm <= 0f) return 1f;
            float sizePow = cap / distTerm;
            float size = Mathf.Pow(Mathf.Max(0f, sizePow), 1f / p.sizeExponent);
            return Mathf.Clamp01(size);
        }

        /// <summary>持続可能な最大艦隊規模（既定Params版）。</summary>
        public static float SustainableForceAtDistance(float distance, float supplyCapacity)
            => SustainableForceAtDistance(distance, supplyCapacity, LogisticsBurdenParams.Default);

        /// <summary>
        /// 集中と分散のトレードオフ（0..1・大きいほど集中が不利）＝戦力を集中すると兵站が重く、
        /// 分散すると守りにくい。集中時の超線形兵站負担（totalForce 一塊）から、デポ支援で兵站を緩和する。
        /// =集中兵站負担×(1−depotSupport)＝デポが充実するほど集中の兵站負担が和らぐ（前進補給基地）。
        /// </summary>
        public static float ConcentrationVsDispersion(float totalForce, float distance, float depotSupport,
            LogisticsBurdenParams p)
        {
            float depot = Mathf.Clamp01(depotSupport);
            float concentratedBurden = SupplyBurden(totalForce, distance, p);
            float relieved = concentratedBurden * (1f - depot); // デポ支援で兵站を緩和
            return Mathf.Clamp01(relieved);
        }

        /// <summary>集中と分散のトレードオフ（既定Params版）。</summary>
        public static float ConcentrationVsDispersion(float totalForce, float distance, float depotSupport)
            => ConcentrationVsDispersion(totalForce, distance, depotSupport, LogisticsBurdenParams.Default);

        /// <summary>
        /// 補給の距離減衰（minSupplyEfficiency..1）＝距離が遠いほど補給効率が落ちる（運ぶ途中で消費される）。
        /// =1/(1+distance^supplyDecayExponent×(1−supplyEfficiency))＝補給能力が高いほど遠くまで効率を保つ。
        /// 距離0なら1.0（基地直上は満杯）、遠くても下限までしか落ちない。
        /// </summary>
        public static float DistanceDecayOfSupply(float distance, float supplyEfficiency, LogisticsBurdenParams p)
        {
            float dist = Mathf.Clamp01(distance);
            float eff = Mathf.Clamp01(supplyEfficiency);
            if (dist <= 0f) return 1f;
            float falloff = Mathf.Pow(dist, p.supplyDecayExponent) * (1f - eff);
            float decayed = 1f / (1f + falloff);
            return Mathf.Max(p.minSupplyEfficiency, decayed);
        }

        /// <summary>補給の距離減衰（既定Params版）。</summary>
        public static float DistanceDecayOfSupply(float distance, float supplyEfficiency)
            => DistanceDecayOfSupply(distance, supplyEfficiency, LogisticsBurdenParams.Default);

        /// <summary>
        /// 過伸張の閾値超過か＝規模×距離（超線形の兵站負担）が閾値を超えると兵站が破綻する。
        /// SupplyBurden が threshold を超えたら true＝大軍を遠くへ伸ばしすぎた（CulminatingPointRules と整合）。
        /// </summary>
        public static bool OverstretchThreshold(float fleetSize, float distance, float threshold,
            LogisticsBurdenParams p)
        {
            float burden = SupplyBurden(fleetSize, distance, p);
            return burden > Mathf.Max(0f, threshold);
        }

        /// <summary>過伸張の閾値超過か（既定閾値・既定Params版）。</summary>
        public static bool OverstretchThreshold(float fleetSize, float distance)
            => OverstretchThreshold(fleetSize, distance,
                LogisticsBurdenParams.Default.overstretchThreshold, LogisticsBurdenParams.Default);

        /// <summary>
        /// 兵站が持続不能か＝兵站負担が供給能力に対して閾値を超えて持続できない＝補給が追いつかない。
        /// supplyBurden÷supplyCapacity が threshold を超えたら true。供給能力0なら（負担があれば）必ず持続不能。
        /// </summary>
        public static bool IsLogisticallyUnsustainable(float supplyBurden, float supplyCapacity, float threshold)
        {
            float burden = Mathf.Max(0f, supplyBurden);
            float cap = Mathf.Clamp01(supplyCapacity);
            float thr = Mathf.Max(0.0001f, threshold);
            if (cap <= 0f) return burden > 0f; // 養う能力が無ければ負担があれば持続不能
            return burden / cap > thr;
        }

        /// <summary>兵站が持続不能か（既定閾値版）。</summary>
        public static bool IsLogisticallyUnsustainable(float supplyBurden, float supplyCapacity)
            => IsLogisticallyUnsustainable(supplyBurden, supplyCapacity,
                LogisticsBurdenParams.Default.unsustainableThreshold);
    }
}
