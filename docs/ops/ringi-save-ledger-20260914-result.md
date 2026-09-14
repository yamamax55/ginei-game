# ringi-save-ledger-20260914 / a1 結果（稟議台帳の保存ずれの修正）

実施：Claude（Read/Grep/Glob/Edit/Write のみ）。**コンパイル・テストは一切実行していない**（実行は ChatGPT 側）。
コミット・push・`git add` なし。Unity・シェルは起動していない。アセット・シーン・ワークフロー・実セーブ（`campaign_save.json`）には触れていない。
ブランチ `integration/local-cloud-20260914` の既存変更（統治政策 Alt+T 等）はそのまま残している。

---

## 1. 直した問題（integration-local-cloud-20260914-result.md 7.4 の続き）

| # | 問題 | 原因 | 直し方 |
|---|---|---|---|
| A | 保存される稟議が常に空で、ロード後のカードを裁可しても何も起きない | `RingiDirector.Ledger`／`FleetRingiDirector.Ledger` が `StrategySession.Petitions`／`FleetPetitions` とは別の static だった | 両 `Ledger` を `StrategySession` の台帳を**そのまま返すプロパティ**に変更（null なら作って入れる）。台帳は1つだけになった |
| B | タイトルの「戦役を再開」で、読み込んだ稟議とカードがすぐ消える | `TitleManager.ContinueCampaign` が **LoadSession → ResetCampaignStatics** の順で、リセットが台帳と `DecisionDeck.ClearQueue()` を消していた（A を直すと表面化する） | `GalaxyView.ContinueCampaignFromSave()`（**リセット → ロード**）を追加し、`TitleManager` はそれを呼ぶ |
| C | 税と編制の稟議 id の取り違え | 2つの台帳はどちらも 1 から採番。両 Director が `Resolved` を購読し `Ledger.Get(petitionId)` だけで引くので、編制カード（petitionId=1）で税の稟議 1 を実行しうる | カードの効果キーで担当を分ける（`fleet.establish`/`fleet.disband` は編制、それ以外は税・統治）。さらに稟議の `effectKey` とカードの `effectKey` が一致する時だけ自分の案件とみなす |
| D | 台帳に無い稟議を指すカード（古いセーブ・台帳が空だった頃のセーブ）を裁可しても、黙って何も起きない | `pet == null` で return していた | 効果は出さず、`ClaimForApply` を1回だけ取って「対象なし：対応する稟議の記録がありません（古いセーブなど）。実行されませんでした」と結果と通知に残す |
| E | ロード後に新しい稟議が古いカードの id を使いうる | 採番は読み込んだ稟議の最大 id までしか戻らない。台帳に無い稟議を指すカード（D）と同じ id を新しい稟議が取ると、古いカードの裁可で新しい案件が実行されうる | `PetitionLedger.ReserveId(int)` と `CampaignSerializer.ReservePetitionIds(queue, ledger)` を追加。`LoadSession` で、カードが指す稟議 id を**両方の**台帳の採番に予約する |
| F | 統治政策の執行で、盤面（GalaxyView）が無いと所有の確認を飛ばしていた | `BriefingContext()`（GalaxyView 経由）の map が null なら確認なしで執行 | `StrategySession.Map` で確認する。**星系が地図に無い／Map が null／所有が稟議の勢力と違う**なら失敗（対象なし）として記録し、稟議を閉じ、政策は変えない |

A〜F 以外（保存形式・Director の起案ロジック・カードの見た目・権限判定）は変えていない。セーブのフィールド追加もない（旧セーブは今までどおり読める）。

## 2. 変更したファイル

### 本体
- `Assets/Scripts/Game/RingiDirector.cs`
  - `Ledger` → `StrategySession.Petitions` を返すプロパティ。
  - `OwnPetition(d)` を追加（編制キーは除外・id と効果キーの両方が合う時だけ）。`ActivePendingCount` と `OnResolved` で使う。
  - `OnResolved`：編制キーは無視。自分の稟議が見つからなければ `FleetRingiDirector.RecordMissingPetition`（D）。
  - `ExecuteGovernancePolicy`：所有の確認を `StrategySession.Map` に変更（F）。
- `Assets/Scripts/Game/FleetRingiDirector.cs`
  - `Ledger` → `StrategySession.FleetPetitions` を返すプロパティ。
  - `internal static IsFleetEffectKey` / `SameEffectKey` / `RecordMissingPetition` を追加（両 Director が使う）。
  - `OnResolved`：編制キーのカードだけを扱う。稟議が無い／効果キーが違うなら失敗として記録。
- `Assets/Scripts/Game/GalaxyView.Persistence.cs`：`ContinueCampaignFromSave()` を追加（B）。`ResetCampaignStatics` はコメントのみ変更。
- `Assets/Scripts/Game/TitleManager.cs`：`ContinueCampaign` がリセット→ロードの順の新メソッドを呼ぶ。
- `Assets/Scripts/Data/CampaignSaveManager.cs`
  - `LoadSession`：台帳が null なら作る／カードの稟議 id を両台帳へ予約（E）。
  - `public static string SaveFilePath` を追加（テストが実ファイルを退避・復元するための読み取り専用。書き込みには使わない）。
- `Assets/Scripts/Core/Economy/PetitionLedger.cs`：`ReserveId(int id)` を追加。
- `Assets/Scripts/Core/Society/CampaignSerializer.cs`：`ReservePetitionIds(DecisionQueue, PetitionLedger)` を追加。
- `Assets/Scripts/Core/Strategy/StrategySession.cs`：コメントのみ（Director の Ledger がこれを指すこと）。

### テスト（新規ファイルなし＝.meta 不要）
- `Assets/Tests/EditMode/PetitionLedgerTests.cs`（+2）
  - `ReserveId_AdvancesSeq_WithoutAddingItems_AndNeverGoesBack`
  - `Clear_ResetsReservedSeq`
- `Assets/Tests/EditMode/RingiCompletionTests.cs`（+3）
  - `LegacySave_CardsWithoutPetitions_ReservedIds_DoNotCollide`：台帳なしの旧セーブ相当で、新しい稟議がカードの id(12) を使わず 13 になる
  - `Petitions_RoundTrip_ContinuesIdsAfterHighestRestored`：読み込みで前の案件が消え、採番は最大 id の続き
  - `ReservePetitionIds_NullSafe`
- `Assets/Tests/PlayMode/RingiFlowPlayModeTests.cs`（既存5件は維持＋新規3件）
  - 既存の統治政策テストの前提に、**同盟所有の星系を `StrategySession.Map` に置く**処理を追加（F で Map が必須になったため）。Map も TearDown で元に戻す。
  - `Governance_TargetNowForeignOrMissing_FailsWithoutChange`：決裁待ちの間に①所有が帝国に変わる②地図から消える → 裁可しても政策は変わらず、結果＝対象なし、稟議は執行済（閉じる）。
  - `OrphanOrOtherLedgerPetitionId_IsNotExecuted`：①税の稟議と同じ id を指す編制カードを裁可 → 税率も税の稟議の状態も変わらず、カードは対象なしで記録②台帳に無い id を指す税カード → 効果なし・対象なし③`ReservePetitionIds` 後の新しい稟議はその id より大きい。
  - `SaveLoad_RestoredGovernanceAndFleetCards_ResolveExactlyOnce`（本題）：
    1. 実際の `RingiDirector.SubmitGovernancePolicy` と `FleetRingiDirector.ForcePlayerReview` で、統治政策カード（未決）と編制設立カード（**最小化＝保留**）を作る。台帳が `StrategySession` と同じ物であることも確認。
    2. **利用者の `campaign_save.json` の有無とバイト列を退避**してから、実際の `CampaignSaveManager.SaveSession` で保存。
    3. Director を破棄し、`StrategySession.Clear()` のあと前の戦役の残り（稟議・カード）を置く。
    4. タイトルと同じ `GalaxyView.ContinueCampaignFromSave()`（リセット→`LoadSession`）で読み込み、**直後に `finally` で利用者のセーブを戻す**（TearDown でも再度戻す）。Director を作り直す。
    5. 確認：残りが消えている／稟議2件が決裁待ちで復元／カードは未適用・編制は最小化のまま／Province は読み込んだ別オブジェクトで民生／採番（稟議2種・カード id）が既存より先。
    6. 統治政策カードを裁可 → 読み込んだ Province が動員に、結果＝実行、稟議＝執行済。編制カードを裁可 → 同盟の現役艦隊が +1、結果＝実行。
    7. もう一度裁可 → 両方弾かれ、`Resolved` も効果（政策・艦隊数）も2回目は起きない。

  TearDown で戻すもの：セーブファイル（最初に実行）、Director、`FleetPool`（同盟の値）とテストで設立した艦隊（解散）、`StrategySession` の各参照、`AuthorityCheck`。

## 3. リスク・注意

- **テストが消すグローバル状態**：`ContinueCampaignFromSave` は `ResetCampaignStatics` を呼ぶので、`GrowthRegistry`/`FameRegistry`/`PersonDecisionLedger`/`FinancialHoldingRegistry`/`PropertyDeedRegistry`/`LandLeaseRegistry`/`CollateralLoanRegistry` がテスト中に空になる（元へは戻さない）。PlayMode の他テストがこれらの事前状態に依存していると影響しうる。`FleetRoster` には解散済みの艦隊（と欠番）が残る。
- **セーブファイル**：テストは実際の `persistentDataPath/campaign_save.json` に一時的に書く。有無とバイト列を退避し、ロード直後と TearDown の2回戻す。**Unity が保存〜ロードの間にクラッシュ・強制終了した場合は戻らない**。実行前に手元でコピーを取っておくと安全。
- **再開の順序変更の副作用（良い方向のはず）**：以前は「再開」で `LoadSession` が戻した提督の会戦成長（`GrowthRegistry`）もリセットで消えていた。今回リセット→ロードにしたので、再開後に成長が残るようになる。ロードに失敗した場合、タイトル画面で戦役 static がリセット済みになる（タイトルでは使っていないので実害はない見込み）。
- **統治政策の所有確認が厳しくなった**：`StrategySession.Map` が null（盤面の無いテストや特殊な経路）では、統治政策の裁可は必ず失敗する。実プレイでは `GalaxyView` が新規・ロードとも `StrategySession.Map` を使うので同じ地図を見る（`GalaxyView.Governance.cs` の `StrategySession.Set(map, reg)` / `map = StrategySession.Map`）。多勢力（`ownerData`）は見ておらず、今までどおり enum の `owner` と稟議の勢力で比べる。
- **効果キーでの振り分け**：カードの `effectKey` と稟議の `effectKey` は、3つの起案経路（税の状況起案・サンプル・統治政策／編制）でどちらも同じ値を入れている（コードで確認）。保存で null は空文字になるので、null と空は同じ扱いにした。今後、カードの効果キーを稟議と違う値にする経路を足すと、そのカードは「対応する稟議の記録がありません」になる。
- **旧セーブ**：台帳が空だった頃のセーブの未決カードは、稟議が失われているので**実行はできない**（D のとおり失敗として記録される）。直せるのは「黙って何も起きない」ことだけ。
- `GalaxyView.LoadCampaign`（戦略画面の F9）はリセットを呼ばない既存の流れのまま。台帳は `ReadPetitions` がその場で入れ替えるので今回の修正で正しく読める。
- `ProtagonistCareerDirector.SubmitPetition` は `RingiDirector.Ledger` に稟議を積む（カードは作らない）。今回から保存対象になる（`career.petition`）。
- Core の新 API（`ReserveId`/`ReservePetitionIds`）は `docs/catalog/core-modules-catalog.md` に追記していない（既存モジュールへの小さな追加のため）。

## 4. テスト（すべて未実行）

ChatGPT で実行してほしい順：
1. Unity コンパイル（Core：`PetitionLedger`/`CampaignSerializer`/`StrategySession`、Data：`CampaignSaveManager`、Game：`RingiDirector`/`FleetRingiDirector`/`GalaxyView.Persistence`/`TitleManager`、PlayMode テスト）
2. TestHarness：`dotnet test`（EditMode の追加5件を含む）
3. EditMode：`PetitionLedgerTests`・`RingiCompletionTests`（全件）
4. PlayMode：`RingiFlowPlayModeTests`（既存5件＋新規3件）。**実行前に `campaign_save.json` のコピーを手元に取る**ことを推奨。実行後、ファイルの有無・内容（ハッシュ）が実行前と同じか確認してほしい。
5. PlayMode 全件（`ResetCampaignStatics` を呼ぶテストを足したので、他テストへの影響の確認）

## 5. 未確認（実画面）

- Strategy で決裁待ちカード（統治政策 Alt+T／編制 F8）を残して F5 保存 → タイトルへ戻る → 「戦役を再開」→ カードが右下に出る → 裁可で政策が変わる／艦隊が増える（1回だけ）。
- 同じく戦略画面の F9 ロード後の裁可。
- 稟議オブザーバ（Alt+I）にロード後の稟議が出ること。
- 旧セーブ（台帳なし）の未決カードを裁可したとき、「対応する稟議の記録がありません」の通知と結果が出ること。
- 再開後に提督の会戦成長が残ること（B の副作用）。

## 6. ChatGPTによるレビュー・検証（2026-09-14）

Claudeの編集終了・結果受領を確認後、変更差分をレビューした。台帳の保存対象の統一、タイトル再開のリセット順、効果キーによる担当分離、欠落した稟議IDの予約を確認。

- Core TestHarness：9,785 / 9,785 成功、失敗・スキップなし（ローカル.NET 10でMajor roll-forward）。
- Unity 6000.6.0f1 PlayMode：選択した7スイート計68 / 68 成功、失敗・スキップなし。全PlayModeスイートの実行ではない。
- RingiFlowPlayModeTestsの保存→タイトル再開処理→統治政策／編制の裁可・一度だけ反映を含む。
- git diff --check：問題なし。
- 証跡：Codex作業フォルダー outputs/ringi-save-20260914/core.trx、playmode.xml、unity.log。

### 画面・実操作の結果

Unity EditorのFull HD Game Viewで、マウス操作により以下を確認した。

1. タイトルの「戦役を再開」で既存セーブを読み込める。
2. 戦略画面の「停止／再開」ボタンで停止表示になり、日時の進行が止まる。
3. メニュー→セーブで「セーブしました」の通知が表示される。
4. メニュー→タイトルへ戻る→戦役を再開で決裁カードが復元される。期限経過で保留されたカード2件もクリックで再展開できる。
5. 復元された「減税の建白」を裁可すると「権限外」の通知が出てカードが残る（現在のプレイヤーの権限による拒否）。裁可成功や政策変更を確認したことにはしない。

この既存セーブの2カードはpetitionId=0のサンプルカードで、petitionsは空だった。そのため、画面操作での「稟議本文の保存・裁可成功」を検証したとは扱わない。停止状態は再開後に解除され、時間が進んだ。

### 未確認・次の検証条件

- 自軍星系にカーソルを置いたAlt+T入力、および比較用P入力に画面上の反応が出なかった。マウスの保存・再開操作は反応する。原因はゲーム側か自動入力側か未確定。反応しないキーを繰り返すことは避けた。
- 実キーボード、または確実に入力を届けられる検証環境で、Alt+T新規上申→保存→タイトル再開→権限のある決裁者による裁可成功を確認する。
- Alt+PとPの競合なし、F9再読込、Alt+I台帳表示、旧セーブの孤立稟議カードの欠落通知、提督成長の画面表示は未確認。
- 上記は自動試験合格と区別し、Issueを完了扱いにしない。

実機検証終了後にPlay modeを停止し、campaign_save.jsonとsetup_save.jsonを検証前のバックアップへ復元。両方のSHA-256一致を確認した。ユーザーの元のセーブは保全済み。
