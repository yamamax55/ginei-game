using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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

        // 部隊グループ（#83・Alt＋数字）。グループ番号→割り当て艦隊。選択中ならそのグループへ割当、
        // 空なら呼び出して選択する（割当/呼出は同じキーで状況により切替＝GameInput の説明どおり）。
        private readonly Dictionary<int, List<Selectable>> controlGroups = new Dictionary<int, List<Selectable>>();

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

        [Tooltip("選択中の自艦隊のZOC（支配領域）を示す薄い円の色 #81")]
        public Color zocSelfColor = new Color(0.3f, 1f, 0.5f, 0.28f);          // 薄緑

        [Tooltip("攻撃目標指定中に敵艦隊のZOCを示す薄い円の色 #81（情報過多を避けこの時だけ表示）")]
        public Color zocEnemyColor = new Color(1f, 0.3f, 0.3f, 0.3f);          // 薄赤

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
        // 移動先プレビュー：選択した各艦隊ぶんの半透明ゴースト（複数選択で全艦隊分を表示）
        private readonly List<MovePreviewUnit> movePreviews = new List<MovePreviewUnit>();

        /// <summary>移動先プレビュー1艦隊ぶん（ゴースト＋選択群の重心からの相対位置）。</summary>
        private class MovePreviewUnit
        {
            public Selectable sel;
            public FormationPreview preview;   // Squadron が無ければ null
            public Vector2 offset;             // 選択群の重心からの相対位置（隊形維持用）
        }
        private bool isAiming = false;        // 右ボタン押下後、向き決め中か
        private Vector2 moveTargetPos;        // 右押下で確定した目標地点
        private float? aimAngle = null;       // 指定された向き（z角）。null=未指定
        private bool moveIsReverse = false;   // 後退モードでの移動先指定中か（向きは現在の向きを維持）
        private bool pendingAttackMove = false; // 移動指定フローを「アタックムーブ」として確定するか（#85）

        [Header("ドラッグ矩形選択 (#82)")]
        [Tooltip("クリックとドラッグを区別する最小移動量（ピクセル）")]
        public float dragSelectPixelThreshold = 8f;
        [Tooltip("クリック選択の許容半径（ピクセル）。コライダー直撃が無くても、この範囲内の最寄り自艦隊を選ぶ（小さな艦でも選びやすく）")]
        public float clickSelectPixelTolerance = 36f;
        [Tooltip("艦隊を選択したら自動でコマンドメニューを開く（選択直後に指示できる）")]
        public bool autoOpenMenuOnSelect = true;
        [Tooltip("選択矩形の枠の色")]
        public Color selectionBoxBorderColor = new Color(0.3f, 1f, 0.5f, 0.95f);
        [Tooltip("選択矩形の塗りの色")]
        public Color selectionBoxFillColor = new Color(0.3f, 1f, 0.5f, 0.12f);

        // ドラッグ矩形選択の状態（実行時）
        private bool leftPressedInWorld = false;   // 左押下がワールド上で始まったか（UI上で始まったら無視）
        private bool isBoxSelecting = false;       // ドラッグ矩形選択中か
        private Vector2 dragStartScreen;           // ドラッグ開始のスクリーン座標
        private GameObject selectionBoxCanvas;     // 選択矩形用 Canvas（実行時生成）
        private RectTransform selectionBoxRect;    // 選択矩形（半透明の塗り＝本体）

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

            // 部隊グループ（#83・Ctrl＋数字）はキーボードのみ＝マウス null でも処理する。
            HandleControlGroups();

            if (Mouse.current == null) return;

            // 左ボタン: クリック選択／ドラッグ矩形選択（#82。Shiftで追加選択）
            HandleLeftSelectionInput();

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
        public void StartWaitingForMoveTarget() => BeginMoveTargeting(false);

        /// <summary>
        /// 後退コマンドの目的地指定モードを開始します（向きは現在の向きを維持して下がる）。
        /// </summary>
        public void StartWaitingForReverseTarget() => BeginMoveTargeting(true);

        /// <summary>
        /// アタックムーブの目的地指定モードを開始します（#85）。移動指定フロー＆プレビューを流用し、
        /// 確定時に「進撃しつつ捕捉した敵と交戦する」標準命令を全選択艦隊へ与えます。
        /// </summary>
        public void StartWaitingForAttackMove()
        {
            BeginMoveTargeting(false);
            pendingAttackMove = true;
        }

        /// <summary>移動／後退の目的地指定モードを開始する共通処理。</summary>
        private void BeginMoveTargeting(bool reverse)
        {
            isWaitingForMoveTarget = true;
            moveIsReverse = reverse;
            pendingAttackMove = false;   // 既定は通常移動。アタックムーブは StartWaitingForAttackMove で上書き
            isAiming = false;
            aimAngle = null;
            ShowPreview();
            Debug.Log(reverse
                ? "後退：カーソルで位置→右クリックで確定（向き＝射界は現在のまま維持）。Escでキャンセル。"
                : "カーソルで位置→右クリック押下→押したままドラッグで向き→離して確定。Escでキャンセル。");
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

            // 選択中の艦隊：緑の円＋（ZOCを張れるなら）薄いZOC円
            foreach (var sel in selectedFleets)
            {
                if (sel == null) continue;
                Squadron sq = sel.GetComponent<Squadron>();
                if (sq == null) continue;
                DrawCircleForFleet(used++, sq, selectionCircleColor);

                FleetStrength fs = sel.GetComponent<FleetStrength>();
                if (fs != null)
                {
                    float zr = ZoneOfControl.GetRadius(fs);
                    if (zr > 0f)
                    {
                        sq.GetBoundingCircle(out Vector3 c, out _);
                        DrawCircleRadius(used++, c, zr, zocSelfColor);
                    }
                }
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

                        // 敵ZOC（薄赤）：攻撃目標指定中のみ表示（情報過多を避ける）
                        float zr = ZoneOfControl.GetRadius(ef);
                        if (zr > 0f)
                        {
                            sq.GetBoundingCircle(out Vector3 zc, out _);
                            DrawCircleRadius(used++, zc, zr, zocEnemyColor);
                        }
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
            sq.GetBoundingCircle(out Vector3 center, out float radius);
            DrawCircleRadius(index, center, radius, color);
        }

        /// <summary>中心・半径・色を直接指定して円を描く（ZOC円など外接円以外の半径用）。</summary>
        private void DrawCircleRadius(int index, Vector3 center, float radius, Color color)
        {
            LineRenderer lr = GetCircle(index);
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

            // 後退モード：向きは現在の向きのまま。位置だけ指定し、右クリックで即確定。
            // 後退できない前方（現在の向き側）にはプレビューを出さず、確定もしない。
            if (moveIsReverse)
            {
                bool canReverseHere = IsReversibleTarget(mouseWorld);
                if (canReverseHere)
                {
                    SetPreviewsPose(mouseWorld, DefaultFacingAngle());
                }
                else
                {
                    SetPreviewsActive(false);
                }
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && canReverseHere)
                {
                    ExecuteReverseCommand(mouseWorld);
                }
                return;
            }

            if (!isAiming)
            {
                // 押す前：カーソル位置に既定の向きで全プレビュー追従
                SetPreviewsPose(mouseWorld, DefaultFacingAngle());

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
                SetPreviewsPose(moveTargetPos, angle);

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
        /// 後退で到達できる目標か（前方＝現在の向き側でないか）を判定する。
        /// 前方成分が正なら後退できない（FleetMovement が前方成分を除去するため）。
        /// </summary>
        private bool IsReversibleTarget(Vector2 target)
        {
            if (selectedFleets.Count == 0 || selectedFleets[0] == null) return true;
            Vector2 pos = selectedFleets[0].transform.position;
            Vector2 up = selectedFleets[0].transform.up;
            return Vector2.Dot(target - pos, up) <= 0f;
        }

        /// <summary>
        /// 移動を実行します（到着時の向き指定があれば渡す）。
        /// </summary>
        private void ExecuteMoveCommand(Vector2 pos, float? facingAngleZ)
        {
            // 隊形を保つため、各艦隊は「目標地点＋重心からのオフセット」へ向かう（重なり防止）
            foreach (var mu in movePreviews)
            {
                if (mu.sel == null) continue;

                // 移動命令により攻撃目標を解除（追尾を止めて指定地点へ向かう）
                FleetWeapon weapon = mu.sel.GetComponent<FleetWeapon>();
                if (weapon != null) weapon.ClearManualTarget();

                if (pendingAttackMove)
                {
                    // アタックムーブ：継続命令コンポーネントに委ねる（進みつつ捕捉した敵と交戦）
                    FleetStandardOrder order = EnsureStandardOrder(mu.sel);
                    if (order != null) order.SetAttackMove(pos + mu.offset);
                }
                else
                {
                    // 通常移動：標準命令の stance は解除して指定地点へ
                    FleetStandardOrder order = mu.sel.GetComponent<FleetStandardOrder>();
                    if (order != null) order.ClearOrder();
                    FleetMovement movement = mu.sel.GetComponent<FleetMovement>();
                    if (movement != null) movement.SetDestination(pos + mu.offset, facingAngleZ);
                }
            }
            Debug.Log(pendingAttackMove ? "アタックムーブを発令しました。" : "移動命令を発令しました。");
            EndMoveTargeting();

            // 命令後に選択を解除
            DeselectAll();
        }

        /// <summary>
        /// 後退移動を実行します（向き＝射界を保ったまま目標地点へ下がる）。
        /// </summary>
        private void ExecuteReverseCommand(Vector2 pos)
        {
            foreach (var mu in movePreviews)
            {
                if (mu.sel == null) continue;

                // 後退中も追尾はさせない（手動目標の追尾は前進命令を出し後退と競合するため解除）。
                // 射界内の敵には FleetWeapon が自動で発砲を続ける＝戦いながら下がれる。
                FleetWeapon weapon = mu.sel.GetComponent<FleetWeapon>();
                if (weapon != null) weapon.ClearManualTarget();

                // 後退は手動の移動命令なので標準命令(アタックムーブ/保持)を解除
                FleetStandardOrder order = mu.sel.GetComponent<FleetStandardOrder>();
                if (order != null) order.ClearOrder();

                FleetMovement movement = mu.sel.GetComponent<FleetMovement>();
                if (movement != null) movement.SetReverseDestination(pos + mu.offset);
            }
            EndMoveTargeting();
            Debug.Log("後退命令を発令しました。");

            DeselectAll();
        }

        /// <summary>選択中の全艦隊を停止させる（標準命令解除＋追尾解除＋その場で停止）。#85</summary>
        public void StopSelected()
        {
            foreach (var sel in selectedFleets)
            {
                if (sel == null) continue;
                FleetStandardOrder order = sel.GetComponent<FleetStandardOrder>();
                if (order != null) order.ClearOrder();
                FleetWeapon weapon = sel.GetComponent<FleetWeapon>();
                if (weapon != null) weapon.ClearManualTarget();
                FleetMovement movement = sel.GetComponent<FleetMovement>();
                if (movement != null) movement.Stop();
            }
            Debug.Log("停止命令を発令しました。");
        }

        /// <summary>選択中の全艦隊に「その場保持」を命じる（移動せず射界内の敵に自動発砲）。#85</summary>
        public void HoldSelected()
        {
            foreach (var sel in selectedFleets)
            {
                if (sel == null) continue;
                FleetStandardOrder order = EnsureStandardOrder(sel);
                if (order != null) order.SetHold();
            }
            Debug.Log("その場保持を発令しました。");
        }

        /// <summary>艦隊に FleetStandardOrder（標準命令の継続処理）を保証して返す。#85</summary>
        private FleetStandardOrder EnsureStandardOrder(Selectable sel)
        {
            if (sel == null) return null;
            FleetStandardOrder order = sel.GetComponent<FleetStandardOrder>();
            if (order == null) order = sel.gameObject.AddComponent<FleetStandardOrder>();
            return order;
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
            moveIsReverse = false;
            pendingAttackMove = false;
            isAiming = false;
            aimAngle = null;
            ClearMovePreviews();
        }

        /// <summary>選択中の全艦隊ぶんの移動先プレビューを構築・表示する（重心からの相対位置を保持）。</summary>
        private void ShowPreview()
        {
            ClearMovePreviews();
            if (selectedFleets.Count == 0) return;

            // 選択群の重心（null は除外）
            Vector2 centroid = Vector2.zero;
            int n = 0;
            foreach (var s in selectedFleets)
            {
                if (s == null) continue;
                centroid += (Vector2)s.transform.position;
                n++;
            }
            if (n == 0) return;
            centroid /= n;

            // 各艦隊ぶんのゴーストを生成し、重心からの相対位置を記録
            foreach (var s in selectedFleets)
            {
                if (s == null) continue;
                MovePreviewUnit mu = new MovePreviewUnit
                {
                    sel = s,
                    offset = (Vector2)s.transform.position - centroid
                };
                Squadron squad = s.GetComponent<Squadron>();
                if (squad != null)
                {
                    GameObject go = new GameObject("FormationPreview");
                    mu.preview = go.AddComponent<FormationPreview>();
                    mu.preview.Show(squad);
                }
                movePreviews.Add(mu);
            }
        }

        /// <summary>全プレビューを「目標地点＋各オフセット」に配置し、向きを揃える。</summary>
        private void SetPreviewsPose(Vector2 target, float angleZ)
        {
            foreach (var mu in movePreviews)
            {
                if (mu.preview == null) continue;
                if (mu.sel == null) { mu.preview.Hide(); continue; }   // 破棄された艦のゴーストは隠す
                if (!mu.preview.gameObject.activeSelf) mu.preview.gameObject.SetActive(true);
                mu.preview.SetPose(target + mu.offset, angleZ);
            }
        }

        /// <summary>全プレビューの表示/非表示を一括切替（後退不可地点などで使う）。</summary>
        private void SetPreviewsActive(bool on)
        {
            foreach (var mu in movePreviews)
            {
                if (mu.preview != null) mu.preview.gameObject.SetActive(on);
            }
        }

        /// <summary>移動先プレビューを全破棄してリストを空にする（堅牢化：残骸を残さない）。</summary>
        private void ClearMovePreviews()
        {
            foreach (var mu in movePreviews)
            {
                if (mu != null && mu.preview != null) Destroy(mu.preview.gameObject);
            }
            movePreviews.Clear();
        }

        /// <summary>
        /// クリック位置のオブジェクトを選択します。
        /// </summary>
        private void HandleSelection(bool additive)
        {
            Vector2 worldPos = GetMouseWorldPosition();
            Collider2D collider = Physics2D.OverlapPoint(worldPos);

            // 追加選択(Shift)でなければ一旦すべて解除
            if (!additive) DeselectAll();

            // まずコライダー直撃を判定（配下艦を撃っても親までさかのぼり旗艦の Selectable を取得）。
            Selectable selectable = (collider != null) ? collider.GetComponentInParent<Selectable>() : null;

            // 直撃が無ければ、クリック近傍（許容ピクセル内）で最寄りの自艦隊を選ぶ。
            // 小さな三角の艦でもピクセル単位の精密クリックを要求しない＝選びやすくする。
            if (selectable == null)
            {
                selectable = FindNearestSelectableFlagship(clickSelectPixelTolerance);
            }

            if (selectable != null)
            {
                SelectFleet(selectable);
            }
        }

        /// <summary>
        /// クリック位置（スクリーン）から許容ピクセル内で最寄りの「プレイヤーが操作できる旗艦」を返す。
        /// コライダー直撃が無い時のフォールバック。AI/敵艦は直撃クリックでのみ選ぶ（誤選択を防ぐ）。
        /// </summary>
        private Selectable FindNearestSelectableFlagship(float pixelTolerance)
        {
            if (mainCamera == null || Mouse.current == null) return null;
            Vector2 clickScreen = Mouse.current.position.ReadValue();

            Selectable best = null;
            float bestDist = pixelTolerance;
            foreach (FleetStrength fs in FleetRegistry.AllFlagships)
            {
                if (fs == null) continue;

                FleetAI ai = fs.GetComponent<FleetAI>();
                if (ai != null && ai.enabled) continue;   // プレイヤー操作艦のみ（AI/敵は対象外）

                Selectable sel = fs.GetComponent<Selectable>();
                if (sel == null) continue;

                Vector3 sp = mainCamera.WorldToScreenPoint(fs.transform.position);
                if (sp.z < 0f) continue;                   // カメラ後方は除外

                float d = Vector2.Distance(new Vector2(sp.x, sp.y), clickScreen);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = sel;
                }
            }
            return best;
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
        /// 選択中の全艦隊の陣形を変更します（陣形変更ロジックの実体・唯一の窓口）。
        /// CommandMenu / FleetHUDManager はここに委譲する。
        /// </summary>
        /// <param name="formationIdx">Formation enum のインデックス</param>
        public void ChangeFormation(int formationIdx)
        {
            Formation f = (Formation)formationIdx;
            foreach (var selectable in selectedFleets)
            {
                if (selectable == null) continue;
                Squadron sq = selectable.GetComponent<Squadron>();
                if (sq != null) sq.currentFormation = f;
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

        // ===== 部隊グループ（#83・Ctrl＋数字）=====

        /// <summary>
        /// 部隊グループの入力処理（Alt＋1/2/3）。選択中の艦隊があればそのグループへ割り当て、
        /// 無ければ既存グループを呼び出して選択する（割り当て/呼び出しは同じキーで状況により切替）。
        /// バインドは <see cref="GameInput"/>（会戦コンテキスト・Alt修飾で倍速・Unityエディタの Ctrl＋数字と分離）。
        /// </summary>
        private void HandleControlGroups()
        {
            if (GameInput.WasPressed(GameAction.グループ選択1)) ToggleControlGroup(1);
            else if (GameInput.WasPressed(GameAction.グループ選択2)) ToggleControlGroup(2);
            else if (GameInput.WasPressed(GameAction.グループ選択3)) ToggleControlGroup(3);
        }

        /// <summary>選択中なら割り当て、空なら呼び出し。</summary>
        private void ToggleControlGroup(int group)
        {
            if (selectedFleets.Count > 0) AssignControlGroup(group);
            else RecallControlGroup(group);
        }

        /// <summary>現在の選択をグループ番号へ割り当てる（上書き）。</summary>
        private void AssignControlGroup(int group)
        {
            var members = new List<Selectable>();
            foreach (var sel in selectedFleets)
                if (sel != null && !members.Contains(sel)) members.Add(sel);
            controlGroups[group] = members;
            ShowGroupMessage($"グループ {group} に {members.Count} 艦隊を割り当て");
        }

        /// <summary>グループ番号の艦隊を呼び出して選択する（破棄・退却した艦は除外）。</summary>
        private void RecallControlGroup(int group)
        {
            if (!controlGroups.TryGetValue(group, out var members) || members == null) return;

            DeselectAll();
            int n = 0;
            foreach (var sel in members)
            {
                if (sel == null) continue;                  // 破棄済み（Unity の擬似null）は除外
                FleetStrength fs = sel.GetComponent<FleetStrength>();
                if (fs != null && !fs.IsAlive) continue;    // 退却・撃沈した艦隊は呼び出さない
                SelectFleet(sel);
                n++;
            }
            if (n > 0)
            {
                ShowGroupMessage($"グループ {group} を呼び出し（{n} 艦隊）");
                if (autoOpenMenuOnSelect) OpenCommandMenu();
            }
        }

        /// <summary>HUD へグループ操作の通知を出す（無ければログのみ）。</summary>
        private void ShowGroupMessage(string msg)
        {
            FleetHUDManager hud = Object.FindAnyObjectByType<FleetHUDManager>();
            if (hud != null) hud.ShowMessage(msg, 1.5f);
            Debug.Log(msg);
        }

        /// <summary>
        /// コマンドメニューを開きます。
        /// </summary>
        private void OpenCommandMenu()
        {
            if (Mouse.current == null) return;
            CommandMenu menu = Object.FindAnyObjectByType<CommandMenu>();
            if (menu != null)
            {
                menu.OpenMenu(Mouse.current.position.ReadValue());
            }
        }

        // ===== ドラッグ矩形選択（#82）=====

        /// <summary>
        /// 左ボタン入力を処理。短く押せばクリック選択、ドラッグすれば矩形選択。
        /// Shift 押下中は既存選択に追加する。UI 上で押し始めた場合は無視。
        /// </summary>
        private void HandleLeftSelectionInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // 押下：開始点を記録（UI 上なら無視）
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (IsPointerOverUI())
                {
                    leftPressedInWorld = false;
                    return;
                }
                leftPressedInWorld = true;
                isBoxSelecting = false;
                dragStartScreen = mouse.position.ReadValue();
            }
            // 押し続け：閾値を超えたら矩形選択に移行し、枠を更新
            else if (leftPressedInWorld && mouse.leftButton.isPressed)
            {
                Vector2 cur = mouse.position.ReadValue();
                if (!isBoxSelecting &&
                    (cur - dragStartScreen).sqrMagnitude >= dragSelectPixelThreshold * dragSelectPixelThreshold)
                {
                    isBoxSelecting = true;
                }
                if (isBoxSelecting) UpdateSelectionBoxVisual(dragStartScreen, cur);
            }
            // 離す：矩形選択 or クリック選択を確定
            else if (leftPressedInWorld && mouse.leftButton.wasReleasedThisFrame)
            {
                leftPressedInWorld = false;
                bool additive = IsShiftHeld();
                if (isBoxSelecting)
                {
                    BoxSelect(dragStartScreen, mouse.position.ReadValue(), additive);
                    isBoxSelecting = false;
                    HideSelectionBoxVisual();
                }
                else
                {
                    HandleSelection(additive);
                }

                // 選択が成立したら（1隻以上）コマンドメニューを自動で開く＝選択直後に指示できる。
                if (autoOpenMenuOnSelect && selectedFleets.Count > 0) OpenCommandMenu();
            }
        }

        /// <summary>
        /// スクリーン矩形内の「プレイヤーが操作できる艦隊」をすべて選択する。
        /// additive=false なら既存選択を解除してから選び直す。
        /// </summary>
        private void BoxSelect(Vector2 aScreen, Vector2 bScreen, bool additive)
        {
            if (mainCamera == null) return;
            if (!additive) DeselectAll();

            Rect rect = ScreenRect(aScreen, bScreen);
            // 全旗艦から、プレイヤー操作艦（FleetAI が無効）で矩形内のものを選択
            foreach (FleetStrength fs in FleetRegistry.AllFlagships)
            {
                if (fs == null) continue;

                FleetAI ai = fs.GetComponent<FleetAI>();
                if (ai != null && ai.enabled) continue;   // AI/敵 は対象外

                Selectable sel = fs.GetComponent<Selectable>();
                if (sel == null) continue;

                Vector3 sp = mainCamera.WorldToScreenPoint(fs.transform.position);
                if (sp.z < 0f) continue;                    // カメラ後方は除外
                if (rect.Contains(new Vector2(sp.x, sp.y)))
                {
                    SelectFleet(sel);
                }
            }
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b)
        {
            return new Rect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        private static bool IsShiftHeld()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        private static bool IsPointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // ----- 選択矩形のビジュアル（スクリーン空間オーバーレイ・実行時生成）-----

        private void EnsureSelectionBoxVisual()
        {
            if (selectionBoxCanvas != null) return;

            selectionBoxCanvas = new GameObject("DragSelectCanvas");
            Canvas canvas = selectionBoxCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;   // 通常UIより手前（クリックは奪わない＝Raycaster なし）

            // 本体＝半透明の塗りのみ（中の艦隊が透けて見えるよう、不透明な面で覆わない）。
            // 左下アンカー＝スクリーンピクセルでそのまま配置。
            selectionBoxRect = NewBox(selectionBoxCanvas.transform, "Box", selectionBoxFillColor);
            selectionBoxRect.anchorMin = Vector2.zero;
            selectionBoxRect.anchorMax = Vector2.zero;
            selectionBoxRect.pivot = Vector2.zero;

            // 枠＝四辺の細い線だけ（不透明な面で中を覆わない）
            AddEdge(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));   // 上
            AddEdge(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));   // 下
            AddEdge(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));   // 左
            AddEdge(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f));   // 右

            selectionBoxCanvas.SetActive(false);
        }

        /// <summary>選択矩形の枠（細い辺）を1本生成。size の 0 成分はアンカーに追従、非0 成分が線の太さ。</summary>
        private void AddEdge(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            RectTransform e = NewBox(selectionBoxRect, "Edge", selectionBoxBorderColor);
            e.anchorMin = anchorMin;
            e.anchorMax = anchorMax;
            e.pivot = pivot;
            e.anchoredPosition = Vector2.zero;
            e.sizeDelta = size;
        }

        private static RectTransform NewBox(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RawImage img = go.GetComponent<RawImage>();
            img.texture = Texture2D.whiteTexture;
            img.color = color;
            img.raycastTarget = false;
            return go.GetComponent<RectTransform>();
        }

        private void UpdateSelectionBoxVisual(Vector2 aScreen, Vector2 bScreen)
        {
            EnsureSelectionBoxVisual();
            if (!selectionBoxCanvas.activeSelf) selectionBoxCanvas.SetActive(true);
            Rect r = ScreenRect(aScreen, bScreen);
            selectionBoxRect.anchoredPosition = new Vector2(r.xMin, r.yMin);
            selectionBoxRect.sizeDelta = new Vector2(r.width, r.height);
        }

        private void HideSelectionBoxVisual()
        {
            if (selectionBoxCanvas != null) selectionBoxCanvas.SetActive(false);
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

