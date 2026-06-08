using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// AdmiralData の実効能力（参謀補完・基準値非破壊＝実効値パターン）と
    /// 得意陣形判定の現状動作を固定する特性テスト。
    /// </summary>
    public class AdmiralDataTests
    {
        private AdmiralData MakeAdmiral(int attack)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.attack = attack;
            a.staffBonusRatio = 0.2f;
            a.staffOfficers = new AdmiralData[0];
            return a;
        }

        [Test]
        public void EffectiveAttack_NoStaff_EqualsBase()
        {
            var a = MakeAdmiral(80);
            Assert.AreEqual(80, a.EffectiveAttack);
            Assert.IsFalse(a.HasStaff);
        }

        [Test]
        public void EffectiveAttack_WithStaff_AddsBestTimesRatio()
        {
            var a = MakeAdmiral(50);
            var staff = MakeAdmiral(100);
            a.staffOfficers = new[] { staff };
            // 50 + round(100 * 0.2) = 70。
            Assert.AreEqual(70, a.EffectiveAttack);
            // 基準フィールドは非破壊。
            Assert.AreEqual(50, a.attack);
            Assert.IsTrue(a.HasStaff);
        }

        [Test]
        public void EffectiveAttack_ClampedToMaxStatValue()
        {
            var a = MakeAdmiral(95);
            var staff = MakeAdmiral(100);
            a.staffOfficers = new[] { staff };
            // 95 + 20 = 115 → 上限 100 にクランプ。
            Assert.AreEqual(AdmiralData.MaxStatValue, a.EffectiveAttack);
        }

        [Test]
        public void ComputeEffective_TakesBestStaffNotSum()
        {
            var a = MakeAdmiral(50);
            a.staffOfficers = new[] { MakeAdmiral(60), MakeAdmiral(100), MakeAdmiral(70) };
            // 最高値 100 のみ採用：50 + 20 = 70（合計ではない）。
            Assert.AreEqual(70, a.EffectiveAttack);
        }

        [Test]
        public void Staff_NullAndSelfReference_AreIgnored()
        {
            var a = MakeAdmiral(50);
            a.staffOfficers = new AdmiralData[] { null, a };
            Assert.IsFalse(a.HasStaff);
            Assert.AreEqual(50, a.EffectiveAttack);
        }

        [Test]
        public void GetStaffNames_JoinsWithSeparator_RespectsMaxStaff()
        {
            var a = MakeAdmiral(50);
            var s1 = MakeAdmiral(60); s1.admiralName = "参謀1";
            var s2 = MakeAdmiral(60); s2.admiralName = "参謀2";
            var s3 = MakeAdmiral(60); s3.admiralName = "参謀3";
            var s4 = MakeAdmiral(60); s4.admiralName = "参謀4";
            a.staffOfficers = new[] { s1, s2, s3, s4 };
            // MaxStaff=3 まで、「、」区切り。
            Assert.AreEqual("参謀1、参謀2、参謀3", a.GetStaffNames());
        }

        [Test]
        public void IsPreferredFormation_DisabledByDefault()
        {
            var a = MakeAdmiral(80);
            a.hasPreferredFormation = false;
            a.preferredFormation = Formation.鶴翼陣;
            Assert.IsFalse(a.IsPreferredFormation(Formation.鶴翼陣));
        }

        [Test]
        public void IsPreferredFormation_EnabledAndMatching_IsTrue()
        {
            var a = MakeAdmiral(80);
            a.hasPreferredFormation = true;
            a.preferredFormation = Formation.鶴翼陣;
            Assert.IsTrue(a.IsPreferredFormation(Formation.鶴翼陣));
            Assert.IsFalse(a.IsPreferredFormation(Formation.円陣));
        }
    }
}
