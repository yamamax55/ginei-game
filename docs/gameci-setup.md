# GameCI セットアップ（留守中に無人でゲームをテストする土台）

> GameCI = GitHub Actions 上で headless Unity を動かす仕組み。これで dotnet TestHarness では
> 検証できない **Game 層（MonoBehaviour/UI）のコンパイル＋Unity テスト**を無人で回せる。
> 将来：自動プレイテストharness（[`playtest-harness.md`](./playtest-harness.md)）を PlayMode で回し、
> スクショ＋ビジョンで視覚バグ/改善点を検出 → `agent-ready` issue → 自動実装（[`auto-implement-workflow.md`](./auto-implement-workflow.md)）へ一周つなぐ。

Unity バージョン：**6000.4.9f1**（`ProjectSettings/ProjectVersion.txt`）。ライセンス：**Personal（無料）**。

## ⚠ 重要：Personal の「CIでのアクティベーション」は廃止された
かつての `game-ci/unity-request-activation-file`（`.alf`→`.ulf`）は **Unity が Personal の手動アクティベーション自体を廃止**したため**もう動きません**（GitHub Actions の `unity-activation` ワークフローは削除済み）。
代わりに、**ローカルでアクティベーション済みの Unity のライセンスファイル（`Unity_lic.ulf`）をそのまま GitHub Secret に貼る**のが現行の確実な方法です。あなたは Unity をローカルで使えているので、追加のアクティベーション作業は不要です。

参考：[mackysoft/Unity-ManualActivation](https://github.com/mackysoft/Unity-ManualActivation)。

## 手順（3ステップ）

### 1. ローカルの `Unity_lic.ulf` を見つける
ローカルで Unity（Personal）にサインイン済みなら、ライセンスファイルがここにある：

| OS | パス |
|---|---|
| Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |
| macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |
| Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |

> 見つからない場合は、Unity Hub にサインイン → エディタを一度起動して Personal ライセンスを取得すると生成される。

### 2. 中身をコピー
`Unity_lic.ulf` を**テキストエディタで開き、中身（XML）を全文コピー**する。

### 3. GitHub Secret に貼る
GitHub → リポジトリ **Settings** → **Secrets and variables** → **Actions** → **New repository secret**。
> ⚠ Unity Cloud（cloud.unity.com）の Secrets ではない。**GitHub リポジトリ**の Secrets。
- Name：`UNITY_LICENSE`
- Secret：手順2でコピーした `.ulf` の中身（全文）
- Add secret。

### 動作確認
GitHub → Actions → **`unity-test`** → **Run workflow**（または PR を更新）。
`UNITY_LICENSE` が入っていれば EditMode テストが実 Unity で走る（未設定なら自動スキップ）。
初回は Library キャッシュが無く時間がかかる（以降キャッシュで短縮）。

## ワークフロー

| ファイル | 役割 |
|---|---|
| `.github/workflows/unity-test.yml` | 実 Unity で EditMode テスト＝**Game 層のコンパイル＋テスト検証**。PR＋6時間ごと。`UNITY_LICENSE` 未設定なら自動スキップ。 |
| `.github/workflows/dotnet-tests.yml` | 既存。純ロジックを Unity 無しで高速検証（こちらは常に走る）。 |

## 次の段（このセットアップ後）
1. **PlayMode で自動プレイテスト**：`unity-test.yml` に PlayMode ステップを足し、`PlaytestRunner` を駆動して `PlaytestReport`（バグ/改善点）をアーティファクト化。
2. **スクショ＋ビジョン**：自動会戦中のスクショをアーティファクト化 → Claude のビジョンが視覚バグ/UX を指摘。
3. **改善点の自動起票**：レポート＋スクショを定期レビュー → `agent-ready` issue → 自動実装へ。

## Pro / Plus を使う場合（参考）
GitHub Secrets に `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` の3つを入れ、
`unity-test.yml` の env をそれに合わせる（現状は Personal 前提で `UNITY_LICENSE` を読む）。
