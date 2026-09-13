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
    /// 固定会戦QAの機能スイッチ（SPEED-08）を実コンポーネントで確かめる。
    /// ★見るのは「実経路が止まる／動く」「1つ変えても他2つは変わらない」「終了・再試行・準備失敗・シーン破棄で戻る」だけ。
    /// ★戦況イベント=ON は自然抽選（強制発火しない）＝発火の有無は合否にしない（抽選が実際に回ったことだけを見る）。
    /// 効果の対象・通知の行き先は<b>決定論の効果確認</b>（登録済み選択肢を1回適用）で別に見る＝自然発生の確認とは区別する。
    /// ★援軍=ON は BattleSetup の時限増援の実生成（QAは艦隊を作らない）を到着前0・到着1回・時間継続で重複0・OFF は0で見る。
    /// ★自然会戦の合格・固定合格との比較には使わない。
    /// </summary>
    public class ReproducibleBattleQaFeatureSwitchPlayModeTests
    {
        private const float PrepareTimeoutReal = 10f;
        private const float ObserveTimeoutReal = 20f;
        /// <summary>包囲の判断が成立する seed 違い（シーン通番＝軍団キーの決定論 roll が変わる）を探す上限。</summary>
        private const int EnvelopmentAttempts = 6;

        private float savedTimeScale;
        private bool savedAuditEnabled;
        private float savedAuditMinAbsDelta;
        private bool restorePlayerFaction;
        private Faction savedPlayerFaction;

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            savedAuditEnabled = MoraleAuditLog.Enabled;
            savedAuditMinAbsDelta = MoraleAuditLog.MinAbsDelta;
            restorePlayerFaction = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ReproducibleBattleQaSession.Active != null) ReproducibleBattleQaSession.Active.End("試験の後始末");
            yield return WaitRealFrames(3);
            Time.timeScale = savedTimeScale;
            MoraleAuditLog.Enabled = savedAuditEnabled;
            MoraleAuditLog.MinAbsDelta = savedAuditMinAbsDelta;
            if (restorePlayerFaction && GameSettings.Instance != null) GameSettings.Instance.playerFaction = savedPlayerFaction;
            restorePlayerFaction = false;
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

        private static void AssertReady(ReproducibleBattleQaSession s)
        {
            Assert.IsNotNull(s, "セッションが作られていない");
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備完了, s.CurrentPhase, "準備が完了していない：" + s.FailureReason +
                "\n" + (s.Log != null ? s.Log.Dump("") : ""));
        }

        private static bool AnyQaSceneLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded && sc.name.StartsWith(ReproducibleBattleQaSession.SceneNamePrefix)) return true;
            }
            return false;
        }

        private static int CountInScene<T>(Scene scene) where T : Component
        {
            T[] all = Object.FindObjectsByType<T>();
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].gameObject.scene == scene) n++;
            return n;
        }

        private static int CountEnabledEverywhere<T>() where T : Behaviour
        {
            T[] all = Object.FindObjectsByType<T>();
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].isActiveAndEnabled) n++;
            return n;
        }

        private static int CountQaSceneObjects<T>() where T : Component
        {
            T[] all = Object.FindObjectsByType<T>();
            int n = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.scene.name.StartsWith(ReproducibleBattleQaSession.SceneNamePrefix)) n++;
            return n;
        }

        private static BattleQaFeatureSwitches Switch(BattleQaFeature f, bool on) => BattleQaFeatureSwitches.FixedDefault.With(null, f, on);

        /// <summary>
        /// 包囲の判断が成立する小さな会戦：有能な軍団長（統率100＋既定の情報80＝能力0.9）＋隷下2（後衛あり）、
        /// 敵1隊を正面40以内（交戦距離）に置く。無傷＝総退却は起きない。種別は退却（QAから命令を出さない）。
        /// </summary>
        private static BattleQaPreset EnvelopmentPreset()
        {
            const string corps = "包囲試験軍団";
            const int ships = 1000;
            const int ableLeadership = 100;
            const int stat = BattleQaPresetCatalog.NeutralStat;
            const float morale = BattleQaPresetCatalog.DefaultMorale;
            var f = new List<BattleQaFleetSpec>
            {
                new BattleQaFleetSpec(1, "包囲軍団長", Faction.同盟, corps, BattleQaCommandRole.軍団長,
                    ships, ships, "QA包囲軍団長", ableLeadership, stat, morale, new Vector2(0f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(2, "包囲隷下・前", Faction.同盟, corps, BattleQaCommandRole.隷下,
                    ships, ships, "QA包囲隷下前", stat, stat, morale, new Vector2(-8f, -4f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(3, "包囲隷下・後", Faction.同盟, corps, BattleQaCommandRole.隷下,
                    ships, ships, "QA包囲隷下後", stat, stat, morale, new Vector2(8f, -12f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(11, "正面の敵", Faction.帝国, "", BattleQaCommandRole.敵,
                    ships, ships, "QA包囲敵", stat, stat, morale, new Vector2(0f, 25f), 180f, Formation.紡錘陣, false),
            };
            return new BattleQaPreset(BattleQaPresetKind.退却, "包囲切り分け", BattleQaPresetCatalog.DefaultSeed, BattleQaPresetCatalog.DefaultTimeScale, true,
                "QA命令なし：軍団長AIの回り込み（包囲）の発意だけを観測する試験用", f);
        }

        /// <summary>1回ぶんの包囲観測。</summary>
        private class EnvelopmentRun
        {
            public int issued;
            public int suppressed;
            public bool everEnveloping;
            public bool aiAllEnabled = true;
            public bool corpsSlotsAssigned;
            public int eventManagersInQa;
            public bool qaHostAllowed;
            public BattleQaSnapshot snapshot;
            public string log;
        }

        private static IEnumerator ObserveEnvelopment(BattleQaFeatureSwitches switches, EnvelopmentRun run)
        {
            BattleQaPreset p = EnvelopmentPreset();
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p, BattleQaTuningProfile.Default, switches);
            yield return WaitPrepared(s);
            AssertReady(s);
            Assert.IsNotNull(s.CommandManager, "軍団長AIが無い");
            Assert.AreEqual(switches.Get(BattleQaFeature.自動包囲), s.CommandManager.autoEnvelopment, "スイッチが軍団長AIに当たっていない");
            run.snapshot = s.InitialSnapshot;
            Assert.IsTrue(s.StartRun());

            float until = Time.realtimeSinceStartup + ObserveTimeoutReal;
            float gameUntil = Time.time + 4f;   // 軍団長AIの周期1秒を数回跨ぐ
            while (Time.time < gameUntil && Time.realtimeSinceStartup < until)
            {
                if (s.CountEnvelopingFleets() > 0) run.everEnveloping = true;
                IReadOnlyList<BattleQaFleetSpec> specs = p.Fleets;
                for (int i = 0; i < specs.Count; i++)
                {
                    FleetStrength f = s.Fleet(specs[i].fleetId);
                    FleetAI ai = f != null ? f.GetComponent<FleetAI>() : null;
                    if (ai == null) continue;
                    if (specs[i].aiEnabled && !ai.enabled) run.aiAllEnabled = false;
                    if (specs[i].role == BattleQaCommandRole.隷下 && ai.hasCorpsSlot) run.corpsSlotsAssigned = true;
                }
                yield return null;
            }
            run.issued = s.CommandManager.EnvelopmentOrdersIssued;
            run.suppressed = s.CommandManager.EnvelopmentSuppressed;
            run.eventManagersInQa = CountInScene<BattleEventManager>(s.QaScene);
            run.qaHostAllowed = BattleEventManager.HasQaHostScene;
            run.log = s.DescribeFeatureSwitchReadback() + "\n" + s.Log.Dump("");
            s.End("包囲の観測");
            yield return WaitRealFrames(3);
        }

        // ===== 既定＝先行の固定QAと同じ状態 =====

        [UnityTest]
        public IEnumerator Default_ReproducesPriorFixedQaState_AndLogsSwitches()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p);   // 従来の入口
            yield return WaitPrepared(s);
            AssertReady(s);

            Assert.IsTrue(s.Switches.IsFixedComparable, "従来の入口が既定以外のスイッチを使った");
            Assert.IsTrue(s.CommandManager.autoEnvelopment, "既定で自動包囲が止まった");
            Assert.IsTrue(s.EnvelopmentBaseline, "通常の軍団長AIの既定が true でない");
            Assert.IsNull(s.QaEventManager, "既定で戦況イベントを置いた");
            Assert.AreEqual(0, CountInScene<BattleEventManager>(s.QaScene));
            Assert.AreEqual(0, CountEnabledEverywhere<BattleEventManager>(), "稼働中の会戦イベントがある（隔離が崩れた）");
            Assert.AreEqual(0, CountInScene<BattleSetup>(s.QaScene));
            Assert.IsNull(s.QaReinforcementSetup, "既定で援軍を予約した");
            Assert.IsFalse(BattleSetup.HasQaHostScene, "既定で援軍のQA許可が立った");
            Assert.IsFalse(BattleEventManager.HasQaHostScene, "既定でQA許可が立った");
            Assert.GreaterOrEqual(s.SwitchesAppliedRealtime, 0f, "適用時刻が記録されていない");

            string dump = s.Log.Dump("");
            StringAssert.Contains("機能スイッチ「既定」版" + BattleQaFeatureSwitches.CurrentVersion, dump);
            StringAssert.Contains("自動包囲=ON", dump);
            StringAssert.Contains("援軍=OFF", dump);
            StringAssert.Contains("戦況イベント=OFF", dump);
            StringAssert.Contains("分類＝固定", dump);
            StringAssert.Contains("自然発火は観測できない", dump, "既定の隔離注記が変わった");

            Assert.IsTrue(s.StartRun());
            yield return null;
            StringAssert.Contains("機能スイッチの実状態", s.Log.Dump(""));
        }

        // ===== 自動包囲：実判断を止める／戻す・他2つ不変 =====

        [UnityTest]
        public IEnumerator EnvelopmentOff_SuppressesRealDecision_AiAndOtherSwitchesUnchanged_OnRestores()
        {
            // ON：実際に下令され、隷下 FleetAI が回り込み命令を受ける（前提が成立する通番を探す）。
            EnvelopmentRun on = null;
            for (int i = 0; i < EnvelopmentAttempts; i++)
            {
                var r = new EnvelopmentRun();
                yield return ObserveEnvelopment(BattleQaFeatureSwitches.FixedDefault, r);
                if (r.issued > 0) { on = r; break; }
            }
            if (on == null) Assert.Inconclusive("自動包囲ON で " + EnvelopmentAttempts + " 回とも下令されなかった（前提不成立＝合格にしない）");
            Assert.IsTrue(on.everEnveloping, "下令したのに FleetAI.enveloping が一度も立たない（実経路が届いていない）\n" + on.log);
            StringAssert.Contains("自動包囲=ON：autoEnvelopment=True", on.log);

            // OFF：判断は成立するが下令しない（見送りを数える）。
            EnvelopmentRun off = null;
            for (int i = 0; i < EnvelopmentAttempts; i++)
            {
                var r = new EnvelopmentRun();
                yield return ObserveEnvelopment(Switch(BattleQaFeature.自動包囲, false), r);
                Assert.AreEqual(0, r.issued, "自動包囲OFF なのに下令した\n" + r.log);
                Assert.IsFalse(r.everEnveloping, "自動包囲OFF なのに回り込み命令が立った\n" + r.log);
                if (r.suppressed > 0) { off = r; break; }
            }
            if (off == null) Assert.Inconclusive("自動包囲OFF で判断成立（見送り）を一度も観測できなかった（止めた証拠なし＝合格にしない）");

            // AI全体停止と区別：AI・軍団長AIのスロット配布は動いている。
            Assert.IsTrue(off.aiAllEnabled, "自動包囲OFF で FleetAI まで止まった");
            Assert.IsTrue(off.corpsSlotsAssigned, "自動包囲OFF で軍団長AIの他の判断（スロット配布）まで止まった");
            // 他2つ不変。
            Assert.AreEqual(0, off.eventManagersInQa, "自動包囲OFF で戦況イベントが置かれた");
            Assert.IsFalse(off.qaHostAllowed);
            StringAssert.Contains("援軍=OFF", off.log);
            // 初期条件は変わらない。
            CollectionAssert.IsEmpty(BattleQaSnapshot.Compare(on.snapshot, off.snapshot, BattleQaTolerance.Default),
                "スイッチで初期条件まで変わった");
            StringAssert.Contains("分類＝切り分け", off.log);

            // 終了後に既定で準備し直すと、通常値（ON）に戻っている。
            var probe = new GameObject("QAスイッチ_軍団長AI既定の採取");
            probe.SetActive(false);
            Assert.IsTrue(probe.AddComponent<BattlefieldCommandManager>().autoEnvelopment, "通常の軍団長AIの既定が変わった");
            Object.DestroyImmediate(probe);
            ReproducibleBattleQaSession after = ReproducibleBattleQaSession.Begin(EnvelopmentPreset());
            yield return WaitPrepared(after);
            AssertReady(after);
            Assert.IsTrue(after.CommandManager.autoEnvelopment, "前の実行の自動包囲OFF が漏れた");
        }

        // ===== 戦況イベント：実コンポーネントの自然抽選・他2つ不変・終了で許可解除 =====

        [UnityTest]
        public IEnumerator EventsOn_RealManagerRunsLotteryInQaScene_OthersUnchanged_EndRestoresGuard()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            BattleQaFeatureSwitches sw = Switch(BattleQaFeature.戦況イベント, true);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p, BattleQaTuningProfile.Default, sw);
            yield return WaitPrepared(s);
            AssertReady(s);

            BattleEventManager em = s.QaEventManager;
            Assert.IsNotNull(em, "戦況イベント=ON なのに実コンポーネントが無い");
            Assert.AreEqual(s.QaScene, em.gameObject.scene, "QAシーン以外に置いた");
            Assert.IsFalse(em.enabled, "開始前から抽選できる状態");
            Assert.AreEqual(0, em.TickCount, "開始前に抽選した");
            Assert.IsTrue(BattleEventManager.HasQaHostScene);
            Assert.AreEqual(0, CountEnabledEverywhere<BattleEventManager>(), "開始前に稼働中の会戦イベントがある（他シーンの隔離が崩れた）");
            // 他2つ不変。
            Assert.IsTrue(s.CommandManager.autoEnvelopment, "戦況イベントON で自動包囲が変わった");
            Assert.AreEqual(0, CountInScene<BattleSetup>(s.QaScene), "戦況イベントON で援軍経路が置かれた");
            Assert.IsFalse(BattleSetup.HasQaHostScene, "戦況イベントON で援軍のQA許可が立った");
            Assert.AreEqual(s.AlliedFaction, em.TargetFaction, "対象勢力がQAの同盟艦隊と一致しない");
            Assert.IsNotNull(em.NotificationSink, "QA所有の会戦イベントの通知先が共有の通知履歴のまま");
            Assert.IsFalse(s.Switches.IsFixedComparable);
            StringAssert.Contains("分類＝自然イベントONモード", s.Log.Dump(""));
            StringAssert.Contains("戦況イベント=ON", s.Log.Dump(""));

            const float interval = 2f;
            s.eventTickIntervalOverride = interval;
            Assert.IsTrue(s.StartRun());
            Assert.IsTrue(em.enabled, "開始で有効にならない");
            float startTime = Time.time;

            float until = Time.realtimeSinceStartup + ObserveTimeoutReal;
            while (em != null && em.TickCount == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsNotNull(em, "観測中に会戦イベントが消えた（シーン名ガードで自壊した可能性）");
            Assert.GreaterOrEqual(em.TickCount, 1, "実コンポーネントの抽選が一度も回らない（Update が動いていない）");
            Assert.GreaterOrEqual(Time.time - startTime, interval - 0.05f, "抽選間隔より早く抽選した（強制発火の疑い）");
            Assert.IsTrue(s.CommandManager.autoEnvelopment);
            string readback = s.DescribeFeatureSwitchReadback();
            StringAssert.Contains("抽選=" + em.TickCount, readback);
            StringAssert.Contains("発火=" + em.FiredCount, readback);
            TestContext.WriteLine("戦況イベントON：抽選 " + em.TickCount + " 回・発火 " + em.FiredCount + " 件（発火の有無は合否にしない）");

            s.End("試験");
            yield return WaitRealFrames(3);
            Assert.IsTrue(em == null, "QA所有の会戦イベントが残った");
            Assert.IsFalse(BattleEventManager.HasQaHostScene, "QA許可が残った");
            StringAssert.Contains("戦況イベントのQA許可 解除", ReproducibleBattleQaSession.LastRestoreReport);
            StringAssert.Contains("QA所有 BattleEventManager を破棄", ReproducibleBattleQaSession.LastSwitchDisposal);
            Assert.AreEqual(0, CountQaSceneObjects<BattleEventManager>());

            // シーン名ガードが元どおり：Battle 以外に置いた個体は自壊する。
            var stray = new GameObject("QAスイッチ_ガード確認");
            BattleEventManager strayEm = stray.AddComponent<BattleEventManager>();
            yield return WaitRealFrames(2);
            Assert.IsTrue(strayEm == null, "終了後も Battle 以外で会戦イベントが生き残った（ガードが戻っていない）");
            if (stray != null) Object.Destroy(stray);
        }

        // ===== 援軍：BattleSetup の時限増援を実経路で使う（ON＝到着で1回だけ実生成／OFF＝同じ時刻を過ぎても0） =====

        /// <summary>
        /// 開始からのゲーム経過が <paramref name="elapsed"/> に達するまで進める。観測完了で一時停止していたら再開して時間を流し続ける
        /// （援軍の重複や移動を「時間継続」で見るため）。実時間の上限つき。
        /// </summary>
        private static IEnumerator AdvanceUntilElapsed(ReproducibleBattleQaSession s, float elapsed)
        {
            float until = Time.realtimeSinceStartup + ObserveTimeoutReal;
            while (s != null && s.Elapsed < elapsed && Time.realtimeSinceStartup < until)
            {
                if (s.IsObservationDone && s.PauseCtl != null && s.PauseCtl.IsPaused) s.PauseCtl.Resume();
                yield return null;
            }
        }

        private static BattleQaCheck FindCheck(ReproducibleBattleQaSession s, string name)
        {
            IReadOnlyList<BattleQaCheck> checks = s.Log.Checks;
            for (int i = 0; i < checks.Count; i++) if (checks[i].name == name) return checks[i];
            Assert.Fail("判定「" + name + "」が無い\n" + s.Log.Dump(""));
            return default;
        }

        private static bool SharedHistoryHasSince(long seq, string fragment)
        {
            List<Notification> since = NotificationCenter.Since(seq);
            for (int i = 0; i < since.Count; i++) if (since[i].message != null && since[i].message.Contains(fragment)) return true;
            return false;
        }

        private static int CountFlagshipsInScene(Scene scene)
        {
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            int n = 0;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].gameObject.scene == scene) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator ReinforcementOn_RealTimedSpawnOnceAtArrival_NoDuplicateOverTime_MovesAsAlly_EndCleansUp()
        {
            Time.timeScale = 1.25f;
            Random.InitState(4242);
            string randomBefore = JsonUtility.ToJson(Random.state);
            long seqBefore = NotificationCenter.LastSeq;
            WarpReinforcementLedger ledger = StrategySession.Reinforcements;
            int ledgerPending = ledger.PendingCount, ledgerClosed = ledger.ClosedCount;

            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            BattleQaFeatureSwitches sw = Switch(BattleQaFeature.援軍, true);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p, BattleQaTuningProfile.Default, sw);
            yield return WaitPrepared(s);
            AssertReady(s);
            float arrival = s.ReinforcementPlan.arrivalSeconds;
            Assert.AreEqual(BattleQaReinforcementPlan.DefaultArrivalSeconds, arrival, 1e-4f);

            // 開始前：実コンポーネントの読み戻し（設定値ではなく）。
            BattleSetup setup = s.QaReinforcementSetup;
            Assert.IsNotNull(setup, "援軍=ON なのに QA所有の BattleSetup が無い");
            Assert.AreEqual(s.QaScene, setup.gameObject.scene, "QAシーン以外に置いた");
            Assert.IsFalse(setup.enabled, "開始前から経過を数える状態");
            Assert.AreEqual(1, setup.PendingReinforcementCount, "予約が1件でない");
            Assert.AreEqual(0, setup.SpawnedReinforcementCount);
            Assert.AreEqual(0, s.ReinforcementSpawnCount);
            Assert.AreEqual(0, s.CountUnplannedFleetsInQaScene(), "テンプレートが盤面に出ている");
            Assert.AreEqual(p.Fleets.Count, CountFlagshipsInScene(s.QaScene), "開始前に艦隊が増えた");
            Assert.IsTrue(BattleSetup.HasQaHostScene);
            // 他2つ不変。
            Assert.IsTrue(s.CommandManager.autoEnvelopment, "援軍ON で自動包囲が変わった");
            Assert.IsNull(s.QaEventManager, "援軍ON で戦況イベントが置かれた");
            Assert.AreEqual(0, CountEnabledEverywhere<BattleEventManager>());
            string dump = s.Log.Dump("");
            StringAssert.Contains("援軍=ON", dump);
            StringAssert.Contains("固定ID=" + BattleQaReinforcementPlan.DefaultFleetId, dump);
            StringAssert.Contains("seed=" + p.seed, dump);
            StringAssert.Contains("分類＝切り分け", dump);

            // 一時停止中は何フレーム待っても出ない。
            yield return WaitRealFrames(5);
            Assert.AreEqual(0, s.ReinforcementSpawnCount, "一時停止中に出現した");

            Assert.IsTrue(s.StartRun());
            Assert.IsTrue(setup.enabled, "開始で有効にならない");

            // 到着時刻の前：0。
            float realUntil = Time.realtimeSinceStartup + ObserveTimeoutReal;
            while (s.Elapsed < arrival - 0.1f && Time.realtimeSinceStartup < realUntil)
            {
                Assert.AreEqual(0, s.ReinforcementSpawnCount, "到着時刻の前に出現した（経過 " + s.Elapsed + "）");
                Assert.AreEqual(0, s.CountUnplannedFleetsInQaScene());
                yield return null;
            }
            // 到着：1回。
            while (s.ReinforcementSpawnCount == 0 && s.Elapsed < arrival + 1f && Time.realtimeSinceStartup < realUntil) yield return null;
            Assert.AreEqual(1, s.ReinforcementSpawnCount, "到着時刻を過ぎても出現しない／複数出た\n" + s.DescribeReinforcementReadback());
            Assert.AreEqual(1, setup.SpawnedReinforcementCount, "BattleSetup の実生成が1件でない");
            Assert.AreEqual(0, setup.PendingReinforcementCount, "生成後も予約が残った");
            Assert.AreEqual(0, s.ReinforcementEarlySpawnCount);
            Assert.GreaterOrEqual(s.ReinforcementSpawnElapsed, arrival - 0.05f, "早着");
            Assert.LessOrEqual(s.ReinforcementSpawnElapsed, arrival + 0.5f, "到着が遅すぎる");

            FleetStrength f = s.ReinforcementFleet;
            Assert.IsNotNull(f);
            Assert.AreEqual(s.QaScene, f.gameObject.scene, "援軍がQAシーン外に生まれた");
            Assert.AreEqual(BattleQaReinforcementPlan.DefaultFleetId, f.fleetNumber);
            Assert.AreEqual(BattleQaPresetCatalog.FormationCorps, f.corpsName);
            Assert.AreEqual(s.AlliedFaction, f.faction);
            Assert.AreEqual(Faction.同盟, f.faction);
            Assert.AreEqual(BattleQaReinforcementPlan.DefaultShipCount, f.strength, "艦艇数が明細と違う");
            Assert.IsFalse(string.IsNullOrEmpty(f.shipName), "SpawnFleet の旗艦名の払い出しを通っていない");
            string shipName = f.shipName;
            Assert.IsTrue(ShipNameRegistry.IsInUse(shipName));
            Assert.IsTrue(f.GetComponent<FleetAI>().enabled, "援軍の AI が有効でない（SpawnFleet を通っていない）");
            Assert.IsTrue(f.GetComponent<FleetWeapon>().enabled);
            Assert.IsFalse(FactionRelations.IsHostile(f, s.Fleet(1)), "援軍が味方と敵対");
            Assert.IsTrue(FactionRelations.IsHostile(f, s.Fleet(11)), "援軍が敵と敵対しない");
            Assert.AreEqual(p.Fleets.Count + 1, CountFlagshipsInScene(s.QaScene), "索敵在庫に援軍が1隊だけ載っていない");
            StringAssert.Contains("固定ID=" + BattleQaReinforcementPlan.DefaultFleetId, s.DescribeFeatureSwitchReadback());
            GameObject fleetGo = f.gameObject;
            Vector3 spawnPos = f.transform.position;

            // 時間を流し続けても重複しない＋味方として動ける。
            yield return AdvanceUntilElapsed(s, arrival + BattleQaReinforcementPlan.MinMoveObserveSeconds + 1.5f);
            Assert.AreEqual(1, s.ReinforcementSpawnCount, "時間継続で援軍が重複した");
            Assert.AreEqual(1, setup.SpawnedReinforcementCount);
            Assert.AreEqual(1, s.CountUnplannedFleetsInQaScene(), "予定外の艦隊が増えた");
            Assert.GreaterOrEqual(Vector3.Distance(spawnPos, f.transform.position), BattleQaReinforcementPlan.MinMoveDisplacement,
                "援軍が出現位置から動かない\n" + s.DescribeReinforcementReadback());

            // 観測完了の判定（ON のときだけ足される）。
            if (!s.IsObservationDone)
            {
                float u = Time.realtimeSinceStartup + ObserveTimeoutReal;
                while (!s.IsObservationDone && Time.realtimeSinceStartup < u) yield return null;
            }
            Assert.IsTrue(s.IsObservationDone, "観測が終わらない");
            BattleQaCheck arrivalCheck = FindCheck(s, "援軍の到着（時限増援の実生成・1回だけ）");
            Assert.AreEqual(BattleQaVerdict.合格, arrivalCheck.verdict, arrivalCheck.detail);

            // 通知：QAローカルログに入り、共有の通知履歴には入らない。
            Assert.IsTrue(ContainsFragment(s.QaNotifications, "増援到着"), "援軍の通知がQAローカルログに無い");
            Assert.IsFalse(SharedHistoryHasSince(seqBefore, "増援到着"), "QA所有の援軍の通知が共有の通知履歴へ漏れた");

            s.End("試験");
            yield return WaitRealFrames(3);
            Assert.IsTrue(fleetGo == null, "援軍艦隊が残った");
            Assert.IsTrue(setup == null, "QA所有の BattleSetup が残った");
            Assert.AreEqual(0, CountQaSceneObjects<BattleSetup>());
            Assert.IsFalse(BattleSetup.HasQaHostScene, "援軍のQA許可が残った");
            Assert.IsFalse(ShipNameRegistry.IsInUse(shipName), "援軍の旗艦名が返却されていない");
            Assert.IsFalse(ShipNameRegistry.IsRetired(shipName));
            Assert.AreEqual(0, FleetRegistry.AllFlagships.Count, "QAの艦隊が索敵在庫に残った");
            Assert.AreSame(ledger, StrategySession.Reinforcements, "戦略の援軍台帳が差し替わった");
            Assert.AreEqual(ledgerPending, ledger.PendingCount);
            Assert.AreEqual(ledgerClosed, ledger.ClosedCount);
            Assert.IsTrue(ReproducibleBattleQaSession.LastStrategyLedgerUnchanged);
            StringAssert.Contains("援軍艦隊 1 隊", ReproducibleBattleQaSession.LastSwitchDisposal);
            StringAssert.Contains("援軍のQA許可 解除", ReproducibleBattleQaSession.LastRestoreReport);
            Assert.IsFalse(SharedHistoryHasSince(seqBefore, "増援到着"), "終了後に共有の通知履歴へ漏れた");
            Assert.AreEqual(1.25f, Time.timeScale, 1e-4f);
            Assert.AreEqual(randomBefore, JsonUtility.ToJson(Random.state));
            Assert.IsFalse(AnyQaSceneLoaded());

            // 許可が戻った：QA許可の無いシーンの BattleSetup は予約を受け付けない。
            var stray = new GameObject("QAスイッチ_援軍ガード確認");
            BattleSetup straySetup = stray.AddComponent<BattleSetup>();
            straySetup.fleetPrefab = stray;
            Assert.IsFalse(straySetup.ScheduleReinforcementForQa(new ScenarioData.FleetEntry { reinforcementDelay = 1f }, Faction.同盟),
                "終了後もQA以外のシーンで予約を受け付けた");
            Object.Destroy(stray);
        }

        private static bool ContainsFragment(IReadOnlyList<string> lines, string fragment)
        {
            for (int i = 0; i < lines.Count; i++) if (lines[i] != null && lines[i].Contains(fragment)) return true;
            return false;
        }

        [UnityTest]
        public IEnumerator ReinforcementOff_SameArrivalTimePasses_NoFleetSpawned()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p, BattleQaTuningProfile.Default, BattleQaFeatureSwitches.FixedDefault);
            yield return WaitPrepared(s);
            AssertReady(s);
            Assert.IsFalse(s.Switches.Get(BattleQaFeature.援軍), "既定で援軍が ON");
            float arrival = s.ReinforcementPlan.arrivalSeconds;
            Assert.AreEqual(BattleQaReinforcementPlan.DefaultArrivalSeconds, arrival, 1e-4f, "OFF でも比較用の到着時刻は同じ");
            Assert.IsNull(s.QaReinforcementSetup);
            Assert.AreEqual(0, CountInScene<BattleSetup>(s.QaScene));
            Assert.IsFalse(BattleSetup.HasQaHostScene, "OFF でQA許可が立った");

            Assert.IsTrue(s.StartRun());
            yield return AdvanceUntilElapsed(s, arrival + 1.5f);
            Assert.GreaterOrEqual(s.Elapsed, arrival + 1.5f, "到着時刻を過ぎるまで時間が進まなかった（前提不成立）");
            Assert.AreEqual(0, s.ReinforcementSpawnCount, "援軍OFF なのに出現した");
            Assert.AreEqual(0, s.CountUnplannedFleetsInQaScene(), "援軍OFF なのに予定外の艦隊が生まれた");
            Assert.AreEqual(p.Fleets.Count, CountFlagshipsInScene(s.QaScene), "援軍OFF で艦隊数が変わった");
            Assert.AreEqual(0, CountInScene<BattleSetup>(s.QaScene));
            Assert.AreEqual(0, CountEnabledEverywhere<BattleManager>(), "BattleManager の隔離が崩れた");
            StringAssert.Contains("援軍=OFF：予約しない", s.DescribeFeatureSwitchReadback());
            CollectionAssert.IsEmpty(FindChecksNamed(s, "援軍の到着（時限増援の実生成・1回だけ）"), "OFF（既定）のログに援軍の判定が足された");
        }

        private static List<BattleQaCheck> FindChecksNamed(ReproducibleBattleQaSession s, string name)
        {
            var found = new List<BattleQaCheck>();
            IReadOnlyList<BattleQaCheck> checks = s.Log.Checks;
            for (int i = 0; i < checks.Count; i++) if (checks[i].name == name) found.Add(checks[i]);
            return found;
        }

        [UnityTest]
        public IEnumerator ReinforcementOn_PrepareFailureAfterCreation_RemovesSetupTemplateAndPermission()
        {
            Time.timeScale = 1.5f;
            Random.InitState(99);
            string randomBefore = JsonUtility.ToJson(Random.state);
            // 調整プリセットの適用値あふれ＝援軍の予約を作った後で準備失敗する。
            BattleQaTuningProfile badTuning = BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.移動速度, float.MaxValue);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.FormationChange(1), badTuning,
                Switch(BattleQaFeature.援軍, true));
            yield return WaitPrepared(s);

            Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備失敗, s.CurrentPhase);
            StringAssert.Contains("調整プリセット", s.FailureReason);
            StringAssert.Contains("援軍の明細", ReproducibleBattleQaSession.LastLog.Dump(""), "失敗前に予約したことがログに無い");
            StringAssert.Contains("援軍＝QA所有の BattleSetup", ReproducibleBattleQaSession.LastSwitchDisposal);
            Assert.IsFalse(BattleSetup.HasQaHostScene, "準備失敗で援軍のQA許可が残った");
            Assert.AreEqual(1.5f, Time.timeScale, 1e-4f);
            Assert.AreEqual(randomBefore, JsonUtility.ToJson(Random.state));
            Assert.IsTrue(ReproducibleBattleQaSession.LastStrategyLedgerUnchanged);
            yield return WaitRealFrames(3);
            Assert.AreEqual(0, CountQaSceneObjects<BattleSetup>(), "準備失敗でQA所有の BattleSetup が残った");
            Assert.AreEqual(0, FleetRegistry.AllFlagships.Count);
            Assert.IsFalse(AnyQaSceneLoaded());
        }

        [UnityTest]
        public IEnumerator ReinforcementOn_SceneUnloadedAfterSpawn_RemovesFleetNameAndPermission()
        {
            Time.timeScale = 1f;
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.FormationChange(1),
                BattleQaTuningProfile.Default, Switch(BattleQaFeature.援軍, true));
            s.reinforcementArrivalSeconds = 0.5f;   // 準備の2フレーム目に明細を決める＝同じフレームで設定すれば効く
            yield return WaitPrepared(s);
            AssertReady(s);
            Assert.AreEqual(0.5f, s.ReinforcementPlan.arrivalSeconds, 1e-4f);
            Assert.IsTrue(s.StartRun());
            float until = Time.realtimeSinceStartup + ObserveTimeoutReal;
            while (s.ReinforcementSpawnCount == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(1, s.ReinforcementSpawnCount, "援軍が出現しない（前提不成立）");
            GameObject fleetGo = s.ReinforcementFleet.gameObject;
            string shipName = s.ReinforcementFleet.shipName;

            SceneManager.UnloadSceneAsync(s.QaScene);
            yield return WaitRealFrames(5);

            Assert.IsNull(ReproducibleBattleQaSession.Active, "破棄されたセッションが残っている");
            Assert.IsTrue(fleetGo == null, "シーン破棄で援軍艦隊が残った");
            Assert.AreEqual(0, CountQaSceneObjects<BattleSetup>());
            Assert.IsFalse(BattleSetup.HasQaHostScene, "シーン破棄で援軍のQA許可が残った");
            Assert.IsFalse(ShipNameRegistry.IsInUse(shipName), "シーン破棄で援軍の旗艦名が返却されていない");
            StringAssert.Contains("援軍のQA許可 解除", ReproducibleBattleQaSession.LastRestoreReport);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f);
        }

        [UnityTest]
        public IEnumerator ReinforcementOn_RetryReschedulesOnNewSetup_OldFleetRemoved_EndDoesNotLeak()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            BattleQaFeatureSwitches sw = Switch(BattleQaFeature.援軍, true);
            ReproducibleBattleQaSession s1 = ReproducibleBattleQaSession.Begin(p, BattleQaTuningProfile.Default, sw);
            s1.reinforcementArrivalSeconds = 0.5f;
            yield return WaitPrepared(s1);
            AssertReady(s1);
            BattleSetup oldSetup = s1.QaReinforcementSetup;
            Assert.IsTrue(s1.StartRun());
            float until = Time.realtimeSinceStartup + ObserveTimeoutReal;
            while (s1.ReinforcementSpawnCount == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(1, s1.ReinforcementSpawnCount, "援軍が出現しない（前提不成立）");
            GameObject oldFleet = s1.ReinforcementFleet.gameObject;

            ReproducibleBattleQaSession s2 = ReproducibleBattleQaSession.Retry();
            Assert.IsTrue(oldFleet == null, "前の実行の援軍艦隊が残っている");
            Assert.IsTrue(oldSetup == null, "前の実行の BattleSetup が残っている");
            yield return WaitPrepared(s2);
            AssertReady(s2);
            Assert.AreSame(sw, s2.Switches);
            Assert.AreEqual(0.5f, s2.ReinforcementPlan.arrivalSeconds, 1e-4f, "再試行で到着時刻が引き継がれていない");
            Assert.IsNotNull(s2.QaReinforcementSetup, "再試行で援軍が予約し直されていない");
            Assert.AreEqual(1, s2.QaReinforcementSetup.PendingReinforcementCount);
            Assert.AreEqual(0, s2.ReinforcementSpawnCount, "前の実行の出現回数を引きずった");
            Assert.AreEqual(0, s2.CountUnplannedFleetsInQaScene());
            Assert.IsTrue(BattleSetup.HasQaHostScene);

            s2.End("試験");
            yield return WaitRealFrames(3);
            Assert.IsFalse(BattleSetup.HasQaHostScene);

            ReproducibleBattleQaSession s3 = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s3);
            AssertReady(s3);
            Assert.IsNull(s3.QaReinforcementSetup, "前の実行の援軍ON が漏れた");
            Assert.AreEqual(0, CountInScene<BattleSetup>(s3.QaScene));
            Assert.IsFalse(BattleSetup.HasQaHostScene);
        }

        // ===== 戦況イベント：決定論の効果確認（自然発生の確認ではない）＝対象はQA同盟艦隊・通知はQAローカル =====

        [UnityTest]
        public IEnumerator EventsOn_DeterministicEffect_TargetsQaAlliedFleets_NotificationsStayLocal()
        {
            // ★GameSettings.playerFaction を QA の味方と違う値にして、効果が GameSettings でなく明示指定に従うことを見る（TearDown で戻す）。
            savedPlayerFaction = GameSettings.Instance.playerFaction;
            restorePlayerFaction = true;
            GameSettings.Instance.playerFaction = Faction.帝国;
            long seqBefore = NotificationCenter.LastSeq;

            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p, BattleQaTuningProfile.Default, Switch(BattleQaFeature.戦況イベント, true));
            yield return WaitPrepared(s);
            AssertReady(s);
            BattleEventManager em = s.QaEventManager;
            Assert.IsNotNull(em);
            Assert.AreEqual(Faction.同盟, s.AlliedFaction);
            Assert.IsTrue(em.HasTargetFactionOverride, "対象勢力が明示されていない");
            Assert.AreEqual(Faction.同盟, em.TargetFaction, "対象勢力がQAの同盟艦隊と一致しない");
            Assert.AreEqual(Faction.帝国, GameSettings.Instance.playerFaction, "QAが GameSettings.playerFaction を書き換えた");
            Assert.IsNotNull(em.NotificationSink, "通知の送り先がQAローカルでない");

            // 一時停止中（AI・武装・自然回復が止まっている）に、登録済みの選択肢の効果を1回だけ適用する。
            var before = new Dictionary<int, float>();
            IReadOnlyList<BattleQaFleetSpec> specs = p.Fleets;
            for (int i = 0; i < specs.Count; i++) before[specs[i].fleetId] = s.Fleet(specs[i].fleetId).GetComponent<FleetMorale>().morale;
            int localBefore = s.QaNotifications.Count;

            Assert.IsFalse(em.ApplyChoiceForQaVerification("存在しないイベント", 0));
            Assert.IsFalse(em.ApplyChoiceForQaVerification("battle_supply", 5));
            Assert.IsTrue(em.ApplyChoiceForQaVerification("battle_supply", 0), "「補給線に不安」の慎重（士気-5）を適用できない");

            const float supplyDelta = -5f;
            for (int i = 0; i < specs.Count; i++)
            {
                float now = s.Fleet(specs[i].fleetId).GetComponent<FleetMorale>().morale;
                if (specs[i].faction == Faction.同盟)
                    Assert.AreEqual(before[specs[i].fleetId] + supplyDelta, now, 1e-3f, "QA同盟艦隊 " + specs[i].fleetId + " に効果が掛からない");
                else
                    Assert.AreEqual(before[specs[i].fleetId], now, 1e-3f, "対象外（" + specs[i].faction + "）の艦隊 " + specs[i].fleetId + " に効果が掛かった");
            }
            Assert.AreEqual(0, em.TickCount, "決定論の効果確認が自然抽選に数えられた");
            Assert.AreEqual(0, em.FiredCount, "決定論の効果確認が発火に数えられた");
            Assert.AreEqual(localBefore + 1, s.QaNotifications.Count, "効果の通知がQAローカルログに1件入らない");
            Assert.IsTrue(ContainsFragment(s.QaNotifications, "会戦イベント：味方の士気が下がった"));
            Assert.IsFalse(SharedHistoryHasSince(seqBefore, "会戦イベント："), "QA所有の会戦イベントの通知が共有の通知履歴へ漏れた");
            StringAssert.Contains("会戦イベント：味方の士気が下がった", s.Log.Dump(""), "QAローカルログ（結果ログ）に通知が無い");
            StringAssert.Contains("対象勢力=同盟（明示=True", s.DescribeFeatureSwitchReadback());
            TestContext.WriteLine("決定論の効果確認（自然発生の確認ではない）：QA同盟艦隊の士気 " + supplyDelta + "・通知はQAローカル");

            s.End("試験");
            yield return WaitRealFrames(3);
            Assert.IsFalse(SharedHistoryHasSince(seqBefore, "会戦イベント："), "終了後に共有の通知履歴へ漏れた");
            Assert.AreEqual(Faction.帝国, GameSettings.Instance.playerFaction, "終了で GameSettings.playerFaction が変わった");
            StringAssert.Contains("共有の通知履歴 LastSeq", ReproducibleBattleQaSession.LastRestoreReport);

            // 通常の個体（QA許可の無いシーン）は従来どおり：送り先は共有・対象は GameSettings・QAの効果確認は受け付けない。
            var stray = new GameObject("QAスイッチ_通常個体の確認");
            BattleEventManager normal = stray.AddComponent<BattleEventManager>();
            Assert.IsNull(normal.NotificationSink, "通常の個体の通知先が共有でない");
            Assert.IsFalse(normal.HasTargetFactionOverride);
            Assert.AreEqual(Faction.帝国, normal.TargetFaction, "通常の個体の対象が GameSettings.playerFaction でない");
            Assert.IsFalse(normal.ApplyChoiceForQaVerification("battle_supply", 0), "QA許可の無い個体が効果確認を受け付けた");
            yield return WaitRealFrames(2);
            if (stray != null) Object.Destroy(stray);
        }

        // ===== 例外経路：戦況イベント生成後の準備失敗・シーン破棄 =====

        [UnityTest]
        public IEnumerator EventsOn_PrepareFailureAfterCreation_RemovesManagerAndPermission()
        {
            Time.timeScale = 1.5f;
            // 調整プリセットの適用値あふれ＝戦況イベント生成の後で準備失敗する。
            BattleQaTuningProfile badTuning = BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.移動速度, float.MaxValue);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.FormationChange(1), badTuning,
                Switch(BattleQaFeature.戦況イベント, true));
            yield return WaitPrepared(s);

            Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備失敗, s.CurrentPhase);
            StringAssert.Contains("調整プリセット", s.FailureReason);
            StringAssert.Contains("戦況イベント=ON", ReproducibleBattleQaSession.LastLog.Dump(""), "失敗前に生成したことがログに無い");
            StringAssert.Contains("戦況イベント", ReproducibleBattleQaSession.LastSwitchDisposal);
            Assert.IsFalse(BattleEventManager.HasQaHostScene, "準備失敗でQA許可が残った");
            Assert.AreEqual(1.5f, Time.timeScale, 1e-4f);
            yield return WaitRealFrames(3);
            Assert.AreEqual(0, CountQaSceneObjects<BattleEventManager>(), "準備失敗でQA所有の会戦イベントが残った");
            Assert.IsFalse(AnyQaSceneLoaded());
        }

        [UnityTest]
        public IEnumerator EventsOn_SceneUnloadedWithoutEnd_RemovesManagerAndPermission()
        {
            Time.timeScale = 1f;
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.FormationChange(1),
                BattleQaTuningProfile.Default, Switch(BattleQaFeature.戦況イベント, true));
            yield return WaitPrepared(s);
            AssertReady(s);
            BattleEventManager em = s.QaEventManager;
            Assert.IsTrue(s.StartRun());
            yield return WaitRealFrames(2);

            SceneManager.UnloadSceneAsync(s.QaScene);
            yield return WaitRealFrames(5);

            Assert.IsNull(ReproducibleBattleQaSession.Active, "破棄されたセッションが残っている");
            Assert.IsTrue(em == null, "シーン破棄でQA所有の会戦イベントが残った");
            Assert.IsFalse(BattleEventManager.HasQaHostScene, "シーン破棄でQA許可が残った");
            StringAssert.Contains("戦況イベントのQA許可 解除", ReproducibleBattleQaSession.LastRestoreReport);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f);
        }

        // ===== 再試行：同じスイッチを新しい対象へ当て直す・終了後に漏れない =====

        [UnityTest]
        public IEnumerator Retry_ReappliesSameSwitchesToNewTargets_AndEndDoesNotLeak()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            BattleQaFeatureSwitches sw = Switch(BattleQaFeature.戦況イベント, true).With(null, BattleQaFeature.自動包囲, false);
            ReproducibleBattleQaSession s1 = ReproducibleBattleQaSession.Begin(p, BattleQaTuningProfile.Default, sw);
            yield return WaitPrepared(s1);
            AssertReady(s1);
            BattleEventManager oldEm = s1.QaEventManager;
            BattlefieldCommandManager oldCm = s1.CommandManager;
            Assert.IsTrue(s1.StartRun());
            float until = Time.time + 1f;
            while (Time.time < until) yield return null;

            ReproducibleBattleQaSession s2 = ReproducibleBattleQaSession.Retry();
            Assert.IsTrue(oldEm == null, "前の実行の会戦イベントが残っている");
            Assert.IsTrue(oldCm == null, "前の実行の軍団長AIが残っている");
            yield return WaitPrepared(s2);
            AssertReady(s2);
            Assert.AreSame(sw, s2.Switches, "再試行でスイッチが引き継がれていない");
            Assert.IsNotNull(s2.QaEventManager, "再試行で戦況イベントが当て直されていない");
            Assert.AreEqual(s2.QaScene, s2.QaEventManager.gameObject.scene);
            Assert.IsFalse(s2.CommandManager.autoEnvelopment, "再試行で自動包囲OFF が当て直されていない");
            Assert.IsTrue(s2.EnvelopmentBaseline, "再試行の基準が前の実行の適用値を引きずった");
            Assert.IsTrue(BattleEventManager.HasQaHostScene);

            s2.End("試験");
            yield return WaitRealFrames(3);
            Assert.IsFalse(BattleEventManager.HasQaHostScene);

            ReproducibleBattleQaSession s3 = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s3);
            AssertReady(s3);
            Assert.IsTrue(s3.Switches.IsFixedComparable);
            Assert.IsTrue(s3.CommandManager.autoEnvelopment, "前の実行の自動包囲OFF が漏れた");
            Assert.IsNull(s3.QaEventManager, "前の実行の戦況イベントON が漏れた");
            Assert.IsFalse(BattleEventManager.HasQaHostScene);
        }
    }
}
#endif
