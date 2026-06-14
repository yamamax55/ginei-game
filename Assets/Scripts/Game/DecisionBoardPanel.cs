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
        private object escWindowToken; // UIWindowStack 登録トークン（#ウィンドウESC）
        private string lastSig = "";
        private Transform[] columns;            // 列の中身（4本）
        private TextMeshProUGUI[] columnHeaders; // 列見出し（件数表示）

        // 詳細ウィンドウ（チップをクリックで開く）
        private GameObject detailRoot;
        private Image detailFrameImage;
        private GameObject detailArtHolder;
        private Image detailArt;
        private TextMeshProUGUI detailTitle;
        private TextMeshProUGUI detailBody;
        private Transform detailChoices;

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
            // ESC は UIWindowStack 経由で「手前から閉じる」（#ウィンドウESC）。K は従来どおり開閉トグル。
            escWindowToken = UIWindowStack.Register(() => root != null && root.activeSelf, Close, 1100, "決裁ボード");
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            if (instance == this) instance = null;
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
            // クリックで詳細ウィンドウを開く
            var chipBtn = chip.AddComponent<Button>();
            chipBtn.targetGraphic = bg;
            PendingDecision clicked = d;
            chipBtn.onClick.AddListener(() => OpenDetail(clicked));
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

        // ===== 詳細ウィンドウ（チップをクリックで開く） =====

        private void OpenDetail(PendingDecision d)
        {
            if (d == null || detailRoot == null) return;
            if (detailFrameImage != null) // モーダルの色味を決裁の重要度に合わせる（少し暗く・不透明）
            {
                var cc = CardColor(d.severity);
                detailFrameImage.color = new Color(cc.r * 0.8f, cc.g * 0.8f, cc.b * 0.8f, 0.98f);
            }

            // フレーバー画像（あれば枠つきで表示・無ければ枠ごと隠す）
            Sprite flavor = LoadFlavor(d);
            if (detailArt != null) detailArt.sprite = flavor;
            if (detailArtHolder != null) detailArtHolder.SetActive(flavor != null);

            detailTitle.text = $"<b>[{d.severity}]</b> {d.title}";
            detailBody.text = string.IsNullOrEmpty(d.body) ? "（詳細なし）" : d.body;

            for (int i = detailChoices.childCount - 1; i >= 0; i--)
                Destroy(detailChoices.GetChild(i).gameObject);

            bool resolved = d.status == DecisionStatus.自動解決 || d.status == DecisionStatus.決裁済;
            if (resolved)
            {
                string how = d.status == DecisionStatus.自動解決 ? "自動処理" : "決裁済";
                AddLabel(detailChoices, $"<color=#9fe0a0>{how}：{ChoiceLabel(d, d.chosenIndex)}</color>", 18f, Color.white);
            }
            else
            {
                for (int i = 0; i < d.choices.Count; i++)
                {
                    int idx = i; PendingDecision dd = d;
                    AddButton(detailChoices, d.choices[i], () => ResolveFromDetail(dd, idx));
                }
            }
            AddButton(detailChoices, "閉じる", CloseDetail);

            detailRoot.transform.SetAsLastSibling(); // 最前面へ
            detailRoot.SetActive(true);
        }

        private void CloseDetail() { if (detailRoot != null) detailRoot.SetActive(false); }

        private void ResolveFromDetail(PendingDecision d, int idx)
        {
            if (d == null) return;
            DecisionDeck.Queue.Resolve(d, idx);
            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                $"［裁可］{d.title} → {ChoiceLabel(d, idx)}");
            CloseDetail();
            lastSig = ""; RefreshColumns(); // 列を即更新（決裁済へ移動）
        }

        private static string ChoiceLabel(PendingDecision d, int idx)
        {
            if (d == null || d.choices == null || idx < 0 || idx >= d.choices.Count) return "（既定）";
            return d.choices[idx];
        }

        /// <summary>決裁のフレーバー画像を Resources/Flavor から読む（imageKey 指定が無ければ flavor_default）。無ければ null。</summary>
        private static Sprite LoadFlavor(PendingDecision d)
        {
            string key = (d != null && !string.IsNullOrEmpty(d.imageKey)) ? d.imageKey : "flavor_default";
            return Resources.Load<Sprite>("Flavor/" + key);
        }

        private GameObject AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var btnObj = new GameObject("Button");
            btnObj.transform.SetParent(parent, false);
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.26f, 0.40f, 0.56f, 1f);
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            if (action != null) btn.onClick.AddListener(action);
            var le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 42f; le.preferredHeight = 42f;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            var rt = txtObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
            var tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 18f; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white; tmp.raycastTarget = false;
            if (jpFont != null) tmp.font = jpFont;
            return btnObj;
        }

        private void BuildDetail()
        {
            detailRoot = new GameObject("Detail");
            detailRoot.transform.SetParent(root.transform, false);
            var drt = detailRoot.AddComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var dim = detailRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            var dimBtn = detailRoot.AddComponent<Button>(); // 外側クリックで閉じる
            dimBtn.targetGraphic = dim;
            dimBtn.transition = UnityEngine.UI.Selectable.Transition.None; // Ginei.Selectable と区別＋ちらつき防止
            dimBtn.onClick.AddListener(CloseDetail);

            var frame = new GameObject("DetailFrame");
            frame.transform.SetParent(detailRoot.transform, false);
            var frt = frame.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.5f, 0.5f); frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f); frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(640f, 0f);
            detailFrameImage = frame.AddComponent<Image>();
            detailFrameImage.color = new Color(0.06f, 0.08f, 0.13f, 0.99f);
            var fvlg = frame.AddComponent<VerticalLayoutGroup>();
            fvlg.padding = new RectOffset(24, 24, 20, 20);
            fvlg.spacing = 12f;
            fvlg.childControlWidth = true; fvlg.childControlHeight = true;
            fvlg.childForceExpandWidth = true; fvlg.childForceExpandHeight = false;
            var fcsf = frame.AddComponent<ContentSizeFitter>();
            fcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            detailTitle = MakeDetailLabel(frame.transform, 26f, new Color(1f, 0.85f, 0.4f), 40f);

            // フレーバー画像（金枠つき）。Resources/Flavor/{imageKey} を読み込み、preserveAspect でレターボックス表示
            detailArtHolder = new GameObject("Art");
            detailArtHolder.transform.SetParent(frame.transform, false);
            var artBorder = detailArtHolder.AddComponent<Image>();
            artBorder.color = new Color(0.72f, 0.6f, 0.34f, 1f); // 金枠
            var artLe = detailArtHolder.AddComponent<LayoutElement>();
            artLe.preferredHeight = 270f; artLe.minHeight = 120f;
            var inner = new GameObject("Img");
            inner.transform.SetParent(detailArtHolder.transform, false);
            var irt = inner.AddComponent<RectTransform>();
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(5f, 5f); irt.offsetMax = new Vector2(-5f, -5f);
            var innerBg = inner.AddComponent<Image>();
            innerBg.color = new Color(0.02f, 0.03f, 0.05f, 1f); // 余白（レターボックス）の地色
            var artGo = new GameObject("Sprite");
            artGo.transform.SetParent(inner.transform, false);
            var art = artGo.AddComponent<RectTransform>();
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;
            detailArt = artGo.AddComponent<Image>();
            detailArt.preserveAspect = true;
            detailArt.raycastTarget = false;

            detailBody = MakeDetailLabel(frame.transform, 18f, new Color(0.9f, 0.92f, 0.96f), 60f);

            var cc = new GameObject("Choices");
            cc.transform.SetParent(frame.transform, false);
            cc.AddComponent<RectTransform>();
            var cvlg = cc.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing = 8f; cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            detailChoices = cc.transform;

            detailRoot.SetActive(false);
        }

        private TextMeshProUGUI MakeDetailLabel(Transform parent, float size, Color color, float minHeight)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size; tmp.color = color; tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true; tmp.raycastTarget = false;
            if (jpFont != null) tmp.font = jpFont;
            var le = go.AddComponent<LayoutElement>(); le.minHeight = minHeight;
            return tmp;
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
            var dimImage = root.AddComponent<Image>();
            dimImage.color = new Color(0.02f, 0.03f, 0.06f, 0.92f);
            WindowChrome.MakeNonModal(dimImage); // ウィンドウ化＝非モーダル（盤面を塞がない）

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

            WindowChrome.AddTitleBarLayout(frt, "決裁ボード", () => SetVisible(false));

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

            BuildDetail();
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
