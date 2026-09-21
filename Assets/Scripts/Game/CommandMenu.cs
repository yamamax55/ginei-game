using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        // 現在開いているサブメニューのカテゴリ（同じカテゴリ再クリックで閉じるトグル判定）
        private string openCategory;

        private bool windowAttachDone; // 窓内へメニューを親替え済みか（WIN-4・ウィンドウ化会戦のみ）

        private void Start()
        {
            // 複数会戦が同時にロードされていても自分の会戦の指揮官を掴む（同シーン優先・WIN-4）。
            if (commander == null) commander = BattleWindowUI.FindInSceneOrAny<FleetCommander>(gameObject.scene);
            CacheFormationSubMenu();
            CloseMenu();
        }

        /// <summary>
        /// ウィンドウ化会戦（WIN-4）ではコマンドメニュー（menuRoot）を自分の窓内 UI 親へ親替えする
        /// （全画面に広げない・複数窓で重ならない）。メニューはマウス画面座標で開くので、親が窓矩形になっても
        /// ScreenSpaceOverlay 上の座標一致は保たれ、ClampToScreen は窓矩形内へ収める。
        /// フルスクリーン会戦（会戦シーン＝アクティブシーン）では何もしない（後方互換）。
        /// </summary>
        private void TryWindowAttach()
        {
            if (windowAttachDone || menuRoot == null) return;
            if (gameObject.scene == SceneManager.GetActiveScene()) { windowAttachDone = true; return; }
            if (BattleWindowUI.TryAttach(gameObject.scene, menuRoot.GetComponent<RectTransform>())) windowAttachDone = true;
        }

        public void AttachForTest() => TryWindowAttach();
        public bool WindowAttachedForTest => windowAttachDone;
        public Transform WindowParentForTest => menuRoot != null ? menuRoot.transform.parent : null;

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

        private void Update()
        {
            TryWindowAttach();
            if (!BattleWindowUI.AcceptsInput(gameObject.scene)) return;

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
            openCategory = null;

            bool fleetSelected = commander.SelectedFleets.Count > 0;
            int buttonCount = 0;

            // 右クリックメニューはトップを少数のカテゴリに集約し、詳細はサブメニュー（▸）へ送る（項目過多の解消）。
            if (fleetSelected)
            {
                // 1. 移動（最頻＝トップに直置き）
                CreateButton("移動", CommandMove);
                buttonCount++;

                // 2. 攻撃（トップに直置き。選んだ後に攻撃目標を指定）
                CreateButton("攻撃", CommandAttack);
                buttonCount++;

                // 3. 特殊 ▸（アタックムーブ／後退／停止／その場保持・#85）
                CreateButton("特殊 ▸", () => OpenCategory("standard", StandardOrderItems()));
                buttonCount++;

                // 4. 陣形 ▸（Formation enum から動的生成）
                CreateButton("陣形 ▸", () => OpenCategory("formation", FormationItems(false)));
                buttonCount++;

                // 4. 軍団 ▸（軍団旗艦＝CSG、または軍団に属する艦隊の選択時）。軍団全体の隊形＋前列交代＋解除をまとめる。
                //    適用中の軍団命令（軍団名・隊形・手動/自動・形成中/完了）をラベルに出して状態を確認できるようにする。
                if (SelectionHasCorps())
                {
                    string status = CorpsStatusOfSelection();
                    string label = string.IsNullOrEmpty(status) ? "軍団 ▸" : $"軍団 ▸  {status}";
                    CreateButton(label, () => OpenCategory("corps", FormationItems(true)));
                    buttonCount++;
                }

                // 5. 特殊指揮 ▸（一斉砲撃／突撃／不退転・#2175）
                CreateButton("特殊指揮 ▸", () => OpenCategory("special", SpecialCommandItems()));
                buttonCount++;

                // 6. 交戦規定 ▸（ROE・#2258）
                CreateButton("交戦規定 ▸", () => OpenCategory("roe", RoeItems()));
                buttonCount++;

                // 7. 情報（クリック対象が無ければ先頭の選択艦隊の詳細）
                Selectable infoTarget = (lastClickedFleet != null) ? lastClickedFleet : commander.SelectedFleets[0];
                CreateButton("情報", () => { ShowFleetInfo(infoTarget); CloseMenu(); });
                buttonCount++;
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
            => CreateButtonIn(menuRect, label, action);

        private void CreateButtonIn(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = Instantiate(buttonPrefab, parent);
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
                textComp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
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

        /// <summary>
        /// カテゴリのサブメニュー（▸）を開く汎用窓口。単一の <see cref="formationSubMenu"/> パネルを使い回し、
        /// 渡された項目でボタンを作り直してメインメニューの脇へ配置する。同じカテゴリの再クリックで閉じる（トグル）。
        /// </summary>
        private void OpenCategory(string key, List<(string label, UnityEngine.Events.UnityAction action)> items)
        {
            if (formationSubMenu == null) return;

            // 同じカテゴリを再クリック＝閉じる
            if (formationSubMenu.activeSelf && openCategory == key)
            {
                formationSubMenu.SetActive(false);
                openCategory = null;
                return;
            }

            openCategory = key;
            ClearSubMenuButtons();
            for (int i = 0; i < items.Count; i++)
                CreateButtonIn(formationSubMenu.transform, items[i].label, items[i].action);

            formationSubMenu.SetActive(true);
            if (formationSubRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(formationSubRect);
            PositionFormationSubMenu();
        }

        /// <summary>サブメニューパネルの子ボタンを全て破棄する（再構築のため）。</summary>
        private void ClearSubMenuButtons()
        {
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform child in formationSubMenu.transform)
            {
                if (child.gameObject == buttonPrefab) continue;
                toDestroy.Add(child.gameObject);
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

        /// <summary>「特殊 ▸」カテゴリの標準命令（アタックムーブ／後退／停止／その場保持・#85）。移動・攻撃はトップに直置き。</summary>
        private List<(string, UnityEngine.Events.UnityAction)> StandardOrderItems() => new List<(string, UnityEngine.Events.UnityAction)>
        {
            ("アタックムーブ", CommandAttackMove),
            ("後退", CommandReverse),
            ("停止", CommandStop),
            ("その場保持", CommandHold),
        };

        /// <summary>
        /// 「配下艦隊の陣形を一括指定 ▸」の項目（確定仕様1）。
        /// 軍団隊形（艦隊の並べ方）ではなく<b>各艦隊の中の陣形</b>をまとめて指定する。
        /// 対象と権限の判定は <see cref="FleetCommander.ChangeFormationForCorps"/> が #67 の関門で行う。
        /// </summary>
        private List<(string, UnityEngine.Events.UnityAction)> CorpsFleetFormationItems()
        {
            var items = new List<(string, UnityEngine.Events.UnityAction)>();
            bool anyTranscendent = SelectionHasTranscendent();
            var values = (Formation[])System.Enum.GetValues(typeof(Formation));
            for (int i = 0; i < values.Length; i++)
            {
                Formation f = values[i];
                if (FormationAccessRules.IsTranscendentOnly(f) && !anyTranscendent) continue;
                int idx = (int)f;
                items.Add((f.ToString(), () =>
                {
                    if (commander != null) commander.ChangeFormationForCorps(idx);
                    CloseMenu();
                }));
            }
            return items;
        }

        /// <summary>
        /// 選択中の艦隊の<b>陣形の保持</b>を解いて AI 自動へ返す（確定仕様1）。
        /// 移動・攻撃の命令には触らない。#67 の関門を通した対象だけが動く。
        /// </summary>
        private void ReleaseSelectedFormationHold()
        {
            List<FleetStrength> targets = SelectedFleetStrengths("陣形の保持を解除");
            int released = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                Squadron sq = targets[i] != null ? targets[i].GetComponent<Squadron>() : null;
                if (sq != null && sq.ReleaseFormationHold("指揮官の解除")) released++;
            }
            if (released == 0)
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                    "陣形を保持している艦隊がありません");
        }

        /// <summary>「陣形 ▸」「軍団 ▸」の項目（Formation enum から動的生成・軍団モードは前列交代を追加）。</summary>
        private List<(string, UnityEngine.Events.UnityAction)> FormationItems(bool corps)
        {
            var items = new List<(string, UnityEngine.Events.UnityAction)>();
            bool anyTranscendent = SelectionHasTranscendent();
            var values = (Formation[])System.Enum.GetValues(typeof(Formation));
            for (int i = 0; i < values.Length; i++)
            {
                Formation f = values[i];
                // 軍神専用陣形（車懸かり）は、選択中に軍神がいる時だけメニューに出す。
                if (FormationAccessRules.IsTranscendentOnly(f) && !anyTranscendent) continue;
                int idx = (int)f; // クロージャ用にキャプチャ
                items.Add((f.ToString(), () => ApplyFormation(idx, corps)));
            }
            if (corps)
            {
                // ★対象を明示して渡す＝「最後に操作した軍団」ではなく<b>選択中の艦隊が属する軍団だけ</b>が交代する
                //   （A軍団の前列交代がB軍団へ波及しない）。
                items.Add(("前列交代", () => { RotateSelectedCorps(); CloseMenu(); }));
                // 手動指定の明示解除＝AI 自動（ドクトリン）へ返す唯一のプレイヤー操作。
                items.Add(("軍団指定を解除", () => { ReleaseSelectedCorps(); CloseMenu(); }));
                // ★軍団隊形（艦隊の並べ方）とは別に、配下<b>各艦隊の陣形</b>をまとめて指定する（確定仕様1）。
                //   ここは軍団隊形を変えない＝2つのレイヤーを混ぜない。
                items.Add(("配下艦隊の陣形を一括指定 ▸",
                    () => OpenCategory("corpsFleetFormation", CorpsFleetFormationItems())));
            }
            else
            {
                // 艦隊の陣形保持を解いて AI 自動（ドクトリン）へ返す（確定仕様1）。
                items.Add(("陣形の保持を解除", () => { ReleaseSelectedFormationHold(); CloseMenu(); }));
            }
            return items;
        }

        /// <summary>選択中の艦隊が属する軍団だけを前列交代させる（他軍団へ波及しない）。</summary>
        private void RotateSelectedCorps()
        {
            if (CorpsFormation.Instance == null) return;
            int n = CorpsFormation.Instance.RotateCorps(SelectedFleetStrengths());
            if (n == 0)
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                    "前列交代：選択中の艦隊に軍団隊形の命令がありません");
        }

        /// <summary>選択中の艦隊が属する軍団の手動指定を解除して AI 自動へ返す。</summary>
        private void ReleaseSelectedCorps()
        {
            if (CorpsFormation.Instance == null) return;
            // #67：解除もプレイヤーの命令。選択を関門（CommandableSelection）へ通し、
            // 解除側でも軍団ごとに指揮権を確かめる（系統外の軍団は解けない）。
            CorpsFormation.Instance.ReleaseCorps(SelectedFleetStrengths("軍団指定の解除"),
                                                 CommandOrderSource.プレイヤー);
        }

        /// <summary>選択中の艦隊（旗艦コア）を集める共通処理。</summary>
        /// <summary>
        /// 命令の対象になる選択（<b>指揮系統の内側だけ</b>・#67）。
        /// ★<see cref="FleetCommander.CommandableSelection"/> を通すので、軍団隊形・特殊指揮・交戦規定など
        /// <see cref="FleetCommander"/> を経由しない命令も系統外へ漏れない。
        /// </summary>
        private List<FleetStrength> SelectedFleetStrengths(string orderName = "命令")
        {
            var members = new List<FleetStrength>();
            if (commander == null) return members;
            List<Selectable> targets = commander.CommandableSelection(orderName);
            for (int i = 0; i < targets.Count; i++)
            {
                FleetStrength fs = targets[i] != null ? targets[i].GetComponent<FleetStrength>() : null;
                if (fs != null) members.Add(fs);
            }
            return members;
        }

        /// <summary>選択中に軍団旗艦（軍団長乗艦）または軍団所属の艦隊がいるか＝「軍団 ▸」を出す条件。</summary>
        private bool SelectionHasCorps()
        {
            if (commander == null) return false;
            for (int i = 0; i < commander.SelectedFleets.Count; i++)
            {
                Selectable sel = commander.SelectedFleets[i];
                FleetStrength fs = sel != null ? sel.GetComponent<FleetStrength>() : null;
                if (fs == null) continue;
                if (fs.IsCorpsFlagship || !string.IsNullOrEmpty(fs.corpsName)) return true;
                if (!string.IsNullOrEmpty(CorpsFormation.StatusFor(fs))) return true; // 臨時編成でも命令中なら出す
            }
            return false;
        }

        /// <summary>選択中の艦隊に適用中の軍団命令の表示文字列（軍団名・隊形・手動/自動・形成中/完了）。無ければ空。</summary>
        private string CorpsStatusOfSelection()
        {
            if (commander == null) return "";
            for (int i = 0; i < commander.SelectedFleets.Count; i++)
            {
                Selectable sel = commander.SelectedFleets[i];
                FleetStrength fs = sel != null ? sel.GetComponent<FleetStrength>() : null;
                string s = CorpsFormation.StatusFor(fs);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return "";
        }

        /// <summary>選択中の艦隊に軍神（限界突破型・車懸かり可）がいるか。</summary>
        private bool SelectionHasTranscendent()
        {
            if (commander == null) return false;
            for (int i = 0; i < commander.SelectedFleets.Count; i++)
            {
                Selectable sel = commander.SelectedFleets[i];
                if (sel == null) continue;
                FleetStrength fs = sel.GetComponent<FleetStrength>();
                if (fs != null && fs.admiralData != null && fs.admiralData.isTranscendent) return true;
            }
            return false;
        }

        /// <summary>陣形（艦隊／軍団）を適用してメニューを閉じる。</summary>
        private void ApplyFormation(int idx, bool corps)
        {
            corpsFormationMode = corps;
            ChangeFormation(idx);
        }

        /// <summary>「特殊指揮 ▸」の項目（一斉砲撃／突撃／不退転・#2175）。</summary>
        private List<(string, UnityEngine.Events.UnityAction)> SpecialCommandItems() => new List<(string, UnityEngine.Events.UnityAction)>
        {
            ("一斉砲撃", () => { IssueActiveCommand(ActiveCommand.一斉砲撃); CloseMenu(); }),
            ("突撃", () => { IssueActiveCommand(ActiveCommand.突撃); CloseMenu(); }),
            ("不退転", () => { IssueActiveCommand(ActiveCommand.不退転); CloseMenu(); }),
        };

        /// <summary>「交戦規定 ▸」の項目（ROE・#2258）。</summary>
        private List<(string, UnityEngine.Events.UnityAction)> RoeItems() => new List<(string, UnityEngine.Events.UnityAction)>
        {
            ("攻撃的", () => { SetStanceAll(EngagementStance.攻撃的); CloseMenu(); }),
            ("防御的", () => { SetStanceAll(EngagementStance.防御的); CloseMenu(); }),
            ("射撃管制", () => { SetStanceAll(EngagementStance.射撃管制); CloseMenu(); }),
            ("退避", () => { SetStanceAll(EngagementStance.退避); CloseMenu(); }),
        };

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

        /// <summary>選択中の全艦隊へ特殊指揮（#2175）を発令。発令できた数を通知する。</summary>
        private void IssueActiveCommand(ActiveCommand cmd)
        {
            if (commander == null) return;
            // #67：特殊指揮も指揮系統の内側だけ。
            List<FleetStrength> members = SelectedFleetStrengths("特殊指揮 " + cmd);
            int issued = 0, total = members.Count;
            for (int i = 0; i < members.Count; i++)
                if (ActiveCommandState.Issue(members[i], cmd)) issued++;
            if (total > 0 && issued == 0)
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                    $"特殊指揮『{cmd}』は今は使えません（クールダウン/効果中）");
        }

        /// <summary>
        /// 選択中の全艦隊へ交戦規定スタンスを一括設定する（ROE・#2258）。
        /// </summary>
        private void SetStanceAll(EngagementStance newStance)
        {
            if (commander == null) return;
            // #67：交戦規定も指揮系統の内側だけ（FleetCommander を経由しない命令なのでここで通す）。
            List<FleetStrength> members = SelectedFleetStrengths("交戦規定の変更");
            for (int i = 0; i < members.Count; i++) members[i].stance = newStance;
            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                $"交戦規定を「{newStance}」に設定しました");
        }

        public void ChangeFormation(int formationIdx)
        {
            if (corpsFormationMode)
            {
                // 軍団<b>全体</b>の隊形（軍団内で艦隊をどう並べるか）＝各艦隊内の配下艦の陣形（Squadron）とは別レイヤー。
                // 選択中の艦隊（どの隷下艦隊を含めるか＝プレイヤーの選択）で組む。選択が複数の軍団に跨っていても
                // CorpsFormation が軍団ごとに別命令へ束ねる＝A軍団とB軍団で別々の隊形を同時に維持できる。
                // 1隊だけ選択ならその軍団を自動集結。敗走中の艦隊は CorpsFormation 側で除外。
                if (commander != null && CorpsFormation.Instance != null && commander.SelectedFleets.Count > 0)
                    CorpsFormation.Instance.FormCorpsFromSelection(SelectedFleetStrengths("軍団隊形"), (Formation)formationIdx);
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
            openCategory = null;
        }
    }
}
