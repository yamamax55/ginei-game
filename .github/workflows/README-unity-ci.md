# Unity CI（GitHub Actions / GameCI）セットアップ

このリポジトリには、各 Pull Request と `master` への push で **プロジェクトがコンパイルできるか**を
自動検証する CI を用意しています（[GameCI](https://game.ci/) の `unity-builder` を使用）。

- ワークフロー本体: [`unity-ci.yml`](./unity-ci.yml) … `StandaloneLinux64` でビルド＝全スクリプトをコンパイル検証
- Unity バージョン: `ProjectSettings/ProjectVersion.txt`（現在 **6000.4.9f1**）から自動検出

> CI で Unity を動かすには **Unity ライセンス認証情報**を GitHub の **Secrets** に登録する必要があります。
> 登録するまでビルドはスキップされ、警告だけが表示されます（PR が赤くなって止まることはありません）。

---

## セットアップ手順（Unity Personal＝無料ライセンス・推奨）

開発PCに Unity を入れて開発しているなら、**ローカルで既に発行済みのライセンスファイル(`.ulf`)を
そのまま Secret に使う**のが最短・最確実です（追加のワークフロー実行は不要）。

### 1. ローカルの `.ulf` を見つける
Unity Hub / Editor で Personal ライセンスを認証済みなら、開発PCの次の場所にあります：

| OS | パス |
|---|---|
| Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |
| macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |
| Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |

（見つからない場合は Unity Hub で一度サインイン＆Personalライセンスを取得すると生成されます。）

### 2. Secrets を登録する
リポジトリの **Settings → Secrets and variables → Actions → New repository secret** で以下を登録：

| Secret 名 | 値 |
|---|---|
| `UNITY_LICENSE` | `Unity_lic.ulf` ファイルの**中身全体**（XML テキストをそのまま貼り付け） |
| `UNITY_EMAIL` | Unity アカウントのメールアドレス |
| `UNITY_PASSWORD` | Unity アカウントのパスワード |

### 3. CI を実行する
- 以後、PR や `master` への push で **Unity CI** が走り、`StandaloneLinux64` ビルド＝コンパイル検証が実行されます。
- 既存の PR に CI を効かせるには、その PR に `master` を取り込んでください。
- 手動で試すなら Actions タブの **Unity CI → Run workflow** からも実行できます。

---

## Unity Pro / Plus（有料ライセンス）を使う場合
`.ulf` は不要。Secrets に以下を登録すれば動きます（`unity-ci.yml` はそのまま利用可）：

| Secret 名 | 値 |
|---|---|
| `UNITY_SERIAL` | シリアルキー（`XX-XXXX-...`） |
| `UNITY_EMAIL` | Unity アカウントのメールアドレス |
| `UNITY_PASSWORD` | Unity アカウントのパスワード |

`unity-builder` は `UNITY_SERIAL` があればそれを優先して認証します（`unity-ci.yml` にも
`UNITY_SERIAL` を env に追加してください。Personal の場合は不要）。

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

> 補足：GameCI の旧 `unity-request-activation-file` アクションは廃止されたため、認証ファイル(.alf)取得用
> ワークフローは削除しました。上記「ローカルの .ulf を使う」方法を利用してください（最新の手順は
> <https://game.ci/docs/github/activation> も参照）。
