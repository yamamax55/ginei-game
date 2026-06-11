# 自動実装ワークフロー（留守中でも実装が前進する仕組み）

> 設計の狙い：あなたがPCの前にいない時間にも、**仕様が固まった低リスクの実装**を Claude が進め、
> 帰宅後は **draft PR をレビュー/マージするだけ** にする。`worldview-epic`（設計＋issue化の自動化）の
> 「実装版」。世界観パイプラインが *タネ（issue）* を撒き、こちらが *芽（実装PR）* を出す。

ワークフロー定義: [`.github/workflows/auto-implement.yml`](../.github/workflows/auto-implement.yml)

## 使い方（3ステップ）

1. **無人で実装してよい issue に `agent-ready` ラベルを付ける。**
   - ラベルはワークフロー初回実行時に自動作成される（緑・`agent-ready`）。
   - 付ける基準＝**仕様が明確 / テストで検証できる / 低リスク**。下の「向き/不向き」を参照。
2. **待つ。** 4時間ごと（cron `47 */4 * * *`）に1件ずつ拾って実装する。
   すぐ回したいときは Actions タブ → `auto-implement` → **Run workflow**（issue番号を直接指定も可）。
3. **draft PR をレビューしてマージ。**
   - PR本文に変更要約・設計判断・TestHarness 結果が載る。
   - Game層に触れた PR は本文冒頭に「⚠ Unity目視検証が必要」と明記される → エディタで確認してからマージ。

## 安全装置（なぜ留守中に任せても壊れないか）

| 装置 | 内容 |
|---|---|
| ラベルゲート | `agent-ready` を**人が付けた** issue だけが対象。誤爆しない。無ければ何もしない。 |
| テストゲート | `dotnet test TestHarness/GineiLogic.Tests.csproj` がグリーンになるまで直す（純ロジック）。 |
| draft 限定 | PR は必ず draft。あなたの承認なしに master へ入らない。 |
| 直列実行 | concurrency group で1度に1 issue。並走PRのコンフリクトを防ぐ。 |
| 撤退条件 | 仕様が曖昧/大規模リファクタ/規約に抵触で自信が持てなければ、**実装せず** issue にコメントを残して終了。憶測で大改修しない。 |
| 正直さ | TestHarness が RED のままなら隠さず PR本文に「⚠ TestHarness RED」と明記。 |

## 向き / 不向き（`agent-ready` を付ける判断）

**向いている（無人でよい）**
- 純ロジック（`Core/`・`Data/`）の新規/拡張＝EditModeテストで完全に検証できる。
  例：新しい `*Rules`（static）モジュール、enum追加、既存窓口へのメソッド追加。
- 仕様・完了条件が issue に明記され、接続先（設計書§2）が決まっているもの。
- CLAUDE.md「テスト基盤」節の *配線待ちの純ロジック*（Wave1/Wave2）を盤面/UIへ薄く配線する系の前半（純ロジック部分）。

**不向き（自分でやる / GameCI 導入後に回す）**
- Game層の見た目・操作感（MonoBehaviour/UI/シーン）が主目的＝Unity 目視が必須。
- 固定の子オブジェクト名規約・選択管理・シングルトン・Esc優先順位など**壊すと不具合**の核に触る変更。
- アーキテクチャ判断を伴う大きな設計変更。

> 非コア（Game層）も無人で安全に回すには、レバー1-B＝**GameCI で headless Unity PlayMode テスト**を CI 化する必要がある。
> 導入後はこのワークフローのテストゲートに Unity テストを追加し、不向きの一部を「向き」へ昇格できる。

## 調整ポイント

- **頻度**: `.github/workflows/auto-implement.yml` の `cron` を変更（例：夜間だけ／毎時）。
- **コスト**: 1実行=1 Claude 実装セッション。`agent-ready` の数 ≦ 1日あたりの実行回数 に収まる範囲で運用。
- **PR の CI**: GITHUB_TOKEN で作った PR は `pull_request` ワークフローを起動しない仕様のため、テストはこのジョブ内で完結させている。
  PR にも独立CIを走らせたい場合は PAT（`repo`/`workflow` 権限）を secret に入れて push に使う。

## 関連

- `worldview-epic` スキル / `.github/workflows/worldview-epic*.yml` … 設計＋issue化の自動化（このワークフローの上流）。
- `TestHarness/README.md` … Unity 無し純ロジック検証の土台。
- `CLAUDE.md` … 最上位規約（自動実装エージェントもこれに従う）。
