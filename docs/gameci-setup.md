# GameCI セットアップ（留守中に無人でゲームをテストする土台）

> GameCI = GitHub Actions 上で headless Unity を動かす仕組み。これで dotnet TestHarness では
> 検証できない **Game 層（MonoBehaviour/UI）のコンパイル＋Unity テスト**を無人で回せる。
> 将来：自動プレイテストharness（[`playtest-harness.md`](./playtest-harness.md)）を PlayMode で回し、
> スクショ＋ビジョンで視覚バグ/改善点を検出 → `agent-ready` issue → 自動実装（[`auto-implement-workflow.md`](./auto-implement-workflow.md)）へ一周つなぐ。

Unity バージョン：**6000.4.9f1**（`ProjectSettings/ProjectVersion.txt`）。ライセンス：**Personal（無料）**。

## 手順（Personal ライセンス）

ライセンスファイルは鶏卵問題があり、一度だけアクティベーションが要る。

### 1. `.alf`（アクティベーション要求ファイル）を作る
- GitHub → Actions → **`unity-activation`** → **Run workflow**。
- 完了後、その実行ページの **Artifacts** から `Manual Activation File`（`Unity_v6000.4.9f1.alf`）をダウンロード。

### 2. `.alf` を `.ulf` に変換する
- https://license.unity3d.com/manual を開く。
- ダウンロードした `.alf` をアップロード。
- ライセンス種別＝**Personal**（Unity Personal edition）を選んで進む。
- `Unity_v20XX.x.ulf` がダウンロードされる。

### 3. `.ulf` の中身を GitHub Secret に入れる
- GitHub → リポジトリ **Settings** → **Secrets and variables** → **Actions** → **New repository secret**。
  > ⚠ Unity Cloud（cloud.unity.com）の Secrets ではない。**GitHub リポジトリ**の Secrets。
- Name：`UNITY_LICENSE`
- Secret：`.ulf` ファイルを**テキストエディタで開いて中身（XML）を全文コピペ**。
- Add secret。

### 4. 動作確認
- GitHub → Actions → **`unity-test`** → **Run workflow**（または PR を更新）。
- `UNITY_LICENSE` が入っていれば EditMode テストが実 Unity で走る（未設定なら自動スキップ）。
- 初回は Library キャッシュが無く時間がかかる（以降キャッシュで短縮）。

## ワークフロー

| ファイル | 役割 |
|---|---|
| `.github/workflows/unity-activation.yml` | `.alf` を生成（手動・一度だけ）。 |
| `.github/workflows/unity-test.yml` | 実 Unity で EditMode テスト＝**Game 層のコンパイル＋テスト検証**。PR＋6時間ごと。`UNITY_LICENSE` 未設定なら自動スキップ。 |
| `.github/workflows/dotnet-tests.yml` | 既存。純ロジックを Unity 無しで高速検証（こちらは常に走る）。 |

## 次の段（このセットアップ後）
1. **PlayMode で自動プレイテスト**：`unity-test.yml` に PlayMode ステップを足し、`PlaytestRunner` を駆動して `PlaytestReport`（バグ/改善点）をアーティファクト化。
2. **スクショ＋ビジョン**：自動会戦中のスクショをアーティファクト化 → Claude のビジョンが視覚バグ/UX を指摘。
3. **改善点の自動起票**：レポート＋スクショを定期レビュー → `agent-ready` issue → 自動実装へ。

## Pro / Plus を使う場合（参考）
アクティベーション不要。GitHub Secrets に `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` の3つを入れ、
`unity-test.yml` の env をそれに合わせる（現状は Personal 前提で `UNITY_LICENSE` を読む）。
