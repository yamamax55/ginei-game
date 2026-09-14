using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 閣僚の決裁権限を決裁デスクの共通入口（<see cref="DecisionDeck.Resolve"/>）と上申の確定（<see cref="DecisionAuthorityDirector.ConcludeApproval"/>）へ
    /// 接続したことを実ランタイムで固定する（#2768 #67）：政務官の裁可は所管大臣への上申になり効果は出ない、大臣の承認で効果は1回だけ、
    /// 審査中に大臣が解任されたら古い権限で承認せず効果なしで差し戻す、大臣本人は共通入口で直接裁可できる、上申の確定後も権限判定のフックが戻る。
    /// 盤面（GalaxyView）は置かず、判定は Game の入口と同じ Core の合成（<see cref="CabinetDecisionAuthorityRules.Evaluate"/>）を差し込む。
    /// </summary>
    public class CabinetDecisionBridgePlayModeTests
    {
        const int Year = 800;
        const Faction F = Faction.同盟;
        const int Top = 1000, Okura = 1003;
        const int Premier = 1, Minister = 2, Secretary = 3;
        static readonly CabinetParams Prm = CabinetParams.Default;

        private GameObject directorGo;
        private System.Func<PendingDecision, DecisionAuthorityResult> savedCheck;
        private DecisionQueue savedQueue;
        private PendingDecision card;
        private int resolvedCount;

        private PoliticsState pol;
        private List<Ministry> tree;
        private List<Person> roster;

        [SetUp]
        public void SetUp()
        {
            savedCheck = DecisionDeck.AuthorityCheck;
            savedQueue = StrategySession.Decisions;
            StrategySession.Decisions = new DecisionQueue();
            resolvedCount = 0;
            card = null;
            DecisionDeck.Resolved += OnResolved;
            BuildWorld();
        }

        [TearDown]
        public void TearDown()
        {
            DecisionDeck.Resolved -= OnResolved;
            if (directorGo != null) Object.Destroy(directorGo);
            DecisionDeck.AuthorityCheck = savedCheck;
            StrategySession.Decisions = savedQueue;
        }

        private void OnResolved(PendingDecision d, int choice)
        {
            if (d != null && d == card && choice == 0) resolvedCount++;
        }

        /// <summary>与党（党首=首相1・党員2,3）、太政官の下に大蔵省。大蔵大臣2・大蔵大臣政務官3。</summary>
        private void BuildWorld()
        {
            pol = new PoliticsState();
            var ruling = new Party(1, "民政党", F) { leaderId = Premier };
            ruling.memberIds.AddRange(new[] { Premier, Minister, Secretary });
            pol.parties.Add(ruling);
            pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = Premier, partyId = 1, formedYear = Year, sourceElectionId = "e1",
            };
            tree = new List<Ministry>
            {
                new Ministry(Top, "太政官", OfficeDomain.内政),
                new Ministry(Okura, "大蔵省", OfficeDomain.財政) { parentId = Top },
            };
            roster = new List<Person>();
            for (int i = 1; i <= 3; i++)
                roster.Add(new Person(i, "政治家" + i, F, PersonRole.文民) { isPolitician = true, birthYear = 760 });
            CabinetAppointmentRules.Reconcile(pol, F, tree, Top, roster, Year, Prm);
            Assert.IsTrue(CabinetAppointmentRules.TryAppoint(pol, F, Premier, tree, Top, Okura, CabinetPostKind.大臣, Minister, roster, Year, "試験", Prm).ok);
            Assert.IsTrue(CabinetAppointmentRules.TryAppoint(pol, F, Premier, tree, Top, Okura, CabinetPostKind.政務官, Secretary, roster, Year, "試験", Prm).ok);
        }

        private Person P(int id) => roster.Find(x => x.id == id);

        /// <summary>Game の <see cref="DecisionAuthorityDirector.EvaluateFor"/> と同じ合成（役職なし・文民統制）。</summary>
        private DecisionAuthorityResult Judge(int actorId, string effectKey)
            => CabinetDecisionAuthorityRules.Evaluate(P(actorId), effectKey, new List<Office>(), CivilianControlType.文民統制, null,
                new CabinetDecisionContext(pol, F, tree, Top, roster, Year), id => P(id));

        /// <summary>権限判定のフックに操作者の判定を差し込み、上申を受ける Director を置く。</summary>
        private System.Func<PendingDecision, DecisionAuthorityResult> InstallPlayer(int actorId)
        {
            directorGo = new GameObject("DecisionAuthorityDirector(Test)");
            directorGo.AddComponent<DecisionAuthorityDirector>(); // Awake が自分のフックを差すので、直後に差し替える
            System.Func<PendingDecision, DecisionAuthorityResult> check = d => Judge(actorId, d != null ? d.effectKey : "");
            DecisionDeck.AuthorityCheck = check;
            return check;
        }

        private PendingDecision EnqueueTaxCard()
        {
            card = new PendingDecision(DecisionDeck.NextDecisionId(91000), "増税の建白（試験）", DecisionSeverity.通常,
                DecisionSource.建白結果, "tax.hike", defaultChoiceIndex: 1);
            card.choices.Add("裁可する");
            card.choices.Add("見送る（現状維持）");
            DecisionDeck.Enqueue(card);
            return card;
        }

        [Test]
        public void Secretary_EscalatesToMinister_ApprovalAppliesExactlyOnce()
        {
            System.Func<PendingDecision, DecisionAuthorityResult> check = InstallPlayer(Secretary);
            PendingDecision d = EnqueueTaxCard();

            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0), "政務官は裁可できない");
            Assert.IsTrue(d.escalated, "所管大臣へ上申される");
            Assert.AreEqual(Minister, d.deciderId);
            Assert.AreEqual("政治家2", d.deciderName);
            Assert.IsFalse(DecisionResolutionRules.IsSettled(d));
            Assert.AreEqual(0, resolvedCount, "上申だけでは効果を出さない");
            Assert.AreEqual(check(d).basis, d.authorityBasis, "カードの根拠は見込み表示と同じ判定");

            DecisionAuthorityResult deciderAuth = Judge(Minister, d.effectKey);
            Assert.IsTrue(deciderAuth.CanDecide, deciderAuth.basis);
            Assert.IsTrue(DecisionAuthorityDirector.ConcludeApproval(d, "政治家2", deciderAuth));
            Assert.AreEqual(1, resolvedCount, "大臣の承認で効果は1回");
            Assert.IsTrue(DecisionResolutionRules.IsSettled(d));
            Assert.AreSame(check, DecisionDeck.AuthorityCheck, "確定の後に権限判定のフックが戻る");

            Assert.IsFalse(DecisionAuthorityDirector.ConcludeApproval(d, "政治家2", deciderAuth), "二重の確定はしない");
            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0));
            Assert.AreEqual(1, resolvedCount, "再解決で効果を重ねない");
        }

        [Test]
        public void MinisterDismissedDuringReview_IsSentBack_WithoutEffect()
        {
            System.Func<PendingDecision, DecisionAuthorityResult> check = InstallPlayer(Secretary);
            PendingDecision d = EnqueueTaxCard();
            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0));
            Assert.AreEqual(Minister, d.deciderId);

            // 審査中に大臣が解任された（上申の時点の権限を持ち越さない）
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(pol, F, Premier, Okura, CabinetPostKind.大臣, roster, Year, "試験の解任", Prm).ok);
            DecisionAuthorityResult deciderAuth = Judge(Minister, d.effectKey);
            Assert.IsFalse(deciderAuth.CanDecide);

            Assert.IsFalse(DecisionAuthorityDirector.ConcludeApproval(d, "政治家2", deciderAuth));
            Assert.AreEqual(0, resolvedCount, "古い権限で承認しない＝効果なし");
            Assert.IsTrue(DecisionResolutionRules.IsSettled(d), "差し戻しとして閉じる");
            Assert.IsTrue(d.applied, "誰も後から効果を出さないよう適用の権利を消費");
            Assert.AreEqual(PetitionActionOutcome.対象外, d.outcome);
            StringAssert.Contains("権限", d.resultDetail);
            Assert.AreSame(check, DecisionDeck.AuthorityCheck);

            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0));
            Assert.AreEqual(0, resolvedCount);
        }

        [Test]
        public void MinisterPlayer_DecidesDirectly_ThroughCommonEntry()
        {
            InstallPlayer(Minister);
            PendingDecision d = EnqueueTaxCard();
            Assert.IsTrue(DecisionDeck.Resolve(d.id, 0), "所管大臣は共通入口で裁可できる");
            Assert.IsFalse(d.escalated);
            Assert.AreEqual(1, resolvedCount);
            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0));
            Assert.AreEqual(1, resolvedCount, "効果は1回だけ");
        }
    }
}
