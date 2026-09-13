#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 再現可能な会戦QA（固定会戦）のセッション本体。<b>Editor 限定</b>（<c>UNITY_EDITOR</c> 外ではコンパイルされない＝製品に入らない）。
    ///
    /// Game アセンブリに置くのは、PlayMode 試験（Editor アセンブリを参照できない）と
    /// Editor メニュー（<c>ReproducibleBattleQaMenu</c>）の<b>両方から同じ実装を使う</b>ため。
    ///
    /// <b>やること</b>
    /// <list type="bullet">
    ///   <item>使い捨てシーンを作り、プリセットの明細どおりに実コンポーネントの艦隊を固定ID順で組む（セーブ・シーン・プレハブ・提督SOに書かない）。</item>
    ///   <item>準備完了まで <see cref="PauseManager"/> で一時停止し、timeScale と一致させる。</item>
    ///   <item>開始で命令を出し、実命令経路（<see cref="ActiveCommandState.Issue"/>・<see cref="Squadron.RequestFormation"/>・
    ///         <see cref="BattlefieldCommandManager"/> の総退却・ROE）と実被弾で動いた結果を許容誤差つきで判定する。</item>
    ///   <item>Random.state・timeScale・士気の原因台帳の設定・隔離したゲーム系コンポーネントは
    ///         <b>終了・再試行・準備失敗・シーン破棄（Play 停止含む）</b>で元へ戻す。</item>
    /// </list>
    /// <b>しないこと</b>：結果の書換え、製品の調整値・計算式の変更、会戦イベントの強制発火（自然発火の確認には使えない）。
    /// </summary>
    public class ReproducibleBattleQaSession : MonoBehaviour
    {
        /// <summary>使い捨てシーン名の接頭辞（実行ごとに通番を付けて一意にする）。</summary>
        public const string SceneNamePrefix = "QA_ReproducibleBattle_";

        public enum Phase
        {
            準備中,
            準備完了,
            実行中,
            観測完了,
            準備失敗,
            終了,
        }

        [Header("退却の観測")]
        [Tooltip("開始から軍団総退却の発令を待つ上限（ゲーム秒）")]
        public float retreatOrderTimeout = 6f;
        [Tooltip("発令後に退却の変位を観測する時間（ゲーム秒）。敵を向いた艦隊はその場で約180度回頭しないと加速できない" +
                 "（FleetMovement 既定＝回頭42度/秒×機動、正面±10度で加速開始）＝回頭約4秒＋加速1.4で1単位に約1.2秒。" +
                 "最低約5.3秒に余裕を足した値（a2 の4秒は回頭の途中で打ち切っていた）")]
        public float retreatObserveSeconds = 7f;
        [Tooltip("退却の持続判定：観測中の最大変位から終了時の変位までの戻りの許容（ワールド単位）")]
        public float retreatRegressTolerance = 0.05f;

        [Header("不退転の観測")]
        [Tooltip("効果の終了を待つ上限（ゲーム秒）")]
        public float lockTimeout = 30f;
        [Tooltip("撃ち手へ射撃管制を命じてから被弾停止の計測を始めるまでの猶予（ゲーム秒）")]
        public float lockStopGraceSeconds = 0.5f;
        [Tooltip("効果終了後に被弾・敗走を観測する時間（ゲーム秒）")]
        public float lockPostEndSeconds = 8f;

        [Header("陣形変更の観測")]
        [Tooltip("保持が軍団AIの周期を跨ぐか観測する時間（ゲーム秒・軍団AIの周期1秒より長く）")]
        public float formationHoldObserveSeconds = 3.5f;
        [Tooltip("保持解除後に軍団AIの陣形へ戻るか観測する上限（ゲーム秒）")]
        public float formationReleaseObserveSeconds = 3.5f;

        [Header("軍団隊形間隔の記録")]
        [Tooltip("開始から軍団長AIが全軍団の間隔を算出するのを待ってログへ残す上限（ゲーム秒・軍団AIの周期1秒より長く）")]
        public float corpsSpacingLogTimeout = 3f;

        [Header("戦況イベント（機能スイッチ ON 時のみ・SPEED-08）")]
        [Tooltip("QA所有の BattleEventManager の抽選間隔（tickInterval・ゲーム秒）をQA個体にだけ上書きする。0以下＝コンポーネント既定のまま。" +
                 "開始時に適用。★間隔の変更は無効化でも強制発火でもない（抽選と発火可否は製品側の自然抽選。2回目以降は製品側の下限5秒）")]
        public float eventTickIntervalOverride = 0f;

        [Header("援軍（機能スイッチ ON 時のみ・SPEED-08）")]
        [Tooltip("開始から時限増援が到着するまでのゲーム秒（QA所有の BattleSetup の予約1件の reinforcementDelay）。OFF でも同じ時刻を記録して比較に使う")]
        public float reinforcementArrivalSeconds = BattleQaReinforcementPlan.DefaultArrivalSeconds;

        [Header("画面表示")]
        [Tooltip("状態パネルを画面左上に出す")]
        public bool showOverlay = true;

        private const float OverlayWidth = 560f;
        private const float OverlayLineHeight = 18f;
        private const float MoraleFloorEpsilon = 0.001f;
        /// <summary>QAローカルの通知の保持上限（超えた分は数だけ残す）。</summary>
        private const int MaxQaNotifications = 200;
        /// <summary>時限増援の到着の許容（BattleSetup の経過と開始からの経過は最大1フレームずれる）。</summary>
        private const float ArrivalTolerance = 0.05f;

        // ===== 公開状態 =====

        /// <summary>進行中のセッション（無ければ null）。</summary>
        public static ReproducibleBattleQaSession Active { get; private set; }

        /// <summary>直近の結果ログ（終了後もメニューで出力できるよう残す）。</summary>
        public static BattleQaRunLog LastLog { get; private set; }

        /// <summary>直近の初期スナップショット（2回初期化の一致確認用）。</summary>
        public static BattleQaSnapshot LastSnapshot { get; private set; }

        /// <summary>直近の終了時に戻した内容（試験・報告用）。</summary>
        public static string LastRestoreReport { get; private set; } = "";

        private static int serial;

        public BattleQaPreset Preset { get; private set; }
        /// <summary>検証用調整プリセット（SPEED-07。null は渡さない＝既定に置換）。</summary>
        public BattleQaTuningProfile Tuning { get; private set; }
        /// <summary>検証用機能スイッチ（SPEED-08。null は渡さない＝固定QAの既定に置換）。</summary>
        public BattleQaFeatureSwitches Switches { get; private set; }
        public Phase CurrentPhase { get; private set; } = Phase.準備中;
        public string FailureReason { get; private set; } = "";
        public BattleQaRunLog Log { get; private set; }
        public BattleQaSnapshot InitialSnapshot { get; private set; }
        /// <summary>このQAシーンの PauseManager（一時停止の唯一の窓口）。</summary>
        public PauseManager PauseCtl { get; private set; }
        /// <summary>このセッションの使い捨てシーン。</summary>
        public Scene QaScene => qaScene;
        public bool IsReady => CurrentPhase == Phase.準備完了;
        public bool IsObservationDone => CurrentPhase == Phase.観測完了;

        /// <summary>PauseManager の停止状態と timeScale が一致しているか。</summary>
        public bool IsPauseConsistent => PauseCtl != null && PauseCtl.IsPaused == (Time.timeScale == 0f);

        /// <summary>隔離の条件（画面・ログに出す）。</summary>
        public IReadOnlyList<string> IsolationNotes => isolationNotes;

        /// <summary>調整プリセットを当てた1艦隊1項目ぶんの記録（基準＝適用前の実効値）。</summary>
        public readonly struct TuningRecord
        {
            public readonly int fleetId;
            public readonly BattleQaTuningField field;
            public readonly float baseline;
            public readonly float applied;

            public TuningRecord(int fleetId, BattleQaTuningField field, float baseline, float applied)
            {
                this.fleetId = fleetId;
                this.field = field;
                this.baseline = baseline;
                this.applied = applied;
            }
        }

        /// <summary>準備で当てた調整（固定ID昇順・項目順。未指定項目も基準＝適用で記録）。</summary>
        public IReadOnlyList<TuningRecord> AppliedTuning => appliedTuning;

        /// <summary>このQAの軍団長AI（プリセットが軍団長AIを使わなければ null）。軍団隊形間隔の適用先。</summary>
        public BattlefieldCommandManager CommandManager => commandManager;

        /// <summary>軍団隊形間隔の適用先があるか（＝軍団長AIを組んだ）。</summary>
        public bool HasCorpsSpacingTarget { get; private set; }
        /// <summary>適用前の最小間隔（BattlefieldCommandManager.corpsMinSpacing の実効値）。</summary>
        public float CorpsMinSpacingBaseline { get; private set; }
        /// <summary>適用した最小間隔（未指定は基準のまま）。★実間隔ではない＝実間隔は軍団ごとに <see cref="BattlefieldCommandManager.TryGetCorpsSpacing"/>。</summary>
        public float CorpsMinSpacingApplied { get; private set; }

        /// <summary>このQAが所有する会戦イベント（戦況イベント=ON のときだけ。開始まで無効）。</summary>
        public BattleEventManager QaEventManager => eventManager;
        /// <summary>機能スイッチを当てた実時間（realtimeSinceStartup・準備中＝開始前。未適用は負）。</summary>
        public float SwitchesAppliedRealtime { get; private set; } = -1f;
        /// <summary>適用前の自動包囲（QAの軍団長AIの既定値。軍団長AIが無ければ false）。</summary>
        public bool EnvelopmentBaseline { get; private set; }
        /// <summary>直近の終了で機能スイッチの対象をどう片付けたか（試験・報告用）。</summary>
        public static string LastSwitchDisposal { get; private set; } = "";

        /// <summary>QA同盟艦隊の陣営（プリセットの味方。戦況イベントの対象・援軍の陣営に使う）。</summary>
        public Faction AlliedFaction { get; private set; } = Faction.同盟;
        /// <summary>援軍の明細（ON/OFF とも準備で決める。OFF は予約しない＝比較用の時刻だけ）。</summary>
        public BattleQaReinforcementPlan ReinforcementPlan { get; private set; }
        /// <summary>QA所有の BattleSetup（援軍=ON のときだけ。開始まで無効）。</summary>
        public BattleSetup QaReinforcementSetup => reinforcementSetup;
        /// <summary>時限増援で出現した最初の援軍艦隊（無ければ null）。</summary>
        public FleetStrength ReinforcementFleet => reinforcementFleets.Count > 0 ? reinforcementFleets[0] : null;
        /// <summary>QAが受け取った出現の回数（BattleSetup の生成を1件ずつ受領）。</summary>
        public int ReinforcementSpawnCount { get; private set; }
        /// <summary>到着時刻より前に出現した回数。</summary>
        public int ReinforcementEarlySpawnCount { get; private set; }
        /// <summary>最初の出現の開始からの経過ゲーム秒（未出現は負）。</summary>
        public float ReinforcementSpawnElapsed { get; private set; } = -1f;
        /// <summary>最初の出現位置。</summary>
        public Vector2 ReinforcementSpawnPosition { get; private set; }
        /// <summary>QA所有の会戦イベント・援軍の通知（共有の NotificationCenter へは送らない）。</summary>
        public IReadOnlyList<string> QaNotifications => qaNotifications;
        /// <summary>保持上限を超えて一覧から省いた通知の数。</summary>
        public int QaNotificationsDropped { get; private set; }
        /// <summary>直近の終了で、QA中に共有の通知履歴（NotificationCenter.LastSeq）が進んだ件数（QA所有の通知は含まない）。</summary>
        public static long LastSharedNotificationDelta { get; private set; }
        /// <summary>直近の終了で、戦略の援軍台帳（StrategySession.Reinforcements）が参照・件数とも変わっていないか。</summary>
        public static bool LastStrategyLedgerUnchanged { get; private set; } = true;

        // ===== 内部 =====

        private readonly SortedDictionary<int, FleetStrength> fleets = new SortedDictionary<int, FleetStrength>();
        private readonly List<string> isolationNotes = new List<string>();
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly List<AdmiralData> tempAdmirals = new List<AdmiralData>();
        private readonly List<TuningRecord> appliedTuning = new List<TuningRecord>();
        private GameObject commandManagerGo;
        private BattlefieldCommandManager commandManager;
        private GameObject eventManagerGo;
        private BattleEventManager eventManager;
        private GameObject reinforcementSetupGo;
        private BattleSetup reinforcementSetup;
        private GameObject reinforcementTemplateRoot;
        private readonly List<FleetStrength> reinforcementFleets = new List<FleetStrength>();
        private readonly List<GameObject> reinforcementFleetGos = new List<GameObject>();
        private readonly List<string> reinforcementShipNames = new List<string>();
        private readonly List<string> qaNotifications = new List<string>();
        private Scene qaScene;
        private Scene previousActiveScene;
        private float startedAt;

        // 元へ戻す値
        private bool ended;
        private bool stateSaved;
        private bool stateRestored;
        private float savedTimeScale;
        private Random.State savedRandom;
        private bool savedAuditEnabled;
        private float savedAuditMinAbsDelta;
        private int auditBaselineTotal;
        private int auditBaselineDropped;
        private long savedNotificationSeq;
        private WarpReinforcementLedger savedLedger;
        private int savedLedgerPending;
        private int savedLedgerClosed;

        /// <summary>固定IDで艦隊を引く（無ければ null）。</summary>
        public FleetStrength Fleet(int fleetId) => fleets.TryGetValue(fleetId, out FleetStrength f) ? f : null;

        /// <summary>開始からのゲーム内経過秒（開始前は0）。</summary>
        public float Elapsed => CurrentPhase == Phase.実行中 || CurrentPhase == Phase.観測完了 ? Time.time - startedAt : 0f;

        // ===================================================================
        // 入口
        // ===================================================================

        /// <summary>
        /// プリセットで準備を始める（Play 中のみ）。準備はコルーチンで進み、
        /// 完了すると <see cref="CurrentPhase"/> が 準備完了、失敗すると 準備失敗（状態は戻して片付け済み）になる。
        /// 既にセッションがあれば null（先に <see cref="End"/> か <see cref="Retry"/>）。
        /// </summary>
        public static ReproducibleBattleQaSession Begin(BattleQaPreset preset)
            => Begin(preset, BattleQaTuningProfile.Default);

        /// <summary>
        /// 調整プリセットつきで準備を始める（SPEED-07）。調整は準備中に使い捨て艦隊のコンポーネントへだけ当てる
        /// （アセット・セーブ・ゲーム既定には書かない）。null は既定（全項目未指定）。不正な指定は準備失敗。
        /// </summary>
        public static ReproducibleBattleQaSession Begin(BattleQaPreset preset, BattleQaTuningProfile tuning)
            => Begin(preset, tuning, BattleQaFeatureSwitches.FixedDefault);

        /// <summary>
        /// 機能スイッチつきで準備を始める（SPEED-08）。スイッチは準備中＝開始前にだけ当て、途中切替はしない。
        /// null は固定QAの既定（自動包囲ON・援軍OFF・戦況イベントOFF）。未接続の項目を ON にしたら準備失敗。
        /// </summary>
        public static ReproducibleBattleQaSession Begin(BattleQaPreset preset, BattleQaTuningProfile tuning, BattleQaFeatureSwitches switches)
        {
            if (!Application.isPlaying) { Debug.LogWarning("［固定会戦QA］Play 中に準備してください。"); return null; }
            if (Active != null) { Debug.LogWarning("［固定会戦QA］既にセッションがあります。終了か再試行を使ってください。"); return null; }
            if (preset == null) { Debug.LogWarning("［固定会戦QA］プリセットがありません。"); return null; }

            serial++;
            Scene prev = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene(SceneNamePrefix + serial.ToString("000"));

            var go = new GameObject("QA_ReproducibleBattleSession");
            SceneManager.MoveGameObjectToScene(go, scene);
            var session = go.AddComponent<ReproducibleBattleQaSession>();
            session.qaScene = scene;
            session.previousActiveScene = prev;
            session.Preset = preset;
            session.Tuning = tuning ?? BattleQaTuningProfile.Default;
            session.Switches = switches ?? BattleQaFeatureSwitches.FixedDefault;
            Active = session;
            session.SaveGlobalState();
            session.StartCoroutine(session.PrepareRoutine());
            return session;
        }

        /// <summary>同じプリセット・同じ seed・同じ調整プリセット・同じ機能スイッチで最初からやり直す（前の実行の状態は戻して片付け、調整とスイッチは新しい対象へ当て直す）。</summary>
        public static ReproducibleBattleQaSession Retry()
        {
            if (Active == null) { Debug.LogWarning("［固定会戦QA］再試行するセッションがありません。"); return null; }
            BattleQaPreset preset = Active.Preset;
            BattleQaTuningProfile tuning = Active.Tuning;
            BattleQaFeatureSwitches switches = Active.Switches;
            float tickOverride = Active.eventTickIntervalOverride;
            float arrival = Active.reinforcementArrivalSeconds;
            Active.End("再試行");
            ReproducibleBattleQaSession s = Begin(preset, tuning, switches);
            if (s != null)
            {
                s.eventTickIntervalOverride = tickOverride;
                s.reinforcementArrivalSeconds = arrival;   // 援軍の明細は準備の2フレーム目に決める＝同じフレームで設定すれば効く
            }
            return s;
        }

        /// <summary>準備完了から開始する（命令を出し、AI・武装・軍団長AIを有効化して時間を進める）。</summary>
        public bool StartRun()
        {
            if (CurrentPhase != Phase.準備完了)
            {
                Debug.LogWarning("［固定会戦QA］準備完了ではないので開始できません（現在＝" + CurrentPhase + "）。");
                return false;
            }

            CurrentPhase = Phase.実行中;
            IssueOpeningCommands();

            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                if (kv.Value == null) continue;
                var w = kv.Value.GetComponent<FleetWeapon>();
                if (w != null) w.enabled = true;
                if (Preset.TryGetFleet(kv.Key, out BattleQaFleetSpec spec))
                {
                    var ai = kv.Value.GetComponent<FleetAI>();
                    if (ai != null) ai.enabled = spec.aiEnabled;
                }
            }
            if (commandManager != null) commandManager.enabled = true;
            // 援軍=ON：QA所有の BattleSetup を有効化＝以後の Update が game-time で経過を数え、到着で実生成する。
            if (reinforcementSetup != null) reinforcementSetup.enabled = true;
            if (eventManager != null)
            {
                // 間隔の上書きは Start（有効化で走る）より前に当てる＝初回の抽選時刻に効く。
                if (eventTickIntervalOverride > 0f) eventManager.tickInterval = eventTickIntervalOverride;
                eventManager.enabled = true;
            }

            PauseCtl.SetTimeScale(Preset.timeScale);
            PauseCtl.Resume();
            startedAt = Time.time;
            Log.Add(0f, "開始（timeScale=" + Time.timeScale.ToString("0.##") + " / PauseManager 一致=" + IsPauseConsistent + "）");
            Log.Add(0f, DescribeTuningReadback());
            Log.Add(0f, DescribeFeatureSwitchReadback());
            StartCoroutine(ScenarioRoutine());
            if (commandManager != null) StartCoroutine(LogCorpsSpacingWhenResolved());
            return true;
        }

        /// <summary>
        /// 終了：実行を止め、艦隊を片付け、Random.state・timeScale・台帳設定・隔離を元へ戻し、使い捨てシーンを閉じる。
        /// 何度呼んでもよい。
        /// </summary>
        public void End(string reason)
        {
            if (ended) return;
            ended = true;
            StopAllCoroutines();
            if (CurrentPhase != Phase.準備失敗) CurrentPhase = Phase.終了;
            if (Log != null) Log.Add(Elapsed, "終了（理由＝" + (reason ?? "") + "）" + AuditSummary());
            RecordSwitchDisposal(false);

            CleanupWorld(true);
            RestoreGlobalState(true);
            if (Active == this) Active = null;

            // シーンを閉じる（最後の1枚は閉じられないので残し、その旨を出す）。
            if (qaScene.IsValid() && qaScene.isLoaded)
            {
                if (SceneManager.sceneCount > 1) SceneManager.UnloadSceneAsync(qaScene);
                else
                {
                    Debug.LogWarning("［固定会戦QA］使い捨てシーンが最後の1枚のため閉じられません（中身は片付け済み）。");
                    Destroy(gameObject);
                }
            }
        }

        // ===================================================================
        // 準備
        // ===================================================================

        private IEnumerator PrepareRoutine()
        {
            CurrentPhase = Phase.準備中;
            string suffix = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Log = new BattleQaRunLog(BattleQaRunLog.FormatRunId(Preset.name, Preset.seed, serial, suffix), Preset.name, Preset.seed);
            LastLog = Log;
            LastSnapshot = null;
            Log.Add(0f, "準備開始：AI=" + Preset.AiMode + " / 軍団長AI=" + (Preset.useCorpsCommandManager ? "あり" : "なし") +
                        " / 開始後timeScale=" + Preset.timeScale.ToString("0.##") + " / 命令＝" + Preset.commandPlan);

            List<string> errors = Preset.Validate();
            if (errors.Count > 0) { Fail("プリセットの矛盾：" + string.Join("／", errors)); yield break; }

            // 調整プリセット（SPEED-07）：設定名・版を先に残し、不正な指定は何も組まずに準備失敗。
            Log.Add(0f, Tuning.Describe());
            List<string> tuningErrors = Tuning.Validate();
            if (tuningErrors.Count > 0) { Fail("調整プリセットの不正：" + string.Join("／", tuningErrors)); yield break; }

            // 機能スイッチ（SPEED-08）：設定名・版・各スイッチ・分類を先に残し、未接続の ON 等は何も組まずに準備失敗。
            Log.Add(0f, Switches.Describe());
            List<string> switchErrors = Switches.Validate();
            if (switchErrors.Count > 0) { Fail("機能スイッチの不正：" + string.Join("／", switchErrors)); yield break; }
            if (!BattleQaReinforcementPlan.TryResolveAlliedFaction(Preset, out Faction allied) &&
                (Switches.Get(BattleQaFeature.援軍) || Switches.Get(BattleQaFeature.戦況イベント)))
            { Fail("機能スイッチの不正：プリセットに味方の艦隊が無い（援軍・戦況イベントの対象勢力を合わせられない）"); yield break; }
            AlliedFaction = allied;
            if (float.IsNaN(eventTickIntervalOverride) || float.IsInfinity(eventTickIntervalOverride))
            { Fail("機能スイッチの不正：戦況イベントの抽選間隔の上書きが有限でない（" + eventTickIntervalOverride + "）"); yield break; }

            // ★通常会戦と混ぜない：既存の艦隊がいれば準備しない（索敵・勝敗が交ざる）。
            int foreign = FleetRegistry.AllFlagships.Count;
            if (foreign > 0)
            {
                Fail("既存の艦隊が " + foreign + " 隊います（通常会戦と混ざるため準備しない）。Title シーンから Play して準備してください");
                yield break;
            }

            SceneManager.SetActiveScene(qaScene);
            Isolate();

            // 士気の原因台帳（既存QA）を有効にする。★既存の記録は消さない（件数の基準だけ控える）。
            auditBaselineTotal = MoraleAuditLog.Count + MoraleAuditLog.Dropped;
            auditBaselineDropped = MoraleAuditLog.Dropped;
            MoraleAuditLog.Enabled = true;
            Log.Add(0f, "士気の原因台帳：有効化（既存 " + MoraleAuditLog.Count + " 件は保持・しきい値 " +
                        MoraleAuditLog.MinAbsDelta.ToString("0.####") + "）");

            // 一時停止（PauseManager の Start が既定倍速を当てるので、1フレーム待ってから止める）。
            var pmGo = new GameObject("QA_PauseManager");
            SceneManager.MoveGameObjectToScene(pmGo, qaScene);
            PauseCtl = pmGo.AddComponent<PauseManager>();
            yield return null;
            if (PauseCtl == null || !PauseCtl.enabled) { Fail("PauseManager が有効になりませんでした（アクティブシーン不一致）"); yield break; }
            PauseCtl.Pause();

            if (Preset.useCorpsCommandManager)
            {
                commandManagerGo = new GameObject("QA_BattlefieldCommandManager");
                SceneManager.MoveGameObjectToScene(commandManagerGo, qaScene);
                commandManager = commandManagerGo.AddComponent<BattlefieldCommandManager>();
                commandManager.enabled = false;   // 開始まで判断させない
            }

            // 戦況イベント=ON：実コンポーネントを QA シーンに1つだけ置く（許可はこのシーン限り・終了で解除）。開始まで抽選させない。
            if (Switches.Get(BattleQaFeature.戦況イベント))
            {
                BattleEventManager.AllowQaHostScene(qaScene);
                eventManagerGo = new GameObject("QA_BattleEventManager");
                SceneManager.MoveGameObjectToScene(eventManagerGo, qaScene);
                eventManager = eventManagerGo.AddComponent<BattleEventManager>();   // Awake のシーン名ガードはここで評価される
                if (eventManager != null)
                {
                    eventManager.enabled = false;
                    // 対象はQAの同盟艦隊に明示（GameSettings.playerFaction は書き換えない）。通知はQAローカルログへ（共有の通知履歴へ送らない）。
                    eventManager.SetTargetFaction(AlliedFaction);
                    eventManager.NotificationSink = ReceiveQaNotification;
                }
            }

            // 援軍：明細は ON/OFF とも決める（OFF は予約しない＝同じ時刻を比較用に記録するだけ）。
            ReinforcementPlan = BattleQaReinforcementPlan.ForPreset(Preset, reinforcementArrivalSeconds);
            if (Switches.Get(BattleQaFeature.援軍))
            {
                string reinforcementFailure = PrepareReinforcement();
                if (reinforcementFailure != null) { Fail(reinforcementFailure); yield break; }
            }

            // 固定ID順に組む（Preset.Fleets は ID 昇順）。
            IReadOnlyList<BattleQaFleetSpec> specs = Preset.Fleets;
            for (int i = 0; i < specs.Count; i++) BuildFleet(specs[i]);

            yield return null;   // 全部品の Awake/Start（停止中でも走る）

            // ガードの自壊（Destroy は遅延）を1フレーム後に確かめる＝ON なのに実コンポーネントが居ない、を黙って通さない。
            if (Switches.Get(BattleQaFeature.戦況イベント) && (eventManager == null || eventManagerGo == null))
            {
                Fail("戦況イベント=ON だが BattleEventManager が QA シーンに残らなかった（シーン名ガードで自壊＝QAの許可が効いていない）");
                yield break;
            }
            // 援軍=ON：設定値でなく読み戻しで確かめる（予約1件・未生成・予定外の艦隊0・テンプレートは盤面に出ていない）。
            if (Switches.Get(BattleQaFeature.援軍))
            {
                if (reinforcementSetup == null || reinforcementSetup.PendingReinforcementCount != 1 ||
                    reinforcementSetup.SpawnedReinforcementCount != 0 || ReinforcementSpawnCount != 0 || CountUnplannedFleetsInQaScene() != 0)
                {
                    Fail("援軍=ON だが準備後の読み戻しが一致しない（BattleSetup=" + (reinforcementSetup != null) +
                         " 予約=" + (reinforcementSetup != null ? reinforcementSetup.PendingReinforcementCount : -1) +
                         " 生成=" + (reinforcementSetup != null ? reinforcementSetup.SpawnedReinforcementCount : -1) +
                         " 予定外の艦隊=" + CountUnplannedFleetsInQaScene() + "）");
                    yield break;
                }
            }

            // 初期条件を当て直す（Start が上書きしうる値を明細どおりに戻す）。
            for (int i = 0; i < specs.Count; i++) ApplyInitialConditions(specs[i]);

            // 調整プリセットを当てる（Awake/Start の後＝起動処理に上書きされない）。適用値が不正なら準備失敗。
            string tuningFailure = ApplyTuning();
            if (tuningFailure != null) { Fail("調整プリセットの適用値が不正：" + tuningFailure); yield break; }

            ApplyFeatureSwitches();

            if (!IsPauseConsistent || Time.timeScale != 0f)
            {
                PauseCtl.Pause();
                if (!IsPauseConsistent) { Fail("一時停止と timeScale が一致しません"); yield break; }
            }

            // 決定論的な乱数の初期化（QAセッション内だけ）。★組み立ての<b>後</b>に行う＝
            // 初回だけ生成される常駐物（GameSettings 等）の乱数消費に左右されず、開始時点の乱数列が seed だけで決まる。
            Random.InitState(Preset.seed);
            Log.Add(0f, "Random.InitState(" + Preset.seed + ")（準備完了の直前。終了時に元の Random.state へ戻す）");

            InitialSnapshot = CaptureSnapshot();
            List<string> diffs = BattleQaSnapshot.CompareWithPreset(Preset, InitialSnapshot, BattleQaTolerance.Default);
            if (diffs.Count > 0) { Fail("初期値が明細と一致しません：" + string.Join("／", diffs)); yield break; }

            LastSnapshot = InitialSnapshot;
            CurrentPhase = Phase.準備完了;
            Log.Add(0f, InitialSnapshot.Describe());
            Log.Add(0f, "準備完了（一時停止中・PauseManager 一致=" + IsPauseConsistent + "）。開始で命令を出します");
        }

        private void BuildFleet(BattleQaFleetSpec spec)
        {
            var go = new GameObject("QA固定艦隊_" + spec.fleetId);
            SceneManager.MoveGameObjectToScene(go, qaScene);
            go.SetActive(false);   // ★全部品が揃うまで Awake を遅らせる（FleetStrength が FleetMorale を掴めるように）
            go.transform.position = new Vector3(spec.position.x, spec.position.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, spec.headingDeg);

            var strength = go.AddComponent<FleetStrength>();
            strength.faction = spec.faction;
            strength.admiralName = spec.admiralName;
            strength.fleetNumber = spec.fleetId;
            strength.corpsName = spec.corpsName;
            strength.strength = spec.shipCount;
            strength.maxStrength = spec.maxShipCount;
            if (spec.role == BattleQaCommandRole.軍団長)
            {
                // 使い捨ての軍団長データ（アセット化しない・終了時に破棄）。
                var ad = ScriptableObject.CreateInstance<AdmiralData>();
                ad.hideFlags = HideFlags.DontSave;
                ad.admiralName = spec.admiralName;
                ad.leadership = spec.leadership;
                ad.ambition = spec.ambition;
                ad.rankTier = CommandCapacityRules.CommanderTierFor(EchelonType.軍団);
                tempAdmirals.Add(ad);
                strength.corpsCommander = ad;
            }

            go.AddComponent<FleetMorale>();
            go.AddComponent<FleetMovement>();
            go.AddComponent<WeaponArc>();
            var weapon = go.AddComponent<FleetWeapon>();
            weapon.enabled = false;   // 開始まで撃たせない（停止中の初回発砲で初期値が変わらないように）
            var sq = go.AddComponent<Squadron>();
            sq.escortCount = 0;
            sq.currentFormation = spec.formation;
            var ai = go.AddComponent<FleetAI>();
            ai.enabled = false;       // 開始まで判断させない（有効化は明細の aiEnabled どおり）

            go.SetActive(true);
            fleets[spec.fleetId] = strength;
        }

        private void ApplyInitialConditions(BattleQaFleetSpec spec)
        {
            FleetStrength f = Fleet(spec.fleetId);
            if (f == null) return;
            f.strength = spec.shipCount;
            f.maxStrength = spec.maxShipCount;
            var sq = f.GetComponent<Squadron>();
            if (sq != null) sq.currentFormation = spec.formation;
            var mo = f.GetComponent<FleetMorale>();
            if (mo != null && Mathf.Abs(mo.morale - spec.initialMorale) > MoraleFloorEpsilon)
                mo.ApplyMoraleDelta(spec.initialMorale - mo.morale, MoraleChangeSource.初期化, "固定会戦QAの初期条件");
        }

        // ===== 調整プリセット（SPEED-07） =====

        private static readonly BattleQaTuningField[] AppliedFields =
        {
            BattleQaTuningField.移動速度, BattleQaTuningField.回頭速度,
            BattleQaTuningField.士気回復量, BattleQaTuningField.敗走回復待ち,
        };

        /// <summary>コンポーネントの既存 public 項目を読む（無ければ false）。</summary>
        private static bool TryReadField(FleetStrength f, BattleQaTuningField field, out float value)
        {
            value = 0f;
            if (f == null) return false;
            switch (field)
            {
                case BattleQaTuningField.移動速度:
                case BattleQaTuningField.回頭速度:
                {
                    var mv = f.GetComponent<FleetMovement>();
                    if (mv == null) return false;
                    value = field == BattleQaTuningField.移動速度 ? mv.maxSpeed : mv.rotationSpeed;
                    return true;
                }
                case BattleQaTuningField.士気回復量:
                case BattleQaTuningField.敗走回復待ち:
                {
                    var mo = f.GetComponent<FleetMorale>();
                    if (mo == null) return false;
                    value = field == BattleQaTuningField.士気回復量 ? mo.recoveryRate : mo.routedRecoveryDelay;
                    return true;
                }
                default:
                    return false;
            }
        }

        private static void WriteField(FleetStrength f, BattleQaTuningField field, float value)
        {
            switch (field)
            {
                case BattleQaTuningField.移動速度: f.GetComponent<FleetMovement>().maxSpeed = value; break;
                case BattleQaTuningField.回頭速度: f.GetComponent<FleetMovement>().rotationSpeed = value; break;
                case BattleQaTuningField.士気回復量: f.GetComponent<FleetMorale>().recoveryRate = value; break;
                case BattleQaTuningField.敗走回復待ち: f.GetComponent<FleetMorale>().routedRecoveryDelay = value; break;
            }
        }

        /// <summary>
        /// 使い捨て艦隊へ調整を当て、艦隊ごとに基準（適用前の実効値）→適用値を記録する。
        /// ★先に全件の適用値を検証し、1件でも不正なら何も書かずに理由を返す（部分適用を残さない）。null＝成功。
        /// </summary>
        private string ApplyTuning()
        {
            appliedTuning.Clear();

            // 軍団隊形間隔＝軍団長AIの最小間隔（艦隊ごとでなく軍団長AIに1つ）。適用先が無いのに指定されたら準備失敗。
            BattleQaTuningOverride spacingOv = Tuning.Get(BattleQaTuningField.軍団隊形間隔);
            HasCorpsSpacingTarget = commandManager != null;
            CorpsMinSpacingBaseline = HasCorpsSpacingTarget ? commandManager.corpsMinSpacing : 0f;
            CorpsMinSpacingApplied = CorpsMinSpacingBaseline;
            if (HasCorpsSpacingTarget)
            {
                float applied = spacingOv.Resolve(CorpsMinSpacingBaseline);
                string err = BattleQaTuningProfile.ValidateResolved(BattleQaTuningField.軍団隊形間隔, applied);
                if (err != null) return "軍団長AI：" + err + "（基準 " + CorpsMinSpacingBaseline.ToString("0.####") + "）";
                CorpsMinSpacingApplied = applied;
            }
            else if (spacingOv.IsSet)
            {
                return "軍団隊形間隔：このプリセットは軍団長AI（BattlefieldCommandManager）を使わないため適用先が無い";
            }

            var pending = new List<TuningRecord>();
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                for (int i = 0; i < AppliedFields.Length; i++)
                {
                    BattleQaTuningField field = AppliedFields[i];
                    if (!TryReadField(kv.Value, field, out float baseline))
                        return "艦隊" + kv.Key + " に " + BattleQaTuningProfile.ComponentFieldName(field) + " が無い";
                    float applied = Tuning.Get(field).Resolve(baseline);
                    string err = BattleQaTuningProfile.ValidateResolved(field, applied);
                    if (err != null) return "艦隊" + kv.Key + "：" + err + "（基準 " + baseline.ToString("0.####") + "）";
                    pending.Add(new TuningRecord(kv.Key, field, baseline, applied));
                }
            }

            var sb = new StringBuilder("調整の適用（基準＝コンポーネントの実効値→適用値。速度・回頭は機動補正を掛ける前の基準項目）：");
            int lastId = int.MinValue;
            for (int i = 0; i < pending.Count; i++)
            {
                TuningRecord r = pending[i];
                if (Tuning.Get(r.field).IsSet) WriteField(Fleet(r.fleetId), r.field, r.applied);
                appliedTuning.Add(r);
                if (r.fleetId != lastId) { sb.Append("\n  艦隊").Append(r.fleetId).Append(' '); lastId = r.fleetId; }
                sb.Append(BattleQaTuningProfile.ComponentFieldName(r.field)).Append('=')
                  .Append(r.baseline.ToString("0.####"));
                if (Tuning.Get(r.field).IsSet) sb.Append("→").Append(r.applied.ToString("0.####"));
                sb.Append("　");
            }
            sb.Append("\n  軍団隊形間隔：").Append(BattleQaTuningProfile.ComponentFieldName(BattleQaTuningField.軍団隊形間隔));
            if (HasCorpsSpacingTarget)
            {
                if (spacingOv.IsSet) commandManager.corpsMinSpacing = CorpsMinSpacingApplied;
                sb.Append("（最小間隔）=").Append(CorpsMinSpacingBaseline.ToString("0.####"));
                if (spacingOv.IsSet) sb.Append("→").Append(CorpsMinSpacingApplied.ToString("0.####"));
                sb.Append("。実間隔＝Max(最小間隔, 2×最大占有半径＋").Append(CorpsSpacingRules.FootprintMargin.ToString("0.##"))
                  .Append(")＝開始後に軍団ごとに算出して記録（占有半径の下限を割って重ねない）");
            }
            else
            {
                sb.Append("＝適用先なし（軍団長AIを使わないプリセット）");
            }
            Log.Add(0f, sb.ToString());
            return null;
        }

        // ===== 機能スイッチ（SPEED-08） =====

        /// <summary>
        /// 開始前に機能スイッチを当てて記録する。適用先はQAが作った使い捨ての対象だけ：
        /// 自動包囲＝QAの軍団長AI、戦況イベント＝QAシーン所有の BattleEventManager（準備で生成済み）、援軍＝QAシーン所有の BattleSetup の時限増援の予約（準備で作成済み・OFF は予約しない）。
        /// </summary>
        private void ApplyFeatureSwitches()
        {
            SwitchesAppliedRealtime = Time.realtimeSinceStartup;
            var sb = new StringBuilder("機能スイッチの適用（準備中＝開始前・途中切替なし・実時間 ")
                .Append(SwitchesAppliedRealtime.ToString("0.00")).Append(" 秒）：");

            bool env = Switches.Get(BattleQaFeature.自動包囲);
            sb.Append("\n  自動包囲=").Append(BattleQaFeatureSwitches.OnOff(env)).Append("：");
            if (commandManager != null)
            {
                EnvelopmentBaseline = commandManager.autoEnvelopment;
                commandManager.autoEnvelopment = env;
                sb.Append("BattlefieldCommandManager.autoEnvelopment ").Append(EnvelopmentBaseline).Append("→").Append(env)
                  .Append("（QAの軍団長AIのみ。AI全体・カウンター遮蔽・決戦・総退却は止めない）");
            }
            else
            {
                EnvelopmentBaseline = false;
                sb.Append("適用先なし（軍団長AIを使わないプリセット＝包囲の経路はもとから動かない。").Append(env ? "ON でも包囲は起きない" : "OFF は構造上成立").Append("）");
            }

            bool rei = Switches.Get(BattleQaFeature.援軍);
            sb.Append("\n  援軍=").Append(BattleQaFeatureSwitches.OnOff(rei)).Append("：");
            if (rei)
                sb.Append(BattleQaFeatureSwitches.ReinforcementConnectionNote).Append("。予約=")
                  .Append(reinforcementSetup != null ? reinforcementSetup.PendingReinforcementCount : 0).Append(" 件（開始まで無効）");
            else
                sb.Append("予約しない（QAシーンに BattleSetup を置かない。比較用の到着時刻＝開始から ")
                  .Append(ReinforcementPlan != null ? ReinforcementPlan.arrivalSeconds.ToString("0.##") : "-").Append(" 秒）");
            sb.Append("。戦略の援軍台帳 StrategySession.Reinforcements は触らない（BattleManager は隔離で停止）");

            bool evt = Switches.Get(BattleQaFeature.戦況イベント);
            sb.Append("\n  戦況イベント=").Append(BattleQaFeatureSwitches.OnOff(evt)).Append("：");
            if (evt) sb.Append("QAシーン所有の BattleEventManager を生成（開始まで無効・自然抽選＝強制発火しない）。対象勢力＝QA同盟艦隊の ")
                       .Append(AlliedFaction).Append("（明示指定・GameSettings.playerFaction は書き換えない）。通知＝QAローカルログ（共有の通知履歴へ送らない）");
            else sb.Append("QAシーンに BattleEventManager を置かない（他シーンの既存個体は隔離で停止）");

            sb.Append("\n  分類＝").Append(Switches.Classification);
            Log.Add(0f, sb.ToString());
        }

        // ===== 援軍（SPEED-08・BattleSetup の時限増援を実経路で使う） =====

        /// <summary>
        /// 援軍=ON の準備：QA所有のテンプレート・一時提督・BattleSetup を作り、時限増援を1件予約する（null＝成功）。
        /// ★生成は製品の BattleSetup.Update→SpawnReinforcement→SpawnFleet に任せる（QAは艦隊を作らない）。
        /// ★予約は QA の BattleSetup にだけ持たせ、戦略の援軍台帳 StrategySession.Reinforcements には触れない。
        /// </summary>
        private string PrepareReinforcement()
        {
            List<string> errors = ReinforcementPlan.Validate(Preset);
            if (errors.Count > 0) return "援軍の明細の不正：" + string.Join("／", errors);

            BattleSetup.AllowQaHostScene(qaScene);

            // テンプレート：非アクティブの親の下に置く＝Awake が走らず索敵にも載らない。Instantiate の複製は親無し＝有効で生まれる。
            reinforcementTemplateRoot = new GameObject("QA_ReinforcementTemplateRoot");
            SceneManager.MoveGameObjectToScene(reinforcementTemplateRoot, qaScene);
            reinforcementTemplateRoot.SetActive(false);
            var template = new GameObject("QA援軍テンプレート");
            template.transform.SetParent(reinforcementTemplateRoot.transform, false);
            template.AddComponent<FleetStrength>();
            template.AddComponent<FleetMorale>();
            template.AddComponent<FleetMovement>();
            template.AddComponent<WeaponArc>();
            template.AddComponent<FleetWeapon>();
            var sq = template.AddComponent<Squadron>();
            sq.escortCount = 0;
            sq.currentFormation = ReinforcementPlan.formation;
            template.AddComponent<FleetAI>().enabled = false;   // 生成時に SpawnFleet が有効化する

            // 使い捨ての提督（アセット化しない・終了時に破棄）。統率は中立＝艦艇数は明細どおり。
            var ad = ScriptableObject.CreateInstance<AdmiralData>();
            ad.hideFlags = HideFlags.DontSave;
            ad.admiralName = "QA援軍";
            ad.faction = ReinforcementPlan.faction;
            ad.leadership = BattleQaReinforcementPlan.NeutralStat;
            tempAdmirals.Add(ad);

            // 予約（艦隊番号0＝艦隊台帳・編制ツリーに登録しない。固定ID・軍団は出現を受領したときQA艦隊にだけ付ける）。
            var entry = new ScenarioData.FleetEntry
            {
                admiral = ad,
                faction = ReinforcementPlan.faction,
                spawnPosition = new Vector2(0f, ReinforcementPlan.spawnY),
                formation = ReinforcementPlan.formation,
                baseStrength = ReinforcementPlan.shipCount,
                fleetNumber = 0,
                reinforcementDelay = ReinforcementPlan.arrivalSeconds,
            };

            reinforcementSetupGo = new GameObject("QA_BattleSetup_援軍");
            SceneManager.MoveGameObjectToScene(reinforcementSetupGo, qaScene);
            reinforcementSetup = reinforcementSetupGo.AddComponent<BattleSetup>();   // QA許可のシーン＝Awake は通常の初期化をしない
            reinforcementSetup.enabled = false;   // 開始まで経過を数えない
            reinforcementSetup.fleetPrefab = template;
            reinforcementSetup.reinforcementEdgeRadius = ReinforcementPlan.edgeRadius;
            reinforcementSetup.NotificationSink = ReceiveQaNotification;
            reinforcementSetup.ReinforcementSpawned = OnReinforcementSpawned;
            if (!reinforcementSetup.ScheduleReinforcementForQa(entry, AlliedFaction))
                return "援軍=ON だが QA所有の BattleSetup が予約を受け付けなかった（QA許可・テンプレート・到着時刻のいずれか）";

            Log.Add(0f, ReinforcementPlan.Describe(Preset.seed) + "\n  予約：QA所有の BattleSetup に1件（予約=" +
                        reinforcementSetup.PendingReinforcementCount + " 生成=" + reinforcementSetup.SpawnedReinforcementCount +
                        "・開始まで無効）。テンプレート・一時提督はQA所有。戦略の援軍台帳は未使用");
            return null;
        }

        /// <summary>BattleSetup が時限増援を生成した直後に呼ばれる：QAシーンへ帰属させ、固定ID・軍団を付けて記録する。</summary>
        private void OnReinforcementSpawned(GameObject go, ScenarioData.FleetEntry entry)
        {
            if (go == null) return;
            if (qaScene.IsValid() && qaScene.isLoaded && go.scene != qaScene && go.transform.parent == null)
                SceneManager.MoveGameObjectToScene(go, qaScene);
            reinforcementFleetGos.Add(go);

            float el = Elapsed;
            ReinforcementSpawnCount++;
            if (ReinforcementPlan != null && el < ReinforcementPlan.arrivalSeconds - ArrivalTolerance) ReinforcementEarlySpawnCount++;

            FleetStrength fs = go.GetComponent<FleetStrength>();
            if (fs != null)
            {
                if (ReinforcementPlan != null)
                {
                    fs.fleetNumber = ReinforcementPlan.fleetId;
                    fs.corpsName = ReinforcementPlan.corpsName;
                }
                reinforcementFleets.Add(fs);
                if (!string.IsNullOrEmpty(fs.shipName)) reinforcementShipNames.Add(fs.shipName);
            }
            if (ReinforcementSpawnCount == 1)
            {
                ReinforcementSpawnElapsed = el;
                ReinforcementSpawnPosition = go.transform.position;
            }

            var ai = go.GetComponent<FleetAI>();
            if (Log != null)
                Log.Add(el, "援軍の出現（BattleSetup の時限増援・" + ReinforcementSpawnCount + " 回目）：固定ID=" + (fs != null ? fs.fleetNumber : 0) +
                            " 軍団=" + (fs != null && !string.IsNullOrEmpty(fs.corpsName) ? fs.corpsName : "(なし)") +
                            " 旗艦=" + (fs != null ? fs.shipName : "-") + "（軍団旗艦=" + (fs != null && fs.IsCorpsFlagship) + "）" +
                            " 艦艇数=" + (fs != null ? fs.strength + "/" + fs.maxStrength : "-") +
                            " 陣営=" + (fs != null ? fs.faction.ToString() : "-") +
                            " 到着 game 時刻=開始から " + el.ToString("0.00") + " 秒（予定 " + (ReinforcementPlan != null ? ReinforcementPlan.arrivalSeconds.ToString("0.##") : "-") +
                            "・BattleSetup の経過 " + (reinforcementSetup != null ? reinforcementSetup.ReinforcementElapsed.ToString("0.00") : "-") + "）" +
                            " 位置=" + ((Vector2)go.transform.position).ToString("0.0") +
                            " AI有効=" + (ai != null && ai.enabled) + " seed=" + Preset.seed);
        }

        /// <summary>QA所有の会戦イベント・援軍の通知の受け口（QAローカルログへ。共有の NotificationCenter へは送らない）。</summary>
        private void ReceiveQaNotification(NotificationCategory category, NotificationSeverity severity, string message)
        {
            string line = "[" + category + "/" + severity + "] " + (message ?? "");
            if (qaNotifications.Count < MaxQaNotifications) qaNotifications.Add(line);
            else QaNotificationsDropped++;
            if (Log != null) Log.Add(Elapsed, "QA通知（共有の通知履歴へは送らない）：" + line);
        }

        /// <summary>QAシーンにいる、プリセットで組んだもの以外の艦隊（ルートの FleetStrength・有効なもの）の数。</summary>
        public int CountUnplannedFleetsInQaScene()
        {
            FleetStrength[] found = FindObjectsByType<FleetStrength>();
            int n = 0;
            for (int i = 0; i < found.Length; i++)
            {
                FleetStrength f = found[i];
                if (f == null || f.gameObject.scene != qaScene || f.transform.parent != null) continue;
                if (!fleets.ContainsValue(f)) n++;
            }
            return n;
        }

        /// <summary>援軍=ON の判定（観測完了時）：到着1回・早着0・予約残り0、味方として動けるか。</summary>
        private void JudgeReinforcement()
        {
            if (ReinforcementPlan == null || !Switches.Get(BattleQaFeature.援軍)) return;
            float el = Elapsed;
            int pending = reinforcementSetup != null ? reinforcementSetup.PendingReinforcementCount : -1;
            int produced = reinforcementSetup != null ? reinforcementSetup.SpawnedReinforcementCount : -1;
            int unplanned = CountUnplannedFleetsInQaScene();
            string detail = "予定 " + ReinforcementPlan.arrivalSeconds.ToString("0.##") + " 秒・観測 " + el.ToString("0.00") + " 秒／出現（QA受領）=" +
                            ReinforcementSpawnCount + " 生成（BattleSetup）=" + produced + " 予約残り=" + pending + " 早着=" + ReinforcementEarlySpawnCount +
                            " 予定外の艦隊（QAシーン）=" + unplanned + " 初回の到着=" + (ReinforcementSpawnElapsed >= 0f ? ReinforcementSpawnElapsed.ToString("0.00") + " 秒" : "未");
            BattleQaVerdict arrival;
            if (ReinforcementSpawnCount == 0 && el < ReinforcementPlan.arrivalSeconds) arrival = BattleQaVerdict.未判定;
            else arrival = ReinforcementSpawnCount == 1 && produced == 1 && pending == 0 && ReinforcementEarlySpawnCount == 0 && unplanned == 1
                ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
            Log.AddCheck(new BattleQaCheck("援軍の到着（時限増援の実生成・1回だけ）", arrival, detail, el));

            FleetStrength f = ReinforcementFleet;
            if (ReinforcementSpawnCount == 0) return;
            if (f == null) { Log.AddCheck(new BattleQaCheck("援軍が味方として動く", BattleQaVerdict.不合格, "出現した援軍が消失", el)); return; }
            float observed = el - ReinforcementSpawnElapsed;
            var ai = f.GetComponent<FleetAI>();
            var weapon = f.GetComponent<FleetWeapon>();
            var mv = f.GetComponent<FleetMovement>();
            bool friendly = true, hostileToEnemy = false, anyAlly = false, anyEnemy = false;
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                if (kv.Value == null || !Preset.TryGetFleet(kv.Key, out BattleQaFleetSpec spec)) continue;
                if (spec.role == BattleQaCommandRole.敵) { anyEnemy = true; if (FactionRelations.IsHostile(f, kv.Value)) hostileToEnemy = true; }
                else { anyAlly = true; if (FactionRelations.IsHostile(f, kv.Value)) friendly = false; }
            }
            float disp = Vector2.Distance(ReinforcementSpawnPosition, f.transform.position);
            bool componentsOn = ai != null && ai.enabled && weapon != null && weapon.enabled && mv != null && mv.enabled;
            string moveDetail = "陣営=" + f.faction + "（QA同盟=" + AlliedFaction + "） 味方と非敵対=" + (anyAlly ? friendly.ToString() : "味方なし") +
                                " 敵と敵対=" + (anyEnemy ? hostileToEnemy.ToString() : "敵なし") + " AI/武装/移動 有効=" + componentsOn +
                                " 生存=" + f.IsAlive + " 出現からの変位=" + disp.ToString("0.00") + "（しきい " + BattleQaReinforcementPlan.MinMoveDisplacement +
                                "・出現後 " + observed.ToString("0.0") + " 秒）";
            BattleQaVerdict moves;
            if (observed < BattleQaReinforcementPlan.MinMoveObserveSeconds) moves = BattleQaVerdict.未判定;
            else moves = f.faction == AlliedFaction && friendly && (!anyEnemy || hostileToEnemy) && componentsOn &&
                         disp >= BattleQaReinforcementPlan.MinMoveDisplacement ? BattleQaVerdict.合格 : BattleQaVerdict.不合格;
            Log.AddCheck(new BattleQaCheck("援軍が味方として動く", moves, moveDetail, el));
        }

        /// <summary>援軍の実状態（読み戻し・ログ用）。</summary>
        public string DescribeReinforcementReadback()
        {
            var sb = new StringBuilder();
            bool on = Switches != null && Switches.Get(BattleQaFeature.援軍);
            sb.Append("援軍=").Append(BattleQaFeatureSwitches.OnOff(on)).Append("：");
            if (reinforcementSetup != null)
            {
                sb.Append("QA所有 BattleSetup 有効=").Append(reinforcementSetup.enabled)
                  .Append(" 予約=").Append(reinforcementSetup.PendingReinforcementCount)
                  .Append(" 生成=").Append(reinforcementSetup.SpawnedReinforcementCount)
                  .Append(" 経過=").Append(reinforcementSetup.ReinforcementElapsed.ToString("0.00"));
            }
            else if (on)
            {
                sb.Append("QA所有 BattleSetup なし（未生成または破棄済み）");
            }
            else
            {
                sb.Append("予約しない　QAシーンの BattleSetup=").Append(CountInQaScene<BattleSetup>()).Append(" 件");
            }
            sb.Append(" 出現（QA受領）=").Append(ReinforcementSpawnCount).Append(" 早着=").Append(ReinforcementEarlySpawnCount)
              .Append(" 予定外の艦隊（QAシーン）=").Append(CountUnplannedFleetsInQaScene())
              .Append(" 到着予定=").Append(ReinforcementPlan != null ? ReinforcementPlan.arrivalSeconds.ToString("0.##") + " 秒" : "-")
              .Append(" 経過=").Append(Elapsed.ToString("0.00")).Append(" 秒");
            FleetStrength f = ReinforcementFleet;
            if (f != null)
            {
                var ai = f.GetComponent<FleetAI>();
                sb.Append("\n    援軍艦隊：固定ID=").Append(f.fleetNumber).Append(" 軍団=").Append(!string.IsNullOrEmpty(f.corpsName) ? f.corpsName : "(なし)")
                  .Append(" 旗艦=").Append(f.shipName).Append("（軍団旗艦=").Append(f.IsCorpsFlagship).Append("）")
                  .Append(" 艦艇数=").Append(f.strength).Append('/').Append(f.maxStrength)
                  .Append(" 陣営=").Append(f.faction).Append(" AI有効=").Append(ai != null && ai.enabled)
                  .Append(" 位置=").Append(((Vector2)f.transform.position).ToString("0.0"));
            }
            sb.Append("／稼働中の BattleManager（全シーン）=").Append(CountEnabled<BattleManager>())
              .Append(" 件／戦略の援軍台帳=未使用（予約 ").Append(StrategySession.Reinforcements != null ? StrategySession.Reinforcements.PendingCount : 0).Append(" 件）");
            return sb.ToString();
        }

        private int CountInQaScene<T>() where T : Component
        {
            T[] found = FindObjectsByType<T>();
            int n = 0;
            for (int i = 0; i < found.Length; i++) if (found[i] != null && found[i].gameObject.scene == qaScene) n++;
            return n;
        }

        private static int CountEnabled<T>() where T : Behaviour
        {
            T[] found = FindObjectsByType<T>();
            int n = 0;
            for (int i = 0; i < found.Length; i++) if (found[i] != null && found[i].isActiveAndEnabled) n++;
            return n;
        }

        /// <summary>いま回り込み（包囲）命令を受けているQA艦隊の数。</summary>
        public int CountEnvelopingFleets()
        {
            int n = 0;
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                FleetAI ai = kv.Value != null ? kv.Value.GetComponent<FleetAI>() : null;
                if (ai != null && ai.enveloping) n++;
            }
            return n;
        }

        /// <summary>機能スイッチの対象がいま実際にどうなっているか（生成・有効・予約・判断回数）。</summary>
        public string DescribeFeatureSwitchReadback()
        {
            var sb = new StringBuilder("機能スイッチの実状態（").Append(Switches.name).Append(" 版").Append(Switches.version)
                .Append("・適用＝開始前 実時間 ").Append(SwitchesAppliedRealtime.ToString("0.00")).Append(" 秒）：");

            sb.Append("\n  自動包囲=").Append(BattleQaFeatureSwitches.OnOff(Switches.Get(BattleQaFeature.自動包囲))).Append("：");
            if (commandManager != null)
                sb.Append("autoEnvelopment=").Append(commandManager.autoEnvelopment).Append(" 軍団長AI有効=").Append(commandManager.enabled)
                  .Append(" 下令=").Append(commandManager.EnvelopmentOrdersIssued).Append(" 見送り=").Append(commandManager.EnvelopmentSuppressed)
                  .Append(" 回り込み中の艦隊=").Append(CountEnvelopingFleets());
            else sb.Append("適用先なし（軍団長AIなし）");

            sb.Append("\n  ").Append(DescribeReinforcementReadback());

            sb.Append("\n  戦況イベント=").Append(BattleQaFeatureSwitches.OnOff(Switches.Get(BattleQaFeature.戦況イベント))).Append("：");
            if (eventManager != null)
                sb.Append("QA所有 BattleEventManager 有効=").Append(eventManager.enabled)
                  .Append(" 抽選間隔=").Append(eventManager.tickInterval.ToString("0.##"))
                  .Append(" 抽選=").Append(eventManager.TickCount).Append(" 発火=").Append(eventManager.FiredCount)
                  .Append(" 未解決の決裁=").Append(eventManager.PendingDecisionCount)
                  .Append(" 次の抽選まで=").Append(eventManager.NextTickAt > 0f ? (eventManager.NextTickAt - Time.time).ToString("0.0") + " 秒" : "未開始")
                  .Append(" 対象勢力=").Append(eventManager.TargetFaction).Append("（明示=").Append(eventManager.HasTargetFactionOverride)
                  .Append("・GameSettings.playerFaction=").Append(GameSettings.Instance != null ? GameSettings.Instance.playerFaction.ToString() : "-")
                  .Append("） 通知先=").Append(eventManager.NotificationSink != null ? "QAローカルログ" : "★共有の通知履歴")
                  .Append(" QA通知=").Append(qaNotifications.Count).Append(" 件（省略 ").Append(QaNotificationsDropped).Append("）")
                  .Append(" 共有の通知履歴の増加=").Append(NotificationCenter.LastSeq - savedNotificationSeq).Append(" 件");
            else
                sb.Append("QA所有なし　QAシーンの BattleEventManager=").Append(CountInQaScene<BattleEventManager>())
                  .Append(" 件／稼働中の BattleEventManager（全シーン）=").Append(CountEnabled<BattleEventManager>()).Append(" 件");
            sb.Append("\n  分類＝").Append(Switches.Classification);
            return sb.ToString();
        }

        /// <summary>終了時：機能スイッチの対象をどう片付けるかを記録する（片付けの前に呼ぶ）。</summary>
        private void RecordSwitchDisposal(bool sceneTearingDown)
        {
            if (Switches == null) return;
            var sb = new StringBuilder("機能スイッチ「").Append(Switches.name).Append("」の後始末：");
            if (eventManager != null)
                sb.Append("戦況イベント＝QA所有 BattleEventManager を破棄（発火 ").Append(eventManager.FiredCount)
                  .Append(" 件・未解決の決裁 ").Append(eventManager.PendingDecisionCount).Append(" 件は適用も自動採択もせず破棄）");
            else if (Switches.Get(BattleQaFeature.戦況イベント))
                sb.Append("戦況イベント＝QA所有の個体は既に無い（").Append(sceneTearingDown ? "シーン破棄で先に消滅" : "未生成または生成失敗")
                  .Append("＝未解決件数は不明）");
            else
                sb.Append("戦況イベント＝QA所有なし");
            sb.Append("／自動包囲＝");
            if (commandManager != null)
                sb.Append("QAの軍団長AIごと破棄（下令 ").Append(commandManager.EnvelopmentOrdersIssued)
                  .Append("・見送り ").Append(commandManager.EnvelopmentSuppressed).Append("。通常の軍団長AIの既定 true は未変更）");
            else sb.Append("QAの軍団長AIなし");
            sb.Append("／援軍＝");
            int liveFleets = 0;
            for (int i = 0; i < reinforcementFleetGos.Count; i++) if (reinforcementFleetGos[i] != null) liveFleets++;
            if (reinforcementSetup != null || reinforcementTemplateRoot != null || liveFleets > 0)
                sb.Append("QA所有の BattleSetup（未到着の予約 ").Append(reinforcementSetup != null ? reinforcementSetup.PendingReinforcementCount : 0)
                  .Append(" 件は生成せず破棄）・援軍艦隊 ").Append(liveFleets).Append(" 隊（出現 ").Append(ReinforcementSpawnCount)
                  .Append(" 回）・テンプレート・一時提督を破棄。旗艦名 ").Append(reinforcementShipNames.Count).Append(" 件は旗艦名台帳へ返却");
            else if (Switches.Get(BattleQaFeature.援軍))
                sb.Append("QA所有の対象は既に無い（").Append(sceneTearingDown ? "シーン破棄で先に消滅" : "未生成または生成失敗")
                  .Append("・出現 ").Append(ReinforcementSpawnCount).Append(" 回。旗艦名 ").Append(reinforcementShipNames.Count).Append(" 件は返却）");
            else
                sb.Append("QA所有なし（予約しない）");
            sb.Append("。戦略の援軍台帳 StrategySession.Reinforcements は未使用");
            sb.Append("／QA通知＝").Append(qaNotifications.Count).Append(" 件（省略 ").Append(QaNotificationsDropped).Append("）はQAローカルログのみ");
            LastSwitchDisposal = sb.ToString();
            if (Log != null) Log.Add(Elapsed, LastSwitchDisposal);
        }

        /// <summary>適用値がいまも盤面に残っているか（食い違いの一覧・空＝一致）。消失した艦隊は数えない。</summary>
        public List<string> TuningReadbackMismatches()
        {
            var diffs = new List<string>();
            for (int i = 0; i < appliedTuning.Count; i++)
            {
                TuningRecord r = appliedTuning[i];
                FleetStrength f = Fleet(r.fleetId);
                if (f == null) continue;
                if (!TryReadField(f, r.field, out float now)) { diffs.Add("艦隊" + r.fleetId + " " + r.field + "：読めない"); continue; }
                if (now != r.applied)
                    diffs.Add("艦隊" + r.fleetId + " " + BattleQaTuningProfile.ComponentFieldName(r.field) + "：適用 " +
                              r.applied.ToString("0.####") + " / 現在 " + now.ToString("0.####"));
            }
            if (HasCorpsSpacingTarget && commandManager != null && commandManager.corpsMinSpacing != CorpsMinSpacingApplied)
                diffs.Add(BattleQaTuningProfile.ComponentFieldName(BattleQaTuningField.軍団隊形間隔) + "：適用 " +
                          CorpsMinSpacingApplied.ToString("0.####") + " / 現在 " + commandManager.corpsMinSpacing.ToString("0.####"));
            return diffs;
        }

        private string DescribeTuningReadback()
        {
            List<string> d = TuningReadbackMismatches();
            return "調整値の読み戻し（" + Tuning.name + " 版" + Tuning.version + "）：" +
                   (d.Count == 0 ? "適用値のまま" : "★食い違い " + string.Join("／", d)) +
                   (commandManager != null ? "\n  " + DescribeCorpsSpacing() : "");
        }

        /// <summary>QA艦隊の軍団キー（重複なし・固定ID順）。</summary>
        private List<string> CorpsKeys()
        {
            var keys = new List<string>();
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                string key = kv.Value != null ? CorpsFormation.KeyFor(kv.Value) : null;
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key)) keys.Add(key);
            }
            return keys;
        }

        /// <summary>軍団ごとの直近の間隔算出（指定最小間隔・占有下限・実間隔）。軍団長AIが無ければ空文字。</summary>
        public string DescribeCorpsSpacing()
        {
            if (commandManager == null) return "";
            var sb = new StringBuilder("軍団隊形間隔の算出（調整＝最小間隔 ").Append(CorpsMinSpacingApplied.ToString("0.####")).Append("）：");
            List<string> keys = CorpsKeys();
            if (keys.Count == 0) sb.Append("軍団なし");
            for (int i = 0; i < keys.Count; i++)
            {
                sb.Append("\n    ").Append(CorpsFormationOrderRules.DisplayName(keys[i])).Append('：');
                if (commandManager.TryGetCorpsSpacing(keys[i], out CorpsSpacingResult r)) sb.Append(r.Describe());
                else sb.Append("未算出");
            }
            return sb.ToString();
        }

        /// <summary>開始後、軍団長AIが全軍団の間隔を算出したら（または期限で）ログへ残す。</summary>
        private IEnumerator LogCorpsSpacingWhenResolved()
        {
            float deadline = Time.time + corpsSpacingLogTimeout;
            List<string> keys = CorpsKeys();
            while (Time.time < deadline && commandManager != null)
            {
                bool all = true;
                for (int i = 0; i < keys.Count; i++)
                    if (!commandManager.TryGetCorpsSpacing(keys[i], out _)) { all = false; break; }
                if (all) break;
                yield return null;
            }
            if (commandManager != null) Log.Add(Elapsed, DescribeCorpsSpacing());
        }

        /// <summary>盤面から初期スナップショットを読む（固定ID順）。</summary>
        public BattleQaSnapshot CaptureSnapshot()
        {
            var list = new List<BattleQaFleetSnapshot>();
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                FleetStrength f = kv.Value;
                if (f == null) continue;
                var sq = f.GetComponent<Squadron>();
                var mo = f.GetComponent<FleetMorale>();
                bool aiOn = Preset.TryGetFleet(kv.Key, out BattleQaFleetSpec spec) && spec.aiEnabled;
                list.Add(new BattleQaFleetSnapshot(kv.Key, f.corpsName, f.IsCorpsFlagship, f.strength, f.maxStrength,
                    mo != null ? mo.morale : 0f, f.transform.position, f.transform.eulerAngles.z,
                    sq != null ? sq.currentFormation : Formation.紡錘陣, aiOn, f.faction));
            }
            return new BattleQaSnapshot(Preset.name, Preset.seed, list);
        }

        private void Fail(string reason)
        {
            FailureReason = reason ?? "";
            CurrentPhase = Phase.準備失敗;
            if (Log != null) Log.Add(0f, "★準備失敗：" + FailureReason + "（状態を戻して片付けます）");
            Debug.LogWarning("［固定会戦QA］準備失敗：" + FailureReason);
            End("準備失敗");
        }

        // ===================================================================
        // 隔離と復元
        // ===================================================================

        private void SaveGlobalState()
        {
            savedTimeScale = Time.timeScale;
            savedRandom = Random.state;
            savedAuditEnabled = MoraleAuditLog.Enabled;
            savedAuditMinAbsDelta = MoraleAuditLog.MinAbsDelta;
            // 読むだけ（戻す値ではなく「変えていない」ことの証跡）：共有の通知履歴の番号・戦略の援軍台帳。
            savedNotificationSeq = NotificationCenter.LastSeq;
            savedLedger = StrategySession.Reinforcements;
            savedLedgerPending = savedLedger != null ? savedLedger.PendingCount : 0;
            savedLedgerClosed = savedLedger != null ? savedLedger.ClosedCount : 0;
            stateSaved = true;
            stateRestored = false;
        }

        /// <summary>対象外要因（会戦イベント・勝敗判定・戦略の暦Tick＝生産/外交/財政・稟議）をQA中だけ止める。</summary>
        private void Isolate()
        {
            isolationNotes.Clear();
            isolationNotes.Add("使い捨てシーン（名前が Battle でない）＝会戦イベント・勝敗判定・BattleSetup は自動生成されない");
            Suspend<BattleEventManager>("会戦イベント（ランダム戦況イベント）");
            Suspend<BattleManager>("勝敗判定");
            Suspend<GalaxyView>("戦略の暦Tick（生産・外交・財政ほか）");
            Suspend<RingiDirector>("税の稟議");
            Suspend<FleetRingiDirector>("艦隊の稟議");
            Suspend<BattleDirector>("ウィンドウ会戦の時間駆動");
            isolationNotes.Add("統一クロック（GameClock）はこのQAでは進めない（会戦の Time.time で観測）");
            if (Switches != null && Switches.Get(BattleQaFeature.戦況イベント))
                isolationNotes.Add("★戦況イベント=ON：他シーンの既存個体は停止のまま、QAシーン所有の BattleEventManager を1つだけ自然抽選させる" +
                                   "（強制発火しない）。結果は自然イベントONモード＝固定合格の比較対象外。通常会戦での自然発火の合格にも使わない");
            else
                isolationNotes.Add("★会戦イベントの自然発火は観測できない（このモードを自然発火の確認に使わない）");
            if (Switches != null && Switches.Get(BattleQaFeature.援軍))
                isolationNotes.Add("★援軍=ON：QAシーン所有の BattleSetup を1つ置き、時限増援を1件だけ予約（通常の初期化・台帳のクリアはしない）。" +
                                   "戦略の援軍台帳（StrategySession.Reinforcements）と他シーンの BattleSetup は使わない。結果は切り分け＝固定合格の比較対象外");
            for (int i = 0; i < isolationNotes.Count; i++) Log.Add(0f, "隔離：" + isolationNotes[i]);
        }

        private void Suspend<T>(string label) where T : Behaviour
        {
            T[] found = FindObjectsByType<T>();
            int n = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null || !found[i].enabled) continue;
                found[i].enabled = false;
                suspended.Add(found[i]);
                n++;
            }
            isolationNotes.Add(label + "：既存 " + n + " 件を一時停止（終了で再開）");
        }

        /// <param name="restoreActiveScene">シーン破棄中（Play 停止・シーン離脱）はアクティブシーンに触らない。</param>
        private void RestoreGlobalState(bool restoreActiveScene)
        {
            if (!stateSaved || stateRestored) return;
            var sb = new StringBuilder();

            int resumed = 0;
            for (int i = 0; i < suspended.Count; i++)
                if (suspended[i] != null) { suspended[i].enabled = true; resumed++; }
            suspended.Clear();
            sb.Append("隔離の解除 ").Append(resumed).Append(" 件／");

            Random.state = savedRandom;
            sb.Append("Random.state 復元／");

            Time.timeScale = savedTimeScale;
            sb.Append("timeScale=").Append(savedTimeScale.ToString("0.##")).Append("／");

            MoraleAuditLog.Enabled = savedAuditEnabled;
            MoraleAuditLog.MinAbsDelta = savedAuditMinAbsDelta;
            sb.Append("士気の原因台帳 Enabled=").Append(savedAuditEnabled).Append("／");

            // 機能スイッチ：戦況イベントのQA許可を解く（以後は従来どおり Battle シーン以外で自壊）。対象は片付け済み。
            BattleEventManager.ClearQaHostScene();
            sb.Append("機能スイッチ「").Append(Switches != null ? Switches.name : "-").Append("」＝戦況イベントのQA許可 解除（許可残り=")
              .Append(BattleEventManager.HasQaHostScene).Append("）・").Append(LastSwitchDisposal).Append("／");

            // 援軍：BattleSetup のQA許可を解く（以後は従来どおり Battle シーン以外の Awake は何もしない）。対象は片付け済み。
            BattleSetup.ClearQaHostScene();
            sb.Append("援軍のQA許可 解除（許可残り=").Append(BattleSetup.HasQaHostScene).Append("）／");

            // 変えていないことの証跡（戻して帳尻を合わせない＝Clear しない）。
            LastSharedNotificationDelta = NotificationCenter.LastSeq - savedNotificationSeq;
            sb.Append("共有の通知履歴 LastSeq ").Append(savedNotificationSeq).Append("→").Append(NotificationCenter.LastSeq)
              .Append("（QA中の増加 ").Append(LastSharedNotificationDelta).Append(" 件。QA所有の会戦イベント・援軍の通知はQAローカルログへ送り、ここに含まない。履歴の Clear はしない）／");
            WarpReinforcementLedger ledgerNow = StrategySession.Reinforcements;
            int pendingNow = ledgerNow != null ? ledgerNow.PendingCount : 0;
            int closedNow = ledgerNow != null ? ledgerNow.ClosedCount : 0;
            LastStrategyLedgerUnchanged = ReferenceEquals(ledgerNow, savedLedger) && pendingNow == savedLedgerPending && closedNow == savedLedgerClosed;
            sb.Append("戦略の援軍台帳 未変更=").Append(LastStrategyLedgerUnchanged).Append("（予約 ").Append(savedLedgerPending).Append("→").Append(pendingNow)
              .Append("・締め ").Append(savedLedgerClosed).Append("→").Append(closedNow).Append("）／");

            // 調整プリセットは使い捨て艦隊のコンポーネントにだけ書いた＝艦隊の破棄で消える（戻す共有値は無い）。
            sb.Append("調整プリセット「").Append(Tuning != null ? Tuning.name : "-").Append("」＝QA艦隊とQAの軍団長AIのみに適用・破棄で消滅（アセット/既定値は未変更）／");

            if (!restoreActiveScene)
            {
                sb.Append("アクティブシーンは変更せず（シーン破棄中）");
            }
            else if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
                sb.Append("アクティブシーン=").Append(previousActiveScene.name);
            }
            else
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene s = SceneManager.GetSceneAt(i);
                    if (s != qaScene && s.isLoaded) { SceneManager.SetActiveScene(s); sb.Append("アクティブシーン=").Append(s.name); break; }
                }
            }

            stateRestored = true;
            LastRestoreReport = sb.ToString();
            if (Log != null) Log.Add(0f, "復元：" + LastRestoreReport);
        }

        /// <summary>QAで作ったものを片付ける（immediate=false はシーン破棄中＝Unity に任せ、SO だけ消す）。</summary>
        private void CleanupWorld(bool immediate)
        {
            if (immediate)
            {
                foreach (KeyValuePair<int, FleetStrength> kv in fleets)
                    if (kv.Value != null) DestroyImmediate(kv.Value.gameObject);
                if (commandManagerGo != null) DestroyImmediate(commandManagerGo);
                if (eventManagerGo != null)
                {
                    DestroyImmediate(eventManagerGo);
                    // デスクUIが作った EventSystem は QA シーン（アクティブ）に置かれる＝QA所有なので一緒に片付ける。
                    if (qaScene.IsValid() && qaScene.isLoaded)
                    {
                        GameObject[] roots = qaScene.GetRootGameObjects();
                        for (int i = 0; i < roots.Length; i++)
                            if (roots[i] != null && roots[i].GetComponent<EventSystem>() != null) DestroyImmediate(roots[i]);
                    }
                }
                if (PauseCtl != null) DestroyImmediate(PauseCtl.gameObject);
                // 援軍：コールバックを外してから、出現した艦隊・BattleSetup（未到着の予約ごと）・テンプレートを破棄。
                if (reinforcementSetup != null) { reinforcementSetup.ReinforcementSpawned = null; reinforcementSetup.NotificationSink = null; }
                for (int i = 0; i < reinforcementFleetGos.Count; i++)
                    if (reinforcementFleetGos[i] != null) DestroyImmediate(reinforcementFleetGos[i]);
                if (reinforcementSetupGo != null) DestroyImmediate(reinforcementSetupGo);
                if (reinforcementTemplateRoot != null) DestroyImmediate(reinforcementTemplateRoot);
            }
            else if (reinforcementSetup != null)
            {
                // シーン破棄中：オブジェクトは Unity が消す。以後の生成・通知だけ止める。
                reinforcementSetup.ReinforcementSpawned = null;
                reinforcementSetup.NotificationSink = null;
                reinforcementSetup.enabled = false;
            }
            // 援軍の旗艦名は static の旗艦名台帳から払い出される＝QAの分を返却する（撃沈で永久欠番になっていれば解く）。
            // Assign は使用中・欠番の名を返さない＝QA前はどちらでもなかった名なので、戻しても既存の割り当てを壊さない。
            for (int i = 0; i < reinforcementShipNames.Count; i++)
            {
                ShipNameRegistry.Release(reinforcementShipNames[i]);
                ShipNameRegistry.Unretire(reinforcementShipNames[i]);
            }
            reinforcementShipNames.Clear();
            reinforcementFleets.Clear();
            reinforcementFleetGos.Clear();
            reinforcementSetupGo = null;
            reinforcementSetup = null;
            reinforcementTemplateRoot = null;
            fleets.Clear();
            commandManagerGo = null;
            commandManager = null;
            eventManagerGo = null;
            eventManager = null;
            PauseCtl = null;

            for (int i = 0; i < tempAdmirals.Count; i++)
                if (tempAdmirals[i] != null) Destroy(tempAdmirals[i]);
            tempAdmirals.Clear();
        }

        /// <summary>★シーン離脱・Play 停止でも必ず戻す（End を経ずに破棄された場合の保険）。</summary>
        private void OnDestroy()
        {
            if (!stateRestored)
            {
                ended = true;
                StopAllCoroutines();
                if (Log != null) Log.Add(Elapsed, "★終了操作なしで破棄（シーン離脱／Play 停止）。状態を戻します" + AuditSummary());
                RecordSwitchDisposal(true);
                CleanupWorld(false);
                RestoreGlobalState(false);
                if (CurrentPhase != Phase.準備失敗) CurrentPhase = Phase.終了;
            }
            if (Active == this) Active = null;
        }

        private string AuditSummary()
        {
            int added = (MoraleAuditLog.Count + MoraleAuditLog.Dropped) - auditBaselineTotal;
            int droppedHere = MoraleAuditLog.Dropped - auditBaselineDropped;
            string loss = BattleQaRunLog.LossNotice(Log != null ? Log.Dropped : 0, droppedHere, false);
            return "／士気の原因台帳：QA中に " + Mathf.Max(0, added) + " 件、うちあふれ " + Mathf.Max(0, droppedHere) + " 件" +
                   (loss.Length > 0 ? " " + loss : "");
        }

        // ===================================================================
        // 命令と観測
        // ===================================================================

        /// <summary>開始の瞬間に出す命令（AI を有効にする前＝AI と取り合わない）。</summary>
        private void IssueOpeningCommands()
        {
            switch (Preset.kind)
            {
                case BattleQaPresetKind.不退転:
                    IssueLock(21);
                    IssueLock(22);
                    break;
                case BattleQaPresetKind.陣形変更:
                {
                    Squadron sq = SquadronOf(2);
                    if (sq == null) { Log.Add(0f, "命令：隷下A（2）が無い"); break; }
                    FormationOrderResult r = sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);
                    Log.AddCheck(new BattleQaCheck("直接命令の受理（隷下A→円陣）",
                        r == FormationOrderResult.受理 && sq.IsFormationHeld ? BattleQaVerdict.合格 : BattleQaVerdict.不合格,
                        "結果=" + r + " 保持=" + sq.IsFormationHeld + " スキルP=" + sq.SkillPoints.ToString("0.0"), 0f));
                    break;
                }
                default:
                    Log.Add(0f, "命令：QAからは出さない（軍団長AIの判断を観測）");
                    break;
            }
        }

        private void IssueLock(int id)
        {
            FleetStrength f = Fleet(id);
            bool ok = f != null && ActiveCommandState.Issue(f, ActiveCommand.不退転);
            Log.Add(0f, "命令：艦隊" + id + " へ不退転（ActiveCommandState.Issue）→ " + (ok ? "発令" : "失敗") +
                        " / 効果=" + (f != null && f.activeMoraleLock));
        }

        private IEnumerator ScenarioRoutine()
        {
            switch (Preset.kind)
            {
                case BattleQaPresetKind.退却: yield return RetreatScenario(); break;
                case BattleQaPresetKind.不退転: yield return MoraleLockScenario(); break;
                default: yield return FormationScenario(); break;
            }

            // 観測を終えたら止めて読める状態にする（PauseManager 経由＝timeScale と一致）。
            if (PauseCtl != null) PauseCtl.Pause();
            CurrentPhase = Phase.観測完了;
            JudgeReinforcement();   // 援軍=ON のときだけ判定を足す（OFF＝既定のログは従来どおり）
            Log.Add(Elapsed, DescribeTuningReadback());
            Log.Add(Elapsed, DescribeFeatureSwitchReadback());
            Log.Add(Elapsed, "観測完了：全体の結論＝" + Log.Overall + "（一時停止・PauseManager 一致=" + IsPauseConsistent + "）" + AuditSummary());
        }

        // ----- 退却 -----

        private IEnumerator RetreatScenario()
        {
            FleetStrength cmd = Fleet(1);
            FleetStrength enemy = Fleet(11);
            if (cmd == null || enemy == null) { Log.AddCheck(new BattleQaCheck("前提", BattleQaVerdict.未判定, "艦隊1または11が無い", Elapsed)); yield break; }

            var start = new Dictionary<int, Vector2>();
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
                if (kv.Value != null) start[kv.Key] = kv.Value.transform.position;
            Vector2 enemyStart = start[11];
            string key = CorpsFormation.KeyFor(cmd);

            float deadline = Time.time + retreatOrderTimeout;
            while (!BattlefieldCommandManager.IsCorpsRetreatOrdered(key) && Time.time < deadline) yield return null;
            bool ordered = BattlefieldCommandManager.IsCorpsRetreatOrdered(key);
            Log.AddCheck(new BattleQaCheck("軍団総退却の発令（" + BattleQaPresetCatalog.RetreatCorps + "）",
                ordered ? BattleQaVerdict.合格 : BattleQaVerdict.未判定,
                ordered ? "発令を観測" : retreatOrderTimeout + " 秒以内に発令されず（前提不成立）", Elapsed));
            if (!ordered) yield break;

            // 所属外が一度でも撤退状態になったかを毎フレーム見る。
            // 退却軍団の各隊は、変位・移動開始・停滞（動き出した後の速度0＝経路の打ち直し）・退却と逆向きの目的地を毎フレーム記録する。
            IReadOnlyList<BattleQaFleetSpec> specs = Preset.Fleets;
            var everRetreat = new HashSet<int>();
            var motion = new Dictionary<int, RetreatMotionTrack>();
            for (int i = 0; i < specs.Count; i++)
                if (specs[i].corpsName == BattleQaPresetCatalog.RetreatCorps)
                    motion[specs[i].fleetId] = new RetreatMotionTrack
                    {
                        dir = BattleQaJudgeRules.RetreatDirection(start[specs[i].fleetId], enemyStart, Vector2.down),
                    };
            float observeStart = Time.time;
            float until = Time.time + retreatObserveSeconds;
            while (Time.time < until)
            {
                foreach (KeyValuePair<int, FleetStrength> kv in fleets)
                {
                    FleetAI ai = kv.Value != null ? kv.Value.GetComponent<FleetAI>() : null;
                    if (ai != null && ai.currentState == FleetAI.AIState.撤退) everRetreat.Add(kv.Key);
                    if (kv.Value != null && motion.TryGetValue(kv.Key, out RetreatMotionTrack t))
                        TrackRetreatMotion(t, kv.Value, start[kv.Key], Time.time - observeStart);
                }
                yield return null;
            }

            for (int i = 0; i < specs.Count; i++)
            {
                BattleQaFleetSpec spec = specs[i];
                FleetStrength f = Fleet(spec.fleetId);
                if (spec.role == BattleQaCommandRole.敵) continue;
                if (f == null) { Log.AddCheck(new BattleQaCheck("艦隊" + spec.fleetId, BattleQaVerdict.未判定, "消失", Elapsed)); continue; }

                FleetAI ai = f.GetComponent<FleetAI>();
                float ratio = f.maxStrength > 0 ? (float)f.strength / f.maxStrength : 1f;
                float individual = ai != null ? ai.retreatRatio : 0f;
                bool inRetreat = ai != null && ai.currentState == FleetAI.AIState.撤退;
                string corpsKey = CorpsFormation.KeyFor(f);
                bool ownOrdered = BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey);

                if (spec.corpsName == BattleQaPresetCatalog.RetreatCorps)
                {
                    Vector2 dir = BattleQaJudgeRules.RetreatDirection(start[spec.fleetId], enemyStart, Vector2.down);
                    float disp = BattleQaJudgeRules.DisplacementAlong(start[spec.fleetId], f.transform.position, dir);
                    BattleQaVerdict v = BattleQaJudgeRules.JudgeRetreatingMember(ownOrdered, inRetreat, disp, BattleQaJudgeRules.MinRetreatDisplacement);

                    // 持続の確認（しきいを下げずに厳しくする側だけ）：終了時も退却方向へ進行中で、変位が戻っておらず、
                    // 動き出した後に速度0へ落ちていない（＝退却経路の打ち直しで止まっていない）こと。
                    motion.TryGetValue(spec.fleetId, out RetreatMotionTrack mt);
                    FleetMovement mv = f.GetComponent<FleetMovement>();
                    bool movingAtEnd = mv != null && mv.IsMoving && mv.currentSpeed > 0f &&
                                       Vector2.Dot(mv.Destination - (Vector2)f.transform.position, mt != null ? mt.dir : Vector2.zero) > 0f;
                    float regress = mt != null ? mt.maxDisp - disp : 0f;
                    bool sustained = mt != null && mt.moveStartedAt >= 0f && movingAtEnd && regress <= retreatRegressTolerance &&
                                     mt.stallFramesAfterStart == 0 && mt.oppositeDestFrames == 0;
                    if (v == BattleQaVerdict.合格 && !sustained) v = BattleQaVerdict.不合格;

                    string cause = BattleQaJudgeRules.RetreatCauseIsolated(ratio, individual) ? "原因は軍団総退却に切り分け済み" : "★艦艇数比が個艦撤退比を下回り原因を分けられない";
                    if (!BattleQaJudgeRules.RetreatCauseIsolated(ratio, individual) && v == BattleQaVerdict.合格) v = BattleQaVerdict.未判定;
                    Log.AddCheck(new BattleQaCheck("配下の退却（艦隊" + spec.fleetId + "・" + spec.role + "）", v,
                        "状態=" + (ai != null ? ai.currentState.ToString() : "AIなし") + " 退却方向への変位=" + disp.ToString("0.00") +
                        "（しきい " + BattleQaJudgeRules.MinRetreatDisplacement + "・観測 " + retreatObserveSeconds + " 秒）" +
                        DescribeRetreatMotion(mt, movingAtEnd, regress, mv) +
                        " 艦艇数=" + f.strength + "/" + f.maxStrength + " 生存=" + f.IsAlive + " " + cause, Elapsed));
                }
                else
                {
                    BattleQaVerdict v = BattleQaJudgeRules.JudgeBystander(ownOrdered, everRetreat.Contains(spec.fleetId), ratio, individual);
                    Log.AddCheck(new BattleQaCheck("所属外の非巻き込み（艦隊" + spec.fleetId + "・" + spec.role + "）", v,
                        "軍団=" + (spec.corpsName.Length > 0 ? spec.corpsName : "(なし)") + " 自軍団の総退却=" + ownOrdered +
                        " 観測中に撤退状態=" + everRetreat.Contains(spec.fleetId) + " 艦艇数=" + f.strength + "/" + f.maxStrength, Elapsed));
                }
            }
        }

        /// <summary>退却軍団1隊ぶんの実移動の記録（読むだけ・盤面に書かない）。時刻は発令観測からのゲーム秒（未到達は負）。</summary>
        private class RetreatMotionTrack
        {
            public Vector2 dir;
            public float maxDisp = float.MinValue;
            public float turnedAt = -1f;
            public float moveStartedAt = -1f;
            public float reachedAt = -1f;
            public int stallFramesAfterStart;
            public int oppositeDestFrames;
        }

        private static void TrackRetreatMotion(RetreatMotionTrack t, FleetStrength f, Vector2 start, float sinceOrder)
        {
            Vector2 pos = f.transform.position;
            float disp = BattleQaJudgeRules.DisplacementAlong(start, pos, t.dir);
            if (disp > t.maxDisp) t.maxDisp = disp;
            if (t.reachedAt < 0f && disp >= BattleQaJudgeRules.MinRetreatDisplacement) t.reachedAt = sinceOrder;

            FleetMovement mv = f.GetComponent<FleetMovement>();
            if (mv == null) return;
            if (t.turnedAt < 0f && Vector2.Angle(f.transform.up, t.dir) <= mv.faceThreshold) t.turnedAt = sinceOrder;
            // 移動開始＝退却方向へ回頭し終えてから速度が乗った最初のフレーム（発令前の接近の残速度は数えない）。
            if (t.moveStartedAt < 0f)
            {
                if (t.turnedAt >= 0f && mv.currentSpeed > 0f) t.moveStartedAt = sinceOrder;
            }
            else if (mv.currentSpeed <= 0f)
            {
                t.stallFramesAfterStart++;
            }
            // 回頭し終えた後に目的地が退却と逆向きへ打ち直されたら数える（発令同フレームの AI 更新順の差は数えない）。
            if (t.turnedAt >= 0f && mv.IsMoving && Vector2.Dot(mv.Destination - pos, t.dir) <= 0f) t.oppositeDestFrames++;
        }

        private static string DescribeRetreatMotion(RetreatMotionTrack t, bool movingAtEnd, float regress, FleetMovement mv)
        {
            if (t == null) return " ★移動の記録なし";
            return " 回頭完了=" + FormatAt(t.turnedAt) + " 移動開始=" + FormatAt(t.moveStartedAt) +
                   " しきい到達=" + FormatAt(t.reachedAt) + " 最大変位=" + (t.maxDisp == float.MinValue ? "-" : t.maxDisp.ToString("0.00")) +
                   " 戻り=" + regress.ToString("0.00") + " 動き出し後の停止フレーム=" + t.stallFramesAfterStart +
                   " 逆向き目的地フレーム=" + t.oppositeDestFrames + " 終了時に退却方向へ進行中=" + movingAtEnd +
                   " 終了時速度=" + (mv != null ? mv.currentSpeed.ToString("0.00") : "-");
        }

        private static string FormatAt(float t) => t < 0f ? "未" : "t+" + t.ToString("0.00");

        // ----- 不退転 -----

        private class LockTrack
        {
            public int id;
            public bool applied;
            public bool ended;
            public float endedAt;
            public int routedFramesDuringLock;
            public float minMorale = float.MaxValue;
            public float floorReachedAt = -1f;
            public int shipsAtStart;
            public int shipsAtEnd;
            public bool lostBeforeEnd;
        }

        private IEnumerator MoraleLockScenario()
        {
            var a = new LockTrack { id = 21 };
            var b = new LockTrack { id = 22 };
            FleetStrength fa = Fleet(21), fb = Fleet(22), shooterB = Fleet(32);
            if (fa == null || fb == null || shooterB == null) { Log.AddCheck(new BattleQaCheck("前提", BattleQaVerdict.未判定, "艦隊21/22/32が無い", Elapsed)); yield break; }

            a.applied = fa.activeMoraleLock; a.shipsAtStart = fa.strength;
            b.applied = fb.activeMoraleLock; b.shipsAtStart = fb.strength;
            Log.Add(Elapsed, "発動：21 効果=" + a.applied + " / 22 効果=" + b.applied);

            float timeout = Time.time + lockTimeout;
            while ((!a.ended || !b.ended) && Time.time < timeout)
            {
                TrackLock(a, fa);
                TrackLock(b, fb);
                yield return null;
            }

            JudgeLock(a, fa, "被弾継続組");
            JudgeLock(b, fb, "被弾停止組");
            if (!a.ended || !b.ended) yield break;

            // 被弾停止：22 を撃つ撃ち手の交戦規定を射撃管制へ（実命令経路）。31 は撃ち続ける。
            shooterB.stance = EngagementStance.射撃管制;
            Log.Add(Elapsed, "命令：撃ち手32の交戦規定＝射撃管制（22への被弾を止める）。撃ち手31は攻撃的のまま");

            float grace = Time.time + lockStopGraceSeconds;
            while (Time.time < grace) yield return null;

            int aPost = fa != null ? fa.strength : 0;
            int bPost = fb != null ? fb.strength : 0;
            float postStart = Time.time;
            bool aRouted = false, bRouted = false;
            float until = Time.time + lockPostEndSeconds;
            while (Time.time < until)
            {
                var ma = fa != null ? fa.GetComponent<FleetMorale>() : null;
                var mb = fb != null ? fb.GetComponent<FleetMorale>() : null;
                if (ma != null && ma.IsRouted && !aRouted) { aRouted = true; Log.Add(Elapsed, "終了後：21 が敗走（士気 " + ma.morale.ToString("0.00") + "）"); }
                if (mb != null && mb.IsRouted && !bRouted) { bRouted = true; Log.Add(Elapsed, "終了後：22 が敗走（士気 " + mb.morale.ToString("0.00") + "）"); }
                yield return null;
            }
            float secs = Time.time - postStart;

            if (fa != null)
                Log.AddCheck(new BattleQaCheck("終了後の被弾継続（21）",
                    BattleQaJudgeRules.JudgeDamageFlow(true, aPost, fa.strength, secs, 0),
                    "艦艇数 " + aPost + "→" + fa.strength + "（" + secs.ToString("0.0") + " 秒）生存=" + fa.IsAlive, Elapsed));
            if (fb != null)
                Log.AddCheck(new BattleQaCheck("終了後の被弾停止（22）",
                    BattleQaJudgeRules.JudgeDamageFlow(false, bPost, fb.strength, secs, BattleQaJudgeRules.StoppedDamageTolerance),
                    "艦艇数 " + bPost + "→" + fb.strength + "（" + secs.ToString("0.0") + " 秒・猶予 " + lockStopGraceSeconds + " 秒後から）生存=" + fb.IsAlive, Elapsed));

            // 終了後の敗走（21）：効果中に下限へ届いていれば、撃たれ続ける以上は通常の敗走が戻るはず。
            // ★到達は「効果が乗っている間」に観測したものだけ数える（切れた瞬間の値で前提成立にしない）。
            bool floorReached = a.floorReachedAt >= 0f;
            BattleQaVerdict rv = !floorReached ? BattleQaVerdict.未判定 : (aRouted ? BattleQaVerdict.合格 : BattleQaVerdict.不合格);
            Log.AddCheck(new BattleQaCheck("終了後の通常敗走の再開（21・被弾継続）", rv,
                "効果中の最低士気=" + a.minMorale.ToString("0.00") +
                (floorReached ? "（下限到達 t=" + a.floorReachedAt.ToString("0.00") + "）" : "（下限未到達＝前提不足）") +
                " 初期士気=" + (Preset.TryGetFleet(21, out BattleQaFleetSpec spec21) ? spec21.initialMorale.ToString("0.#") : "-") +
                " 終了後に敗走を観測=" + aRouted, Elapsed));
            Log.Add(Elapsed, "観測：22（被弾停止）の終了後の敗走=" + bRouted + "（判定対象外・自艦の交戦低下でも動くため参考値）");
        }

        private void TrackLock(LockTrack t, FleetStrength f)
        {
            if (t.ended) return;
            if (f == null || !f.IsAlive) { t.lostBeforeEnd = true; t.ended = true; t.endedAt = Elapsed; Log.Add(Elapsed, "★艦隊" + t.id + " が効果中に失われた"); return; }
            var mo = f.GetComponent<FleetMorale>();
            if (mo != null)
            {
                if (mo.morale < t.minMorale) t.minMorale = mo.morale;
                if (f.activeMoraleLock && mo.IsRouted) t.routedFramesDuringLock++;
                if (f.activeMoraleLock && t.floorReachedAt < 0f && mo.morale <= MoraleLockRules.LockedFloor + MoraleFloorEpsilon)
                {
                    t.floorReachedAt = Elapsed;
                    Log.Add(Elapsed, "効果中：艦隊" + t.id + " の士気が下限 " + MoraleLockRules.LockedFloor + " に到達（艦艇数 " + f.strength + "／原因は士気の原因台帳で確認）");
                }
            }
            if (!f.activeMoraleLock)
            {
                t.ended = true;
                t.endedAt = Elapsed;
                t.shipsAtEnd = f.strength;
                Log.Add(Elapsed, "終了：艦隊" + t.id + " の不退転が切れた（士気 " + (mo != null ? mo.morale.ToString("0.00") : "-") + " 艦艇数 " + f.strength + "）");
            }
        }

        private void JudgeLock(LockTrack t, FleetStrength f, string label)
        {
            bool alive = t.ended && !t.lostBeforeEnd;
            Log.AddCheck(new BattleQaCheck("効果中は敗走しない（" + t.id + "・" + label + "）",
                t.ended ? BattleQaJudgeRules.JudgeLockDuration(t.applied, alive, t.routedFramesDuringLock) : BattleQaVerdict.未判定,
                "効果=" + t.applied + " 終了=" + t.ended + (t.ended ? "（t=" + t.endedAt.ToString("0.0") + "）" : "（期限切れ）") +
                " 効果中に失われた=" + t.lostBeforeEnd + " 敗走フレーム=" + t.routedFramesDuringLock +
                " 最低士気=" + (t.minMorale == float.MaxValue ? "-" : t.minMorale.ToString("0.00")), Elapsed));
            if (t.ended && !t.lostBeforeEnd)
                Log.AddCheck(new BattleQaCheck("効果中の実被弾（" + t.id + "）",
                    BattleQaJudgeRules.JudgeDamageFlow(true, t.shipsAtStart, t.shipsAtEnd, t.endedAt, 0),
                    "艦艇数 " + t.shipsAtStart + "→" + t.shipsAtEnd, Elapsed));
        }

        // ----- 陣形変更 -----

        private IEnumerator FormationScenario()
        {
            Squadron held = SquadronOf(2), control = SquadronOf(3);
            if (held == null || control == null) { Log.AddCheck(new BattleQaCheck("前提", BattleQaVerdict.未判定, "隷下2/3が無い", Elapsed)); yield break; }

            // 所属と軍団旗艦を控える（観測中に変わったら記録）。
            var corpsAtStart = new Dictionary<int, string>();
            var flagshipAtStart = new Dictionary<int, bool>();
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                if (kv.Value == null) continue;
                corpsAtStart[kv.Key] = kv.Value.corpsName;
                flagshipAtStart[kv.Key] = kv.Value.IsCorpsFlagship;
            }
            int membershipChanges = 0;

            int holdBrokenFrames = 0;
            bool controlChanged = false;
            float until = Time.time + formationHoldObserveSeconds;
            while (Time.time < until)
            {
                if (!held.IsFormationHeld || held.currentFormation != Formation.円陣) holdBrokenFrames++;
                if (control.LastFormationSource == FormationOrderSource.軍団AI) controlChanged = true;
                membershipChanges += CountMembershipChanges(corpsAtStart, flagshipAtStart);
                yield return null;
            }
            Log.AddCheck(new BattleQaCheck("保持が軍団AIの周期を跨ぐ（隷下A）",
                BattleQaJudgeRules.JudgeHoldAgainstCorpsAi(controlChanged, control.currentFormation,
                    held.IsFormationHeld && holdBrokenFrames == 0, held.currentFormation, Formation.円陣),
                "対照（隷下B）を軍団AIが変更=" + controlChanged + "（陣形=" + control.currentFormation + "）" +
                " 隷下A 陣形=" + held.currentFormation + " 保持=" + held.IsFormationHeld + " 崩れたフレーム=" + holdBrokenFrames, Elapsed));

            // 優先順位：保持中の艦隊へ軍団AI名義の指定を当てる（実窓口）。
            FormationOrderResult probe = held.RequestFormation(Formation.横陣, FormationOrderSource.軍団AI);
            Log.AddCheck(new BattleQaCheck("命令優先順位（保持中に軍団AIの指定）",
                probe == FormationOrderResult.保持により拒否 && held.currentFormation == Formation.円陣 ? BattleQaVerdict.合格 : BattleQaVerdict.不合格,
                "結果=" + probe + " 陣形=" + held.currentFormation, Elapsed));

            // 保持解除 → 軍団AIへ戻るか。
            bool released = held.ReleaseFormationHold("QA 保持解除");
            Log.Add(Elapsed, "命令：隷下Aの保持を解除 → " + released + "（スキルP " + held.SkillPoints.ToString("0.0") + "）");
            until = Time.time + formationReleaseObserveSeconds;
            while (Time.time < until && held.LastFormationSource != FormationOrderSource.軍団AI)
            {
                membershipChanges += CountMembershipChanges(corpsAtStart, flagshipAtStart);
                yield return null;
            }

            BattleQaVerdict back = BattleQaJudgeRules.JudgeReturnToCorpsAi(held.IsFormationHeld, held.LastFormationSource,
                held.currentFormation, control.currentFormation);
            // 費用（仕様2の論点）で変更が通らなかった可能性は不合格と区別する。
            if (back == BattleQaVerdict.不合格 && held.SkillPoints < held.formationPeaceCost) back = BattleQaVerdict.未判定;
            Log.AddCheck(new BattleQaCheck("保持解除後は軍団AIの陣形へ戻る（隷下A）", back,
                "保持=" + held.IsFormationHeld + " 最後に決めた=" + held.LastFormationSource + " 陣形=" + held.currentFormation +
                " 軍団AIの陣形（対照）=" + control.currentFormation + " スキルP=" + held.SkillPoints.ToString("0.0"), Elapsed));

            Log.AddCheck(new BattleQaCheck("軍団所属と軍団旗艦の保持",
                membershipChanges == 0 ? BattleQaVerdict.合格 : BattleQaVerdict.不合格,
                "観測中の変化フレーム数=" + membershipChanges, Elapsed));
        }

        private int CountMembershipChanges(Dictionary<int, string> corps, Dictionary<int, bool> flagship)
        {
            int n = 0;
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                FleetStrength f = kv.Value;
                if (f == null || !corps.ContainsKey(kv.Key)) continue;
                if (f.corpsName != corps[kv.Key] || f.IsCorpsFlagship != flagship[kv.Key]) n++;
            }
            return n;
        }

        private Squadron SquadronOf(int id)
        {
            FleetStrength f = Fleet(id);
            return f != null ? f.GetComponent<Squadron>() : null;
        }

        // ===================================================================
        // 画面
        // ===================================================================

        private void OnGUI()
        {
            if (!showOverlay || Preset == null) return;
            var sb = new StringBuilder();
            sb.Append("固定会戦QA（Editor専用・自然発火の確認には使えない）\n");
            sb.Append("プリセット=").Append(Preset.name).Append(" seed=").Append(Preset.seed)
              .Append(" run_id=").Append(Log != null ? Log.runId : "-").Append('\n');
            sb.Append("段階=").Append(CurrentPhase).Append(" AI=").Append(Preset.AiMode)
              .Append(" 経過=").Append(Elapsed.ToString("0.0")).Append("秒 timeScale=").Append(Time.timeScale.ToString("0.##"))
              .Append(" 一時停止一致=").Append(IsPauseConsistent).Append('\n');
            if (Tuning != null) sb.Append(Tuning.Describe()).Append('\n');
            if (Switches != null)
            {
                sb.Append(Switches.Describe()).Append('\n');
                sb.Append("スイッチ適用＝開始前（実時間 ").Append(SwitchesAppliedRealtime >= 0f ? SwitchesAppliedRealtime.ToString("0.0") + " 秒" : "未適用")
                  .Append("）　OFF：");
                bool anyOff = false;
                foreach (BattleQaFeature f in new[] { BattleQaFeature.自動包囲, BattleQaFeature.援軍, BattleQaFeature.戦況イベント })
                    if (!Switches.Get(f)) { sb.Append(anyOff ? "・" : "").Append(f); anyOff = true; }
                if (!anyOff) sb.Append("なし");
                sb.Append('\n');
                if (Switches.Get(BattleQaFeature.援軍) && ReinforcementPlan != null)
                    sb.Append("援軍：到着予定 ").Append(ReinforcementPlan.arrivalSeconds.ToString("0.##")).Append(" 秒 予約=")
                      .Append(reinforcementSetup != null ? reinforcementSetup.PendingReinforcementCount : 0)
                      .Append(" 出現=").Append(ReinforcementSpawnCount).Append(" QA通知=").Append(qaNotifications.Count).Append('\n');
            }
            sb.Append(eventManager != null
                ? "隔離：勝敗判定・戦略Tick・稟議と他シーンの会戦イベントを停止中（QA所有の戦況イベントは自然抽選中）\n"
                : "隔離：会戦イベント・勝敗判定・戦略Tick・稟議を停止中\n");
            foreach (KeyValuePair<int, FleetStrength> kv in fleets)
            {
                FleetStrength f = kv.Value;
                if (f == null) { sb.Append("ID").Append(kv.Key).Append(" 消失\n"); continue; }
                var mo = f.GetComponent<FleetMorale>();
                var ai = f.GetComponent<FleetAI>();
                var sq = f.GetComponent<Squadron>();
                sb.Append("ID").Append(kv.Key).Append(' ').Append(f.admiralName)
                  .Append(" 艦艇数=").Append(f.strength).Append('/').Append(f.maxStrength)
                  .Append(" 士気=").Append(mo != null ? mo.morale.ToString("0.0") : "-")
                  .Append(mo != null && mo.IsRouted ? "［敗走］" : "")
                  .Append(f.activeMoraleLock ? "［不退転］" : "")
                  .Append(" 陣形=").Append(sq != null ? sq.currentFormation.ToString() : "-")
                  .Append(sq != null && sq.IsFormationHeld ? "［保持］" : "")
                  .Append(" AI=").Append(ai != null && ai.enabled ? ai.currentState.ToString() : "停止")
                  .Append('\n');
            }
            if (Log != null)
                sb.Append("判定：合格 ").Append(Log.CountVerdict(BattleQaVerdict.合格))
                  .Append(" / 不合格 ").Append(Log.CountVerdict(BattleQaVerdict.不合格))
                  .Append(" / 未判定 ").Append(Log.CountVerdict(BattleQaVerdict.未判定)).Append('\n');
            string text = sb.ToString();
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            GUI.Box(new Rect(8f, 8f, OverlayWidth, lines * OverlayLineHeight + 8f), text);
        }
    }
}
#endif
