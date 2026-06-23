using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ネットワーク中心戦（NCW）を固定する：センサー×処理で情報優越、ネットワーク分配で共有状況認識、
    /// 共有認識から自己同期（中央指令なしの協調）と決定優越、両者でテンポ加速。接続ノードの規模効果は
    /// 収穫逓増だが上限で飽和（メトカーフ近似）、データ量×非選別が閾値を超えれば情報過多。最終的に
    /// テンポ×規模効果−過多で戦闘力の相乗倍率（0.5〜2.0）。境界クランプと既定Params期待値を手計算で検算。
    /// </summary>
    public class NetworkCentricWarfareRulesTests
    {
        private static readonly NetworkCentricWarfareParams P = NetworkCentricWarfareParams.Default;
        // 共有下駄0.2・規模効果上限1.0・半飽和点10・過多閾値0.6・過多感度2.0・相乗上振れ1.0・過多下振れ1.0

        [Test]
        public void InformationSuperiority_RequiresBothSensorAndProcessing()
        {
            // センサー0.8×処理0.5 = 0.4（両立が要る＝どちらか0なら0）
            Assert.AreEqual(0.4f, NetworkCentricWarfareRules.InformationSuperiority(0.8f, 0.5f, P), 1e-4f);
            Assert.AreEqual(0f, NetworkCentricWarfareRules.InformationSuperiority(0f, 1f, P), 1e-4f);
            // 入力 clamp（範囲外でも 1.0 上限）
            Assert.AreEqual(1f, NetworkCentricWarfareRules.InformationSuperiority(5f, 5f, P), 1e-4f);
        }

        [Test]
        public void SharedAwareness_DistributionFloorAndFullDistribution()
        {
            // 分配1.0なら Lerp=1 → 0.4×1 = 0.4
            Assert.AreEqual(0.4f, NetworkCentricWarfareRules.SharedAwareness(0.4f, 1f, P), 1e-4f);
            // 分配0でも下駄0.2 → 0.4×0.2 = 0.08（つながっていなくても最低限は共有）
            Assert.AreEqual(0.08f, NetworkCentricWarfareRules.SharedAwareness(0.4f, 0f, P), 1e-4f);
        }

        [Test]
        public void SelfSynchronization_NeedsAwarenessAndInitiative()
        {
            // 共有0.5×主体性0.6 = 0.3
            Assert.AreEqual(0.3f, NetworkCentricWarfareRules.SelfSynchronization(0.5f, 0.6f, P), 1e-4f);
            // 主体性0なら自己同期は起きない（命令待ちのまま）
            Assert.AreEqual(0f, NetworkCentricWarfareRules.SelfSynchronization(0.9f, 0f, P), 1e-4f);
        }

        [Test]
        public void DecisionSuperiority_AwarenessTimesSpeed()
        {
            // 共有0.5×決定速度0.8 = 0.4
            Assert.AreEqual(0.4f, NetworkCentricWarfareRules.DecisionSuperiority(0.5f, 0.8f, P), 1e-4f);
        }

        [Test]
        public void TempoAcceleration_FromSyncAndDecision()
        {
            // 自己同期0.3×決定優越0.4 = 0.12
            Assert.AreEqual(0.12f, NetworkCentricWarfareRules.TempoAcceleration(0.3f, 0.4f, P), 1e-4f);
        }

        [Test]
        public void NetworkScaleEffect_SaturatesWithDiminishingReturns()
        {
            // 1ノード以下は規模効果なし（つながって初めて価値）
            Assert.AreEqual(0f, NetworkCentricWarfareRules.NetworkScaleEffect(1, P), 1e-3f);
            Assert.AreEqual(0f, NetworkCentricWarfareRules.NetworkScaleEffect(0, P), 1e-3f);
            // 11ノード：effective=10、10/(10+10)=0.5 → maxScaleEffect1.0×0.5 = 0.5（半飽和点）
            Assert.AreEqual(0.5f, NetworkCentricWarfareRules.NetworkScaleEffect(11, P), 1e-3f);
            // 多いほど上限へ漸近するが超えない＝収穫逓増だが天井あり
            float big = NetworkCentricWarfareRules.NetworkScaleEffect(1001, P);
            Assert.Greater(big, 0.5f);
            Assert.Less(big, 1.0f);
        }

        [Test]
        public void InformationOverload_OnlyAboveThreshold()
        {
            // データ量1.0×非選別(1-0)=1.0、閾値0.6超過0.4×感度2.0 = 0.8（過多）
            Assert.AreEqual(0.8f, NetworkCentricWarfareRules.InformationOverload(1f, 0f, P), 1e-4f);
            // 負荷0.25は閾値0.6以下＝さばける（過多なし）
            Assert.AreEqual(0f, NetworkCentricWarfareRules.InformationOverload(0.5f, 0.5f, P), 1e-4f);
            // 選別能力が高ければ大量でも過多にならない
            Assert.AreEqual(0f, NetworkCentricWarfareRules.InformationOverload(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void CombatPowerMultiplier_SynergyAndOverloadAndClamp()
        {
            // テンポ0.8×規模効果1.0：synergy=1.0×0.8×(1+1)×0.5=0.8 → 1+0.8 = 1.8（過多なし）
            Assert.AreEqual(1.8f, NetworkCentricWarfareRules.CombatPowerMultiplier(0.8f, 1.0f, 0f, P), 1e-4f);
            // 同条件＋情報過多0.5：penalty=1.0×0.5=0.5 → 1.8-0.5 = 1.3
            Assert.AreEqual(1.3f, NetworkCentricWarfareRules.CombatPowerMultiplier(0.8f, 1.0f, 0.5f, P), 1e-4f);
            // 完全劣勢＋過多MAXでも下限0.5は残る
            Assert.AreEqual(0.5f, NetworkCentricWarfareRules.CombatPowerMultiplier(0f, 0f, 1f, P), 1e-4f);
            // 上限2.0を超えない
            Assert.AreEqual(2.0f, NetworkCentricWarfareRules.CombatPowerMultiplier(1f, 4f, 0f, P), 1e-4f);
        }

        [Test]
        public void Params_ClampOutOfRangeInputs()
        {
            // ctor が範囲外を安全側へクランプ（決定論・破綻させない）
            var p = new NetworkCentricWarfareParams(5f, 99f, 0f, 5f, 99f, 99f, -1f);
            Assert.AreEqual(1f, p.distributionFloor, 1e-4f);      // Clamp01
            Assert.AreEqual(4f, p.maxScaleEffect, 1e-4f);          // 0..4
            Assert.AreEqual(1f, p.scaleHalfNodes, 1e-4f);          // 1..10000
            Assert.AreEqual(1f, p.overloadThreshold, 1e-4f);       // Clamp01
            Assert.AreEqual(10f, p.overloadScale, 1e-4f);          // 0..10
            Assert.AreEqual(4f, p.synergyGain, 1e-4f);             // 0..4
            Assert.AreEqual(0f, p.overloadPenalty, 1e-4f);         // 0..4（負は0）
        }

        // ── 物語テスト：情報優越から戦力相乗まで一気通貫 ──────────────
        [Test]
        public void Narrative_InformationSharingMultipliesCombatPower()
        {
            // 高度なセンサー網と処理で情報優越を得る
            float info = NetworkCentricWarfareRules.InformationSuperiority(0.9f, 0.9f, P); // 0.81
            Assert.Greater(info, 0.7f);

            // ネットワークで全艦に配り共有状況認識を作る
            float awareness = NetworkCentricWarfareRules.SharedAwareness(info, 0.9f, P);
            Assert.Greater(awareness, 0.6f);

            // 共有認識が自己同期（中央指令なしの協調）と決定優越を生む
            float sync = NetworkCentricWarfareRules.SelfSynchronization(awareness, 0.9f, P);
            float decision = NetworkCentricWarfareRules.DecisionSuperiority(awareness, 0.9f, P);
            float tempo = NetworkCentricWarfareRules.TempoAcceleration(sync, decision, P);
            Assert.Greater(tempo, 0f);

            // 多数のノードが接続し規模効果が効く（情報過多は選別で抑えている）
            float scale = NetworkCentricWarfareRules.NetworkScaleEffect(101, P);
            float overloadLow = NetworkCentricWarfareRules.InformationOverload(0.8f, 0.9f, P); // 選別で0
            float multHigh = NetworkCentricWarfareRules.CombatPowerMultiplier(tempo, scale, overloadLow, P);

            // 対照：選別能力が低く情報過多に溺れた同じ艦隊
            float overloadHigh = NetworkCentricWarfareRules.InformationOverload(1f, 0f, P); // 0.8
            float multLow = NetworkCentricWarfareRules.CombatPowerMultiplier(tempo, scale, overloadHigh, P);

            // ネットワーク中心戦が戦力を相乗的に高める（情報過多に溺れた側を上回る）
            Assert.Greater(multHigh, 1.0f);
            Assert.Greater(multHigh, multLow);
            // どちらも 0.5〜2.0 に収まる
            Assert.GreaterOrEqual(multHigh, 0.5f);
            Assert.LessOrEqual(multHigh, 2.0f);
            Assert.GreaterOrEqual(multLow, 0.5f);
        }
    }
}
