using System.Collections.Generic;
using NUnit.Framework;

namespace Ginei.Tests
{
    public class BattleResultQueueLifecyclePlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleResultQueue.Clear();
            ReinforcementReturnQueue.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleResultQueue.Clear();
            ReinforcementReturnQueue.Clear();
        }

        [Test]
        public void CampaignReset_DiscardsPriorBattleResultsAndReinforcementReturns()
        {
            BattleResultQueue.Push(new BattleHandoff.State
            {
                Pending = true,
                Resolved = true,
                fleetIdA = 10,
                fleetIdB = 20,
                sideAWon = true,
                survivorStrength = 50,
            });
            ReinforcementReturnQueue.Push(new WarpReinforcement { id = 1, fleetId = 30 });
            ReinforcementReturnQueue.PushArrived(40);

            Assert.AreEqual(1, BattleResultQueue.Count);
            Assert.AreEqual(1, ReinforcementReturnQueue.Count);

            GalaxyView.ResetCampaignStatics();

            Assert.AreEqual(0, BattleResultQueue.Count, "前戦役の会戦結果が新戦役へ残った");
            Assert.AreEqual(0, ReinforcementReturnQueue.Count, "前戦役の援軍差し戻しが新戦役へ残った");
            var arrived = new List<int>();
            Assert.IsFalse(ReinforcementReturnQueue.TakeArrivedIds(arrived), "前戦役の到着済み援軍IDが新戦役へ残った");
            Assert.IsEmpty(arrived);
        }
    }
}
