using NUnit.Framework;

namespace Ginei.Tests
{
    public class EventDecisionRulesTests
    {
        [Test]
        public void Create_Copies_Event_Into_Deck_Card()
        {
            var def = new GameEventDef("unrest", "民衆の不満", "どう応える？")
                .AddChoice("減税")
                .AddChoice("強硬策");
            def.severity = DecisionSeverity.重要;

            PendingDecision d = EventDecisionRules.Create(def, 93001, def.severity, 1);

            Assert.AreEqual(DecisionSource.イベント, d.source);
            Assert.AreEqual("event:unrest", d.effectKey);
            Assert.AreEqual("どう応える？", d.body);
            Assert.AreEqual(DecisionSeverity.重要, d.severity);
            CollectionAssert.AreEqual(new[] { "減税", "強硬策" }, d.choices);
            Assert.AreEqual(1, d.defaultChoiceIndex);
            Assert.IsTrue(DecisionEffectRegistryRules.IsImplemented(d.effectKey));
        }

        [Test]
        public void ResolveById_Applies_Once_And_Removes_Queued_Event()
        {
            int applied = 0;
            var def = new GameEventDef("unrest", "民衆の不満", "")
                .AddChoice("減税", _ => applied++);
            def.condition = _ => true;
            var engine = new EventEngine();
            engine.Register(def);
            Assert.AreSame(def, engine.Tick(new EventContext(), 1f, 0f));

            Assert.IsTrue(engine.ResolveById("unrest", 0, new EventContext()));
            Assert.AreEqual(1, applied);
            Assert.AreEqual(0, engine.PendingCount);
            Assert.IsFalse(engine.ResolveById("missing", 0, new EventContext()));
            Assert.AreEqual(1, applied);
        }

        [Test]
        public void ResolveById_Works_After_TransientQueueWasRebuilt()
        {
            int applied = 0;
            var def = new GameEventDef("saved-event", "保存済み案件", "")
                .AddChoice("実行", _ => applied++);
            var rebuilt = new EventEngine();
            rebuilt.Register(def);

            Assert.IsTrue(rebuilt.ResolveById("saved-event", 0, new EventContext()));
            Assert.AreEqual(1, applied);
        }
    }
}
