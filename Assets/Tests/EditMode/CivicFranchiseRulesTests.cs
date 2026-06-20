using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍功市民制（STSH-1 #2353）の EditMode テスト。
    /// 服役→参政権の境界値・フランチャイズ比率→正統性・サポート修正子・就任/投票資格・null 安全を担保。
    /// </summary>
    public class CivicFranchiseRulesTests
    {
        // OfficeRules.CanHold 委譲を検証するための最小 ICharacter スタブ。
        private sealed class StubCharacter : ICharacter
        {
            public int Id { get; set; }
            public string CharacterName { get; set; } = "テスト";
            public Faction Faction { get; set; } = Faction.同盟;
            public int RankTier { get; set; }
            public bool IsMilitary { get; set; }
            public bool IsPolitician { get; set; }
            public int BirthYear { get; set; }
            public bool IsDeceased { get; set; }
            public bool IsAvailable { get; set; } = true;
        }

        // ---- IsEnfranchised：服役の完遂が参政権を授ける ----

        [Test]
        public void Franchise_名誉除隊かつ年数充足は有資格()
        {
            var rec = new ServiceRecord(1, 0, CivicFranchiseRules.MinServiceYears, true);
            Assert.IsTrue(CivicFranchiseRules.IsEnfranchised(rec));
        }

        [Test]
        public void Franchise_未服役は無資格()
        {
            var rec = new ServiceRecord(1, 0, 0, true);
            Assert.IsFalse(CivicFranchiseRules.IsEnfranchised(rec));
        }

        [Test]
        public void Franchise_年数不足は無資格_境界値()
        {
            var rec = new ServiceRecord(1, 0, CivicFranchiseRules.MinServiceYears - 1, true);
            Assert.IsFalse(CivicFranchiseRules.IsEnfranchised(rec));
        }

        [Test]
        public void Franchise_不名誉除隊は無資格()
        {
            var rec = new ServiceRecord(1, 0, CivicFranchiseRules.MinServiceYears + 5, false);
            Assert.IsFalse(CivicFranchiseRules.IsEnfranchised(rec));
        }

        [Test]
        public void Franchise_nullは無資格()
        {
            Assert.IsFalse(CivicFranchiseRules.IsEnfranchised(null));
        }

        // ---- Enfranchise：レコードへ確定を書き戻す ----

        [Test]
        public void Enfranchise_有資格でフラグ立つ()
        {
            var rec = new ServiceRecord(1, 0, CivicFranchiseRules.MinServiceYears, true);
            bool ok = CivicFranchiseRules.Enfranchise(rec);
            Assert.IsTrue(ok);
            Assert.IsTrue(rec.isEnfranchised);
        }

        [Test]
        public void Enfranchise_無資格でフラグ立たない()
        {
            var rec = new ServiceRecord(1, 0, 0, true);
            bool ok = CivicFranchiseRules.Enfranchise(rec);
            Assert.IsFalse(ok);
            Assert.IsFalse(rec.isEnfranchised);
        }

        [Test]
        public void Enfranchise_nullはfalseで例外なし()
        {
            Assert.IsFalse(CivicFranchiseRules.Enfranchise(null));
        }

        // ---- EnfranchisedCount：勢力ごとの有権者数 ----

        [Test]
        public void Count_勢力フィルタと資格で数える()
        {
            var recs = new List<ServiceRecord>
            {
                new ServiceRecord(1, 0, 3, true),   // 有資格・勢力0
                new ServiceRecord(2, 0, 1, true),   // 年数不足・勢力0
                new ServiceRecord(3, 0, 3, false),  // 不名誉・勢力0
                new ServiceRecord(4, 1, 3, true),   // 有資格だが勢力1
                null,                                // null は無視
            };
            Assert.AreEqual(1, CivicFranchiseRules.EnfranchisedCount(recs, 0));
            Assert.AreEqual(1, CivicFranchiseRules.EnfranchisedCount(recs, 1));
        }

        [Test]
        public void Count_nullリストは0()
        {
            Assert.AreEqual(0, CivicFranchiseRules.EnfranchisedCount(null, 0));
        }

        // ---- FranchiseShare：比率（0..1）とゼロ除算回避 ----

        [Test]
        public void Share_比率を返す()
        {
            Assert.AreEqual(0.25f, CivicFranchiseRules.FranchiseShare(25, 100), 1e-4f);
        }

        [Test]
        public void Share_総数0は0_ゼロ除算回避()
        {
            Assert.AreEqual(0f, CivicFranchiseRules.FranchiseShare(10, 0), 1e-6f);
        }

        [Test]
        public void Share_0to1にクランプ()
        {
            Assert.AreEqual(1f, CivicFranchiseRules.FranchiseShare(200, 100), 1e-6f);
            Assert.AreEqual(0f, CivicFranchiseRules.FranchiseShare(-5, 100), 1e-6f);
        }

        [Test]
        public void Share_records版も同値()
        {
            var recs = new List<ServiceRecord>
            {
                new ServiceRecord(1, 0, 3, true),
                new ServiceRecord(2, 0, 3, true),
            };
            Assert.AreEqual(0.5f, CivicFranchiseRules.FranchiseShare(recs, 0, 4), 1e-4f);
        }

        // ---- LegitimacyFromFranchise：比率→正統性寄与 ----

        [Test]
        public void Legitimacy_比率に比例し上限はWeight()
        {
            Assert.AreEqual(0f, CivicFranchiseRules.LegitimacyFromFranchise(0f), 1e-6f);
            Assert.AreEqual(CivicFranchiseRules.LegitimacyWeight,
                CivicFranchiseRules.LegitimacyFromFranchise(1f), 1e-6f);
            Assert.AreEqual(CivicFranchiseRules.LegitimacyWeight * 0.5f,
                CivicFranchiseRules.LegitimacyFromFranchise(0.5f), 1e-6f);
        }

        [Test]
        public void Legitimacy_範囲外入力もクランプ()
        {
            Assert.AreEqual(CivicFranchiseRules.LegitimacyWeight,
                CivicFranchiseRules.LegitimacyFromFranchise(5f), 1e-6f);
            Assert.AreEqual(0f, CivicFranchiseRules.LegitimacyFromFranchise(-1f), 1e-6f);
        }

        // ---- SupportModifier：0.5中心で±に振れる ----

        [Test]
        public void Support_中心0_広い参政で正_狭い参政で負()
        {
            Assert.AreEqual(0f, CivicFranchiseRules.SupportModifier(0.5f), 1e-6f);
            Assert.Greater(CivicFranchiseRules.SupportModifier(1f), 0f);
            Assert.Less(CivicFranchiseRules.SupportModifier(0f), 0f);
        }

        [Test]
        public void Support_振れ幅はSupportSwing準拠()
        {
            Assert.AreEqual(CivicFranchiseRules.SupportSwing * 0.5f,
                CivicFranchiseRules.SupportModifier(1f), 1e-6f);
            Assert.AreEqual(-CivicFranchiseRules.SupportSwing * 0.5f,
                CivicFranchiseRules.SupportModifier(0f), 1e-6f);
        }

        // ---- CanHoldOffice：市民ゲート＋OfficeRules 委譲 ----

        [Test]
        public void Office_市民かつ資格充足で就任可()
        {
            var rec = new ServiceRecord(1, 0, 3, true);
            var c = new StubCharacter { RankTier = 5, IsMilitary = false };
            var o = new Office(1, "総督", OfficeScope.星系, OfficeDomain.内政);
            Assert.IsTrue(CivicFranchiseRules.CanHoldOffice(rec, c, o));
        }

        [Test]
        public void Office_非市民は資格充足でも就任不可()
        {
            var rec = new ServiceRecord(1, 0, 0, true); // 未服役＝非市民
            var c = new StubCharacter { RankTier = 9, IsMilitary = false };
            var o = new Office(1, "総督", OfficeScope.星系, OfficeDomain.内政);
            Assert.IsFalse(CivicFranchiseRules.CanHoldOffice(rec, c, o));
        }

        [Test]
        public void Office_市民でもOfficeRules不適合なら就任不可()
        {
            var rec = new ServiceRecord(1, 0, 3, true);
            var c = new StubCharacter { IsMilitary = true };
            var o = new Office(1, "文官", OfficeScope.国家, OfficeDomain.内政) { civilianOnly = true };
            Assert.IsFalse(CivicFranchiseRules.CanHoldOffice(rec, c, o)); // 委譲先で軍人を拒否
        }

        [Test]
        public void Office_null安全()
        {
            Assert.IsFalse(CivicFranchiseRules.CanHoldOffice(null, null, null));
        }

        // ---- CanVote：投票資格は参政権と一致 ----

        [Test]
        public void Vote_市民は投票可_非市民は不可()
        {
            Assert.IsTrue(CivicFranchiseRules.CanVote(new ServiceRecord(1, 0, 3, true)));
            Assert.IsFalse(CivicFranchiseRules.CanVote(new ServiceRecord(2, 0, 0, true)));
            Assert.IsFalse(CivicFranchiseRules.CanVote(null));
        }
    }
}
