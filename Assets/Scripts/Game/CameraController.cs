using UnityEngine;
using UnityEngine.InputSystem;

namespace Ginei
{
    /// <summary>
    /// メインカメラの操作（パン、ズーム、フォーカス）を制御するクラス。
    /// 2D Orthographic カメラを前提としています。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("パン設定")]
        [Tooltip("キー入力による移動速度")]
        public float panSpeed = 20f;
        [Tooltip("中ボタンドラッグの移動感度")]
        public float dragSensitivity = 1.0f;
        
        [Header("ズーム設定")]
        [Tooltip("ズーム感度（ホイール速度に比例。大きいほど速い）")]
        public float zoomSpeed = 20f;
        [Tooltip("ズームの滑らかさ（目標サイズへ寄る速さ。大きいほどキビキビ）")]
        public float zoomSmoothing = 12f;
        [Tooltip("最小ズームサイズ（小さいほど深くズームイン）")]
        public float minZoom = 1f;
        [Tooltip("最大ズームサイズ（大きいほど広くズームアウト＝MAP全体）")]
        public float maxZoom = 300f;
        [Tooltip("開始時のズーム（大きいほど引いた画。会戦開始時に適用・フォールバック）")]
        public float startZoom = 16f;
        [Tooltip("会戦開始時に全艦隊が収まるよう自動でMAP全体にズームアウトする")]
        public bool frameWholeBattlefieldOnStart = true;
        [Tooltip("MAP全体フレーミング時の余白倍率（1.0=ぴったり／大きいほど引く）")]
        public float frameMargin = 1.25f;

        [Header("画面端スクロール (#87)")]
        [Tooltip("マウスを画面端に寄せるとパンする（設定で有効/無効・GameSettings.edgeScrollEnabled を優先）")]
        public bool edgeScrollEnabled = true;
        [Tooltip("画面端と判定する余白（px）")]
        public float edgeMargin = 12f;
        [Tooltip("画面端スクロールの速度")]
        public float edgeScrollSpeed = 18f;

        [Header("フォーカス設定")]
        [Tooltip("フォーカス時の移動の滑らかさ")]
        public float focusSmoothTime = 0.2f;

        [Header("境界設定")]
        [Tooltip("カメラが移動可能な最小座標")]
        public Vector2 minBounds = new Vector2(-50f, -50f);
        [Tooltip("カメラが移動可能な最大座標")]
        public Vector2 maxBounds = new Vector2(50f, 50f);

        [Header("シェイク設定")]
        [Tooltip("撃沈時のカメラ揺れの強さ")]
        public float shakeMagnitude = 0.15f;
        [Tooltip("撃沈時のカメラ揺れの長さ (秒)")]
        public float shakeDuration = 0.15f;

        private Camera cam;
        private FleetCommander commander;
        private Vector3 velocity = Vector3.zero;
        private bool isFocusing = false;
        private Vector2 lastMousePos;

        private float shakeTimer = 0f;
        private Vector3 lastShakeOffset = Vector3.zero;

        private float zoomTarget;          // 滑らかズームの目標 orthographicSize
        private bool framedOnce;           // MAP全体フレーミング（艦隊が湧くのを待って一度だけ）

        private void Awake()
        {
            cam = GetComponent<Camera>();
            commander = Object.FindAnyObjectByType<FleetCommander>();
        }

        private void Start()
        {
            // 会戦開始時のズームは設定画面の値（GameSettings.cameraStartZoom）を優先。
            // 未取得時は Inspector の startZoom にフォールバック。
            float z = startZoom;
            if (GameSettings.Instance != null) z = GameSettings.Instance.cameraStartZoom;
            if (cam != null) cam.orthographicSize = Mathf.Clamp(z, minZoom, maxZoom);
            zoomTarget = cam != null ? cam.orthographicSize : z; // 滑らかズームの初期目標
        }

        private void Update()
        {
            // 設定画面のトグル（GameSettings.edgeScrollEnabled）を反映（#87）
            if (GameSettings.Instance != null) edgeScrollEnabled = GameSettings.Instance.edgeScrollEnabled;

            // 会戦開始時：全艦隊が湧いたらMAP全体が見渡せるよう一度だけズームアウト（艦隊登録を待つ）。
            if (!framedOnce && frameWholeBattlefieldOnStart) framedOnce = TryFrameWholeBattlefield();

            HandlePan();
            HandleEdgeScroll();
            HandleZoom();
            HandleFocus();

            // 範囲制限
            ClampPosition();
        }

        /// <summary>
        /// WASDキーおよび中ボタンドラッグによるパン操作。
        /// </summary>
        private void HandlePan()
        {
            Vector2 input = Vector2.zero;

            // キー入力（WASD / 矢印）は GameInput に集約（#107）。各アクションは複数キー（W/↑等）を OR 評価。
            if (GameInput.IsHeld(GameAction.カメラ上)) input.y += 1;
            if (GameInput.IsHeld(GameAction.カメラ下)) input.y -= 1;
            if (GameInput.IsHeld(GameAction.カメラ左)) input.x -= 1;
            if (GameInput.IsHeld(GameAction.カメラ右)) input.x += 1;

            if (input != Vector2.zero)
            {
                isFocusing = false; // キー操作時はフォーカス解除
                // ズーム量に応じて移動速度を補正（ズームイン中は遅く、アウト中は速く）
                float speedMultiplier = cam.orthographicSize / 10f;
                transform.position += (Vector3)(input.normalized * panSpeed * speedMultiplier * Time.deltaTime);
            }

            // 中ボタンドラッグ
            if (Mouse.current != null)
            {
                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                
                if (Mouse.current.middleButton.wasPressedThisFrame)
                {
                    lastMousePos = currentMousePos;
                }
                
                if (Mouse.current.middleButton.isPressed)
                {
                    isFocusing = false;
                    Vector2 delta = currentMousePos - lastMousePos;
                    // スクリーン座標のデルタをワールド空間の移動量に変換
                    // ズームレベル（orthographicSize）を考慮
                    float worldDeltaX = (delta.x / Screen.width) * cam.orthographicSize * 2 * cam.aspect;
                    float worldDeltaY = (delta.y / Screen.height) * cam.orthographicSize * 2;
                    
                    transform.position -= new Vector3(worldDeltaX, worldDeltaY, 0) * dragSensitivity;
                    lastMousePos = currentMousePos;
                }
            }
        }

        /// <summary>
        /// マウスが画面端 edgeMargin(px) に入ったらその方向へパンする（#87）。
        /// ウィンドウ非フォーカス時・カーソルが画面外のときは無効。中ドラッグ中も加算しない。
        /// </summary>
        private void HandleEdgeScroll()
        {
            if (!edgeScrollEnabled) return;
            if (!Application.isFocused) return;          // 非フォーカス時は無効
            if (Mouse.current == null) return;
            if (Mouse.current.middleButton.isPressed) return; // ドラッグ中は端スクロールしない

            Vector2 m = Mouse.current.position.ReadValue();
            // カーソルが画面外なら無効（誤爆防止）
            if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return;

            Vector2 dir = Vector2.zero;
            if (m.x <= edgeMargin) dir.x -= 1f;
            else if (m.x >= Screen.width - edgeMargin) dir.x += 1f;
            if (m.y <= edgeMargin) dir.y -= 1f;
            else if (m.y >= Screen.height - edgeMargin) dir.y += 1f;

            if (dir != Vector2.zero)
            {
                isFocusing = false;
                float speedMultiplier = cam.orthographicSize / 10f; // ズームに応じて速度補正（パンと同方針）
                transform.position += (Vector3)(dir.normalized * edgeScrollSpeed * speedMultiplier * Time.deltaTime);
            }
        }

        /// <summary>
        /// マウスホイールによるズーム操作。**カーソル中心ズーム（#87）**＝ズーム前後でカーソル下のワールド座標を維持する。
        /// </summary>
        private void HandleZoom()
        {
            if (cam == null) return;

            // ホイール入力で目標サイズを更新（倍率式＝深度に依らず一定の体感／ホイール速度に比例）。
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float raw = scroll * zoomSpeed * 0.01f;            // 速く回すほど大きい
                    float factor = Mathf.Clamp(1f - raw, 0.4f, 2.5f); // 1イベントの倍率（暴れ防止のクランプ）
                    zoomTarget = Mathf.Clamp(zoomTarget * factor, minZoom, maxZoom);
                }
            }

            // 目標サイズへ滑らかに寄せる（フレームレート非依存・ポーズ中も効くよう unscaled）＋カーソル中心維持。
            if (Mathf.Abs(cam.orthographicSize - zoomTarget) > 0.0001f)
            {
                Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue()
                    : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector3 worldBefore = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));
                float t = 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, zoomTarget, t);
                Vector3 worldAfter = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));
                Vector3 shift = worldBefore - worldAfter;
                transform.position += new Vector3(shift.x, shift.y, 0f);
            }
        }

        /// <summary>
        /// 全旗艦（艦隊）が画面に収まるよう、その外接矩形の中心へカメラを移し orthographicSize を合わせる
        /// （会戦開始時に一度・MAP全体が見渡せる引いた画）。艦隊未登録（湧く前/非戦闘ビュー）のときは false で次フレーム再試行。
        /// </summary>
        private bool TryFrameWholeBattlefield()
        {
            if (cam == null) return false;
            var flags = FleetRegistry.AllFlagships;
            if (flags == null || flags.Count == 0) return false;

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            int n = 0;
            for (int i = 0; i < flags.Count; i++)
            {
                var fs = flags[i];
                if (fs == null) continue;
                Vector2 p = fs.transform.position;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                n++;
            }
            if (n == 0) return false;

            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            float halfH = (maxY - minY) * 0.5f;
            float halfW = (maxX - minX) * 0.5f;
            float aspect = cam.aspect > 0.01f ? cam.aspect : 1.6f;
            float needSize = Mathf.Max(halfH, halfW / aspect) * frameMargin;
            needSize = Mathf.Clamp(Mathf.Max(needSize, 4f), minZoom, maxZoom); // 1隊だけ等で潰れないよう下限

            cam.orthographicSize = needSize;
            zoomTarget = needSize;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
            return true;
        }

        /// <summary>
        /// Fキーによる選択艦隊へのフォーカス。
        /// </summary>
        private void HandleFocus()
        {
            if (GameInput.WasPressed(GameAction.選択フォーカス))
            {
                isFocusing = true;
            }

            if (isFocusing && commander != null && commander.SelectedFleets.Count > 0)
            {
                Transform target = commander.SelectedFleets[0].transform;
                Vector3 targetPos = new Vector3(target.position.x, target.position.y, transform.position.z);
                
                // 滑らかに追従
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, focusSmoothTime);
                
                // 到着したら（あるいは距離が十分近ければ）微調整はSmoothDampに任せるが、
                // ユーザーが手動操作したらisFocusingがfalseになるように設計
            }
            else if (commander != null && commander.SelectedFleets.Count == 0)
            {
                isFocusing = false;
            }
        }

        /// <summary>
        /// カメラ位置を指定された境界内にクランプします。
        /// </summary>
        private void ClampPosition()
        {
            float x = Mathf.Clamp(transform.position.x, minBounds.x, maxBounds.x);
            float y = Mathf.Clamp(transform.position.y, minBounds.y, maxBounds.y);
            transform.position = new Vector3(x, y, transform.position.z);
        }

        /// <summary>
        /// 撃沈時などに短いカメラシェイクを開始します。
        /// </summary>
        public void Shake()
        {
            shakeTimer = shakeDuration;
        }

        /// <summary>
        /// パン/クランプ後にシェイクのオフセットを一時的に加える。
        /// 毎フレーム前回のオフセットを戻すため、位置に恒久的に蓄積しない。
        /// 撃沈で会戦終了(timeScale=0)しても揺れが見えるよう unscaled で減衰させる。
        /// </summary>
        private void LateUpdate()
        {
            // 前フレームのシェイクオフセットを戻す
            transform.position -= lastShakeOffset;
            lastShakeOffset = Vector3.zero;

            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                float damper = Mathf.Clamp01(shakeTimer / shakeDuration);
                Vector2 rnd = Random.insideUnitCircle * shakeMagnitude * damper;
                lastShakeOffset = new Vector3(rnd.x, rnd.y, 0f);
                transform.position += lastShakeOffset;
            }
        }
    }
}
