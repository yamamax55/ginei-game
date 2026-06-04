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
        [Tooltip("ズーム速度")]
        public float zoomSpeed = 5f;
        [Tooltip("最小ズームサイズ")]
        public float minZoom = 2f;
        [Tooltip("最大ズームサイズ")]
        public float maxZoom = 20f;

        [Header("フォーカス設定")]
        [Tooltip("フォーカス時の移動の滑らかさ")]
        public float focusSmoothTime = 0.2f;

        [Header("境界設定")]
        [Tooltip("カメラが移動可能な最小座標")]
        public Vector2 minBounds = new Vector2(-50f, -50f);
        [Tooltip("カメラが移動可能な最大座標")]
        public Vector2 maxBounds = new Vector2(50f, 50f);

        private Camera cam;
        private FleetCommander commander;
        private Vector3 velocity = Vector3.zero;
        private bool isFocusing = false;
        private Vector2 lastMousePos;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            commander = Object.FindAnyObjectByType<FleetCommander>();
        }

        private void Update()
        {
            HandlePan();
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
            
            // キー入力（WASD / 矢印）
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1;
            }

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
        /// マウスホイールによるズーム操作。
        /// </summary>
        private void HandleZoom()
        {
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // スクロール方向に応じてサイズを変更
                float newSize = cam.orthographicSize - (scroll * 0.01f * zoomSpeed);
                cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
            }
        }

        /// <summary>
        /// Fキーによる選択艦隊へのフォーカス。
        /// </summary>
        private void HandleFocus()
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
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
    }
}
