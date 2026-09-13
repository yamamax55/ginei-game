using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 陣形の保持まわりの<b>短い状態変化</b>を、会戦が終わってシーンが替わったあとでも読み返せるように
    /// 記録する QA 補助（Play 中のみ記録・Editor 限定）。
    ///
    /// <b>なぜ要るか</b>：実機で「標的を撃沈 → その直後の保持」を読む前に会戦が決着して
    /// 戦略マップへ自動帰還してしまい、肝心の遷移を観測できなかった。
    /// 画面（HUD・通知）は流れて消えるので、<b>変化した瞬間だけ</b>を静的な履歴へ残す。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item><b>記録するだけ</b>。陣形も保持も命令も一切変更しない（結果を強制設定しない）。</item>
    ///   <item>履歴は<b>上限つき</b>（<see cref="Capacity"/>）。古いものから捨てる。</item>
    ///   <item>履歴は static なので<b>シーンが替わっても残る</b>。記録係だけが
    ///         <c>DontDestroyOnLoad</c> で生き延びる。</item>
    ///   <item><b>次の Play セッションへ混ぜない</b>＝Play に入るたびに履歴を消す。
    ///         Play を抜けたら記録係を片付ける（履歴は読めるまま残す）。</item>
    ///   <item>士気やスキルポイントの<b>自然な増減では記録しない</b>（ログ洪水を避ける）。
    ///         意味のある変化が起きたときに、その前後の値を添える。</item>
    /// </list>
    ///
    /// <b>観測の限界</b>：Play のフレームごとに見ているので、
    /// 1フレーム未満で元へ戻るような変化は写らない。記録が無いことは「起きなかった」証明にならない。
    /// </summary>
    public static class FormationObservationQaMenu
    {
        /// <summary>残す履歴の上限（古いものから捨てる）。</summary>
        private const int Capacity = 400;

        private static readonly List<string> log = new List<string>();
        private static bool truncated;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // ★新しい Play セッションの記録に前回ぶんを混ぜない。
            if (state == PlayModeStateChange.EnteredPlayMode) { log.Clear(); truncated = false; }

            // Play を抜けるときは記録係を片付ける（履歴は読めるまま残す）。
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                FormationChangeRecorder rec = Object.FindFirstObjectByType<FormationChangeRecorder>();
                if (rec != null) Object.DestroyImmediate(rec.gameObject);
            }
        }

        /// <summary>1行足す（上限を超えたら古いものから捨て、捨てたことを覚えておく）。</summary>
        internal static void Add(string line)
        {
            log.Add(line);
            while (log.Count > Capacity) { log.RemoveAt(0); truncated = true; }
        }

        internal static int Count => log.Count;

        /// <summary>上限超過で古い行を捨てたか（固定会戦QAの記録喪失の明示に使う）。</summary>
        internal static bool Truncated => truncated;

        // ===== メニュー =====

        [MenuItem("Ginei/QA: 陣形保持 変化の記録を開始（Play中）", false, 349)]
        public static void StartRecording()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("陣形保持の記録", "Play 中に実行してください。", "OK");
                return;
            }
            if (Object.FindFirstObjectByType<FormationChangeRecorder>() != null)
            {
                EditorUtility.DisplayDialog("陣形保持の記録", "すでに記録中です。", "OK");
                return;
            }

            var go = new GameObject("QA_FormationChangeRecorder");
            Object.DontDestroyOnLoad(go);     // ★会戦が終わって戦略へ戻っても記録を続ける
            go.AddComponent<FormationChangeRecorder>();

            EditorUtility.DisplayDialog("陣形保持の記録",
                "記録を開始しました（シーンが替わっても続きます）。\n\n" +
                "陣形の指定・保持の解除・攻撃標的の増減・敗走・所属の変化・生存の変化が\n" +
                "起きた瞬間だけを残します（士気やスキルPの自然な増減では残しません）。\n\n" +
                "会戦が終わって戦略へ戻ったあとでも\n" +
                "「QA: 陣形保持 変化の記録を出力」で読み返せます。\n\n" +
                "※記録するだけで、状態は一切変更しません。", "OK");
        }

        [MenuItem("Ginei/QA: 陣形保持 変化の記録を停止", false, 350)]
        public static void StopRecording()
        {
            FormationChangeRecorder rec = Object.FindFirstObjectByType<FormationChangeRecorder>();
            if (rec == null)
            {
                EditorUtility.DisplayDialog("陣形保持の記録", "記録していません。", "OK");
                return;
            }
            Object.DestroyImmediate(rec.gameObject);
            EditorUtility.DisplayDialog("陣形保持の記録",
                "記録を停止しました（履歴は残っています）。", "OK");
        }

        [MenuItem("Ginei/QA: 陣形保持 変化の記録を出力", false, 351)]
        public static void DumpRecording()
        {
            var sb = new StringBuilder();
            sb.Append("陣形の保持まわりの変化（古い順）。記録するだけで状態は変えていません。\n");
            sb.Append("★観測の限界：Play のフレームごとに見ているため、1フレーム未満で元へ戻る変化は写りません。\n");
            sb.Append("　記録が無いことは「起きなかった」証明にはなりません。\n");
            if (truncated)
                sb.Append("★上限 ").Append(Capacity).Append(" 行を超えたため、古い行を捨てています。\n");
            sb.Append("　記録中か＝").Append(Object.FindFirstObjectByType<FormationChangeRecorder>() != null)
              .Append("　行数＝").Append(log.Count).Append("\n\n");

            if (log.Count == 0) sb.Append("（記録なし）\n");
            for (int i = 0; i < log.Count; i++) sb.Append(log[i]).Append('\n');

            string msg = sb.ToString();
            Debug.Log("[陣形保持・変化の記録]\n" + msg);
            EditorUtility.DisplayDialog("陣形保持の記録",
                log.Count + " 行を出力しました。\n\n全文は Console の [陣形保持・変化の記録] に出しています。", "OK");
        }

        [MenuItem("Ginei/QA: 陣形保持 変化の記録を消去", false, 352)]
        public static void ClearRecording()
        {
            log.Clear();
            truncated = false;
            EditorUtility.DisplayDialog("陣形保持の記録", "履歴を消去しました。", "OK");
        }
    }

    /// <summary>
    /// 上の記録係の本体（Editor アセンブリなので製品には入らない）。
    /// <c>DontDestroyOnLoad</c> で会戦→戦略のシーン遷移をまたいで生き延びる。
    /// </summary>
    public class FormationChangeRecorder : MonoBehaviour
    {
        /// <summary>1艦隊ぶんの前回値（この値からの変化だけを記録する）。</summary>
        private struct Snap
        {
            public Formation formation;
            public bool held;
            public FormationOrderSource source;
            public FormationOrderSource lastSource;
            public ManualOverrideKind overrideKind;
            public bool hasManualTarget;
            public string targetName;
            public bool routed;
            public bool moraleLock;      // 不退転（#2175）が効いているか
            public float morale;         // 士気の値（★自然増減では記録しない＝変化行に添えるだけ）
            public bool alive;
            public string corpsName;
            public float skillPoints;
            public bool corpsRetreat;
        }

        private readonly Dictionary<int, Snap> last = new Dictionary<int, Snap>();
        private readonly Dictionary<int, string> names = new Dictionary<int, string>();
        private string lastSceneName = "";
        private long lastNoteSeq;

        private void Start()
        {
            lastSceneName = SceneManager.GetActiveScene().name;
            IReadOnlyList<Notification> all = NotificationCenter.All;
            lastNoteSeq = all.Count > 0 ? all[all.Count - 1].seq : 0;
            FormationObservationQaMenu.Add(Stamp() + " 記録開始");
        }

        private void Update()
        {
            // シーンが替わった（会戦の決着で戦略へ帰還した等）。
            string scene = SceneManager.GetActiveScene().name;
            if (scene != lastSceneName)
            {
                FormationObservationQaMenu.Add(Stamp() + " ■シーン変化 " + lastSceneName + " → " + scene);
                lastSceneName = scene;
            }

            var seen = new HashSet<int>();
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null) continue;

                // 安定した識別子：GameObject の instance id（1会戦のあいだ不変）。名前も併記する。
                int id = f.gameObject.GetHashCode();
                seen.Add(id);
                if (!names.ContainsKey(id)) names[id] = f.gameObject.name + "/" + f.admiralName;

                Snap now = Capture(f);
                if (!last.TryGetValue(id, out Snap prev))
                {
                    last[id] = now;
                    FormationObservationQaMenu.Add(Stamp() + " 初回観測 " + names[id] + "  " + Describe(now));
                    continue;
                }

                string change = Diff(prev, now);
                if (string.IsNullOrEmpty(change)) { last[id] = now; continue; }

                // ★意味のある変化が起きたときだけ記録し、その前後のスキルPを添える
                //   （スキルPや士気の自然な増減だけでは記録しない＝ログ洪水を避ける）。
                FormationObservationQaMenu.Add(
                    Stamp() + " " + names[id] + "  " + change +
                    "  [スキルP " + prev.skillPoints.ToString("0.0") + " → " + now.skillPoints.ToString("0.0") + "]" +
                    "  " + Describe(now));
                AppendNewNotifications();
                last[id] = now;
            }

            // 消えた艦隊（撃沈・退却・シーン破棄）。
            if (seen.Count != last.Count)
            {
                var gone = new List<int>();
                foreach (KeyValuePair<int, Snap> kv in last)
                    if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
                for (int i = 0; i < gone.Count; i++)
                {
                    string n = names.TryGetValue(gone[i], out string nm) ? nm : "(不明)";
                    FormationObservationQaMenu.Add(Stamp() + " ●消滅（レジストリから除外） " + n);
                    last.Remove(gone[i]);
                }
            }
        }

        private static Snap Capture(FleetStrength f)
        {
            var sq = f.GetComponent<Squadron>();
            var ai = f.GetComponent<FleetAI>();
            var wp = f.GetComponent<FleetWeapon>();
            var mo = f.GetComponent<FleetMorale>();

            Squadron target = wp != null ? wp.ManualTargetFleet : null;
            FleetStrength targetFs = target != null ? target.GetComponent<FleetStrength>() : null;

            return new Snap
            {
                formation = sq != null ? sq.currentFormation : Formation.紡錘陣,
                held = sq != null && sq.IsFormationHeld,
                source = sq != null ? sq.FormationHold.source : FormationOrderSource.なし,
                lastSource = sq != null ? sq.LastFormationSource : FormationOrderSource.なし,
                overrideKind = ai != null ? ai.OverrideKind : ManualOverrideKind.なし,
                hasManualTarget = wp != null && wp.HasManualTarget,
                targetName = targetFs != null ? targetFs.admiralName : (target != null ? target.name : ""),
                routed = mo != null && mo.IsRouted,
                moraleLock = f.activeMoraleLock,
                morale = mo != null ? mo.morale : 0f,
                alive = f.IsAlive,
                corpsName = f.corpsName ?? "",
                skillPoints = sq != null ? sq.SkillPoints : 0f,
                corpsRetreat = BattlefieldCommandManager.IsCorpsRetreatOrdered(CorpsFormation.KeyFor(f)),
            };
        }

        /// <summary>意味のある変化だけを文にする（無ければ空文字＝記録しない）。</summary>
        private static string Diff(in Snap a, in Snap b)
        {
            var sb = new StringBuilder();
            if (a.formation != b.formation) Append(sb, "陣形 " + a.formation + "→" + b.formation);
            if (a.held != b.held) Append(sb, b.held ? "★保持 開始" : "★保持 解除");
            if (a.source != b.source) Append(sb, "指定元 " + a.source + "→" + b.source);
            if (!b.held && a.lastSource != b.lastSource)
                Append(sb, "最後に決めた " + a.lastSource + "→" + b.lastSource);
            if (a.overrideKind != b.overrideKind)
                Append(sb, "override " + a.overrideKind + "→" + b.overrideKind);
            if (a.hasManualTarget != b.hasManualTarget)
                Append(sb, b.hasManualTarget ? "★手動標的 設定(" + b.targetName + ")" : "★手動標的 喪失(直前=" + a.targetName + ")");
            else if (a.hasManualTarget && a.targetName != b.targetName)
                Append(sb, "手動標的 " + a.targetName + "→" + b.targetName);
            // ★不退転の入り／切れは記録する（敗走との前後関係を読むのに要る）。
            //   ただし士気の値そのものは変化の引き金にしない（自然増減でログを増やさないため）。
            if (a.moraleLock != b.moraleLock)
                Append(sb, b.moraleLock ? "★不退転 発動" : "★不退転 終了");
            if (a.routed != b.routed)
                Append(sb, (b.routed ? "★敗走 開始" : "敗走 解除")
                         + "（士気 " + a.morale.ToString("0.0") + "→" + b.morale.ToString("0.0")
                         + " 不退転=" + b.moraleLock + "）");
            if (a.alive != b.alive) Append(sb, b.alive ? "生存 回復" : "★生存 喪失");
            if (a.corpsName != b.corpsName) Append(sb, "所属 " + Show(a.corpsName) + "→" + Show(b.corpsName));
            if (a.corpsRetreat != b.corpsRetreat)
                Append(sb, b.corpsRetreat ? "★軍団総退却 発令" : "軍団総退却 解除");
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string s)
        {
            if (sb.Length > 0) sb.Append(" / ");
            sb.Append(s);
        }

        private static string Show(string s) => string.IsNullOrEmpty(s) ? "(なし)" : s;

        /// <summary>変化後の状態の要約（あとから読んで前後関係が分かるように）。</summary>
        private static string Describe(in Snap s)
        {
            return "{陣形=" + s.formation
                 + " 保持=" + s.held + (s.held ? "(" + s.source + ")" : "")
                 + " override=" + s.overrideKind
                 + " 標的=" + (s.hasManualTarget ? s.targetName : "なし")
                 + " 敗走=" + s.routed
                 + " 士気=" + s.morale.ToString("0.0")
                 + " 不退転=" + s.moraleLock
                 + " 生存=" + s.alive
                 + " 所属=" + Show(s.corpsName)
                 + " 軍団総退却=" + s.corpsRetreat + "}";
        }

        /// <summary>
        /// 変化が起きた前後の通知を拾って添える。
        ///
        /// ★<b>士気を動かす出来事の通知も通す</b>（#士気原因の切り分け）。
        /// 以前は 陣形／保持／要請 しか通さなかったため、
        /// 「会戦イベント：味方の士気が上がった（+6）」のような<b>原因の通知が1行も残らず</b>、
        /// 記録には「敗走 解除（士気 0.0→6.0）」という結果だけが写っていた
        /// （過去ログに 与ダメ内訳 や 特殊指揮 が写っていたのは、
        ///  文面に「陣形」、艦隊名に「要請」がたまたま含まれていたためで、意図した通過ではない）。
        /// 通すのは原因になりうる少数の語だけ＝通常時のログ洪水にはしない。
        /// </summary>
        private void AppendNewNotifications()
        {
            IReadOnlyList<Notification> all = NotificationCenter.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].seq <= lastNoteSeq) continue;
                lastNoteSeq = all[i].seq;
                string m = all[i].message;
                if (string.IsNullOrEmpty(m)) continue;
                if (!IsInteresting(m)) continue;
                FormationObservationQaMenu.Add("        └ 通知: " + m);
            }
        }

        /// <summary>記録に添える価値のある通知か（陣形まわり＋士気を動かす出来事）。</summary>
        private static bool IsInteresting(string m)
        {
            return m.IndexOf("陣形", System.StringComparison.Ordinal) >= 0
                || m.IndexOf("保持", System.StringComparison.Ordinal) >= 0
                || m.IndexOf("要請", System.StringComparison.Ordinal) >= 0
                // ↓ 士気を動かす出来事（原因の候補）。いずれも稀にしか出ない。
                || m.IndexOf("会戦イベント", System.StringComparison.Ordinal) >= 0
                || m.IndexOf("撃墜", System.StringComparison.Ordinal) >= 0
                || m.IndexOf("撃沈", System.StringComparison.Ordinal) >= 0
                || m.IndexOf("捨てがまり", System.StringComparison.Ordinal) >= 0;
        }

        private static string Stamp()
        {
            string clock = "";
            if (TimeDisplay.TryFormatNow(out string c, out _)) clock = " 暦=" + c.Replace('\n', ' ');
            return "F" + Time.frameCount
                 + " t=" + Time.unscaledTime.ToString("0.00")
                 + " ts=" + Time.timeScale.ToString("0.0")
                 + clock;
        }
    }
}
