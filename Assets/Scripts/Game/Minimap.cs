using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 会戦のミニマップ（俯瞰・クリックジャンプ #84・RTS-3）。
    /// 第2カメラを使わず、uGUI 上に <see cref="FleetRegistry.AllFlagships"/> を陣営色ドットで
    /// 間引きプロットし、ブラックホール（<see cref="BlackHole.All"/>）もアイコン表示する。
    /// 現在のカメラ視界を矩形で重畳し、ミニマップのクリック／ドラッグで <see cref="CameraController.JumpTo"/>。
    /// 戦場ワールド矩形は <see cref="CameraController.MinBounds"/>/<see cref="CameraController.MaxBounds"/> を写像。
    /// Battle シーンに RuntimeInitializeOnLoadMethod で自動生成（手置き不要）。表示/非表示は C キー（#107）。
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Header("レイアウト")]
        [Tooltip("ミニマップ枠の一辺サイズ（px）")]
        public float panelSize = 220f;
        [Tooltip("画面端からの余白（px）")]
        public float screenMargin = 12f;
        [Tooltip("枠内の内側余白（px）")]
        public float contentPadding = 8f;

        [Header("ドット")]
        [Tooltip("艦隊ドットの一辺（px）")]
        public float dotSize = 5f;
        [Tooltip("ブラックホールアイコンの一辺（px）")]
        public float blackHoleSize = 8f;

        [Header("更新")]
        [Tooltip("ドット再配置の間隔（秒・unscaled）。視界矩形は毎フレーム更新")]
        public float refreshInterval = 0.1f;

        // 帝国/同盟の既定色（FactionData 未設定時のフォールバック・FactionColor と同値）。
        private static readonly Color ImperialColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color AllianceColor = new Color(0.2f, 0.5f, 0.9f);
        private static readonly Color BlackHoleColor = new Color(0.6f, 0.3f, 0.9f);
        private static readonly Color ViewRectColor = new Color(1f, 1f, 1f, 0.18f);

        private Canvas canvas;
        private RectTransform content;     // ドットを配置する内側領域（pivot 中央）
        private RectTransform viewRect;    // 現在のカメラ視界を示す矩形
        private Image frameImage;          // クリック判定の対象（raycastTarget）
        private readonly List<Image> dotPool = new List<Image>();
        private readonly List<Image> blackHolePool = new List<Image>();

        private Camera cam;
        private CameraController camController;
        private float refreshTimer;
        private bool visible = true;

        // ===== 自動生成エントリーポイント（BattleAllegianceManager と同型）=====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle") return;
            if (Object.FindAnyObjectByType<Minimap>() != null) return;
            new GameObject("Minimap").AddComponent<Minimap>();
        }

        // ===== 本体 =====

        private void Start()
        {
            cam = Camera.main;
            camController = Object.FindAnyObjectByType<CameraController>();
            EnsureEventSystem();
            Build();
        }

        private void Update()
        {
            // 表示/非表示トグル（C・GameInput #107）
            if (GameInput.WasPressed(GameAction.ミニマップ切替))
            {
                visible = !visible;
                if (canvas != null) canvas.gameObject.SetActive(visible);
            }
            if (!visible || canvas == null) return;

            // 視界矩形は毎フレーム、ドットは間引いて更新（unscaled＝ポーズ中も追従）。
            UpdateViewRect();
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= refreshInterval)
            {
                refreshTimer = 0f;
                PlotFleets();
                PlotBlackHoles();
            }

            HandleClickJump();
        }

        /// <summary>戦場ワールド矩形（カメラ移動境界）。CameraController が無ければ既定値。</summary>
        private void GetWorldBounds(out Vector2 min, out Vector2 max)
        {
            if (camController != null)
            {
                min = camController.MinBounds;
                max = camController.MaxBounds;
            }
            else
            {
                min = new Vector2(-200f, -200f);
                max = new Vector2(200f, 200f);
            }
        }

        /// <summary>ワールド座標→ミニマップ内のローカル座標（content 中央原点）。</summary>
        private Vector2 WorldToLocal(Vector2 world)
        {
            GetWorldBounds(out Vector2 min, out Vector2 max);
            float u = Mathf.Clamp01(Mathf.InverseLerp(min.x, max.x, world.x));
            float v = Mathf.Clamp01(Mathf.InverseLerp(min.y, max.y, world.y));
            float w = content.rect.width;
            float h = content.rect.height;
            return new Vector2((u - 0.5f) * w, (v - 0.5f) * h);
        }

        private void PlotFleets()
        {
            IReadOnlyList<FleetStrength> flags = FleetRegistry.AllFlagships;
            int used = 0;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength fs = flags[i];
                if (fs == null || !fs.IsAlive) continue;

                Image dot = GetPooledDot(dotPool, used, dotSize, "Dot");
                dot.color = ResolveColor(fs);
                dot.rectTransform.anchoredPosition = WorldToLocal(fs.transform.position);
                dot.enabled = true;
                used++;
            }
            // 余ったドットは隠す
            for (int i = used; i < dotPool.Count; i++) dotPool[i].enabled = false;
        }

        private void PlotBlackHoles()
        {
            IReadOnlyList<BlackHole> holes = BlackHole.All;
            int used = 0;
            for (int i = 0; i < holes.Count; i++)
            {
                BlackHole bh = holes[i];
                if (bh == null) continue;

                Image icon = GetPooledDot(blackHolePool, used, blackHoleSize, "BlackHole");
                icon.color = BlackHoleColor;
                icon.rectTransform.anchoredPosition = WorldToLocal(bh.transform.position);
                icon.enabled = true;
                used++;
            }
            for (int i = used; i < blackHolePool.Count; i++) blackHolePool[i].enabled = false;
        }

        /// <summary>現在のカメラ視界（orthographicSize×aspect）をミニマップ矩形に写す。</summary>
        private void UpdateViewRect()
        {
            if (cam == null || viewRect == null) return;
            GetWorldBounds(out Vector2 min, out Vector2 max);

            Vector2 camCenter = cam.transform.position;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            float w = content.rect.width;
            float h = content.rect.height;
            float spanX = Mathf.Max(0.0001f, max.x - min.x);
            float spanY = Mathf.Max(0.0001f, max.y - min.y);

            viewRect.anchoredPosition = WorldToLocal(camCenter);
            float rw = Mathf.Clamp((halfW * 2f) / spanX, 0f, 1f) * w;
            float rh = Mathf.Clamp((halfH * 2f) / spanY, 0f, 1f) * h;
            viewRect.sizeDelta = new Vector2(rw, rh);
        }

        /// <summary>ミニマップ上の左クリック／ドラッグでカメラをその現場へジャンプさせる。</summary>
        private void HandleClickJump()
        {
            if (camController == null || Mouse.current == null) return;
            if (!Mouse.current.leftButton.isPressed) return; // 押下中＝ドラッグ追従

            Vector2 screen = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(content, screen, null)) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, null, out Vector2 local))
                return;

            float w = content.rect.width;
            float h = content.rect.height;
            float u = Mathf.Clamp01(local.x / w + 0.5f);
            float v = Mathf.Clamp01(local.y / h + 0.5f);
            GetWorldBounds(out Vector2 min, out Vector2 max);
            Vector2 world = new Vector2(Mathf.Lerp(min.x, max.x, u), Mathf.Lerp(min.y, max.y, v));
            camController.JumpTo(world);
        }

        /// <summary>艦隊の陣営色（FactionData があればその色・無ければ enum 既定色）。FactionColor と同方針。</summary>
        private static Color ResolveColor(FleetStrength fs)
        {
            if (fs.factionData != null) return fs.factionData.color;
            return fs.faction == Faction.帝国 ? ImperialColor : AllianceColor;
        }

        private Image GetPooledDot(List<Image> pool, int index, float size, string label)
        {
            if (index < pool.Count) return pool[index];

            GameObject obj = new GameObject(label);
            obj.transform.SetParent(content, false);
            Image img = obj.AddComponent<Image>();
            img.raycastTarget = false; // クリック判定は枠（frameImage）だけが担う
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            pool.Add(img);
            return img;
        }

        // ===== UI 構築 =====

        private void Build()
        {
            // Canvas（最前面手前・ポーズメニュー sortingOrder 1000 より下）
            GameObject canvasObj = new GameObject("MinimapCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObj.AddComponent<GraphicRaycaster>(); // IsPointerOverGameObject 用（FleetCommander の誤選択回避）

            // 枠（右下アンカー）＝クリック判定の対象
            GameObject frame = new GameObject("MinimapFrame");
            frame.transform.SetParent(canvasObj.transform, false);
            frameImage = frame.AddComponent<Image>();
            frameImage.color = new Color(0.05f, 0.07f, 0.1f, 0.78f);
            frameImage.raycastTarget = true;
            RectTransform frameRT = frameImage.rectTransform;
            frameRT.anchorMin = frameRT.anchorMax = new Vector2(1f, 0f); // 右下
            frameRT.pivot = new Vector2(1f, 0f);
            frameRT.sizeDelta = new Vector2(panelSize, panelSize);
            frameRT.anchoredPosition = new Vector2(-screenMargin, screenMargin);

            // 内側のマップ領域（padding ぶん縮めた中央 pivot の領域）
            GameObject contentObj = new GameObject("MinimapContent");
            contentObj.transform.SetParent(frame.transform, false);
            content = contentObj.AddComponent<RectTransform>();
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.offsetMin = new Vector2(contentPadding, contentPadding);
            content.offsetMax = new Vector2(-contentPadding, -contentPadding);
            // マスク：はみ出したドットを枠内に収める
            Image maskImg = contentObj.AddComponent<Image>();
            maskImg.color = new Color(0.1f, 0.13f, 0.16f, 0.5f);
            maskImg.raycastTarget = false;
            contentObj.AddComponent<RectMask2D>();

            // 視界矩形（白の半透明・最後に作って手前へ）
            GameObject vr = new GameObject("ViewRect");
            vr.transform.SetParent(content, false);
            Image vrImg = vr.AddComponent<Image>();
            vrImg.color = ViewRectColor;
            vrImg.raycastTarget = false;
            viewRect = vrImg.rectTransform;
            viewRect.anchorMin = viewRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.sizeDelta = new Vector2(20f, 20f);
        }

        /// <summary>EventSystem が無ければ生成（IsPointerOverGameObject／クリック判定に必要）。</summary>
        private void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
