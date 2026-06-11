using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>作戦摩擦（FrictionRules・CLZ-1 #1133）の純ロジックテスト。</summary>
    public class FrictionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>摩擦の大きさ＝命令深度・補給不足・低士気の重み付き合成。全ゼロは0・全1は1。</summary>
        [Test]
        public void FrictionLevel_合成と境界()
        {
            Assert.AreEqual(0f, FrictionRules.FrictionLevel(0f, 0f, 0f), Eps);
            Assert.AreEqual(1f, FrictionRules.FrictionLevel(1f, 1f, 1f), Eps);
            // 既定重み0.4/0.35/0.25・総和1。深度のみ1.0→0.4。
            Assert.AreEqual(0.4f, FrictionRules.FrictionLevel(1f, 0f, 0f), Eps);
            // 補給のみ1.0→0.35。
            Assert.AreEqual(0.35f, FrictionRules.FrictionLevel(0f, 1f, 0f), Eps);
            // 入力はクランプされる。
            Assert.AreEqual(0.25f, FrictionRules.FrictionLevel(0f, 0f, 5f), Eps);
        }

        /// <summary>実行成功＝計画の質×(1−摩擦)。摩擦が計画を目減りさせる。</summary>
        [Test]
        public void ExecutionSuccess_摩擦で目減り()
        {
            // 計画満点・摩擦ゼロ→計画どおり。
            Assert.AreEqual(1f, FrictionRules.ExecutionSuccess(1f, 0f), Eps);
            // 計画0.8・摩擦0.5→0.4。
            Assert.AreEqual(0.4f, FrictionRules.ExecutionSuccess(0.8f, 0.5f), Eps);
            // 摩擦最大→成功しない。
            Assert.AreEqual(0f, FrictionRules.ExecutionSuccess(1f, 1f), Eps);
        }

        /// <summary>計画の陳腐化度は摩擦に比例（接敵後の計画の崩れ）。</summary>
        [Test]
        public void PlanDegradation_摩擦比例()
        {
            Assert.AreEqual(0f, FrictionRules.PlanDegradation(0f), Eps);
            Assert.AreEqual(0.6f, FrictionRules.PlanDegradation(0.6f), Eps);
            Assert.AreEqual(1f, FrictionRules.PlanDegradation(1f), Eps);
        }

        /// <summary>遅延は命令深度×時間で累積＝指揮系統が長いほど積み重なる。</summary>
        [Test]
        public void CompoundingDelays_深度と時間で累積()
        {
            // 既定 delayPerDepth=0.2。深度1.0・dt2→0.4。
            Assert.AreEqual(0.4f, FrictionRules.CompoundingDelays(1f, 2f), Eps);
            // 深度半分なら遅延も半分。
            Assert.AreEqual(0.2f, FrictionRules.CompoundingDelays(0.5f, 2f), Eps);
            // 深度ゼロは遅延ゼロ。
            Assert.AreEqual(0f, FrictionRules.CompoundingDelays(0f, 10f), Eps);
        }

        /// <summary>戦場の霧＝摩擦×不確実性。どちらか一方でも欠ければ霧は薄い。</summary>
        [Test]
        public void FogOfWarPenalty_摩擦と不確実性の積()
        {
            Assert.AreEqual(0f, FrictionRules.FogOfWarPenalty(0.8f, 0f), Eps);
            Assert.AreEqual(0f, FrictionRules.FogOfWarPenalty(0f, 0.8f), Eps);
            Assert.AreEqual(0.5f, FrictionRules.FogOfWarPenalty(0.5f, 1f), Eps);
            Assert.AreEqual(0.25f, FrictionRules.FogOfWarPenalty(0.5f, 0.5f), Eps);
        }

        /// <summary>摩擦の緩和＝経験と単純さが揃うほど摩擦を削る（両方1で0倍＝完全緩和）。</summary>
        [Test]
        public void FrictionMitigation_経験と単純さ()
        {
            // 経験も単純さも無ければ緩和なし＝1倍。
            Assert.AreEqual(1f, FrictionRules.FrictionMitigation(0f, 0f), Eps);
            // 経験1・単純さ1→1−1×1=0倍（摩擦消滅）。
            Assert.AreEqual(0f, FrictionRules.FrictionMitigation(1f, 1f), Eps);
            // 経験0.5・単純さ0.5→1−0.25=0.75倍。
            Assert.AreEqual(0.75f, FrictionRules.FrictionMitigation(0.5f, 0.5f), Eps);
            // 片方だけ高くても他方が0なら緩和されない。
            Assert.AreEqual(1f, FrictionRules.FrictionMitigation(1f, 0f), Eps);
        }

        /// <summary>摩擦が士気を削る＝摩擦×侵食率×dt 分だけ低下（思い通りにいかない苛立ち）。</summary>
        [Test]
        public void MoraleUnderFriction_士気侵食()
        {
            // 既定 moraleErosionRate=0.3。摩擦1・dt1→0.3低下。0.8→0.5。
            Assert.AreEqual(0.5f, FrictionRules.MoraleUnderFriction(0.8f, 1f, 1f), Eps);
            // 摩擦ゼロなら士気不変。
            Assert.AreEqual(0.8f, FrictionRules.MoraleUnderFriction(0.8f, 0f, 5f), Eps);
            // 下限0でクランプ。
            Assert.AreEqual(0f, FrictionRules.MoraleUnderFriction(0.1f, 1f, 5f), Eps);
        }

        /// <summary>停滞判定＝実行成功率が閾値を下回るか。</summary>
        [Test]
        public void IsOperationBoggedDown_閾値判定()
        {
            // 既定閾値0.4。
            Assert.IsTrue(FrictionRules.IsOperationBoggedDown(0.3f));
            Assert.IsFalse(FrictionRules.IsOperationBoggedDown(0.5f));
            Assert.IsFalse(FrictionRules.IsOperationBoggedDown(0.4f));
            // 明示閾値。
            Assert.IsTrue(FrictionRules.IsOperationBoggedDown(0.5f, 0.6f));
        }
    }
}
