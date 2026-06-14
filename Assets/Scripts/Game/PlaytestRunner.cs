using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 自動プレイテストの駆動殻（MonoBehaviour）。AI 対 AI で会戦を速回しし、観測を集めて
    /// 純ロジック <see cref="PlaytestInvariants"/> に渡し、バグ/改善点のレポート（<see cref="PlaytestReport"/>）を JSON で出す。
    /// 「観てる人間が気づく異常」を機械化する第一歩＝CI（GameCI headless Unity）で無人実行する想定。
    ///
    /// 起動方法は2通り：
    ///  ① バッチ実行：起動引数 <c>-ginei-playtest [シナリオ名] [出力パス]</c> があれば <see cref="Bootstrap"/> が自動生成（quit する）。
    ///  ② エディタ：本コンポーネントを任意の GameObject に AddComponent（または Battle シーンに置く）し Play。
    ///
    /// 判定ロジックは持たない（Core 委譲）。観測の収集だけを行う薄い殻＝既存スクリプトは一切変更しない（追加のみ）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaytestRunner : MonoBehaviour
    {
        [Header("実行設定")]
        [Tooltip("回すシナリオ名。空なら GameSettings の現在値を使う。")]
        public string scenarioName = "";
        [Tooltip("観戦の速回し倍率（会戦時間を進める速さ）。")]
        public float timeScale = 4f;
        [Tooltip("この会戦経過秒を超えても決着しなければ未決着として打ち切る。")]
        public float maxDurationSeconds = 180f;
        [Tooltip("サンプリング間隔（会戦経過秒）。")]
        public float sampleInterval = 1f;
        [Tooltip("全艦を FleetAI 制御にする（プレイヤー入力の無い無人実行で会戦が進むように）。")]
        public bool forceAllAI = true;
        [Tooltip("完了時に Application.Quit する（バッチ実行向け）。")]
        public bool quitWhenDone = false;
        [Tooltip("レポート JSON の出力先。空なら persistentDataPath/playtest-report.json。")]
        public string outputPath = "";

        [Header("スクショ（CI 可視化用・既定OFF）")]
        [Tooltip("会戦中のスクリーンショットを撮るか。貧弱PCでもクラウドで会戦を描画→PNGを見るための可視化。")]
        public bool captureScreenshots = false;
        [Tooltip("スクショの出力ディレクトリ。空なら persistentDataPath。")]
        public string screenshotDir = "";
        [Tooltip("スクショ間隔（実時間秒）。CI のソフトGL描画は遅いので実時間基準で等間隔に撮る。")]
        public float screenshotIntervalSeconds = 8f;
        [Tooltip("撮るスクショの最大枚数（撮りすぎ防止）。")]
        public int maxScreenshots = 20;
        [Tooltip("決着しなくても、この実時間秒で打ち切ってレポートを書き出し終了する（CI の timeout 前に確実に締める＝report.json を必ず出す）。")]
        public float maxRealSeconds = 150f;

        /// <summary>起動引数キー。次の引数があればシナリオ名、その次があれば出力パスとして解釈する。</summary>
        public const string PlaytestArg = "-ginei-playtest";

        /// <summary>スクショ撮影の起動引数。次の引数があればスクショ出力ディレクトリ（指定で撮影ON）。</summary>
        public const string ScreenshotArg = "-ginei-shots";

        /// <summary>直近の実行結果（エディタ/テストから参照）。</summary>
        public static PlaytestReport LastReport { get; private set; }

        // --- 観測の蓄積 ---
        private readonly List<string> errorLogs = new List<string>();
        private readonly Dictionary<FleetStrength, Vector3> lastPositions = new Dictionary<FleetStrength, Vector3>();
        private const int MaxErrorLogs = 50;
        private bool resolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int idx = Array.IndexOf(args, PlaytestArg);
            if (idx < 0) return; // 引数が無ければ通常プレイ（何もしない）

            var go = new GameObject("PlaytestRunner");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<PlaytestRunner>();
            r.quitWhenDone = true;
            if (idx + 1 < args.Length && !args[idx + 1].StartsWith("-")) r.scenarioName = args[idx + 1];
            if (idx + 2 < args.Length && !args[idx + 2].StartsWith("-")) r.outputPath = args[idx + 2];

            // -ginei-shots <dir> があればスクショ撮影を有効化（CI 可視化用）。
            int sidx = Array.IndexOf(args, ScreenshotArg);
            if (sidx >= 0)
            {
                r.captureScreenshots = true;
                if (sidx + 1 < args.Length && !args[sidx + 1].StartsWith("-")) r.screenshotDir = args[sidx + 1];
            }
        }

        private void Start() => StartCoroutine(RunPlaytest());

        private IEnumerator RunPlaytest()
        {
            Application.logMessageReceived += OnLog;

            if (!string.IsNullOrEmpty(scenarioName)) GameSettings.Instance.scenarioName = scenarioName;

            // Battle シーンへ（既に Battle ならそのまま）。BattleSetup の生成を数フレーム待つ。
            if (SceneManager.GetActiveScene().name != "Battle")
            {
                SceneManager.LoadScene("Battle");
                yield return null;
            }
            for (int i = 0; i < 3; i++) yield return null;

            scenarioName = GameSettings.Instance.scenarioName;
            if (forceAllAI) ForceAllAI();
            Time.timeScale = Mathf.Max(0.01f, timeScale);

            bool hostileStart = HasHostilePair();
            float timeLimit = ScenarioData.ActiveScenario != null ? ScenarioData.ActiveScenario.timeLimit : 0f;

            var aliveSamples = new List<int>();
            var moveSamples = new List<float>();
            SnapshotPositions();

            SceneManager.activeSceneChanged += OnSceneChanged;

            float elapsed = 0f;
            float nextSample = 0f;
            int shotIndex = 0;
            // スクショ・打ち切りは実時間(unscaled)基準。CI のソフトGLは描画が遅く battle-time が
            // 進みにくいので、実時間で等間隔に撮り、実時間上限で確実にループを抜けてレポートを書く。
            float startReal = Time.realtimeSinceStartup;
            float nextShotReal = startReal;
            while (elapsed < maxDurationSeconds && !resolved
                   && (Time.realtimeSinceStartup - startReal) < maxRealSeconds)
            {
                if (elapsed >= nextSample)
                {
                    aliveSamples.Add(CountAliveCombatantFlagships());
                    moveSamples.Add(AccumulatedMovement());
                    nextSample += Mathf.Max(0.01f, sampleInterval);
                }
                if (captureScreenshots && shotIndex < maxScreenshots
                    && Time.realtimeSinceStartup >= nextShotReal)
                {
                    CaptureShot(shotIndex++);
                    nextShotReal += Mathf.Max(0.1f, screenshotIntervalSeconds);
                }
                elapsed += Time.deltaTime; // 会戦経過（timeScale 追従＝速回し）
                yield return null;
            }

            // 決着の瞬間を1枚撮り、末尾フレームを数回回して PNG を確実に書き出してから quit する。
            if (captureScreenshots && shotIndex < maxScreenshots)
            {
                CaptureShot(shotIndex++);
                for (int k = 0; k < 3; k++) yield return null;
            }

            SceneManager.activeSceneChanged -= OnSceneChanged;
            Application.logMessageReceived -= OnLog;

            var obs = new PlaytestObservations
            {
                scenarioName = scenarioName,
                durationSeconds = elapsed,
                timeLimitSeconds = timeLimit,
                resolved = resolved,
                hadHostilePairAtStart = hostileStart,
                screenWidth = Screen.width > 0 ? Screen.width : 1920,
                screenHeight = Screen.height > 0 ? Screen.height : 1080,
                errorLogs = errorLogs,
                leakedMaterialCount = -1, // v1 は未計測（リーク計測は GameCI/可視化フェーズで追加）
                aliveFlagshipSamples = aliveSamples,
                totalMovementSamples = moveSamples,
                sampleIntervalSeconds = sampleInterval,
                // hudBounds は v1 では収集しない（スクショ/可視化フェーズで追加）。
            };

            var report = PlaytestInvariants.Evaluate(obs);
            LastReport = report;
            WriteReport(report);
            LogSummary(report);

            if (quitWhenDone)
            {
                Time.timeScale = 1f;
                Application.Quit(report.SuggestedExitCode);
            }
        }

        // --- 観測ヘルパ ---

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (errorLogs.Count >= MaxErrorLogs) return; // フラッド防止（打ち切りは Count で分かる）
            errorLogs.Add(condition);
        }

        private void OnSceneChanged(Scene from, Scene to)
        {
            // 会戦の決着で BattleManager が Result へ遷移する＝決着とみなす。
            if (to.name == "Result") resolved = true;
        }

        /// <summary>生存している戦闘艦の旗艦数。</summary>
        private static int CountAliveCombatantFlagships()
        {
            int n = 0;
            var all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                var fs = all[i];
                if (fs != null && fs.IsAlive && fs.IsCombatant) n++;
            }
            return n;
        }

        /// <summary>敵対する戦闘艦旗艦のペアが存在するか（敵対判定は FactionRelations が唯一の窓口）。</summary>
        private static bool HasHostilePair()
        {
            var all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (a == null || !a.IsAlive || !a.IsCombatant) continue;
                for (int j = i + 1; j < all.Count; j++)
                {
                    var b = all[j];
                    if (b == null || !b.IsAlive || !b.IsCombatant) continue;
                    if (FactionRelations.IsHostile(a, b)) return true;
                }
            }
            return false;
        }

        /// <summary>全旗艦の FleetAI を有効化＝プレイヤー入力の無い無人実行でも会戦が進む（AI 対 AI）。</summary>
        private static void ForceAllAI()
        {
            var all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                var ai = all[i] != null ? all[i].GetComponent<FleetAI>() : null;
                if (ai != null) ai.enabled = true;
            }
        }

        private void SnapshotPositions()
        {
            lastPositions.Clear();
            var all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null) lastPositions[all[i]] = all[i].transform.position;
        }

        /// <summary>前回サンプル以降の全旗艦の総移動量（全停止＝移動破綻の検出用）。</summary>
        private float AccumulatedMovement()
        {
            float sum = 0f;
            var all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                var fs = all[i];
                if (fs == null) continue;
                Vector3 cur = fs.transform.position;
                if (lastPositions.TryGetValue(fs, out Vector3 prev)) sum += (cur - prev).magnitude;
                lastPositions[fs] = cur;
            }
            return sum;
        }

        /// <summary>会戦画面のスクショを1枚撮る（CI 可視化用）。ScreenCapture は描画が要る＝-nographics では不可。</summary>
        private void CaptureShot(int index)
        {
            try
            {
                string dir = string.IsNullOrEmpty(screenshotDir) ? Application.persistentDataPath : screenshotDir;
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"shot_{index:000}.png");
                ScreenCapture.CaptureScreenshot(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Playtest] スクショ失敗: {e.Message}");
            }
        }

        private void WriteReport(PlaytestReport report)
        {
            string path = string.IsNullOrEmpty(outputPath)
                ? Path.Combine(Application.persistentDataPath, "playtest-report.json")
                : outputPath;
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log($"[Playtest] レポートを書き出しました: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Playtest] レポート書き出しに失敗: {e.Message}");
            }
        }

        private static void LogSummary(PlaytestReport report)
        {
            Debug.Log($"[Playtest] シナリオ「{report.scenarioName}」 経過{report.durationSeconds:0.0}s " +
                      $"所見{report.findings.Count}件 最高重要度={report.HighestSeverity} 合否={(report.Passed ? "PASS" : "FAIL")}");
            for (int i = 0; i < report.findings.Count; i++)
            {
                var f = report.findings[i];
                Debug.Log($"[Playtest]  - [{f.severity}/{f.category}] {f.message}");
            }
        }
    }
}
