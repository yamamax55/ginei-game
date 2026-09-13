#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 再現可能な会戦QA（固定会戦）を<b>実コンポーネント・実命令経路</b>で確かめる。
    ///
    /// <list type="bullet">
    ///   <item>準備：一時停止のまま明細どおりの初期値になり、PauseManager と timeScale が一致する。</item>
    ///   <item>2回初期化で主要初期値が一致し、seed の違いは区別される。</item>
    ///   <item>退却・不退転・陣形変更：生存・実被弾・変位などの<b>実結果</b>をセッションの判定と盤面の両方で確かめる。
    ///         前提が崩れた（未判定）ときは合格にせず Inconclusive にする。</item>
    ///   <item>準備失敗・準備中断・再試行・終了・シーン破棄で、timeScale・Random.state・台帳設定・シーン・艦隊が漏れない。</item>
    /// </list>
    /// ★このQAは会戦イベントを隔離しているので、自然発火の確認にはならない。
    /// </summary>
    public class ReproducibleBattleQaPlayModeTests
    {
        /// <summary>準備完了を待つ上限（実時間秒）。準備中は timeScale=0 なので実時間で数える。</summary>
        private const float PrepareTimeoutReal = 10f;

        /// <summary>観測完了を待つ上限（実時間秒）。不退転は約 17 ゲーム秒かかる。</summary>
        private const float ObserveTimeoutReal = 60f;

        private float savedTimeScale;
        private bool savedAuditEnabled;
        private float savedAuditMinAbsDelta;
        private readonly List<GameObject> strays = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            savedAuditEnabled = MoraleAuditLog.Enabled;
            savedAuditMinAbsDelta = MoraleAuditLog.MinAbsDelta;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ReproducibleBattleQaSession.Active != null) ReproducibleBattleQaSession.Active.End("試験の後始末");
            for (int i = 0; i < strays.Count; i++) if (strays[i] != null) Object.DestroyImmediate(strays[i]);
            strays.Clear();
            yield return WaitRealFrames(3);   // シーンのアンロード待ち
            Time.timeScale = savedTimeScale;
            MoraleAuditLog.Enabled = savedAuditEnabled;
            MoraleAuditLog.MinAbsDelta = savedAuditMinAbsDelta;
        }

        // ===== 補助 =====

        private static IEnumerator WaitRealFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private static IEnumerator WaitPrepared(ReproducibleBattleQaSession s)
        {
            float until = Time.realtimeSinceStartup + PrepareTimeoutReal;
            while (s != null && s.CurrentPhase == ReproducibleBattleQaSession.Phase.準備中 && Time.realtimeSinceStartup < until)
                yield return null;
        }

        private static IEnumerator WaitObserved(ReproducibleBattleQaSession s)
        {
            float until = Time.realtimeSinceStartup + ObserveTimeoutReal;
            while (s != null && s.CurrentPhase == ReproducibleBattleQaSession.Phase.実行中 && Time.realtimeSinceStartup < until)
                yield return null;
        }

        private static string RandomStateJson() => JsonUtility.ToJson(Random.state);

        private static bool AnyQaSceneLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded && sc.name.StartsWith(ReproducibleBattleQaSession.SceneNamePrefix)) return true;
            }
            return false;
        }

        private static void AssertReady(ReproducibleBattleQaSession s)
        {
            Assert.IsNotNull(s, "セッションが作られていない");
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備完了, s.CurrentPhase, "準備が完了していない：" + s.FailureReason +
                "\n" + (s.Log != null ? s.Log.Dump("") : ""));
        }

        /// <summary>判定名の接頭辞で引き、全件が合格であること。未判定は Inconclusive（合格にしない）。</summary>
        private static void AssertChecks(BattleQaRunLog log, string namePrefix, int expectedAtLeast)
        {
            int found = 0;
            for (int i = 0; i < log.Checks.Count; i++)
            {
                BattleQaCheck c = log.Checks[i];
                if (!c.name.StartsWith(namePrefix)) continue;
                found++;
                if (c.verdict == BattleQaVerdict.未判定)
                    Assert.Inconclusive("前提が崩れて判定できない：" + c.name + "：" + c.detail + "\n" + log.Dump(""));
                Assert.AreEqual(BattleQaVerdict.合格, c.verdict, c.name + "：" + c.detail + "\n" + log.Dump(""));
            }
            Assert.GreaterOrEqual(found, expectedAtLeast, "判定「" + namePrefix + "」が足りない（観測が走っていない）\n" + log.Dump(""));
        }

        // ===== 準備 =====

        [UnityTest]
        public IEnumerator Prepare_StaysPausedUntilStart_AndMatchesPreset()
        {
            BattleQaPreset p = BattleQaPresetCatalog.Retreat(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s);
            AssertReady(s);

            Assert.AreEqual(0f, Time.timeScale, "準備完了なのに時間が流れている");
            Assert.IsNotNull(s.PauseCtl, "PauseManager が無い");
            Assert.IsTrue(s.PauseCtl.IsPaused, "PauseManager が一時停止になっていない");
            Assert.IsTrue(s.IsPauseConsistent, "PauseManager と timeScale が食い違う");
            Assert.AreEqual(p.Fleets.Count, FleetRegistry.AllFlagships.Count, "実艦隊が明細の数だけ登録されていない");
            CollectionAssert.IsEmpty(BattleQaSnapshot.CompareWithPreset(p, s.InitialSnapshot, BattleQaTolerance.Default));
            Assert.IsTrue(MoraleAuditLog.Enabled, "士気の原因台帳が有効になっていない");

            // 停止中は何フレーム経っても初期値が動かない（命令もAIも出していない）。
            yield return WaitRealFrames(20);
            CollectionAssert.IsEmpty(BattleQaSnapshot.Compare(s.InitialSnapshot, s.CaptureSnapshot(), BattleQaTolerance.Default),
                "開始前に初期値が動いた");
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備完了, s.CurrentPhase);
        }

        [UnityTest]
        public IEnumerator SamePresetTwice_InitialValuesMatch_DifferentSeedIsDistinguished()
        {
            foreach (BattleQaPresetKind kind in new[] { BattleQaPresetKind.退却, BattleQaPresetKind.不退転, BattleQaPresetKind.陣形変更 })
            {
                ReproducibleBattleQaSession s1 = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.Create(kind, BattleQaPresetCatalog.DefaultSeed));
                yield return WaitPrepared(s1);
                AssertReady(s1);
                BattleQaSnapshot first = s1.InitialSnapshot;
                string firstRandom = RandomStateJson();
                string firstRunId = s1.Log.runId;
                s1.End("1回目");
                yield return WaitRealFrames(3);

                ReproducibleBattleQaSession s2 = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.Create(kind, BattleQaPresetCatalog.DefaultSeed));
                yield return WaitPrepared(s2);
                AssertReady(s2);
                CollectionAssert.IsEmpty(BattleQaSnapshot.Compare(first, s2.InitialSnapshot, BattleQaTolerance.Default),
                    kind + "：2回の初期化で主要初期値が違う");
                Assert.AreEqual(firstRandom, RandomStateJson(), kind + "：同じ seed なのに準備完了時の Random.state が違う");
                Assert.AreNotEqual(firstRunId, s2.Log.runId, "run_id が実行ごとに一意でない");
                s2.End("2回目");
                yield return WaitRealFrames(3);

                ReproducibleBattleQaSession s3 = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.Create(kind, BattleQaPresetCatalog.AlternateSeed));
                yield return WaitPrepared(s3);
                AssertReady(s3);
                List<string> diff = BattleQaSnapshot.Compare(first, s3.InitialSnapshot, BattleQaTolerance.Default);
                Assert.AreEqual(1, diff.Count, kind + "：seed 以外の初期値まで変わった／seed を区別していない：" + string.Join("／", diff));
                StringAssert.StartsWith("seed", diff[0]);
                Assert.AreNotEqual(firstRandom, RandomStateJson(), kind + "：seed が違うのに Random.state が同じ");
                s3.End("別 seed");
                yield return WaitRealFrames(3);
            }
        }

        // ===== 退却 =====

        [UnityTest]
        public IEnumerator Retreat_CorpsRetreatMovesEveryMember_AndSparesBystanders()
        {
            BattleQaPreset p = BattleQaPresetCatalog.Retreat(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s);
            AssertReady(s);
            Assert.IsTrue(s.StartRun());
            Assert.IsTrue(s.IsPauseConsistent, "開始後に PauseManager と timeScale が食い違う");
            Assert.Greater(Time.timeScale, 0f, "開始したのに停止のまま");

            yield return WaitObserved(s);
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.観測完了, s.CurrentPhase, "観測が終わらない\n" + s.Log.Dump(""));
            Assert.IsTrue(s.IsPauseConsistent && Time.timeScale == 0f, "観測完了で一時停止していない");

            AssertChecks(s.Log, "軍団総退却の発令", 1);
            AssertChecks(s.Log, "配下の退却", 3);
            AssertChecks(s.Log, "所属外の非巻き込み", 3);

            // セッションの判定とは別に、盤面の実コンポーネントでも確かめる（判定の実装だけを信じない）。
            Vector2 enemyStart = p.Fleets[p.Fleets.Count - 1].position;
            for (int i = 0; i < p.Fleets.Count; i++)
            {
                BattleQaFleetSpec spec = p.Fleets[i];
                if (spec.role == BattleQaCommandRole.敵) continue;
                FleetStrength f = s.Fleet(spec.fleetId);
                Assert.IsNotNull(f, "艦隊" + spec.fleetId + " が消えた");
                FleetAI ai = f.GetComponent<FleetAI>();
                bool ordered = BattlefieldCommandManager.IsCorpsRetreatOrdered(CorpsFormation.KeyFor(f));
                if (spec.corpsName == BattleQaPresetCatalog.RetreatCorps)
                {
                    Assert.IsTrue(ordered, "艦隊" + spec.fleetId + "：軍団の総退却が発令中でない");
                    Assert.AreEqual(FleetAI.AIState.撤退, ai.currentState, "艦隊" + spec.fleetId + "：撤退状態でない");
                    Vector2 dir = BattleQaJudgeRules.RetreatDirection(spec.position, enemyStart, Vector2.down);
                    float disp = BattleQaJudgeRules.DisplacementAlong(spec.position, f.transform.position, dir);
                    Assert.GreaterOrEqual(disp, BattleQaJudgeRules.MinRetreatDisplacement, "艦隊" + spec.fleetId + "：退却方向へ実際に動いていない");
                }
                else
                {
                    Assert.IsFalse(ordered, "艦隊" + spec.fleetId + "：所属外の軍団まで総退却になった");
                    Assert.AreNotEqual(FleetAI.AIState.撤退, ai.currentState, "艦隊" + spec.fleetId + "：所属外が撤退に巻き込まれた");
                }
            }
        }

        // ===== 不退転 =====

        [UnityTest]
        public IEnumerator MoraleLock_ActiveEndAfter_DistinguishesContinuedAndStoppedFire()
        {
            BattleQaPreset p = BattleQaPresetCatalog.MoraleLock(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s);
            AssertReady(s);
            Assert.AreEqual(BattleQaAiMode.AI停止, p.AiMode, "この試験は AI 停止で行う（AI の特殊指揮を混ぜない）");
            Assert.IsTrue(s.StartRun());
            Assert.IsTrue(s.Fleet(21).activeMoraleLock && s.Fleet(22).activeMoraleLock, "不退転が実経路で乗っていない");

            yield return WaitObserved(s);
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.観測完了, s.CurrentPhase, "観測が終わらない\n" + s.Log.Dump(""));

            AssertChecks(s.Log, "効果中は敗走しない", 2);
            AssertChecks(s.Log, "効果中の実被弾", 2);
            AssertChecks(s.Log, "終了後の被弾継続", 1);
            AssertChecks(s.Log, "終了後の被弾停止", 1);
            AssertChecks(s.Log, "終了後の通常敗走の再開", 1);

            // 盤面：効果は切れ、両艦隊は生存（効果終了前に失われていない）。
            Assert.IsFalse(s.Fleet(21).activeMoraleLock);
            Assert.IsFalse(s.Fleet(22).activeMoraleLock);
            Assert.IsTrue(s.Fleet(21).IsAlive && s.Fleet(22).IsAlive, "標的が失われた");
            Assert.Less(s.Fleet(21).strength, p.Fleets[0].shipCount, "被弾継続組の艦艇数が減っていない（実被弾していない）");
        }

        // ===== 陣形変更 =====

        [UnityTest]
        public IEnumerator Formation_HoldBeatsCorpsAi_PriorityHolds_ReleaseReturnsToCorpsAi()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s);
            AssertReady(s);
            Assert.IsTrue(s.StartRun());

            yield return WaitObserved(s);
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.観測完了, s.CurrentPhase, "観測が終わらない\n" + s.Log.Dump(""));

            AssertChecks(s.Log, "直接命令の受理", 1);
            AssertChecks(s.Log, "保持が軍団AIの周期を跨ぐ", 1);
            AssertChecks(s.Log, "命令優先順位", 1);
            AssertChecks(s.Log, "保持解除後は軍団AIの陣形へ戻る", 1);
            AssertChecks(s.Log, "軍団所属と軍団旗艦の保持", 1);

            // 盤面：所属と軍団旗艦は明細どおり、隷下Aは保持なし・軍団AIの陣形。
            for (int i = 0; i < p.Fleets.Count; i++)
            {
                FleetStrength f = s.Fleet(p.Fleets[i].fleetId);
                Assert.AreEqual(p.Fleets[i].corpsName, f.corpsName);
                Assert.AreEqual(p.Fleets[i].role == BattleQaCommandRole.軍団長, f.IsCorpsFlagship);
            }
            Squadron held = s.Fleet(2).GetComponent<Squadron>();
            Squadron control = s.Fleet(3).GetComponent<Squadron>();
            Assert.IsFalse(held.IsFormationHeld);
            Assert.AreEqual(FormationOrderSource.軍団AI, held.LastFormationSource);
            Assert.AreEqual(control.currentFormation, held.currentFormation);
        }

        // ===== 状態が漏れない =====

        [UnityTest]
        public IEnumerator PrepareFailure_ForeignFleet_RestoresAndLeavesNothing()
        {
            // 通常会戦の艦隊が居る状況（混ざるので準備してはいけない）。
            var stray = new GameObject("QA外の艦隊");
            strays.Add(stray);
            stray.SetActive(false);   // 全部品が揃ってから Awake（既存試験と同じ組み方）
            stray.AddComponent<FleetStrength>();
            stray.AddComponent<FleetMorale>();
            stray.AddComponent<FleetMovement>();
            stray.AddComponent<WeaponArc>();
            stray.AddComponent<FleetWeapon>();
            stray.AddComponent<Squadron>().escortCount = 0;
            stray.SetActive(true);
            yield return null;   // Start でレジストリ登録
            Assert.Greater(FleetRegistry.AllFlagships.Count, 0, "前提：既存艦隊が登録されていること");

            Time.timeScale = 1.5f;
            Random.InitState(424242);
            string randomBefore = RandomStateJson();
            MoraleAuditLog.Enabled = false;

            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.Retreat(1));
            yield return WaitPrepared(s);

            // ★破棄済みでも C# オブジェクトとしては読める（Unity の == null ではなく参照で判定する）。
            Assert.IsNotNull((object)s, "セッションが作られていない");
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備失敗, s.CurrentPhase);
            StringAssert.Contains("既存の艦隊", s.FailureReason);
            StringAssert.Contains("準備失敗", ReproducibleBattleQaSession.LastLog.Dump(""));
            StringAssert.Contains("既存の艦隊", ReproducibleBattleQaSession.LastLog.Dump(""));
            Assert.IsNull(ReproducibleBattleQaSession.Active, "失敗したセッションが残っている");
            Assert.AreEqual(1.5f, Time.timeScale, 1e-4f, "timeScale が戻っていない");
            Assert.AreEqual(randomBefore, RandomStateJson(), "Random.state が戻っていない");
            Assert.IsFalse(MoraleAuditLog.Enabled, "台帳の有効状態が戻っていない");
            Assert.AreEqual(1, FleetRegistry.AllFlagships.Count, "QAの艦隊が残った");

            yield return WaitRealFrames(3);
            Assert.IsFalse(AnyQaSceneLoaded(), "使い捨てシーンが残った");
        }

        [UnityTest]
        public IEnumerator AbortDuringPreparation_RestoresState()
        {
            Time.timeScale = 1f;
            Random.InitState(777);
            string randomBefore = RandomStateJson();
            MoraleAuditLog.Enabled = false;

            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.MoraleLock(1));
            Assert.IsNotNull(s);
            // Begin 直後（最初の yield まで進んだ＝シード初期化・台帳有効化・PauseManager 生成済み）に中断する。
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備中, s.CurrentPhase);
            Assert.IsTrue(MoraleAuditLog.Enabled, "前提：準備で台帳が有効になっていること");
            s.End("準備中断");

            Assert.IsNull(ReproducibleBattleQaSession.Active);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f);
            Assert.AreEqual(randomBefore, RandomStateJson(), "中断で Random.state が戻っていない");
            Assert.IsFalse(MoraleAuditLog.Enabled);
            Assert.AreEqual(0, FleetRegistry.AllFlagships.Count);
            StringAssert.Contains("隔離の解除", ReproducibleBattleQaSession.LastRestoreReport);
            yield return WaitRealFrames(3);
            Assert.IsFalse(AnyQaSceneLoaded());
        }

        [UnityTest]
        public IEnumerator Retry_RebuildsSameInitialState_WithoutLeakingPreviousRun()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s1 = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s1);
            AssertReady(s1);
            BattleQaSnapshot first = s1.InitialSnapshot;
            string firstRandom = RandomStateJson();
            string firstRunId = s1.Log.runId;
            FleetStrength oldFleet = s1.Fleet(2);

            Assert.IsTrue(s1.StartRun());
            float until = Time.time + 1.5f;
            while (Time.time < until) yield return null;
            Assert.IsTrue(oldFleet.GetComponent<Squadron>().IsFormationHeld, "前提：1回目で状態が動いていること（保持あり）");

            ReproducibleBattleQaSession s2 = ReproducibleBattleQaSession.Retry();
            Assert.IsTrue(oldFleet == null, "前の実行の艦隊が残っている");
            yield return WaitPrepared(s2);
            AssertReady(s2);

            Assert.AreNotSame(s1, s2);
            Assert.AreNotEqual(firstRunId, s2.Log.runId);
            Assert.AreEqual(p.Fleets.Count, FleetRegistry.AllFlagships.Count, "前の実行の艦隊が混ざった");
            CollectionAssert.IsEmpty(BattleQaSnapshot.Compare(first, s2.InitialSnapshot, BattleQaTolerance.Default), "再試行で初期値が変わった");
            Assert.AreEqual(firstRandom, RandomStateJson(), "再試行で Random.state が1回目の準備完了時と違う");
            Assert.IsFalse(s2.Fleet(2).GetComponent<Squadron>().IsFormationHeld, "前の実行の保持が漏れた");
            Assert.IsTrue(s2.IsPauseConsistent && Time.timeScale == 0f, "再試行で一時停止に戻っていない");
        }

        [UnityTest]
        public IEnumerator End_AfterRunning_RestoresGlobalState()
        {
            Time.timeScale = 1f;
            string randomBefore = RandomStateJson();
            MoraleAuditLog.Enabled = false;

            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.MoraleLock(1));
            yield return WaitPrepared(s);
            AssertReady(s);
            Assert.IsTrue(s.StartRun());
            float until = Time.time + 1f;
            while (Time.time < until) yield return null;

            s.End("試験");
            Assert.IsNull(ReproducibleBattleQaSession.Active);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "timeScale が戻っていない");
            Assert.AreEqual(randomBefore, RandomStateJson(), "Random.state が戻っていない");
            Assert.IsFalse(MoraleAuditLog.Enabled, "台帳の有効状態が戻っていない");
            Assert.AreEqual(0, FleetRegistry.AllFlagships.Count, "QAの艦隊が残った");
            StringAssert.Contains("終了（理由＝試験）", ReproducibleBattleQaSession.LastLog.Dump(""));
            yield return WaitRealFrames(3);
            Assert.IsFalse(AnyQaSceneLoaded(), "使い捨てシーンが残った");
        }

        [UnityTest]
        public IEnumerator SceneUnloadedWithoutEnd_StillRestores()
        {
            Time.timeScale = 1f;
            string randomBefore = RandomStateJson();
            MoraleAuditLog.Enabled = false;

            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.Retreat(1));
            yield return WaitPrepared(s);
            AssertReady(s);
            Scene qa = s.QaScene;

            // シーン離脱（終了操作なし）。アクティブシーンは先に戻しておく（最後の1枚は閉じられないため）。
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene other = SceneManager.GetSceneAt(i);
                if (other != qa && other.isLoaded) { SceneManager.SetActiveScene(other); break; }
            }
            SceneManager.UnloadSceneAsync(qa);
            yield return WaitRealFrames(5);

            Assert.IsNull(ReproducibleBattleQaSession.Active, "破棄されたセッションが残っている");
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "シーン離脱で timeScale が戻っていない");
            Assert.AreEqual(randomBefore, RandomStateJson(), "シーン離脱で Random.state が戻っていない");
            Assert.IsFalse(MoraleAuditLog.Enabled, "シーン離脱で台帳の有効状態が戻っていない");
            Assert.AreEqual(0, FleetRegistry.AllFlagships.Count, "シーン離脱で艦隊が残った");
            StringAssert.Contains("終了操作なしで破棄", ReproducibleBattleQaSession.LastLog.Dump(""));
        }
    }
}
#endif
