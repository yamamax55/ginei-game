using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 稟議トリアージ（優先順位・MEYASU #1296）：決裁待ちの山を 緊急度×起案者の格×箱の格×滞留 で並べる窓口を固定する。
    /// 重みは <see cref="Petition"/> の既存フィールド（origin/box/drafterId/distorted/status/vindicated）から導く＝Petition は不変。
    /// 状態遷移は <see cref="WorkflowRules"/> へ委譲する（ここは並び替え・滞留の評価）。決定論・null/空安全を担保。
    /// </summary>
    public class PetitionTriageRulesTests
    {
        private static Petition New(int id, BoxKind box,
            PetitionOrigin origin = PetitionOrigin.建白, int drafterId = 0)
            => new Petition(id, "案", Faction.同盟, box, origin, "tax.cut")
            {
                drafterId = drafterId,
            };

        // ----- PriorityScore: 重みの順序 -----

        [Test]
        public void PriorityScore_KingBox_OutranksRegionBox()
        {
            var p = PetitionTriageParams.Default;
            float king = PetitionTriageRules.PriorityScore(New(1, BoxKind.国王), 0f, p);
            float region = PetitionTriageRules.PriorityScore(New(2, BoxKind.地方), 0f, p);
            Assert.Greater(king, region);
        }

        [Test]
        public void PriorityScore_SponsoredPetition_OutranksAnonymous()
        {
            var p = PetitionTriageParams.Default;
            float sponsored = PetitionTriageRules.PriorityScore(New(1, BoxKind.政治家, drafterId: 42), 0f, p);
            float anon = PetitionTriageRules.PriorityScore(New(2, BoxKind.政治家, drafterId: 0), 0f, p);
            Assert.Greater(sponsored, anon);
        }

        [Test]
        public void PriorityScore_InquiryOrigin_OutranksProposal()
        {
            var p = PetitionTriageParams.Default;
            float inquiry = PetitionTriageRules.PriorityScore(New(1, BoxKind.政治家, PetitionOrigin.諮問), 0f, p);
            float proposal = PetitionTriageRules.PriorityScore(New(2, BoxKind.政治家, PetitionOrigin.建白), 0f, p);
            Assert.Greater(inquiry, proposal);
        }

        [Test]
        public void PriorityScore_Resurfaced_OutranksPlain()
        {
            var p = PetitionTriageParams.Default;
            var plain = New(1, BoxKind.政治家);
            var resurfaced = New(2, BoxKind.政治家);
            resurfaced.vindicated = true;
            float a = PetitionTriageRules.PriorityScore(resurfaced, 0f, p);
            float b = PetitionTriageRules.PriorityScore(plain, 0f, p);
            Assert.Greater(a, b);
        }

        [Test]
        public void PriorityScore_Distorted_LowersScore()
        {
            var p = PetitionTriageParams.Default;
            var clean = New(1, BoxKind.政治家);
            var dirty = New(2, BoxKind.政治家);
            dirty.distorted = true;
            float a = PetitionTriageRules.PriorityScore(clean, 0f, p);
            float b = PetitionTriageRules.PriorityScore(dirty, 0f, p);
            Assert.Greater(a, b);
        }

        [Test]
        public void PriorityScore_Older_OutranksNewer_BelowStaleDeadline()
        {
            var p = PetitionTriageParams.Default;
            var pet = New(1, BoxKind.政治家);
            // 同一稟議でも滞留が長い方が（陳腐化前は）優先される＝飢餓回避。
            float young = PetitionTriageRules.PriorityScore(pet, 10f, p);
            float old = PetitionTriageRules.PriorityScore(pet, 100f, p);
            Assert.Greater(old, young);
        }

        // ----- 滞留・陳腐化 -----

        [Test]
        public void IsStale_TrueOnlyAfterDeadline()
        {
            var p = PetitionTriageParams.Default; // staleDeadline=120
            Assert.IsFalse(PetitionTriageRules.IsStale(100f, p));
            Assert.IsTrue(PetitionTriageRules.IsStale(120f, p));
            Assert.IsTrue(PetitionTriageRules.IsStale(500f, p));
        }

        [Test]
        public void IsStale_NeverWhenDeadlineNonPositive()
        {
            var p = new PetitionTriageParams(0.5f, 1f, 0.7f, 0.5f, 0.4f, 0.6f, 0.3f, 0.01f,
                staleDeadline: 0f, stalingDecayPerSecond: 0.005f);
            Assert.IsFalse(PetitionTriageRules.IsStale(99999f, p));
        }

        [Test]
        public void StalingDecay_FullBeforeDeadline_DecaysAfter_BoundedAbove005()
        {
            var p = PetitionTriageParams.Default;
            Assert.AreEqual(1f, PetitionTriageRules.StalingDecay(50f, p), 1e-4f);
            Assert.AreEqual(1f, PetitionTriageRules.StalingDecay(120f, p), 1e-4f);
            float mid = PetitionTriageRules.StalingDecay(140f, p); // over=20 -> 1-0.1=0.9
            Assert.Less(mid, 1f);
            Assert.Greater(mid, 0.05f);
            float ancient = PetitionTriageRules.StalingDecay(100000f, p);
            Assert.AreEqual(0.05f, ancient, 1e-4f); // 下限クランプ
        }

        // ----- スコアの境界 -----

        [Test]
        public void PriorityScore_NeverNegative()
        {
            var p = PetitionTriageParams.Default;
            var dirty = New(1, BoxKind.地方);
            dirty.distorted = true;
            float s = PetitionTriageRules.PriorityScore(dirty, -50f, p); // 負の age も 0 扱い
            Assert.GreaterOrEqual(s, 0f);
        }

        [Test]
        public void PriorityScore_NullPetition_IsZero()
        {
            Assert.AreEqual(0f, PetitionTriageRules.PriorityScore(null, 10f), 1e-6f);
        }

        // ----- Order: 決定論・安定・並び -----

        [Test]
        public void Order_PlacesHigherPriorityFirst()
        {
            var backlog = new List<Petition>
            {
                New(1, BoxKind.地方),                       // 低
                New(2, BoxKind.国王, drafterId: 7),         // 高（国王＋起案者）
                New(3, BoxKind.政治家),                     // 中
            };
            var ordered = PetitionTriageRules.Order(backlog);
            Assert.AreEqual(2, ordered[0].id);
            Assert.AreEqual(3, ordered[1].id);
            Assert.AreEqual(1, ordered[2].id);
        }

        [Test]
        public void Order_IsDeterministic_AcrossRepeatedCalls()
        {
            var backlog = new List<Petition>
            {
                New(5, BoxKind.政治家),
                New(2, BoxKind.政治家),
                New(9, BoxKind.政治家),
                New(1, BoxKind.政治家),
            };
            var a = PetitionTriageRules.Order(backlog);
            var b = PetitionTriageRules.Order(backlog);
            CollectionAssert.AreEqual(IdsOf(a), IdsOf(b));
        }

        [Test]
        public void Order_TiesBrokenByIdAscending_WhenEqualScoreAndAge()
        {
            // 同一条件（同箱・同 age）＝同スコア → id 小さい先で安定。
            var backlog = new List<Petition>
            {
                New(30, BoxKind.政治家),
                New(10, BoxKind.政治家),
                New(20, BoxKind.政治家),
            };
            var ordered = PetitionTriageRules.Order(backlog, _ => 5f);
            Assert.AreEqual(10, ordered[0].id);
            Assert.AreEqual(20, ordered[1].id);
            Assert.AreEqual(30, ordered[2].id);
        }

        [Test]
        public void Order_AgeFunc_OlderRisesAbove_SameBaseScore()
        {
            var young = New(1, BoxKind.政治家);
            var old = New(2, BoxKind.政治家);
            var ages = new Dictionary<int, float> { { 1, 5f }, { 2, 200f } };
            // age=200 は陳腐化(120超)するが、ここでは底上げが効く範囲を含むので決定論性のみ確認しつつ、
            // 明確に古い方を強くするため非陳腐化の値で比較する。
            ages[2] = 90f;
            var backlog = new List<Petition> { young, old };
            var ordered = PetitionTriageRules.Order(backlog, pet => ages[pet.id]);
            Assert.AreEqual(2, ordered[0].id); // 古い方が先
        }

        [Test]
        public void Order_SkipsNullElements()
        {
            var backlog = new List<Petition> { null, New(1, BoxKind.政治家), null };
            var ordered = PetitionTriageRules.Order(backlog);
            Assert.AreEqual(1, ordered.Count);
            Assert.AreEqual(1, ordered[0].id);
        }

        [Test]
        public void Order_NullInput_ReturnsEmpty_NotNull()
        {
            var ordered = PetitionTriageRules.Order(null);
            Assert.IsNotNull(ordered);
            Assert.AreEqual(0, ordered.Count);
        }

        [Test]
        public void Order_EmptyInput_ReturnsEmpty()
        {
            var ordered = PetitionTriageRules.Order(new List<Petition>());
            Assert.AreEqual(0, ordered.Count);
        }

        [Test]
        public void Order_DoesNotMutateInput()
        {
            var backlog = new List<Petition>
            {
                New(1, BoxKind.地方),
                New(2, BoxKind.国王),
            };
            int before = backlog.Count;
            var ordered = PetitionTriageRules.Order(backlog);
            Assert.AreEqual(before, backlog.Count);
            Assert.AreEqual(1, backlog[0].id); // 元の順序は不変
            Assert.AreEqual(2, backlog[1].id);
            Assert.AreNotSame(backlog, ordered);
        }

        // ----- Next -----

        [Test]
        public void Next_ReturnsTopOfOrder()
        {
            var backlog = new List<Petition>
            {
                New(1, BoxKind.地方),
                New(2, BoxKind.国王, drafterId: 7),
            };
            var next = PetitionTriageRules.Next(backlog, null);
            Assert.IsNotNull(next);
            Assert.AreEqual(2, next.id);
        }

        [Test]
        public void Next_EmptyOrNull_ReturnsNull()
        {
            Assert.IsNull(PetitionTriageRules.Next(null, null));
            Assert.IsNull(PetitionTriageRules.Next(new List<Petition>(), null));
        }

        private static List<int> IdsOf(List<Petition> list)
        {
            var ids = new List<int>();
            for (int i = 0; i < list.Count; i++) ids.Add(list[i].id);
            return ids;
        }
    }
}
