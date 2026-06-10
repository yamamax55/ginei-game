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

        // ===================================================================
        // ===== 敵対的エッジケース（境界・クランプ・分岐・異常入力・不変条件）追加 =====
        // ===================================================================

        // --- パラメータ ctor の負値クランプ（declareWarHit/breakTreatyHit/driftRate は Max(0,x)） ---

        [Test]
        public void DiplomacyParams_NegativeHitsAndDrift_ClampedToZero()
        {
            // 仕様：declareWarOpinionHit/breakTreatyOpinionHit/opinionDriftRate は Max(0,x) で非負
            var p = new DP(
                allyOpinionThreshold: 50f, warOpinionThreshold: -50f,
                declareWarOpinionHit: -40f, breakTreatyOpinionHit: -30f, opinionDriftRate: -5f,
                ideologyWeight: 40f, tradeWeight: 20f, borderPenalty: 15f, betrayalPenalty: 40f, marriageBonus: 20f);
            Assert.AreEqual(0f, p.declareWarOpinionHit, 1e-5f);
            Assert.AreEqual(0f, p.breakTreatyOpinionHit, 1e-5f);
            Assert.AreEqual(0f, p.opinionDriftRate, 1e-5f);
            // 一方、重み・閾値は素通し（負も許容）
            Assert.AreEqual(-50f, p.warOpinionThreshold, 1e-5f);
        }

        [Test]
        public void DeclareWar_WithZeroClampedHit_DoesNotChangeOpinion()
        {
            // 負の declareWarHit → 0 にクランプされるので opinion は不変（仕様：宣戦で改善しない）
            var s = new DiplomacyState();
            var p = new DP(50f, -50f, -40f, 30f, 5f, 40f, 20f, 15f, 40f, 20f); // declareWarHit=-40→0
            DiplomacyRules.AdjustOpinion(s, A, B, 25f);
            Assert.IsTrue(DiplomacyRules.DeclareWar(s, A, B, p));
            Assert.AreEqual(25f, s.Opinion(A, B), 1e-4f); // 0 引かれる＝据え置き
        }

        // --- OpinionFactors ctor の入力クランプ ---

        [Test]
        public void OpinionFactors_ClampsOutOfRangeInputs()
        {
            var p = DP.Default; // ideology40/trade20/border-15/betrayal-40/marriage20
            // ideology 2→1, trade 5→1, betrayal -3→0（Clamp01）
            var f = new DiplomacyRules.OpinionFactors(
                ideologyAffinity: 2f, tradeVolume: 5f, sharedBorder: false, pastBetrayal: -3f, marriageTie: false);
            // 1*40 + 1*20 - 0 = 60
            Assert.AreEqual(60f, DiplomacyRules.TargetOpinion(f, p), 1e-3f);
        }

        [Test]
        public void OpinionFactors_NegativeIdeologyClampedAtMinusOne()
        {
            var p = DP.Default;
            var f = new DiplomacyRules.OpinionFactors(-99f, 0f, false, 0f, false); // ideology -99→-1
            // -1*40 = -40
            Assert.AreEqual(-40f, DiplomacyRules.TargetOpinion(f, p), 1e-3f);
        }

        // --- TargetOpinion の ±100 クランプ両端 ---

        [Test]
        public void TargetOpinion_ClampsAboveMax()
        {
            // 全項目最大寄与で合算が 100 を超える → 100 に丸める
            var p = new DP(50f, -50f, 40f, 30f, 5f, 80f, 50f, 0f, 0f, 30f);
            var f = new DiplomacyRules.OpinionFactors(1f, 1f, false, 0f, true);
            // 1*80 + 1*50 + 30 = 160 → clamp 100
            Assert.AreEqual(DiplomacyRules.OpinionMax, DiplomacyRules.TargetOpinion(f, p), 1e-3f);
        }

        [Test]
        public void TargetOpinion_ClampsBelowMin()
        {
            var p = new DP(50f, -50f, 40f, 30f, 5f, 80f, 0f, 50f, 80f, 0f);
            var f = new DiplomacyRules.OpinionFactors(-1f, 0f, true, 1f, false);
            // -1*80 - 50 - 80 = -210 → clamp -100
            Assert.AreEqual(DiplomacyRules.OpinionMin, DiplomacyRules.TargetOpinion(f, p), 1e-3f);
        }

        // --- AdjustOpinion の下限クランプ（既存は +100 のみ） ---

        [Test]
        public void AdjustOpinion_ClampsToMinus100()
        {
            var s = new DiplomacyState();
            DiplomacyRules.AdjustOpinion(s, A, B, -250f);
            Assert.AreEqual(DiplomacyRules.OpinionMin, s.Opinion(A, B), 1e-4f);
        }

        [Test]
        public void AdjustOpinion_NullState_NoThrowNoEntry()
        {
            Assert.DoesNotThrow(() => DiplomacyRules.AdjustOpinion(null, A, B, 10f));
        }

        [Test]
        public void AdjustOpinion_SameName_NoEntryCreated()
        {
            var s = new DiplomacyState();
            DiplomacyRules.AdjustOpinion(s, A, A, 10f); // 同名＝GetEntry null
            Assert.AreEqual(0, s.entries.Count);
        }

        // --- DriftOpinion：dt<=0 no-op／オーバーシュート防止／target 側もクランプ ---

        [Test]
        public void DriftOpinion_ZeroOrNegativeDt_NoChange()
        {
            var s = new DiplomacyState();
            var p = DP.Default;
            DiplomacyRules.AdjustOpinion(s, A, B, 30f);
            DiplomacyRules.DriftOpinion(s, A, B, 100f, 0f, p);
            Assert.AreEqual(30f, s.Opinion(A, B), 1e-4f);
            DiplomacyRules.DriftOpinion(s, A, B, 100f, -2f, p);
            Assert.AreEqual(30f, s.Opinion(A, B), 1e-4f);
        }

        [Test]
        public void DriftOpinion_DoesNotOvershootTarget()
        {
            var s = new DiplomacyState();
            var p = DP.Default; // driftRate 5
            DiplomacyRules.AdjustOpinion(s, A, B, 8f);
            // 目標 10、ステップ 5*1=5 だが MoveTowards で 10 を越えない
            DiplomacyRules.DriftOpinion(s, A, B, 10f, 1f, p);
            Assert.AreEqual(10f, s.Opinion(A, B), 1e-4f);
        }

        [Test]
        public void DriftOpinion_TargetBeyondRange_ClampedTo100()
        {
            var s = new DiplomacyState();
            // driftRate 大きく一気に詰める。target 500 は内部で Clamp(100) される
            var p = new DP(50f, -50f, 40f, 30f, 1000f, 40f, 20f, 15f, 40f, 20f);
            DiplomacyRules.DriftOpinion(s, A, B, 500f, 1f, p);
            Assert.AreEqual(DiplomacyRules.OpinionMax, s.Opinion(A, B), 1e-4f);
        }

        [Test]
        public void DriftOpinion_DownwardTowardNegative()
        {
            var s = new DiplomacyState();
            var p = DP.Default; // driftRate 5
            DiplomacyRules.AdjustOpinion(s, A, B, 0f); // 既定 0
            DiplomacyRules.DriftOpinion(s, A, B, -100f, 1f, p);
            Assert.AreEqual(-5f, s.Opinion(A, B), 1e-4f);
        }

        // --- IsHostile：各条約状態の写像をステート経由で ---

        [Test]
        public void IsHostile_TreatyStatuses_MapCorrectlyViaState()
        {
            var s = new DiplomacyState();
            DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.同盟);
            Assert.IsTrue(DiplomacyRules.IsHostile(s, A, B) == false); // 同盟＝非敵対

            var s2 = new DiplomacyState();
            DiplomacyRules.DeclareWar(s2, A, B, DP.Default);
            Assert.IsTrue(DiplomacyRules.IsHostile(s2, A, B) == true); // 交戦＝敵対

            // 無向：逆順でも同じ
            Assert.IsTrue(DiplomacyRules.IsHostile(s2, B, A) == true);
        }

        // --- SignTreaty：条約→別条約の切替（交戦でなければ可） ---

        [Test]
        public void SignTreaty_SwitchBetweenTreaties_Allowed()
        {
            var s = new DiplomacyState();
            Assert.IsTrue(DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.不可侵));
            Assert.IsTrue(DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.同盟)); // 不可侵→同盟へ
            Assert.AreEqual(DS.DiplomaticStatus.同盟, s.Status(A, B));
        }

        [Test]
        public void SignTreaty_NullStateOrSameName_False()
        {
            Assert.IsFalse(DiplomacyRules.SignTreaty(null, A, B, DS.DiplomaticStatus.同盟));
            var s = new DiplomacyState();
            Assert.IsFalse(DiplomacyRules.SignTreaty(s, A, A, DS.DiplomaticStatus.同盟)); // 同名＝entry null
            Assert.AreEqual(0, s.entries.Count);
        }

        // --- BreakTreaty：属国/同盟からも解消・下限クランプ ---

        [Test]
        public void BreakTreaty_FromVassal_ReturnsToPeace()
        {
            var s = new DiplomacyState();
            var p = DP.Default;
            DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.属国);
            Assert.IsTrue(DiplomacyRules.BreakTreaty(s, A, B, p));
            Assert.AreEqual(DS.DiplomaticStatus.平時, s.Status(A, B));
        }

        [Test]
        public void BreakTreaty_OpinionClampsAtMin()
        {
            var s = new DiplomacyState();
            var p = DP.Default; // breakHit 30
            DiplomacyRules.AdjustOpinion(s, A, B, -90f);
            DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.同盟);
            Assert.IsTrue(DiplomacyRules.BreakTreaty(s, A, B, p));
            // -90 - 30 = -120 → clamp -100
            Assert.AreEqual(DiplomacyRules.OpinionMin, s.Opinion(A, B), 1e-4f);
        }

        [Test]
        public void BreakTreaty_DuringWar_False()
        {
            var s = new DiplomacyState();
            var p = DP.Default;
            DiplomacyRules.DeclareWar(s, A, B, p);
            Assert.IsFalse(DiplomacyRules.BreakTreaty(s, A, B, p)); // 交戦中は破棄対象外
            Assert.AreEqual(DS.DiplomaticStatus.交戦, s.Status(A, B));
        }

        // --- MakePeace：非交戦では create せず entry を作らない ---

        [Test]
        public void MakePeace_NonWarTreaty_FalseAndNoSideEffect()
        {
            var s = new DiplomacyState();
            DiplomacyRules.SignTreaty(s, A, B, DS.DiplomaticStatus.同盟);
            Assert.IsFalse(DiplomacyRules.MakePeace(s, A, B)); // 同盟から講和は不可
            Assert.AreEqual(DS.DiplomaticStatus.同盟, s.Status(A, B)); // 状態は不変
        }

        [Test]
        public void MakePeace_NoEntry_DoesNotCreateEntry()
        {
            var s = new DiplomacyState();
            Assert.IsFalse(DiplomacyRules.MakePeace(s, A, B));
            Assert.AreEqual(0, s.entries.Count); // create:false なので増えない
        }

        // --- DeclareWar：opinion 下限クランプ ---

        [Test]
        public void DeclareWar_OpinionClampsAtMin()
        {
            var s = new DiplomacyState();
            var p = DP.Default; // hit 40
            DiplomacyRules.AdjustOpinion(s, A, B, -80f);
            Assert.IsTrue(DiplomacyRules.DeclareWar(s, A, B, p));
            // -80 - 40 = -120 → clamp -100
            Assert.AreEqual(DiplomacyRules.OpinionMin, s.Opinion(A, B), 1e-4f);
        }

        // --- CanProposeAlliance：閾値ちょうど（>= の境界）・null state ---

        [Test]
        public void CanProposeAlliance_ExactThreshold_True()
        {
            var s = new DiplomacyState();
            var p = DP.Default; // threshold 50
            DiplomacyRules.AdjustOpinion(s, A, B, 50f); // ちょうど 50
            Assert.IsTrue(DiplomacyRules.CanProposeAlliance(s, A, B, p)); // >= なので可
        }

        [Test]
        public void CanProposeAlliance_JustBelowThreshold_False()
        {
            var s = new DiplomacyState();
            var p = DP.Default;
            DiplomacyRules.AdjustOpinion(s, A, B, 49.999f);
            Assert.IsFalse(DiplomacyRules.CanProposeAlliance(s, A, B, p));
        }

        [Test]
        public void CanProposeAlliance_NullState_False()
        {
            Assert.IsFalse(DiplomacyRules.CanProposeAlliance(null, A, B, DP.Default));
        }

        // --- Clamp 静的ヘルパの両端 ---

        [Test]
        public void Clamp_BothEnds()
        {
            Assert.AreEqual(DiplomacyRules.OpinionMax, DiplomacyRules.Clamp(123f), 1e-5f);
            Assert.AreEqual(DiplomacyRules.OpinionMin, DiplomacyRules.Clamp(-123f), 1e-5f);
            Assert.AreEqual(0f, DiplomacyRules.Clamp(0f), 1e-5f);
        }
    }
}
