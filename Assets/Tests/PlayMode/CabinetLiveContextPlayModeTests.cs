using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 閣僚の決裁権限を<b>実際の GalaxyView の盤面</b>から通す PlayMode 試験（#2768 #141 #67）。
    /// 無効な GameObject の GalaxyView に固定条件の同盟（共和制・2党・文民政治家30名）を載せ、本番の政府シードと年次の政治 Tick で
    /// 実省庁・人物・<see cref="CabinetState"/> を作る。<see cref="GalaxyView.SwapActiveForQa"/> で観測世界に張り、
    /// 本番の <see cref="DecisionAuthorityDirector"/>（Awake で差す権限フック・見込み表示・上申の審査 Update・承認直前の再判定）を通す。
    /// 権限の結果はテストから渡さない（操作者の人物だけを <see cref="GalaxyView.BindPlayerCharacterForQa"/> で固定）。
    /// Start・実セーブは走らせない。TearDown で static・Registry・Clock・Active・決裁キュー・権限フックを戻す。
    /// </summary>
    public class CabinetLiveContextPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int FirstYear = 797;
        private const int CapitalId = 1, FrontierId = 2;
        private const int PoliticianCount = 30;
        private const int FirstPoliticianId = 11;
        private const string TaxKey = "tax.hike";
        private const Faction F = Faction.同盟;
        private static readonly CabinetParams Prm = CabinetParams.Default;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;
        private GameClock savedClock;
        private DecisionQueue savedDecisions;
        private System.Func<PendingDecision, DecisionAuthorityResult> savedCheck;
        private List<GovernmentRegistry.Appointment> savedAppointments;
        private GalaxyView savedActive;

        private readonly Dictionary<PendingDecision, int> applied = new Dictionary<PendingDecision, int>();

        // 実世界（BuildLiveWorld で作る）
        private GalaxyView view;
        private GameObject directorGo;
        private DecisionAuthorityDirector director;
        private List<Person> civilians;
        private PoliticsState pol;
        private int premierId, okuraId;
        private string okuraName;

        [SetUp]
        public void SetUp()
        {
            savedActive = GalaxyView.Active;
            savedCampaign = StrategySession.Campaign;
            savedMap = StrategySession.Map;
            savedProvinces = StrategySession.Provinces;
            savedClock = StrategySession.Clock;
            savedDecisions = StrategySession.Decisions;
            savedCheck = DecisionDeck.AuthorityCheck;
            savedAppointments = new List<GovernmentRegistry.Appointment>(GovernmentRegistry.Appointments);
            StrategySession.Decisions = new DecisionQueue();
            applied.Clear();
            DecisionDeck.Resolved += OnResolved;
        }

        [TearDown]
        public void TearDown()
        {
            DecisionDeck.Resolved -= OnResolved;
            if (directorGo != null) Object.DestroyImmediate(directorGo); // OnDestroy が自分のフックだけ外す
            directorGo = null;
            director = null;
            DecisionDeck.AuthorityCheck = savedCheck;
            if (view != null) view.BindPlayerCharacterForQa(null);
            view = null;
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            GalaxyView.SwapActiveForQa(savedActive);
            StrategySession.Campaign = savedCampaign;
            StrategySession.Map = savedMap;
            StrategySession.Provinces = savedProvinces;
            StrategySession.Clock = savedClock;
            StrategySession.Decisions = savedDecisions;
            GovernmentRegistry.Clear();
            for (int i = 0; i < savedAppointments.Count; i++)
            {
                GovernmentRegistry.Appointment a = savedAppointments[i];
                GovernmentRegistry.TryAppoint(a.faction, a.office, a.holder, a.scopeKey);
            }
        }

        private void OnResolved(PendingDecision d, int choice)
        {
            if (d == null || choice != 0 || !applied.ContainsKey(d)) return;
            applied[d]++;
        }

        // ===== 実世界の組み立て =====

        /// <summary>同盟の文民政治家30名（ID 11..40・資質は上申の審査で承認に届く値）。</summary>
        private static List<Person> NewCivilians()
        {
            var list = new List<Person>();
            for (int i = 0; i < PoliticianCount; i++)
            {
                int id = FirstPoliticianId + i;
                list.Add(new Person(id, "実世界政治家" + id, F, PersonRole.文民)
                {
                    isPolitician = true, birthYear = 760, charisma = 40 + i, intelligence = 60, operation = 70,
                });
            }
            return list;
        }

        /// <summary>
        /// 本番の経路で内閣まで作る：政府シード（要職・省庁）→ 年次の政治 Tick（総裁選・国政選挙・組閣・党三役）。
        /// その後 Active を張り、本番の DecisionAuthorityDirector を置く（Awake が権限フックを差す）。
        /// </summary>
        private void BuildLiveWorld()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(CapitalId, "実世界首都星", Vector2.zero, F));
            map.AddSystem(new StarSystem(FrontierId, "実世界辺境星", new Vector2(1f, 0f), F));
            var provinces = new Dictionary<int, Province>
            {
                { CapitalId, new Province(CapitalId, "", 1000f) },
                { FrontierId, new Province(FrontierId, "", 600f) },
            };
            civilians = NewCivilians();
            var campaign = new CampaignState(map);
            var alliance = new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            alliance.politics.parties.Add(new Party(1, "実世界民政党", F) { support = 0.6f });
            alliance.politics.parties.Add(new Party(2, "実世界進歩党", F) { support = 0.4f });
            campaign.states.Add(alliance);
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };

            var go = new GameObject("GalaxyView_CabinetLiveQa");
            go.SetActive(false); // Start を走らせない
            spawned.Add(go);
            view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            view.SeedGovernmentForQa();
            Assert.AreEqual(FirstYear, view.ElectionYearForQa);
            view.RunPoliticsTickForQa();

            pol = alliance.politics;
            Assert.IsNotNull(pol.government, "前提：組閣されていない");
            premierId = pol.government.premierPersonId;
            Assert.GreaterOrEqual(premierId, FirstPoliticianId, "前提：首相が決まらない");
            Assert.IsNotNull(pol.cabinet, "前提：内閣の台帳がない");
            Assert.IsNull(CabinetDecisionAuthorityRules.CabinetProblem(view.CabinetDecisionContextOf(F)), "前提：内閣が権限を持たない");

            // 大蔵省＝実省庁ツリーで太政官の直下・所掌が財政の省（id を決め打ちしない）
            okuraId = -1;
            foreach (Ministry m in CabinetAppointmentRules.CabinetMinistries(view.MinistriesOf(F) as IList<Ministry>, view.TopMinistryIdOf(F)))
                if (m.domain == OfficeDomain.財政) { okuraId = m.id; okuraName = m.ministryName; }
            Assert.GreaterOrEqual(okuraId, 0, "前提：財政の省が内閣にない");

            GalaxyView.SwapActiveForQa(view);
            directorGo = new GameObject("DecisionAuthorityDirector(LiveQa)");
            director = directorGo.AddComponent<DecisionAuthorityDirector>();
            Assert.IsNotNull(DecisionDeck.AuthorityCheck, "本番の Director が権限フックを差していない");
        }

        private CabinetPost Post(int ministryId, CabinetPostKind kind)
        {
            CabinetPost p = CabinetAppointmentRules.FindPost(pol.cabinet, ministryId, kind);
            Assert.IsNotNull(p, "前提：" + ministryId + " " + kind + " の職がない");
            Assert.GreaterOrEqual(p.holderId, 0, "前提：" + CabinetAppointmentRules.PostTitle(p) + " が空席（" + p.vacancyReason + "）");
            return p;
        }

        private Person P(int id)
        {
            Person p = view.FindPersonById(id);
            Assert.IsNotNull(p, "人物#" + id + " が実名簿にない");
            return p;
        }

        private int MinisterId => Post(okuraId, CabinetPostKind.大臣).holderId;

        private PendingDecision EnqueueTaxCard(string title)
        {
            var d = new PendingDecision(DecisionDeck.NextDecisionId(91000), title, DecisionSeverity.通常,
                DecisionSource.建白結果, TaxKey, defaultChoiceIndex: 1);
            d.choices.Add("裁可する");
            d.choices.Add("見送る（現状維持）");
            DecisionDeck.Enqueue(d);
            applied[d] = 0;
            return d;
        }

        /// <summary>上申の審査時間を実 Clock で越える（本番の Director.Update が次のフレームで結論を出す）。</summary>
        private void AdvancePastReview()
            => StrategySession.Clock.elapsedSeconds += director.reviewSeconds + 1d;

        private static int CountMessages(long afterSeq, string needle)
        {
            int n = 0;
            List<Notification> list = NotificationCenter.Since(afterSeq);
            for (int i = 0; i < list.Count; i++)
                if (list[i].message.Contains(needle)) n++;
            return n;
        }

        // ===== 試験 =====

        /// <summary>
        /// 実盤面の組立て：CabinetDecisionContextOf の年・陣営・名簿・省が GalaxyView の実データと一致し、
        /// 実在の大蔵大臣は tax キーを裁可、大蔵政務官と他省の大臣は実在の大蔵大臣へ上申、見込み表示も同じ判定、大臣本人は本番フック経由で直接裁可（効果1回）。
        /// </summary>
        [Test]
        public void LiveContext_MatchesGalaxyView_MinisterDecides_OthersEscalateToRealMinister()
        {
            BuildLiveWorld();

            CabinetDecisionContext ctx = view.CabinetDecisionContextOf(F);
            Assert.IsNotNull(ctx);
            Assert.AreSame(pol, ctx.politics, "政治状態が戦役の FactionState と別物");
            Assert.AreEqual(F, ctx.faction);
            Assert.AreSame(view.MinistriesOf(F), ctx.ministries, "省庁ツリーが GalaxyView の実省庁と別物");
            Assert.AreEqual(view.TopMinistryIdOf(F), ctx.topMinistryId);
            Assert.GreaterOrEqual(ctx.topMinistryId, 0);
            CollectionAssert.AreEqual(civilians, ctx.roster, "名簿が GalaxyView の人物（軍人＋文民）と一致しない");
            Assert.AreEqual(FirstYear, ctx.year, "年が統一クロックの暦と一致しない");
            Assert.AreEqual(4, CabinetAppointmentRules.CabinetMinistries(ctx.ministries, ctx.topMinistryId).Count, "太政官の下の4省");

            int minister = MinisterId;
            CabinetPost secretary = Post(okuraId, CabinetPostKind.政務官);
            CabinetPost otherMinister = null;
            foreach (CabinetPost p in pol.cabinet.posts)
                if (p.kind == CabinetPostKind.大臣 && p.ministryId != okuraId && p.holderId >= 0) { otherMinister = p; break; }
            Assert.IsNotNull(otherMinister, "前提：他省の大臣が在任していない");

            DecisionAuthorityResult byMinister = DecisionAuthorityDirector.EvaluateFor(P(minister), TaxKey);
            Assert.IsTrue(byMinister.CanDecide, byMinister.basis);
            StringAssert.Contains(okuraName, byMinister.basis, "根拠に所管省が出ない");

            DecisionAuthorityResult bySecretary = DecisionAuthorityDirector.EvaluateFor(P(secretary.holderId), TaxKey);
            Assert.AreEqual(DecisionAuthority.上申, bySecretary.authority, bySecretary.basis);
            Assert.AreEqual(minister, bySecretary.addresseeId, "政務官の上申先が実在の大蔵大臣でない");
            Assert.AreEqual(P(minister).name, bySecretary.addresseeName);

            DecisionAuthorityResult byOther = DecisionAuthorityDirector.EvaluateFor(P(otherMinister.holderId), TaxKey);
            Assert.AreEqual(DecisionAuthority.上申, byOther.authority, byOther.basis);
            Assert.AreEqual(minister, byOther.addresseeId, "他省の大臣の上申先が実在の大蔵大臣でない");

            // 見込み表示（本番の Director・操作者＝実在の政務官）
            view.BindPlayerCharacterForQa(P(secretary.holderId));
            Assert.IsTrue(DecisionAuthorityDirector.TryPreviewAuthority(TaxKey, out DecisionAuthorityResult preview), "権限判定が差し込まれていない");
            Assert.AreEqual(DecisionAuthority.上申, preview.authority);
            Assert.AreEqual(minister, preview.addresseeId);
            Assert.AreEqual(bySecretary.basis, preview.basis, "見込み表示と裁可時の判定が別");

            // 大臣本人は本番フック経由で直接裁可（効果1回）
            view.BindPlayerCharacterForQa(P(minister));
            PendingDecision d = EnqueueTaxCard("増税の建白（実世界・大臣）");
            Assert.IsTrue(DecisionDeck.Resolve(d.id, 0), "所管大臣が共通入口で裁可できない：" + d.authorityBasis);
            Assert.IsFalse(d.escalated);
            Assert.AreEqual(1, applied[d]);
            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0));
            Assert.AreEqual(1, applied[d], "効果は1回だけ");
        }

        /// <summary>政務官の裁可は実在の大蔵大臣へ上申→本番 Director の Update が実 Clock で審査→承認直前の再判定を通って効果1回。</summary>
        [UnityTest]
        public IEnumerator SecretaryEscalation_ApprovedByLiveDirector_AppliesExactlyOnce()
        {
            BuildLiveWorld();
            System.Func<PendingDecision, DecisionAuthorityResult> hook = DecisionDeck.AuthorityCheck;
            int minister = MinisterId;
            view.BindPlayerCharacterForQa(P(Post(okuraId, CabinetPostKind.政務官).holderId));
            Assert.IsTrue(DecisionAuthorityDirector.TryPreviewAuthority(TaxKey, out DecisionAuthorityResult preview));

            PendingDecision d = EnqueueTaxCard("増税の建白（実世界・政務官）");
            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0), "政務官が裁可できた");
            Assert.IsTrue(d.escalated, "上申されない");
            Assert.AreEqual(minister, d.deciderId);
            Assert.AreEqual(P(minister).name, d.deciderName);
            Assert.AreEqual(preview.basis, d.authorityBasis, "カードの根拠が見込み表示と別");

            yield return null; // 審査時間の前＝結論は出ない
            Assert.IsFalse(DecisionResolutionRules.IsSettled(d));
            Assert.AreEqual(0, applied[d], "上申だけで効果が出た");

            long seq = NotificationCenter.LastSeq;
            AdvancePastReview();
            yield return null;
            Assert.IsTrue(DecisionResolutionRules.IsSettled(d), "本番 Director が審査の結論を出さない");
            Assert.IsFalse(d.escalated);
            Assert.AreEqual(1, applied[d], "大臣の承認で効果は1回");
            Assert.AreEqual(1, CountMessages(seq, "［上申の裁可］"));
            StringAssert.Contains(okuraName, d.authorityBasis, "承認の根拠が所管大臣の権限でない");
            Assert.AreSame(hook, DecisionDeck.AuthorityCheck, "確定の後に本番の権限フックが戻らない");

            AdvancePastReview();
            yield return null;
            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0));
            Assert.AreEqual(1, applied[d], "再解決・次のフレームで効果を重ねた");
        }

        /// <summary>審査中に実在の大蔵大臣が首相に解任された：本番 Director の承認は決裁時点の再判定で効果なしの差し戻しになる。</summary>
        [UnityTest]
        public IEnumerator MinisterDismissedDuringReview_LiveDirectorSendsBack_WithoutEffect()
        {
            BuildLiveWorld();
            int minister = MinisterId;
            view.BindPlayerCharacterForQa(P(Post(okuraId, CabinetPostKind.政務官).holderId));
            PendingDecision d = EnqueueTaxCard("増税の建白（実世界・解任）");
            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0));
            Assert.AreEqual(minister, d.deciderId);
            Assert.GreaterOrEqual(DecisionAuthorityDirector.Favor(P(minister)), PetitionEscalationParams.Default.approveThreshold,
                "前提：審査は承認に届く（却下でなく承認直前の再判定を通す）");

            Assert.IsTrue(CabinetAppointmentRules.Dismiss(pol, F, premierId, okuraId, CabinetPostKind.大臣, civilians, FirstYear, "試験：審査中の解任", Prm).ok);
            Assert.IsFalse(DecisionAuthorityDirector.EvaluateFor(P(minister), TaxKey).CanDecide, "解任後も実盤面で裁可できる");

            long seq = NotificationCenter.LastSeq;
            AdvancePastReview();
            yield return null;
            Assert.IsTrue(DecisionResolutionRules.IsSettled(d), "差し戻しとして閉じない");
            Assert.AreEqual(0, applied[d], "古い権限で承認した＝効果が出た");
            Assert.IsTrue(d.applied, "適用の権利を消費していない");
            Assert.AreEqual(PetitionActionOutcome.対象外, d.outcome);
            StringAssert.Contains("権限", d.resultDetail);
            Assert.AreEqual(1, CountMessages(seq, "［差し戻し］"));
            Assert.AreEqual(0, CountMessages(seq, "［上申の裁可］"));

            yield return null;
            Assert.IsFalse(DecisionDeck.Resolve(d.id, 0));
            Assert.AreEqual(0, applied[d]);
        }

        /// <summary>
        /// 大臣が副大臣へ所管決裁を今年まで委任：委任中は副大臣が本番フックで裁可、実 Clock が翌年へ進むと（年次整理を待たずに）決裁不可となり
        /// 実在の大臣へ上申、本番 Director の承認で効果1回。
        /// </summary>
        [UnityTest]
        public IEnumerator DelegatedVice_LosesAuthority_WhenLiveClockPassesDelegationEnd()
        {
            BuildLiveWorld();
            int minister = MinisterId;
            CabinetPost vice = Post(okuraId, CabinetPostKind.副大臣);
            AppointmentResult del = CabinetAppointmentRules.Delegate(pol, F, minister, okuraId, CabinetDelegation.所管決裁, FirstYear, civilians, FirstYear, Prm);
            Assert.IsTrue(del.ok, del.reason);

            DecisionAuthorityResult during = DecisionAuthorityDirector.EvaluateFor(P(vice.holderId), TaxKey);
            Assert.IsTrue(during.CanDecide, "委任中の副大臣が裁可できない：" + during.basis);
            view.BindPlayerCharacterForQa(P(vice.holderId));
            PendingDecision first = EnqueueTaxCard("増税の建白（実世界・委任中）");
            Assert.IsTrue(DecisionDeck.Resolve(first.id, 0), first.authorityBasis);
            Assert.AreEqual(1, applied[first]);

            // 実 Clock を翌年へ（政治 Tick は回さない＝委任の記録は残ったまま、決裁時点で失効を見る）
            StrategySession.Clock.elapsedSeconds = YearSeconds * 2d + 1d;
            Assert.AreEqual(FirstYear + 1, view.ElectionYearForQa);
            Assert.AreEqual(FirstYear + 1, view.CabinetDecisionContextOf(F).year, "判定の年が実 Clock に追従しない");
            Assert.AreNotEqual(CabinetDelegation.なし, vice.delegation, "前提：年次整理を回していない");

            DecisionAuthorityResult after = DecisionAuthorityDirector.EvaluateFor(P(vice.holderId), TaxKey);
            Assert.IsFalse(after.CanDecide, "委任期限を過ぎても副大臣が裁可できる");
            Assert.AreEqual(DecisionAuthority.上申, after.authority, after.basis);
            Assert.AreEqual(minister, after.addresseeId);
            StringAssert.Contains("期限", after.basis);

            PendingDecision second = EnqueueTaxCard("増税の建白（実世界・委任切れ）");
            Assert.IsFalse(DecisionDeck.Resolve(second.id, 0), "委任切れの副大臣が本番フックを通った");
            Assert.IsTrue(second.escalated);
            Assert.AreEqual(minister, second.deciderId);
            Assert.AreEqual(0, applied[second]);

            AdvancePastReview();
            yield return null;
            Assert.IsTrue(DecisionResolutionRules.IsSettled(second));
            Assert.AreEqual(1, applied[second], "大臣の承認で効果は1回");
            Assert.AreEqual(1, applied[first], "前の案件の効果が重なった");
        }
    }
}
