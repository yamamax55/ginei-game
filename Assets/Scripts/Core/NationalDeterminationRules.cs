using UnityEngine;

namespace Ginei
{
    /// <summary>国家意志・後発国の底力（#1433）の調整係数。</summary>
    public readonly struct NationalDeterminationParams
    {
        /// <summary>存亡の切迫の重み（survivalStakes=1 で国家意志がこの幅まで立ち上がる）。</summary>
        public readonly float survivalWeight;
        /// <summary>国民の一体感の重み（nationalUnity=1 で国家意志がこの幅まで上乗せ）。</summary>
        public readonly float unityWeight;
        /// <summary>劣勢補正の最大幅（forceRatio→0 で国家意志がこの幅まで戦闘効率を底上げ＝必死の踏ん張り）。</summary>
        public readonly float underdogBonusScale;
        /// <summary>士気回復の加速基準（nationalWill=1 で回復速度にこの係数を上乗せ・per dt 換算）。</summary>
        public readonly float recoveryAccelScale;
        /// <summary>背水（backsToWall=1）の決意上乗せ幅（後がない時に底力が跳ねる）。</summary>
        public readonly float lastStandScale;
        /// <summary>国家意志の摩耗率（warDuration1あたり・per dt＝必死さは続かない）。</summary>
        public readonly float fatigueRate;
        /// <summary>意志で覆せる戦力差の限界比（forceRatio がこれを下回ると意志でも補正が効かなくなる＝精神論の限界）。</summary>
        public readonly float resolveFloorRatio;
        /// <summary>危機による潜在国力の動員幅（survivalStakes=1 で latentCapacity をこの割合まで引き出す＝火事場の馬鹿力）。</summary>
        public readonly float mobilizationScale;

        public NationalDeterminationParams(float survivalWeight, float unityWeight, float underdogBonusScale,
                                           float recoveryAccelScale, float lastStandScale, float fatigueRate,
                                           float resolveFloorRatio, float mobilizationScale)
        {
            this.survivalWeight = Mathf.Clamp01(survivalWeight);
            this.unityWeight = Mathf.Clamp01(unityWeight);
            this.underdogBonusScale = Mathf.Max(0f, underdogBonusScale);
            this.recoveryAccelScale = Mathf.Max(0f, recoveryAccelScale);
            this.lastStandScale = Mathf.Max(0f, lastStandScale);
            this.fatigueRate = Mathf.Max(0f, fatigueRate);
            this.resolveFloorRatio = Mathf.Clamp01(resolveFloorRatio);
            this.mobilizationScale = Mathf.Max(0f, mobilizationScale);
        }

        /// <summary>
        /// 既定＝存亡重み0.6・一体感重み0.4・劣勢補正幅0.5・回復加速0.5・背水上乗せ0.5・摩耗率0.3・
        /// 意志の限界比0.2・危機動員幅0.6。
        /// </summary>
        public static NationalDeterminationParams Default => new NationalDeterminationParams(
            0.6f, 0.4f, 0.5f, 0.5f, 0.5f, 0.3f, 0.2f, 0.6f);
    }

    /// <summary>
    /// 国家意志・後発国の底力（#1433・坂の上の雲型）の純ロジック。戦力で劣る後発国が、国家存亡を
    /// かけた決意（国家意志＝<see cref="NationalWill"/>）で実力以上の力を発揮する＝日露戦争の日本のように、
    /// 劣位の戦力比ほど背水の決意・国民の一体感が戦闘効率を補正し（<see cref="UnderdogCombatBonus"/>）、
    /// 敗勢でも士気が折れず回復が速い（<see cref="MoraleRecoveryAcceleration"/>）。後がない時に底力は
    /// 跳ね（<see cref="LastStandResolve"/>）、危機は潜在国力を引き出す（<see cref="MobilizationFromCrisis"/>＝
    /// 火事場の馬鹿力）。だが長期戦では意志も摩耗し（<see cref="DeterminationFatigue"/>＝必死さは続かない）、
    /// 限度を超えた戦力差は意志でも覆せない（<see cref="OverextensionVsResolve"/>＝精神論の限界）。
    /// 分担：<see cref="FleetMorale"/>（Game層）＝士気そのものの係数算出（本クラスは士気回復の加速係数を返すのみ・基準非破壊）／
    /// <see cref="RoyalPresenceRules"/>（#899・親征の士気＝君主の臨御）／CommitmentRules（背水の陣・別EPIC KORY）／
    /// <see cref="MobilizationRules"/>（戦時動員＝生産の切替）とは別＝本クラスは「背水の劣勢国の底力＝
    /// 国家意志による劣勢補正」。乱数なし・全入力クランプ・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NationalDeterminationRules
    {
        /// <summary>
        /// 国家意志（0..1）＝背水の決意。国家存亡の切迫（survivalStakes 0..1）×survivalWeight と
        /// 国民の一体感（nationalUnity 0..1）×unityWeight の和＝存亡を意識し国民が一体になるほど高い。
        /// 既定重み（0.6/0.4）の和は1.0＝両者最大で国家意志1.0。
        /// </summary>
        public static float NationalWill(float survivalStakes, float nationalUnity, NationalDeterminationParams p)
        {
            float stakes = Mathf.Clamp01(survivalStakes);
            float unity = Mathf.Clamp01(nationalUnity);
            return Mathf.Clamp01(p.survivalWeight * stakes + p.unityWeight * unity);
        }

        public static float NationalWill(float survivalStakes, float nationalUnity)
            => NationalWill(survivalStakes, nationalUnity, NationalDeterminationParams.Default);

        /// <summary>
        /// 劣勢の戦闘効率補正（≥1.0）。戦力比（forceRatio 0..1＝自軍÷敵軍。小さいほど劣勢）が劣るほど
        /// 国家意志（nationalWill 0..1）が戦闘効率を底上げする＝必死の側が踏ん張る。補正幅は
        /// (1−forceRatio)×nationalWill×underdogBonusScale で、優勢（forceRatio=1）や意志ゼロでは1.0
        /// （ボーナスなし＝底力は劣勢の決意でのみ湧く）。実効値（基準非破壊）。
        /// </summary>
        public static float UnderdogCombatBonus(float forceRatio, float nationalWill, NationalDeterminationParams p)
        {
            float ratio = Mathf.Clamp01(forceRatio);
            float will = Mathf.Clamp01(nationalWill);
            return 1f + (1f - ratio) * will * p.underdogBonusScale;
        }

        public static float UnderdogCombatBonus(float forceRatio, float nationalWill)
            => UnderdogCombatBonus(forceRatio, nationalWill, NationalDeterminationParams.Default);

        /// <summary>
        /// 士気回復の加速係数（≥0・per dt）。国家意志（nationalWill 0..1）が高いほど敗勢でも士気回復が速い
        /// ＝折れない。nationalWill×recoveryAccelScale×dt を FleetMorale の回復に上乗せする係数として返す
        /// （士気そのものは <see cref="FleetMorale"/> が持つ・本クラスは加算係数のみ＝基準非破壊）。
        /// </summary>
        public static float MoraleRecoveryAcceleration(float nationalWill, float dt, NationalDeterminationParams p)
        {
            float will = Mathf.Clamp01(nationalWill);
            return will * p.recoveryAccelScale * Mathf.Max(0f, dt);
        }

        public static float MoraleRecoveryAcceleration(float nationalWill, float dt)
            => MoraleRecoveryAcceleration(nationalWill, dt, NationalDeterminationParams.Default);

        /// <summary>
        /// 背水の決意（0..1）＝追い込まれた底力。国家意志（nationalWill 0..1）に、後がない度合い
        /// （backsToWall 0..1＝退路が断たれるほど1）×lastStandScale を上乗せ＝背水＝後がない時に最大の底力。
        /// 上限1.0でクランプ。
        /// </summary>
        public static float LastStandResolve(float nationalWill, float backsToWall, NationalDeterminationParams p)
        {
            float will = Mathf.Clamp01(nationalWill);
            float wall = Mathf.Clamp01(backsToWall);
            return Mathf.Clamp01(will + wall * p.lastStandScale);
        }

        public static float LastStandResolve(float nationalWill, float backsToWall)
            => LastStandResolve(nationalWill, backsToWall, NationalDeterminationParams.Default);

        /// <summary>
        /// 摩耗後の国家意志（0..nationalWill）。長期戦（warDuration 0..1＝戦争が長引くほど1）で
        /// 国家意志も摩耗する＝必死さは続かない。warDuration×fatigueRate×dt を意志から差し引く（下限0）。
        /// dt の無い1回適用は dt=1 相当。
        /// </summary>
        public static float DeterminationFatigue(float nationalWill, float warDuration, float dt, NationalDeterminationParams p)
        {
            float will = Mathf.Clamp01(nationalWill);
            float duration = Mathf.Clamp01(warDuration);
            float loss = duration * p.fatigueRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(will - loss);
        }

        public static float DeterminationFatigue(float nationalWill, float warDuration, float dt)
            => DeterminationFatigue(nationalWill, warDuration, dt, NationalDeterminationParams.Default);

        /// <summary>
        /// 意志を加味した実効戦力比（≥forceRatio）。劣勢（forceRatio 0..1）を国家意志（nationalWill 0..1）が
        /// <see cref="UnderdogCombatBonus"/> ぶん補うが、戦力比が resolveFloorRatio を下回るほどの致命的劣勢では
        /// 意志でも覆せない＝精神論の限界。下限を割ると補正を効かせず素の戦力比を返す（意志は無力）。
        /// </summary>
        public static float OverextensionVsResolve(float forceRatio, float nationalWill, NationalDeterminationParams p)
        {
            float ratio = Mathf.Clamp01(forceRatio);
            if (ratio < p.resolveFloorRatio) return ratio; // 限度を超えた戦力差は意志でも覆せない
            float will = Mathf.Clamp01(nationalWill);
            return ratio * UnderdogCombatBonus(ratio, will, p);
        }

        public static float OverextensionVsResolve(float forceRatio, float nationalWill)
            => OverextensionVsResolve(forceRatio, nationalWill, NationalDeterminationParams.Default);

        /// <summary>
        /// 危機による潜在国力の動員（0..latentCapacity）。国家存亡の危機（survivalStakes 0..1）が
        /// 眠っていた潜在国力（latentCapacity 0..1）を survivalStakes×mobilizationScale の割合まで引き出す
        /// ＝火事場の馬鹿力・総力結集。危機なし／潜在ゼロでは0。
        /// </summary>
        public static float MobilizationFromCrisis(float survivalStakes, float latentCapacity, NationalDeterminationParams p)
        {
            float stakes = Mathf.Clamp01(survivalStakes);
            float latent = Mathf.Clamp01(latentCapacity);
            return latent * stakes * p.mobilizationScale;
        }

        public static float MobilizationFromCrisis(float survivalStakes, float latentCapacity)
            => MobilizationFromCrisis(survivalStakes, latentCapacity, NationalDeterminationParams.Default);

        /// <summary>
        /// 闘志に燃えた状態か＝国家意志（nationalWill 0..1）が threshold 以上＝戦力差を意志で補おうとする
        /// 燃え立った国家の判定。<see cref="IsFightingSpirit(float)"/> は既定閾値0.5。
        /// </summary>
        public static bool IsFightingSpirit(float nationalWill, float threshold)
        {
            return Mathf.Clamp01(nationalWill) >= Mathf.Clamp01(threshold);
        }

        public static bool IsFightingSpirit(float nationalWill) => IsFightingSpirit(nationalWill, 0.5f);
    }
}
