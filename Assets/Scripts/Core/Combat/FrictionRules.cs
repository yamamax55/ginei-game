using UnityEngine;

namespace Ginei
{
    /// <summary>作戦摩擦の調整係数。</summary>
    public readonly struct FrictionParams
    {
        /// <summary>命令深度が摩擦に寄与する重み。</summary>
        public readonly float depthWeight;
        /// <summary>補給不足が摩擦に寄与する重み。</summary>
        public readonly float supplyWeight;
        /// <summary>低士気が摩擦に寄与する重み。</summary>
        public readonly float moraleWeight;
        /// <summary>命令深度1あたり遅延が積み重なる速さ（per dt）。</summary>
        public readonly float delayPerDepth;
        /// <summary>摩擦が士気を削る最大速度（per dt）。</summary>
        public readonly float moraleErosionRate;
        /// <summary>作戦停滞とみなす実行成功率の既定閾値。</summary>
        public readonly float boggedThreshold;

        public FrictionParams(float depthWeight, float supplyWeight, float moraleWeight,
            float delayPerDepth, float moraleErosionRate, float boggedThreshold)
        {
            this.depthWeight = Mathf.Max(0f, depthWeight);
            this.supplyWeight = Mathf.Max(0f, supplyWeight);
            this.moraleWeight = Mathf.Max(0f, moraleWeight);
            this.delayPerDepth = Mathf.Max(0f, delayPerDepth);
            this.moraleErosionRate = Mathf.Max(0f, moraleErosionRate);
            this.boggedThreshold = Mathf.Clamp01(boggedThreshold);
        }

        /// <summary>既定＝深度重み0.4・補給重み0.35・士気重み0.25・遅延0.2/深度・士気侵食0.3・停滞閾値0.4。</summary>
        public static FrictionParams Default => new FrictionParams(0.4f, 0.35f, 0.25f, 0.2f, 0.3f, 0.4f);
    }

    /// <summary>
    /// 作戦摩擦の純ロジック（クラウゼヴィッツ『戦争論』の「摩擦（Friktion）」・CLZ-1 #1133）。
    /// 紙の上では単純なことも、実戦では無数の小さな障害（誤解・遅延・疲労・天候）が積み重なって
    /// 困難になる＝「戦争では全てが単純だが、単純なことが難しい」。命令深度（指揮系統の長さ）×
    /// 補給状態×士気が、計画が実行で目減りする度合いを左右し、経験と計画の単純さが摩擦を減らす。
    /// 入力となる計画の質は <see cref="OperationPlanRules"/>（作戦計画の質）、指揮の遅延そのものは
    /// <see cref="CommunicationsRules"/>（命令の到達遅延）が出し、ここは「摩擦＝計画が実行で削られる
    /// 度合い」の写像のみを担う。規模が摩擦を増す効果は別テーマ（MassEngagementRules・別Issue想定）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FrictionRules
    {
        /// <summary>
        /// 摩擦の大きさ（0..1）＝命令深度・補給不足・低士気の重み付き合成（障害の積み重なり）。
        /// 全入力0..1。指揮系統が長く・補給が枯れ・士気が低いほど摩擦が増す。
        /// </summary>
        public static float FrictionLevel(float commandDepth, float supplyShortage, float lowMorale, FrictionParams p)
        {
            float depth = Mathf.Clamp01(commandDepth) * p.depthWeight;
            float supply = Mathf.Clamp01(supplyShortage) * p.supplyWeight;
            float morale = Mathf.Clamp01(lowMorale) * p.moraleWeight;
            float total = p.depthWeight + p.supplyWeight + p.moraleWeight;
            if (total <= 0f) return 0f;
            return Mathf.Clamp01((depth + supply + morale) / total);
        }

        public static float FrictionLevel(float commandDepth, float supplyShortage, float lowMorale)
            => FrictionLevel(commandDepth, supplyShortage, lowMorale, FrictionParams.Default);

        /// <summary>
        /// 実行成功確率（0..1）＝計画の質が摩擦で目減りした値＝planQuality×(1−frictionLevel)。
        /// 紙の上の計画が現実で削られる＝摩擦ゼロなら計画どおり、摩擦最大なら成功しない。
        /// </summary>
        public static float ExecutionSuccess(float planQuality, float frictionLevel, FrictionParams p)
        {
            return Mathf.Clamp01(planQuality) * (1f - Mathf.Clamp01(frictionLevel));
        }

        public static float ExecutionSuccess(float planQuality, float frictionLevel)
            => ExecutionSuccess(planQuality, frictionLevel, FrictionParams.Default);

        /// <summary>
        /// 計画の陳腐化度（0..1）＝摩擦が計画をどれだけ崩すか＝摩擦に比例（接敵後の計画の崩れ）。
        /// 摩擦が大きいほど計画は速く陳腐化する。
        /// </summary>
        public static float PlanDegradation(float frictionLevel, FrictionParams p)
        {
            return Mathf.Clamp01(frictionLevel);
        }

        public static float PlanDegradation(float frictionLevel)
            => PlanDegradation(frictionLevel, FrictionParams.Default);

        /// <summary>
        /// 累積する遅延＝命令深度×delayPerDepth×経過時間（指揮系統が長いほど遅延が積み重なる）。
        /// 命令が届くまでに状況が変わる＝深い指揮系統ほど時間とともに遅れが膨らむ。
        /// </summary>
        public static float CompoundingDelays(float commandDepth, float dt, FrictionParams p)
        {
            return Mathf.Clamp01(commandDepth) * p.delayPerDepth * Mathf.Max(0f, dt);
        }

        public static float CompoundingDelays(float commandDepth, float dt)
            => CompoundingDelays(commandDepth, dt, FrictionParams.Default);

        /// <summary>
        /// 戦場の霧ペナルティ（0..1）＝摩擦と不確実性が判断を誤らせる＝frictionLevel×uncertainty。
        /// 摩擦も不確実性も無ければ霧は晴れる（どちらか一方でも欠ければ判断は曇りにくい）。
        /// </summary>
        public static float FogOfWarPenalty(float frictionLevel, float uncertainty, FrictionParams p)
        {
            return Mathf.Clamp01(frictionLevel) * Mathf.Clamp01(uncertainty);
        }

        public static float FogOfWarPenalty(float frictionLevel, float uncertainty)
            => FogOfWarPenalty(frictionLevel, uncertainty, FrictionParams.Default);

        /// <summary>
        /// 摩擦の緩和倍率（0..1）＝経験と計画の単純さが摩擦を減らす＝(1−experience×simplicity)。
        /// 歴戦の軍と単純な計画は摩擦に強い＝両方揃って初めて摩擦を大きく削れる。摩擦に掛けて使う。
        /// </summary>
        public static float FrictionMitigation(float experience, float simplicity, FrictionParams p)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(experience) * Mathf.Clamp01(simplicity));
        }

        public static float FrictionMitigation(float experience, float simplicity)
            => FrictionMitigation(experience, simplicity, FrictionParams.Default);

        /// <summary>
        /// 摩擦下の士気（0..1）＝摩擦が士気を削る（思い通りにいかない苛立ち）。摩擦×侵食率×dt 分だけ低下。
        /// </summary>
        public static float MoraleUnderFriction(float morale, float frictionLevel, float dt, FrictionParams p)
        {
            float erosion = Mathf.Clamp01(frictionLevel) * p.moraleErosionRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(morale) - erosion);
        }

        public static float MoraleUnderFriction(float morale, float frictionLevel, float dt)
            => MoraleUnderFriction(morale, frictionLevel, dt, FrictionParams.Default);

        /// <summary>
        /// 作戦が摩擦で停滞したか＝実行成功率が閾値を下回る（紙の上の単純な作戦が現実で動かない）。
        /// </summary>
        public static bool IsOperationBoggedDown(float executionSuccess, float threshold)
        {
            return Mathf.Clamp01(executionSuccess) < Mathf.Clamp01(threshold);
        }

        public static bool IsOperationBoggedDown(float executionSuccess)
            => IsOperationBoggedDown(executionSuccess, FrictionParams.Default.boggedThreshold);
    }
}
