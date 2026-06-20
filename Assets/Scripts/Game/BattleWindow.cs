using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 会戦をウィンドウ化して表示する（WIN-1 #2568）。戦略マップを背後に残したまま Battle シーンを
    /// additive ロード（独立 2D 物理）し、その専用カメラを <see cref="RenderTexture"/> に描いて RawImage 窓へ映す。
    /// タイトルバーでドラッグ移動・× で閉じ、決着/離脱（BattleManager → <see cref="NotifyBattleEnded"/>）でも閉じてシーンをアンロードする。
    /// 入力は <see cref="BattleViewport"/> が画面→会戦ワールドへ変換する。既存のフルスクリーン会戦は
    /// <c>GameSettings.windowedBattles=false</c> で従来どおり（後方互換）。
    /// 既知の第1版の制限（実機で順次対応）：HUD/コマンドメニュー/通知は全画面オーバーレイ／会戦のマウスズーム・
    /// 矩形選択は窓座標へ未対応（キーパン・直クリック選択・命令は可）／会戦の一時停止は全体時間に効く。
    /// </summary>
    public class BattleWindow : MonoBehaviour
    {
        private static BattleWindow instance;

        /// <summary>会戦ウィンドウが開いているか。</summary>
        public static bool IsOpen => instance != null && instance.isOpen;

        /// <summary>カーソルが会戦ウィンドウ（枠全体）の上にあるか。GalaxyView がこの間はマップ操作を譲る。</summary>
        public static bool PointerOverWindow
        {
            get
            {
                if (instance == null || !instance.isOpen || instance.windowRT == null || Mouse.current == null) return false;
                return RectTransformUtility.RectangleContainsScreenPoint(instance.windowRT, Mouse.current.position.ReadValue(), null);
            }
        }

        [Header("ウィンドウ")]
        public Vector2 windowSize = new Vector2(1040f, 660f);
        public int rtWidth = 1040;
        public int rtHeight = 630;

        private const float TitleBarHeight = 30f;

        private bool isOpen;
        private GameObject root;
        private RectTransform windowRT;
        private RawImage mapImage;
        private RectTransform mapRT;
        private RenderTexture rt;
        private Camera battleCam;
        private Scene battleScene;
        private bool sceneLoaded;
        private object escWindowToken;
        private TextMeshProUGUI titleCap;

        /// <summary>会戦ウィンドウを開く（必要なら生成）。BattleHandoff は呼び出し側が事前に Queue 済みであること。</summary>
        public static void Open()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("BattleWindow");
                instance = go.AddComponent<BattleWindow>();
                instance.Build();
            }
            instance.OpenInternal();
        }

        /// <summary>会戦が決着/離脱したら（BattleManager から）窓を閉じてシーンをアンロードする。</summary>
        public static void NotifyBattleEnded()
        {
            if (instance != null) instance.Close();
        }

        // ===== UI 構築 =====

        private void Build()
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject("BattleWindowCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 940; // 通知(880)/星系図(950)近辺・観測窓(1090)より後ろ
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            GameObject win = new GameObject("Window", typeof(RectTransform));
            win.transform.SetParent(root.transform, false);
            windowRT = win.GetComponent<RectTransform>();
            windowRT.anchorMin = windowRT.anchorMax = windowRT.pivot = new Vector2(0.5f, 0.5f);
            windowRT.sizeDelta = windowSize;
            windowRT.anchoredPosition = Vector2.zero;
            Image winImg = win.AddComponent<Image>();
            winImg.color = new Color(0.03f, 0.04f, 0.07f, 0.98f);
            Outline border = win.AddComponent<Outline>();
            border.effectColor = new Color(1f, 0.84f, 0.36f, 0.5f);
            border.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup vlg = win.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0f;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            BuildTitleBar(win.transform);

            // マップ領域（RawImage＝RenderTexture を映す。raycastTarget=false＝会戦クリックは FleetCommander が直接処理）
            GameObject mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(win.transform, false);
            mapRT = mapGo.GetComponent<RectTransform>();
            LayoutElement le = mapGo.AddComponent<LayoutElement>();
            le.preferredHeight = windowSize.y - TitleBarHeight;
            mapImage = mapGo.AddComponent<RawImage>();
            mapImage.color = Color.white;
            mapImage.raycastTarget = false;

            root.SetActive(false);
            escWindowToken = UIWindowStack.Register(() => isOpen, Close, 940, "会戦");
        }

        private void BuildTitleBar(Transform parent)
        {
            GameObject bar = new GameObject("TitleBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            Image img = bar.AddComponent<Image>();
            img.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            LayoutElement le = bar.AddComponent<LayoutElement>();
            le.minHeight = TitleBarHeight; le.preferredHeight = TitleBarHeight;
            UIDragMove drag = bar.AddComponent<UIDragMove>();
            drag.target = windowRT;

            titleCap = CreateText(bar.transform, "≡ 会戦　（ドラッグで移動／× で離脱）", 15f, new Color(1f, 0.84f, 0.36f), TextAlignmentOptions.Left);
            RectTransform crt = titleCap.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(12f, 0f); crt.offsetMax = new Vector2(-42f, 0f);

            GameObject cb = new GameObject("Close", typeof(RectTransform));
            cb.transform.SetParent(bar.transform, false);
            RectTransform cbrt = cb.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1f, 0f); cbrt.anchorMax = new Vector2(1f, 1f);
            cbrt.pivot = new Vector2(1f, 0.5f); cbrt.sizeDelta = new Vector2(34f, 0f);
            cbrt.anchoredPosition = new Vector2(-3f, 0f);
            Image cimg = cb.AddComponent<Image>();
            cimg.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            Button cbtn = cb.AddComponent<Button>();
            cbtn.transition = UnityEngine.UI.Selectable.Transition.None;
            cbtn.onClick.AddListener(OnCloseButton);
            TextMeshProUGUI glyph = CreateText(cb.transform, "×", 18f, Color.white, TextAlignmentOptions.Center);
            StretchFull(glyph.rectTransform);
        }

        // ===== 開閉 =====

        private void OpenInternal()
        {
            if (isOpen) return; // 1会戦のみ（複数同時は WIN-3）
            Cleanup();

            rt = new RenderTexture(rtWidth, rtHeight, 16);
            rt.Create();
            if (mapImage != null) mapImage.texture = rt;

            isOpen = true;
            if (root != null) root.SetActive(true);

            // 戦場を遠方オフセットへ置く（戦略マップと同一ワールド空間のため、会戦カメラに戦略が映り込むのを防ぐ）。
            // BattleSetup.Awake が additive ロード中にこの値を読んで自シーンへ確定登録する＝ロード前に設定する。
            BattleField.PendingOrigin = new Vector2(0f, 100000f);

            // Battle を additive ロード（独立 2D 物理＝会戦間/戦略との干渉防止）。完了で会戦カメラを RT に束ねる。
            sceneLoaded = false;
            SceneLoader.Instance.LoadSceneAdditive("Battle", true, OnBattleLoaded);
        }

        private void OnBattleLoaded(Scene scene)
        {
            battleScene = scene;
            sceneLoaded = scene.IsValid() && scene.isLoaded;
            battleCam = FindBattleCamera(scene);
            if (battleCam != null)
            {
                battleCam.targetTexture = rt;      // 画面でなくウィンドウ（RT）へ描く
                BattleViewport.SetActive(scene, battleCam, mapRT);
            }
            else
            {
                Debug.LogWarning("BattleWindow: 会戦カメラが見つかりませんでした（additive ロード後）。");
            }
        }

        private static Camera FindBattleCamera(Scene scene)
        {
            if (!scene.IsValid()) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Camera c = roots[i].GetComponentInChildren<Camera>(true);
                if (c != null) return c;
            }
            return null;
        }

        private void OnCloseButton()
        {
            // × は「戦略へ離脱」＝現状の優勢側を勝者として書き戻すため、BattleManager の復帰経路を使う。
            BattleManager bm = sceneLoaded ? FindBattleManager() : null;
            if (bm != null) bm.LeaveToStrategy();
            else Close(); // 見つからなければ単に閉じる
        }

        private BattleManager FindBattleManager()
        {
            if (!battleScene.IsValid()) return null;
            GameObject[] roots = battleScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                BattleManager bm = roots[i].GetComponentInChildren<BattleManager>(true);
                if (bm != null) return bm;
            }
            return null;
        }

        /// <summary>窓を閉じて会戦シーンをアンロードし、描画資源を解放する。</summary>
        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            BattleViewport.Clear();
            if (battleCam != null) { battleCam.targetTexture = null; battleCam = null; }
            if (sceneLoaded && battleScene.IsValid())
                SceneLoader.Instance.UnloadSceneAdditive(battleScene);
            sceneLoaded = false;

            Cleanup();
            if (root != null) root.SetActive(false);

            if (battleScene.IsValid()) BattleField.ClearScene(battleScene); // 戦場中心の登録を解除
            Time.timeScale = 1f;                       // 念のため通常速度へ
            GameInput.SetContext(InputContext.戦略);    // 会戦が会戦コンテキストにしているため戦略へ戻す
        }

        private void Cleanup()
        {
            if (rt != null)
            {
                if (mapImage != null) mapImage.texture = null;
                rt.Release();
                Destroy(rt);
                rt = null;
            }
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            Cleanup();
            if (instance == this) instance = null;
        }

        // ===== ヘルパ =====

        private static TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) t.font = ja;
            return t;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
