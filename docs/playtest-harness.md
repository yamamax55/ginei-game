# 自動プレイテストharness（ゲーム画面の自動テスト）

> 狙い：AI 対 AI で会戦を速回しし、**「観てる人間が気づく異常」を機械化**してバグ/改善点をレポートする。
> 留守中でも無人で回す（GameCI headless Unity）ことを見据えた土台。設計の議論はチャット参照。

## 構成（Core 判定 ＋ Game 殻）

| 層 | クラス | 役割 |
|---|---|---|
| Core（純ロジック・test-first） | `PlaytestInvariants` | 観測→所見の**判定ロジック**。閾値・式はここに集約 |
| Core | `PlaytestObservations` | 殻が集める**観測入力**（Unity 型非依存） |
| Core | `PlaytestReport` / `PlaytestFinding` | **所見レポート**（JSON 出力・合否・CI 終了コード推奨値） |
| Game（MonoBehaviour 殻） | `PlaytestRunner` | 会戦を駆動し観測を集めて Core に渡すだけ（判定は持たない） |

判定ロジックは `Assets/Tests/EditMode/PlaytestInvariantsTests.cs` で EditMode（TestHarness）担保。
**Game 殻はエディタ/headless Unity でのみ動く**（dotnet TestHarness の対象外）。

## 検出する不変条件（v1）

| 所見 | 重要度 | 何を捕まえるか |
|---|---|---|
| 実行時例外/Error ログ | 致命 | NRE・フォント例外など（過去の「一瞬で艦隊が消える」級） |
| 会戦が決着しない | 警告 | デッドロック・勝敗判定の不備 |
| 旗艦の瞬間大量消失 | 警告 | 1サンプルで8割以上消失＝消滅バグの兆候 |
| 全艦が静止 | 注意 | 移動・AI が機能していない |
| HUD が画面外/見切れ | 注意 | レイアウト不備（v1 は収集未配線・Core は対応済み） |
| Material 破棄漏れ | 注意 | 実行時生成 Material のリーク（v1 は未計測） |
| 開始時に敵対ペア無し | 注意 | シナリオ/陣営設定の不備 |

## 使い方

### エディタで（今すぐ）
1. Battle シーンを開く（または任意の GameObject）に `PlaytestRunner` を AddComponent。
2. `scenarioName`（空なら現在設定）・`timeScale`（速回し）・`maxDurationSeconds` を設定して Play。
3. コンソールに所見サマリ、`Application.persistentDataPath/playtest-report.json` にレポートが出る。`PlaytestRunner.LastReport` でも参照可。

### バッチ（headless・無人実行の土台）
```
Unity -batchmode -nographics -projectPath . \
  -executeMethod (起動シーン経由) -ginei-playtest "アスターテ会戦" "./playtest-report.json"
```
起動引数 `-ginei-playtest [シナリオ名] [出力パス]` があれば `PlaytestRunner.Bootstrap`（`RuntimeInitializeOnLoadMethod`）が
自動で Runner を生成し、Battle シーンへ遷移→会戦を回し→JSON 出力→`SuggestedExitCode` で `Application.Quit`。
**引数が無ければ通常プレイに一切影響しない**（no-op）。

## ロードマップ

1. **（済）Core 判定＋Game 殻 v1** — 機能系の不変条件＋JSON レポート。
2. **GameCI 配線（レバー1-B）** — `UNITY_LICENSE` を Secrets に入れ、headless Unity でこの harness を定期実行＝**留守中に無人でゲーム画面テスト**。`SuggestedExitCode` で CI を赤/緑判定。
3. **画面テスト（スクショ＋ビジョン）** — 自動会戦中にカメラからスクショ→アーティファクト→Claude のビジョンが視覚バグ/UX改善を指摘。`PlaytestReport` の表示系所見（HUD 見切れ）もここで殻側の収集を有効化。
4. **改善提案の自動起票** — レポート＋スクショを定期レビューし、`agent-ready` issue/PR コメントへ。

> ⚠ Game 殻（`PlaytestRunner`）は headless/エディタ実行でのみ検証可能（dotnet CI 対象外）。
> 初回はエディタ Play で挙動を目視確認してから GameCI へ載せること。

## 関連
- [`auto-implement-workflow.md`](./auto-implement-workflow.md) … 自動実装（4のレポートを issue 化→自動実装に接続できる）。
- `TestHarness/README.md` … 判定ロジックの Unity 無し検証。
