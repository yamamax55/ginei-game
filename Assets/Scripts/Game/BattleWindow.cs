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
    /// 1会戦ぶんのウィンドウ（WIN-1 #2568 / WIN-3 #2570 で複数同時対応）。戦略マップを背後に残したまま Battle シーンを
    /// additive ロード（独立 2D 物理）し、その専用カメラを <see cref="RenderTexture"/> に描いて RawImage 窓へ映す。
    /// タイトルバーでドラッグ移動・× で離脱（BattleManager 経由）・決着でも閉じてシーンをアンロードする。
    /// 生成・直列ロード・オフセット割当・フォーカス・結果反映は <see cref="BattleDirector"/> が司る。
    /// 入力は <see cref="BattleViewport"/>（フォーカス窓のみ）が画面→会戦ワールドへ変換する。
    /// 既存のフルスクリーン会戦は <c>GameSettings.windowedBattles=false</c> で従来どおり（後方互換）。
    /// 既知の制限：HUD/コマンドメニュー/通知は全画面オーバーレイ／一時停止は全体時間に効く（WIN-4 で対応）。
    /// </summary>
    public class BattleWindow : MonoBehaviour
    {
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
        private System.Action<BattleWindow> onLoaded;
        private System.Action<BattleWindow> onClosed;
        private string title = "会戦";

        /// <summary>この窓が開いているか。</summary>
        public bool IsOpen => isOpen;
        /// <summary>この窓の会戦シーン。</summary>
        public Scene BattleScene => battleScene;
        /// <summary>この窓の会戦カメラ。</summary>
        public Camera Cam => battleCam;
        /// <summary>この窓のマップ領域 RectTransform（入力変換用）。</summary>
        public RectTransform MapRect => mapRT;
        /// <summary>シーンのロードと描画束ねが完了したか。</summary>
        public bool Ready => sceneLoaded && battleCam != null;

        /// <summary>カーソルがこの窓（枠全体）の上にあるか。</summary>
        public bool ContainsPointer
        {
            get
            {
                if (!isOpen || windowRT == null || Mouse.current == null) return false;
                return RectTransformUtility.RectangleContainsScreenPoint(windowRT, Mouse.current.position.ReadValue(), null);
            }
        }

        // ===== 公開ライフサイクル（BattleDirector が呼ぶ）=====

        /// <summary>
        /// 会戦シーンを additive ロードして窓に表示する。<paramref name="worldOffset"/> は戦場の遠方オフセット
        /// （会戦ごとに固有）。<paramref name="anchoredPos"/> は窓の初期位置。完了で <paramref name="loadedCb"/>。
        /// 呼び出し前に BattleDirector が BattleHandoff を当該会戦のスナップショットへ復元済みであること。
        /// </summary>
        public void BeginOpen(Vector2 worldOffset, Vector2 anchoredPos, string windowTitle,
            System.Action<BattleWindow> loadedCb, System.Action<BattleWindow> closedCb)
        {
            onLoaded = loadedCb;
            onClosed = closedCb;
            if (!string.IsNullOrEmpty(windowTitle)) title = windowTitle;
            EnsureEventSystem();
            Build(anchoredPos);

            rt = new RenderTexture(rtWidth, rtHeight, 16);
            rt.Create();
            if (mapImage != null) mapImage.texture = rt;

            isOpen = true;
            if (root != null) root.SetActive(true);

            // 戦場を会戦ごとの遠方オフセットへ置く（戦略・他会戦と同一ワールド空間でも映り込まないよう隔離）。
            // BattleSetup.Awake が additive ロード中にこの値を読んで自シーンへ確定登録する＝ロード前に設定する。
            BattleField.PendingOrigin = worldOffset;

            sceneLoaded = false;
            SceneLoader.Instance.LoadSceneAdditive("Battle", true, OnBattleLoaded);
        }

        /// <summary>窓を閉じて会戦シーンをアンロードし、描画資源を解放する（BattleDirector が呼ぶ）。</summary>
        public void CloseWindow()
        {
            if (!isOpen) return;
            isOpen = false;

            if (battleCam != null) { battleCam.targetTexture = null; battleCam = null; }
            if (sceneLoaded && battleScene.IsValid())
                SceneLoader.Instance.UnloadSceneAdditive(battleScene);
            if (battleScene.IsValid()) BattleField.ClearScene(battleScene);
            sceneLoaded = false;

            Cleanup();
            UIWindowStack.Unregister(escWindowToken);
            escWindowToken = null;
            onClosed?.Invoke(this);
        }

        // ===== ロード完了 =====

        private void OnBattleLoaded(Scene scene)
        {
            battleScene = scene;
            sceneLoaded = scene.IsValid() && scene.isLoaded;
            battleCam = FindBattleCamera(scene);
            if (battleCam != null)
            {
                battleCam.targetTexture = rt; // 画面でなくウィンドウ（RT）へ描く
                // 会戦カメラの AudioListener は無効化（戦略シーンの1つだけに保つ＝「2 audio listeners」警告/競合回避）。
                var al = battleCam.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
            else
            {
                Debug.LogWarning("BattleWindow: 会戦カメラが見つかりませんでした（additive ロード後）。");
            }

            // 会戦シーンに含まれる EventSystem を無効化（戦略シーンの1つに統一）。
            // additive ロードで EventSystem が複数になると「2 event systems」警告が大量に出て uGUI 入力が競合する。
            DisableSceneEventSystems(scene);

            onLoaded?.Invoke(this);
        }

        /// <summary>指定シーン内の EventSystem をすべて無効化する（複数 EventSystem の競合/警告を防ぐ）。</summary>
        private static void DisableSceneEventSystems(Scene scene)
        {
            if (!scene.IsValid()) return;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                EventSystem[] systems = roots[i].GetComponentsInChildren<EventSystem>(true);
                for (int j = 0; j < systems.Length; j++)
                    if (systems[j] != null) systems[j].gameObject.SetActive(false);
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

        /// <summary>この会戦の BattleManager を返す（スナップショット注入・離脱に使う）。</summary>
        public BattleManager FindBattleManager()
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

        /// <summary>× / Esc：現状の優勢側を勝者として書き戻して離脱する（BattleManager 経由）。</summary>
        private void RequestLeave()
        {
            BattleManager bm = Ready ? FindBattleManager() : null;
            if (bm != null) bm.LeaveToStrategy();
            else BattleDirector.NotifyBattleEnded(battleScene); // 見つからなければ単に閉じる
        }

        // ===== UI 構築 =====

        private void Build(Vector2 anchoredPos)
        {
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
            windowRT.anchoredPosition = anchoredPos;
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
            // flexibleHeight=1＝タイトルバー以外の残り高さを埋める＝ウィンドウのサイズ変更に追従する。
            GameObject mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(win.transform, false);
            mapRT = mapGo.GetComponent<RectTransform>();
            LayoutElement le = mapGo.AddComponent<LayoutElement>();
            le.minHeight = 120f;
            le.flexibleHeight = 1f;
            mapImage = mapGo.AddComponent<RawImage>();
            mapImage.color = Color.white;
            mapImage.raycastTarget = false;

            BuildResizeGrip(win.transform);

            escWindowToken = UIWindowStack.Register(() => isOpen, RequestLeave, 940, "会戦");
        }

        /// <summary>右下のサイズ変更グリップ（つかんでドラッグでウィンドウを拡縮）。</summary>
        private void BuildResizeGrip(Transform winParent)
        {
            GameObject grip = new GameObject("ResizeGrip", typeof(RectTransform));
            grip.transform.SetParent(winParent, false);
            RectTransform g = grip.GetComponent<RectTransform>();
            g.anchorMin = g.anchorMax = new Vector2(1f, 0f); // 右下
            g.pivot = new Vector2(1f, 0f);
            g.sizeDelta = new Vector2(20f, 20f);
            g.anchoredPosition = Vector2.zero;
            LayoutElement gle = grip.AddComponent<LayoutElement>();
            gle.ignoreLayout = true; // VerticalLayoutGroup の行にせず右下に浮かせる
            Image gi = grip.AddComponent<Image>();
            gi.color = new Color(1f, 0.84f, 0.36f, 0.5f); // 金色の小さなつまみ
            MapWindowDrag drag = grip.AddComponent<MapWindowDrag>();
            drag.onDragDelta = OnResizeDrag;
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

            titleCap = CreateText(bar.transform, $"≡ {title} の戦い　（上部ドラッグで移動／右下で拡縮／× 離脱）", 15f, new Color(1f, 0.84f, 0.36f), TextAlignmentOptions.Left);
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
            cbtn.onClick.AddListener(RequestLeave);
            TextMeshProUGUI glyph = CreateText(cb.transform, "×", 18f, Color.white, TextAlignmentOptions.Center);
            StretchFull(glyph.rectTransform);
        }

        /// <summary>右下グリップのドラッグでウィンドウサイズを変える（最小/最大でクランプ）。</summary>
        private void OnResizeDrag(Vector2 delta)
        {
            if (windowRT == null) return;
            // 画面の下方向ドラッグ＝高さ増。右方向ドラッグ＝幅増。
            Vector2 sz = windowRT.sizeDelta + new Vector2(delta.x, -delta.y);
            sz.x = Mathf.Clamp(sz.x, 480f, 1920f);
            sz.y = Mathf.Clamp(sz.y, 320f, 1080f);
            windowRT.sizeDelta = sz;
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
