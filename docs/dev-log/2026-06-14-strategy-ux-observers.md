# 開発ログ 2026-06-14 ── 戦略マップ UX 大改修／観測層の拡充（法令・教育・艦艇・人事）／God Object 解体／会戦の膠着解消

前回（06-11）以降、**「Core は積み上がるが盤面で何も見えない／触れない」乖離を潰す**方向に大きく舵を切った数日。観測層を 6→8 窓口へ拡げ、戦略マップの操作感を作り直し、3925 行に肥大した `GalaxyView` を partial class で物理分割した。あわせて会戦の「お互い静観で膠着」を解消し、配下艦の運動品質（EMOV-1〜5）をフル実装。CI（GameCI／可視化 playtest）も実運用に乗せた。

## 1. 戦略マップ UX の作り直し（#2384 ほか）
> 「眺めるだけのデモ」から「触って気持ちいい盤面」へ。

- **カーソル中心ズーム**を倍率＋指数追従で正規化（スケール非依存・滑らか）。**ホイールでカーソル下のワールド座標を維持**。
- **ドラッグでマップをグラブ移動**＋`SmoothDamp` で慣性追従。WASD/矢印パンを追加し、**画面端スクロールは廃止**（戦略マップでは誤爆が多かった）。
- **背景星雲**を実装しつつ減光して盤面の可読性を確保。平時の「速度・選択数」浮きバナーを廃止し上メニューへ集約。
- **二重操作の解消**：星系図窓・決裁デスク等の UI 上ではマップ操作を譲る（二重ズーム／二重ドラッグを止める）。`UIDragMove` はドラッグ窓を**常時画面内＋上メニュー帯の下**へクランプ。
- 情報窓に**経済類型／資源産出／希少資源**を追加。星系図窓にもホイールズーム／右ドラッグパン。

## 2. ウィンドウの統一 ── `WindowChrome` と `UIWindowStack`
- **`WindowChrome`（static・新規）**：上メニューから開く 8 つの観測オーバーレイ（勢力/財政/軍事/人事/決裁/情報/通知/ヘルプ）を `SystemMapWindow` と同じ Windows 風ウィンドウへ統一。`AddTitleBarLayout`／`AddTitleBarAnchored`／`MakeNonModal`（盤面を塞がない）／`MakeDraggable`。**タイトルバーを各画面で二重実装しない**集約窓口。
- **`UIWindowStack`（Core・純ロジック・test-first）**：重ねて開いたウィンドウを **ESC で最前面から 1 枚ずつ閉じ**、閉じる窓が尽きたらシステムメニュー（会戦＝ポーズメニュー／戦略＝新設 `StrategySystemMenu`）へフォールバック。各ウィンドウは Awake/Build で `Register`＋OnDestroy で `Unregister` するだけ＝**Esc の直読み（`escapeKey`）を全廃**し「手前から閉じる」判定を二重実装しない。
- 決裁ボードを移動／リサイズ（`UIResize` 右下グリップ）／最小化可能なウィンドウ化。締切が近い決裁チップを点滅、カードヘッダのクリックで中央モーダル表示。上メニュー「解決」→「**決裁**」へ改称。

## 3. 観測層を 6 → 8 窓口へ（read-only 第1層の継続）
- **`LawObserverOverlay`（L・上メニュー「法令」）**：法の支配 4 要素＋合成指数・**法治どまり判定**・派生効果、所有惑星の治安を `LawTickRules.TickProvince` で集約（犯罪圧力→公共秩序→抑圧度）。デモ法体系（同盟＝法の支配／帝国＝法治）と同じ計算を映す。
- **`EducationObserverOverlay`（U・上メニュー「教育」）**：教育チェーン 幼→小→中→高＋上級学校、派生（候補母数倍率・実効素質＝`GalaxyView.BuildEducationDump`）。
- **`FleetObserverOverlay`（B・上メニュー「艦艇」）**：艦艇プール（総/割当/残）＋艦隊台帳（兵力・役割・状態・指揮班）。**旧 `FleetOrganizationPanel`（艦隊編成）は一旦廃棄して B キーを継承**（操作化は後段）。
- **人事オーバーレイをタブ化**（指導者/軍人/文民）＝`PersonVocationRules` で振り分け。
- **初期配置のシード充実**：`SeedDemoMilitary`（編制ツリー＝軍集団⊃軍団⊃艦隊＋指揮班＋予備艦隊）／`SeedDemoCivilService`（君主・政治家・文官・技術者）で**各観測層が実際に埋まる**ように。
- 入力（L/U/B）は `GameInput` の `法令観測切替`/`教育観測切替`/`艦艇観測切替` に集約・HelpOverlay 掲載。CLAUDE.md 観測層節を更新。

## 4. God Object 解体 ── `GalaxyView` を partial class で 10 ファイルへ
- 3925 行・戦略レイヤー全 Core の配線ハブだった `GalaxyView` を**責務クラスタごとに partial class へ物理分割**（`Visuals`/`Input`/`Personnel`/`Economy`/`Government` ほか）。
- 同一クラスの分割ゆえ**コンパイル結果・Inspector・シリアライズ・MonoBehaviour 参照（GUID）は完全不変＝挙動ゼロ変更**。狙いは並列開発時のマージ衝突低減（共有ホットファイルの緩和）。

## 5. 会戦の改善
- **静観膠着の解消**：潜行時の `intrigue` 注入を「弱った国（基準忠誠 < 0.5）だけ」に限定（以前は常時注入で `loyalty<0.75` の艦が軒並み静観し両軍が固まっていた）。`BattleAllegianceRules.BreakStalemate` を追加＝双方静観なら各陣営の**最忠実な前衛**が開戦（EditMode 2 件）。
- **裸の敵旗艦の包囲**：`EncircleRules`（Core・包囲リング幾何・test-first 4 件）。
- **接敵／会戦の通知**：前線でプレイヤー艦隊が接敵すると通知（自動解決まで残り秒数も）、**通知ダブルクリックでその会戦へ潜行**（`NotificationActionRegistry`）。決着（潜行/自動解決/観てない戦線）も結果通知（`StrategyRules.ResolveEncounters` に `EncounterOutcome` 収集オーバーロード＋テスト 1 件）。
- **会戦記録ピン**：会戦の起きた回廊に控えめな × 印（薄い勝者色）を残し、ホバーであらまし表示。1 年で自動消滅・上限超過は古い順に削除。
- 右クリックメニューを 移動／攻撃／特殊（アタックムーブ・後退・停止・保持）／陣形… に再編。

## 6. 配下艦の運動品質（EMOV-1〜5・#2389）
- **EMOV-1** スロット割当の安定化＝`EscortSlotAssignmentRules.Assign`（最近傍・距離のみ）で戦死再フィットの**席替え交差を解消**、陣形変更/初回は `AssignWithClass`。
- **EMOV-2** 艦種を考慮した配置＝戦艦を前面/外周・駆逐艦を側面へ（`PreferredClassForSlot`/`slotClassBias`）。
- **EMOV-3** 加減速ランプ（`escortAcceleration`）＋任意の回頭バンク（既定 OFF）。
- **EMOV-4** 航行⇄戦闘の隊形密度切替（実効間隔 `spacingFactor`・基準非破壊）。
- **EMOV-5** 分離の負荷対策＝グリッド近傍比較で O(n²)→～O(n)＋画面外 LOD。数式は Core 純ロジックへ委譲し Game 側に二重実装しない。

## 7. CI・playtest の地ならし
- **GameCI 有効化**（Personal ライセンス訂正＋ガード＋境界テスト 3 件修正）。
- **可視化 playtest（`playtest-visual`）**をクラウドで実運用：StandaloneLinux64 をビルド→xvfb＋ソフト GL で起動→会戦スクショ＋`report.json` を artifact 化。実行ファイルの動的検出、ビルド時 JP 表示（豆腐）対策、`SpaceBackground` の null シェーダ例外、イベントモーダルが無人実行で PAUSE して凍結する不具合などを順次潰した。**非決着は FAIL でなく PASS 扱い**にし、実バグだけを捕捉する方針へ。
- 税率レバーは通常プレイから隠す（デバッグモード化）。

## 検証
- Core 純ロジックは EditMode／TestHarness で担保（本セッションで `EncircleRules` 4・`EncounterOutcome` 1・`BreakStalemate` 2・`UIWindowStack` ほかを追加）。
- 戦略マップ／観測オーバーレイ／会戦の見た目・操作感は実機 Play もしくは可視化 playtest のスクショで確認（入力注入が環境制約のため）。

## ドキュメント
- CLAUDE.md：観測層節（窓口 6→8・L/U/B）、`WindowChrome`/`UIWindowStack`/`StrategySystemMenu` のコンポーネント表、Esc 優先順位チェーンを反映。
- `docs/late-game-performance-design.md` の規律（個体粒度へ降りない・暦境界 Tick・差分/収束/キャッシュ・N² を増やさない・シミュ LOD）を観測層シードと EMOV-5 で踏襲。
