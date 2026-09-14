# cabinet-observer-qa-fix-20260914 / a1 結果（#2768 #141）

## 失敗
`CabinetPostsPlayModeTests.AnnualTick_FormsCabinetAndPartyExecutives_SurvivesSave_DeathAndPremierChangeClearAuthority` 行344
`StringAssert.Contains("職業官僚", government.DumpTextForTest)`（前段の知事／党三役の兼任チェックは通過済み）。

## 原因＝試験 fixture の接続不足（画面ロジックの欠陥ではない）
- `GovernmentObserverOverlay` は省庁（`MinistriesOf`）と人物名（`CivilianRoster`/`CommanderRoster`）を `GalaxyView.Active` から読む。
- `GalaxyView.Active` は `GalaxyView.Start()`（GalaxyView.cs:371）でだけ設定され、`OnDestroy` で解除される。
- 試験は `GalaxyView` を無効な GameObject に載せて Start を走らせない（`BindElectionQaWorld`）ため、`Active` は null のまま。
  → `AppendCabinet` の `mins` が null → 大臣行の「職業官僚 N/M名」が出ない（人物名も `id N` 表記に落ちる。省庁ツリーも出ない）。
- 本番の戦略シーンでは Start が必ず `Active` を張るので、表示ロジック自体は正しい。表示チェックは削除せず、試験側を本番と同じ参照に接続した。

## 実装（最小）
1. `Assets/Scripts/Game/GalaxyView.ElectionQa.cs`：既存の非保存 QA 入口に `public static GalaxyView SwapActiveForQa(GalaxyView view)` を追加（`Active` を差し替え直前値を返すだけ・別ロジックなし。Start/セーブ/シーンに触れない）。
2. `Assets/Tests/PlayMode/CabinetPostsPlayModeTests.cs`：
   - SetUp で `GalaxyView.Active` を退避、TearDown で `SwapActiveForQa(savedActive)` により復旧（無効な GameObject は OnDestroy で解除されないため明示）。
   - 手順6（オブザーバ）の直前に `SwapActiveForQa(rebuilt)`＝読込後に再構築した本番経路の GalaxyView を観測に接続。
   - 表示チェックを強化（削除なし）：`省庁 未配線` が出ない／各大臣の省について「職業官僚</color> 在籍/定員名」が `rebuilt.MinistriesOf(同盟)` の `staffIds.Count`/`staffSlots` と一致／首相名と在任閣僚の「職名 人物名」が表示される（id 表記に落ちない）。
- 前修正（知事と党三役の兼任）と未コミットの内閣変更は保持。Core・観測オーバーレイ本体・シーン・プレハブ・設定・ユーザーセーブは変更なし。

## 未実行の試験（実行禁止のため）
- Unity コンパイル（Game 1ファイル・PlayMode テスト1ファイル変更）
- PlayMode `CabinetPostsPlayModeTests`（2件）
- 既存回帰：GalaxyView.Active を読む他の PlayMode（Politics/Government/Election 系オブザーバ試験）が Active の復旧で影響を受けないこと
- Core 変更なしのため TestHarness は対象外（任意）

## 残件・注意
- 追加アサートの文字列は `GovernmentObserverOverlay.AppendCabinet` の現行書式（`<color=#9fb0c0>職業官僚</color> N/M名`、`      職名 人物名`、` 首相 人物名`）に依存。書式変更時は試験も同期。
- 他の QA 試験が今後オブザーバの省庁/人物名を検証する場合も同じく `SwapActiveForQa` で接続し TearDown で戻すこと。
- 別残件（前票から継続）：閣僚の権限（`CabinetAppointmentRules.Authority`）を決裁デスク／稟議へ接続。
