# 選挙の Game 接続検証 — 結果報告（election-integration-qa-20260914 / a1）

- 対象 Issue：#2768
- 基点：`integration/local-cloud-20260914`（HEAD `e1749c93`）＋未コミットの選挙変更（elections-20260914 a1。消さずに残した）
- 実施者：Claude（Read/Grep/Glob/Edit/Write のみ）。**コンパイル・試験は実行していない**（ChatGPT 側で実行）。git 操作なし。
- 事前に読んだもの：`docs/ops/claude-status.json`（elections a1 の ChatGPT 検証メモ＝分離 Unity で PlayMode 1/1・Core 48/48 合格）、`docs/ops/elections-20260914-result.md`、`docs/ops/integration-local-cloud-20260914-result.md` 8節。専用の分離プロジェクト検証ファイルは docs/ops に見つからなかったので、上の status の記載を検証結果として扱った。

## 1. 調べて分かった接続の穴（Game 配線を最小修正）

1. **政治 Tick の後に起きた欠缺で、知事の権限が約1年残る。** 年次処理の順番は `RunPoliticsTick` → … → `RunUniversityTick`（ここで文民が老衰死）→ `RunCivilAppointmentTick` → `RunGovernorAppointmentTick`。首相は宰相銓衡（`MaintainElectedPremier`）で入れ替わるが、民主政の総督銓衡は `continue` するだけだった。そのため、死去・拘束・離反した知事や、同じ年の後段（反乱など）で手放した星系の知事が、翌年の政治 Tick まで `GovernmentRegistry` に残っていた。
2. **兼任を Game 側で拒否していなかった。** Core の `LocalElectionRules.RunDue` は候補の段階で兼任を除く。しかし次の2つの経路は見ていなかった。
   - 保存データの `RestoreElectedOffices`：人物が居るかどうかしか確かめていなかった。
   - 選挙の無い年の再組閣：知事が党首から首相になると、翌年の政治 Tick まで首相と知事を兼ねていた。

修正（`GalaxyView.Politics.cs`／`GalaxyView.Government.cs`）：
- `RunGovernorAppointmentTick` は、銓衡の前に、知事を選挙で選ぶ勢力ごとに `RefreshElectedGovernors` を呼ぶ。処理は次の3段で、新しい計算は足していない。
  1. `LocalElectionRules.Reconcile`：手放した星系の知事を失職させる。
  2. `VacateUnavailableGovernors`：不在・死亡の知事を空席にして、補欠選挙を翌年に入れる。
  3. `SyncElectedGovernors`：知事職を台帳と一致させる。
- `SyncElectedGovernors` に兼任の拒否を追加（`GovernorConflictReason`）。
  - 首相本人が知事に載っている場合 →「首相と兼任できないため空席」。
  - 同じ人物が複数の星系に載っている場合 → 星系IDが最小の1つだけ残し、他は「〇〇の知事と兼任できないため空席」。
  - いずれも補欠選挙を翌年に入れ、人事の通知を1通出す。
- `RestoreElectedOffices` の知事の空席処理を `VacateUnavailableGovernors` に切り出した。挙動と理由の文面は前と同じ。読込時は今までどおり通知しない。
- 空席化の共通処理は `VacateGovernorRecord`：失職・理由・`termEndYear=0`、次回の知事選は「今の予定」と「翌年」の早いほう。

## 2. 試験用の入口（本番と同じ経路を呼ぶだけ）

新規 `Assets/Scripts/Game/GalaxyView.ElectionQa.cs`（＋.meta）：
- `BindElectionQaWorld(map, provinces, commanders, civilians)`：private の盤面フィールドに差し込むだけ。
- `SeedGovernmentForQa` / `RunPoliticsTickForQa` / `RunCivilAppointmentTickForQa` / `RunGovernorAppointmentTickForQa` / `ElectionYearForQa`：本番の private メソッドをそのまま呼ぶ。
- セーブ・シーン・設定には触れない。GalaxyView は無効な GameObject に載せて使う（`Start`＝銀河構築・BGM・カメラを走らせない）。

## 3. 新規 PlayMode 試験（未実行）

`Assets/Tests/PlayMode/ElectionGameIntegrationPlayModeTests.cs`（＋.meta）4件。

固定条件：
- 暦は SE797（`StrategySession.Clock`＝1年＋1秒）。
- 同盟：共和制・2党（支持 0.6／0.4）・3星系・文民政治家6名（ID 11〜16）。
- 帝国：君主制・1星系。
- 同盟に、官位（宰相相当）と高い文才を持つ**非政治家の官僚**（ID 20）を置く。旧来の銓衡が走れば、この人が宰相・総督に選ばれるはずの対照。

| 試験 | 確認すること |
|---|---|
| `InauguralElection_SeatsPremierAndGovernors_AndAnnualAppointmentsDoNotOverwrite` | 開幕シードだけでは首相なし→初回選挙で両院構成・開票2件・下院の次回 SE801・宰相職に首相・3星系の総督職に当選者・帝国は選挙なし／宰相→総督の年次銓衡でも在任が変わらず、官僚は就かない／同じ年の再処理で開票・通知が増えない／兼任なし／首相・知事は軍事所掌を持たず、会戦系統は総司令官にならない |
| `SaveJsonRoundTrip_RebuildRestoresOfficesSeatsAndSchedules_WithoutRecount` | `CampaignSerializer.ToJson(campaign, people)`→`Parse`/`FromSaveData`/`ReadPeople`（文字列のみ）→旧 GalaxyView を破棄→新しい GalaxyView・人物・政府台帳で `SeedGovernment`（＝`RestoreElectedOffices`）→首相・知事の在任、両院の党別議席、次回日程、任期末が一致／開票記録は2件のまま、選挙・組閣の通知なし／続けて同じ年の年次処理を回しても開票しない |
| `DeathCaptivityAndOwnershipChange_RemoveAuthority_AndShowVacancyReasons` | 政治 Tick の後に、首相が捕虜・辺境星の知事が死去・国境星が帝国に占領→宰相／総督の銓衡。捕虜の首相に役職なし・理由「職務を続けられない」・台帳の首相と宰相職が一致／死去した知事に役職なし・失職・理由「不在・死亡」・補欠 SE798／占領星系の同盟知事職は空席・台帳から外れ、「星系が勢力を離れた」の通知が1通／兼任なし／政治オブザーバに首相と知事の空席理由と「知事 空席」が出る |
| `DualHolding_PremierAndGovernorOrTwoSystems_IsRejectedOnRestoreAndAnnualTick` | 保存データを食い違わせる（首相を首都星の知事にも、辺境星の知事を国境星にも）→復元：首相は宰相職のみ、首都星は「首相と兼任できない」で空席、辺境星は残り、国境星は「兼任できない」で空席／復元後、辺境星の知事を首相に書き換えて年次の銓衡→辺境星は空席、前知事の権限なし、理由「首相と兼任できない」／首相は軍事所掌を持たない |

片付け（TearDown）：
- 元に戻すもの：`StrategySession.Campaign/Map/Provinces/Clock`、`GovernmentRegistry`（試験前の任命を控えて作り直す）。
- 破棄するもの：生成した GameObject（GalaxyView・政治オブザーバ）。
- ユーザーのセーブファイルは読み書きしない（`CampaignSaveManager` を呼ばない）。

## 4. 変更ファイル

- `Assets/Scripts/Game/GalaxyView.Politics.cs`（`SyncElectedGovernors` の兼任拒否、`VacateUnavailableGovernors`／`VacateGovernorRecord`／`GovernorConflictReason`／`RefreshElectedGovernors` を追加、`RestoreElectedOffices` を切り出し）
- `Assets/Scripts/Game/GalaxyView.Government.cs`（`RunGovernorAppointmentTick` の先頭で `RefreshElectedGovernors`）
- `Assets/Scripts/Game/GalaxyView.ElectionQa.cs`（新規）＋`.meta`
- `Assets/Tests/PlayMode/ElectionGameIntegrationPlayModeTests.cs`（新規）＋`.meta`
- `docs/ops/election-integration-qa-20260914-result.md`（本書）、`docs/ops/claude-status.json`

Core・EditMode 試験・シーン・プレハブ・設定・セーブ・ChatGPT レビュー JSON・他の作業票は編集していない。

## 5. 未実行の試験（ChatGPT で実行）

1. Unity コンパイル（Game 層3ファイル・PlayMode 試験。Game は `dotnet test` では検出できない）
2. PlayMode：`ElectionGameIntegrationPlayModeTests`（新規4件）＋`PoliticsObserverElectionPlayModeTests`＋`RingiFlowPlayModeTests` ほか既存 PlayMode の回帰
3. TestHarness `dotnet test`（Core は変更なし。回帰確認のみ）
4. 実画面（任意）：新規戦役で SE797 を越えてから、政府オブザーバ（Alt+G）で首相・知事を確認。翌年以降も銓衡で置き換わらないこと

## 6. 残件・注意

- **挙動の変化（小）**：民主政の勢力が年の途中で星系を得た場合、その年の総督銓衡で `Reconcile` が走る。そのため初回の知事選は「翌年」（従来は、次の政治 Tick で編入扱いとなり「翌々年」）になる。手放した星系の失職は、その年のうちに通知が出る。
- 占領された星系の台帳は旧所有勢力から消える（Core の `Reconcile` の仕様）。空席理由は台帳には残らず、失職の通知として出る。新しい所有者が君主制なら、政治オブザーバには「対象外」と出る。
- 兼任の拒否は「首相を優先し、星系IDが最小の知事職を残す」決定論。誰を残すのが望ましいかは仕様判断の余地がある。
- 首相が捕虜になったときの後任（党首選の結果）は試験で名指ししていない。固定しているのは次の4点：捕虜に役職が残らない／台帳と宰相職が一致する／理由がある／兼任が無い。
- `GalaxyView.ElectionQa.cs.meta` を Write したとき、ツールの表示が「新規作成」ではなく「更新」だった。作業中に hook などで先に .meta が作られていた可能性がある（前の中身は見ていない）。guid は `e7a41c3b95d24f0f8c6a2d1b7e9f4a52` にしてある。Unity が別の guid で取り込み済みなら、そちらに合わせてほしい。
- 通知の在庫（`NotificationCenter`）は試験中に積まれたまま戻していない（実行中のメモリのみで、保存されない）。
- 連立交渉・不信任・解散・党の再編は、この作業票の範囲外（未着手）。
