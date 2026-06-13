# GameCI セットアップ（留守中に無人でゲームをテストする土台）

> GameCI = GitHub Actions 上で headless Unity を動かす仕組み。これで dotnet TestHarness では
> 検証できない **Game 層（MonoBehaviour/UI）のコンパイル＋Unity テスト**を無人で回せる。
> public リポジトリなので GitHub Actions の実行は無料。
> 将来：自動プレイテストharness（[`playtest-harness.md`](./playtest-harness.md)）を PlayMode で回し、
> スクショ＋ビジョンで視覚バグ/改善点を検出 → `agent-ready` issue → 自動実装（[`auto-implement-workflow.md`](./auto-implement-workflow.md)）へ一周つなぐ。

Unity バージョン：**6000.4.9f1**（`ProjectSettings/ProjectVersion.txt`）。ライセンス：**Personal（無料）**。

## ⚠ 重要：Personal ライセンスは `.ulf`（`UNITY_LICENSE`）が必須

**実測（2026-06 CI ログ）で判明：`game-ci/unity-test-runner@v4` の Personal ライセンスは
`UNITY_EMAIL`+`UNITY_PASSWORD` だけでは動かない。** ガード（メール/パスワード検出）は通るが、
実行ステップが即座に `Missing Unity License File and no Serial` で失敗する。
メール＋パスワードだけで足りるのは **Pro/Plus（＋`UNITY_SERIAL`）** の場合だけ。

→ **Personal（無料）は `.ulf` ライセンスファイルの中身を Secret `UNITY_LICENSE` に入れる**のが必須。
`unity-test.yml` は `UNITY_LICENSE` を既に配線済み（追加のワークフロー変更は不要）。

### `.ulf` の入手（Unity が手動 alf→ulf を Personal で廃止したため、ローカル取得が現実解）
かつての `.alf`→`.ulf` 手動アクティベーション（`license.unity3d.com/manual`）は **Unity が Personal で廃止**
（[game-ci/unity-test-runner #235](https://github.com/game-ci/unity-test-runner/issues/235) /
[game-ci/documentation #408](https://github.com/game-ci/documentation/issues/408)）。
現行の確実な方法は**自分の PC で Unity Hub から Personal ライセンスを有効化し、生成された `Unity_lic.ulf` を使う**：

1. Unity Hub →（歯車）Preferences → **Licenses** → **Add** → **Get a free personal license**。
2. OS ごとの場所から `Unity_lic.ulf` を開く：
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`（`ProgramData` は隠しフォルダ）
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. **ファイル全体（XML）をコピー**し、GitHub Secret `UNITY_LICENSE` に貼る。

> ⚠ **Unity 6 / Hub 3.x では `Unity_lic.ulf` がローカルに生成されないことがある**（既知の問題：
> [game-ci/documentation #408](https://github.com/game-ci/documentation/issues/408)）。
> 見つからない場合の現実的な選択肢：(a) Unity Pro/Plus にして `UNITY_SERIAL` 方式（CI が一番安定）、
> (b) **GameCI を諦め、軽量な dotnet TestHarness（`dotnet-tests.yml`＝全 Core/Data を検証）を CI の正とし、
> Game 層はローカル Unity エディタで目視**（リポジトリ既定の「ライセンス地雷回避」方針）。

## 手順（GitHub Secrets）

GitHub → リポジトリ `yamamax55/ginei-game` → **Settings → Secrets and variables → Actions → New repository secret** で作る：

| Name | Secret | 必須？ |
|---|---|---|
| `UNITY_LICENSE` | `.ulf` ファイルの中身（XML 全体） | **Personal は必須** |
| `UNITY_EMAIL` | Unity ID のメールアドレス | 必須 |
| `UNITY_PASSWORD` | Unity ID のパスワード | 必須 |
| `UNITY_SERIAL` | シリアル番号 | **Pro/Plus のみ**（Personal は不要・空のまま） |

> ⚠ Unity Cloud（cloud.unity.com）の Secrets ではなく **GitHub リポジトリ**の Secrets。
> ⚠ Pro/Plus なら `UNITY_LICENSE` の代わりに `UNITY_SERIAL`＋メール/パスワードでよい。


### 動作確認
GitHub → Actions → **`unity-test`** → **Run workflow**（または PR を更新）。
ライセンス情報があれば EditMode テストが実 Unity で走る（未設定なら自動スキップ）。
初回は Library キャッシュが無く時間がかかる（以降キャッシュで短縮）。

### セキュリティ・注意
- **Google/Apple 等の SSO でログインしている場合は Unity 固有のパスワードが無い**ため、メール+パスワード方式は使えない。→ **CI 専用の Unity アカウント（メール+パスワードで新規作成）**を1つ作って使う（下記）。
- Unity アカウントのパスワードを Secret に置くことになる。GitHub Secret は暗号化保管されるが、本アカウントを露出させたくないので **CI 専用アカウント推奨**。
- **2要素認証（2FA）を有効にしているとメール/パスワード方式は失敗する**。CI 専用アカウントは 2FA を付けない。

#### CI 専用 Unity アカウントの作り方
1. ブラウザのシークレットウィンドウで https://id.unity.com を開き、**「Sign in with Google」ではなくメール+パスワードで新規 Unity ID を作成**（Gmail のエイリアス `あなた+unityci@gmail.com` で別アカウントにできる）。確認メールを承認。
2. 2FA は付けない。
3. その資格情報で **Unity Hub に一度サインイン**して Personal ライセンスを取得＋初回の規約同意を済ませる（CI のヘッドレス起動が規約画面で止まらないように）。
4. GitHub Secrets に `UNITY_EMAIL`（新アカウントのメール）/ `UNITY_PASSWORD`（そのパスワード）を入れる。
- Personal はアクティベーション回数に上限がある。`unity-test.yml` は回数を抑えるため**定期 cron を付けず PR＋手動のみ**にしている（頻度を上げたい/Pro 化したら schedule を足す）。

## （参考）`.ulf` が手に入る場合
旧ライセンス方式等で `Unity_lic.ulf` が `C:\ProgramData\Unity\` にあるなら、その中身を Secret `UNITY_LICENSE` に貼ってもよい（`unity-test.yml` は両対応）。新方式で無ければメール＋パスワードを使う。

## ワークフロー

| ファイル | 役割 |
|---|---|
| `.github/workflows/unity-test.yml` | 実 Unity で EditMode テスト＝**Game 層のコンパイル＋テスト検証**。PR＋手動。ライセンス未設定なら自動スキップ。 |
| `.github/workflows/dotnet-tests.yml` | 既存。純ロジックを Unity 無しで高速検証（こちらは常に走る）。 |

## 次の段（このセットアップ後）
1. **PlayMode で自動プレイテスト**：`unity-test.yml` に PlayMode ステップを足し、`PlaytestRunner` を駆動して `PlaytestReport`（バグ/改善点）をアーティファクト化。
2. **スクショ＋ビジョン**：自動会戦中のスクショをアーティファクト化 → Claude のビジョンが視覚バグ/UX を指摘。
3. **改善点の自動起票**：レポート＋スクショを定期レビュー → `agent-ready` issue → 自動実装へ。
