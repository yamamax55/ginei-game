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
        private ScenarioData[] scenarios;
        private TextMeshProUGUI selectionLabel;
        private TMP_FontAsset jaFont;
        private GameObject selectionRoot;   // 自前生成した Canvas（遷移時に破棄）
        private readonly System.Collections.Generic.List<OptionButton> scenarioOptions = new System.Collections.Generic.List<OptionButton>();
        private readonly System.Collections.Generic.List<OptionButton> factionOptions = new System.Collections.Generic.List<OptionButton>();

        // ハイライト色
        private static readonly Color SelectedBg = new Color(0.95f, 0.75f, 0.15f, 1f);   // 金
        private static readonly Color NormalBg = new Color(0.15f, 0.2f, 0.35f, 0.95f);   // 濃紺

        /// <summary>選択UIの1ボタン（ハイライト切替用に参照を保持）。</summary>
        private class OptionButton
        {
            public Button button;
            public Image bg;
            public TextMeshProUGUI text;
            public string label;
        }

        private void Start()
        {
            // セーブデータがある場合のみ「続きから」ボタンを有効化
            if (continueButton != null)
            {
                continueButton.interactable = SaveManager.HasSave();
            }

            if (settingsPanel != null) settingsPanel.SetActive(false);

            BuildSelectionUI();

            AudioManager.Instance.PlayBGM(AudioManager.Instance.bgmTitle);
        }

        // ----- シナリオ／陣営 選択UI（実行時生成） -----

        private void BuildSelectionUI()
        {
            jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            scenarios = Resources.LoadAll<ScenarioData>("");
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

            // 画面中央に縦並びのパネルを生成（dim の後＝手前に描画）
            GameObject panel = new GameObject("ScenarioSelectPanel",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(canvas.transform, false);

            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(380f, 100f);

            panel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.97f);

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

            // シナリオ一覧
            CreateLabel(panel.transform, "■ シナリオ選択", 22f);
            if (scenarios == null || scenarios.Length == 0)
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

            // プレイ陣営（index = Faction enum: 0=帝国, 1=同盟）
            CreateLabel(panel.transform, "■ プレイ陣営", 22f);
            factionOptions.Add(CreateButton(panel.transform, "帝国", () => SelectPlayerFaction((int)Faction.帝国)));
            factionOptions.Add(CreateButton(panel.transform, "同盟", () => SelectPlayerFaction((int)Faction.同盟)));

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
                if (!found && scenarios.Length > 0 && scenarios[0] != null)
                    current = scenarios[0].scenarioName;
            }
            if (!string.IsNullOrEmpty(current)) GameSettings.Instance.scenarioName = current;

            RefreshHighlights();
            UpdateSelectionLabel();

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

        /// <summary>プレイ陣営を選択（GameSettings.playerFaction に反映）。</summary>
        public void SelectPlayerFaction(int faction)
        {
            GameSettings.Instance.playerFaction = (Faction)faction;
            RefreshHighlights();
            UpdateSelectionLabel();
            Debug.Log($"TitleManager: プレイ陣営選択 → {(Faction)faction}");
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
            for (int i = 0; i < factionOptions.Count; i++)
            {
                ApplyStyle(factionOptions[i], i == f);
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
                selectionLabel.text = $"― 選択中 ―\n{gs.scenarioName}\n{gs.playerFaction}軍でプレイ";
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
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
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
            if (jaFont != null) t.font = jaFont;
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
            if (jaFont != null) t.font = jaFont;

            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return new OptionButton { button = go.GetComponent<Button>(), bg = bg, text = t, label = label };
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
                settings.playerFaction = (Faction)data.playerFaction;
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
                scenarioName = settings.scenarioName,
                selectedAdmiral = settings.selectedAdmiral
            };
            SaveManager.Save(data);
        }

        /// <summary>
        /// 設定画面を開く。
        /// </summary>
        public void OpenSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        public void SetVolume(float volume)
        {
            GameSettings.Instance.masterVolume = volume;
            AudioListener.volume = volume;
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
