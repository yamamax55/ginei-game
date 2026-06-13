using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>大規模会戦の規模限界（MassEngagementRules・WAP-3 #1417）の純ロジック検証。</summary>
    public class MassEngagementRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>規模摩擦乗数＝兵員と指揮幅が大きいほど乗算的に増し≥1.0、上限でクランプ（大軍ほど膨らむ）。</summary>
        [Test]
        public void ScaleFrictionMultiplier_GrowsWithTroopsAndSpan()
        {
            // 兵員0なら摩擦増無し＝1.0
            Assert.AreEqual(1f, MassEngagementRules.ScaleFrictionMultiplier(0f, 1f), Eps);
            // 兵員0.5・指揮幅0.4 → 1 + 0.5×(1 + 0.4×0.5) = 1 + 0.5×1.2 = 1.6
            Assert.AreEqual(1.6f, MassEngagementRules.ScaleFrictionMultiplier(0.5f, 0.4f), Eps);
            // 兵員多×指揮幅広で上限3.0にクランプ：1 + 1×(1 + 1×0.5) = 2.5（上限内）
            Assert.AreEqual(2.5f, MassEngagementRules.ScaleFrictionMultiplier(1f, 1f), Eps);
            // 指揮幅が広いほど大きい（単調増）
            Assert.Greater(MassEngagementRules.ScaleFrictionMultiplier(0.7f, 0.9f),
                MassEngagementRules.ScaleFrictionMultiplier(0.7f, 0.1f));
        }

        /// <summary>指揮統制の負担＝兵員数^1.5×(1−機構)＝大軍ほど非線形に増し、堅い機構が緩和する。</summary>
        [Test]
        public void CommandControlStrain_NonlinearInTroops()
        {
            // 兵員1.0・機構0 → 1^1.5×1 = 1.0
            Assert.AreEqual(1f, MassEngagementRules.CommandControlStrain(1f, 0f), Eps);
            // 兵員0.5・機構0 → 0.5^1.5 = 0.353553…
            Assert.AreEqual(Mathf.Pow(0.5f, 1.5f), MassEngagementRules.CommandControlStrain(0.5f, 0f), Eps);
            // 堅い指揮機構が負担を消す
            Assert.AreEqual(0f, MassEngagementRules.CommandControlStrain(1f, 1f), Eps);
        }

        /// <summary>連携の破綻＝兵員数^1.5×(1−通信)＝大軍は連携が崩れ、良い通信が保つ。</summary>
        [Test]
        public void CoordinationBreakdown_FallsWithCommunication()
        {
            // 兵員0.8・通信0 → 0.8^1.5 = 0.715541…
            Assert.AreEqual(Mathf.Pow(0.8f, 1.5f), MassEngagementRules.CoordinationBreakdown(0.8f, 0f), Eps);
            // 通信最良で破綻なし
            Assert.AreEqual(0f, MassEngagementRules.CoordinationBreakdown(0.8f, 1f), Eps);
            // 通信が良いほど破綻が減る（単調減）
            Assert.Less(MassEngagementRules.CoordinationBreakdown(0.6f, 0.8f),
                MassEngagementRules.CoordinationBreakdown(0.6f, 0.2f));
        }

        /// <summary>補給のスケール負担＝兵員数^1.3×(1−補給能力)＝大軍ほど補給が重い。</summary>
        [Test]
        public void LogisticalScaleStrain_NonlinearAndEasedByCapacity()
        {
            // 兵員1.0・補給能力0 → 1.0
            Assert.AreEqual(1f, MassEngagementRules.LogisticalScaleStrain(1f, 0f), Eps);
            // 兵員0.5・補給能力0 → 0.5^1.3 = 0.406126…
            Assert.AreEqual(Mathf.Pow(0.5f, 1.3f), MassEngagementRules.LogisticalScaleStrain(0.5f, 0f), Eps);
            // 補給能力で半分に → 0.5^1.3 ×(1−0.5)
            Assert.AreEqual(Mathf.Pow(0.5f, 1.3f) * 0.5f,
                MassEngagementRules.LogisticalScaleStrain(0.5f, 0.5f), Eps);
        }

        /// <summary>実効戦闘力＝生戦力÷規模摩擦乗数＝数がそのまま力にならない（大軍ほど目減り）。</summary>
        [Test]
        public void EffectiveCombatPower_DilutedByScaleFriction()
        {
            // 摩擦乗数1.0なら無傷
            Assert.AreEqual(0.8f, MassEngagementRules.EffectiveCombatPower(0.8f, 1f), Eps);
            // 生戦力0.9・乗数2.0 → 0.45
            Assert.AreEqual(0.45f, MassEngagementRules.EffectiveCombatPower(0.9f, 2f), Eps);
            // 乗数が大きいほど実効戦力が小さい（数が多くても摩擦で弱くなる）
            Assert.Less(MassEngagementRules.EffectiveCombatPower(1f, 2.5f),
                MassEngagementRules.EffectiveCombatPower(1f, 1.5f));
        }

        /// <summary>最適兵力規模＝指揮容量×地形制約＝統御できる範囲を超えると逆に弱くなる境目。</summary>
        [Test]
        public void OptimalForceSize_BoundedByCapacityAndTerrain()
        {
            // 容量0.8・地形0.5 → 0.4
            Assert.AreEqual(0.4f, MassEngagementRules.OptimalForceSize(0.8f, 0.5f), Eps);
            // 地形が狭ければ最適規模も小さい（隘路では大軍を活かせない）
            Assert.Less(MassEngagementRules.OptimalForceSize(1f, 0.2f),
                MassEngagementRules.OptimalForceSize(1f, 0.9f));
        }

        /// <summary>混乱リスク＝兵員×戦場の霧＝大軍が霧の中で同士討ち・混乱する危険（どちらか欠ければ起きにくい）。</summary>
        [Test]
        public void MassConfusionRisk_NeedsBothTroopsAndFog()
        {
            // 兵員0.6・霧0.5 → 0.3
            Assert.AreEqual(0.3f, MassEngagementRules.MassConfusionRisk(0.6f, 0.5f), Eps);
            // 霧が晴れていれば混乱しない
            Assert.AreEqual(0f, MassEngagementRules.MassConfusionRisk(1f, 0f), Eps);
            // 兵員が居なければ混乱しない
            Assert.AreEqual(0f, MassEngagementRules.MassConfusionRisk(0f, 1f), Eps);
        }

        /// <summary>統御不能な大軍判定＝規模摩擦乗数が閾値（既定2.0）超で持て余す。</summary>
        [Test]
        public void IsUnwieldyHost_AboveThreshold()
        {
            // 既定閾値2.0：2.5は統御不能、1.8は統御可能
            Assert.IsTrue(MassEngagementRules.IsUnwieldyHost(2.5f));
            Assert.IsFalse(MassEngagementRules.IsUnwieldyHost(1.8f));
            // 閾値ちょうどは超でない
            Assert.IsFalse(MassEngagementRules.IsUnwieldyHost(2f));
            // 規模摩擦乗数と連動：大兵員×広指揮幅で統御不能になる
            float mul = MassEngagementRules.ScaleFrictionMultiplier(1f, 1f); // 2.5
            Assert.IsTrue(MassEngagementRules.IsUnwieldyHost(mul));
        }
    }
}
