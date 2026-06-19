using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>機雷戦（敷設→密度→通過損害/封鎖/掃海/味方リスク）の純ロジック検証。既定Params固定。</summary>
    public class MineWarfareRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void MinefieldDensity_ScalesWithMinesAndWidth()
        {
            // 5000機を幅5に敷くと密度1.0、幅2に1000機で0.5。
            Assert.AreEqual(1.0f, MineWarfareRules.MinefieldDensity(5000f, 5f), Eps);
            Assert.AreEqual(0.5f, MineWarfareRules.MinefieldDensity(1000f, 2f), Eps);
        }

        [Test]
        public void MinefieldDensity_ClampsAtOne()
        {
            // 過密でも密度は1.0で頭打ち。
            Assert.AreEqual(1.0f, MineWarfareRules.MinefieldDensity(3000f, 2f), Eps);
        }

        [Test]
        public void TransitDamage_IncreasesWithSpeed()
        {
            // 基準速度10で密度0.5なら100。速い20で踏みやすく200、遅い5で50。
            Assert.AreEqual(100f, MineWarfareRules.TransitDamage(0.5f, 10f), Eps);
            Assert.AreEqual(200f, MineWarfareRules.TransitDamage(0.5f, 20f), Eps);
            Assert.AreEqual(50f, MineWarfareRules.TransitDamage(0.5f, 5f), Eps);
        }

        [Test]
        public void PassageDenial_EqualsDensity()
        {
            Assert.AreEqual(0.5f, MineWarfareRules.PassageDenial(0.5f), Eps);
            Assert.AreEqual(1.0f, MineWarfareRules.PassageDenial(1.5f), Eps); // クランプ
        }

        [Test]
        public void MinesweepingTime_ScalesAndZeroCapacityIsInfinite()
        {
            // 密度0.5を能力2で掃海＝0.5*100/2=25。能力0は無限。
            Assert.AreEqual(25f, MineWarfareRules.MinesweepingTime(0.5f, 2f), Eps);
            Assert.AreEqual(float.PositiveInfinity, MineWarfareRules.MinesweepingTime(0.5f, 0f));
        }

        [Test]
        public void ChannelClearing_ReducesDensityOverTime()
        {
            // 密度0.5を能力2でdt10掃海＝0.5-2*10/100=0.3。
            Assert.AreEqual(0.3f, MineWarfareRules.ChannelClearing(2f, 0.5f, 10f), Eps);
        }

        [Test]
        public void FriendlyTransitRisk_ReducedByRouteMarking()
        {
            // 標識なしは密度そのまま、標識1.0で味方リスク0。
            Assert.AreEqual(0.8f, MineWarfareRules.FriendlyTransitRisk(0.8f, 0f), Eps);
            Assert.AreEqual(0.6f, MineWarfareRules.FriendlyTransitRisk(0.8f, 0.25f), Eps);
            Assert.AreEqual(0f, MineWarfareRules.FriendlyTransitRisk(0.8f, 1f), Eps);
        }

        [Test]
        public void LayingCost_AndBlockedThreshold()
        {
            Assert.AreEqual(500f, MineWarfareRules.LayingCost(1000f), Eps);
            Assert.IsTrue(MineWarfareRules.IsChannelBlocked(0.5f, 0.3f));
            Assert.IsFalse(MineWarfareRules.IsChannelBlocked(0.2f, 0.3f));
        }

        [Test]
        public void Story_MinefieldBlocksChannelDamagesEnemyButHindersFriendly()
        {
            // 物語：回廊（幅2）へ4000機の宙雷源を敷く。
            float density = MineWarfareRules.MinefieldDensity(4000f, 2f);
            Assert.AreEqual(1.0f, density, Eps); // 4000/2000=2.0→クランプ1.0＝濃密に封鎖

            // 航路は封鎖される（封鎖度1.0≧閾値0.5）。
            float denial = MineWarfareRules.PassageDenial(density);
            Assert.IsTrue(MineWarfareRules.IsChannelBlocked(denial, 0.5f));

            // 強行突破する敵は損害を受け、速いほど大きい。
            float slow = MineWarfareRules.TransitDamage(density, 5f);
            float fast = MineWarfareRules.TransitDamage(density, 20f);
            Assert.Greater(fast, slow);
            Assert.AreEqual(100f, slow, Eps);  // 1.0*200*0.5
            Assert.AreEqual(400f, fast, Eps);  // 1.0*200*2.0

            // 掃海には時間が要る（能力2で密度1.0＝50秒）。
            float sweepTime = MineWarfareRules.MinesweepingTime(density, 2f);
            Assert.AreEqual(50f, sweepTime, Eps);

            // 掃海を進めると安全航路が開く（密度が減る）。
            float afterSweep = MineWarfareRules.ChannelClearing(2f, density, 10f);
            Assert.Less(afterSweep, density);

            // だが標識のない味方も通行を妨げられる（標識を整備すれば軽減）。
            float unmarkedRisk = MineWarfareRules.FriendlyTransitRisk(density, 0f);
            float markedRisk = MineWarfareRules.FriendlyTransitRisk(density, 0.5f);
            Assert.Greater(unmarkedRisk, markedRisk);

            // 敷設にはコストが掛かる。
            Assert.AreEqual(2000f, MineWarfareRules.LayingCost(4000f), Eps); // 4000*0.5
        }
    }
}
