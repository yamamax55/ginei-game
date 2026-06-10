# TestHarness — 純ロジック検証ハーネス（Unity 不要）

`Assets/Scripts/Core`（純ロジック・#496 asmdef 4分割後）と `Assets/Tests/EditMode` のテストを、
Unity 無しの環境（クラウドセッション・CI）で **dotnet test** によりコンパイル＆実行する。

```bash
cd TestHarness
dotnet test -v q     # .NET 8 SDK（Ubuntu: apt install dotnet-sdk-8.0）
```

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
