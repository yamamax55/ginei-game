# GameCI セットアップ（留守中に無人でゲームをテストする土台）

> GameCI = GitHub Actions 上で headless Unity を動かす仕組み。これで dotnet TestHarness では
> 検証できない **Game 層（MonoBehaviour/UI）のコンパイル＋Unity テスト**を無人で回せる。
> public リポジトリなので GitHub Actions の実行は無料。
> 将来：自動プレイテストharness（[`playtest-harness.md`](./playtest-harness.md)）を PlayMode で回し、
> スクショ＋ビジョンで視覚バグ/改善点を検出 → `agent-ready` issue → 自動実装（[`auto-implement-workflow.md`](./auto-implement-workflow.md)）へ一周つなぐ。

Unity バージョン：**6000.4.9f1**（`ProjectSettings/ProjectVersion.txt`）。ライセンス：**Personal（無料）**。

## ⚠ 重要：Unity 6 の新ライセンス方式では `.ulf` が作られない
- かつての `.alf`→`.ulf` 手動アクティベーションは **Unity が Personal で廃止**（`unity-request-activation-file` は削除済み）。
- さらに **Unity 6 / Hub 3.x の新ライセンス方式では、ローカルにも `Unity_lic.ulf` が生成されない**ことがある（既知の問題：[game-ci/documentation #469](https://github.com/game-ci/documentation/issues/469)）。`C:\ProgramData\Unity` を探しても `.ulf` が無いのはこのため。

→ **現行の確実な方法＝Unity ID の「メール＋パスワード」を GitHub Secret に入れ、CI が実行ごとにアクティベートする**（GameCI v4 が対応）。`.ulf` は不要。

## 手順（メール＋パスワード方式・推奨）

GitHub → リポジトリ `yamamax55/ginei-game` → **Settings → Secrets and variables → Actions → New repository secret** で**2つ**作る：

| Name | Secret |
|---|---|
| `UNITY_EMAIL` | Unity ID のメールアドレス |
| `UNITY_PASSWORD` | Unity ID のパスワード |

> ⚠ Unity Cloud（cloud.unity.com）の Secrets ではなく **GitHub リポジトリ**の Secrets。
> ⚠ Pro/Plus なら追加で `UNITY_SERIAL` も入れる（Personal は不要）。

### 動作確認
GitHub → Actions → **`unity-test`** → **Run workflow**（または PR を更新）。
ライセンス情報があれば EditMode テストが実 Unity で走る（未設定なら自動スキップ）。
初回は Library キャッシュが無く時間がかかる（以降キャッシュで短縮）。

### セキュリティ・注意
- Unity アカウントのパスワードを Secret に置くことになる。GitHub Secret は暗号化保管されるが、気になるなら **CI 専用の Unity アカウントを作り、その組織に Personal シートを割り当てて**使うと安全（本アカウントを露出させない）。
- **2要素認証（2FA）を有効にしているとメール/パスワード方式は失敗する**ことがある（CI 専用アカウントは 2FA 無しにするか、トークン方式を使う）。
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
