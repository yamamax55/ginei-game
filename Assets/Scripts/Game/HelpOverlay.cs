using System.Collections.Generic;
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
    /// 見た目はゲーム意匠（暗い宇宙＋金色見出し）に合わせ、ほぼ全画面・2カラムで視認性を確保する。
    /// </summary>
    public class HelpOverlay : MonoBehaviour
    {
        // ===== 調整可能なパラメーター =====

        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1100;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.78f;

        [Tooltip("画面端からのウィンドウ余白（参照解像度ピクセル）。小さいほど全画面に近い")]
        public float windowMargin = 48f;

        [Tooltip("ウィンドウ背景色")]
        public Color panelColor = new Color(0.05f, 0.07f, 0.12f, 0.97f);

        [Tooltip("見出し・タイトル・装飾線の金色アクセント")]
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);

        [Tooltip("ウィンドウ枠線の色")]
        public Color borderColor = new Color(0.45f, 0.40f, 0.22f, 0.9f);

        [Tooltip("タイトルのフォントサイズ")]
        public float titleFontSize = 28f;

        [Tooltip("見出しのフォントサイズ")]
        public float headerFontSize = 22f;

        [Tooltip("操作項目のフォントサイズ")]
        public float itemFontSize = 18f;

        // ===== 内部状態 =====

        // オーバーレイ全体の root（Canvas オブジェクト）
        private GameObject overlayRoot;
        // 表示/非表示を切り替えるパネル本体（ディマー＋コンテンツ）
        private GameObject panel;
        // このヘルプのシーン文脈（Strategy/Battle で出すキー・操作を切り替える）
        private InputContext helpContext = InputContext.会戦;

        // 2カラム配置用：各カラムの Transform と、均等配分のための累積項目数
        private Transform leftColumn;
        private Transform rightColumn;
        private int leftItemCount;
        private int rightItemCount;

        // ===== 自動生成エントリーポイント =====

        /// <summary>
        /// Battle/Strategy シーンが読み込まれるたびに HelpOverlay を自動生成する。
        /// RuntimeInitializeOnLoadMethod はアプリ起動時に1回しか呼ばれないため、
        /// シーン遷移にも対応できるよう sceneLoaded を購読する。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreate(scene);
        }

        /// <summary>Battle/Strategy シーンに HelpOverlay が無ければ生成する（重複生成ガード）。</summary>
        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle" && scene.name != "Strategy") return;
            if (Object.FindAnyObjectByType<HelpOverlay>() != null) return;

            GameObject go = new GameObject("HelpOverlay");
            go.AddComponent<HelpOverlay>();
        }

        // ===== Unity ライフサイクル =====

        // UIWindowStack 登録トークン（#ウィンドウESC）
        private object escWindowToken;

        private void Awake()
        {
            // シーンから文脈を決定（このシーンで有効なキー/操作だけをヘルプに出す）
            helpContext = SceneManager.GetActiveScene().name == "Strategy"
                ? InputContext.戦略 : InputContext.会戦;
            BuildUI();
            // 初期状態は非表示
            SetVisible(false);
            // ESC は UIWindowStack 経由で「手前から閉じる」（H は従来どおり開閉トグル）。
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "ヘルプ");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

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
        /// Canvas / 全画面ディマー / 全画面ウィンドウ（ヘッダー＋本文）。
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
            WindowChrome.MakeNonModal(dimImage); // ウィンドウ化＝非モーダル（盤面を塞がない）

            // 全画面ウィンドウを生成
            BuildHelpContentPanel(panel.transform);
        }

        /// <summary>
        /// ヘルプウィンドウ（ゲーム意匠・ほぼ全画面）を生成する：枠線つきウィンドウ＋細いヘッダー＋本文。
        /// </summary>
        private void BuildHelpContentPanel(Transform parent)
        {
            // ウィンドウ本体（画面端から windowMargin の余白・枠線つき・全画面ストレッチ）
            GameObject window = new GameObject("HelpWindow");
            window.transform.SetParent(parent, false);
            RectTransform rt = window.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(windowMargin, windowMargin);
            rt.offsetMax = new Vector2(-windowMargin, -windowMargin);

            Image bg = window.AddComponent<Image>();
            bg.color = panelColor;
            Outline border = window.AddComponent<Outline>();
            border.effectColor = borderColor;
            border.effectDistance = new Vector2(2f, -2f);

            // 縦並び：ヘッダー（固定高）＋本文（残り）
            VerticalLayoutGroup vlg = window.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            string sceneLabel = helpContext == InputContext.戦略 ? "戦略マップ" : "会戦";
            BuildHeader(window.transform, $"操作ヘルプ　・　{sceneLabel}");
            BuildBody(window.transform);
        }

        /// <summary>
        /// 細いヘッダー帯を生成する：左にタイトル（金・太字）／右に小さな×／下端に金色のルール線。
        /// 高さは帯オブジェクト自身の LayoutElement で固定し、横並びは内側の伸縮子（HLG）で行う
        /// ＝高さ制御と HLG を別オブジェクトに分けることでヘッダーが肥大化するレイアウト不具合を防ぐ。
        /// </summary>
        private void BuildHeader(Transform parent, string title)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent, false);
            header.AddComponent<RectTransform>();
            Image hbg = header.AddComponent<Image>();
            hbg.color = new Color(0.09f, 0.12f, 0.18f, 1f);
            LayoutElement hle = header.AddComponent<LayoutElement>();
            hle.minHeight = 60f;
            hle.preferredHeight = 60f;
            WindowChrome.MakeDraggable(header, parent as RectTransform); // ウィンドウ化：ヘッダーをつかんで移動

            // 内側の伸縮子（ヘッダー帯いっぱいに広げ、ここで横並びを行う）
            GameObject inner = new GameObject("HeaderInner");
            inner.transform.SetParent(header.transform, false);
            RectTransform irt = inner.AddComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(28f, 0f);
            irt.offsetMax = new Vector2(-6f, 0f);

            HorizontalLayoutGroup hlg = inner.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 0f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // タイトル（金・太字・左寄せ・残り幅を占有して×を右端へ）
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(inner.transform, false);
            TextMeshProUGUI t = titleObj.AddComponent<TextMeshProUGUI>();
            t.text = title;
            t.fontSize = titleFontSize;
            t.color = accentColor;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Left;
            t.raycastTarget = false;
            ApplyJapaneseFont(t);
            LayoutElement tle = titleObj.AddComponent<LayoutElement>();
            tle.flexibleWidth = 1f;

            // 閉じる×（右端・ホバーで赤）
            BuildCloseGlyph(inner.transform);

            // ヘッダー下端の金色ルール線
            GameObject rule = new GameObject("Rule");
            rule.transform.SetParent(header.transform, false);
            RectTransform rrt = rule.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(0f, 2f);
            rrt.anchoredPosition = Vector2.zero;
            Image ri = rule.AddComponent<Image>();
            ri.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.55f);
            ri.raycastTarget = false;
        }

        /// <summary>ヘッダー右端の小さな閉じる×ボタン（ホバーで赤）。</summary>
        private void BuildCloseGlyph(Transform parent)
        {
            GameObject b = new GameObject("CloseGlyph");
            b.transform.SetParent(parent, false);
            Image img = b.AddComponent<Image>();
            img.color = Color.white;

            Button btn = b.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.09f, 0.12f, 0.18f, 1f); // ヘッダー背景になじませる
            cb.highlightedColor = new Color(0.86f, 0.15f, 0.18f, 1f);
            cb.pressedColor = new Color(0.70f, 0.10f, 0.13f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.onClick.AddListener(() => SetVisible(false));

            LayoutElement le = b.AddComponent<LayoutElement>();
            le.minWidth = 52f;
            le.preferredWidth = 52f;
            le.flexibleWidth = 0f;

            GameObject g = new GameObject("X");
            g.transform.SetParent(b.transform, false);
            RectTransform grt = g.AddComponent<RectTransform>();
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.sizeDelta = Vector2.zero;
            grt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI tmp = g.AddComponent<TextMeshProUGUI>();
            tmp.text = "×";
            tmp.fontSize = titleFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            ApplyJapaneseFont(tmp);
        }

        /// <summary>本文（操作一覧スクロール＋下部の閉じる案内）を生成する。</summary>
        private void BuildBody(Transform parent)
        {
            GameObject body = new GameObject("Body");
            body.transform.SetParent(parent, false);
            body.AddComponent<RectTransform>();
            LayoutElement le = body.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f; // ヘッダー以外の残り領域を占有

            VerticalLayoutGroup vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 18, 14);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            BuildScrollView(body.transform);
            BuildFooter(body.transform);
        }

        /// <summary>下部の閉じる案内（控えめなテキスト＝ゲーム意匠に合わせ白ボタンは置かない）。</summary>
        private void BuildFooter(Transform parent)
        {
            GameObject f = new GameObject("Footer");
            f.transform.SetParent(parent, false);
            LayoutElement le = f.AddComponent<LayoutElement>();
            le.minHeight = 26f;
            le.preferredHeight = 26f;
            TextMeshProUGUI t = f.AddComponent<TextMeshProUGUI>();
            t.text = "［ H ］キー または × で閉じる";
            t.fontSize = itemFontSize;
            t.color = new Color(0.6f, 0.65f, 0.72f, 1f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            ApplyJapaneseFont(t);
        }

        /// <summary>
        /// 操作一覧を含むスクロールビューを生成する。コンテンツは2カラム（横並び）で幅を活かす。
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
            scrollRect.scrollSensitivity = 36f;

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

            // Content（2カラム横並び・縦に自動伸長）
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup contentHlg = content.AddComponent<HorizontalLayoutGroup>();
            contentHlg.padding = new RectOffset(8, 8, 4, 4);
            contentHlg.spacing = 48f;
            contentHlg.childAlignment = TextAnchor.UpperLeft;
            contentHlg.childControlWidth = true;
            contentHlg.childControlHeight = true;
            contentHlg.childForceExpandWidth = true;  // 2カラムを等幅に
            contentHlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            // 2カラムを作成し、各セクションを項目数の少ない側へ振り分けて高さを均す
            leftColumn = CreateColumn(content.transform);
            rightColumn = CreateColumn(content.transform);
            leftItemCount = 0;
            rightItemCount = 0;

            PopulateHelpContent();
        }

        /// <summary>2カラムの1列（縦並び・等幅）を生成して Transform を返す。</summary>
        private Transform CreateColumn(Transform parent)
        {
            GameObject col = new GameObject("Column");
            col.transform.SetParent(parent, false);
            col.AddComponent<RectTransform>();
            VerticalLayoutGroup vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            LayoutElement le = col.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            return col.transform;
        }

        /// <summary>項目数の少ないカラムを返し、見出し＋項目ぶんの重みを加算する（高さの均し）。</summary>
        private Transform NextColumn(int itemCount)
        {
            // 見出し＋スペーサーの高さぶんを +2 として概算（厳密な高さ計測はしない）
            int weight = itemCount + 2;
            if (leftItemCount <= rightItemCount)
            {
                leftItemCount += weight;
                return leftColumn;
            }
            rightItemCount += weight;
            return rightColumn;
        }

        // キーボード操作のカテゴリ表示順（#107・自動列挙したアクションをこの順でグループ化）。
        private static readonly string[] CategoryOrder =
            { "システム", "観測オーバーレイ", "時間・ポーズ", "カメラ", "部隊グループ", "会戦", "その他" };

        /// <summary>
        /// 操作一覧を2カラムに生成する。マウス/メニュー操作はシーン別にハードコード（GameInput 対象外）、
        /// キーボード操作は <see cref="GameInput.ActionsInContext"/> から現在シーンの有効アクションを
        /// 自動列挙し <see cref="CategoryOf"/> でグループ化する（#107・新キー追加で自動反映）。
        /// </summary>
        private void PopulateHelpContent()
        {
            // ---- マウス/メニュー操作（シーン別・ハードコード） ----
            if (helpContext == InputContext.戦略) PopulateStrategyMouse();
            else PopulateBattleMouse();

            // ---- キーボード操作（GameInput から自動生成・カテゴリ別） ----
            var actions = GameInput.ActionsInContext(helpContext);
            foreach (string cat in CategoryOrder)
            {
                var items = new List<(string key, string desc)>();
                for (int i = 0; i < actions.Count; i++)
                {
                    GameAction a = actions[i];
                    if (CategoryOf(a) != cat) continue;
                    string keys = GameInput.KeyLabelFull(a);
                    if (string.IsNullOrEmpty(keys)) continue; // 未割当は出さない
                    items.Add((keys, GameInput.GetDescription(a)));
                }
                if (items.Count == 0) continue;
                AddSection("■ " + cat, items.ToArray());
            }
        }

        /// <summary>見出し＋複数項目を1セクションとして、均し配分で選んだカラムへ追加する。</summary>
        private void AddSection(string header, (string key, string desc)[] items)
        {
            Transform col = NextColumn(items.Length);
            AddGroupHeader(col, header);
            foreach (var it in items) AddItem(col, it.key, it.desc);
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
        private void PopulateBattleMouse()
        {
            AddSection("■ 選択（マウス）", new (string, string)[]
            {
                ("左クリック", "部隊を選択（空白クリックで解除）"),
                ("右クリック", "コマンドメニューを開く"),
            });
            AddSection("■ 移動・後退（メニュー→マウス）", new (string, string)[]
            {
                ("メニュー「移動」", "右押下で位置→ドラッグで向き→離して確定（Escで取消）"),
                ("メニュー「後退」", "向き（射界）を保ったまま下がる"),
            });
            AddSection("■ 攻撃・目標指定（メニュー→マウス）", new (string, string)[]
            {
                ("メニュー「攻撃」→左ク", "通常攻撃を即時発令"),
                ("メニュー「攻撃」→右ク", "攻撃種別メニュー（通常/ミサイル）"),
            });
            AddSection("■ 陣形・編制", new (string, string)[]
            {
                ("メニュー「陣形変更」", "紡錘陣 / 鶴翼陣 / 円陣 / 横陣 / 方陣"),
                ("O", "編制管理（軍集団 ⊃ 軍団 ⊃ 艦隊）"),
            });
            AddSection("■ カメラ（マウス）", new (string, string)[]
            {
                ("中ボタンドラッグ", "カメラパン"),
                ("マウスホイール", "ズームイン / アウト"),
                ("画面端（設定で有効化）", "カーソルを端に寄せるとパン"),
            });
        }

        /// <summary>戦略マップのマウス/キー操作（ハードコード＝GalaxyView の直読み分）。</summary>
        private void PopulateStrategyMouse()
        {
            AddSection("■ 戦略マップ（マウス）", new (string, string)[]
            {
                ("左クリック", "戦略艦隊を選択（Shiftで追加）"),
                ("右クリック", "星系へ進軍 / 回廊上で停止保持"),
                ("回廊ダブルクリック", "交戦中の回廊へ潜行（手動指揮）"),
                ("星系ダブルクリック", "システムビュー（恒星系の閲覧）"),
            });
            AddSection("■ 戦略マップ（キー）", new (string, string)[]
            {
                ("Space / 1・2・3", "時間の停止 / 速度"),
                ("I", "星系情報パネル"),
                ("[ / ]", "税率を下げる / 上げる"),
                ("C", "任務発令：マウス直下の敵星系を攻略（参謀本部が必要兵力を自動動員）"),
                ("V", "勢力攻略：対立勢力へ任務（参謀本部が攻撃目標を選定し戦力を集中動員）"),
                ("7 / 8 / 9", "対立勢力へ 宣戦布告 / 講和 / 同盟"),
            });
        }

        // ===== ヘルパー：コンテンツ行生成 =====

        /// <summary>グループ見出し行を生成する（前にスペーサー付き・左寄せ・金色）。</summary>
        private void AddGroupHeader(Transform parent, string title)
        {
            // 上余白スペーサー
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.minHeight = 8f;
            spacerLE.preferredHeight = 8f;

            // 見出しテキスト（左寄せ・金色）
            GameObject go = new GameObject("Header");
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = headerFontSize;
            tmp.color = accentColor;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            ApplyJapaneseFont(tmp);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = headerFontSize + 8f;
            le.preferredHeight = headerFontSize + 8f;
        }

        /// <summary>操作1項目（キー列 + 説明列）の横並び行を生成する。行高は折り返した説明に追従する。</summary>
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
            // preferredHeight を固定すると2行以上の説明が行からはみ出し、下の行と重なる。
            LayoutElement rowLE = row.AddComponent<LayoutElement>();
            rowLE.minHeight = itemFontSize + 8f;

            // キー列（固定幅）
            GameObject keyObj = new GameObject("Key");
            keyObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI keyTmp = keyObj.AddComponent<TextMeshProUGUI>();
            keyTmp.text = key;
            keyTmp.fontSize = itemFontSize;
            keyTmp.color = new Color(0.62f, 0.82f, 1f);
            keyTmp.alignment = TextAlignmentOptions.Left;
            keyTmp.raycastTarget = false;
            ApplyJapaneseFont(keyTmp);
            LayoutElement keyLE = keyObj.AddComponent<LayoutElement>();
            keyLE.minWidth = 250f;
            keyLE.preferredWidth = 250f;
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

            // 説明列（残り幅を使い切る・折り返す）
            GameObject descObj = new GameObject("Desc");
            descObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI descTmp = descObj.AddComponent<TextMeshProUGUI>();
            descTmp.text = description;
            descTmp.fontSize = itemFontSize;
            descTmp.color = new Color(0.86f, 0.88f, 0.92f);
            descTmp.alignment = TextAlignmentOptions.Left;
            descTmp.raycastTarget = false;
            ApplyJapaneseFont(descTmp);
            LayoutElement descLE = descObj.AddComponent<LayoutElement>();
            descLE.flexibleWidth = 1f;
        }

        // ===== ヘルパー：共通 UI 部品 =====

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
