# 開発ログ 2026-06-10 ── 統一時間・艦隊編成プール・通知システム

戦略レイヤーの「時間」「編成」「通知」基盤を一気に配線した日。純ロジックは Core で test-first、Game 配線は GalaxyView/BattleManager へ。TestHarness は 440→**1020** テストへ。

## 1. 統一時間（EPIC #946 TIME-1〜7 ／ #959）
- **TIME-1〜4**：`GameClock`（速度/ポーズの唯一の権威・`StrategySession.Clock`）／`GameDate`（宇宙暦SE・帝国暦IC・HH:MM）／`TimeDisplay`（右上HUD・+/-で速度）／`AutoBattleSim`（Lanchester二乗則で自動解決の**所要 game-time** を算出）。
- **TIME-5 二層連続**：`BattleManager` も同一クロックを進める＝**潜行/復帰で時間が止まらない**。戦果は `BattleHandoff` で還元。
- **TIME-6 暦粒度Tick**：`CalendarTick`/`CalendarDispatcher` が日/月/年境界を数え、`GalaxyView` が日次で財政(`TickEconomyDay`)・支持低下イベント・造船、年次で加齢を回す。倍速で暦比一定・ポーズで停止。
- **TIME-7 自動スロー（Paradox風）**：`TimeFlowRules`＝平時は暦を圧縮して速く流し（既定 1年≈12分）、会戦の生起・前線突入で**実時間へ自動減速**。実時間アクション（艦隊移動）の速さは不変＝暦だけ伸縮。
- 史実調査：会戦＝数時間〜数日（関ヶ原≒半日／バーミリオン≒11日）、攻城＝数ヶ月〜年（鳥取/三木/応仁）→ 暦コスト設計の根拠に。

## 2. 艦隊編成プール（#148 ／ #884 造船供給）
- `FleetPool`（勢力の総艦艇）＋`FleetPoolRules`（配分・超過防止・損耗`ApplyAttrition`）。
- `FleetOrganizationPanel`（**Bキー**・戦略マップから艦艇数±＋司令/副提督/参謀を階級ゲート付きで配属。`OrderOfBattlePanel` の姉妹）。
- 造船 `ShipyardRules.CommissionToPool`＝**建艦で総艦艇が増える**。星系ごとの造船所（全勢力＝AIも）・生産力(`ProductionFactor(Province)` 安定度比例)連動・会戦損耗でプール減。
- 提督の加齢/死亡 `AnnualLifecycleRules`（暦の年境界・`LifecycleRules` 委譲）。

## 3. 通知システム（EPIC #964 NOTIF-1〜3）
- `NotificationCenter`（単一窓口・seq採番・`Since`差分・リングバッファ）＋`NotificationFeed`（画面**左下**トースト・実時間フェード・重要度色分け）。
- GalaxyView の battleMsg（会戦結果/占領/造船/提督死去）を集約。バナーは現在状態のみに整理。
- 参考：Stellaris のイベント=トースト/状態=アラート二分、Civ の通知パネル、RTS の下部トースト。アラート/履歴/設定は NOTIF-4〜6 で後続。

## 検証
- 各機能 Core は test-first（EditMode/TestHarness 1020緑）。Game 配線は Unity コンパイル0エラー＋Strategy/Battle の Play 0エラーで確認。
- UITK/overlay UI（編成画面・左下フィード）の見た目は実機目視が要（入力注入が環境制約のため）。

## リポジトリ整理
- `.gitignore`：`TestResults/`（dotnet test 出力）と未使用の `Assets/SpaceSkies Free/`（約483MB・コード/シーン未参照）を追跡対象外に。
- `JapaneseFont_TMP.asset`：新UIのグリフ追加で atlas が成長＝コミット。
