using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>EventEngine→決裁デスク→効果適用が実ランタイムで一つの経路になることを固定する（DESK-6 #1634）。</summary>
    public class EventDecisionBridgePlayModeTests
    {
        private DecisionQueue savedQueue;
        private System.Func<PendingDecision, DecisionAuthorityResult> savedAuthority;
        private GameObject viewObject;
        private GalaxyView view;
        private bool savedLegacyPanelEnabled;
        private float savedTimeScale;

        [SetUp]
        public void SetUp()
        {
            savedQueue = StrategySession.Decisions;
            savedAuthority = DecisionDeck.AuthorityCheck;
            savedLegacyPanelEnabled = StrategyEventPanel.Enabled;
            savedTimeScale = Time.timeScale;
            StrategySession.Decisions = new DecisionQueue();
            DecisionDeck.AuthorityCheck = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null) view.UnbindPolicyEventForQa();
            if (viewObject != null) Object.DestroyImmediate(viewObject);
            StrategyEventPanel.Hide();
            StrategyEventPanel.Enabled = savedLegacyPanelEnabled;
            Time.timeScale = savedTimeScale;
            DecisionDeck.AuthorityCheck = savedAuthority;
            StrategySession.Decisions = savedQueue;
        }

        [Test]
        public void Event_Card_Uses_Common_Resolve_Path_And_Applies_Only_Once()
        {
            int applied = 0;
            var def = new GameEventDef("bridge-event", "政策イベント", "対応を選ぶ")
                .AddChoice("実行", _ => applied++)
                .AddChoice("見送る");
            def.condition = _ => true;

            var engine = new EventEngine();
            engine.Register(def);
            Assert.AreSame(def, engine.Tick(new EventContext(), 1f, 0f));

            viewObject = new GameObject("EventDecisionBridgeQa");
            viewObject.SetActive(false); // Startで戦略盤面を組まず、検証対象の配線だけ使う。
            view = viewObject.AddComponent<GalaxyView>();
            view.BindPolicyEventForQa(engine, new EventContext());
            view.EnqueuePolicyEventForQa(def);

            Assert.AreEqual(1, DecisionDeck.Queue.ActiveCount());
            PendingDecision card = DecisionDeck.Queue.items[0];
            Assert.AreEqual(DecisionSource.イベント, card.source);
            Assert.AreEqual(0, applied, "裁可前に効果が出た");

            Assert.IsTrue(DecisionDeck.Resolve(card.id, 0));
            Assert.AreEqual(1, applied);
            Assert.IsTrue(card.applied);
            Assert.AreEqual(PetitionActionOutcome.実行, card.outcome);
            Assert.IsFalse(DecisionDeck.Resolve(card.id, 0), "同じ案件を再裁可できた");
            Assert.AreEqual(1, applied, "効果が二重適用された");
        }

        [Test]
        public void Critical_Event_Uses_Central_Panel_Even_When_Legacy_Panel_Is_Suppressed()
        {
            var def = new GameEventDef("critical-event", "重大政策イベント", "必ず判断する")
                .AddChoice("決断する");
            def.severity = DecisionSeverity.重大;

            var engine = new EventEngine();
            engine.Register(def);
            viewObject = new GameObject("CriticalEventDecisionBridgeQa");
            viewObject.SetActive(false);
            view = viewObject.AddComponent<GalaxyView>();
            view.BindPolicyEventForQa(engine, new EventContext());

            StrategyEventPanel.Enabled = false;
            view.EnqueuePolicyEventForQa(def);

            Assert.AreEqual(1, DecisionDeck.Queue.ActiveCount());
            Assert.AreEqual(DecisionSeverity.重大, DecisionDeck.Queue.items[0].severity);
            Assert.IsTrue(StrategyEventPanel.IsOpen, "重大案件が中央モーダルに出なかった");
            Assert.AreEqual(0f, Time.timeScale, "重大案件で時間が止まらなかった");
        }
    }
}
