using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Ginei
{
    /// <summary>
    /// 右クリックで表示されるコマンドメニューを管理するクラス。
    /// 状況に応じたコマンド（移動、攻撃、陣形変更など）を提供します。
    /// </summary>
    public class CommandMenu : MonoBehaviour
    {
        [Header("参照")]
        public FleetCommander commander;
        public GameObject menuRoot; // メニューの親パネル
        public GameObject buttonPrefab; // ボタンのプレハブ（または雛形）
        public RectTransform menuRect;

        [Header("サブメニュー（陣形用）")]
        public GameObject formationSubMenu;

        /// <summary>メニューが開いているか（Escの優先処理判定用）。</summary>
        public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

        private Vector2 lastClickWorldPos;
        private Selectable lastClickedFleet;
        private bool corpsFormationMode; // 陣形サブメニューを軍団モードで使うか（軍団陣形）

        // 陣形サブメニューの配置制御用（Start でキャッシュ）
        private RectTransform formationSubRect;
        private LayoutElement formationSubLayout;

        private void Start()
        {
            if (commander == null) commander = Object.FindAnyObjectByType<FleetCommander>();
            BuildFormationButtons();
            CacheFormationSubMenu();
            CloseMenu();
        }

        /// <summary>
        /// 陣形サブメニューを親（メインメニュー）のレイアウトグループから除外する。
        /// これをしないと VerticalLayoutGroup がサブメニューを縦積みの一員として扱い、
        /// メインメニューのボタンと重なる（陣形メニュー重なり対策）。
        /// </summary>
        private void CacheFormationSubMenu()
        {
            if (formationSubMenu == null) return;
            formationSubRect = formationSubMenu.GetComponent<RectTransform>();
            formationSubLayout = formationSubMenu.GetComponent<LayoutElement>();
            if (formationSubLayout == null) formationSubLayout = formationSubMenu.AddComponent<LayoutElement>();
            formationSubLayout.ignoreLayout = true; // 親レイアウトに巻き込ませない（重なり防止）
        }

        /// <summary>
        /// 陣形サブメニューのボタンを Formation enum から動的生成する。
        /// enum の変更（種類・順序）に自動追従し、シーン側の手配線に依存しない。
        /// </summary>
        private void BuildFormationButtons()
        {
            if (formationSubMenu == null || buttonPrefab == null) return;

            // 既存の子（手配線のボタン等）を除去（buttonPrefab 自体は除く）
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform child in formationSubMenu.transform)
            {
                if (child.gameObject != buttonPrefab) toDestroy.Add(child.gameObject);
            }
            foreach (var obj in toDestroy)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(obj);
                else Destroy(obj);
#else
                Destroy(obj);
#endif
            }

            string[] names = System.Enum.GetNames(typeof(Formation));
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i; // クロージャ用にキャプチャ
                GameObject btnObj = Instantiate(buttonPrefab, formationSubMenu.transform);
                btnObj.SetActive(true);

                TextMeshProUGUI textComp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = names[idx];
                    if (textComp.font == null) textComp.font = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
                }

                Button btnComp = btnObj.GetComponent<Button>();
                if (btnComp != null) btnComp.onClick.AddListener(() => ChangeFormation(idx));
            }
        }

        private void Update()
        {
            // メニュー外クリックまたはEscで閉じる
            if (menuRoot.activeSelf)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseMenu();
                }

                // 左クリックでメニュー外を触ったら閉じる
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    {
                        CloseMenu();
                    }
                }
            }
        }

        /// <summary>
        /// 指定されたスクリーン座標にメニューを開きます。
        /// </summary>
        public void OpenMenu(Vector2 screenPos)
        {
            lastClickWorldPos = commander.GetMouseWorldPosition();
            Collider2D collider = Physics2D.OverlapPoint(lastClickWorldPos);
            lastClickedFleet = collider != null ? collider.GetComponent<Selectable>() : null;

            SetupMenuOptions();

            menuRoot.SetActive(true);
            
            // レイアウトを即座に更新してサイズを確定させる
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuRect);
            
            // ピボットを左上に設定 (マウス位置がメニューの左上になるように)
            menuRect.pivot = new Vector2(0, 1);

            // Screen Space - Overlay では transform.position はスクリーン座標（ピクセル）と一致する
            menuRect.position = screenPos;
            
            // 画面内からはみ出さないように調整
            ClampToScreen();
        }

        /// <summary>
        /// 状況（クリック対象、現在の選択状態）に合わせてボタンを構築します。
        /// </summary>
        private void SetupMenuOptions()
        {
            // 既存のボタンを削除（サブメニュー以外）
            ClearDynamicButtons();

            formationSubMenu.SetActive(false);

            bool fleetSelected = commander.SelectedFleets.Count > 0;
            int buttonCount = 0;

            // 要件：艦隊選択時の右クリックメニューの一番上は移動にする
            if (fleetSelected)
            {
                // 1. 移動 (最優先)
                CreateButton("移動", CommandMove);
                buttonCount++;

                // 2. 後退 (向きを保ったまま下がる＝戦いながら離脱)
                CreateButton("後退", CommandReverse);
                buttonCount++;

                // 3. 攻撃 (選択中は常に表示。選んだ後に攻撃目標の敵旗艦をクリックで指定する)
                CreateButton("攻撃", CommandAttack);
                buttonCount++;

                // 3b. 標準命令（#85）：アタックムーブ／停止／その場保持
                CreateButton("アタックムーブ", CommandAttackMove);
                buttonCount++;
                CreateButton("停止", CommandStop);
                buttonCount++;
                CreateButton("その場保持", CommandHold);
                buttonCount++;

                // 3. 陣形変更
                CreateButton("陣形変更", ToggleFormationSubMenu);
                buttonCount++;

                // 3c. 軍団陣形（隷下艦隊を集結させ軍団陣形を組む・軍団長は後方／方陣は前列ローテーション）
                CreateButton("軍団陣形", ToggleCorpsFormationSubMenu);
                buttonCount++;
                CreateButton("前列交代", () => { if (CorpsFormation.Instance != null) CorpsFormation.Instance.RotateCorps(); CloseMenu(); });
                buttonCount++;
                
                // 4. 情報（選択中は常に表示。クリック対象が無ければ先頭の選択艦隊の詳細を出す）
                Selectable infoTarget = (lastClickedFleet != null) ? lastClickedFleet : commander.SelectedFleets[0];
                CreateButton("情報", () => { ShowFleetInfo(infoTarget); CloseMenu(); });
                buttonCount++;

                // 「選択」「選択解除」は要件により非表示
            }
            else if (lastClickedFleet != null)
            {
                // 何も選択していない状態で艦隊をクリックした場合のみ「選択」を出す
                CreateButton("選択", () => { commander.SelectFleet(lastClickedFleet); CloseMenu(); });
                CreateButton("情報", () => { ShowFleetInfo(lastClickedFleet); CloseMenu(); });
                buttonCount += 2;
            }

            // 表示するボタンがない場合は閉じる
            if (buttonCount == 0)
            {
                CloseMenu();
            }
        }

        private void CreateButton(string label, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = Instantiate(buttonPrefab, menuRect);
            btnObj.SetActive(true);
            
            // テキスト設定
            TextMeshProUGUI textComp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = label;
                // フォントが未設定の場合のフォールバック
                if (textComp.font == null)
                {
                    textComp.font = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
                }
                // 長いラベル（アタックムーブ/その場保持）が折り返して縦に伸び・見切れるのを防ぐ：
                // 折り返し禁止＋自動縮小で常に1行に収める（ボタン高さ・メニュー高さを一定に保つ）。
                textComp.enableWordWrapping = false;
                float baseSize = textComp.fontSize;
                textComp.enableAutoSizing = true;
                textComp.fontSizeMin = 10f;
                textComp.fontSizeMax = baseSize > 0f ? baseSize : 24f;
            }
            
            Button btnComp = btnObj.GetComponent<Button>();
            if (btnComp != null)
            {
                btnComp.onClick.AddListener(action);
            }
        }

        private void CommandMove()
        {
            if (commander != null)
            {
                commander.StartWaitingForMoveTarget();
            }
            CloseMenu();
        }

        private void CommandReverse()
        {
            if (commander != null)
            {
                commander.StartWaitingForReverseTarget();
            }
            CloseMenu();
        }

        private void CommandAttack()
        {
            if (commander != null)
            {
                commander.StartWaitingForAttackTarget();
            }
            CloseMenu();
        }

        /// <summary>アタックムーブ：目標地点を指定→進撃しつつ捕捉した敵と交戦（#85）。</summary>
        private void CommandAttackMove()
        {
            if (commander != null) commander.StartWaitingForAttackMove();
            CloseMenu();
        }

        /// <summary>停止：選択中の全艦隊をその場で停止（#85）。</summary>
        private void CommandStop()
        {
            if (commander != null) commander.StopSelected();
            CloseMenu();
        }

        /// <summary>その場保持：移動せず射界内の敵に自動発砲（#85）。</summary>
        private void CommandHold()
        {
            if (commander != null) commander.HoldSelected();
            CloseMenu();
        }

        /// <summary>
        /// 攻撃種別（通常/ミサイル）の選択メニューを指定スクリーン座標に開く。
        /// 攻撃目標選択中に敵艦隊を右クリックすると FleetCommander から呼ばれる。
        /// </summary>
        public void OpenAttackTypeMenu(Vector2 screenPos)
        {
            ClearDynamicButtons();
            if (formationSubMenu != null) formationSubMenu.SetActive(false);

            CreateButton("通常攻撃", () => { if (commander != null) commander.ConfirmPendingAttack(false); CloseMenu(); });
            CreateButton("ミサイル攻撃", () => { if (commander != null) commander.ConfirmPendingAttack(true); CloseMenu(); });

            menuRoot.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuRect);
            menuRect.pivot = new Vector2(0, 1);
            menuRect.position = screenPos;
            ClampToScreen();
        }

        /// <summary>動的に生成したボタンを削除する（サブメニュー・雛形は残す）。</summary>
        private void ClearDynamicButtons()
        {
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform child in menuRect)
            {
                if (child.gameObject != formationSubMenu && child.gameObject != buttonPrefab)
                {
                    toDestroy.Add(child.gameObject);
                }
            }
            foreach (var obj in toDestroy)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(obj);
                else Destroy(obj);
#else
                Destroy(obj);
#endif
            }
        }

        /// <summary>陣形サブメニューの開閉。開くときはメインメニューの脇（画面端と反対側）へ配置する。</summary>
        private void ToggleFormationSubMenu()
        {
            corpsFormationMode = false; // 通常＝艦隊の陣形変更
            if (formationSubMenu == null) return;
            bool show = !formationSubMenu.activeSelf;
            formationSubMenu.SetActive(show);
            if (show) PositionFormationSubMenu();
        }

        /// <summary>軍団陣形サブメニューの開閉。陣形を選ぶと選択艦隊の軍団を集結させ陣形を組む（軍団長は後方）。</summary>
        private void ToggleCorpsFormationSubMenu()
        {
            corpsFormationMode = true; // 同じサブメニューを軍団モードで使う
            if (formationSubMenu == null) return;
            bool show = !formationSubMenu.activeSelf;
            formationSubMenu.SetActive(show);
            if (show) PositionFormationSubMenu();
        }

        /// <summary>
        /// 陣形サブメニューをメインメニューの左右どちらかの脇に配置する。
        /// メインメニューが画面の右寄りなら左へ、左寄りなら右へ開く（近い画面端と反対側＝見切れ回避）。
        /// メインメニューの辺にアンカーするので、メニュー幅やキャンバス縮尺に依存しない。
        /// </summary>
        private void PositionFormationSubMenu()
        {
            if (formationSubRect == null || menuRect == null) return;

            if (formationSubLayout != null) formationSubLayout.ignoreLayout = true; // 念のため再保証
            formationSubMenu.transform.SetAsLastSibling();                          // メニュー前面へ

            // メインメニューのサイズ・位置を確定
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuRect);

            // メインメニューが画面右半分にあるなら左へ、左半分なら右へ開く（position はオーバーレイで画面px）
            bool openLeft = menuRect.position.x > Screen.width * 0.5f;
            const float gap = 4f;

            if (openLeft)
            {
                // メインメニューの左隣：親(menuRect)の左上にアンカーし、自分の右上を合わせて左へ
                formationSubRect.anchorMin = formationSubRect.anchorMax = new Vector2(0f, 1f);
                formationSubRect.pivot = new Vector2(1f, 1f);
                formationSubRect.anchoredPosition = new Vector2(-gap, 0f);
            }
            else
            {
                // メインメニューの右隣：親の右上にアンカーし、自分の左上を合わせて右へ
                formationSubRect.anchorMin = formationSubRect.anchorMax = new Vector2(1f, 1f);
                formationSubRect.pivot = new Vector2(0f, 1f);
                formationSubRect.anchoredPosition = new Vector2(gap, 0f);
            }
        }

        public void ChangeFormation(int formationIdx)
        {
            if (corpsFormationMode)
            {
                // 軍団陣形：選択中の艦隊（どの隷下艦隊を含めるか＝プレイヤーの選択）で陣形を組む。
                // 1隊だけ選択ならその軍団を自動集結。敗走中の艦隊は CorpsFormation 側で除外。
                if (commander != null && CorpsFormation.Instance != null && commander.SelectedFleets.Count > 0)
                {
                    var members = new List<FleetStrength>(commander.SelectedFleets.Count);
                    for (int i = 0; i < commander.SelectedFleets.Count; i++)
                    {
                        FleetStrength fs = commander.SelectedFleets[i] != null
                            ? commander.SelectedFleets[i].GetComponent<FleetStrength>() : null;
                        if (fs != null) members.Add(fs);
                    }
                    CorpsFormation.Instance.FormCorpsFromSelection(members, (Formation)formationIdx);
                }
                corpsFormationMode = false;
                CloseMenu();
                return;
            }
            // 陣形変更の実体は FleetCommander に集約（重複排除）。ここではメニューを閉じるだけ担当。
            if (commander != null) commander.ChangeFormation(formationIdx);
            CloseMenu();
        }

        private void ShowFleetInfo(Selectable fleet)
        {
            // 詳細はモーダルの FleetDetailPanel に表示（表示中はポーズ）。HUDとは別。
            if (fleet != null) FleetDetailPanel.Show(fleet);
        }

        private void ClampToScreen()
        {
            RectTransform canvasRT = menuRect.parent as RectTransform;
            if (canvasRT == null) return;

            Vector2 parentSize = canvasRT.rect.size;
            Vector2 menuSize = menuRect.rect.size;
            Vector2 pos = menuRect.anchoredPosition;

            // X軸クランプ
            if (pos.x + menuSize.x > parentSize.x) pos.x = parentSize.x - menuSize.x;
            if (pos.x < 0) pos.x = 0;

            // Y軸クランプ (ピボットが(0,1)なので、pos.yは上端)
            if (pos.y > parentSize.y) pos.y = parentSize.y;
            if (pos.y - menuSize.y < 0) pos.y = menuSize.y;

            menuRect.anchoredPosition = pos;
        }

        public void CloseMenu()
        {
            if (menuRoot != null) menuRoot.SetActive(false);
            if (formationSubMenu != null) formationSubMenu.SetActive(false);
        }
    }
}

