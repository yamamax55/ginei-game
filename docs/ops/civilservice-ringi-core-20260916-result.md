# civilservice-ringi-core-20260916 / a2 結果

> #141 の次工程（前半）。省内職位の人事（配属・異動・昇任・降任・解任）を
> **権限者は即時実行／権限外は既存の稟議カードへ上申 → 裁可後に同じ `CivilServicePostRules.Execute` で一度だけ実行**
> という1本の経路へ接続し、次作業の操作画面が呼べる公開入口まで作った。UI 本体は次作業。

## 1. 追加・変更したもの

| 層 | ファイル | 内容 |
|---|---|---|
| Core | `Assets/Scripts/Core/Government/CivilServiceRingiRules.cs`（新規） | 効果キーの相互変換・承認を問う段の解決・カード文面と理由の出し入れ・結果型 |
| Core | `Assets/Scripts/Core/Government/DecisionEffectRegistryRules.cs` | 人事キーを「実装済み」と認識（復元できるキーだけ） |
| Game | `Assets/Scripts/Game/GalaxyView.Cabinet.cs` | 見込み／申し出／権限判定／裁可後の執行の公開入口 |
| Game | `Assets/Scripts/Game/RingiDirector.cs` | 決裁の確定から人事を専用実行へ分岐 |
| Game | `Assets/Scripts/Game/DecisionAuthorityDirector.cs` | 人事キーの権限判定を分野推定でなく人事の承認権限へ委譲 |
| 試験 | `Assets/Tests/EditMode/CivilServiceRingiRulesTests.cs`（新規） | キー往復・不正キー・段の解決・理由・文面・レジストリ |
| 試験 | `Assets/Tests/PlayMode/CivilServiceRingiPlayModeTests.cs`（新規） | 直接実行・上申・重複・裁可1回・見送り・状態変化・保存往復 |

`TestHarness/GineiLogic.Tests.csproj` は `Core/**/*.cs` と `Tests/EditMode/*.cs` を **glob で取り込む**ため、
新規 Core ルールと EditMode 試験の**登録作業は不要**（`TestHarness/Program.cs` は存在しない＝変更なし）。

## 2. 効果キー（保存できる ASCII・版番号つき）

```
civilservice.post:{版}:{行為}:{省id}:{人物id}:{段}
例) civilservice.post:1:2:1004:31:1   ＝ 版1・昇任・省#1004・人物#31・課長級
```

- 数字と `:` だけ（非 ASCII・人物名・表示文面を**キーへ入れない**）。
- `TryDecode` は **正規形と完全一致するものだけ**通す＝接頭辞違い／未知の版（0・2）／項目数違い／
  範囲外の行為・段／負数／符号・空白・桁区切り・桁揃え（`031`）はすべて拒否し、`out` は既定値のまま。
- 版・項目数・接頭辞は `KeyVersion` / `KeyFieldCount` / `EffectPrefix` の定数（構造を変えたら版を上げる）。
- `IsCivilServiceKey`（前方一致）は**振り分け専用**＝壊れた人事キーも人事経路へ送り、
  一般の分野推定（`DomainOf`→内政）へ黙って落とさない。中身の妥当性は `TryDecode` が見る。

## 3. 段（承認を問う相手）の決め方

`ResolveApprovalGrade(台帳, 行為, 人物, 指定段)`
- 配属＝`一般官僚`（入省の段）
- 昇任・降任＝就ける段
- 異動・解任＝**台帳の現職の段**（未在任なら指定段のまま）

`CivilServicePostRules.Check` の①（職位の組み立て）と同じ規則を**再実装せず**、
公開の参照窓口 `FindServing` を読むだけ。可否は一切判定しない。

## 4. 経路（1本に統合）

```
UI（次作業） → GalaxyView.PreviewPlayerCivilServicePost   … read-only（Check そのもの）
             → GalaxyView.SubmitPlayerCivilServicePost
                  ├ Check.ok        → CivilServicePostRules.Execute（即時・カードを作らない）
                  ├ Check.canPetition→ Petition＋PendingDecision（既存台帳・既存保存経路）
                  └ それ以外         → 却下（理由つき・状態は不変）

決裁デスク → DecisionDeck.Resolve → AuthorityCheck（DecisionAuthorityDirector）
          → RingiDirector.OnResolved → GalaxyView.ExecuteApprovedCivilServicePost
          → CivilServicePostRules.Execute（決裁時の状態でやり直す）
```

- 返り値 `CivilServiceRequestResult`＝`outcome{実行/上申/却下}`・`reason`・`decisionId`・`addresseeId`・`effectKey`。
  呼出側は `DidExecute` / `DidPetition` / `Accepted` で分岐できる。
- 操作者は `PlayerCharacter()` のみ（UI から任意の人物を操作者に渡せない）。
- 台帳（`FactionState.civilService`）が無い勢力は**作らずに理由を返す**＝年次人事の初期化＋既存配属の移行
  （`MigrateExistingStaff`）を飛ばさない。

## 5. 稟議に載せるときの約束

- **官僚機構の生存ロールを通さない**：`RingiPipeline.Propagate` を挟まず `Submit`→`SendToDecision`。
  権限外の操作を正規の上申先へ**必ず**届ける（握り潰し・黙殺で消えない）。
- **省益で人事を値切らない**：`friction = 0`、執行の実効量は 1.0 固定（人事は規模を持たない二値の決定）。
  `PetitionEffects` / `RingiPipeline.ExecuteAndApply` / `PetitionActionRules` には**流さない**。
- カードに載せるもの＝提案者・決裁者（＝復号した省の所管大臣／事務次官級は首相）・権限の根拠・対象・理由、
  選択肢は「裁可する」「見送る（現状維持）」（既定＝見送る）。
- **同一の未解決 effectKey は二重起票しない**（決裁カードから判定＝シーン往復・保存でも失わない）。
- 保存形式は**据え置き**（`PetitionSave` / `DecisionSave` に新規フィールドを足していない）。
  理由はカード本文の**最終行**（`理由：…`）に置き、裁可時に `ExtractReason` で人事履歴へ渡す。

## 6. 権限判定（#7 の要求）

`DecisionAuthorityDirector.EvaluateFor` に人事キーの分岐を1つだけ足し、
`GalaxyView.EvaluateCivilServiceAuthority` → **復号した省id・段・人物id**を
`CivilServicePostRules.ApprovalAuthority` に渡す（一般の `DecisionAuthorityRules.DomainOf` は使わない）。

- この分岐は `AuthorityCheck`（右下カード・決裁ボード・スクリプト）／上申の承認確定（`EvaluateDecider`）／
  見込み表示（`TryPreviewAuthority`）／起票の帰属（`StampAttribution`）の**すべてが通る1か所**。
- 毎回その時点の内閣・委任・名簿から組み直す＝**大臣交代・委任の期限切れ・首相交代・失職を古い権限で通さない**。
- `CabinetDecisionAuthorityRules`（一般の決裁）には触れていない＝統治政策・艦隊・税の稟議の判定は不変。
- 執行の直前にも `ResolveCivilServiceApprover` で承認権限を引き直す（カード記録の決裁者 → 現在の操作者の順）。
  どちらも承認できなければ台帳を変えず理由を返す。

## 7. 一度だけ・承認と執行成功の区別

- 適用の権利は既存の `DecisionResolutionRules.ClaimForApply` のまま（承認・自動解決・二重クリック・
  シーン往復のどれでも1回）。`RingiPipeline.Decide` と結果記録も既存経路。
- 裁可できたことと効いたことは別＝空席消滅・資格喪失・異動済み・対象者の死亡などは
  `PendingDecision.resultDetail`（`outcome != 実行`）と人事カテゴリの通知に残し、台帳は動かさない。
  執行できなかった稟議は実効0で閉じる（「承認」のまま在庫に残さない）。
- 見送りは人事を変えず `Petition` を却下で閉じ、カードも一度だけ確定する。

## 8. 試験（**未実行＝本作業票でテスト実行が禁止**）

EditMode `CivilServiceRingiRulesTests`（9件）
1. 正規形・ASCII・版番号の固定（`civilservice.post:1:2:1004:31:1`）
2. 全行為×全段の往復＋再エンコード一致
3. 大きな id の保持
4. 不正キー20種の拒否（null/空/空白/他系統キー/項目数/大小/版/範囲外/負数/符号/空白/桁揃え/非数字/桁区切り）
5. `IsCivilServiceKey` の前方一致（壊れたキーも人事へ振り分ける）
6. 承認を問う段の解決（配属/昇任/降任/異動/解任・未在任・null 安全）
7. 理由の既定文・改行畳み・長文切り詰め・見出し衝突の回避
8. 本文への埋め込みと `ExtractReason` の往復（理由は最終行）
9. `DecisionEffectRegistryRules` が人事キーを実装済みと認識し、壊れたキーは未実装（既存キーの扱いは不変）

PlayMode `CivilServiceRingiPlayModeTests`（5件）
1. 所管大臣は直接実行（カードを作らない・台帳と `Ministry.staffIds` が一致・理由が履歴へ）
2. 理由が空なら既定文を履歴へ残す
3. 権限外の起案者 → **軍事所管の兵部省の大臣**へ上申（＝分野推定〔内政〕で代用していない）／
   カードの効果キー・提案者・決裁者・選択肢・本文の理由／稟議は決裁待ちで台帳に載る（握り潰されない）／
   二重起票の拒否／起案者本人では裁可できず台帳が動かない／大臣の裁可で一度だけ反映／二度目は無効
4. 見送りは無変更で稟議を却下＝再裁可もできない／承認前に対象者が死亡した場合は台帳不変・理由を結果へ・
   稟議を「承認」のまま残さない
5. 保存往復（`WriteDecisions`/`WritePetitions`→JSON→`ReadDecisions`/`ReadPetitions`）で
   効果キー・対象（復号した省と人物）・決裁者・提案者・稟議id・理由が保たれる

既存試験は**未編集**（緩めていない）。

## 9. やっていないこと・次作業へ

- 操作画面（UI 本体）＝次作業。本作業は公開入口（`Preview*` / `Submit*` / `ExecuteApproved*` / `EvaluateCivilServiceAuthority`）まで。
- `docs/catalog/core-modules-catalog.md` への1行追記は**許可ファイル外のため未実施**（次作業か親で追記が要る）。
- シーン・プレハブ・実セーブ・保存形式は未変更。AI 勢力の自動人事は年次人事（`CivilServiceAnnualRules`）のまま。
