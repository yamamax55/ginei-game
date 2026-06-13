# Unity Build Automation（UBA）調査 — ライセンス不要で実Unityテストを回す

> 背景：GameCI は Personal の `.ulf` が要るが、Unity が Personal の手動アクティベーションを廃止し、
> `.ulf` は「サインイン済みマシン」からしか取れない（[gameci-setup.md](./gameci-setup.md) 参照）。
> ローカル Unity マシンに触れない間の代替として **Unity Cloud の Build Automation（UBA）** を調査した。

## 結論：UBA は使える（ライセンス不要）

- **ライセンスファイル不要**：Unity ID があれば誰でも無料で開始でき、**ライセンスは Unity のクラウドが管理**する＝`.ulf` を用意しなくてよい。これが GameCI に対する最大の利点（あなたの今のブロッカーを回避）。
- **Unity Test Framework（EditMode/PlayMode）を実行できる**：ダッシュボードの Build Automation → Configurations → 対象の編集（鉛筆）→ **Advanced Settings → Tests** で有効化。**「Failed Unit Test Fails Build」**をオンにすればテスト失敗でビルドを失敗扱いにできる。結果はそのビルドの Summary の **Tests タブ**＋ログに出る。
- **GitHub リポジトリ連携**：UBA はソース管理（GitHub 等）に接続してビルド/テストを回す。
- **無料枠（Personal）**：1組織に3シート、**Windows ビルド 200 分/月**、ストレージ（5GB→25GB へ拡張）。

## 注意・トレードオフ（GameCI との違い）

| 観点 | Unity Build Automation | GameCI（GitHub Actions） |
|---|---|---|
| ライセンス | **不要**（Unityが管理） | Personal は `.ulf` が要る（今ブロック中） |
| 設定場所 | **Unity ダッシュボードでGUI設定**（コード化されない） | リポジトリの YAML（Infrastructure as Code） |
| 結果の出どころ | Unity Cloud のビルド Summary（Tests タブ/ログ） | **GitHub の PR チェック＋アーティファクト**（自動実装/PRフローと密** |
| 課金 | 無料枠（200 Win分/月）超過は従量。**2026/3/1〜 Unity DevOps の新課金**が適用 | GitHub Actions の無料枠（public は無料）＋Unityライセンスのみ |
| テスト実行 | ビルド構成に紐づく（ビルド中心） | 専用 `unity-test-runner`（テスト専用） |

→ **UBA はGUI設定・結果はUnity Cloud側・従量**。GameCI は **コード化・GitHub PR と密・無料**だが `.ulf` 待ち。**相補的**＝今は UBA で実Unity検証を解禁し、PCに触れて `.ulf` を取れたら GameCI へ寄せる、が良い。

## 進め方

### フェーズ1：コード変更ゼロで「実Unityテスト」を解禁（あなたがダッシュボードで設定）
1. https://cloud.unity.com → 対象プロジェクト → **DevOps / Build Automation**。
2. ソース管理に **GitHub リポジトリ `yamamax55/ginei-game`** を接続（`master` ブランチ）。
3. **Build target** を1つ作成（Linux か Windows。テスト目的なので最小構成でよい）。
4. その target の **Advanced Settings → Tests** を有効化し、**「Failed Unit Test Fails Build」**をオン（まずは **EditMode**）。
5. ビルドを実行 → **既存の EditMode テスト（1000件超）が実 Unity で走る**＝dotnet TestHarness では出せない **Game 層のコンパイル＋テスト検証**がライセンス無しで得られる（`PlaytestRunner` 等が壊れていないかもここで分かる）。

> ※ UBA の設定はダッシュボードのGUIで完結し、リポジトリのコードには現れない（だから私の側からは設定できない＝あなたの操作が要る）。

### フェーズ2：自動プレイテストを PlayMode テスト化（私がコード用意）
現状の `PlaytestRunner` は起動引数駆動で `[UnityTest]` ではないため、UBA/Test Framework はそのままでは回さない。
**`PlaytestRunner` の駆動を PlayMode テスト（`[UnityTest] IEnumerator`）でラップ**し、`PlaytestReport` を `Assert` する薄いテストを足せば、
UBA でも GameCI でも「AI対AIで会戦を回してバグ/改善点を検査」が回る（PlayMode 用 asmdef が要る）。これは GameCI へ移っても無駄にならない。

### フェーズ3以降
スクショ＋ビジョン（視覚バグ/UX）→ 改善点を `agent-ready` issue → 自動実装（[auto-implement-workflow.md](./auto-implement-workflow.md)）へ一周。

## 出典
- [Unit tests • Build Automation • Unity Docs](https://docs.unity.com/en-us/build-automation/reference/unit-tests)
- [Advanced settings reference • Build Automation • Unity Docs](https://docs.unity.com/en-us/build-automation/advanced-build-configuration/overview)
- [Unity Pricing Changes | Unity](https://unity.com/products/pricing-updates)
- [Understanding Unity DevOps charges（2026/3/1〜）| Unity Support](https://support.unity.com/hc/en-us/articles/34748492914964-Understanding-Unity-DevOps-charges)
