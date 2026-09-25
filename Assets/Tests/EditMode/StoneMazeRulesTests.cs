using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>石兵八陣（八陣図）の純ロジック：八門の吉凶・罠判定・幻惑倍率・石塁配置。</summary>
    public class StoneMazeRulesTests
    {
        [Test]
        public void GateAtAngle_MapsOctants_InOrder()
        {
            // 0..45度=休(0)、45..90=生(1)、…、315..360=開(7)
            Assert.AreEqual(EightGate.休, StoneMazeRules.GateAtAngle(0f));
            Assert.AreEqual(EightGate.休, StoneMazeRules.GateAtAngle(44f));
            Assert.AreEqual(EightGate.生, StoneMazeRules.GateAtAngle(45f));
            Assert.AreEqual(EightGate.傷, StoneMazeRules.GateAtAngle(90f));
            Assert.AreEqual(EightGate.開, StoneMazeRules.GateAtAngle(316f));
        }

        [Test]
        public void GateAtAngle_WrapsNegativeAndOver360()
        {
            Assert.AreEqual(EightGate.開, StoneMazeRules.GateAtAngle(-1f));   // -1 → 359 → 開
            Assert.AreEqual(EightGate.休, StoneMazeRules.GateAtAngle(360f));  // 360 → 0 → 休
            Assert.AreEqual(EightGate.生, StoneMazeRules.GateAtAngle(405f));  // 405 → 45 → 生
        }

        [Test]
        public void SafeGates_AreThreeAuspicious()
        {
            Assert.IsTrue(StoneMazeRules.IsSafeGate(EightGate.休));
            Assert.IsTrue(StoneMazeRules.IsSafeGate(EightGate.生));
            Assert.IsTrue(StoneMazeRules.IsSafeGate(EightGate.開));
            // 五凶門
            Assert.IsFalse(StoneMazeRules.IsSafeGate(EightGate.傷));
            Assert.IsFalse(StoneMazeRules.IsSafeGate(EightGate.杜));
            Assert.IsFalse(StoneMazeRules.IsSafeGate(EightGate.景));
            Assert.IsFalse(StoneMazeRules.IsSafeGate(EightGate.死));
            Assert.IsFalse(StoneMazeRules.IsSafeGate(EightGate.驚));

            // 吉門は3つ・凶門は5つ
            int safe = 0;
            foreach (EightGate g in System.Enum.GetValues(typeof(EightGate)))
                if (StoneMazeRules.IsSafeGate(g)) safe++;
            Assert.AreEqual(3, safe);
        }

        [Test]
        public void IsTrapped_TrueOnInauspiciousBearing()
        {
            Assert.IsFalse(StoneMazeRules.IsTrapped(10f));  // 休門=吉
            Assert.IsFalse(StoneMazeRules.IsTrapped(50f));  // 生門=吉
            Assert.IsTrue(StoneMazeRules.IsTrapped(95f));   // 傷門=凶
            Assert.IsTrue(StoneMazeRules.IsTrapped(200f));  // 死門=凶
        }

        [Test]
        public void DisorientMobility_HalvesWhenTrapped()
        {
            var p = StoneMazeParams.Default;
            Assert.AreEqual(0.5f, StoneMazeRules.DisorientMobilityFactor(true, p), 1e-4f);
            Assert.AreEqual(1.0f, StoneMazeRules.DisorientMobilityFactor(false, p), 1e-4f);
        }

        [Test]
        public void DisorientMobility_ClampedToFloor()
        {
            var p = new StoneMazeParams(0.01f); // 下限0.1へクランプ
            Assert.AreEqual(0.1f, StoneMazeRules.DisorientMobilityFactor(true, p), 1e-4f);
        }

        [Test]
        public void MazeNodeLocal_OctagonOnRadius()
        {
            float r = 4f;
            // 8塁すべて半径 r 上・等間隔
            for (int i = 0; i < StoneMazeRules.GateCount; i++)
                Assert.AreEqual(r, StoneMazeRules.MazeNodeLocal(i, r).magnitude, 1e-3f);
            // index 0 は +X 方向
            Vector2 n0 = StoneMazeRules.MazeNodeLocal(0, r);
            Assert.AreEqual(r, n0.x, 1e-3f);
            Assert.AreEqual(0f, n0.y, 1e-3f);
            // index は GateCount で巡回
            Vector2 n8 = StoneMazeRules.MazeNodeLocal(8, r);
            Assert.AreEqual(n0.x, n8.x, 1e-3f);
            Assert.AreEqual(n0.y, n8.y, 1e-3f);
        }
    }
}
