# WIN-4 引き継ぎプロンプト（窓ごと HUD／コマンドメニュー／通知の帰属整理）

> EPIC #2567「会戦のウィンドウ化＋複数同時潜行（完全同時ライブ）」の最後の段階 WIN-4（#2571）を、
> **Unity MCP のあるローカルセッション**で実施するための引き継ぎ。クラウドセッションは Game レイヤーを
> コンパイル/実機検証できないため、UI の反復検証が必要な本段階はローカルへ委譲する。
> 下記をそのままローカルの Claude Code に貼り付ければ着手できる。

---

あなたは `yamamax55/ginei-game`（Unity 6.4 / 2D URP / C# / namespace `Ginei`、PS版『銀河英雄伝説』風の戦術艦隊戦）を開発しています。プロジェクトルートの `CLAUDE.md` が最上位ルールです。必ず最初に読んでから着手してください。

## このセッションの前提（重要）
- あなたは **Unity MCP が使えるローカルセッション**です。クラウドセッションでは Game レイヤーをコンパイルできず Unity に接続できなかったため、UI の反復検証が必要な **WIN-4** をあなたへ引き継ぎました。
- **Unity Editor を起動した状態**で作業してください。Unity MCP 経由で「Hierarchy／Console／Play モード／実行中の RawImage/Canvas の状態」を確認しながら反復してください。これが引き継ぎの主目的です。
- まず `git pull origin master` で最新を取得（WIN-1〜WIN-3 はマージ済み）。作業は `claude/win4-windowed-battle-ui` を master から切って行い、完了したら **draft PR** を作成。master へ自動マージしない。
- コミット trailer は `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`。PR 本文末尾は `🤖 Generated with [Claude Code](https://claude.com/claude-code)`。モデル識別子はコミット/PR/コード/コメントに入れない。

## これまでの到達点（EPIC #2567）
WIN-1〜WIN-3 完了。戦略マップから潜行すると会戦が **別シーンへ additive ロード**され、**RenderTexture → uGUI RawImage の可動ウィンドウ**として開く。複数会戦を**完全同時ライブ**で進行でき、各会戦は固有の遠方ワールドオフセットで空間隔離、時間は `StrategySession.Clock`（速度/ポーズ）に統一追従、カーソルが乗る窓だけが入力フォーカスを得る。設計は `docs/windowed-concurrent-battles-design.md`。実機で2会戦同時動作を確認済み（ミザール／アンタレス）。

主要ファイル（既読推奨）：
- `Assets/Scripts/Game/BattleDirector.cs` … 複数会戦の司令塔（ロード直列化・オフセット割当・フォーカス・`Time.timeScale` 統一駆動・`maxConcurrent=4`）。
- `Assets/Scripts/Game/BattleWindow.cs` … 1会戦＝1窓。additive Battle シーン＋RT＋ドラッグ/拡縮/×。**ここが WIN-4 の主戦場**。
- `Assets/Scripts/Game/BattleViewport.cs` … static 入力アダプタ（フォーカス窓のスクリーン座標→ワールド変換）。
- `Assets/Scripts/Game/BattleField.cs` … シーンごとの戦場原点（移動封じ込め・撤退縁・カメラ clamp）。
- `Assets/Scripts/Game/BattleResultQueue.cs` … 会戦結果を戦略へ直列反映。
- `Assets/Scripts/Game/BattleManager.cs` / `BattleSetup.cs` … `SceneWindowed` 判定、scene-scoped レジストリ（`FleetRegistry.FlagshipsIn/ClearScene`）、`ClearExistingFleets` はシーン限定。

## WIN-4 のスコープ（実機テストで判明した第一カットの制約＝今回直す対象）
現状、各 additive Battle シーンが**自前の ScreenSpaceOverlay Canvas**（HUD／CommandMenu／PauseManager／NotificationFeed／Minimap）を**フルスクリーンで描画**するため、複数窓で重なって表示されます。会戦 UI を**その会戦ウィンドウの中に帰属**させるのが WIN-4 の核心です。

1. **会戦 HUD / コマンドメニュー / 通知を窓内に帰属**：各 Battle シーンの会戦系 Canvas を、フルスクリーンではなく**対応する `BattleWindow` の RawImage 矩形内**に収める（RT 内に描く／Camera Space 化／窓 Canvas へ親替え 等、Unity MCP で実物を見ながら最適解を選ぶ）。`FleetHUDManager`・`CommandMenu`・`NotificationFeed`・`Minimap`(#84) が対象。
2. **EventSystem / 入力ルーティングの整理**：窓ごとに 1 EventSystem 相当のクリーンな入力（フォーカス窓のみ反応）。現状は読み込み時に Battle シーンの EventSystem を無効化しているが、UI クリック（CommandMenu 等）が窓内で正しく動くか実機確認して詰める。
3. **時間制御の方針確定**：現状 `BattleDirector` が統一クロックで全会戦を駆動。必要なら窓ごとのポーズ/速度トグルを足す（任意・ユーザー確認の上）。
4. **結果反映・ロスター会戦スコープ化の磨き込み**：`BattleResultQueue` の複数同時決着の反映確認。windowed で現状スキップしている `FleetRoster`/`OrderOfBattle` の会戦ごとスコープ化（必要なら）。
5. **任意の仕上げ**：窓位置/サイズの永続化、ウィンドウマネージャ（タスクバー/タブ）、窓ごとミニマップ（潜行オフセット対応）。

## 進め方
- `CLAUDE.md` の規約（実効値パターン、Inspector 直列化優先、固定子オブジェクト名を壊さない、Esc 優先順位チェーン、`UIWindowStack` 登録、終盤ラグ回避5原則）を厳守。
- 純ロジックを足すなら test-first（`TestHarness/` で `dotnet test`）。ただし WIN-4 は大半が Game レイヤー UI なので、**Unity Editor の Play で目視＋MCP 確認**が主検証。
- 既存の `SystemMapWindow` / `WindowChrome` / `GineiUITK` の意匠・流儀に倣う。
- 大きな構造変更（例：RT 内 UI へ全面移行）に踏み込む前に、`AskUserQuestion` で方針を確認してから着手。
- 完了したら `docs/components-catalog.md` と `CLAUDE.md` 索引を更新し、draft PR を作成。

最初のアクション：master を pull → ブランチ作成 → `BattleWindow.cs` と各会戦 Canvas 生成箇所（`FleetHUDManager`/`CommandMenu`/`PauseManager`/`NotificationFeed`/`Minimap`）を読み、Unity Editor を起動して 2 会戦を開いた状態の Canvas 重なりを MCP で観測し、窓内帰属の具体案を提示してください。

## 受け入れ条件（WIN-4 #2571）
- 各窓の HUD/コマンドメニュー/ミニマップ/通知がその会戦に帰属し、複数窓で重ならない。
- 複数会戦が同時に決着しても戦略へ正しく反映（兵力換算・占領・通知）。
- 一連の流れ（複数潜行→各々指揮→決着→戦略反映）が破綻しない。
