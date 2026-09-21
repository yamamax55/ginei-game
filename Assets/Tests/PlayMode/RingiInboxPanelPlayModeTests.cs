using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class RingiInboxPanelPlayModeTests
    {
        private float savedTimeScale;
        private GameClock savedClock;
        private PetitionLedger savedPetitions;
        private PetitionLedger savedFleetPetitions;

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            savedClock = StrategySession.Clock;
            savedPetitions = StrategySession.Petitions;
            savedFleetPetitions = StrategySession.FleetPetitions;
            Time.timeScale = 1.5f;
            StrategySession.Clock = new GameClock();
            StrategySession.Petitions = new PetitionLedger();
            StrategySession.FleetPetitions = new PetitionLedger();
            UIWindowStack.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (RingiObserverOverlay.InstanceForTest != null)
                Object.DestroyImmediate(RingiObserverOverlay.InstanceForTest.gameObject);
            UIWindowStack.Clear();
            StrategySession.Clock = savedClock;
            StrategySession.Petitions = savedPetitions;
            StrategySession.FleetPetitions = savedFleetPetitions;
            Time.timeScale = savedTimeScale;
        }

        [Test]
        public void Inbox_BuildsScrollableProposalUi_AndPausesUntilEsc()
        {
            var petition = new Petition(0, "辺境防衛の建白", Faction.同盟, BoxKind.政治家,
                PetitionOrigin.建白, "qa.noop") { status = PetitionStatus.決裁待ち };
            StrategySession.Petitions.Add(petition);

            var go = new GameObject("RingiInboxPanel_Qa");
            var panel = go.AddComponent<RingiObserverOverlay>();
            panel.OpenForTest();

            Assert.IsTrue(RingiObserverOverlay.IsOpen);
            Assert.AreEqual(0f, Time.timeScale, "受信箱を開いても描画時間が止まらない");
            Assert.IsTrue(StrategySession.Clock.paused, "受信箱を開いても戦役時計が止まらない");
            Assert.IsTrue(panel.ScrollbarVisibleForTest, "スクロール可能な受信箱に見えるスクロールバーがない");
            Assert.IsTrue(panel.ProposalFormExistsForTest, "誰の箱・WHAT・WHYの建白フォームがない");
            Assert.Greater(panel.InboxRowCountForTest, 0, "進行中の建白が一覧に現れない");
            Assert.IsTrue(panel.EscRegisteredForTest);

            Assert.IsTrue(UIWindowStack.CloseTopmost(), "Escスタックから受信箱を閉じられない");
            Assert.IsFalse(RingiObserverOverlay.IsOpen);
            Assert.AreEqual(1.5f, Time.timeScale, 1e-4f, "閉じた後に元の速度へ戻らない");
            Assert.IsFalse(StrategySession.Clock.paused, "閉じた後も戦役時計が止まっている");
        }
    }
}
