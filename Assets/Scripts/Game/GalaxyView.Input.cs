using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    public partial class GalaxyView
    {
        /// <summary>マウス直下の星系の情報パネルを開く（I キー・#759）。表示中は戦略マップがポーズ。</summary>
        private void OpenSystemInfoAtMouse()
        {
            if (cam == null) return;
            Vector2 w = WorldMouse();
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || !GovernanceProposalRules.AcceptsPointerDistance(d, ClickRadiusFor(sysId))) return;
            OpenSystemInfo(sysId);
        }

        /// <summary>
        /// 盤面の入力・進行を止めるモーダル（イベント提示／艦隊編成／決裁ボードと詳細／終了画面／システムメニュー）が
        /// 開いているか。<see cref="Update"/> の早期 return と同じ条件＝マウスの入口（星系図のボタン・上申ボタン）も
        /// ここを見る（判定を二重に書かない）。
        /// </summary>
        public static bool IsBoardModalOpen =>
            StrategyEventPanel.IsOpen || FleetOrganizationPanel.IsOpen || DecisionBoardPanel.IsOpen
            || DecisionBoardPanel.DetailOpen || CampaignEndOverlay.IsOpen || StrategySystemMenu.IsOpen;

        /// <summary>指定星系の情報パネルを開く（I キー・星系図の入口ボタン共通）。星系が無い／モーダル表示中は false。</summary>
        public bool OpenSystemInfo(int sysId)
        {
            if (IsBoardModalOpen) return false;
            if (!TryGetSystemInfo(sysId, out StarSystem s, out Province prov, out int neighborCount, out string fleetSummary))
                return false;
            SystemDetailPanel.Show(s, prov, neighborCount, fleetSummary);
            return true;
        }

        /// <summary>
        /// 星系情報パネルに出す最新データを読む（読み取りのみ＝開閉・ポーズなどの副作用なし）。
        /// <see cref="OpenSystemInfo"/> と、開いたままの <see cref="SystemDetailPanel"/> の定期更新が共用する。
        /// </summary>
        public bool TryGetSystemInfo(int sysId, out StarSystem s, out Province prov, out int neighborCount, out string fleetSummary)
        {
            s = null; prov = null; neighborCount = 0; fleetSummary = "";
            if (map == null || reg == null) return false;
            s = map.GetSystem(sysId);
            if (s == null) return false;
            provinces.TryGetValue(sysId, out prov);
            neighborCount = map.Neighbors(sysId).Count;
            fleetSummary = FleetSummaryAt(sysId);
            return true;
        }

        /// <summary>
        /// 星系に停泊中の戦略艦隊を勢力ごとに「N隊・M隻」で要約する。
        /// 合計は<b>各艦隊の実艦艇数の合計</b>＝抽象兵力に「隻」を付け替えたものではない。
        /// </summary>
        private string FleetSummaryAt(int sysId)
        {
            // 要塞に駐留している艦隊は要塞側で数えるので、星系の集計からは外す（二重計上の防止）。
            var here = FortressGarrisonRules.ExcludeGarrisoned(map, reg.FleetsAt(sysId));
            if (here == null || here.Count == 0) return "";
            var strengthByF = new Dictionary<Faction, int>();
            var countByF = new Dictionary<Faction, int>();
            for (int i = 0; i < here.Count; i++)
            {
                StrategicFleet f = here[i];
                if (f == null) continue;
                strengthByF.TryGetValue(f.faction, out int st); strengthByF[f.faction] = st + f.Ships;
                countByF.TryGetValue(f.faction, out int c); countByF[f.faction] = c + 1;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in strengthByF)
                sb.AppendLine($"{kv.Key}：{countByF[kv.Key]}隊・{kv.Value:N0}隻");
            return sb.ToString().TrimEnd();
        }

        // ===== デモ銀河 =====

        /// <summary>
        /// ESC の解決（#ウィンドウESC）：①重ねたウィンドウ（観測オーバーレイ・各パネル）を最前面から1枚閉じる。
        /// ②閉じる窓が無ければシステムメニュー（再開/セーブ/タイトル）を開閉する。
        /// </summary>
        private void HandleStrategyEscape()
        {
            // 文字入力中は Escape をゲーム操作として解釈しない（入力のキャンセルは各欄に任せる）。
            if (IsTextInputFocused()) return;

            // 優先順位（1回の Escape で1動作だけ）：
            // ①最前面の艦隊一覧 → ②重ねたウィンドウを1枚 → ③選択の解除 → ④システムメニュー。
            if (FleetClusterListPanel.IsOpen) { FleetClusterListPanel.Hide(); openedCluster = null; return; }
            if (UIWindowStack.CloseTopmost()) return;        // 手前のウィンドウを1枚閉じる
            if (selectedFleets.Count > 0) { selectedFleets.Clear(); return; } // 選択を解除
            // ESC で閉じない専用モーダル（イベント提示は選択が必要／終了画面は終端）の上にはシステムメニューを被せない。
            if (StrategyEventPanel.IsOpen || CampaignEndOverlay.IsOpen) return;
            StrategySystemMenu menu = Object.FindAnyObjectByType<StrategySystemMenu>();
            if (menu != null) menu.Toggle();                  // 無ければシステムメニュー開閉
        }

        /// <summary>
        /// 戦略UI上の右クリックを「一つ上へ戻る」として統一する。
        /// 盤面上の右クリックは近年仕様の艦隊メニュー入口として残し、UI窓上だけ万能撤回にする。
        /// </summary>
        private bool HandleStrategyRightClickReturn()
        {
            if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame) return false;
            return HandleStrategyRightClickReturn(IsBoardModalOpen, PointerOverUI());
        }

        /// <summary>入力装置から切り離した右クリック復帰の実処理。PlayModeで経路を固定検証する。</summary>
        private bool HandleStrategyRightClickReturn(bool modalOpen, bool pointerOverUi)
        {
            if (!modalOpen && !pointerOverUi) return false;
            if (IsTextInputFocused()) return true;

            if (FleetClusterListPanel.IsOpen)
            {
                FleetClusterListPanel.Hide();
                openedCluster = null;
                return true;
            }
            if (UIWindowStack.CloseTopmost()) return true;

            // システムメニュー自体が最前面なら右クリックで閉じて固定トップバーへ戻る。
            if (StrategySystemMenu.IsOpen)
            {
                StrategySystemMenu menu = Object.FindAnyObjectByType<StrategySystemMenu>();
                if (menu != null) menu.Toggle();
            }
            return true; // メニューバー等のUI上では盤面右クリックへ流さない
        }

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            GameClock clock = StrategySession.Clock;
            // ポーズ/速度プリセットは統一クロックを駆動（TIME-1）。速度の +/- は TimeDisplay が全シーン共通で処理。
            if (kb.spaceKey.wasPressedThisFrame && clock != null) clock.TogglePause();
            // デバッグモード切替（` キー）。税率レバー等のデバッグ専用機能の入力/表示をまとめてゲートする。
            if (kb.backquoteKey.wasPressedThisFrame) debugMode = !debugMode;
            // 税率レバー（★デバッグ専用）：] で増税 / [ で減税。通常プレイでは税率は内政/AI委任で動かしレバーは出さない（タイクン化回避）。
            if (debugMode)
            {
                FactionState ps = PlayerState();
                if (ps != null)
                {
                    if (kb.rightBracketKey.wasPressedThisFrame) ps.taxRate = Mathf.Clamp01(ps.taxRate + taxStep);
                    if (kb.leftBracketKey.wasPressedThisFrame) ps.taxRate = Mathf.Clamp01(ps.taxRate - taxStep);
                }
            }
            if (clock != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { clock.SetSpeed(0.5f); clock.Resume(); }
                if (kb.digit2Key.wasPressedThisFrame) { clock.SetSpeed(1f); clock.Resume(); }
                if (kb.digit3Key.wasPressedThisFrame) { clock.SetSpeed(2f); clock.Resume(); }
            }
            if (kb.iKey.wasPressedThisFrame) OpenSystemInfoAtMouse(); // 星系情報パネル(#759)
            // 星系別統治政策を稟議上申（#67/#109 P-5）。P＝人物名鑑／Alt+P＝生産観測と重ならないよう GameInput 経由
            if (GameInput.WasPressed(GameAction.統治政策上申)) CycleGovernancePolicyAtMouse();
            if (kb.fKey.wasPressedThisFrame) ResetView(); // F：既定のズーム/位置へ戻す（#2384）

            HandleKeyPan(kb); // ステラリス風：WASD/矢印キーで視点パン（押しっぱで連続）

            // 外交コマンド（#2119 操作化）：対立勢力へ 7=宣戦 / 8=講和 / 9=同盟。自勢力の外交はプレイヤーが握る。
            if (kb.digit7Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.宣戦布告);
            if (kb.digit8Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.講和);
            if (kb.digit9Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.同盟);

            // ミッションコマンド（任務戦術）：C＝マウス直下の敵対星系へ攻略任務／V＝対立勢力を攻略（参謀本部が目標選定・必要兵力を見積もり自動動員）。
            // 援軍（#38 C-5）：Alt+A で艦隊メニューを開く（派遣の入口もメニューへ集約）。
            // カーソル下に交戦中の回廊があればそれを宛先候補として選んだ状態で開く。
            if (GameInput.WasPressed(GameAction.援軍派遣)) OpenFleetMenuForReinforcement();

            if (kb.cKey.wasPressedThisFrame) IssueMissionAtMouse();
            if (kb.vKey.wasPressedThisFrame) IssueCampaignAgainstRival();

            // セーブ/ロード（continue・全永続化）：F5=保存／F9=読込（読込後 Strategy を再ロードして再構築）。
            if (kb.f5Key.wasPressedThisFrame) SaveCampaign();
            if (kb.f9Key.wasPressedThisFrame) LoadCampaign();
        }

        private void HandleMouse()
        {
            if (Mouse.current == null || cam == null) return;

            // 会戦ウィンドウ（WIN-1/3）の上ではマップ操作を会戦へ譲る（背後の戦略マップが二重に反応しない）。
            if (BattleDirector.AnyPointerOverWindow()) return;

            HandleZoom(); // マウスホイール：カーソル中心ズーム（滑らかに追従・回し幅で加速）

            // いずれかの UI 窓（観測オーバーレイ/決裁デスク/通知/星系図等）をドラッグ中は、マップ操作を窓へ譲る
            // ＝窓を動かすと同時にマップがスクロールする問題の確実な解消（raycast 判定の取りこぼし対策の二重防御）。
            if (UIDragMove.AnyDragging) return;

            // マップ窓のタイトルバー/リサイズグリップを掴んでいる間も盤面へ渡さない（窓移動と内部パンの分離）。
            if (MapWindowDrag.AnyGrabbing) return;

            // 中ボタン（ホイール押し込み）ドラッグでもスクロールできる（左ドラッグと同方式）。
            // 押し始めが UI（決裁デスク/通知/星系図/メニュー）上なら、そのドラッグ中はマップを動かさない
            // （ドラッグ中にカーソルが UI から外れても誤スクロールしないよう、判定は「押した瞬間」で固定する）。
            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                midPanning = true;
                midPressOverUI = PointerOverUI();
            }
            else if (Mouse.current.middleButton.isPressed && midPanning)
            {
                if (!midPressOverUI) ScrollViewByMouseDelta();
            }
            else if (Mouse.current.middleButton.wasReleasedThisFrame)
            {
                midPanning = false;
            }

            // 左ボタン：①単独ドラッグ＝マップスクロール ②ダブルクリック＋ドラッグ＝艦隊の矩形選択（マーキー）
            // ③小さく押して離す＝単クリック選択 ④ダブルクリック（ドラッグなし）＝潜行/突入/閲覧。
            // 確定は「離した時」＝各操作が競合しない。押し始めが UI 上なら一切マップに渡さない。
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                leftPressScreen = Mouse.current.position.ReadValue();
                leftDragging = false;
                leftPressOverUI = PointerOverUI();
                // この押下がダブルクリックの2打目か（直前の単クリックから時間内＆近接）＝ドラッグで矩形選択に入る。
                leftPressIsDouble = (Time.realtimeSinceStartup - lastClickTime <= doubleClickWindow)
                                    && Vector2.Distance(WorldMouse(), lastClickWorld) <= 0.6f;
            }
            else if (Mouse.current.leftButton.isPressed && !leftPressOverUI)
            {
                Vector2 cur = Mouse.current.position.ReadValue();
                if (!leftDragging && Vector2.Distance(cur, leftPressScreen) > dragThresholdPixels)
                    leftDragging = true;
                if (leftDragging)
                {
                    if (leftPressIsDouble) UpdateMarquee(cur);   // ダブルクリック＋ドラッグ＝矩形選択（スクロールしない）
                    else ScrollViewByMouseDelta();               // 単独ドラッグ＝マップスクロール（復活）
                }
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (!leftPressOverUI)
                {
                    if (leftDragging)
                    {
                        if (leftPressIsDouble) DoMarqueeSelect(Mouse.current.position.ReadValue()); // 矩形内の艦隊を選択
                        // 単独ドラッグはスクロール済み＝クリック扱いしない
                    }
                    else if (leftPressIsDouble)
                    {
                        // ダブルクリック（ドラッグなし）＝回廊潜行＞星系上の艦隊戦＞攻城突入＞システムビュー（短絡＝先に成立した1つだけ）
                        Vector2 w = WorldMouse();
                        bool _ = TryDescend(w) || TryDescendSystemBattle(w) || TryDescendPlanet(w) || TryEnterSystem(w);
                        lastClickTime = -1f; // 3連クリックで連鎖しないようリセット
                    }
                    else
                    {
                        // 単クリック＝選択（次のダブルクリック判定のため時刻/位置を記録）。
                        Vector2 w = WorldMouse();
                        SelectAtClick(w);
                        lastClickTime = Time.realtimeSinceStartup; lastClickWorld = w;
                    }
                }
                leftDragging = false;
                ClearMarquee();
            }
            // ★右クリックでの<b>進軍発令は廃止</b>（移動命令の入口は艦隊メニューへ集約）。
            // 盤面の誤クリックで艦隊が動いてしまう事故を無くし、「誰を・どこへ・なぜ動かせるか」を
            // 一覧で確認してから出す形にする。MAP は「どこからどこへ移動中か」の表示に徹する。
            // 選択・詳細/艦隊メニューを開く・カメラ操作は従来どおり残す。
            if (!rightClickConsumedThisFrame && Mouse.current.rightButton.wasPressedThisFrame && !PointerOverUI())
            {
                OpenFleetMenuForRightClick();
            }
        }

        /// <summary>
        /// 盤面の右クリック＝<b>艦隊メニューを開く</b>（発令はしない）。
        /// カーソル下に自軍の艦隊があればそれを選択した状態で開き、無ければ選択を保ったまま開く。
        /// 「右クリックで動く」と思って押した人が、そのまま移動命令へ辿り着ける導線にする。
        /// </summary>
        private void OpenFleetMenuForRightClick()
        {
            Vector2 w = WorldMouse();
            StrategicFleet f = NearestFleet(w, 0.7f);
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            if (f != null && f.faction == player) SelectOnly(f);
            FleetOrderPanel.Show();
        }

        // ===== 艦隊メニューの窓口（移動命令はここへ集約・MAP からは発令しない）=====

        /// <summary>選択中の戦略艦隊（読み取り用）。艦隊メニューが一覧の選択状態を映すのに使う。</summary>
        public IReadOnlyList<StrategicFleet> SelectedFleets => selectedFleets;

        /// <summary>その艦隊だけを選択する（艦隊メニューの行クリック）。</summary>
        public void SelectOnly(StrategicFleet f)
        {
            selectedFleets.Clear();
            if (f != null) selectedFleets.Add(f);
        }

        /// <summary>選択に入れる／外す（艦隊メニューの複数選択）。</summary>
        public void ToggleSelect(StrategicFleet f)
        {
            if (f == null) return;
            if (!selectedFleets.Remove(f)) selectedFleets.Add(f);
        }

        /// <summary>選択を解除する。</summary>
        public void ClearSelection() => selectedFleets.Clear();

        /// <summary>星系名（無ければ #id）。艦隊メニューの表示に使う。</summary>
        public string SystemNameOf(int systemId) => SystemName(systemId);

        /// <summary>
        /// 戦略的な移動命令の<b>唯一の窓口</b>（艦隊メニューから呼ぶ）。
        /// 可否は <see cref="FleetOrderRules.CanMoveTo"/> に従い、出せない理由は
        /// <paramref name="reason"/> に日本語で返す。要塞に封鎖されていても<b>発令は通す</b>
        /// （回り道が無いだけで、行って制圧するのは正当な選択なので）。理由は警告として返す。
        /// 進行中の艦隊への再指示も同じ経路で、実際の扱いは <see cref="StrategicFleet.WarpTo"/> の
        /// 既存規則（到達予定の星系まで進んでから引き直す）に従う。
        /// </summary>
        public bool OrderMove(StrategicFleet fleet, int goalId, out string reason)
        {
            reason = "";
            if (fleet == null || map == null) { reason = "艦隊または盤面がありません"; return false; }

            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            MoveOrderRejection r = FleetOrderRules.CanMoveTo(map, fleet, player, goalId);

            // 要塞封鎖は「行けるが力ずくになる」＝発令は許し、理由だけ伝える。
            if (r != MoveOrderRejection.なし && r != MoveOrderRejection.要塞封鎖)
            {
                reason = FleetOrderRules.RejectionText(r);
                return false;
            }
            if (r == MoveOrderRejection.要塞封鎖) reason = FleetOrderRules.RejectionText(r);

            // #40 駐留艦隊：要塞に駐留したまま動かすと名簿と盤面がずれるので、先に出撃させる。
            // 移動命令は「出撃して移動」を1手で行う＝プレイヤーに2度操作させない。
            string sortieNote = "";
            if (FortressGarrisonRules.IsGarrisoned(map, fleet)
                && FortressGarrisonRules.SortieFrom(map, fleet, out Fortress from))
                sortieNote = $"{(from != null ? from.fortressName : "要塞")} から出撃";

            if (!fleet.WarpTo(map, goalId))
            {
                reason = "移動を開始できませんでした";
                return false;
            }
            // 訓練中の出撃は命令を拒まず、訓練・共同演習を中断して即応状態へ戻す。
            InterruptFleetTrainingForSortie(fleet);
            if (!string.IsNullOrEmpty(sortieNote)) reason = JoinNotes(reason, sortieNote);

            // ★実際に止まる星系を確かめて伝える（実機QAで判明した表示と実挙動の食い違い）。
            // 既存規則「飛び石禁止」（<see cref="StrategicFleet.WarpTo"/>）は、経路上で<b>最初の非自勢力星系</b>で
            // 経路を打ち切る＝そこへ入って占領してから改めて先へ進む。経路配列そのものが切り詰められるので、
            // これは表示だけの話ではなく実際の到着地でもある。目的地を伝えるだけだと嘘になるため、
            // 「どこで一旦止まるか」を必ず併記する。
            int stopAt = fleet.FinalDestinationId;
            string note = stopAt != goalId
                ? $"まず {SystemName(stopAt)} を占領（そこで一旦停止）。占領後に改めて命令してください"
                : "";

            string tail = JoinNotes(reason, note);
            // 到着を約束しない：途中で止まるなら「○○ 方面へ」と書く（実際に保存された最終目標は stopAt）。
            string headline = stopAt != goalId
                ? $"第{fleet.id}艦隊に {SystemName(goalId)} 方面への移動を命じました"
                : $"第{fleet.id}艦隊に {SystemName(goalId)} への移動を命じました";
            NotificationCenter.Push(NotificationCategory.システム,
                headline + (string.IsNullOrEmpty(tail) ? "" : $"（{tail}）"));
            reason = tail;   // 艦隊メニューの結果欄にも同じ説明を出す
            return true;
        }

        // ===== 要塞への駐留・出撃（#40 駐留艦隊）=====

        /// <summary>
        /// 指定回廊の要塞へ艦隊を<b>駐留</b>させる（艦隊メニューから呼ぶ唯一の窓口）。
        /// 可否は Core の <see cref="FortressGarrisonRules.CanGarrison"/> に従い、理由を日本語で返す。
        /// 駐留した艦隊は星系に停泊したまま要塞の名簿に載る＝<b>星系と要塞で二重に数えない</b>
        /// （盤面の集計は <see cref="FortressGarrisonRules.ExcludeGarrisoned"/> を通す）。
        /// </summary>
        public bool OrderGarrison(StrategicFleet fleet, int sysA, int sysB, out string reason)
        {
            reason = "";
            Corridor c = map != null ? map.GetCorridor(sysA, sysB) : null;
            Fortress f = c != null ? c.fortress : null;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

            if (!FortressGarrisonRules.Garrison(map, f, fleet, player, out GarrisonRejection r))
            {
                reason = FortressGarrisonRules.RejectionText(r);
                return false;
            }
            NotificationCenter.Push(NotificationCategory.システム,
                $"第{fleet.id}艦隊が {f.fortressName} に駐留しました（{FleetShipCountRules.Label(fleet.Ships)}）");
            return true;
        }

        /// <summary>駐留している要塞から<b>出撃</b>させる（名簿から外して再び動けるようにする）。</summary>
        public bool OrderSortie(StrategicFleet fleet, out string reason)
        {
            reason = "";
            if (fleet == null) { reason = "艦隊がありません"; return false; }
            if (!FortressGarrisonRules.SortieFrom(map, fleet, out Fortress from))
            {
                reason = "この艦隊は要塞に駐留していません";
                return false;
            }
            NotificationCenter.Push(NotificationCategory.システム,
                $"第{fleet.id}艦隊が {(from != null ? from.fortressName : "要塞")} から出撃しました");
            return true;
        }

        /// <summary>その艦隊がいま駐留している要塞（していなければ null）。艦隊メニューの表示に使う。</summary>
        public Fortress GarrisonOf(StrategicFleet fleet)
            => fleet == null || map == null ? null : FortressGarrisonRules.FindGarrison(map, fleet.id);

        /// <summary>要塞のある回廊を <c>min*100000+max</c> のキーで返す（駐留先の候補一覧）。</summary>
        public List<int> FortressCorridors()
        {
            var result = new List<int>();
            if (map == null || map.corridors == null) return result;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress == null) continue;
                int min = Mathf.Min(c.aId, c.bId), max = Mathf.Max(c.aId, c.bId);
                int key = min * 100000 + max;
                if (!result.Contains(key)) result.Add(key);
            }
            return result;
        }

        /// <summary>その回廊の要塞の状態を1行で（所有・守備力・駐留艦隊）。艦隊メニュー／要塞詳細の表示用。</summary>
        public string FortressSummary(int sysA, int sysB)
        {
            Corridor c = map != null ? map.GetCorridor(sysA, sysB) : null;
            if (c == null || c.fortress == null) return "";
            Fortress f = c.fortress;
            string body = reg != null
                ? FortressGarrisonRules.GarrisonSummaryText(f, reg.GetFleet)
                : $"守備力 {Mathf.RoundToInt(f.garrisonStrength)}";
            return $"{f.fortressName}（{f.owner}）　{body}";
        }

        /// <summary>
        /// その回廊の要塞を艦隊メニューの列へ分けて返す（要塞が無ければ false）。
        /// <paramref name="garrisonText"/> は<b>駐留だけ</b>の短縮表示（例「2隊 8,000隻」）＝狭い列でも
        /// 全桁が読める。施設の守備力は含めないので、駐留艦隊と足し合わせて読まれない。
        /// 施設の守備力まで含む1行が要るときは <see cref="FortressSummary"/> を使う。
        /// </summary>
        public bool TryFortressAt(int sysA, int sysB, out string fortressName, out string ownerName, out string garrisonText)
        {
            fortressName = "";
            ownerName = "";
            garrisonText = "";
            Corridor c = map != null ? map.GetCorridor(sysA, sysB) : null;
            if (c == null || c.fortress == null) return false;

            Fortress f = c.fortress;
            fortressName = string.IsNullOrEmpty(f.fortressName) ? "要塞" : f.fortressName;
            ownerName = f.owner.ToString();
            garrisonText = FortressGarrisonRules.GarrisonCompactText(
                f, reg != null ? reg.GetFleet : (System.Func<int, StrategicFleet>)null);
            return true;
        }

        /// <summary>
        /// その要塞へ駐留できるか。判定は Core の <see cref="FortressGarrisonRules.CanGarrison"/> ＝
        /// 艦隊メニューは理由を出すだけで再実装しない。
        /// <paramref name="shortReason"/>＝一覧の狭い列用の短縮形、<paramref name="reason"/>＝全文。
        /// </summary>
        public bool CanGarrisonAt(StrategicFleet fleet, int sysA, int sysB, out string shortReason, out string reason)
        {
            Corridor c = map != null ? map.GetCorridor(sysA, sysB) : null;
            Fortress f = c != null ? c.fortress : null;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            GarrisonRejection r = FortressGarrisonRules.CanGarrison(map, f, fleet, player);
            shortReason = FortressGarrisonRules.ShortRejectionText(r);
            reason = FortressGarrisonRules.RejectionText(r);
            return r == GarrisonRejection.なし;
        }

        /// <summary>2つの補足を「／」で繋ぐ（片方が空なら残ったほう・両方空なら空）。</summary>
        private static string JoinNotes(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? "";
            if (string.IsNullOrEmpty(b)) return a;
            return a + "／" + b;
        }

        /// <summary>
        /// いま交戦中の戦場（回廊）を <c>min*100000+max</c> のキーで返す（#38 援軍の宛先候補）。
        /// 艦隊メニューの「援軍を送る」がここから選ばせる＝MAP をクリックしなくても派遣できる。
        /// </summary>
        public List<int> EngagedBattlefields()
        {
            var result = new List<int>();
            if (reg == null || reg.fleets == null) return result;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                // 艦隊同士の交戦（engaged）に加えて、**要塞に足止めされている回廊も戦場に数える**（#40×#38）。
                // 要塞の固着は Core の blockadeCap が行い engaged を立てないので、これを入れないと
                // 「要塞攻めに援軍を送る」ができない（実機QAで判明）。判定は IsBattlefieldFleet に集約。
                if (!IsBattlefieldFleet(f)) continue;

                int min = Mathf.Min(f.currentSystemId, f.destinationSystemId);
                int max = Mathf.Max(f.currentSystemId, f.destinationSystemId);
                int key = min * 100000 + max;
                if (!result.Contains(key)) result.Add(key);
            }
            return result;
        }

        /// <summary>単クリックの選択処理（最寄り艦隊を選択／空クリックで解除・Shiftで追加トグル）。ダブルクリック判定は呼び出し側。</summary>
        private void SelectAtClick(Vector2 w)
        {
            bool additive = ShiftHeld();

            // #戦略MAPの艦艇表示：畳まれたまとまりを押したら、まず「短い艦隊一覧」を開く。
            // ここで個別に選べるようにしないと、集約した艦隊を選択して進軍させる手段が無くなる。
            if (!additive && TryOpenClusterList()) return;

            FleetClusterListPanel.Hide();
            StrategicFleet nf = NearestFleet(w, 0.7f);
            if (nf != null)
            {
                if (additive) { if (!selectedFleets.Remove(nf)) selectedFleets.Add(nf); }
                else { selectedFleets.Clear(); selectedFleets.Add(nf); }
            }
            else if (!additive) selectedFleets.Clear();
        }

        /// <summary>
        /// クリック位置に「畳まれたまとまり」があれば一覧を開く（開いたら true）。
        /// 一覧の行を押すとその艦隊が選択され、以後は従来どおり右クリックで進軍できる。
        /// </summary>
        private bool TryOpenClusterList()
        {
            if (cam == null || badgeLayer == null || Mouse.current == null) return false;

            // ★描いた矩形で判定する（実機QA：マーカーとクリック判定が 600px ずれていた）。
            // バッジは画面空間に描かれているので、同じ矩形を当てれば「見えている場所を押せば開く」が保証される。
            Vector2 screen = Mouse.current.position.ReadValue();
            if (!badgeLayer.TryHit(screen, out FleetCluster best) || best == null) return false;

            // 名前と「兵力/状態」を別の欄に分ける＝名前が長くて省略されても識別に必要な情報が消えない。
            var names = new List<string>(best.fleetIds.Count);
            var details = new List<string>(best.fleetIds.Count);
            for (int i = 0; i < best.fleetIds.Count; i++)
            {
                StrategicFleet f = FindFleetById(best.fleetIds[i]);
                if (f == null) { names.Add($"第{best.fleetIds[i]}艦隊"); details.Add("不明"); continue; }
                string corps = f.isCorpsFlagship ? "・旗艦" : "";
                names.Add($"第{f.id}艦隊{corps}");
                string state = f.engaged ? "交戦中" : (f.IsMoving ? $"航行 {f.Eta:F1}" : "停泊");
                details.Add($"{f.Ships:N0}隻　{state}");
            }

            string title = $"{best.faction}　{FleetClusterRules.MarkerLabel(best)}";
            Vector2 screenPos = cam.WorldToScreenPoint(best.center);
            openedCluster = best;

            FleetClusterListPanel.Show(title, names, details, best.fleetIds, screenPos, cam.rect, PickFleetFromList);
            return true;
        }

        /// <summary>一覧の行が押されたときの選択（以後は右クリックで進軍＝既存の操作をそのまま使う）。</summary>
        private void PickFleetFromList(int fleetId)
        {
            StrategicFleet f = FindFleetById(fleetId);
            openedCluster = null;
            if (f == null) return;
            selectedFleets.Clear();
            selectedFleets.Add(f);
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報,
                $"第{f.id}艦隊を選択しました（盤面を右クリックで進軍）");
        }

        /// <summary>id から戦略艦隊を引く。</summary>
        private StrategicFleet FindFleetById(int id)
        {
            if (reg == null || reg.fleets == null) return null;
            for (int i = 0; i < reg.fleets.Count; i++)
                if (reg.fleets[i] != null && reg.fleets[i].id == id) return reg.fleets[i];
            return null;
        }

        // ===== 矩形選択（マーキー・左ドラッグ） =====

        /// <summary>左ドラッグ中：押下点→現在カーソルの矩形をワールド座標で枠表示する。</summary>
        private void UpdateMarquee(Vector2 curScreen)
        {
            Vector3 a = ScreenToWorldAt(leftPressScreen);
            Vector3 b = ScreenToWorldAt(curScreen);
            if (marqueeLine == null)
            {
                marqueeLine = NewLine("Marquee", 5); // 艦隊(4)より前面
                marqueeLine.startWidth = marqueeLine.endWidth = 0.06f;
                marqueeLine.loop = false;
            }
            marqueeLine.positionCount = 5;
            marqueeLine.SetPositions(new[]
            {
                new Vector3(a.x, a.y, 0f), new Vector3(b.x, a.y, 0f),
                new Vector3(b.x, b.y, 0f), new Vector3(a.x, b.y, 0f),
                new Vector3(a.x, a.y, 0f),
            });
            marqueeLine.startColor = marqueeLine.endColor = marqueeColor;
            marqueeLine.enabled = true;
        }

        /// <summary>左ドラッグ確定：矩形内の艦隊をすべて選択（Shift で現在の選択に追加）。</summary>
        private void DoMarqueeSelect(Vector2 curScreen)
        {
            Vector3 a = ScreenToWorldAt(leftPressScreen);
            Vector3 b = ScreenToWorldAt(curScreen);
            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);

            if (!ShiftHeld()) selectedFleets.Clear();
            if (reg == null) return;
            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                Vector2 p = FleetWorldPos(f);
                if (p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY && !selectedFleets.Contains(f))
                    selectedFleets.Add(f);
            }
        }

        /// <summary>矩形選択の枠を消す（ドラッグ終了時）。</summary>
        private void ClearMarquee()
        {
            if (marqueeLine != null) marqueeLine.enabled = false;
        }

        /// <summary>
        /// マウスホイールでカーソル中心ズーム。スクロール量に比例して目標ズームを<b>倍率</b>で更新し
        /// （回し幅が大きいほど一気に＝指数スケール）、毎フレーム目標へ滑らかに追従させる（カクつかない）。
        /// </summary>
        private void HandleZoom()
        {
            if (!zoomInit) { zoomTarget = cam.orthographicSize; zoomInit = true; }

            // UI（星系図/決裁デスク/通知/メニュー等）の上ではホイールを読まない（二重ズーム防止）。進行中の追従は継続。
            float raw = PointerOverUI() ? 0f : Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(raw) > 0.01f)
            {
                // スクロール値はプラットフォームで ±120 段（OS標準）や ±1 段などスケールが違う。ノッチ単位へ正規化＝
                // どの環境でも「1ノッチ＝zoomPerNotch ぶん」になり、速い回転（複数ノッチ）は指数で加速する。
                float notches = Mathf.Abs(raw) >= 10f ? raw / 120f : raw;
                float factor = Mathf.Pow(1f - Mathf.Clamp(zoomPerNotch, 0.01f, 0.9f), notches);
                zoomTarget = Mathf.Clamp(zoomTarget * factor, minZoom, maxZoom);
                zoomAnchorScreen = Mouse.current.position.ReadValue(); // この位置を中心に保つ
                anchoredZoom = true;                                  // ホイールのときだけカーソル中心補正を有効化
            }
            ApplyZoomLerp();
        }

        /// <summary>
        /// 現在のズームを目標へ滑らかに寄せる。ホイールのときだけ「カーソル下のワールド点を固定」する補正を掛ける。
        ///
        /// ★全体表示やボタンのズームでは補正を掛けない（実機レビュー指摘）。
        /// これらは <c>zoomTarget</c>/<c>panTarget</c> を自分で決めるのに <c>zoomAnchorScreen</c> は古いままなので、
        /// 補正が働くと「昔カーソルがあった点」を固定しようとしてカメラが毎フレーム引きずられ、
        /// 全体表示を押した後に銀河がじりじり画面外へ流れていた。
        /// </summary>
        private void ApplyZoomLerp()
        {
            if (cam == null) return;
            float cur = cam.orthographicSize;
            if (Mathf.Abs(cur - zoomTarget) < 0.0005f)
            {
                cam.orthographicSize = zoomTarget;
                anchoredZoom = false;   // 到達したら補正モードを解く
                return;
            }

            if (!anchoredZoom)
            {
                // 中心を動かさずに縮尺だけ寄せる（フィット/ボタン）。パン目標は SmoothPan が担う。
                float tf = 1f - Mathf.Exp(-zoomLerpSpeed * Mathf.Max(0.0001f, Time.unscaledDeltaTime));
                cam.orthographicSize = Mathf.Clamp(Mathf.Lerp(cur, zoomTarget, tf), minZoom, maxZoom);
                ClampCameraPan();
                return;
            }

            Vector3 worldBefore = ScreenToWorldAt(zoomAnchorScreen);
            float t = 1f - Mathf.Exp(-zoomLerpSpeed * Mathf.Max(0.0001f, Time.unscaledDeltaTime)); // フレーム非依存の指数追従
            float next = Mathf.Lerp(cur, zoomTarget, t);
            cam.orthographicSize = Mathf.Clamp(next, minZoom, maxZoom);
            Vector3 worldAfter = ScreenToWorldAt(zoomAnchorScreen);
            Vector3 shift = worldBefore - worldAfter; // 中心点を画面上で固定（カーソル下を維持）
            cam.transform.position += shift;
            if (!panInit) { panTarget = cam.transform.position; panInit = true; }
            panTarget += shift; // パン目標も同量ずらす＝滑らか追従がズーム補正を打ち消さない
            panTarget.x = Mathf.Clamp(panTarget.x, -panLimit, panLimit);
            panTarget.y = Mathf.Clamp(panTarget.y, -panLimit, panLimit);
            ClampCameraPan();
        }

        /// <summary>スクリーン座標→ワールド座標（カメラ rect を尊重）。ズーム中心の固定に使う。</summary>
        private Vector3 ScreenToWorldAt(Vector2 screen)
            => cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

        /// <summary>入力欄（TMP/uGUI の InputField）にフォーカスがあるか。キー操作を打鍵と取り違えないための判定。</summary>
        private static bool IsTextInputFocused()
        {
            var es = EventSystem.current;
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            return sel != null && (sel.GetComponent<TMPro.TMP_InputField>() != null
                                || sel.GetComponent<UnityEngine.UI.InputField>() != null);
        }

        private static PointerEventData _uiPointer;
        private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        /// <summary>
        /// カーソルが UI（raycast を受けるパネル/ボタン）の上にあるか。マップ操作をそのUIに譲る判定。
        /// 新 Input System では <c>IsPointerOverGameObject()</c> がフレーム/モジュール依存で不安定なため、
        /// 毎回その場で <see cref="EventSystem.RaycastAll"/> して確実に判定する（決裁デスク等のドラッグと二重反応しない）。
        /// </summary>
        private static bool PointerOverUI()
        {
            var es = EventSystem.current;
            if (es == null || Mouse.current == null) return false;
            if (_uiPointer == null) _uiPointer = new PointerEventData(es);
            _uiPointer.position = Mouse.current.position.ReadValue();
            _uiHits.Clear();
            es.RaycastAll(_uiPointer, _uiHits);
            return _uiHits.Count > 0;
        }

        /// <summary>
        /// 会戦へ潜行する（WIN-1 #2568）。GameSettings.windowedBattles なら会戦をウィンドウで開き、
        /// 戦略マップを背後に残す。OFF なら従来どおり全画面の Battle シーンへ遷移する。
        /// 呼び出し前に BattleHandoff を Queue 済みであること（各 TryDescend* が行う）。
        /// </summary>
        private void LaunchBattleScene()
        {
            StampBattleCommandAuthority();   // #67：この会戦で何を直接動かせるかを決めて持ち込む
            if (GameSettings.Instance != null && GameSettings.Instance.windowedBattles)
                BattleDirector.Open();   // WIN-3：複数同時会戦の司令塔が窓を開く
            else
                SceneManager.LoadScene("Battle");
        }

        /// <summary>
        /// 戦役から潜行する会戦へ、プレイヤーの<b>実際の指揮権</b>を持ち込む（GitHub #67）。
        ///
        /// 全軍を直接動かせるのは<b>国家規模の軍事所掌</b>（総司令官）を持つときだけ。
        /// それ以外は指揮する軍団の隷下と自分の乗艦に限られる。
        /// 主人公を特定できなくても<b>全権限へ落とさない</b>＝役職が無ければ自艦隊のみになる。
        /// フルスクリーン会戦では Strategy が破棄されるので、ここで <see cref="BattleHandoff"/> へ載せる。
        /// </summary>
        private void StampBattleCommandAuthority()
        {
            BattleHandoff.FromCampaign = true;   // 戦略から潜行した＝戦役モード

            Person actor = PlayerCharacter();
            if (actor == null)
            {
                BattleHandoff.PlayerCommandsWholeFleet = false;
                BattleHandoff.PlayerCorpsName = "";
                return;
            }

            // 国家規模の軍事所掌（または元首）を持つか＝全軍の指揮権。
            BattleHandoff.PlayerCommandsWholeFleet = OfficeRules.CanPropose(
                GovernmentRegistry.GetOffices(actor), OfficeDomain.軍事, OfficeScope.国家);

            // 指揮する軍団＝その人物が司令を務める戦略艦隊の軍団。
            BattleHandoff.PlayerCorpsName = CorpsCommandedBy(actor);
        }

        /// <summary>その人物が司令を務める艦隊の軍団名（無ければ空）。</summary>
        private string CorpsCommandedBy(Person actor)
        {
            if (actor == null || reg?.fleets == null) return "";
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.commanderPersonId != actor.id) continue;
                if (!string.IsNullOrEmpty(f.corpsName)) return f.corpsName;
            }
            return "";
        }

        /// <summary>
        /// マウスの当フレーム移動量ぶん、<b>掴んだ地図を指の向きへ動かす</b>（掴んだ点がカーソルに付いてくるグラブ方式）。
        /// スクリーン差分→ワールド距離へ換算（カメラ rect のビューポート高で正規化）。パン目標を動かし cam は滑らかに追従。
        /// </summary>
        private void ScrollViewByMouseDelta()
        {
            Vector2 sd = Mouse.current.delta.ReadValue(); // 当フレームのスクリーン移動量（ピクセル）
            if (sd == Vector2.zero) return;
            float vpH = Screen.height * Mathf.Max(0.0001f, cam.rect.height); // ビューポート（窓）の高さ（ピクセル）
            float worldPerPixel = (cam.orthographicSize * 2f) / Mathf.Max(1f, vpH);
            // 符号はマイナス＝カメラはドラッグと逆へ動く→地図（中身）が指に付いてくる（グラブ＝直感的な向き）。
            MovePanTarget(new Vector3(-sd.x * worldPerPixel, -sd.y * worldPerPixel, 0f));
        }

        /// <summary>
        /// 銀河全体が収まるよう視点を合わせる（#戦略MAP刷新・全体表示ボタン／起動時／セーブ読み込み後）。
        /// マップ窓の実アスペクト（<see cref="Camera.rect"/> 反映済み）で必要なズームを出すので、
        /// 窓をリサイズしても押し直せば全体が入る。<paramref name="instant"/>=true は補間せず即時。
        /// </summary>
        public void FitAll(bool instant = false)
        {
            if (cam == null || map == null || map.systems == null || map.systems.Count == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            int counted = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null) continue;
                Vector2 q = s.position;
                if (q.x < minX) minX = q.x;
                if (q.x > maxX) maxX = q.x;
                if (q.y < minY) minY = q.y;
                if (q.y > maxY) maxY = q.y;
                counted++;
            }
            if (counted == 0) return;

            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            float halfW = (maxX - minX) * 0.5f + fitMargin;
            float halfH = (maxY - minY) * 0.5f + fitMargin;
            float aspect = Mathf.Max(0.05f, cam.aspect);
            float needed = Mathf.Max(halfH, halfW / aspect); // orthographicSize は「縦の半分」

            zoomTarget = Mathf.Clamp(needed, minZoom, maxZoom);
            zoomInit = true;
            // ★カーソル中心の補正を切る（実機レビュー指摘）。古い zoomAnchorScreen を基準に
            // 補正が働くと、全体表示のあとカメラがじりじり流れて銀河が画面外へ出ていく。
            anchoredZoom = false;
            panTarget = new Vector3(
                Mathf.Clamp(center.x, -panLimit, panLimit),
                Mathf.Clamp(center.y, -panLimit, panLimit),
                cam.transform.position.z);
            panInit = true;
            panVelocity = Vector3.zero;

            if (instant)
            {
                cam.orthographicSize = zoomTarget;
                cam.transform.position = panTarget;
            }
        }

        /// <summary>
        /// レイアウトが変わったので、<b>次フレーム以降</b>に全体表示を確定するよう予約する。
        /// camera.rect を入れ替えた直後は <see cref="Camera.aspect"/> がまだ古く、その場で
        /// フィットすると誤った縦横比で縮尺が決まる（実機QA：解像度切替直後だけ端の星系が切れた）。
        /// </summary>
        public void RequestFitAfterLayout() => pendingFitFrames = 3;

        /// <summary>全体表示の基準ズームから1段寄る/引く（マップ窓のズームボタン）。</summary>
        public void NudgeZoom(float factor)
        {
            if (cam == null) return;
            if (!zoomInit) { zoomTarget = cam.orthographicSize; zoomInit = true; }
            anchoredZoom = false; // ボタンのズームは中心を動かさない（カーソル中心補正を使わない）
            zoomTarget = Mathf.Clamp(zoomTarget * Mathf.Max(0.05f, factor), minZoom, maxZoom);
        }

        /// <summary>パン目標を delta だけ動かしてクランプする（cam 本体は LateUpdate で滑らかに追従）。</summary>
        private void MovePanTarget(Vector3 delta)
        {
            if (cam == null) return;
            if (!panInit) { panTarget = cam.transform.position; panInit = true; }
            panTarget += delta;
            panTarget.x = Mathf.Clamp(panTarget.x, -panLimit, panLimit);
            panTarget.y = Mathf.Clamp(panTarget.y, -panLimit, panLimit);
            panTarget.z = cam.transform.position.z;
        }

        /// <summary>
        /// 出陣導線（執務机 →「出陣」ボタン）：プレイヤー勢力の艦隊を1つ選択し、その所在星系へ視点を寄せる。
        /// 主命「出陣」を拝命しても盤面で何をすればよいか分からない、を解消するための入口。
        /// 敵と回廊で接する前線の艦隊を優先する（そこから進軍すれば会戦になる）。
        /// 戻り値 false のとき <paramref name="reason"/> に理由、true のとき寄せた星系名が入る。
        /// </summary>
        public bool FocusOwnFleetForSortie(out string reason)
        {
            reason = "";
            if (map == null || reg == null) { reason = "戦略マップがまだ構築されていません"; return false; }

            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            StrategicFleet pick = null;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != pf) continue;
                if (pick == null) { pick = f; continue; }
                if (!IsFrontlineFleet(pick) && IsFrontlineFleet(f)) pick = f; // 前線を優先
            }
            if (pick == null) { reason = "いま指揮下にある艦隊がありません（昇進を待つ）"; return false; }

            selectedFleets.Clear();
            selectedFleets.Add(pick);

            StarSystem s = map.GetSystem(pick.currentSystemId);
            if (s != null && cam != null)
            {
                panInit = true;
                panTarget = new Vector3(
                    Mathf.Clamp(s.position.x, -panLimit, panLimit),
                    Mathf.Clamp(s.position.y, -panLimit, panLimit),
                    cam.transform.position.z);
            }
            reason = s != null ? s.systemName : "";
            return true;
        }

        /// <summary>その艦隊の所在星系が他勢力と回廊で接しているか（＝進軍すれば会戦になりうる前線）。</summary>
        private bool IsFrontlineFleet(StrategicFleet f)
        {
            if (f == null || map == null) return false;
            StarSystem home = map.GetSystem(f.currentSystemId);
            if (home == null) return false;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                var c = map.corridors[i];
                int other = c.aId == home.id ? c.bId : (c.bId == home.id ? c.aId : -1);
                if (other < 0) continue;
                StarSystem os = map.GetSystem(other);
                if (os != null && os.owner != home.owner) return true;
            }
            return false;
        }

        /// <summary>パン目標へカメラを滑らかに追従させる（SmoothDamp・unscaled）。LateUpdate から毎フレーム呼ぶ。</summary>
        private void SmoothPan()
        {
            if (cam == null || !panInit) return;
            if (panSmoothTime <= 0.0001f) { cam.transform.position = panTarget; panVelocity = Vector3.zero; return; }
            cam.transform.position = Vector3.SmoothDamp(
                cam.transform.position, panTarget, ref panVelocity, panSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        /// <summary>WASD／矢印キーで視点を連続移動（押しっぱで動く・ズーム連動）。</summary>
        private void HandleKeyPan(Keyboard kb)
        {
            if (kb == null || cam == null) return;
            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
            if (dir == Vector2.zero) return;

            float speedMul = cam.orthographicSize / 10f;
            MovePanTarget((Vector3)(dir.normalized * keyPanSpeed * speedMul * Time.unscaledDeltaTime));
        }

        /// <summary>F：カメラを既定のズーム/位置へ戻す（#2384）。</summary>
        private void ResetView()
        {
            if (cam == null) return;
            float z = Mathf.Clamp(defaultZoom, minZoom, maxZoom);
            cam.orthographicSize = z;
            zoomTarget = z; zoomInit = true; // 滑らかズームの目標も既定へ（追従で戻されないように）
            cam.transform.position = new Vector3(0f, 0f, -10f);
            panTarget = cam.transform.position; panVelocity = Vector3.zero; panInit = true; // パン目標も既定へ
        }

        /// <summary>カメラ中心を ±panLimit でクランプ（迷子防止）。z は維持。</summary>
        private void ClampCameraPan()
        {
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            p.x = Mathf.Clamp(p.x, -panLimit, panLimit);
            p.y = Mathf.Clamp(p.y, -panLimit, panLimit);
            cam.transform.position = p;
        }

        /// <summary>
        /// クリック位置に交戦中の回廊があれば、その会戦へ潜行（実会戦・Battleシーン）する（#586 ①）。
        /// 潜行＝手動指揮。戻ると結果が反映され、観ていなかった他戦線は自動解決される。
        /// </summary>
        private bool TryDescend(Vector2 w)
        {
            if (!NearestCorridor(w, out Corridor c, out _, out float d) || d > 0.6f) return false;
            // #40：要塞に釘付けにされた自軍がいる回廊なら、要塞戦の戦術マップへ入る（艦隊同士の会戦より優先）。
            if (DescendFortressCorridor(c)) return true;
            return DescendCorridorBySystems(c.aId, c.bId);
        }

        /// <summary>
        /// 回廊要塞の戦術マップへ潜行する（#40）。要塞の手前に釘付けになっている艦隊がある回廊でだけ成立し、
        /// 岩壁に挟まれた水路＋中央の要塞という戦術マップ（<see cref="CorridorFortressArena"/>）へ入る。
        /// 突入した兵力は戦術側の艦隊規模になり、戻ると突破の成否が回廊へ書き戻される。
        /// </summary>
        public bool DescendFortressCorridor(Corridor c)
        {
            if (reg == null || c == null || c.fortress == null) return false;
            if (SceneManager.GetActiveScene().name != "Strategy") return false;
            if (!FortressRules.BlocksPassage(c.fortress)) return false;

            // 釘付けになっている自勢力（プレイヤー）の艦隊を集める＝突入できるのは足止めされている側だけ。
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            if (!StrategyRules.IsFortressBlocked(c, player)) return false;

            // 突入する自軍艦隊を<b>1隊ずつ明細で</b>運ぶ。合計だけ渡すと、戻ってきたときに
            // 損害を全隊へ按分するしかなくなり、無傷の隊と全滅した隊が同じ扱いになってしまう。
            var entries = new List<BattleHandoff.HandoffFleet>();
            int total = 0;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || !f.IsOnCorridor || f.faction != player) continue;
                if (Mathf.Min(f.currentSystemId, f.destinationSystemId) != Mathf.Min(c.aId, c.bId)) continue;
                if (Mathf.Max(f.currentSystemId, f.destinationSystemId) != Mathf.Max(c.aId, c.bId)) continue;
                if (f.strength <= 0) continue;
                entries.Add(new BattleHandoff.HandoffFleet
                {
                    faction = f.faction, strategicStrength = f.strength, fleetId = f.id,
                    loyalty = 1f, intrigue = 0f, quality = 1f, sideA = true,
                });
                total += f.strength;
            }
            if (entries.Count == 0 || total <= 0) return false;

            BattleHandoff.fleets.Clear();
            BattleHandoff.fleets.AddRange(entries);

            BattleHandoff.Pending = true;
            BattleHandoff.Resolved = false;
            BattleHandoff.IsCorridorFortress = true;
            BattleHandoff.IsPlanetSiege = false;
            BattleHandoff.IsSystemView = false;
            BattleHandoff.returnScene = "Strategy";
            BattleHandoff.fortressCorridorA = c.aId;
            BattleHandoff.fortressCorridorB = c.bId;
            BattleHandoff.fortressName = c.fortress.fortressName;
            BattleHandoff.fortressOwner = c.fortress.owner;
            BattleHandoff.fortressAttacker = player;
            BattleHandoff.fortressGarrison = c.fortress.garrisonStrength;  // 施設側の守備値（砲台・要塞兵）
            BattleHandoff.fortressShield = c.fortress.shieldIntegrity;

            // ★駐留艦隊（#40）を守備側の明細として積む＝<b>実在の艦隊がそのまま守備に並ぶ</b>。
            // 匿名の守備艦隊を毎回生成しない。損害は会戦後にその艦隊へ返る（艦隊ごとに独立）。
            var garrison = FortressGarrisonRules.GarrisonFleets(c.fortress, reg);
            for (int i = 0; i < garrison.Count; i++)
            {
                StrategicFleet g = garrison[i];
                if (g == null || g.strength <= 0) continue;
                BattleHandoff.fleets.Add(new BattleHandoff.HandoffFleet
                {
                    faction = g.faction, strategicStrength = g.strength, fleetId = g.id,
                    loyalty = 1f, intrigue = 0f, quality = 1f, sideA = false,   // 守備側
                });
            }
            BattleHandoff.besiegerStrength = total;
            BattleHandoff.battleLabel =
                $"{c.fortress.fortressName}（{SystemName(c.aId)}–{SystemName(c.bId)} 回廊）";

            // #38：要塞戦も「進行中の戦場」＝援軍の宛先になる。これを設定しないと
            // BattleManager.TakeArrivedReinforcements が宛先を引けず、援軍が永久に到着しない。
            BattleHandoff.battlefield = BattlefieldKey.Corridor(c.aId, c.bId);
            StrategySession.Reinforcements?.ReopenBattlefield(BattleHandoff.battlefield);

            LaunchBattleScene();
            return true;
        }

        /// <summary>指定回廊（両端の星系ID）の要塞戦へ潜行する（通知のダブルクリックから呼ぶ）。</summary>
        public bool DescendFortressBySystems(int sysA, int sysB)
            => map != null && DescendFortressCorridor(map.GetCorridor(sysA, sysB));

        /// <summary>
        /// 援軍を送る（#38 C-5・Alt+A）。<b>カーソル下の交戦中の回廊</b>へ、選択中の自軍艦隊を派遣する。
        /// 到着は即時ではなく、回廊の距離と艦隊のワープ速度ぶんの銀河時間がかかる＝予備を投入する
        /// タイミングそのものが判断になる。既に交戦している艦隊・航行中の援軍は送れない。
        /// </summary>
        private void OpenFleetMenuForReinforcement()
        {
            if (cam == null || map == null || reg == null) return;

            // カーソル下が交戦中の回廊なら、その戦場を宛先候補として開く（一覧から選び直せる）。
            Vector2 w = WorldMouse();
            if (NearestCorridor(w, out Corridor c, out _, out float d) && d <= 0.9f && IsEngagedCorridor(c))
                FleetOrderPanel.ShowForReinforcement(c.aId, c.bId);
            else
                FleetOrderPanel.ShowForReinforcement();
        }

        /// <summary>
        /// 指定回廊（星系 sysA–sysB）上の交戦中ペアへ潜行する（接敵通知のダブルクリックからも呼ぶ）。
        /// その回廊に交戦が無ければ（既に決着等）false。戦略シーン以外では何もしない（stale 起動の保険）。
        /// </summary>
        public bool DescendCorridorBySystems(int sysA, int sysB)
        {
            if (reg == null) return false;
            if (SceneManager.GetActiveScene().name != "Strategy") return false;
            if (!StrategyRules.TryGetEngagementOnCorridor(reg, sysA, sysB, out var a, out var b)) return false;
            return DescendOnEngagement(a, b);
        }

        /// <summary>
        /// 交戦中ペア a/b の会戦へ潜行（Battle シーンへ）。旗幟・軍の質を積んで受け渡す。
        /// 同じ回廊で<b>複数艦隊</b>が交戦している場合は全艦隊を集めて会戦へ持ち込む（接敵内容を会戦へ合わせる）。
        /// </summary>
        private bool DescendOnEngagement(StrategicFleet a, StrategicFleet b)
        {
            if (a == null || b == null) return false;

            // 会戦ウィンドウの見出し（WIN-3 #2570）：戦っている回廊（星系間）の名前。
            BattleHandoff.battleLabel = $"{SystemName(a.currentSystemId)}–{SystemName(a.destinationSystemId)} 回廊";
            // #38：この会戦の戦場キー。援軍台帳はこれで戦場を厳密に分ける（別の会戦へ紛れ込まない）。
            BattleHandoff.battlefield = BattlefieldKey.Corridor(a.currentSystemId, a.destinationSystemId);
            StrategySession.Reinforcements?.ReopenBattlefield(BattleHandoff.battlefield);

            // 同一回廊上で交戦中の全艦隊を2陣営に集める。3隊以上なら複数艦隊モードで会戦へ。
            var sideA = new List<StrategicFleet>();
            var sideB = new List<StrategicFleet>();
            if (StrategyRules.GatherEngagementOnCorridor(reg, a.currentSystemId, a.destinationSystemId, sideA, sideB)
                && (sideA.Count + sideB.Count) > 2)
            {
                var entries = new List<BattleHandoff.HandoffFleet>();
                AddHandoffSide(entries, sideA, true);
                AddHandoffSide(entries, sideB, false);
                BattleHandoff.QueueMulti(entries, sideA[0].faction, sideB[0].faction, sideA[0].id, sideB[0].id, "Strategy");
                LaunchBattleScene();
                return true;
            }

            BattleHandoff.Queue(a, b, "Strategy");

            // 旗幟（#817）：国家状態から基準忠誠/調略の付け入りやすさを積む＝腐った国の艦隊は会戦中に寝返りうる。
            // ただし旗幟が揺らぐのは「国家が実質崩壊」したときだけ（HandoffLoyalty）＝通常の会戦が寝返り/静観で
            // 一瞬で終わるのを防ぐ（時間圧縮で国家状態が劣化しても常用では発火しない）。
            var campaign = StrategySession.Campaign;
            if (campaign != null)
            {
                FactionState sa = CampaignRules.GetState(campaign, a.faction);
                FactionState sb = CampaignRules.GetState(campaign, b.faction);
                if (sa != null) HandoffLoyalty(sa, out BattleHandoff.loyaltyA, out BattleHandoff.intrigueA);
                if (sb != null) HandoffLoyalty(sb, out BattleHandoff.loyaltyB, out BattleHandoff.intrigueB);
            }

            // 軍の質（C4）：降下する艦隊の補給（弾薬即応）を戦闘力倍率へ＝干上がった艦隊は会戦で弱い。
            // 下士官団/新兵練度はユニット未attribute（#210）ゆえ既定（null/0.5中立）。補給×技術を質倍率へ織り込む（自動解決と一貫）。
            BattleHandoff.qualityA = ForceQualityRules.CombatMultiplier(null, 0.5f, MilitaryReadinessRules.FirepowerFactor(a.supply)) * TechEffectRules.CombatStrengthFactor(TechLevelOf(a.faction));
            BattleHandoff.qualityB = ForceQualityRules.CombatMultiplier(null, 0.5f, MilitaryReadinessRules.FirepowerFactor(b.supply)) * TechEffectRules.CombatStrengthFactor(TechLevelOf(b.faction));

            LaunchBattleScene();
            return true;
        }

        /// <summary>
        /// 複数艦隊モード用：1陣営ぶんの戦略艦隊を会戦の明細（<see cref="BattleHandoff.HandoffFleet"/>）へ変換して積む。
        /// 旗幟（#817 国家状態の基準忠誠/調略）と軍の質（C4 補給×技術）を1隊ごとに織り込む（単隊潜行と一貫）。
        /// </summary>
        private void AddHandoffSide(List<BattleHandoff.HandoffFleet> entries, List<StrategicFleet> side, bool isSideA)
        {
            var campaign = StrategySession.Campaign;
            for (int i = 0; i < side.Count; i++)
            {
                StrategicFleet f = side[i];
                if (f == null) continue;

                float loyalty = 1f, intrigue = 0f;
                if (campaign != null)
                {
                    FactionState fs = CampaignRules.GetState(campaign, f.faction);
                    if (fs != null) HandoffLoyalty(fs, out loyalty, out intrigue);
                }
                float quality = ForceQualityRules.CombatMultiplier(null, 0.5f, MilitaryReadinessRules.FirepowerFactor(f.supply))
                                * TechEffectRules.CombatStrengthFactor(TechLevelOf(f.faction));

                entries.Add(new BattleHandoff.HandoffFleet
                {
                    faction = f.faction,
                    strategicStrength = f.strength,
                    admiral = null,
                    fleetId = f.id,
                    loyalty = loyalty,
                    intrigue = intrigue,
                    quality = quality,
                    sideA = isSideA,
                });
            }
        }

        /// <summary>会戦で旗幟（寝返り/静観）が揺らぐ国家崩壊のしきい値（基準忠誠＝正統性/結束/希望の平均）。
        /// これ以上に健全なら艦隊は素直に戦う＝旗幟は実質崩壊時のみの演出（常用での暴発＝会戦の即終了を防ぐ）。</summary>
        private const float CollapseLoyaltyThreshold = 0.3f;

        /// <summary>
        /// 国家状態から会戦へ渡す旗幟（基準忠誠/調略浸透）を導く。<b>国家が実質崩壊（基準忠誠が極端に低い）</b>
        /// したときだけ寝返り/静観が起こり、通常〜やや不安定な国は素直に戦う（loyalty=1/intrigue=0＝旗幟ドーマント）。
        /// 暦の時間圧縮で国家状態が劣化しても、会戦が寝返り/静観で「一瞬」で終わらないようにする
        /// （#817 関ヶ原型の意図＝腐敗で戦う前に決まる は保ちつつ、常用での暴発を抑える）。
        /// </summary>
        private static void HandoffLoyalty(FactionState s, out float loyalty, out float intrigue)
        {
            float baseL = FactionLoyaltyRules.BaselineLoyalty(s);
            if (baseL < CollapseLoyaltyThreshold)
            {
                loyalty = baseL;
                intrigue = FactionLoyaltyRules.BribeSusceptibility(s);
            }
            else
            {
                loyalty = 1f;   // 崩壊未満は確実に戦う＝旗幟は揺らがない（純忠誠 > fightThreshold）
                intrigue = 0f;
            }
        }

        /// <summary>
        /// クリック位置の星系で停泊艦隊どうしが交戦中（惑星上などで接敵＝fleet-vs-fleet）なら、その会戦へ潜行する。
        /// 攻城突入より優先＝防衛艦隊が在席する惑星では、まず艦隊戦で守備を破ってから攻城に入る。
        /// </summary>
        private bool TryDescendSystemBattle(Vector2 w)
        {
            if (reg == null) return false;
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > ClickRadiusFor(sysId)) return false;
            return DescendSystemBattleBySystem(sysId);
        }

        /// <summary>
        /// 指定星系で停泊艦隊どうしが交戦中なら、その会戦（fleet-vs-fleet）へ潜行する（接敵通知のダブルクリックからも呼ぶ）。
        /// 交戦が無ければ false。戦略シーン以外では何もしない（stale 起動の保険）。
        /// </summary>
        public bool DescendSystemBattleBySystem(int systemId)
        {
            if (reg == null) return false;
            if (SceneManager.GetActiveScene().name != "Strategy") return false;

            var sideA = new List<StrategicFleet>();
            var sideB = new List<StrategicFleet>();
            if (!StrategyRules.GatherEngagementAtSystem(reg, systemId, sideA, sideB)) return false;

            var entries = new List<BattleHandoff.HandoffFleet>();
            AddHandoffSide(entries, sideA, true);
            AddHandoffSide(entries, sideB, false);
            BattleHandoff.QueueMulti(entries, sideA[0].faction, sideB[0].faction, sideA[0].id, sideB[0].id, "Strategy");

            // 初期配置：星系の所有勢力＝守備側（中央）／他方＝攻撃側（侵攻方向へ）。所有勢力が交戦当事者のときだけ設定。
            StarSystem sys = map != null ? map.GetSystem(systemId) : null;
            if (sys != null && (sys.owner == sideA[0].faction || sys.owner == sideB[0].faction))
                BattleHandoff.SetDefender(sys.owner, Vector2.right); // 侵攻方向は会戦アリーナ座標では一定（守備中央・攻撃側面）

            // #38：星系での会戦も戦場キーを持たせる（援軍の宛先）。
            BattleHandoff.battlefield = BattlefieldKey.System(systemId);
            StrategySession.Reinforcements?.ReopenBattlefield(BattleHandoff.battlefield);
            BattleHandoff.battleLabel = $"{SystemName(systemId)} 星系"; // 会戦ウィンドウの見出し（WIN-3）
            LaunchBattleScene();
            return true;
        }

        /// <summary>
        /// クリック位置の星系が敵の防衛惑星で、自軍が攻城中なら、惑星攻城の戦術マップ（Battleシーン）へ突入する（#131）。
        /// 中心に惑星・攻城艦隊が包囲・首飾り射程の外までの状態で開始する。
        /// </summary>
        private bool TryDescendPlanet(Vector2 w)
        {
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > ClickRadiusFor(sysId)) return false;
            StarSystem s = map.GetSystem(sysId);
            if (s == null || s.planet == null) return false;

            StrategicFleet besieger = FindBesieger(sysId, s.planet.owner);
            if (besieger == null) return false;

            float defRatio = s.planet.maxOrbitalDefense > 0f ? s.planet.orbitalDefense / s.planet.maxOrbitalDefense : 0f;
            float invRatio = s.planet.invasionThreshold > 0f ? s.planet.invasionProgress / s.planet.invasionThreshold : 0f;
            BattleHandoff.QueuePlanetSiege(s.id, s.systemName, s.planet.owner, defRatio, invRatio,
                besieger.faction, besieger.strength, "Strategy", s.planet.kind);
            BattleHandoff.battleLabel = $"{s.systemName} 攻防"; // 会戦ウィンドウの見出し（WIN-3・攻城）
            LaunchBattleScene();
            return true;
        }

        /// <summary>
        /// クリック位置に星系があれば、戦闘中でなくてもその星系の戦術マップ（システムビュー＝恒星系の閲覧）へ入る。
        /// 交戦回廊(TryDescend)・攻城突入(TryDescendPlanet)が優先で、どれにも該当しない平時の星系がここに来る。
        /// </summary>
        private bool TryEnterSystem(Vector2 w)
        {
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > ClickRadiusFor(sysId)) return false;
            StarSystem s = map.GetSystem(sysId);
            if (s == null) return false;
            // 全画面のシステムビュー（Battleシーン）へ遷移せず、その場で恒星系マップ窓を開く（非モーダル）。
            SystemMapWindow.Show(s.id, s.systemName, s.owner);
            return true;
        }

        /// <summary>指定星系に停泊し惑星所有者と敵対する艦隊（攻城側）を返す。選択中を優先、無ければ任意。</summary>
        private StrategicFleet FindBesieger(int sysId, Faction planetOwner)
        {
            for (int i = 0; i < selectedFleets.Count; i++)
            {
                StrategicFleet f = selectedFleets[i];
                if (f != null && !f.IsOnCorridor && f.currentSystemId == sysId &&
                    FactionRelations.IsHostile(null, f.faction, null, planetOwner)) return f;
            }
            foreach (var f in reg.FleetsAt(sysId))
                if (f != null && FactionRelations.IsHostile(null, f.faction, null, planetOwner)) return f;
            return null;
        }

        private Vector2 WorldMouse()
        {
            Vector3 sp = Mouse.current.position.ReadValue();
            return cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
        }

        private static bool ShiftHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        private StrategicFleet NearestFleet(Vector2 w, float radius)
        {
            StrategicFleet best = null; float bestD = radius;
            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                float d = Vector2.Distance(FleetWorldPos(f), w);
                if (d <= bestD) { bestD = d; best = f; }
            }
            return best;
        }

        /// <summary>最も近い星系IDとその距離を返す（無ければ -1）。</summary>
        private int NearestSystemDist(Vector2 w, out float dist)
        {
            int best = -1; dist = float.MaxValue;
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                float d = Vector2.Distance(s.position, w);
                if (d < dist) { dist = d; best = s.id; }
            }
            return best;
        }

        /// <summary>クリック点に最も近い回廊（線分）と、その上の位置 fracFromA（aId→bId で0..1）と距離を返す。</summary>
        private bool NearestCorridor(Vector2 w, out Corridor best, out float fracFromA, out float dist)
        {
            best = null; fracFromA = 0f; dist = float.MaxValue;
            foreach (var c in map.corridors)
            {
                StarSystem a = map.GetSystem(c.aId), b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;
                Vector2 pa = a.position, ab = b.position - a.position;
                float len2 = ab.sqrMagnitude;
                float t = (len2 > 0f) ? Mathf.Clamp01(Vector2.Dot(w - pa, ab) / len2) : 0f;
                float d = Vector2.Distance(w, pa + ab * t);
                if (d < dist) { dist = d; best = c; fracFromA = t; }
            }
            return best != null;
        }

        // ===== ヘルパ =====

    }
}
