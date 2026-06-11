using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 決裁ボード（DESK・決裁状況の管理画面）。決裁を <b>Kanban のように列で管理</b>する全画面UI＝
    /// 未処理 / 最小化 / 自動処理 / 決裁済 の4列に <see cref="DecisionDeck.Queue"/> の各決裁を並べる。
    /// <b>K キー</b>で開閉（<see cref="GameInput"/> 集約）。表示中は <c>Time.timeScale=0</c>＋<see cref="GameClock"/> ポーズ。
    /// 右下の <see cref="DecisionDeck"/>（能動フィード）に対し、こちらは全体の俯瞰・管理ビュー。Strategy/Battle へ自動生成。
    /// </summary>
    public class DecisionBoardPanel : MonoBehaviour
    {
        private static DecisionBoardPanel instance;
        /// <summary>開いているか（GalaxyView 等が入力を譲るのに使う）。</summary>
        public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

        private GameObject root;
        private TMP_FontAsset jpFont;
        private float savedTimeScale = 1f;
        private bool pausedClock;
        private string lastSig = "";
        private Transform[] columns;            // 列の中身（4本）
        private TextMeshProUGUI[] columnHeaders; // 列見出し（件数表示）

        private static readonly string[] ColumnNames = { "未処理", "最小化", "自動処理", "決裁済" };

        // ===== 自動生成 =====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (UnityEngine.Object.FindAnyObjectByType<DecisionBoardPanel>() != null) return;
            new GameObject("DecisionBoardPanel").AddComponent<DecisionBoardPanel>();
        }

        // ===== ライフサイクル =====

        private void Awake()
        {
            instance = this;
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            EnsureEventSystem();
            BuildUI();
            SetVisible(false);
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.決裁ボード切替)) Toggle();

            if (root != null && root.activeSelf)
            {
                string sig = Sig();
                if (sig != lastSig) { RefreshColumns(); lastSig = sig; }
            }
        }

        public void Toggle()
        {
            if (root != null && root.activeSelf) Close();
            else Open();
        }

        private void Open()
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            GameClock clock = StrategySession.Clock;
            if (clock != null && !clock.paused) { clock.Pause(); pausedClock = true; }
            lastSig = "";          // 開いたら必ず再構築
            RefreshColumns();
            SetVisible(true);
        }

        private void Close()
        {
            SetVisible(false);
            Time.timeScale = savedTimeScale;
            GameClock clock = StrategySession.Clock;
            if (clock != null && pausedClock) { clock.Resume(); pausedClock = false; }
        }

        private void SetVisible(bool v) { if (root != null) root.SetActive(v); }

        // ===== 列の更新 =====

        private string Sig()
        {
            var sb = new StringBuilder();
            var q = DecisionDeck.Queue;
            if (q != null)
                for (int i = 0; i < q.items.Count; i++)
                {
                    var d = q.items[i];
                    if (d == null) continue;
                    sb.Append(d.id).Append(':').Append((int)d.status).Append('|');
                }
            return sb.ToString();
        }

        private static int ColumnFor(DecisionStatus s)
        {
            switch (s)
            {
                case DecisionStatus.新着:
                case DecisionStatus.提示中: return 0; // 未処理
                case DecisionStatus.最小化: return 1; // 最小化
                case DecisionStatus.自動解決: return 2; // 自動処理
                case DecisionStatus.決裁済: return 3; // 決裁済
                default: return 0;
            }
        }

        private void RefreshColumns()
        {
            int[] counts = new int[4];
            for (int c = 0; c < columns.Length; c++)
                for (int i = columns[c].childCount - 1; i >= 0; i--)
                    Destroy(columns[c].GetChild(i).gameObject);

            var q = DecisionDeck.Queue;
            if (q != null)
                for (int i = 0; i < q.items.Count; i++)
                {
                    var d = q.items[i];
                    if (d == null) continue;
                    int col = ColumnFor(d.status);
                    AddChip(columns[col], d);
                    counts[col]++;
                }

            for (int c = 0; c < columnHeaders.Length; c++)
                columnHeaders[c].text = $"{ColumnNames[c]}　{counts[c]}";
        }

        private void AddChip(Transform parent, PendingDecision d)
        {
            var chip = new GameObject("Chip_" + d.id);
            chip.transform.SetParent(parent, false);
            var bg = chip.AddComponent<Image>();
            bg.color = CardColor(d.severity);
            var vlg = chip.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.spacing = 2f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var csf = chip.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddLabel(chip.transform, $"<b>[{d.severity}]</b> {d.title}", 16f,
                d.severity == DecisionSeverity.重大 ? new Color(1f, 0.85f, 0.85f) : Color.white);
            AddLabel(chip.transform, $"<size=12><color=#9fb0c0>{d.source}</color></size>", 12f,
                new Color(0.62f, 0.69f, 0.78f));
        }

        private static Color CardColor(DecisionSeverity s)
        {
            switch (s)
            {
                case DecisionSeverity.重大: return new Color(0.45f, 0.10f, 0.12f, 0.96f);
                case DecisionSeverity.重要: return new Color(0.42f, 0.30f, 0.08f, 0.94f);
                case DecisionSeverity.通常: return new Color(0.10f, 0.20f, 0.32f, 0.92f);
                default: return new Color(0.16f, 0.18f, 0.22f, 0.90f);
            }
        }

        // ===== UI 構築 =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("DecisionBoardCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 全画面ディマー（root）
            root = new GameObject("Root");
            root.transform.SetParent(canvasObj.transform, false);
            var rrt = root.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            root.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 0.92f);

            // 縦：タイトル行 ＋ 列行
            var frame = new GameObject("Frame");
            frame.transform.SetParent(root.transform, false);
            var frt = frame.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.04f, 0.06f); frt.anchorMax = new Vector2(0.96f, 0.94f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            var fvlg = frame.AddComponent<VerticalLayoutGroup>();
            fvlg.spacing = 10f;
            fvlg.childControlWidth = true; fvlg.childControlHeight = true;
            fvlg.childForceExpandWidth = true; fvlg.childForceExpandHeight = false;

            // タイトル
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(frame.transform, false);
            var tle = titleGo.AddComponent<LayoutElement>(); tle.minHeight = 44f;
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "決裁ボード　―　決裁状況の管理（K で閉じる）";
            title.fontSize = 28f; title.color = new Color(1f, 0.85f, 0.4f);
            title.alignment = TextAlignmentOptions.Left;
            if (jpFont != null) title.font = jpFont;

            // 列行（横並び・残り高さを占める）
            var cols = new GameObject("Columns");
            cols.transform.SetParent(frame.transform, false);
            var cle = cols.AddComponent<LayoutElement>(); cle.flexibleHeight = 1f;
            var hlg = cols.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            columns = new Transform[4];
            columnHeaders = new TextMeshProUGUI[4];
            for (int c = 0; c < 4; c++)
                columns[c] = MakeColumn(cols.transform, c);

            root.SetActive(false);
        }

        private Transform MakeColumn(Transform parent, int index)
        {
            var col = new GameObject("Column_" + index);
            col.transform.SetParent(parent, false);
            col.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.9f);
            var le = col.AddComponent<LayoutElement>(); le.flexibleWidth = 1f;
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 6f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            // 見出し
            var headGo = new GameObject("Header");
            headGo.transform.SetParent(col.transform, false);
            var hle = headGo.AddComponent<LayoutElement>(); hle.minHeight = 32f;
            var head = headGo.AddComponent<TextMeshProUGUI>();
            head.text = ColumnNames[index];
            head.fontSize = 20f; head.color = new Color(0.8f, 0.88f, 0.96f);
            head.alignment = TextAlignmentOptions.Center;
            if (jpFont != null) head.font = jpFont;
            columnHeaders[index] = head;

            // 中身（上詰め・はみ出しはクリップ）
            var body = new GameObject("Body");
            body.transform.SetParent(col.transform, false);
            var ble = body.AddComponent<LayoutElement>(); ble.flexibleHeight = 1f;
            body.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
            body.AddComponent<RectMask2D>();
            var bvlg = body.AddComponent<VerticalLayoutGroup>();
            bvlg.padding = new RectOffset(6, 6, 6, 6);
            bvlg.spacing = 6f;
            bvlg.childControlWidth = true; bvlg.childControlHeight = true;
            bvlg.childForceExpandWidth = true; bvlg.childForceExpandHeight = false;
            bvlg.childAlignment = TextAnchor.UpperCenter;

            return body.transform;
        }

        private void AddLabel(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            if (jpFont != null) label.font = jpFont;
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
