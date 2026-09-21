#if UNITY_5_3_OR_NEWER
using NUnit.Framework;

namespace Ginei.Tests
{
    public class RingiNarrativeRuntimeTests
    {
        private sealed class Provider : IRingiNarrativeProvider
        {
            public string prose = "生成された人格的な起案文";
            public int choice = 1;
            public bool throwDraft;

            public bool TryDraft(in RingiDraftRequest request, out string value)
            {
                if (throwDraft) throw new System.InvalidOperationException("offline");
                value = prose;
                return true;
            }

            public bool TryAdjudicate(in RingiAdjudicationRequest request, out int choiceIndex, out string reasoning)
            {
                choiceIndex = choice;
                reasoning = "人物の価値観に基づく判断";
                return true;
            }
        }

        [SetUp]
        public void SetUp()
        {
            StrategySession.Decisions = new DecisionQueue();
            DecisionDeck.AuthorityCheck = null;
            RingiNarrativeRuntime.Configure(null);
        }

        [TearDown]
        public void TearDown()
        {
            DecisionDeck.AuthorityCheck = null;
            RingiNarrativeRuntime.Configure(null);
            StrategySession.Decisions = new DecisionQueue();
        }

        private static PendingDecision Decision(int id = 71001)
        {
            var d = new PendingDecision(id, "増税の建白", DecisionSeverity.通常,
                DecisionSource.建白結果, "tax.hike", defaultChoiceIndex: 1, body: "決定論テンプレ");
            d.choices.Add("裁可する");
            d.choices.Add("見送る");
            return d;
        }

        [Test]
        public void Draft_ChangesOnlyEphemeralProse_NotStructuredDecision()
        {
            var provider = new Provider();
            RingiNarrativeRuntime.Configure(provider);
            PendingDecision d = Decision();

            DecisionDeck.Enqueue(d);

            Assert.AreEqual("生成された人格的な起案文", RingiNarrativeRuntime.TextFor(d));
            Assert.AreEqual("決定論テンプレ", d.body, "生成文を保存対象へ書いてはいけない");
            Assert.AreEqual("tax.hike", d.effectKey);
            Assert.AreEqual(DecisionStatus.新着, d.status);

            RingiNarrativeRuntime.Clear(); // ロード直後と同じ
            Assert.AreEqual("決定論テンプレ", RingiNarrativeRuntime.TextFor(d));
        }

        [Test]
        public void MissingOrFailedProvider_UsesTemplate()
        {
            PendingDecision d = Decision();
            Assert.AreEqual("決定論テンプレ", new RingiDrafter().Draft(d, d.body));

            var failed = new Provider { throwDraft = true };
            Assert.AreEqual("決定論テンプレ", new RingiDrafter(failed).Draft(d, d.body));
        }

        [Test]
        public void NonRingiDecision_DoesNotUseGeneratedProse()
        {
            RingiNarrativeRuntime.Configure(new Provider());
            PendingDecision d = Decision();
            d.source = DecisionSource.イベント;

            DecisionDeck.Enqueue(d);

            Assert.AreEqual("決定論テンプレ", RingiNarrativeRuntime.TextFor(d));
        }

        [Test]
        public void Adjudicator_SuggestionStillPassesCommonCoreValidation()
        {
            var provider = new Provider { choice = 1 };
            RingiNarrativeRuntime.Configure(provider);
            PendingDecision d = Decision();
            DecisionDeck.Enqueue(d);

            Assert.IsTrue(RingiNarrativeRuntime.TryAdjudicate(d, out string reasoning));
            Assert.AreEqual("人物の価値観に基づく判断", reasoning);
            Assert.AreEqual(DecisionStatus.決裁済, d.status);
            Assert.AreEqual(1, d.chosenIndex);
            Assert.IsFalse(RingiNarrativeRuntime.TryAdjudicate(d, out _), "解決済みの再決裁は Core が拒否する");
        }

        [Test]
        public void Adjudicator_CannotForceOutOfRangeChoice()
        {
            RingiNarrativeRuntime.Configure(new Provider { choice = 99 });
            PendingDecision d = Decision();
            DecisionDeck.Enqueue(d);

            Assert.IsFalse(RingiNarrativeRuntime.TryAdjudicate(d, out _));
            Assert.AreEqual(DecisionStatus.新着, d.status);
            Assert.AreEqual(-1, d.chosenIndex);
        }

        [Test]
        public void DetailText_CanUseEphemeralBody_WithoutMutatingSavedBody()
        {
            PendingDecision d = Decision();
            string detail = DecisionAttributionRules.DetailText(d, "一時生成文");

            StringAssert.StartsWith("一時生成文", detail);
            Assert.AreEqual("決定論テンプレ", d.body);
        }

        [Test]
        public void RuntimeProseCache_IsBounded()
        {
            RingiNarrativeRuntime.Configure(new Provider());
            for (int i = 0; i < RingiNarrativeRuntime.Capacity + 9; i++)
                RingiNarrativeRuntime.Prepare(Decision(72000 + i));

            Assert.AreEqual(RingiNarrativeRuntime.Capacity, RingiNarrativeRuntime.CachedCount);
        }
    }
}
#endif
