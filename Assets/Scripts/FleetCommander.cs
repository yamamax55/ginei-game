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

        [Tooltip("向き指定（エイム）と判定する最小ドラッグ距離（ワールド単位）")]
        public float aimDragThreshold = 0.5f;

        private bool isWaitingForMoveTarget = false;

        // 移動先決定の状態
        private FormationPreview preview;
        private bool isAiming = false;        // 右ボタン押下後、向き決め中か
        private Vector2 moveTargetPos;        // 右押下で確定した目標地点
        private float? aimAngle = null;       // 指定された向き（z角）。null=未指定

        /// <summary>移動先指定待ちか（Escの優先処理判定用）。</summary>
        public bool IsWaitingForMoveTarget => isWaitingForMoveTarget;

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
                HandleMoveTargeting();
                return; // 選択モードの時は通常の選択処理は行わない
            }

            if (Mouse.current == null) return;

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
            isAiming = false;
            aimAngle = null;
            ShowPreview();
            Debug.Log("カーソルで位置→右クリック押下→押したままドラッグで向き→離して確定。Escでキャンセル。");
        }

        /// <summary>
        /// 移動先決定モードの入力処理。
        /// カーソルで位置→右押下で目標確定＆向き決め開始→ドラッグで向き→離して確定。
        /// </summary>
        private void HandleMoveTargeting()
        {
            // Escでキャンセル
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelMoveTargetSelection();
                return;
            }

            Vector2 mouseWorld = GetMouseWorldPosition();

            if (!isAiming)
            {
                // 押す前：カーソル位置に既定の向きでプレビュー追従
                if (preview != null) preview.SetPose(mouseWorld, DefaultFacingAngle());

                // 右ボタン押下：押した位置を目標地点として確定し、向き決め開始
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                {
                    moveTargetPos = mouseWorld;
                    aimAngle = null;
                    isAiming = true;
                }
            }
            else
            {
                // 右ボタン保持中：目標→カーソル方向で向きを決める（一定以上ドラッグしたら）
                Vector2 dir = mouseWorld - moveTargetPos;
                if (dir.magnitude > aimDragThreshold)
                {
                    aimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                }

                float angle = aimAngle ?? DefaultFacingAngle();
                if (preview != null) preview.SetPose(moveTargetPos, angle);

                // 離したら確定（ドラッグ無し＝向き指定なし）
                if (Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame)
                {
                    ExecuteMoveCommand(moveTargetPos, aimAngle);
                }
            }
        }

        /// <summary>選択先頭の艦隊の現在の向き（既定プレビュー角度）。</summary>
        private float DefaultFacingAngle()
        {
            if (selectedFleets.Count > 0 && selectedFleets[0] != null)
                return selectedFleets[0].transform.eulerAngles.z;
            return 0f;
        }

        /// <summary>
        /// 移動を実行します（到着時の向き指定があれば渡す）。
        /// </summary>
        private void ExecuteMoveCommand(Vector2 pos, float? facingAngleZ)
        {
            foreach (var selectable in selectedFleets)
            {
                if (selectable == null) continue;
                FleetMovement movement = selectable.GetComponent<FleetMovement>();
                if (movement != null) movement.SetDestination(pos, facingAngleZ);
            }
            EndMoveTargeting();
            Debug.Log("移動命令を発令しました。");

            // 命令後に選択を解除
            DeselectAll();
        }

        /// <summary>
        /// 移動先指定をキャンセルします。
        /// </summary>
        private void CancelMoveTargetSelection()
        {
            EndMoveTargeting();
            Debug.Log("移動命令をキャンセルしました。");
        }

        /// <summary>移動先決定モードを終了し、プレビューを隠す。</summary>
        private void EndMoveTargeting()
        {
            isWaitingForMoveTarget = false;
            isAiming = false;
            aimAngle = null;
            if (preview != null) preview.Hide();
        }

        /// <summary>選択先頭部隊の陣形でプレビューを構築・表示する。</summary>
        private void ShowPreview()
        {
            if (selectedFleets.Count == 0 || selectedFleets[0] == null) return;
            Squadron squad = selectedFleets[0].GetComponent<Squadron>();
            if (squad == null) return;

            if (preview == null)
            {
                GameObject go = new GameObject("FormationPreview");
                preview = go.AddComponent<FormationPreview>();
            }
            preview.Show(squad);
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
                // 配下艦(EscortShip)にもコライダーがあるため、親までさかのぼって旗艦の Selectable を取得する。
                // （配下艦は個別選択しない＝クリックすると所属旗艦が選択される）
                Selectable selectable = collider.GetComponentInParent<Selectable>();
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
            if (Mouse.current == null || mainCamera == null) return Vector2.zero;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            float distanceToPlane = -mainCamera.transform.position.z;
            return mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, distanceToPlane));
        }
    }
}

