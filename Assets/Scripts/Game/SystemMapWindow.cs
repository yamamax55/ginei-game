using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 星系の恒星系マップ（<see cref="SystemView"/>＝恒星＋軌道＋惑星）を**ウィンドウ内に表示**する（非モーダル）。
    /// 旧：星系ダブルクリックで Battle シーンへ全画面遷移。新：その場で窓を開く。
    /// 実装＝SystemView を遠方に生成し専用カメラで <see cref="RenderTexture"/> に描き、RawImage に映す
    /// （SystemView のロジック＝惑星の決定的内政生成・表示を丸ごと再利用）。惑星クリックは窓→
    /// <see cref="SystemView.SelectPlanetAtWorld"/> へ橋渡し。タイトルバーをつかんで移動・× で閉じる。
    /// </summary>
    public class SystemMapWindow : MonoBehaviour
    {
        private static SystemMapWindow instance;

        /// <summary>窓が開いているか。</summary>
        public static bool IsOpen => instance != null && instance.isOpen;

        /// <summary>
        /// カーソルが星系図ウィンドウ（枠）の上にあるか。<see cref="GalaxyView"/> がこの間は
        /// マウス操作（ホイールズーム／ドラッグ／クリック）を星系図に譲り、銀河マップと二重に反応しないようにする。
        /// </summary>
        public static bool PointerOverWindow
        {
            get
            {
                if (instance == null || !instance.isOpen || instance.windowRT == null || Mouse.current == null) return false;
                Vector2 sp = Mouse.current.position.ReadValue();
                return RectTransformUtility.RectangleContainsScreenPoint(instance.windowRT, sp, null);
            }
        }

        [Header("ウィンドウ")]
        [Tooltip("ウィンドウのサイズ（タイトルバー＋マップ領域）")]
        public Vector2 windowSize = new Vector2(760f, 520f);
        [Tooltip("マップ描画（RenderTexture）の解像度＝RawImage と同アスペクトで歪まない")]
        public int rtWidth = 760;
        public int rtHeight = 470;

        [Header("描画カメラ")]
        [Tooltip("SystemView 本体を置く遠方オフセット（主カメラから隔離＝銀河に映り込まない）")]
        public Vector2 viewOffset = new Vector2(5000f, 0f);
        [Tooltip("描画カメラのズーム（orthographicSize）。恒星系全体＋惑星情報が収まる広さ")]
        public float viewSize = 11f;
        [Tooltip("注視中心（本体相対）。右の惑星情報ラベルも入るよう少し右へ寄せる")]
        public Vector2 viewCenter = new Vector2(2.5f, 0f);

        [Header("操作（ホイールズーム／右ドラッグでパン）")]
        [Tooltip("ズームの最小（寄り）／最大（引き）orthographicSize")]
        public float minViewSize = 5f;
        public float maxViewSize = 22f;
        [Tooltip("ホイール1ノッチあたりのズーム量")]
        public float zoomStep = 1.5f;
        [Tooltip("パンで中心からどこまで離れられるか（ワールド単位・±）")]
        public float panRange = 14f;

        private bool isOpen;
        private object escWindowToken;  // UIWindowStack 登録トークン（#ウィンドウESC）
        private float liveViewSize;     // 現在のズーム（viewSize を実行時に上書きせず別持ち＝実効値パターン）
        private Vector2 viewBaseCenter; // パンの基準中心（クランプ用）
        private bool panActive;
        private Vector3 panGrabWorld;   // つかんだワールド点（グラブ式パンの碇）
        private GameObject root;
        private RectTransform windowRT;   // 枠ウィンドウ（カーソル占有判定に使う）
        private RawImage mapImage;
        private RectTransform mapRT;
        private RenderTexture rt;
        private Camera viewCam;
        private SystemView systemView;
        private TextMeshProUGUI titleCap;

        /// <summary>指定星系の恒星系マップ窓を開く（必要なら生成）。</summary>
        public static void Show(int systemId, string systemName, Faction owner)
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SystemMapWindow");
                instance = go.AddComponent<SystemMapWindow>();
                instance.Build();
            }
            instance.Open(systemId, systemName, owner);
        }

        // ===== UI 構築 =====

        private void Build()
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject("SystemMapCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950; // 通知/マップより前・観測窓(1090)より後ろ
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // root：全画面の透明コンテナ（ディマー無し＝非モーダル＝背後のマップ操作を塞がない）
            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            // 枠ウィンドウ（中央・ドラッグ移動）
            GameObject win = new GameObject("Window", typeof(RectTransform));
            win.transform.SetParent(root.transform, false);
            RectTransform winRT = win.GetComponent<RectTransform>();
            windowRT = winRT;
            winRT.anchorMin = winRT.anchorMax = winRT.pivot = new Vector2(0.5f, 0.5f);
            winRT.sizeDelta = windowSize;
            winRT.anchoredPosition = Vector2.zero;
            Image winImg = win.AddComponent<Image>();
            winImg.color = new Color(0.04f, 0.05f, 0.09f, 0.98f);
            Outline border = win.AddComponent<Outline>();
            border.effectColor = new Color(1f, 0.84f, 0.36f, 0.5f);
            border.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup vlg = win.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0f;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            BuildTitleBar(win.transform, winRT);

            // マップ領域（RawImage＝RenderTexture を映す。固定高でRTと同アスペクト）
            GameObject mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(win.transform, false);
            mapRT = mapGo.GetComponent<RectTransform>();
            LayoutElement le = mapGo.AddComponent<LayoutElement>();
            le.preferredHeight = windowSize.y - 30f; // タイトルバー(30)を除いた残り
            mapImage = mapGo.AddComponent<RawImage>();
            mapImage.color = Color.white;

            root.SetActive(false);

            // ESC は UIWindowStack 経由で「手前から閉じる」（#ウィンドウESC）。
            escWindowToken = UIWindowStack.Register(() => isOpen, Close, 950, "星系図");
        }

        private void BuildTitleBar(Transform parent, RectTransform windowRT)
        {
            GameObject bar = new GameObject("TitleBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            Image img = bar.AddComponent<Image>();
            img.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            LayoutElement le = bar.AddComponent<LayoutElement>();
            le.minHeight = 30f; le.preferredHeight = 30f;
            UIDragMove drag = bar.AddComponent<UIDragMove>();
            drag.target = windowRT;

            titleCap = CreateText(bar.transform, "≡ 星系図　（ホイールでズーム／右ドラッグで移動）", 15f, new Color(1f, 0.84f, 0.36f), TextAlignmentOptions.Left);
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
            cbtn.onClick.AddListener(Close);
            TextMeshProUGUI glyph = CreateText(cb.transform, "×", 18f, Color.white, TextAlignmentOptions.Center);
            StretchFull(glyph.rectTransform);
        }

        // ===== 開閉 =====

        private void Open(int systemId, string systemName, Faction owner)
        {
            Cleanup(); // 既存の描画資源を破棄してから作り直す

            rt = new RenderTexture(rtWidth, rtHeight, 16);
            rt.Create();
            if (mapImage != null) mapImage.texture = rt;

            // SystemView 本体を生成し、遠方へ隔離（主カメラに映り込まない）。Build は内部で position=zero にするため後で移動。
            GameObject svGo = new GameObject("WindowSystemView");
            svGo.transform.SetParent(transform, false);
            systemView = svGo.AddComponent<SystemView>();
            systemView.systemId = systemId;
            systemView.systemName = systemName;
            systemView.ownerFaction = owner;
            systemView.windowedMode = true;
            if (owner == Faction.同盟) systemView.starColor = new Color(0.7f, 0.85f, 1f);
            systemView.Build();
            svGo.transform.position = new Vector3(viewOffset.x, viewOffset.y, 0f);

            // 専用カメラ（RenderTexture へ描く）。遠方ゆえ視野には SystemView だけが入る。
            GameObject camGo = new GameObject("SystemViewCamera");
            camGo.transform.SetParent(transform, false);
            viewCam = camGo.AddComponent<Camera>();
            viewCam.orthographic = true;
            viewCam.orthographicSize = viewSize;
            viewCam.transform.position = new Vector3(viewOffset.x + viewCenter.x, viewOffset.y + viewCenter.y, -10f);
            viewCam.clearFlags = CameraClearFlags.SolidColor;
            viewCam.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 1f);
            viewCam.cullingMask = ~0;
            viewCam.targetTexture = rt; // アスペクトは RT 寸法から自動導出（RawImage と一致）

            // ズーム/パンの初期化（窓を開くたびに既定の俯瞰へ戻す）
            liveViewSize = viewSize;
            viewBaseCenter = new Vector2(viewOffset.x + viewCenter.x, viewOffset.y + viewCenter.y);
            panActive = false;

            if (titleCap != null) titleCap.text = $"≡ {systemName} 星系図　（惑星クリックで内政／ホイールでズーム／右ドラッグで移動）";

            isOpen = true;
            if (root != null) root.SetActive(true);
        }

        /// <summary>窓を閉じて描画資源を解放する。</summary>
        public void Close()
        {
            isOpen = false;
            Cleanup();
            if (root != null) root.SetActive(false);
        }

        private void Cleanup()
        {
            if (systemView != null) { Destroy(systemView.gameObject); systemView = null; }
            if (viewCam != null) { Destroy(viewCam.gameObject); viewCam = null; }
            if (rt != null)
            {
                if (mapImage != null) mapImage.texture = null;
                rt.Release();
                Destroy(rt);
                rt = null;
            }
        }

        private void Update()
        {
            if (!isOpen) return;
            // Esc は UIWindowStack 経由（GalaxyView）で「手前から閉じる」＝自前で読まない。
            if (Mouse.current == null || viewCam == null || mapRT == null) return;

            bool overMap = TryCursorWorld(out Vector3 cursorWorld);

            // ① ホイールズーム（カーソル中心＝カーソル下のワールド点を保ったまま寄る/引く）。
            if (overMap)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Vector3 before = cursorWorld;
                    liveViewSize = Mathf.Clamp(liveViewSize - Mathf.Sign(scroll) * zoomStep, minViewSize, maxViewSize);
                    viewCam.orthographicSize = liveViewSize;
                    if (TryCursorWorld(out Vector3 after))
                    {
                        Vector3 shift = before - after;
                        MoveCam(viewCam.transform.position + new Vector3(shift.x, shift.y, 0f));
                    }
                }
            }

            // ② 右/中ドラッグでパン（グラブ式＝つかんだ点をカーソルへ貼り付ける）。左クリックは惑星選択に温存。
            bool dragHeld = Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed;
            bool dragStart = Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame;
            if (dragStart && overMap) { panActive = true; panGrabWorld = cursorWorld; }
            if (!dragHeld) panActive = false;
            if (panActive && dragHeld && TryCursorWorld(out Vector3 nowWorld))
            {
                Vector3 d = panGrabWorld - nowWorld; // つかんだ点が動いたぶんカメラを逆へ寄せる
                MoveCam(viewCam.transform.position + new Vector3(d.x, d.y, 0f));
            }

            // ③ 左クリック → 最寄り惑星を選択（マップ領域内のみ）。
            if (Mouse.current.leftButton.wasPressedThisFrame && overMap && systemView != null)
                systemView.SelectPlanetAtWorld(cursorWorld);
        }

        /// <summary>カーソルがマップ領域内なら、専用カメラの z=0 面でのワールド点を返す。</summary>
        private bool TryCursorWorld(out Vector3 world)
        {
            world = Vector3.zero;
            Vector2 sp = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(mapRT, sp, null)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRT, sp, null, out Vector2 local)) return false;
            Rect r = mapRT.rect;
            float vx = Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1e-4f, r.width));
            float vy = Mathf.Clamp01((local.y - r.yMin) / Mathf.Max(1e-4f, r.height));
            float depth = -viewCam.transform.position.z; // z=0 面までの距離
            world = viewCam.ViewportToWorldPoint(new Vector3(vx, vy, depth));
            world.z = 0f;
            return true;
        }

        /// <summary>描画カメラを基準中心 ±panRange にクランプして移動（z は維持＝虚空へ飛ばさない）。</summary>
        private void MoveCam(Vector3 pos)
        {
            float cx = Mathf.Clamp(pos.x, viewBaseCenter.x - panRange, viewBaseCenter.x + panRange);
            float cy = Mathf.Clamp(pos.y, viewBaseCenter.y - panRange, viewBaseCenter.y + panRange);
            viewCam.transform.position = new Vector3(cx, cy, pos.z);
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
