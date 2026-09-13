# TestHarness — 純ロジック検証ハーネス（Unity 不要）

`Assets/Scripts/Core`（純ロジック・#496 asmdef 4分割後）と `Assets/Tests/EditMode` のテストを、
Unity 無しの環境（クラウドセッション・CI）で **dotnet test** によりコンパイル＆実行する。

```bash
cd TestHarness
dotnet test -v q     # .NET 8 SDK（Ubuntu: apt install dotnet-sdk-8.0）
```

## クラウドでの関連Core試験（.NET 8・Unity 無し）

クラウドセッションのコンテナには既定で `dotnet` が入っていないことがある。
`GineiLogic.Tests.csproj` は `net8.0` 指定なので、**.NET 8 SDK** を用意してから走らせる。

```bash
# 1) .NET 8 SDK（Ubuntu 24.04 公式リポジトリ。無ければ導入）
apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-8.0
dotnet --version        # 8.0.x

# 2) 復元・ビルド
cd TestHarness
dotnet restore
dotnet build -v q --nologo

# 3) 全件
dotnet test --no-build -v q --nologo

# 4) 関連Core試験だけ（BattleQa系＋軍団間隔＋艦名レジストリ）
dotnet test --no-build -v q --nologo \
  --filter "FullyQualifiedName~BattleQa|FullyQualifiedName~CorpsSpacingRulesTests|FullyQualifiedName~ShipNameRegistryTests"
```

対象クラス（`Assets/Tests/EditMode/`）：
`BattleQaFeatureSwitchesTests` / `BattleQaJudgeRulesTests` / `BattleQaPresetCatalogTests` /
`BattleQaReinforcementPlanTests` / `BattleQaRunLogTests` / `BattleQaSnapshotTests` /
`BattleQaTuningProfileTests` / `CorpsSpacingRulesTests` / `ShipNameRegistryTests`。

注意：
- `--list-tests` は `--filter` を無視して全件列挙する。クラス別の内訳が要るときは
  `--filter "FullyQualifiedName~<クラス名>"` を1クラスずつ回す。
- ローカルPCに .NET 8 が無い場合は `DOTNET_ROLL_FORWARD=Major` で .NET 10 実行に倒せるが、
  クラウドでは上記のとおり .NET 8 SDK を入れるのが素直。
- ここで通るのは純ロジックのみ。**PlayMode・描画・シーン挙動は Unity エディタでしか確認できない**
  （クラウドで未実施なら「未実施」と記録する。合格と書かない）。

## 仕組み
- `GineiLogic.Tests.csproj` が `Core/**` 全部＋`Data/`（IO層の `SaveManager`/`CampaignSaveManager` を除く）＋テストを直接コンパイル（asmdef は使わない）。
- `Stubs/UnityStubs.cs`：UnityEngine の使用面だけ実装（`Mathf`/`Vector2`/`Color`/`ScriptableObject`/
  `JsonUtility`(System.Text.Json 代替)/属性/`InputSystem.Key` など）。
- `Stubs/GineiShims.cs`：Unity 型の最小スタブ（`Transform` など）。
  ※`Formation`/`ShipClass` は #496 で Core の単独ファイルになったためシム供給は廃止（Core ソースをそのまま取り込む）。

## このハーネスで検証できないもの（Unity エディタで確認）
- MonoBehaviour / UI / シーン挙動（`Assets/Scripts/Game/` は丸ごと対象外）。
- `DamagePopupStyleTests`（MonoBehaviour 依存のため除外）。
- 実 `JsonUtility` の厳密なシリアライズ挙動（ここでは System.Text.Json 近似）。

## 運用
- 新しい純ロジック＋テストを `Core/` に足したら、そのまま `dotnet test` が拾う（glob で自動包含）。
- **新規ファイルの置き場所が所属を決める**：MonoBehaviour/UI は `Game/`（自動で対象外）・データアクセス/IO は `Data/`（IOなら csproj の Exclude へ）。
- フォルダは `Assets/` 外なので Unity は読み込まない（.meta 不要）。
