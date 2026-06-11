using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 操作ヘルプ・オーバーレイ。Hキーで表示/非表示を切り替える。TimeScale 非依存（ポーズ中も開閉可）。
    /// Battle/Strategy シーンに自動生成（RuntimeInitializeOnLoadMethod）。キーボード操作は <see cref="GameInput"/> から
    /// 現在シーンの有効アクションを自動列挙してカテゴリ別に配置する（#107・新キー追加で自動反映＝手書き更新不要）。
    /// </summary>
    public class HelpOverlay : MonoBehaviour
    {
        // ===== 調整可能なパラメーター =====

        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1100;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.75f;

        [Tooltip("ヘルプパネルの幅（ピクセル）")]
        public float panelWidth = 680f;

        [Tooltip("ヘルプパネルの最大高さ（ピクセル）")]
        public float panelMaxHeight = 540f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.04f, 0.06f, 0.10f, 0.97f);

        [Tooltip("見出しのフォントサイズ")]
        public float headerFontSize = 20f;

        [Tooltip("操作項目のフォントサイズ")]
        public float itemFontSize = 16f;

        [Tooltip("閉じるボタンのフォントサイズ")]
        public float closeBtnFontSize = 20f;

        [Header("ウィンドウ装飾（Windows風）")]
        [Tooltip("タイトルバーの色")]
        public Color titleBarColor = new Color(0.13f, 0.30f, 0.55f, 1f);

        [Tooltip("ウィンドウ枠線の色")]
        public Color borderColor = new Color(0.55f, 0.62f, 0.72f, 1f);

        [Tooltip("タイトルバーの高さ（ピクセル）")]
        public float titleBarHeight = 34f;

        // ===== 内部状態 =====

        // オーバーレイ全体の root（Canvas オブジェクト）
        private GameObject overlayRoot;
        // 表示/非表示を切り替えるパネル本体（ディマー＋コンテンツ）
        private GameObject panel;
        // このヘルプのシーン文脈（Strategy/Battle で出すキー・操作を切り替える）
        private InputContext helpContext = InputContext.会戦;

        // ウィンドウ本体の RectTransform（ドラッグ移動・最小化/最大化のサイズ操作対象）
        private RectTransform windowRT;
        // タイトルバー以外のクライアント領域（最小化で隠す）
        private GameObject clientArea;
        // 通常時のウィンドウサイズ（最小化/最大化からの復元用）
        private Vector2 normalSize;
        private bool maximized;
        private bool minimized;
        // 最大化時のサイズ（参照解像度 1920x1080 内に収める）
        private static readonly Vector2 MaximizedSize = new Vector2(1760f, 980f);

        // ===== 自動生成エントリーポイント =====

        /// <summary>
        /// Battle シーンが読み込まれるたびに HelpOverlay を自動生成する。
        /// RuntimeInitializeOnLoadMethod はアプリ起動時に1回しか呼ばれないため、
        /// Title→Battle のような実行時のシーン遷移にも対応できるよう sceneLoaded を購読する。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            // 起動直後に既に Battle なら即生成（Battle シーンを直接再生した場合に対応）
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreate(scene);
        }

        /// <summary>Battle シーンに HelpOverlay が無ければ生成する（重複生成ガード）。</summary>
        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle" && scene.name != "Strategy") return;
            if (Object.FindAnyObjectByType<HelpOverlay>() != null) return;

            GameObject go = new GameObject("HelpOverlay");
            go.AddComponent<HelpOverlay>();
        }

        // ===== Unity ライフサイクル =====

        private void Awake()
        {
            // シーンから文脈を決定（このシーンで有効なキー/操作だけをヘルプに出す）
            helpContext = SceneManager.GetActiveScene().name == "Strategy"
                ? InputContext.戦略 : InputContext.会戦;
            BuildUI();
            // 初期状態は非表示
            SetVisible(false);
        }

        private void Update()
        {
            // ヘルプ開閉（入力は GameInput に集約・#107／共通アクションでどのシーンでも有効）
            if (GameInput.WasPressed(GameAction.ヘルプ切替))
            {
                Toggle();
            }
        }

        // ===== 公開API =====

        /// <summary>ヘルプ表示を開閉する。</summary>
        public void Toggle()
        {
            bool next = panel != null && !panel.activeSelf;
            SetVisible(next);
        }

        /// <summary>ヘルプパネルの表示状態を直接指定する。</summary>
        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== UI 構築 =====

        /// <summary>
        /// ヘルプオーバーレイのUI一式をコードで生成する。
        /// Canvas / ディマー / スクロールパネル / テキストブロック / 閉じるボタン。
        /// </summary>
        private void BuildUI()
        {
            // EventSystem を保証（新Input System 用）
            EnsureEventSystem();

            // Canvas（最前面 ScreenSpaceOverlay）
            overlayRoot = new GameObject("HelpOverlayCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            // 全画面ディマー兼パネル root（これを panel として表示切替）
            panel = new GameObject("HelpPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            panelRT.anchoredPosition = Vector2.zero;

            // ディマー背景
            Image dimImage = panel.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, dimAlpha);

            // 中央のヘルプコンテンツフレームを生成
            BuildHelpContentPanel(panel.transform);
        }

        /// <summary>
        /// ヘルプウィンドウ（Windows 風）を生成する：枠線つきウィンドウ＋タイトルバー＋クライアント領域。
        /// タイトルバーをつかんでドラッグ移動でき、[－][□][×] で最小化/最大化/閉じる。
        /// </summary>
        private void BuildHelpContentPanel(Transform parent)
        {
            // ウィンドウ本体（固定サイズ・中央配置・枠線つき）
            GameObject window = new GameObject("HelpWindow");
            window.transform.SetParent(parent, false);
            windowRT = window.AddComponent<RectTransform>();
            windowRT.anchorMin = new Vector2(0.5f, 0.5f);
            windowRT.anchorMax = new Vector2(0.5f, 0.5f);
            windowRT.pivot = new Vector2(0.5f, 0.5f);
            windowRT.anchoredPosition = Vector2.zero;
            windowRT.sizeDelta = new Vector2(panelWidth, panelMaxHeight);
            normalSize = windowRT.sizeDelta;

            Image windowImg = window.AddComponent<Image>();
            windowImg.color = panelColor;
            // ウィンドウ枠線（Windows 風の縁取り）
            Outline border = window.AddComponent<Outline>();
            border.effectColor = borderColor;
            border.effectDistance = new Vector2(2f, -2f);

            // 縦並び：タイトルバー（固定高）＋クライアント領域（残り）
            VerticalLayoutGroup vlg = window.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            string sceneLabel = helpContext == InputContext.戦略 ? "戦略マップ" : "会戦";
            BuildTitleBar(window.transform, $"操作ヘルプ ・ {sceneLabel}");
            BuildClientArea(window.transform);
        }

        /// <summary>タイトルバー（左：キャプション／右：[－][□][×]）を生成する。ドラッグで移動可。</summary>
        private void BuildTitleBar(Transform parent, string caption)
        {
            GameObject bar = new GameObject("TitleBar");
            bar.transform.SetParent(parent, false);
            bar.AddComponent<RectTransform>();
            Image barImg = bar.AddComponent<Image>();
            barImg.color = titleBarColor;

            LayoutElement barLE = bar.AddComponent<LayoutElement>();
            barLE.minHeight = titleBarHeight;
            barLE.preferredHeight = titleBarHeight;

            HorizontalLayoutGroup hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 0, 0, 0);
            hlg.spacing = 0f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // タイトルバーをつかんでウィンドウをドラッグ移動（再利用可能な独立コンポーネント）
            UIWindowDrag drag = bar.AddComponent<UIWindowDrag>();
            drag.target = windowRT;

            // キャプション（左寄せ・残り幅を占有してボタンを右端へ押しやる）
            GameObject capObj = new GameObject("Caption");
            capObj.transform.SetParent(bar.transform, false);
            TextMeshProUGUI cap = capObj.AddComponent<TextMeshProUGUI>();
            cap.text = caption;
            cap.fontSize = headerFontSize;
            cap.color = Color.white;
            cap.alignment = TextAlignmentOptions.Left;
            cap.raycastTarget = false; // バー本体にドラッグを通す
            ApplyJapaneseFont(cap);
            LayoutElement capLE = capObj.AddComponent<LayoutElement>();
            capLE.flexibleWidth = 1f;

            // システムボタン（右）：最小化 / 最大化・復元 / 閉じる
            BuildSysButton(bar.transform, "－", false, ToggleMinimize);
            BuildSysButton(bar.transform, "□", false, ToggleMaximize);
            BuildSysButton(bar.transform, "×", true, () => SetVisible(false));
        }

        /// <summary>タイトルバーのシステムボタン1個（ホバーで色変化・閉じるは赤）。</summary>
        private void BuildSysButton(Transform parent, string glyph, bool isClose,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject b = new GameObject(isClose ? "BtnClose" : "BtnSys");
            b.transform.SetParent(parent, false);
            Image img = b.AddComponent<Image>();
            img.color = Color.white;

            Button btn = b.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = titleBarColor;
            cb.highlightedColor = isClose
                ? new Color(0.86f, 0.15f, 0.18f, 1f)
                : new Color(0.27f, 0.45f, 0.68f, 1f);
            cb.pressedColor = isClose
                ? new Color(0.70f, 0.10f, 0.13f, 1f)
                : new Color(0.20f, 0.36f, 0.58f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            LayoutElement le = b.AddComponent<LayoutElement>();
            le.minWidth = 44f;
            le.preferredWidth = 44f;
            le.flexibleWidth = 0f;

            GameObject t = new GameObject("Glyph");
            t.transform.SetParent(b.transform, false);
            RectTransform trt = t.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
            tmp.text = glyph;
            tmp.fontSize = headerFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            ApplyJapaneseFont(tmp);
        }

        /// <summary>クライアント領域（操作一覧スクロール＋下部ボタンバー）を生成する。</summary>
        private void BuildClientArea(Transform parent)
        {
            clientArea = new GameObject("ClientArea");
            clientArea.transform.SetParent(parent, false);
            clientArea.AddComponent<RectTransform>();
            LayoutElement le = clientArea.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f; // タイトルバー以外の残り領域を占有

            VerticalLayoutGroup vlg = clientArea.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // スクロールビュー（操作一覧）
            BuildScrollView(clientArea.transform);
            // 下部ボタンバー（Windows 風の「閉じる」ボタンを右寄せ）
            CreateBottomBar(clientArea.transform);
        }

        /// <summary>最小化：クライアント領域を隠してタイトルバーだけにする（トグル）。</summary>
        private void ToggleMinimize()
        {
            minimized = !minimized;
            if (minimized)
            {
                maximized = false;
                clientArea.SetActive(false);
                windowRT.sizeDelta = new Vector2(normalSize.x, titleBarHeight);
            }
            else
            {
                clientArea.SetActive(true);
                windowRT.sizeDelta = normalSize;
            }
        }

        /// <summary>最大化/復元：通常サイズと最大化サイズを切り替える（トグル）。</summary>
        private void ToggleMaximize()
        {
            if (minimized) { minimized = false; clientArea.SetActive(true); }
            maximized = !maximized;
            windowRT.sizeDelta = maximized ? MaximizedSize : normalSize;
        }

        /// <summary>
        /// 操作一覧テキストを含むスクロールビューを生成する。
        /// </summary>
        private void BuildScrollView(Transform parent)
        {
            // ScrollRect 外枠
            GameObject scrollObj = new GameObject("HelpScrollRect");
            scrollObj.transform.SetParent(parent, false);
            scrollObj.AddComponent<RectTransform>();
            LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;  // 残り領域をすべて使う

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            // Viewport（マスク）
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportRT.anchoredPosition = Vector2.zero;
            // ドラッグ受け用に透明 Image を置き、クリップは RectMask2D で行う。
            // ※ Mask＋alpha0 Image だとステンシルが書かれず中身が全てクリップされて消えるため使わない。
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            // Content（縦並び・自動伸長）
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(8, 8, 4, 4);
            contentVlg.spacing = 4f;
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            // 操作一覧を追加
            PopulateHelpContent(content.transform);
        }

        // キーボード操作のカテゴリ表示順（#107・自動列挙したアクションをこの順でグループ化）。
        private static readonly string[] CategoryOrder =
            { "システム", "観測オーバーレイ", "時間・ポーズ", "カメラ", "部隊グループ", "会戦", "その他" };

        /// <summary>
        /// 操作一覧を生成する。マウス/メニュー操作はシーン別にハードコード（GameInput 対象外）、
        /// キーボード操作は <see cref="GameInput.ActionsInContext"/> から現在シーンの有効アクションを
        /// 自動列挙し <see cref="CategoryOf"/> でグループ化する（#107・新キー追加で自動反映）。
        /// </summary>
        private void PopulateHelpContent(Transform parent)
        {
            // ---- マウス/メニュー操作（シーン別・ハードコード） ----
            if (helpContext == InputContext.戦略) PopulateStrategyMouse(parent);
            else PopulateBattleMouse(parent);

            // ---- キーボード操作（GameInput から自動生成・カテゴリ別） ----
            var actions = GameInput.ActionsInContext(helpContext);
            foreach (string cat in CategoryOrder)
            {
                bool headerAdded = false;
                for (int i = 0; i < actions.Count; i++)
                {
                    GameAction a = actions[i];
                    if (CategoryOf(a) != cat) continue;
                    string keys = GameInput.KeyLabelFull(a);
                    if (string.IsNullOrEmpty(keys)) continue; // 未割当は出さない
                    if (!headerAdded) { AddGroupHeader(parent, "■ " + cat); headerAdded = true; }
                    AddItem(parent, keys, GameInput.GetDescription(a));
                }
            }
        }

        /// <summary>アクションの表示カテゴリ（HelpOverlay のグループ見出し）。未分類は「その他」。</summary>
        private static string CategoryOf(GameAction a)
        {
            switch (a)
            {
                case GameAction.ヘルプ切替:
                case GameAction.キャンセル: return "システム";
                case GameAction.観測オーバーレイ切替:
                case GameAction.状態インスペクタ切替:
                case GameAction.軍観測切替:
                case GameAction.通知ログ切替:
                case GameAction.経済観測切替:
                case GameAction.決裁ボード切替:
                case GameAction.人物名鑑切替: return "観測オーバーレイ";
                case GameAction.ポーズ:
                case GameAction.倍速等速:
                case GameAction.倍速2倍:
                case GameAction.倍速3倍: return "時間・ポーズ";
                case GameAction.カメラ上:
                case GameAction.カメラ下:
                case GameAction.カメラ左:
                case GameAction.カメラ右:
                case GameAction.選択フォーカス: return "カメラ";
                case GameAction.グループ選択1:
                case GameAction.グループ選択2:
                case GameAction.グループ選択3: return "部隊グループ";
                case GameAction.リスタート:
                case GameAction.戦略へ復帰: return "会戦";
                default: return "その他";
            }
        }

        /// <summary>会戦シーンのマウス/メニュー操作（ハードコード）。</summary>
        private void PopulateBattleMouse(Transform parent)
        {
            AddGroupHeader(parent, "■ 選択（マウス）");
            AddItem(parent, "左クリック", "部隊を選択（空白クリックで解除）");
            AddItem(parent, "右クリック", "コマンドメニューを開く");

            AddGroupHeader(parent, "■ 移動・後退（メニュー→マウス）");
            AddItem(parent, "メニュー「移動」", "右押下で位置→ドラッグで向き→離して確定（Escで取消）");
            AddItem(parent, "メニュー「後退」", "向き（射界）を保ったまま下がる");

            AddGroupHeader(parent, "■ 攻撃・目標指定（メニュー→マウス）");
            AddItem(parent, "メニュー「攻撃」→左ク", "通常攻撃を即時発令");
            AddItem(parent, "メニュー「攻撃」→右ク", "攻撃種別メニュー（通常/ミサイル）");

            AddGroupHeader(parent, "■ 陣形・編制");
            AddItem(parent, "メニュー「陣形変更」", "紡錘陣 / 鶴翼陣 / 円陣 / 横陣 / 方陣");
            AddItem(parent, "O", "編制管理（軍集団 ⊃ 軍団 ⊃ 艦隊）");

            AddGroupHeader(parent, "■ カメラ（マウス）");
            AddItem(parent, "中ボタンドラッグ", "カメラパン");
            AddItem(parent, "マウスホイール", "ズームイン / アウト");
            AddItem(parent, "画面端（設定で有効化）", "カーソルを端に寄せるとパン");
        }

        /// <summary>戦略マップのマウス/キー操作（ハードコード＝GalaxyView の直読み分）。</summary>
        private void PopulateStrategyMouse(Transform parent)
        {
            AddGroupHeader(parent, "■ 戦略マップ（マウス）");
            AddItem(parent, "左クリック", "戦略艦隊を選択（Shiftで追加）");
            AddItem(parent, "右クリック", "星系へ進軍 / 回廊上で停止保持");
            AddItem(parent, "回廊ダブルクリック", "交戦中の回廊へ潜行（手動指揮）");
            AddItem(parent, "星系ダブルクリック", "システムビュー（恒星系の閲覧）");

            AddGroupHeader(parent, "■ 戦略マップ（キー）");
            AddItem(parent, "Space / 1・2・3", "時間の停止 / 速度");
            AddItem(parent, "I", "星系情報パネル");
            AddItem(parent, "[ / ]", "税率を下げる / 上げる");
            AddItem(parent, "B", "艦隊編成（プール配分・提督/副提督/参謀の配属）");
        }

        // ===== ヘルパー：コンテンツ行生成 =====

        /// <summary>グループ見出し行を生成する（前後にスペーサー付き）。</summary>
        private void AddGroupHeader(Transform parent, string title)
        {
            // 上余白スペーサー
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.minHeight = 6f;
            spacerLE.preferredHeight = 6f;

            // 見出しテキスト
            CreateTmpLabel(parent, title, headerFontSize,
                new Color(1f, 0.85f, 0.3f), headerFontSize + 6f);
        }

        /// <summary>操作1項目（キー列 + 説明列）の横並び行を生成する。</summary>
        private void AddItem(Transform parent, string key, string description)
        {
            // 横並びコンテナ
            GameObject row = new GameObject("Row");
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            // 折り返しで複数行になる説明列に合わせ、キー/区切りは行の先頭（上）へ寄せる。
            hlg.childAlignment = TextAnchor.UpperLeft;
            // childControlWidth=true＝HLG が各列幅を配分（key/sep は固定幅・desc は残り幅）。
            // これで説明列の折り返し幅が確定し、TMP が正しい折り返し高さを報告できる。
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(4, 0, 0, 0);

            // 行高は固定せず内容（折り返した説明列）に追従させる。
            // preferredHeight を固定すると2行以上の説明が行からはみ出し、下の行と重なる（スクロール時バグ）。
            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.minHeight = itemFontSize + 6f;

            // キー列（固定幅）
            GameObject keyObj = new GameObject("Key");
            keyObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI keyTmp = keyObj.AddComponent<TextMeshProUGUI>();
            keyTmp.text = key;
            keyTmp.fontSize = itemFontSize;
            keyTmp.color = new Color(0.9f, 0.95f, 1f);
            keyTmp.alignment = TextAlignmentOptions.Left;
            keyTmp.raycastTarget = false;
            ApplyJapaneseFont(keyTmp);
            LayoutElement keyLE = keyObj.AddComponent<LayoutElement>();
            keyLE.minWidth = 240f;
            keyLE.preferredWidth = 240f;
            keyLE.flexibleWidth = 0f;

            // 区切り文字
            GameObject sep = new GameObject("Sep");
            sep.transform.SetParent(row.transform, false);
            TextMeshProUGUI sepTmp = sep.AddComponent<TextMeshProUGUI>();
            sepTmp.text = "：";
            sepTmp.fontSize = itemFontSize;
            sepTmp.color = new Color(0.5f, 0.55f, 0.6f);
            sepTmp.alignment = TextAlignmentOptions.Center;
            sepTmp.raycastTarget = false;
            ApplyJapaneseFont(sepTmp);
            LayoutElement sepLE = sep.AddComponent<LayoutElement>();
            sepLE.minWidth = 20f;
            sepLE.preferredWidth = 20f;
            sepLE.flexibleWidth = 0f;

            // 説明列（残り幅を使い切る）
            GameObject descObj = new GameObject("Desc");
            descObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI descTmp = descObj.AddComponent<TextMeshProUGUI>();
            descTmp.text = description;
            descTmp.fontSize = itemFontSize;
            descTmp.color = new Color(0.85f, 0.85f, 0.85f);
            descTmp.alignment = TextAlignmentOptions.Left;
            descTmp.raycastTarget = false;
            ApplyJapaneseFont(descTmp);
            LayoutElement descLE = descObj.AddComponent<LayoutElement>();
            descLE.flexibleWidth = 1f;
        }

        // ===== ヘルパー：共通 UI 部品 =====

        /// <summary>
        /// TMP ラベルを生成して parent に追加し、TextMeshProUGUI を返す。
        /// </summary>
        private TextMeshProUGUI CreateTmpLabel(Transform parent, string text,
            float fontSize, Color color, float minHeight)
        {
            // ラベル名に使う文字数を制限してオブジェクト名を作る
            int nameLen = Mathf.Min(text.Length, 12);
            GameObject go = new GameObject("Label_" + text.Substring(0, nameLen));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            ApplyJapaneseFont(tmp);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = minHeight;
            return tmp;
        }

        /// <summary>
        /// 下部ボタンバーを生成する：右寄せに Windows 風の「閉じる」ボタン（明色＋縁取り＋濃色文字）。
        /// Hキー / タイトルバーの×と同じく SetVisible(false) を呼ぶ。
        /// </summary>
        private void CreateBottomBar(Transform parent)
        {
            GameObject bar = new GameObject("ButtonBar");
            bar.transform.SetParent(parent, false);
            bar.AddComponent<RectTransform>();
            LayoutElement barLE = bar.AddComponent<LayoutElement>();
            barLE.minHeight = 40f;
            barLE.preferredHeight = 40f;

            HorizontalLayoutGroup hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // 左スペーサー（ボタンを右端へ押しやる）
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(bar.transform, false);
            spacer.AddComponent<RectTransform>();
            LayoutElement sLE = spacer.AddComponent<LayoutElement>();
            sLE.flexibleWidth = 1f;

            // 「閉じる」ボタン（Windows 風）
            GameObject btnObj = new GameObject("CloseButton");
            btnObj.transform.SetParent(bar.transform, false);
            Image img = btnObj.AddComponent<Image>();
            img.color = Color.white;
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.85f, 0.86f, 0.90f, 1f);
            cb.highlightedColor = new Color(0.93f, 0.96f, 1f, 1f);
            cb.pressedColor = new Color(0.72f, 0.78f, 0.88f, 1f);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.onClick.AddListener(() => SetVisible(false));

            Outline ol = btnObj.AddComponent<Outline>();
            ol.effectColor = new Color(0.45f, 0.5f, 0.6f, 1f);
            ol.effectDistance = new Vector2(1f, -1f);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minWidth = 150f;
            le.preferredWidth = 150f;
            le.minHeight = 34f;
            le.preferredHeight = 34f;

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRT = txtObj.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            txtRT.anchoredPosition = Vector2.zero;
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "閉じる ( H )";
            tmp.fontSize = closeBtnFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.1f, 0.12f, 0.16f, 1f); // 明色ボタン上の濃色文字（Windows 風）
            tmp.raycastTarget = false;
            ApplyJapaneseFont(tmp);
        }

        /// <summary>
        /// Resources の "JapaneseFont_TMP" を tmp に適用する（文字化け防止）。
        /// PauseManager / TitleManager / FleetHUDManager と同一の呼び方。
        /// </summary>
        private static void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        /// <summary>
        /// シーンに EventSystem が無ければ InputSystemUIInputModule 付きで生成する。
        /// （PauseManager と同一の実装）
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
