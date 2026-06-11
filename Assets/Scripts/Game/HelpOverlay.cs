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

        // ===== 内部状態 =====

        // オーバーレイ全体の root（Canvas オブジェクト）
        private GameObject overlayRoot;
        // 表示/非表示を切り替えるパネル本体（ディマー＋コンテンツ）
        private GameObject panel;
        // このヘルプのシーン文脈（Strategy/Battle で出すキー・操作を切り替える）
        private InputContext helpContext = InputContext.会戦;

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
        /// スクロール可能なヘルプコンテンツパネルを生成する。
        /// </summary>
        private void BuildHelpContentPanel(Transform parent)
        {
            // 外枠フレーム（固定サイズ・中央配置）
            GameObject frame = new GameObject("HelpFrame");
            frame.transform.SetParent(parent, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0.5f, 0.5f);
            frameRT.anchorMax = new Vector2(0.5f, 0.5f);
            frameRT.pivot = new Vector2(0.5f, 0.5f);
            frameRT.anchoredPosition = Vector2.zero;
            frameRT.sizeDelta = new Vector2(panelWidth, panelMaxHeight);

            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = panelColor;

            // 垂直 LayoutGroup（タイトル + スクロールビュー + 閉じるボタン）
            VerticalLayoutGroup frameVlg = frame.AddComponent<VerticalLayoutGroup>();
            frameVlg.padding = new RectOffset(16, 16, 12, 12);
            frameVlg.spacing = 8f;
            frameVlg.childAlignment = TextAnchor.UpperCenter;
            frameVlg.childControlWidth = true;
            frameVlg.childControlHeight = true;
            frameVlg.childForceExpandWidth = true;
            frameVlg.childForceExpandHeight = false;

            // タイトル行（シーン名を出す）
            string sceneLabel = helpContext == InputContext.戦略 ? "戦略マップ" : "会戦";
            CreateTmpLabel(frame.transform, $"[ 操作ヘルプ・{sceneLabel} ]  (H キーで閉じる)",
                headerFontSize + 4f, new Color(1f, 0.9f, 0.4f), 36f);

            // スクロールビュー（操作一覧）
            BuildScrollView(frame.transform);

            // 閉じるボタン
            CreateCloseButton(frame.transform);
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
                case GameAction.通知ログ切替: return "観測オーバーレイ";
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
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(4, 0, 0, 0);

            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.minHeight = itemFontSize + 6f;
            rowLE.preferredHeight = itemFontSize + 6f;

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
        /// 「閉じる」ボタンを生成する（Hキーと同じく SetVisible(false) を呼ぶ）。
        /// </summary>
        private void CreateCloseButton(Transform parent)
        {
            GameObject btnObj = new GameObject("CloseButton");
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.28f, 0.42f, 1f);
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => SetVisible(false));

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 40f;
            le.preferredHeight = 40f;

            // ボタンラベル
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRT = txtObj.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            txtRT.anchoredPosition = Vector2.zero;
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "閉じる  [ H ]";
            tmp.fontSize = closeBtnFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
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
