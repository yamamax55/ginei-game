using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// タイトル画面のUI入力を管理するクラス。
    /// シナリオ選択／プレイ陣営選択UIを実行時に生成し、選択を GameSettings に反映する。
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("UI参照")]
        public UnityEngine.UI.Button continueButton;
        public GameObject settingsPanel;

        // 選択UI（実行時生成）
        private System.Collections.Generic.IReadOnlyList<ScenarioData> scenarios;
        private TextMeshProUGUI selectionLabel;
        private TMP_FontAsset jaFont;
        private GameObject selectionRoot;   // 自前生成した Canvas（遷移時に破棄）
        private readonly System.Collections.Generic.List<OptionButton> scenarioOptions = new System.Collections.Generic.List<OptionButton>();
        private readonly System.Collections.Generic.List<OptionButton> factionOptions = new System.Collections.Generic.List<OptionButton>();

        // ハイライト色
        private static readonly Color SelectedBg = new Color(0.95f, 0.75f, 0.15f, 1f);   // 金
        private static readonly Color NormalBg = new Color(0.15f, 0.2f, 0.35f, 0.95f);   // 濃紺

        // メインタイトル画面（実行時生成）
        private GameObject titleScreenRoot;
        private Button continueMenuButton;

        // 設定画面（実行時生成・フルスクリーン）
        private GameObject settingsScreenRoot;
        private TextMeshProUGUI volumeValueLabel;
        private TextMeshProUGUI zoomValueLabel;
        private readonly System.Collections.Generic.List<OptionButton> speedOptions = new System.Collections.Generic.List<OptionButton>();
        private OptionButton gizmoOption;
        private OptionButton edgeScrollOption; // 画面端スクロール（#87）
        // ゲーム速度の選択肢
        private static readonly float[] SpeedChoices = { 1f, 2f, 3f };
        // 開始ズームのスライダー範囲（CameraController の min/max 内に収まる実用域）
        private const float ZoomMin = 8f;
        private const float ZoomMax = 28f;
        // 背景に使う銀河画像（Assets/Resources/ 配下にこの名前で置く。.jpg/.png いずれでも可）
        private const string TitleBgResource = "Textures/TitleBackground";
        // ボタン用の角丸スプライト（アプリ寿命で1枚だけ生成）
        private static Sprite roundedSprite;

        /// <summary>選択UIの1ボタン（ハイライト切替用に参照を保持）。</summary>
        private class OptionButton
        {
            public Button button;
            public Image bg;
            public TextMeshProUGUI text;
            public string label;
            public FactionData faction; // 陣営ボタンのとき対応する FactionData（enum フォールバック時は null）
        }

        private void Start()
        {
            // セーブデータがある場合のみ「続きから」ボタンを有効化
            if (continueButton != null)
            {
                continueButton.interactable = SaveManager.HasSave();
            }

            if (settingsPanel != null) settingsPanel.SetActive(false);

            BuildTitleScreen();   // かっこいいタイトル画面（背景・タイトル文字・メニュー）を生成
            BuildSelectionUI();

            AudioManager.Instance.PlayBGM(AudioManager.Instance.bgmTitle);
        }

        // ----- シナリオ／陣営 選択UI（実行時生成） -----

        private void BuildSelectionUI()
        {
            EnsureJaFont();
            scenarios = ContentDatabase.AllScenarios(); // SO索引は ContentDatabase に集約（FND-1 #496）
            scenarioOptions.Clear();
            factionOptions.Clear();

            Canvas canvas = CreateOwnCanvas();   // 自前 Canvas（Title シーン所有＝遷移で破棄）
            EnsureEventSystem();

            // 全画面のディマー（選択中はタイトルを暗くしてクリックも遮る＝モーダル）
            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            RectTransform dimRT = dim.GetComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero; dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero; dimRT.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // 画面中央に「枠（スクロール領域）」を生成し、その中に縦並びの内容を入れる。
            // 内容が画面より高くなっても枠内でスクロールでき、上下が見切れない（dim の後＝手前に描画）。
            GameObject frame = new GameObject("ScenarioSelectFrame",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            frame.transform.SetParent(canvas.transform, false);

            RectTransform frameRT = frame.GetComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0.5f, 0.5f);
            frameRT.anchorMax = new Vector2(0.5f, 0.5f);
            frameRT.pivot = new Vector2(0.5f, 0.5f);
            frameRT.anchoredPosition = Vector2.zero;
            frameRT.sizeDelta = new Vector2(460f, 900f);   // 高さは内容に合わせて後で調整（上限）
            frame.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.97f);

            // スクロール内容（VerticalLayoutGroup＋ContentSizeFitter）。これが従来の「パネル」。
            GameObject panel = new GameObject("ScenarioSelectContent",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(frame.transform, false);

            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);   // 上辺基準＝上から下へ伸びる（縦スクロール用）
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 14, 14);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = panel.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // スクロール設定（縦のみ・枠外はマスクで隠す）
            ScrollRect scroll = frame.GetComponent<ScrollRect>();
            scroll.content = rt;
            scroll.viewport = frameRT;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            // シナリオ一覧
            CreateLabel(panel.transform, "■ シナリオ選択", 22f);
            if (scenarios == null || scenarios.Count == 0)
            {
                CreateLabel(panel.transform, "(Resources にシナリオがありません)", 15f);
            }
            else
            {
                foreach (var s in scenarios)
                {
                    if (s == null) continue;
                    string name = s.scenarioName;
                    OptionButton ob = CreateButton(panel.transform, name, () => SelectScenario(name));
                    scenarioOptions.Add(ob);
                }
            }

            // プレイ陣営：FactionData を列挙してボタン生成（索引は ContentDatabase・FND-1 #496）。
            // アセットが無ければ従来の2勢力 enum ボタンにフォールバック。
            CreateLabel(panel.transform, "■ プレイ陣営", 22f);
            System.Collections.Generic.IReadOnlyList<FactionData> factions = ContentDatabase.AllFactions();
            if (factions != null && factions.Count > 0)
            {
                foreach (var fd in factions)
                {
                    if (fd == null) continue;
                    FactionData captured = fd;
                    OptionButton ob = CreateButton(panel.transform, fd.factionName, () => SelectPlayerFactionData(captured));
                    ob.faction = fd;
                    factionOptions.Add(ob);
                }
                EnsureDefaultPlayerFaction(factions);
            }
            else
            {
                // フォールバック：従来の2勢力 enum（index = Faction enum: 0=帝国, 1=同盟）
                factionOptions.Add(CreateButton(panel.transform, "帝国", () => SelectPlayerFaction((int)Faction.帝国)));
                factionOptions.Add(CreateButton(panel.transform, "同盟", () => SelectPlayerFaction((int)Faction.同盟)));
            }

            // 現在の選択表示（大きめ・黄色）
            selectionLabel = CreateLabel(panel.transform, "", 18f);
            selectionLabel.color = new Color(1f, 0.9f, 0.4f);

            // 決定／戻る
            CreateButton(panel.transform, "この設定で会戦開始", BeginBattle);
            CreateButton(panel.transform, "戻る", HideScenarioSelect);

            // 既定選択：現在の scenarioName が一覧にあれば維持、無ければ先頭
            string current = GameSettings.Instance.scenarioName;
            bool found = false;
            if (scenarios != null)
            {
                foreach (var s in scenarios)
                {
                    if (s != null && s.scenarioName == current) { found = true; break; }
                }
                if (!found && scenarios.Count > 0 && scenarios[0] != null)
                    current = scenarios[0].scenarioName;
            }
            if (!string.IsNullOrEmpty(current)) GameSettings.Instance.scenarioName = current;

            RefreshHighlights();
            UpdateSelectionLabel();

            // 内容の高さを測り、枠の高さを内容に合わせる（画面に収まらない分だけスクロール）。
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            float contentHeight = LayoutUtility.GetPreferredHeight(rt);
            const float maxFrameHeight = 940f;   // 1080 リファレンス基準で上下に余白を残す
            frameRT.sizeDelta = new Vector2(460f, Mathf.Min(contentHeight, maxFrameHeight));

            // 初期は非表示（「会戦開始」を押したら表示する）
            if (selectionRoot != null) selectionRoot.SetActive(false);
        }

        /// <summary>シナリオ選択画面を表示する。</summary>
        public void ShowScenarioSelect()
        {
            if (selectionRoot != null) selectionRoot.SetActive(true);
        }

        /// <summary>シナリオ選択画面を閉じてタイトルへ戻る。</summary>
        public void HideScenarioSelect()
        {
            if (selectionRoot != null) selectionRoot.SetActive(false);
        }

        /// <summary>シナリオを選択（GameSettings.scenarioName に反映）。</summary>
        public void SelectScenario(string name)
        {
            GameSettings.Instance.scenarioName = name;
            RefreshHighlights();
            UpdateSelectionLabel();
            Debug.Log($"TitleManager: シナリオ選択 → {name}");
        }

        /// <summary>プレイ陣営を選択（GameSettings.playerFaction に反映）。enum フォールバック用。</summary>
        public void SelectPlayerFaction(int faction)
        {
            GameSettings.Instance.playerFaction = (Faction)faction;
            GameSettings.Instance.playerFactionData = null; // enum 選択時は FactionData をクリア
            RefreshHighlights();
            UpdateSelectionLabel();
            Debug.Log($"TitleManager: プレイ陣営選択 → {(Faction)faction}");
        }

        /// <summary>プレイ陣営を FactionData で選択（多勢力対応。legacyFaction を enum 側へ橋渡し）。</summary>
        public void SelectPlayerFactionData(FactionData fd)
        {
            if (fd == null) return;
            GameSettings.Instance.playerFactionData = fd;
            GameSettings.Instance.playerFaction = fd.legacyFaction; // 既存UI/セーブ/操作判定の橋渡し
            RefreshHighlights();
            UpdateSelectionLabel();
            Debug.Log($"TitleManager: プレイ陣営選択 → {fd.factionName}");
        }

        /// <summary>
        /// FactionData 駆動時の既定プレイ陣営を決める。未設定なら現在の enum playerFaction に
        /// 対応する FactionData、無ければ先頭を選ぶ。
        /// </summary>
        private void EnsureDefaultPlayerFaction(System.Collections.Generic.IReadOnlyList<FactionData> factions)
        {
            if (GameSettings.Instance.playerFactionData != null) return;
            FactionData def = null;
            foreach (var fd in factions)
            {
                if (fd != null && fd.legacyFaction == GameSettings.Instance.playerFaction) { def = fd; break; }
            }
            if (def == null && factions.Count > 0) def = factions[0];
            if (def != null)
            {
                GameSettings.Instance.playerFactionData = def;
                GameSettings.Instance.playerFaction = def.legacyFaction;
            }
        }

        /// <summary>勢力名から FactionData を解決する（見つからなければ null）。索引は ContentDatabase（FND-1 #496）。</summary>
        private FactionData ResolvePlayerFactionData(string name)
        {
            return ContentDatabase.FactionByName(name);
        }

        /// <summary>選択中のボタンを金色＋黒太字＋「●」でハイライトする。</summary>
        private void RefreshHighlights()
        {
            string sel = GameSettings.Instance.scenarioName;
            foreach (var ob in scenarioOptions)
            {
                ApplyStyle(ob, ob.label == sel);
            }

            int f = (int)GameSettings.Instance.playerFaction;
            FactionData selData = GameSettings.Instance.playerFactionData;
            for (int i = 0; i < factionOptions.Count; i++)
            {
                OptionButton ob = factionOptions[i];
                // FactionData ボタンは同一性で、enum フォールバックボタンは index=enum で判定
                bool selected = ob.faction != null ? (ob.faction == selData) : (i == f);
                ApplyStyle(ob, selected);
            }
        }

        private void ApplyStyle(OptionButton ob, bool selected)
        {
            if (ob == null) return;
            if (ob.bg != null) ob.bg.color = selected ? SelectedBg : NormalBg;
            if (ob.text != null)
            {
                ob.text.color = selected ? Color.black : Color.white;
                ob.text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                ob.text.text = selected ? $"● {ob.label}" : ob.label;
            }
        }

        private void UpdateSelectionLabel()
        {
            if (selectionLabel != null)
            {
                GameSettings gs = GameSettings.Instance;
                string factionName = gs.playerFactionData != null ? gs.playerFactionData.factionName : gs.playerFaction.ToString();
                selectionLabel.text = $"― 選択中 ―\n{gs.scenarioName}\n{factionName}軍でプレイ";
            }
        }

        /// <summary>Title シーン所有の専用 Canvas を生成する（永続 Canvas を掴まないため）。</summary>
        private Canvas CreateOwnCanvas()
        {
            GameObject go = new GameObject("ScenarioSelectCanvas");
            selectionRoot = go;
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); // タイトル用Canvasと同じ基準（未設定だと800x600で過剰拡大→見切れ）
            scaler.matchWidthOrHeight = 0.5f;                       // 幅/高さの中間でマッチ（縦長UIの見切れを抑制）
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>会戦遷移時に選択UIを破棄（Battle シーンへ残らないように）。</summary>
        private void DestroySelectionUI()
        {
            if (selectionRoot != null)
            {
                Destroy(selectionRoot);
                selectionRoot = null;
            }
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float size)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            ApplyJaFont(t);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 10f;
            return t;
        }

        private OptionButton CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Button",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            Image bg = go.GetComponent<Image>();
            bg.color = NormalBg;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minHeight = 38f;
            le.preferredHeight = 38f;

            GameObject txt = new GameObject("Text", typeof(RectTransform));
            txt.transform.SetParent(go.transform, false);
            TextMeshProUGUI t = txt.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 20f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            ApplyJaFont(t);

            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return new OptionButton { button = go.GetComponent<Button>(), bg = bg, text = t, label = label };
        }

        // ===================================================================
        //  メインタイトル画面（背景・タイトルロゴ・メニュー）を実行時生成
        // ===================================================================

        /// <summary>日本語 TMP フォントを確実にロードする（呼び出し順に依存しないための堅牢化）。</summary>
        private void EnsureJaFont()
        {
            if (jaFont == null) jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont == null)
                Debug.LogWarning("TitleManager: JapaneseFont_TMP が Resources に見つかりません。日本語が豆腐(□)になる可能性があります。");
        }

        /// <summary>TMP に日本語フォントを適用する（jaFont が無ければ TMP 既定にフォールバック）。</summary>
        private void ApplyJaFont(TextMeshProUGUI t)
        {
            if (t == null) return;
            EnsureJaFont();
            if (jaFont != null) t.font = jaFont;
        }

        /// <summary>銀河背景＋メタリックなタイトル文字＋発光メニューを生成する。</summary>
        private void BuildTitleScreen()
        {
            EnsureJaFont();        // 順序非依存：タイトル生成前に必ず日本語フォントを確保
            EnsureEventSystem();

            GameObject go = new GameObject("TitleScreenCanvas");
            titleScreenRoot = go;
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // シーン手置きUIより手前・シナリオ選択モーダル(50)より後ろ
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();

            // 背景（銀河画像。無ければ濃紺で代替）
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvas.transform, false);
            StretchFull(bg.GetComponent<RectTransform>());
            Image bgImg = bg.GetComponent<Image>();
            Sprite galaxy = LoadBackgroundSprite();
            if (galaxy != null)
            {
                bgImg.sprite = galaxy;
                bgImg.color = Color.white;
                bgImg.preserveAspect = false; // 全画面に伸ばす
            }
            else
            {
                bgImg.color = new Color(0.02f, 0.03f, 0.08f, 1f);
                Debug.LogWarning($"TitleManager: 背景画像 '{TitleBgResource}' が Resources に見つかりません。" +
                    "Assets/Resources/Textures/TitleBackground.(png/jpg) に配置してください。濃紺の代替背景で続行します。");
            }

            // 視認性確保の暗幕
            GameObject veil = new GameObject("Veil", typeof(RectTransform), typeof(Image));
            veil.transform.SetParent(canvas.transform, false);
            StretchFull(veil.GetComponent<RectTransform>());
            veil.GetComponent<Image>().color = new Color(0f, 0f, 0.02f, 0.35f);

            BuildTitleText(canvas.transform);
            BuildMainMenu(canvas.transform);
        }

        /// <summary>RectTransform を親いっぱい（全画面ストレッチ）にする。</summary>
        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>背景用の銀河スプライトを読み込む（Sprite 未設定でも Texture から実行時 Sprite 化）。</summary>
        private Sprite LoadBackgroundSprite()
        {
            Sprite s = Resources.Load<Sprite>(TitleBgResource);
            if (s != null) return s;
            Texture2D tex = Resources.Load<Texture2D>(TitleBgResource);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return null;
        }

        /// <summary>メタリックなタイトルロゴ文字とサブタイトルを生成する。</summary>
        private void BuildTitleText(Transform parent)
        {
            GameObject go = new GameObject("TitleText", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -120f);
            rt.sizeDelta = new Vector2(1700f, 240f);

            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = "銀河英雄伝説";
            t.fontSize = 150f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            ApplyJaFont(t);

            // シルバー→スチールブルーの縦グラデーション
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(
                new Color(0.97f, 0.98f, 1f),   // 上：明るい銀
                new Color(0.97f, 0.98f, 1f),
                new Color(0.45f, 0.62f, 0.92f),// 下：青
                new Color(0.45f, 0.62f, 0.92f));

            TryApplyOutline(t, new Color(0.04f, 0.10f, 0.28f, 1f), 0.18f);

            // サブタイトル
            GameObject sub = new GameObject("SubTitle", typeof(RectTransform));
            sub.transform.SetParent(parent, false);
            RectTransform srt = sub.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 1f);
            srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -310f);
            srt.sizeDelta = new Vector2(1200f, 50f);
            TextMeshProUGUI st = sub.AddComponent<TextMeshProUGUI>();
            st.text = "― 戦術艦隊戦 ―";
            st.fontSize = 34f;
            st.alignment = TextAlignmentOptions.Center;
            st.color = new Color(0.82f, 0.9f, 1f, 0.9f);
            ApplyJaFont(st);
        }

        /// <summary>TMP にアウトライン（縁取り）を適用する。fontMaterial はこのテキスト専用にインスタンス化される。</summary>
        private static void TryApplyOutline(TextMeshProUGUI t, Color color, float width)
        {
            Material m = t.fontMaterial; // 取得でインスタンス化（タイトル1枚なので許容）
            if (m == null) return;
            m.SetColor(ShaderUtilities.ID_OutlineColor, color);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        }

        /// <summary>メインメニュー（縦並びの発光ボタン）を生成する。</summary>
        private void BuildMainMenu(Transform parent)
        {
            GameObject menu = new GameObject("MainMenu", typeof(RectTransform), typeof(VerticalLayoutGroup));
            menu.transform.SetParent(parent, false);
            RectTransform rt = menu.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -110f);
            rt.sizeDelta = new Vector2(560f, 10f);

            VerticalLayoutGroup vlg = menu.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            CreateMenuButton(menu.transform, "会戦開始", StartBattle);
            continueMenuButton = CreateMenuButton(menu.transform, "続きから", ContinueBattle);
            if (continueMenuButton != null) continueMenuButton.interactable = SaveManager.HasSave();
            CreateMenuButton(menu.transform, "設定", OpenSettings);
            CreateMenuButton(menu.transform, "ゲームを終了する", QuitGame);
        }

        /// <summary>発光する半透明ブルーのメニューボタンを生成する（ホバーで明るく光る）。</summary>
        private Button CreateMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("MenuButton",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            Image bg = go.GetComponent<Image>();
            bg.sprite = GetRoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.12f, 0.32f, 0.62f, 0.55f);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minHeight = 64f; le.preferredHeight = 64f;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;                          // bg.color に乗算
            cb.highlightedColor = new Color(1.6f, 1.9f, 2.4f, 1f); // ホバーで明るく発光
            cb.pressedColor = new Color(0.85f, 0.95f, 1.1f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.4f, 0.4f, 0.45f, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.12f;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject txt = new GameObject("Text", typeof(RectTransform));
            txt.transform.SetParent(go.transform, false);
            StretchFull(txt.GetComponent<RectTransform>());
            TextMeshProUGUI t = txt.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 30f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.92f, 0.97f, 1f);
            t.raycastTarget = false;
            ApplyJaFont(t);

            return btn;
        }

        /// <summary>角丸ボタン用スプライト（9スライス）をアプリ寿命で1枚だけ生成する。</summary>
        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            const int size = 48, r = 20;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, r, size - r);
                    float cy = Mathf.Clamp(fy, r, size - r);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f); // 角だけ円弧でアルファを落とす（AA付き）
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return roundedSprite;
        }

        /// <summary>
        /// 「会戦開始」ボタン：シナリオ／プレイ陣営の選択画面を開く（ここではまだ会戦に入らない）。
        /// </summary>
        public void StartBattle()
        {
            Debug.Log("TitleManager: StartBattle（シナリオ選択を開く）");
            ShowScenarioSelect();
        }

        /// <summary>
        /// シナリオ選択画面の「この設定で会戦開始」：現在の選択で会戦シーンへ遷移する。
        /// </summary>
        public void BeginBattle()
        {
            Debug.Log("TitleManager: BeginBattle（会戦へ遷移）");

            // 現在の設定を保存
            SaveCurrentSetup();
            GameSettings.Instance.ResetStats();

            // 選択UIを破棄（Battle シーンに残さない）
            DestroySelectionUI();

            SceneLoader.Instance.LoadScene("Battle");
        }

        /// <summary>
        /// 直近のセットアップを復元して会戦を開始します。
        /// </summary>
        public void ContinueBattle()
        {
            SaveData data = SaveManager.Load();
            if (data != null)
            {
                Debug.Log("TitleManager: Restoring previous setup...");

                // 選択UIを破棄（Battle シーンに残さない）
                DestroySelectionUI();

                GameSettings settings = GameSettings.Instance;
                // 勢力を復元：保存した FactionData 名を Resources/Factions から解決。
                // 見つかればそれを採用し legacyFaction を enum 側へ。無ければ enum にフォールバック。
                FactionData fd = ResolvePlayerFactionData(data.playerFactionName);
                settings.playerFactionData = fd;
                settings.playerFaction = (fd != null) ? fd.legacyFaction : (Faction)data.playerFaction;
                settings.scenarioName = data.scenarioName;
                settings.selectedAdmiral = data.selectedAdmiral;

                settings.ResetStats();
                SceneLoader.Instance.LoadScene("Battle");
            }
            else
            {
                Debug.LogWarning("TitleManager: No save data found to continue.");
            }
        }

        private void SaveCurrentSetup()
        {
            GameSettings settings = GameSettings.Instance;
            SaveData data = new SaveData
            {
                playerFaction = (int)settings.playerFaction,
                // FactionData で選んでいればその名前も保存（続きからで勢力を正確に復元）
                playerFactionName = settings.playerFactionData != null ? settings.playerFactionData.factionName : "",
                scenarioName = settings.scenarioName,
                selectedAdmiral = settings.selectedAdmiral
            };
            SaveManager.Save(data);
        }

        /// <summary>
        /// 設定画面を開く（フルスクリーンの専用画面に遷移）。
        /// </summary>
        public void OpenSettings()
        {
            // 旧式の手置き設定パネルは使わない（しょぼいので隠す）
            if (settingsPanel != null) settingsPanel.SetActive(false);

            if (settingsScreenRoot == null) BuildSettingsScreen();
            if (settingsScreenRoot != null)
            {
                RefreshSettingsUI();
                settingsScreenRoot.SetActive(true);
            }
        }

        /// <summary>設定画面を閉じてタイトルへ戻る（変更を永続化）。</summary>
        public void CloseSettings()
        {
            GameSettings.Instance.SavePrefs();
            if (settingsScreenRoot != null) settingsScreenRoot.SetActive(false);
        }

        public void SetVolume(float volume)
        {
            GameSettings.Instance.masterVolume = volume;
            AudioListener.volume = volume;
        }

        // ===================================================================
        //  設定画面（フルスクリーン）を実行時生成
        // ===================================================================

        /// <summary>銀河背景＋スライダー/ボタンで各種設定を行うフルスクリーン設定画面を生成する。</summary>
        private void BuildSettingsScreen()
        {
            EnsureJaFont();
            EnsureEventSystem();

            GameObject go = new GameObject("SettingsScreenCanvas");
            settingsScreenRoot = go;
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70; // タイトル(10)・シナリオ選択(50)より手前
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();

            // 背景（銀河画像）＋暗幕でタイトルを覆い「別画面」に見せる
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvas.transform, false);
            StretchFull(bg.GetComponent<RectTransform>());
            Image bgImg = bg.GetComponent<Image>();
            Sprite galaxy = LoadBackgroundSprite();
            if (galaxy != null)
            {
                bgImg.sprite = galaxy;
                bgImg.color = Color.white;
                bgImg.preserveAspect = false;
            }
            else
            {
                bgImg.color = new Color(0.02f, 0.03f, 0.08f, 1f);
            }
            GameObject veil = new GameObject("Veil", typeof(RectTransform), typeof(Image));
            veil.transform.SetParent(canvas.transform, false);
            StretchFull(veil.GetComponent<RectTransform>());
            veil.GetComponent<Image>().color = new Color(0f, 0f, 0.02f, 0.78f);

            // 見出し「設定」
            GameObject head = new GameObject("Heading", typeof(RectTransform));
            head.transform.SetParent(canvas.transform, false);
            RectTransform hrt = head.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 1f);
            hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -90f);
            hrt.sizeDelta = new Vector2(800f, 120f);
            TextMeshProUGUI ht = head.AddComponent<TextMeshProUGUI>();
            ht.text = "設定";
            ht.fontSize = 80f;
            ht.fontStyle = FontStyles.Bold;
            ht.alignment = TextAlignmentOptions.Center;
            ht.enableVertexGradient = true;
            ht.colorGradient = new VertexGradient(
                new Color(0.97f, 0.98f, 1f), new Color(0.97f, 0.98f, 1f),
                new Color(0.45f, 0.62f, 0.92f), new Color(0.45f, 0.62f, 0.92f));
            ApplyJaFont(ht);

            // 中央のパネル
            GameObject panel = new GameObject("SettingsPanel",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(canvas.transform, false);
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -30f);
            rt.sizeDelta = new Vector2(720f, 100f);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.92f);

            VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 36, 28, 28);
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = panel.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameSettings gs = GameSettings.Instance;

            // マスター音量（0〜100%）
            CreateSliderRow(panel.transform, "マスター音量", 0f, 1f, gs.masterVolume, false,
                v => $"{Mathf.RoundToInt(v * 100f)}%",
                v => { GameSettings.Instance.masterVolume = v; AudioListener.volume = v; },
                out volumeValueLabel);

            // 開始ズーム（戦場の見え方。大きいほど引いた画）
            CreateSliderRow(panel.transform, "開始ズーム", ZoomMin, ZoomMax, gs.cameraStartZoom, true,
                v => $"{Mathf.RoundToInt(v)}",
                v => { GameSettings.Instance.cameraStartZoom = v; },
                out zoomValueLabel);

            // ゲーム速度（既定の倍速）
            CreateLabel(panel.transform, "ゲーム速度", 22f);
            GameObject speedRow = CreateButtonRow(panel.transform);
            speedOptions.Clear();
            foreach (float sp in SpeedChoices)
            {
                float captured = sp;
                OptionButton ob = CreateButton(speedRow.transform, $"{sp:0}x", () => SetDefaultSpeed(captured));
                ob.label = $"{sp:0}x";
                speedOptions.Add(ob);
            }

            // 射界（ギズモ）常時表示
            gizmoOption = CreateButton(panel.transform, "射界表示", ToggleGizmos);

            // 画面端スクロール（#87）
            edgeScrollOption = CreateButton(panel.transform, "画面端スクロール", ToggleEdgeScroll);

            // 戻る
            CreateButton(panel.transform, "戻る", CloseSettings);
        }

        /// <summary>横並びのボタン置き場（ゲーム速度ボタン用）を作る。</summary>
        private GameObject CreateButtonRow(Transform parent)
        {
            GameObject row = new GameObject("Row",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            row.GetComponent<LayoutElement>().minHeight = 40f;
            return row;
        }

        /// <summary>ラベル＋スライダー＋数値表示の1行を作る。値変更で onChange と数値表示を更新。</summary>
        private void CreateSliderRow(Transform parent, string label, float min, float max, float value,
            bool wholeNumbers, System.Func<float, string> fmt, System.Action<float> onChange,
            out TextMeshProUGUI valueLabel)
        {
            GameObject row = new GameObject("SliderRow_" + label,
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            row.GetComponent<LayoutElement>().minHeight = 44f;

            // 左ラベル
            GameObject lab = new GameObject("Label", typeof(RectTransform));
            lab.transform.SetParent(row.transform, false);
            TextMeshProUGUI lt = lab.AddComponent<TextMeshProUGUI>();
            lt.text = label;
            lt.fontSize = 22f;
            lt.alignment = TextAlignmentOptions.Left;
            lt.color = Color.white;
            ApplyJaFont(lt);
            LayoutElement labLe = lab.AddComponent<LayoutElement>();
            labLe.preferredWidth = 200f;
            labLe.minWidth = 200f;

            // スライダー本体
            Slider slider = CreateSlider(row.transform, min, max, value, wholeNumbers);
            LayoutElement slLe = slider.gameObject.AddComponent<LayoutElement>();
            slLe.flexibleWidth = 1f;
            slLe.minWidth = 320f;
            slLe.minHeight = 24f;

            // 右の数値表示
            GameObject val = new GameObject("Value", typeof(RectTransform));
            val.transform.SetParent(row.transform, false);
            TextMeshProUGUI vt = val.AddComponent<TextMeshProUGUI>();
            vt.text = fmt(value);
            vt.fontSize = 22f;
            vt.alignment = TextAlignmentOptions.Right;
            vt.color = new Color(1f, 0.9f, 0.4f);
            ApplyJaFont(vt);
            LayoutElement valLe = val.AddComponent<LayoutElement>();
            valLe.preferredWidth = 80f;
            valLe.minWidth = 80f;
            valueLabel = vt;

            TextMeshProUGUI capturedLabel = vt;
            slider.onValueChanged.AddListener(v =>
            {
                onChange(v);
                if (capturedLabel != null) capturedLabel.text = fmt(v);
            });
        }

        /// <summary>uGUI Slider をコードで一式生成する（Unity 既定のスライダー階層を再現）。</summary>
        private Slider CreateSlider(Transform parent, float min, float max, float value, bool wholeNumbers)
        {
            GameObject root = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            Slider slider = root.GetComponent<Slider>();

            // 背景トラック
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.25f);
            bgRT.anchorMax = new Vector2(1f, 0.75f);
            bgRT.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.3f, 1f);

            // フィル領域
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0f, 0.25f);
            faRT.anchorMax = new Vector2(1f, 0.75f);
            faRT.anchoredPosition = new Vector2(-5f, 0f);
            faRT.sizeDelta = new Vector2(-20f, 0f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            fillRT.sizeDelta = new Vector2(10f, 0f);
            fill.GetComponent<Image>().color = new Color(0.3f, 0.6f, 1f, 1f);

            // ハンドル領域
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            RectTransform haRT = handleArea.GetComponent<RectTransform>();
            haRT.anchorMin = new Vector2(0f, 0f);
            haRT.anchorMax = new Vector2(1f, 1f);
            haRT.sizeDelta = new Vector2(-20f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRT = handle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(22f, 0f);
            handle.GetComponent<Image>().color = new Color(0.95f, 0.97f, 1f, 1f);

            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            return slider;
        }

        /// <summary>ゲーム速度（既定倍速）を設定し、ボタンのハイライトを更新する。</summary>
        private void SetDefaultSpeed(float speed)
        {
            GameSettings.Instance.defaultTimeScale = speed;
            RefreshSettingsUI();
        }

        /// <summary>射界（ギズモ）常時表示をトグルする。</summary>
        private void ToggleGizmos()
        {
            GameSettings.Instance.alwaysShowGizmos = !GameSettings.Instance.alwaysShowGizmos;
            RefreshSettingsUI();
        }

        /// <summary>画面端スクロール（#87）の有効/無効をトグルする。</summary>
        private void ToggleEdgeScroll()
        {
            GameSettings.Instance.edgeScrollEnabled = !GameSettings.Instance.edgeScrollEnabled;
            RefreshSettingsUI();
        }

        /// <summary>設定画面の各表示（数値・ボタンのハイライト）を現在値に同期する。</summary>
        private void RefreshSettingsUI()
        {
            GameSettings gs = GameSettings.Instance;

            if (volumeValueLabel != null) volumeValueLabel.text = $"{Mathf.RoundToInt(gs.masterVolume * 100f)}%";
            if (zoomValueLabel != null) zoomValueLabel.text = $"{Mathf.RoundToInt(gs.cameraStartZoom)}";

            for (int i = 0; i < speedOptions.Count; i++)
            {
                bool sel = Mathf.Approximately(SpeedChoices[i], gs.defaultTimeScale);
                ApplyStyle(speedOptions[i], sel);
            }

            if (gizmoOption != null)
            {
                bool on = gs.alwaysShowGizmos;
                if (gizmoOption.bg != null) gizmoOption.bg.color = on ? SelectedBg : NormalBg;
                if (gizmoOption.text != null)
                {
                    gizmoOption.text.color = on ? Color.black : Color.white;
                    gizmoOption.text.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                    gizmoOption.text.text = on ? "射界表示： ON" : "射界表示： OFF";
                }
            }

            if (edgeScrollOption != null)
            {
                bool on = gs.edgeScrollEnabled;
                if (edgeScrollOption.bg != null) edgeScrollOption.bg.color = on ? SelectedBg : NormalBg;
                if (edgeScrollOption.text != null)
                {
                    edgeScrollOption.text.color = on ? Color.black : Color.white;
                    edgeScrollOption.text.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                    edgeScrollOption.text.text = on ? "画面端スクロール： ON" : "画面端スクロール： OFF";
                }
            }
        }

        /// <summary>
        /// ゲームを終了します。
/// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
