using NUnit.Framework;
using Ginei;
using Odds = Ginei.PersonnelFateRules.FateOdds;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊壊滅時の軍人の処遇（LIFE-4 #154 拡張）を固定する：roll の帯割り当て（基本は未配属・稀に捕虜/戦死/行方不明）、
    /// 状況（包囲・指揮/士気）からの確率導出、各運命の適用（捕虜=拘留／戦死=死亡プール／行方不明=消息不明）、
    /// 行方不明からの復帰、null/故人の安全性。すべて決定論（roll を渡す）。
    /// </summary>
    public class PersonnelFateRulesTests
    {
        private static Person Mk() => new Person(1, "提督", Faction.同盟, PersonRole.軍人);

        // ===== ResolveFate（帯割り当て） =====

        [Test]
        public void ResolveFate_DefaultBands_PartitionRollRange()
        {
            var o = Odds.Default; // 未配属0.70 / 捕虜0.12 / 戦死0.10 / 行方不明0.08
            Assert.AreEqual(PersonFate.未配属, PersonnelFateRules.ResolveFate(0f, o));
            Assert.AreEqual(PersonFate.未配属, PersonnelFateRules.ResolveFate(0.69f, o));
            Assert.AreEqual(PersonFate.捕虜, PersonnelFateRules.ResolveFate(0.71f, o));   // 0.70..0.82
            Assert.AreEqual(PersonFate.戦死, PersonnelFateRules.ResolveFate(0.85f, o));   // 0.82..0.92
            Assert.AreEqual(PersonFate.行方不明, PersonnelFateRules.ResolveFate(0.95f, o)); // 0.92..1.0
            Assert.AreEqual(PersonFate.行方不明, PersonnelFateRules.ResolveFate(1f, o));    // 末尾は行方不明
        }

        [Test]
        public void ResolveFate_BoundariesAreInclusiveLower()
        {
            var o = new Odds(0.2f, 0.1f, 0.1f); // 未配属0.6
            Assert.AreEqual(PersonFate.未配属, PersonnelFateRules.ResolveFate(0.59f, o));
            Assert.AreEqual(PersonFate.捕虜, PersonnelFateRules.ResolveFate(0.60f, o)); // ちょうど境界は次の帯
            Assert.AreEqual(PersonFate.捕虜, PersonnelFateRules.ResolveFate(0.79f, o));
            Assert.AreEqual(PersonFate.戦死, PersonnelFateRules.ResolveFate(0.80f, o));
            Assert.AreEqual(PersonFate.行方不明, PersonnelFateRules.ResolveFate(0.90f, o));
        }

        [Test]
        public void FateOdds_UnassignedIsRemainder_ClampedNonNegative()
        {
            Assert.AreEqual(0.70f, Odds.Default.Unassigned, 1e-4f);
            // 合計>1 の異常入力でも未配属は0下限
            Assert.AreEqual(0f, new Odds(0.6f, 0.6f, 0.6f).Unassigned, 1e-4f);
        }

        [Test]
        public void ResolveFate_NoSevereOdds_AlwaysUnassigned()
        {
            var o = new Odds(0f, 0f, 0f);
            Assert.AreEqual(PersonFate.未配属, PersonnelFateRules.ResolveFate(0f, o));
            Assert.AreEqual(PersonFate.未配属, PersonnelFateRules.ResolveFate(0.999f, o));
        }

        // ===== OddsFromContext =====

        [Test]
        public void OddsFromContext_EncirclementRaisesSeverity()
        {
            var free = PersonnelFateRules.OddsFromContext(false, 0.5f, 0.5f);
            var trapped = PersonnelFateRules.OddsFromContext(true, 0.5f, 0.5f);
            Assert.Greater(trapped.captured, free.captured);
            Assert.Greater(trapped.killed, free.killed);
            Assert.Less(trapped.Unassigned, free.Unassigned); // 包囲は生還が減る
        }

        [Test]
        public void OddsFromContext_HighCommandAndMoraleEscape()
        {
            // 高指揮・高士気＝逃げ切る＝未配属が広がる（喪失事象が縮む）
            var skilled = PersonnelFateRules.OddsFromContext(false, 1f, 1f);
            var hapless = PersonnelFateRules.OddsFromContext(false, 0f, 0f);
            Assert.Greater(skilled.Unassigned, hapless.Unassigned);
            Assert.Less(skilled.captured, hapless.captured);
        }

        // ===== Apply（各運命の状態遷移） =====

        [Test]
        public void Apply_Unassigned_LeavesPersonFreeAndAvailable()
        {
            var p = Mk();
            Assert.IsTrue(PersonnelFateRules.Apply(p, PersonFate.未配属, Faction.帝国, 800));
            Assert.IsTrue(p.IsAvailable);       // 生還＝再配属可
            Assert.AreEqual(CaptiveStatus.自由, p.captiveStatus);
            Assert.IsFalse(p.IsDeceased);
        }

        [Test]
        public void Apply_Captured_GoesToCustodyOfVictor()
        {
            var p = Mk();
            Assert.IsTrue(PersonnelFateRules.Apply(p, PersonFate.捕虜, Faction.帝国, 800));
            Assert.AreEqual(CaptiveStatus.捕虜, p.captiveStatus);
            Assert.AreEqual(Faction.帝国, p.heldBy);
            Assert.IsFalse(p.IsAvailable);
            Assert.IsFalse(p.IsDeceased); // 生存
        }

        [Test]
        public void Apply_Killed_EntersDeathPool()
        {
            var p = Mk();
            Assert.IsTrue(PersonnelFateRules.Apply(p, PersonFate.戦死, Faction.帝国, 800));
            Assert.IsTrue(p.IsDeceased);     // 戦死＝死亡プール（IsDeceased）
            Assert.AreEqual(800, p.deathYear);
        }

        [Test]
        public void Apply_Missing_SetsMissingStatus_SeatVacatedButRecoverable()
        {
            var p = Mk();
            Assert.IsTrue(PersonnelFateRules.Apply(p, PersonFate.行方不明, Faction.帝国, 800));
            Assert.IsTrue(p.IsMissing);
            Assert.IsFalse(p.IsAvailable); // 席は空く
            Assert.IsFalse(p.IsDeceased);  // 生死不明＝死亡ではない
        }

        [Test]
        public void Apply_DeceasedPerson_IsNoop()
        {
            var p = Mk();
            LifecycleRules.Kill(p, 799);
            Assert.IsFalse(PersonnelFateRules.Apply(p, PersonFate.捕虜, Faction.帝国, 800));
            Assert.AreEqual(CaptiveStatus.自由, p.captiveStatus); // 故人は遷移しない
        }

        [Test]
        public void Apply_NullPerson_IsSafe()
        {
            Assert.DoesNotThrow(() => PersonnelFateRules.Apply(null, PersonFate.戦死, Faction.帝国, 800));
            Assert.IsFalse(PersonnelFateRules.Apply(null, PersonFate.戦死, Faction.帝国, 800));
        }

        // ===== ResolveAndApply / ReturnFromMissing =====

        [Test]
        public void ResolveAndApply_ReturnsFate_AndMutates()
        {
            var p = Mk();
            // roll=1.0 → 行方不明（Default 帯の末尾）
            PersonFate fate = PersonnelFateRules.ResolveAndApply(p, 1f, Odds.Default, Faction.帝国, 800);
            Assert.AreEqual(PersonFate.行方不明, fate);
            Assert.IsTrue(p.IsMissing);
        }

        [Test]
        public void ReturnFromMissing_RestoresFreedom_OnlyFromMissing()
        {
            var p = Mk();
            PersonnelFateRules.Apply(p, PersonFate.行方不明, Faction.帝国, 800);
            Assert.IsTrue(PersonnelFateRules.ReturnFromMissing(p));
            Assert.IsTrue(p.IsAvailable);
            Assert.IsFalse(p.IsMissing);
            // 自由な人物には適用不可
            Assert.IsFalse(PersonnelFateRules.ReturnFromMissing(p));
            Assert.IsFalse(PersonnelFateRules.ReturnFromMissing(null));
        }
    }
}
