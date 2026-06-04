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

        [Header("艦隊円表示")]
        [Tooltip("艦隊を囲う円の線幅")]
        public float circleWidth = 0.15f;

        [Tooltip("選択中の艦隊を囲う円の色")]
        public Color selectionCircleColor = new Color(0.3f, 1f, 0.5f, 0.9f);   // 緑

        [Tooltip("攻撃目標候補（敵艦隊）を囲う円の色")]
        public Color targetCircleColor = new Color(1f, 0.9f, 0.2f, 0.9f);      // 黄

        [Tooltip("攻撃目標選択時、カーソルを合わせた敵艦隊を囲う円の色")]
        public Color targetHoverColor = new Color(1f, 0.45f, 0.1f, 1f);        // 橙

        private bool isWaitingForMoveTarget = false;
        private bool isWaitingForAttackTarget = false;

        // 艦隊円（実行時生成・複数同時表示できるプール）
        private readonly List<LineRenderer> circlePool = new List<LineRenderer>();
        private Material circleMaterial;
        private const int CircleSegments = 48;

        // 攻撃目標指定中にカーソルを合わせている敵艦隊（ハイライト用）
        private Squadron hoveredAttackFleet;

        // 攻撃種別メニュー用の保留対象（右クリックで対象決定→メニューで通常/ミサイルを選ぶ）
        private Squadron pendingAttackFleet;
        private string pendingAttackName;

        // 移動先決定の状態
        private FormationPreview preview;
        private bool isAiming = false;        // 右ボタン押下後、向き決め中か
        private Vector2 moveTargetPos;        // 右押下で確定した目標地点
        private float? aimAngle = null;       // 指定された向き（z角）。null=未指定

        /// <summary>移動先指定待ちか（Escの優先処理判定用）。</summary>
        public bool IsWaitingForMoveTarget => isWaitingForMoveTarget;

        /// <summary>攻撃目標（敵旗艦）指定待ちか（Escの優先処理判定用）。</summary>
        public bool IsWaitingForAttackTarget => isWaitingForAttackTarget;

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

            // 攻撃目標（敵旗艦）指定待ちの状態
            if (isWaitingForAttackTarget)
            {
                HandleAttackTargeting();
                return;
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

        private void LateUpdate()
        {
            // 移動・追従後の最新位置で艦隊円を描画（選択艦隊＋攻撃目標候補）
            UpdateFleetCircles();
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
        /// 攻撃目標（敵旗艦）の指定モードを開始します。
        /// この後の左クリックで敵旗艦を指定すると、選択中の全部隊がその旗艦を狙います。
        /// </summary>
        public void StartWaitingForAttackTarget()
        {
            if (selectedFleets.Count == 0) return;
            isWaitingForAttackTarget = true;
            Debug.Log("攻撃目標の敵艦隊を選択（カーソルで円が強調）。左クリック=通常攻撃／右クリック=攻撃種別メニュー。Escでキャンセル。");
        }

        /// <summary>
        /// 攻撃目標指定モードの入力処理。カーソル下の敵艦隊を円で強調し、
        /// 左クリックで通常攻撃を即時発令、右クリックで攻撃種別メニュー（通常/ミサイル）を開く。
        /// </summary>
        private void HandleAttackTargeting()
        {
            // Escでキャンセル
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                EndAttackTargeting();
                Debug.Log("攻撃命令をキャンセルしました。");
                return;
            }

            if (Mouse.current == null) return;

            // カーソル下の敵艦隊を判定（旗艦・配下艦どちらでも親の艦隊に解決）
            Squadron hoverFleet = null;
            FleetStrength hoverFlag = null;
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!overUI)
            {
                Collider2D collider = Physics2D.OverlapPoint(GetMouseWorldPosition());
                if (collider != null)
                {
                    hoverFleet = collider.GetComponentInParent<Squadron>();
                    hoverFlag = collider.GetComponentInParent<FleetStrength>();
                }
            }

            bool validEnemy = IsValidEnemyTarget(hoverFlag) && hoverFleet != null;

            // カーソル下の敵艦隊を記録（円のハイライトは LateUpdate の UpdateFleetCircles が描画）
            hoveredAttackFleet = validEnemy ? hoverFleet : null;

            // 左クリック：通常攻撃を即時発令
            if (Mouse.current.leftButton.wasPressedThisFrame && !overUI)
            {
                if (validEnemy)
                {
                    ConfirmAttack(hoverFleet, hoverFlag.admiralName, false);
                    EndAttackTargeting();
                }
                else
                {
                    Debug.Log("攻撃目標は敵艦隊を指定してください（Escでキャンセル）。");
                }
            }
            // 右クリック：攻撃種別メニュー（通常/ミサイル）を開く
            else if (Mouse.current.rightButton.wasPressedThisFrame && !overUI)
            {
                if (validEnemy)
                {
                    pendingAttackFleet = hoverFleet;
                    pendingAttackName = hoverFlag.admiralName;
                    EndAttackTargeting();

                    CommandMenu menu = Object.FindAnyObjectByType<CommandMenu>();
                    if (menu != null) menu.OpenAttackTypeMenu(Mouse.current.position.ReadValue());
                    else ConfirmPendingAttack(false); // メニューが無ければ通常攻撃にフォールバック
                }
                else
                {
                    Debug.Log("攻撃目標は敵艦隊を指定してください（Escでキャンセル）。");
                }
            }
        }

        /// <summary>選択中部隊の敵で、生存している攻撃可能な旗艦か。</summary>
        private bool IsValidEnemyTarget(FleetStrength targetFlag)
        {
            if (targetFlag == null || !targetFlag.IsAlive) return false;
            if (selectedFleets.Count == 0 || selectedFleets[0] == null) return false;
            FleetStrength myStr = selectedFleets[0].GetComponent<FleetStrength>();
            return myStr != null && FactionRelations.IsHostile(myStr, targetFlag);
        }

        /// <summary>攻撃目標指定モードを終了する（円の表示は LateUpdate が更新）。</summary>
        private void EndAttackTargeting()
        {
            isWaitingForAttackTarget = false;
            hoveredAttackFleet = null;
        }

        // ----- 艦隊円（選択艦隊＋攻撃目標候補を丸で囲う。実行時生成の LineRenderer プール）-----

        /// <summary>
        /// 選択中の艦隊（常時）と、攻撃目標指定中の敵艦隊（マウスオーバー有無に関わらず全て）を円で囲う。
        /// </summary>
        private void UpdateFleetCircles()
        {
            int used = 0;

            // 選択中の艦隊：緑の円
            foreach (var sel in selectedFleets)
            {
                if (sel == null) continue;
                Squadron sq = sel.GetComponent<Squadron>();
                if (sq == null) continue;
                DrawCircleForFleet(used++, sq, selectionCircleColor);
            }

            // 攻撃目標指定中：全ての敵艦隊を円で表示（カーソル下は橙、他は黄）
            if (isWaitingForAttackTarget && selectedFleets.Count > 0 && selectedFleets[0] != null)
            {
                FleetStrength myStr = selectedFleets[0].GetComponent<FleetStrength>();
                if (myStr != null)
                {
                    // 全旗艦から敵対する勢力の旗艦のみを円で表示（多勢力対応）
                    IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
                    for (int i = 0; i < flagships.Count; i++)
                    {
                        FleetStrength ef = flagships[i];
                        if (ef == null || !ef.IsAlive) continue;
                        if (!FactionRelations.IsHostile(myStr, ef)) continue;
                        Squadron sq = ef.GetComponent<Squadron>();
                        if (sq == null) continue;
                        Color c = (sq == hoveredAttackFleet) ? targetHoverColor : targetCircleColor;
                        DrawCircleForFleet(used++, sq, c);
                    }
                }
            }

            // 使わなかったプールは消す
            for (int i = used; i < circlePool.Count; i++)
            {
                if (circlePool[i] != null) circlePool[i].enabled = false;
            }
        }

        private void DrawCircleForFleet(int index, Squadron sq, Color color)
        {
            LineRenderer lr = GetCircle(index);
            sq.GetBoundingCircle(out Vector3 center, out float radius);

            lr.startColor = color;
            lr.endColor = color;
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = (2f * Mathf.PI * i) / CircleSegments;
                lr.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(a) * radius,
                    center.y + Mathf.Sin(a) * radius,
                    center.z));
            }
            lr.enabled = true;
        }

        /// <summary>プールから index 番目の円 LineRenderer を取得（足りなければ生成）。</summary>
        private LineRenderer GetCircle(int index)
        {
            while (circlePool.Count <= index)
            {
                circlePool.Add(CreateCircleRenderer());
            }
            return circlePool[index];
        }

        private LineRenderer CreateCircleRenderer()
        {
            if (circleMaterial == null)
            {
                // 白マテリアル＋頂点色で各円の色を出し分ける（マテリアルは共有）
                circleMaterial = new Material(Shader.Find("Sprites/Default"));
                circleMaterial.color = Color.white;
            }

            GameObject go = new GameObject("FleetCircle");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = CircleSegments;
            lr.startWidth = circleWidth;
            lr.endWidth = circleWidth;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.sortingOrder = 30;
            lr.material = circleMaterial;
            lr.enabled = false;
            return lr;
        }

        private void OnDestroy()
        {
            // 実行時生成したマテリアルを破棄（リーク防止）
            if (circleMaterial != null) Destroy(circleMaterial);
        }

        /// <summary>
        /// 選択中の全部隊に対し、指定した敵「艦隊全体」を攻撃目標に設定し、追尾・交戦させます。
        /// useMissile=true でミサイル攻撃（残弾がある間のみ。切れたら通常攻撃に移行）。
        /// </summary>
        private void ConfirmAttack(Squadron targetFleet, string targetName, bool useMissile)
        {
            foreach (var selectable in selectedFleets)
            {
                if (selectable == null) continue;

                // 旗艦ではなく艦隊全体を標的に。接近は FleetWeapon の追尾が行う。
                FleetWeapon weapon = selectable.GetComponent<FleetWeapon>();
                if (weapon != null)
                {
                    weapon.SetManualTargetFleet(targetFleet);
                    weapon.SetMissileMode(useMissile);
                }
            }

            // 攻撃対象を画面に一定時間表示
            string kind = useMissile ? "ミサイル攻撃" : "通常攻撃";
            FleetHUDManager hud = Object.FindAnyObjectByType<FleetHUDManager>();
            if (hud != null) hud.ShowMessage($"攻撃対象：{targetName}艦隊（{kind}）", 2.5f);

            Debug.Log($"攻撃命令を発令しました（目標艦隊: {targetName} / {kind}）。");

            DeselectAll();
        }

        /// <summary>
        /// 攻撃種別メニューから呼ばれ、保留中の対象艦隊へ通常/ミサイル攻撃を発令する。
        /// </summary>
        public void ConfirmPendingAttack(bool useMissile)
        {
            if (pendingAttackFleet != null)
            {
                ConfirmAttack(pendingAttackFleet, pendingAttackName, useMissile);
            }
            pendingAttackFleet = null;
            pendingAttackName = null;
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

                // 移動命令により攻撃目標を解除（追尾を止めて指定地点へ向かう）
                FleetWeapon weapon = selectable.GetComponent<FleetWeapon>();
                if (weapon != null) weapon.ClearManualTarget();

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

