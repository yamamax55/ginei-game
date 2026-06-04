using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// プレイヤーの入力を管理し、艦隊の選択およびメニューの呼び出しを制御します。
    /// </summary>
    public class FleetCommander : MonoBehaviour
    {
        [Header("設定")]
        [Tooltip("メインカメラ（nullの場合はCamera.mainを使用）")]
        public Camera mainCamera;

        // 現在選択中の艦隊リスト
        private List<Selectable> selectedFleets = new List<Selectable>();

        public List<Selectable> SelectedFleets => selectedFleets;

        private bool isWaitingForMoveTarget = false;

        private void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void Update()
        {
            // ゲーム停止中は入力を受け付けない
            if (Time.timeScale == 0) return;

            // 移動先指定待ちの状態
if (isWaitingForMoveTarget)
            {
                // 右クリックで目的地を確定
                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    // 修正：UI（艦隊情報パネル等）の上であっても、右クリックなら目的地として受け付ける
                    ExecuteMoveCommand();
                }
                
                // Escキーでキャンセル
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CancelMoveTargetSelection();
                }
                
                return; // 選択モードの時は通常の選択処理は行わない
            }

            // 左クリック: 通常選択
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // UIクリック中なら無視
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

                HandleSelection();
            }

            // 右クリック: コマンドメニューの呼び出し
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                // 修正：UIの上であっても、右クリックメニューを呼び出せるようにする
                OpenCommandMenu();
            }
}

        /// <summary>
        /// 移動コマンドの目的地指定モードを開始します。
        /// </summary>
        public void StartWaitingForMoveTarget()
        {
            isWaitingForMoveTarget = true;
            Debug.Log("移動先を指定してください（右クリック）。Escでキャンセル。");
            // ここでカーソルを変更したり、UIにメッセージを出したりすることも可能
        }

        /// <summary>
        /// 実際に移動を実行します。
        /// </summary>
        private void ExecuteMoveCommand()
        {
            Vector2 worldPos = GetMouseWorldPosition();
            foreach (var selectable in selectedFleets)
            {
                FleetMovement movement = selectable.GetComponent<FleetMovement>();
                if (movement != null)
                {
                    movement.SetDestination(worldPos);
                }
            }
            isWaitingForMoveTarget = false;
            Debug.Log("移動命令を発令しました。");
            
            // 命令後に選択を解除
            DeselectAll();
        }

        /// <summary>
        /// 移動先指定をキャンセルします。
        /// </summary>
        private void CancelMoveTargetSelection()
        {
            isWaitingForMoveTarget = false;
            Debug.Log("移動命令をキャンセルしました。");
        }

        /// <summary>
        /// クリック位置のオブジェクトを選択します。
        /// </summary>
        private void HandleSelection()
        {
            Vector2 worldPos = GetMouseWorldPosition();
            Collider2D collider = Physics2D.OverlapPoint(worldPos);

            // 全ての選択を一旦解除
            DeselectAll();

            if (collider != null)
            {
                Selectable selectable = collider.GetComponent<Selectable>();
                if (selectable != null)
                {
                    SelectFleet(selectable);
                }
            }
        }

        /// <summary>
        /// 特定の艦隊を選択状態にします。
        /// </summary>
        public void SelectFleet(Selectable selectable)
        {
            if (selectable != null && !selectedFleets.Contains(selectable))
            {
                selectable.SetSelected(true);
                selectedFleets.Add(selectable);
            }
        }

        /// <summary>
        /// 全ての選択を解除します。
        /// </summary>
        public void DeselectAll()
        {
            foreach (var selectable in selectedFleets)
            {
                if (selectable != null)
                {
                    selectable.SetSelected(false);
                }
            }
            selectedFleets.Clear();
        }

        /// <summary>
        /// コマンドメニューを開きます。
        /// </summary>
        private void OpenCommandMenu()
        {
            CommandMenu menu = Object.FindAnyObjectByType<CommandMenu>();
            if (menu != null)
            {
                menu.OpenMenu(Mouse.current.position.ReadValue());
            }
        }

        /// <summary>
        /// マウスのスクリーン座標をワールド座標に変換します。
        /// </summary>
        public Vector2 GetMouseWorldPosition()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (mainCamera == null) return Vector2.zero;

            float distanceToPlane = -mainCamera.transform.position.z;
            return mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, distanceToPlane));
        }
    }
}

