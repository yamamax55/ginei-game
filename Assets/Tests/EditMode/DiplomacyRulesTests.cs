using NUnit.Framework;
using Ginei;
using DS = Ginei.DiplomacyState;
using DP = Ginei.DiplomacyRules.DiplomacyParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 外交（EPIC #189・DIP-1/3 入口）を固定する：外交状態→敵対の写像・opinion 修正子合算とドリフト・
    /// 状態遷移（締結/破棄/宣戦/講和）と信義毀損・同盟提案の可否・FactionRelations 駆動の後方互換。すべて純ロジック。
    /// </summary>
    public class DiplomacyRulesTests
    {
        private const string A = "帝国";
        private const string B = "同盟";

        // ===== 外交状態 → 敵対の写像 =====

        [Test]
        public void HostileOverride_War_True()
            => Assert.IsTrue(DiplomacyRules.HostileOverride(DS.DiplomaticStatus.交戦) == true);

        [Test]
        public void HostileOverride_AllianceNonAggressionVassal_False()
        {
            Assert.IsTrue(DiplomacyRules.HostileOverride(DS.DiplomaticStatus.同盟) == false);
            Assert.IsTrue(DiplomacyRules.HostileOverride(DS.DiplomaticStatus.不可侵) == false);
            Assert.IsTrue(DiplomacyRules.HostileOverride(DS.DiplomaticStatus.属国) == false);
        }

        [Test]
        public void HostileOverride_Peace_Null_FallsBackToBase()
            => Assert.IsFalse(DiplomacyRules.HostileOverride(DS.DiplomaticStatus.平時).HasValue);

        [Test]
        public void IsHostile_NullStateOrNoEntry_ReturnsNull()
        {
            Assert.IsFalse(DiplomacyRules.IsHostile(null, A, B).HasValue);
            var s = new DiplomacyState();
            Assert.IsFalse(DiplomacyRules.IsHostile(s, A, B).HasValue);
        }

        // ===== ペアキーの正規化（無向） =====

        [Test]
        public void Entry_IsUndirected_SameRecordEitherOrder()
        {
            var s = new DiplomacyState();
            DiplomacyRules.AdjustOpinion(s, A, B, 10f);
            Assert.AreEqual(10f, s.Opinion(B, A), 1e-4f); // 逆順でも同じ
            Assert.AreEqual(1, s.entries.Count);
        }

        [Test]
        public void GetEntry_SameOrEmptyName_Null()
        {
            var s = new DiplomacyState();
            Assert.IsNull(s.GetEntry(A, A, create: true));
            Assert.IsNull(s.GetEntry("", B, create: true));
            Assert.AreEqual(0, s.entries.Count);
        }

        // ===== opinion 修正子合算 =====

        [Test]
        public void TargetOpinion_SumsWeightedModifiers()
        {
            var p = DP.Default; // ideology40/trade20/border-15/betrayal-40/marriage20
            var f = new DiplomacyRules.OpinionFactors(
                ideologyAffinity: 0.5f, tradeVolume: 1f, sharedBorder: true, pastBetrayal: 0f, marriageTie: true);
            // 0.5*40 + 1*20 - 15 + 20 = 45
            Assert.AreEqual(45f, DiplomacyRules.TargetOpinion(f, p), 1e-3f);
        }

        [Test]
        public void TargetOpinion_Betrayal_LowersAndClamps()
        {
            var p = DP.Default;
            var f = new DiplomacyRules.OpinionFactors(-1f, 0f, true, 1f, false);
            // -40 -15 -40 = -95（範囲内）
            Assert.AreEqual(-95f, DiplomacyRules.TargetOpinion(f, p), 1e-3f);
        }

        [Test]
        public void AdjustOpinion_ClampsTo100()
        {
            var s = new DiplomacyState();
            DiplomacyRules.AdjustOpinion(s, A, B, 250f);
            Assert.AreEqual(DiplomacyRules.OpinionMax, s.Opinion(A, B), 1e-4f);
        }

        [Test]
        public void DriftOpinion_MovesTowardTarget()
        {
            var s = new DiplomacyState();
            var p = DP.Default; // driftRate 5
            DiplomacyRules.DriftOpinion(s, A, B, 100f, 1f, p);
            Assert.AreEqual(5f, s.Opinion(A, B), 1e-4f);
            DiplomacyRules.DriftOpinion(s, A, B, 100f, 1f, p);
            Assert.AreEqual(10f, s.Opinion(A, B), 1e-4f);
        }

        // ===== 状態遷移 =====

        [Test]
        public void DeclareWar_SetsWarAndDamagesOpinion()
        {
            var s = new DiplomacyState();
            var p = DP.Default;
            Assert.IsTrue(DiplomacyRules.DeclareWar(s, A, B, p));
            Assert.AreEqual(DS.DiplomaticStatus.交戦, s.Status(A, B));
            Assert.AreEqual(-40f, s.Opinion(A, B), 1e-4f);
            Assert.IsFalse(DiplomacyRules.DeclareWar(s, A, B, p)); // 既に交戦
        }

        [Test]
        public void MakePeace_OnlyFromWar()
        {
            var s = new DiplomacyState();
            var p = DP.Default;
            Assert.IsFalse(DiplomacyRules.MakePeace(s, A, B)); // 平時から講和不可
            DiplomacyRules.DeclareWar(s, A, B, p);
            Assert.IsTrue(DiplomacyRules.MakePeace(s, A, B));
            Assert.AreEqual(DS.DiplomaticStatus.平時, s.Status(A, B));
        }

        [Test]
        public void SignTreaty_BlockedDuringWar()
        {
            var s = new DiplomacyState();
            var p = DP.Default;
            DiplomacyRules.DeclareWar(s, A, B, p);
            Assert.IsFalse(DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.同盟)); // 交戦中は締結不可（先に講和）
            Assert.AreEqual(DS.DiplomaticStatus.交戦, s.Status(A, B));
            DiplomacyRules.MakePeace(s, A, B);
            Assert.IsTrue(DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.同盟)); // 講和後は締結可
        }

        [Test]
        public void SignTreaty_RejectsNonTreatyStatus()
        {
            var s = new DiplomacyState();
            Assert.IsFalse(DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.交戦));
            Assert.IsFalse(DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.平時));
        }

        [Test]
        public void BreakTreaty_ReturnsToPeaceAndDamagesTrust()
        {
            var s = new DiplomacyState();
            var p = DP.Default;
            DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.不可侵);
            Assert.IsTrue(DiplomacyRules.BreakTreaty(s, A, B, p));
            Assert.AreEqual(DS.DiplomaticStatus.平時, s.Status(A, B));
            Assert.AreEqual(-30f, s.Opinion(A, B), 1e-4f);
            Assert.IsFalse(DiplomacyRules.BreakTreaty(s, A, B, p)); // 平時は対象外
        }

        // ===== 提案の素 =====

        [Test]
        public void CanProposeAlliance_RequiresThresholdAndNotAtWar()
        {
            var s = new DiplomacyState();
            var p = DP.Default; // ally threshold 50
            Assert.IsFalse(DiplomacyRules.CanProposeAlliance(s, A, B, p));
            DiplomacyRules.AdjustOpinion(s, A, B, 60f);
            Assert.IsTrue(DiplomacyRules.CanProposeAlliance(s, A, B, p));
            DiplomacyRules.DeclareWar(s, A, B, p);
            Assert.IsFalse(DiplomacyRules.CanProposeAlliance(s, A, B, p)); // 交戦中は不可
        }
    }
}
