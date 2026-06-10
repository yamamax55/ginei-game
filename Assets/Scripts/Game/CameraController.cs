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
        [Tooltip("ズーム速度（マウスホイール）")]
        public float zoomSpeed = 12f;
        [Tooltip("最小ズームサイズ")]
        public float minZoom = 2f;
        [Tooltip("最大ズームサイズ")]
        public float maxZoom = 30f;
        [Tooltip("開始時のズーム（大きいほど引いた画。会戦開始時に適用）")]
        public float startZoom = 16f;

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
        }

        private void Update()
        {
            // 設定画面のトグル（GameSettings.edgeScrollEnabled）を反映（#87）
            if (GameSettings.Instance != null) edgeScrollEnabled = GameSettings.Instance.edgeScrollEnabled;

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
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) <= 0.01f) return;

            // ズーム前のカーソル下ワールド座標（2D 直交＝入力 z は x/y に影響しない）
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector3 worldBefore = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));

            float newSize = cam.orthographicSize - (scroll * 0.01f * zoomSpeed);
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);

            // ズーム後に同じスクリーン点が指す座標を求め、ズレぶんカメラを移動＝カーソル中心ズーム
            Vector3 worldAfter = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));
            Vector3 shift = worldBefore - worldAfter;
            transform.position += new Vector3(shift.x, shift.y, 0f);
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
