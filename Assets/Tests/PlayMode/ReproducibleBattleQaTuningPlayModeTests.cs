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
    /// 固定会戦QAの調整プリセット（SPEED-07）を実コンポーネントで確かめる。
    /// ★調整値が盤面へ乗ることと、既定一致・1項目比較・不正値拒否・再試行の再適用・漏れないことだけを見る。
    /// 性能や自然会戦の合格は扱わない。
    /// </summary>
    public class ReproducibleBattleQaTuningPlayModeTests
    {
        private const float PrepareTimeoutReal = 10f;

        private float savedTimeScale;
        private bool savedAuditEnabled;
        private float savedAuditMinAbsDelta;

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
            yield return WaitRealFrames(3);
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

        /// <summary>スクリプト既定（Awake 前の新規コンポーネント）を読む。非アクティブで作る＝登録も起動処理も走らない。</summary>
        private static Dictionary<BattleQaTuningField, float> ScriptDefaults()
        {
            var go = new GameObject("QA調整_既定値の採取");
            go.SetActive(false);
            var mv = go.AddComponent<FleetMovement>();
            var mo = go.AddComponent<FleetMorale>();
            var d = new Dictionary<BattleQaTuningField, float>
            {
                { BattleQaTuningField.移動速度, mv.maxSpeed },
                { BattleQaTuningField.回頭速度, mv.rotationSpeed },
                { BattleQaTuningField.士気回復量, mo.recoveryRate },
                { BattleQaTuningField.敗走回復待ち, mo.routedRecoveryDelay },
            };
            Object.DestroyImmediate(go);
            return d;
        }

        private static float ReadLive(FleetStrength f, BattleQaTuningField field)
        {
            switch (field)
            {
                case BattleQaTuningField.移動速度: return f.GetComponent<FleetMovement>().maxSpeed;
                case BattleQaTuningField.回頭速度: return f.GetComponent<FleetMovement>().rotationSpeed;
                case BattleQaTuningField.士気回復量: return f.GetComponent<FleetMorale>().recoveryRate;
                default: return f.GetComponent<FleetMorale>().routedRecoveryDelay;
            }
        }

        private static string RandomStateJson() => JsonUtility.ToJson(Random.state);

        // ===== 既定 =====

        [UnityTest]
        public IEnumerator DefaultTuning_MatchesPriorEffectiveValues_AndLogsNameAndVersion()
        {
            Dictionary<BattleQaTuningField, float> defaults = ScriptDefaults();
            yield return null;

            // 調整を渡さない従来の入口＝先行QAと同じ呼び方。
            BattleQaPreset p = BattleQaPresetCatalog.Retreat(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s);
            AssertReady(s);

            Assert.IsTrue(s.Tuning.IsDefault, "従来の入口が既定以外の調整を使った");
            Assert.AreEqual(p.Fleets.Count * defaults.Count, s.AppliedTuning.Count, "全艦隊×全項目を記録していない");
            for (int i = 0; i < s.AppliedTuning.Count; i++)
            {
                ReproducibleBattleQaSession.TuningRecord r = s.AppliedTuning[i];
                Assert.AreEqual(r.baseline, r.applied, "既定なのに値が変わった：艦隊" + r.fleetId + " " + r.field);
                Assert.AreEqual(defaults[r.field], r.baseline, 1e-6f, "先行QAの実効値（スクリプト既定）と違う：艦隊" + r.fleetId + " " + r.field);
                Assert.AreEqual(r.applied, ReadLive(s.Fleet(r.fleetId), r.field), "盤面の値が記録と違う");
            }
            CollectionAssert.IsEmpty(s.TuningReadbackMismatches());

            string dump = s.Log.Dump("");
            StringAssert.Contains("調整プリセット「既定」版" + BattleQaTuningProfile.CurrentVersion, dump);
            StringAssert.Contains("FleetMovement.maxSpeed=", dump);
            StringAssert.Contains("FleetMorale.recoveryRate=", dump);
        }

        // ===== 1項目だけ変える =====

        [UnityTest]
        public IEnumerator ScaleOneField_ChangesOnlyThatField_OtherValuesAndInitialStateUnchanged()
        {
            BattleQaPreset p = BattleQaPresetCatalog.Retreat(BattleQaPresetCatalog.DefaultSeed);

            ReproducibleBattleQaSession s0 = ReproducibleBattleQaSession.Begin(p, BattleQaTuningProfile.Default);
            yield return WaitPrepared(s0);
            AssertReady(s0);
            var baseline = new List<ReproducibleBattleQaSession.TuningRecord>(s0.AppliedTuning);
            BattleQaSnapshot baseSnap = s0.InitialSnapshot;
            s0.End("既定の採取");
            yield return WaitRealFrames(3);

            foreach (BattleQaTuningField target in new[] { BattleQaTuningField.移動速度, BattleQaTuningField.回頭速度,
                         BattleQaTuningField.士気回復量, BattleQaTuningField.敗走回復待ち })
            {
                const float scale = 1.5f;
                BattleQaTuningProfile tuning = BattleQaTuningCatalog.ScaleOne(target, scale);
                ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p, tuning);
                yield return WaitPrepared(s);
                AssertReady(s);

                Assert.AreEqual(baseline.Count, s.AppliedTuning.Count);
                for (int i = 0; i < baseline.Count; i++)
                {
                    ReproducibleBattleQaSession.TuningRecord b = baseline[i];
                    ReproducibleBattleQaSession.TuningRecord r = s.AppliedTuning[i];
                    Assert.AreEqual(b.fleetId, r.fleetId);
                    Assert.AreEqual(b.field, r.field);
                    Assert.AreEqual(b.baseline, r.baseline, target + "：基準が既定の実行と違う（艦隊" + r.fleetId + " " + r.field + "）");
                    float live = ReadLive(s.Fleet(r.fleetId), r.field);
                    if (r.field == target)
                    {
                        Assert.AreEqual(b.baseline * scale, r.applied, 1e-4f, target + "：倍率が当たっていない");
                        Assert.AreEqual(r.applied, live, target + "：盤面に乗っていない");
                    }
                    else
                    {
                        Assert.AreEqual(b.applied, r.applied, target + "：対象外 " + r.field + " が変わった（艦隊" + r.fleetId + "）");
                        Assert.AreEqual(b.applied, live, target + "：対象外 " + r.field + " の盤面値が変わった");
                    }
                }
                CollectionAssert.IsEmpty(BattleQaSnapshot.Compare(baseSnap, s.InitialSnapshot, BattleQaTolerance.Default),
                    target + "：調整で初期条件（位置・艦艇数・士気等）まで変わった");
                StringAssert.Contains("調整プリセット「" + tuning.name + "」版" + BattleQaTuningProfile.CurrentVersion, s.Log.Dump(""));
                s.End("比較 " + target);
                yield return WaitRealFrames(3);
            }
        }

        // ===== 不正値 =====

        [UnityTest]
        public IEnumerator InvalidTuning_FailsPreparation_AndRestoresState()
        {
            var bad = new[]
            {
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.移動速度, float.NaN),
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.回頭速度, float.PositiveInfinity),
                BattleQaTuningProfile.Default.With("負の回復量", BattleQaTuningField.士気回復量, BattleQaTuningOverride.Absolute(-1f)),
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.移動速度, float.MaxValue),   // 有限だが適用値があふれる
                BattleQaTuningProfile.Default.With("軍団間隔0", BattleQaTuningField.軍団隊形間隔, BattleQaTuningOverride.Absolute(0f)),
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.軍団隊形間隔, float.NaN),
            };
            foreach (BattleQaTuningProfile tuning in bad)
            {
                Time.timeScale = 1.25f;
                Random.InitState(31337);
                string randomBefore = RandomStateJson();
                MoraleAuditLog.Enabled = false;

                ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.Retreat(1), tuning);
                yield return WaitPrepared(s);

                Assert.IsNotNull((object)s, "セッションが作られていない");
                Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備失敗, s.CurrentPhase, tuning.Describe() + " を受け付けた");
                StringAssert.Contains("調整プリセット", s.FailureReason);
                StringAssert.Contains(tuning.name, ReproducibleBattleQaSession.LastLog.Dump(""), "失敗したプリセット名がログに無い");
                Assert.IsNull(ReproducibleBattleQaSession.Active, "失敗したセッションが残っている");
                Assert.AreEqual(1.25f, Time.timeScale, 1e-4f, "timeScale が戻っていない");
                Assert.AreEqual(randomBefore, RandomStateJson(), "Random.state が戻っていない");
                Assert.IsFalse(MoraleAuditLog.Enabled, "台帳の有効状態が戻っていない");
                Assert.AreEqual(0, FleetRegistry.AllFlagships.Count, "QAの艦隊が残った");
                yield return WaitRealFrames(3);
                Assert.IsFalse(AnyQaSceneLoaded(), "使い捨てシーンが残った");
            }
            StringAssert.Contains("軍団隊形間隔", ReproducibleBattleQaSession.LastLog.Dump(""), "軍団隊形間隔の拒否理由がログに無い");

            // 軍団長AIを使わないプリセット（不退転）に軍団隊形間隔を指定＝適用先が無いので黙って無視せず準備失敗。
            ReproducibleBattleQaSession noTarget = ReproducibleBattleQaSession.Begin(BattleQaPresetCatalog.MoraleLock(1),
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.軍団隊形間隔, BattleQaTuningCatalog.CorpsSpacingComparisonScale));
            yield return WaitPrepared(noTarget);
            Assert.AreEqual(ReproducibleBattleQaSession.Phase.準備失敗, noTarget.CurrentPhase, "適用先の無い軍団隊形間隔を受け付けた");
            StringAssert.Contains("適用先が無い", noTarget.FailureReason);
            Assert.AreEqual(0, FleetRegistry.AllFlagships.Count, "QAの艦隊が残った");
            yield return WaitRealFrames(3);
        }

        // ===== 軍団隊形間隔（最小間隔の実接続） =====

        private const float SpacingTolerance = 1e-4f;

        /// <summary>1回ぶんの観測：軍団長AIの算出記録と、隷下に実際に配ったスロット（軍団長基準）。</summary>
        private class SpacingRun
        {
            public float managerMinSpacing;
            public CorpsSpacingResult result;
            public float liveMaxFootprint;
            public readonly Dictionary<int, Vector2> slots = new Dictionary<int, Vector2>();
            public string log;
        }

        private static FleetStrength CorpsCommanderOf(ReproducibleBattleQaSession s)
        {
            IReadOnlyList<BattleQaFleetSpec> specs = s.Preset.Fleets;
            for (int i = 0; i < specs.Count; i++)
                if (specs[i].role == BattleQaCommandRole.軍団長) return s.Fleet(specs[i].fleetId);
            return null;
        }

        /// <summary>準備→開始→軍団長AIの初回算出を待ち、盤面の実スロットを読んで終了する。</summary>
        private static IEnumerator ObserveSpacing(BattleQaPreset p, BattleQaTuningProfile tuning, SpacingRun run)
        {
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(p, tuning);
            yield return WaitPrepared(s);
            AssertReady(s);
            Assert.IsTrue(s.HasCorpsSpacingTarget, "軍団長AIを使うプリセットなのに適用先が無い");
            Assert.IsNotNull(s.CommandManager);
            run.managerMinSpacing = s.CommandManager.corpsMinSpacing;
            Assert.AreEqual(s.CorpsMinSpacingApplied, run.managerMinSpacing, "記録した適用値と軍団長AIの項目が違う");

            FleetStrength cmd = CorpsCommanderOf(s);
            Assert.IsNotNull(cmd, "軍団長が無い");
            string key = CorpsFormation.KeyFor(cmd);
            Assert.IsTrue(s.StartRun());

            float until = Time.realtimeSinceStartup + PrepareTimeoutReal;
            CorpsSpacingResult r = default;
            bool got = false;
            while (!got && Time.realtimeSinceStartup < until)
            {
                got = s.CommandManager != null && s.CommandManager.TryGetCorpsSpacing(key, out r);
                if (!got) yield return null;
            }
            Assert.IsTrue(got, "軍団長AIが間隔を算出しなかった（実接続が動いていない）");
            run.result = r;

            // 算出と同じフレームで盤面を読む（占有半径・配られたスロット）。
            float maxFoot = 0f;
            IReadOnlyList<BattleQaFleetSpec> specs = p.Fleets;
            for (int i = 0; i < specs.Count; i++)
            {
                FleetStrength f = s.Fleet(specs[i].fleetId);
                if (f == null || !f.IsAlive || f.corpsName != cmd.corpsName || f.faction != cmd.faction) continue;
                Squadron sq = f.GetComponent<Squadron>();
                if (sq != null) maxFoot = Mathf.Max(maxFoot, sq.FootprintRadius());
                FleetAI ai = f.GetComponent<FleetAI>();
                if (f != cmd && ai != null && ai.hasCorpsSlot) run.slots[specs[i].fleetId] = ai.corpsSlotLocal;
            }
            run.liveMaxFootprint = maxFoot;
            Assert.IsNotEmpty(run.slots, "隷下にスロットが配られていない");

            // 算出ログ（開始後の記録コルーチン）を待つ。
            until = Time.realtimeSinceStartup + PrepareTimeoutReal;
            while (!s.Log.Dump("").Contains("軍団隊形間隔の算出") && Time.realtimeSinceStartup < until) yield return null;
            run.log = s.Log.Dump("");
            s.End("間隔の観測");
            yield return WaitRealFrames(3);
        }

        private static void AssertSlotsScaled(SpacingRun baseRun, SpacingRun run, string label)
        {
            float ratio = run.result.actualSpacing / baseRun.result.actualSpacing;
            Assert.AreEqual(baseRun.slots.Count, run.slots.Count, label + "：スロットを配った隷下の数が違う");
            foreach (KeyValuePair<int, Vector2> kv in baseRun.slots)
            {
                Assert.IsTrue(run.slots.ContainsKey(kv.Key), label + "：艦隊" + kv.Key + " にスロットが無い");
                Vector2 expected = kv.Value * ratio;
                Vector2 actual = run.slots[kv.Key];
                Assert.AreEqual(expected.x, actual.x, 1e-3f, label + "：艦隊" + kv.Key + " のスロットが実間隔に比例していない（陣形が違う可能性）");
                Assert.AreEqual(expected.y, actual.y, 1e-3f, label + "：艦隊" + kv.Key + " のスロットが実間隔に比例していない（陣形が違う可能性）");
            }
        }

        [UnityTest]
        public IEnumerator CorpsSpacing_DefaultMatchesFormer_WidensAboveFloor_KeepsFloorBelow()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);

            // 既定（未指定）＝以前の算出 Max(7, 2×最大占有半径＋3) と一致。
            var def = new SpacingRun();
            yield return ObserveSpacing(p, BattleQaTuningProfile.Default, def);
            Assert.AreEqual(CorpsSpacingRules.DefaultMinSpacing, def.managerMinSpacing, "既定の最小間隔が7でない");
            Assert.AreEqual(7f, def.result.minSpacing);
            Assert.AreEqual(def.liveMaxFootprint, def.result.maxFootprint, SpacingTolerance, "算出に使った占有半径が盤面と違う");
            Assert.AreEqual(Mathf.Max(7f, 2f * def.liveMaxFootprint + 3f), def.result.actualSpacing, SpacingTolerance, "既定の実間隔が以前の式と違う");
            StringAssert.Contains("軍団隊形間隔の算出", def.log);
            StringAssert.Contains("実間隔=", def.log);
            StringAssert.Contains("調整プリセット「既定」版" + BattleQaTuningProfile.CurrentVersion, def.log);
            float floor = def.result.footprintFloor;

            // 最小間隔が占有下限を超える＝実スロットが広がる。
            float wide = Mathf.Max(def.result.actualSpacing, floor) + 5f;
            BattleQaTuningProfile wideTuning = BattleQaTuningProfile.Default.With("軍団間隔広", BattleQaTuningField.軍団隊形間隔,
                BattleQaTuningOverride.Absolute(wide));
            var w = new SpacingRun();
            yield return ObserveSpacing(p, wideTuning, w);
            Assert.AreEqual(wide, w.managerMinSpacing, SpacingTolerance, "最小間隔が軍団長AIに当たっていない");
            Assert.AreEqual(wide, w.result.minSpacing, SpacingTolerance);
            Assert.AreEqual(wide, w.result.actualSpacing, SpacingTolerance, "下限を超えた最小間隔が実間隔にならない");
            Assert.IsFalse(w.result.FootprintBound);
            Assert.Greater(w.result.actualSpacing, def.result.actualSpacing);
            AssertSlotsScaled(def, w, "広げた");
            foreach (KeyValuePair<int, Vector2> kv in def.slots)
                if (kv.Value.sqrMagnitude > 0f)
                    Assert.Greater(w.slots[kv.Key].magnitude, kv.Value.magnitude, "艦隊" + kv.Key + " の実スロットが広がっていない");
            StringAssert.Contains("軍団間隔広", w.log);
            StringAssert.Contains("最小間隔が効いている", w.log);

            // 最小間隔が占有下限を下回る＝下限を維持（重ねない）。さらに下げても実間隔は変わらない。
            var lowA = new SpacingRun();
            var lowB = new SpacingRun();
            yield return ObserveSpacing(p, BattleQaTuningProfile.Default.With("軍団間隔狭A", BattleQaTuningField.軍団隊形間隔,
                BattleQaTuningOverride.Absolute(floor * 0.5f)), lowA);
            yield return ObserveSpacing(p, BattleQaTuningProfile.Default.With("軍団間隔狭B", BattleQaTuningField.軍団隊形間隔,
                BattleQaTuningOverride.Absolute(floor * 0.25f)), lowB);
            foreach (SpacingRun low in new[] { lowA, lowB })
            {
                Assert.AreEqual(floor, low.result.actualSpacing, SpacingTolerance, "下限を割って実間隔を詰めた");
                Assert.IsTrue(low.result.FootprintBound);
                Assert.Less(low.result.minSpacing, low.result.actualSpacing, "指定値と実間隔を区別して記録していない");
                StringAssert.Contains("占有半径の下限が勝つ", low.log);
                AssertSlotsScaled(def, low, "下限");
            }
            foreach (KeyValuePair<int, Vector2> kv in lowA.slots)
            {
                Assert.AreEqual(kv.Value.x, lowB.slots[kv.Key].x, SpacingTolerance, "下限より下の調整で実スロットが変わった");
                Assert.AreEqual(kv.Value.y, lowB.slots[kv.Key].y, SpacingTolerance, "下限より下の調整で実スロットが変わった");
            }
        }

        [UnityTest]
        public IEnumerator CorpsSpacing_RetryReappliesToNewManager_EndKeepsNormalValue()
        {
            BattleQaTuningProfile tuning = BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.軍団隊形間隔, BattleQaTuningCatalog.CorpsSpacingComparisonScale);
            float expectedMin = CorpsSpacingRules.DefaultMinSpacing * BattleQaTuningCatalog.CorpsSpacingComparisonScale;
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);

            ReproducibleBattleQaSession s1 = ReproducibleBattleQaSession.Begin(p, tuning);
            yield return WaitPrepared(s1);
            AssertReady(s1);
            BattlefieldCommandManager oldManager = s1.CommandManager;
            Assert.AreEqual(expectedMin, oldManager.corpsMinSpacing, SpacingTolerance);
            Assert.IsTrue(s1.StartRun());
            float until = Time.time + 1.5f;
            while (Time.time < until) yield return null;

            ReproducibleBattleQaSession s2 = ReproducibleBattleQaSession.Retry();
            Assert.IsTrue(oldManager == null, "前の実行の軍団長AIが残っている");
            yield return WaitPrepared(s2);
            AssertReady(s2);
            Assert.AreSame(tuning, s2.Tuning);
            Assert.AreEqual(CorpsSpacingRules.DefaultMinSpacing, s2.CorpsMinSpacingBaseline, SpacingTolerance, "再試行の基準が前の適用値を引きずった");
            Assert.AreEqual(expectedMin, s2.CommandManager.corpsMinSpacing, SpacingTolerance, "再試行で新しい軍団長AIへ再適用されていない");
            s2.End("試験");
            StringAssert.Contains("軍団長AI", ReproducibleBattleQaSession.LastRestoreReport);
            yield return WaitRealFrames(3);

            // 終了後は通常値：新しく作った軍団長AIの既定と、既定で準備し直したQAの算出が7に戻る。
            var probe = new GameObject("QA調整_軍団長AI既定の採取");
            probe.SetActive(false);
            BattlefieldCommandManager fresh = probe.AddComponent<BattlefieldCommandManager>();
            Assert.AreEqual(CorpsSpacingRules.DefaultMinSpacing, fresh.corpsMinSpacing, "通常の軍団長AIの既定が変わった");
            Object.DestroyImmediate(probe);

            var after = new SpacingRun();
            yield return ObserveSpacing(p, BattleQaTuningProfile.Default, after);
            Assert.AreEqual(CorpsSpacingRules.DefaultMinSpacing, after.managerMinSpacing, "前の実行の最小間隔が漏れた");
            Assert.AreEqual(CorpsSpacingRules.DefaultMinSpacing, after.result.minSpacing, "前の実行の最小間隔で算出された");
        }

        // ===== 再試行・終了 =====

        [UnityTest]
        public IEnumerator Retry_ReappliesSameTuning_AndEndDoesNotLeakIntoNextRun()
        {
            Dictionary<BattleQaTuningField, float> defaults = ScriptDefaults();
            yield return null;

            BattleQaTuningProfile tuning = BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.士気回復量, BattleQaTuningCatalog.RecoveryComparisonScale);
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            ReproducibleBattleQaSession s1 = ReproducibleBattleQaSession.Begin(p, tuning);
            yield return WaitPrepared(s1);
            AssertReady(s1);
            FleetStrength oldFleet = s1.Fleet(2);
            Assert.IsTrue(s1.StartRun());
            float until = Time.time + 1f;
            while (Time.time < until) yield return null;

            ReproducibleBattleQaSession s2 = ReproducibleBattleQaSession.Retry();
            Assert.IsTrue(oldFleet == null, "前の実行の艦隊が残っている");
            yield return WaitPrepared(s2);
            AssertReady(s2);
            Assert.AreSame(tuning, s2.Tuning, "再試行で調整プリセットが引き継がれていない");
            for (int i = 0; i < s2.AppliedTuning.Count; i++)
            {
                ReproducibleBattleQaSession.TuningRecord r = s2.AppliedTuning[i];
                float expected = r.field == BattleQaTuningField.士気回復量
                    ? defaults[r.field] * BattleQaTuningCatalog.RecoveryComparisonScale : defaults[r.field];
                Assert.AreEqual(defaults[r.field], r.baseline, 1e-6f, "再試行の基準が前の実行の適用値を引きずった：" + r.field);
                Assert.AreEqual(expected, ReadLive(s2.Fleet(r.fleetId), r.field), 1e-4f, "再試行で再適用されていない：" + r.field);
            }

            s2.End("試験");
            StringAssert.Contains("調整プリセット「" + tuning.name + "」", ReproducibleBattleQaSession.LastRestoreReport);
            yield return WaitRealFrames(3);

            // 終了後に既定で準備し直すと、前の調整は残っていない。
            ReproducibleBattleQaSession s3 = ReproducibleBattleQaSession.Begin(p);
            yield return WaitPrepared(s3);
            AssertReady(s3);
            for (int i = 0; i < s3.AppliedTuning.Count; i++)
            {
                ReproducibleBattleQaSession.TuningRecord r = s3.AppliedTuning[i];
                Assert.AreEqual(defaults[r.field], ReadLive(s3.Fleet(r.fleetId), r.field), 1e-6f, "前の実行の調整が漏れた：" + r.field);
            }
        }
    }
}
#endif
