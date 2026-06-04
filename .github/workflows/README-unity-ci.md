# Unity CI（GitHub Actions / GameCI）セットアップ

このリポジトリには、各 Pull Request と `master` への push で **プロジェクトがコンパイルできるか**を
自動検証する CI を用意しています（[GameCI](https://game.ci/) の `unity-builder` を使用）。

- ワークフロー本体: [`unity-ci.yml`](./unity-ci.yml) … `StandaloneLinux64` でビルド＝全スクリプトをコンパイル検証
- 認証ファイル取得用: [`unity-activation.yml`](./unity-activation.yml) … Personal ライセンスの初回設定に使用
- Unity バージョン: `ProjectSettings/ProjectVersion.txt`（現在 **6000.4.9f1**）から自動検出

> CI で Unity を動かすには **Unity ライセンス認証**が必要です。ライセンス認証情報を
> GitHub の **Secrets** に登録するまで、ビルドはスキップされ警告だけが表示されます
> （PR が赤くなって止まることはありません）。

---

## セットアップ手順（Unity Personal＝無料ライセンス）

### 1. 認証ファイル(.alf)を取得する
1. GitHub の **Actions** タブ →「**Unity - Acquire Activation File**」を選び **Run workflow** を実行。
2. 実行完了後、その run の **Artifacts** から `Manual_Activation_File`（`.alf`）をダウンロードして展開。

### 2. ライセンスファイル(.ulf)に変換する
1. <https://license.unity3d.com/manual> を開く。
2. 手順1の `.alf` をアップロードし、**Unity Personal（Personal Edition）**を選択。
3. 発行された `Unity_v20XX.x.ulf`（`.ulf`）をダウンロード。

### 3. Secrets を登録する
リポジトリの **Settings → Secrets and variables → Actions → New repository secret** で以下を登録：

| Secret 名 | 値 |
|---|---|
| `UNITY_LICENSE` | ダウンロードした `.ulf` ファイルの**中身全体**（XML テキストをそのまま貼り付け） |
| `UNITY_EMAIL` | Unity アカウントのメールアドレス |
| `UNITY_PASSWORD` | Unity アカウントのパスワード |

### 4. CI を実行する
- 以後、PR や `master` への push で **Unity CI** が走り、`StandaloneLinux64` ビルド＝コンパイル検証が実行されます。
- 既存の PR に CI を効かせるには、その PR に `master` を取り込んでください（master へこの設定をマージ後）。

---

## Unity Pro / Plus（有料ライセンス）を使う場合
手順1〜2は不要。Secrets に以下を登録すれば動きます（`unity-ci.yml` はそのまま利用可）：

| Secret 名 | 値 |
|---|---|
| `UNITY_SERIAL` | シリアルキー（`XX-XXXX-...`） |
| `UNITY_EMAIL` | Unity アカウントのメールアドレス |
| `UNITY_PASSWORD` | Unity アカウントのパスワード |

`unity-builder` は `UNITY_SERIAL` があればそれを優先して認証します。

---

## 注意・既知のリスク
- **Unity バージョンのDockerイメージがGameCIに存在する必要があります。**
  `6000.4.9f1` のイメージが未公開の場合、ビルドステップが「image not found」で失敗することがあります。
  その際は GameCI のイメージ公開を待つか、`unity-builder` の `with: customImage:` で
  近いバージョンのイメージを指定してください（プロジェクトのバージョンと不一致だと Unity が
  アップグレードを促す点に注意）。GameCI の対応バージョンは <https://game.ci/docs> 参照。
- 無料の GitHub Actions 枠（private リポジトリは月あたり上限あり）を消費します。`concurrency` で
  同一ブランチの古い実行は自動キャンセルしています。
- 本プロジェクトには自動テスト（EditMode/PlayMode）が未整備のため、CI は「ビルド＝コンパイル検証」を
  ゲートにしています。テストを追加したら `game-ci/unity-test-runner` のジョブを足すと、
  実行時の不具合も検出できます。
