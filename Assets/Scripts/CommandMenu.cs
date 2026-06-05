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

        private void Start()
        {
            if (commander == null) commander = Object.FindAnyObjectByType<FleetCommander>();
            BuildFormationButtons();
            CloseMenu();
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

                // 3. 陣形変更
                CreateButton("陣形変更", () => formationSubMenu.SetActive(!formationSubMenu.activeSelf));
                buttonCount++;
                
                // 4. 情報 (選択中も情報は出せると便利なので残す)
                if (lastClickedFleet != null)
                {
                    CreateButton("情報", () => { ShowFleetInfo(lastClickedFleet); CloseMenu(); });
                    buttonCount++;
                }

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

        public void ChangeFormation(int formationIdx)
        {
            // 陣形変更の実体は FleetCommander に集約（重複排除）。ここではメニューを閉じるだけ担当。
            if (commander != null) commander.ChangeFormation(formationIdx);
            CloseMenu();
        }

        private void ShowFleetInfo(Selectable fleet)
        {
            FleetStrength str = fleet.GetComponent<FleetStrength>();
            Squadron sq = fleet.GetComponent<Squadron>();
            if (str != null)
            {
                Debug.Log($"艦隊情報: {str.admiralName} ({str.faction}) - 兵力: {str.strength}/{str.maxStrength}");
            }
            if (sq != null)
            {
                Debug.Log($"現在陣形: {sq.currentFormation}");
            }
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

