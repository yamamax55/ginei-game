# クラウド接続試験 結果（2026-09-13 実施）

Claudeクラウドセッションが `yamamax55/ginei-game` を読み書きし、Unity 無しで純ロジック試験を
回せるかだけを確かめた接続試験。ゲームの振る舞いは一切変更していない。

## 基点
| 項目 | 値 |
|---|---|
| リポジトリ | `yamamax55/ginei-game` |
| 基点ブランチ | `cloud/migration-20260914` |
| 基点コミット | `a1a8ed168595fe736b212a2485b409b95b8e64e1`（`chore: preserve Unity development checkpoint for Claude cloud handoff`） |
| 作業ブランチ | `claude/cloud-smoke-20260914`（基点から分岐） |
| 参照した引き継ぎ | `docs/ops/cloud-handoff.md` |

基点は未検証変更を含む保存用スナップショット。安定版ではない。

## 変更内容（2ファイルのみ）
1. `TestHarness/README.md` — クラウドでの .NET 8 セットアップと関連Core試験の実行手順を追記（節「クラウドでの関連Core試験（.NET 8・Unity 無し）」）。文書のみ。
2. `docs/ops/cloud-smoke-result.md` — 本ファイル（新規）。

`Assets/`・`Packages/`・`ProjectSettings/`・セーブデータ・その他ゲームコードは**未変更**。

## 環境
- コンテナに `dotnet` は未導入だった。
- 公式配布（`https://dot.net/v1/dotnet-install.sh` → `builds.dotnet.microsoft.com`）は
  エグレスポリシーにより 403 で拒否。
- Ubuntu 24.04 公式リポジトリ（`archive.ubuntu.com`）は到達可能だったため、そちらから導入：
  `apt-get install -y dotnet-sdk-8.0` → **.NET SDK 8.0.131 / Runtime 8.0.31**。
- NuGet 復元（`Microsoft.NET.Test.Sdk` 17.11.1 / `NUnit` 3.14.0 / `NUnit3TestAdapter` 4.6.0）は成功。
- Unity エディタは未設定（コンテナに存在しない）。

## 実行コマンドと結果

```bash
apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-8.0
cd TestHarness
dotnet restore
dotnet build -v q --nologo
dotnet test --no-build -v q --nologo \
  --filter "FullyQualifiedName~BattleQa|FullyQualifiedName~CorpsSpacingRulesTests|FullyQualifiedName~ShipNameRegistryTests"
dotnet test --no-build -v q --nologo      # 全件
```

### コンパイル
| 項目 | 結果 |
|---|---|
| `dotnet restore` | **成功** |
| `dotnet build` | **成功**（0 Error / 1 Warning） |

警告は `Assets/Tests/EditMode/InnovationWaveRulesTests.cs(59,19) CS0219`（未使用ローカル変数
`prevGain`）のみ。既存のもので本試験の変更とは無関係。修正していない。

引き継ぎ文書が「`battle-qa-reinforcement-isolation-20260913/a1` はコンパイル・試験未実行」と
していた点について、**本セッションで当該コードを含む TestHarness のコンパイルが通ることを確認した**。

### 関連Core試験（対象9クラス）
一括実行：**合格 71 / 71**（Failed 0, Skipped 0, 124 ms）。

クラス別内訳（`--filter "FullyQualifiedName~<クラス名>"` を1クラスずつ実行）：

| テストクラス | 合格 | 失敗 | 合計 | 判定 |
|---|---:|---:|---:|---|
| `BattleQaFeatureSwitchesTests` | 8 | 0 | 8 | 合格 |
| `BattleQaJudgeRulesTests` | 9 | 0 | 9 | 合格 |
| `BattleQaPresetCatalogTests` | 10 | 0 | 10 | 合格 |
| `BattleQaReinforcementPlanTests` | 4 | 0 | 4 | 合格 |
| `BattleQaRunLogTests` | 8 | 0 | 8 | 合格 |
| `BattleQaSnapshotTests` | 9 | 0 | 9 | 合格 |
| `BattleQaTuningProfileTests` | 10 | 0 | 10 | 合格 |
| `CorpsSpacingRulesTests` | 6 | 0 | 6 | 合格 |
| `ShipNameRegistryTests` | 7 | 0 | 7 | 合格 |
| **計** | **71** | **0** | **71** | **合格** |

### 全件（参考・指示範囲外だが同ハーネスで併せて実行）
**合格 9775 / 9775**（Failed 0, Skipped 0, 3 s）。

## 未実施（クラウドで確認できないもの）
Unity 環境が未設定のため、以下は**未実施**（合格とは記録しない）：
- Unity PlayMode 試験（引き継ぎ文書の想定60件）。
- 描画・シーン挙動・MonoBehaviour/UI の目視検証。
- `DamagePopupStyleTests`（MonoBehaviour 依存のため TestHarness から除外されている）。
- 軍団間隔の見た目、移動/旋回、通常会戦、自然イベント観測、30分プレイ評価。
- 実 `JsonUtility` の厳密なシリアライズ挙動（ハーネスは System.Text.Json 近似）。

## 残件・申し送り
- 上記「未実施」項目は元PCの Unity エディタで確認が必要。
- 公式 .NET 配布サイトはエグレス拒否。クラウドで再現するときは Ubuntu リポジトリ経由（`dotnet-sdk-8.0`）を使う。手順は `TestHarness/README.md` に記載済み。
- `InnovationWaveRulesTests.cs` の CS0219 警告は未修整（本試験の範囲外）。
- 本試験では失敗が無かったため、ゲーム修正は一切行っていない。
