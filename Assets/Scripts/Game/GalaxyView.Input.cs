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
            if (sysId < 0 || d > 1.2f) return;
            StarSystem s = map.GetSystem(sysId);
            if (s == null) return;
            provinces.TryGetValue(sysId, out var prov);
            SystemDetailPanel.Show(s, prov, map.Neighbors(sysId).Count, FleetSummaryAt(sysId));
        }

        /// <summary>星系に停泊中の戦略艦隊を勢力ごとに「N隊・兵力M」で要約する。</summary>
        private string FleetSummaryAt(int sysId)
        {
            var here = reg.FleetsAt(sysId);
            if (here == null || here.Count == 0) return "";
            var strengthByF = new Dictionary<Faction, int>();
            var countByF = new Dictionary<Faction, int>();
            for (int i = 0; i < here.Count; i++)
            {
                StrategicFleet f = here[i];
                if (f == null) continue;
                strengthByF.TryGetValue(f.faction, out int st); strengthByF[f.faction] = st + f.strength;
                countByF.TryGetValue(f.faction, out int c); countByF[f.faction] = c + 1;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in strengthByF)
                sb.AppendLine($"{kv.Key}：{countByF[kv.Key]}隊・兵力 {kv.Value}");
            return sb.ToString().TrimEnd();
        }

        // ===== デモ銀河 =====

        /// <summary>
        /// ESC の解決（#ウィンドウESC）：①重ねたウィンドウ（観測オーバーレイ・各パネル）を最前面から1枚閉じる。
        /// ②閉じる窓が無ければシステムメニュー（再開/セーブ/タイトル）を開閉する。
        /// </summary>
        private void HandleStrategyEscape()
        {
            if (UIWindowStack.CloseTopmost()) return;        // 手前のウィンドウを1枚閉じる
            // ESC で閉じない専用モーダル（イベント提示は選択が必要／終了画面は終端）の上にはシステムメニューを被せない。
            if (StrategyEventPanel.IsOpen || CampaignEndOverlay.IsOpen) return;
            StrategySystemMenu menu = Object.FindAnyObjectByType<StrategySystemMenu>();
            if (menu != null) menu.Toggle();                  // 無ければシステムメニュー開閉
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
            if (kb.fKey.wasPressedThisFrame) ResetView(); // F：既定のズーム/位置へ戻す（#2384）

            HandleKeyPan(kb); // ステラリス風：WASD/矢印キーで視点パン（押しっぱで連続）

            // 外交コマンド（#2119 操作化）：対立勢力へ 7=宣戦 / 8=講和 / 9=同盟。自勢力の外交はプレイヤーが握る。
            if (kb.digit7Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.宣戦布告);
            if (kb.digit8Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.講和);
            if (kb.digit9Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.同盟);

            // ミッションコマンド（任務戦術）：C＝マウス直下の敵対星系へ攻略任務／V＝対立勢力を攻略（参謀本部が目標選定・必要兵力を見積もり自動動員）。
            if (kb.cKey.wasPressedThisFrame) IssueMissionAtMouse();
            if (kb.vKey.wasPressedThisFrame) IssueCampaignAgainstRival();

            // セーブ/ロード（continue・全永続化）：F5=保存／F9=読込（読込後 Strategy を再ロードして再構築）。
            if (kb.f5Key.wasPressedThisFrame) SaveCampaign();
            if (kb.f9Key.wasPressedThisFrame) LoadCampaign();
        }

        private void HandleMouse()
        {
            if (Mouse.current == null || cam == null) return;

            HandleZoom(); // マウスホイール：カーソル中心ズーム（滑らかに追従・回し幅で加速）

            // いずれかの UI 窓（観測オーバーレイ/決裁デスク/通知/星系図等）をドラッグ中は、マップ操作を窓へ譲る
            // ＝窓を動かすと同時にマップがスクロールする問題の確実な解消（raycast 判定の取りこぼし対策の二重防御）。
            if (UIDragMove.AnyDragging) return;

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

            // 左ボタン：ドラッグで星系マップをスクロール（動かした向きに視点が動く）／小さく押して離せばクリック（選択/ダブルクリック）。
            // 確定は「離した時」＝スクロールと選択が競合しない。押し始めが UI 上なら一切マップに渡さない。
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                leftPressScreen = Mouse.current.position.ReadValue();
                leftDragging = false;
                leftPressOverUI = PointerOverUI();
            }
            else if (Mouse.current.leftButton.isPressed && !leftPressOverUI)
            {
                Vector2 cur = Mouse.current.position.ReadValue();
                if (!leftDragging && Vector2.Distance(cur, leftPressScreen) > dragThresholdPixels)
                    leftDragging = true;
                if (leftDragging) ScrollViewByMouseDelta();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (!leftPressOverUI && !leftDragging) DoLeftClick(WorldMouse()); // ドラッグでなければクリック確定
                leftDragging = false;
            }
            // 右クリック：クリックに近い方を採用。星系の点が近ければ進軍、回廊の線が近ければ
            // その位置で停止保持（端点に居る選択艦のみ）。UI 上では発令しない。
            if (Mouse.current.rightButton.wasPressedThisFrame && !PointerOverUI())
            {
                if (selectedFleets.Count == 0) return;
                Vector2 w = WorldMouse();

                int sysId = NearestSystemDist(w, out float sysD);
                bool hasCorr = NearestCorridor(w, out Corridor c, out float fracFromA, out float corrD);

                if (sysId >= 0 && sysD <= systemClickRadius)
                {
                    // 星系の点の上＝最優先で進軍。ハブ星系は放射状の回廊が中心を通るため
                    // 近さ比較だと回廊が勝って惑星へ入れない → 星系の判定半径を優先する。
                    foreach (var f in selectedFleets) if (f != null) f.WarpTo(map, sysId);
                }
                else if (hasCorr && corrD <= 0.6f)
                {
                    // 回廊の線上（星系から離れた位置）＝その位置で停止保持
                    foreach (var f in selectedFleets)
                    {
                        if (f == null) continue;
                        if (f.currentSystemId == c.aId) f.HoldOnCorridor(map, c.bId, fracFromA);
                        else if (f.currentSystemId == c.bId) f.HoldOnCorridor(map, c.aId, 1f - fracFromA);
                    }
                }
                else if (sysId >= 0 && sysD <= 1.6f)
                {
                    // 星系の近く（フォールバック）＝進軍
                    foreach (var f in selectedFleets) if (f != null) f.WarpTo(map, sysId);
                }
            }
        }

        /// <summary>左クリック確定（離した時に呼ぶ＝ドラッグと非競合）。ダブルクリックで潜行/突入/閲覧、単クリックで選択。</summary>
        private void DoLeftClick(Vector2 w)
        {
            // ダブルクリック判定（実時間・近接）→ 交戦回廊への潜行＞攻城突入＞平時の星系をシステムビューで閲覧
            float now = Time.realtimeSinceStartup;
            bool dbl = (now - lastClickTime <= doubleClickWindow) && Vector2.Distance(w, lastClickWorld) <= 0.6f;
            lastClickTime = now; lastClickWorld = w;
            if (dbl && (TryDescend(w) || TryDescendPlanet(w) || TryEnterSystem(w))) return;

            bool additive = ShiftHeld();
            StrategicFleet nf = NearestFleet(w, 0.7f);
            if (nf != null)
            {
                if (additive) { if (!selectedFleets.Remove(nf)) selectedFleets.Add(nf); }
                else { selectedFleets.Clear(); selectedFleets.Add(nf); }
            }
            else if (!additive) selectedFleets.Clear();
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
            }
            ApplyZoomLerp();
        }

        /// <summary>現在のズームを目標へ滑らかに寄せつつ、ズーム中心（最後のカーソル位置）のワールド点を固定する。</summary>
        private void ApplyZoomLerp()
        {
            if (cam == null) return;
            float cur = cam.orthographicSize;
            if (Mathf.Abs(cur - zoomTarget) < 0.0005f) { cam.orthographicSize = zoomTarget; return; }

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
            return DescendCorridorBySystems(c.aId, c.bId);
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

        /// <summary>交戦中ペア a/b の会戦へ潜行（Battle シーンへ）。旗幟・軍の質を積んで受け渡す。</summary>
        private bool DescendOnEngagement(StrategicFleet a, StrategicFleet b)
        {
            if (a == null || b == null) return false;
            BattleHandoff.Queue(a, b, "Strategy");

            // 旗幟（#817）：国家状態から基準忠誠/調略の付け入りやすさを積む＝腐った国の艦隊は会戦中に寝返りうる。
            var campaign = StrategySession.Campaign;
            if (campaign != null)
            {
                FactionState sa = CampaignRules.GetState(campaign, a.faction);
                FactionState sb = CampaignRules.GetState(campaign, b.faction);
                // intrigue（調略済み度）＝「既に敵に付け入られている度合い」。弱った国（基準忠誠<0.5）だけが
                // 事前浸透を抱える。健全〜中庸の国は intrigue=0 で素直に戦う＝全艦が静観して膠着するのを防ぐ
                // （以前は susceptibility をそのまま入れ、loyalty<0.75 の艦隊が全て静観して両軍膠着していた）。
                if (sa != null)
                {
                    float baseA = FactionLoyaltyRules.BaselineLoyalty(sa);
                    BattleHandoff.loyaltyA = baseA;
                    BattleHandoff.intrigueA = baseA < 0.5f ? FactionLoyaltyRules.BribeSusceptibility(sa) : 0f;
                }
                if (sb != null)
                {
                    float baseB = FactionLoyaltyRules.BaselineLoyalty(sb);
                    BattleHandoff.loyaltyB = baseB;
                    BattleHandoff.intrigueB = baseB < 0.5f ? FactionLoyaltyRules.BribeSusceptibility(sb) : 0f;
                }
            }

            // 軍の質（C4）：降下する艦隊の補給（弾薬即応）を戦闘力倍率へ＝干上がった艦隊は会戦で弱い。
            // 下士官団/新兵練度はユニット未attribute（#210）ゆえ既定（null/0.5中立）。
            BattleHandoff.qualityA = ForceQualityRules.CombatMultiplier(null, 0.5f, MilitaryReadinessRules.FirepowerFactor(a.supply));
            BattleHandoff.qualityB = ForceQualityRules.CombatMultiplier(null, 0.5f, MilitaryReadinessRules.FirepowerFactor(b.supply));

            SceneManager.LoadScene("Battle");
            return true;
        }

        /// <summary>
        /// クリック位置の星系が敵の防衛惑星で、自軍が攻城中なら、惑星攻城の戦術マップ（Battleシーン）へ突入する（#131）。
        /// 中心に惑星・攻城艦隊が包囲・首飾り射程の外までの状態で開始する。
        /// </summary>
        private bool TryDescendPlanet(Vector2 w)
        {
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > systemClickRadius) return false;
            StarSystem s = map.GetSystem(sysId);
            if (s == null || s.planet == null) return false;

            StrategicFleet besieger = FindBesieger(sysId, s.planet.owner);
            if (besieger == null) return false;

            float defRatio = s.planet.maxOrbitalDefense > 0f ? s.planet.orbitalDefense / s.planet.maxOrbitalDefense : 0f;
            float invRatio = s.planet.invasionThreshold > 0f ? s.planet.invasionProgress / s.planet.invasionThreshold : 0f;
            BattleHandoff.QueuePlanetSiege(s.id, s.systemName, s.planet.owner, defRatio, invRatio,
                besieger.faction, besieger.strength, "Strategy", s.planet.kind);
            SceneManager.LoadScene("Battle");
            return true;
        }

        /// <summary>
        /// クリック位置に星系があれば、戦闘中でなくてもその星系の戦術マップ（システムビュー＝恒星系の閲覧）へ入る。
        /// 交戦回廊(TryDescend)・攻城突入(TryDescendPlanet)が優先で、どれにも該当しない平時の星系がここに来る。
        /// </summary>
        private bool TryEnterSystem(Vector2 w)
        {
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > systemClickRadius) return false;
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
