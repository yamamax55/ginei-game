# TestHarness — 純ロジック検証ハーネス（Unity 不要）

`Assets/Scripts` の純ロジック（非 MonoBehaviour）と `Assets/Tests/EditMode` のテストを、
Unity 無しの環境（クラウドセッション・CI）で **dotnet test** によりコンパイル＆実行する。

```bash
cd TestHarness
dotnet test -v q     # .NET 8 SDK（Ubuntu: apt install dotnet-sdk-8.0）
```

## 仕組み
- `GineiLogic.Tests.csproj` が純ロジック源＋テストを直接コンパイル（asmdef は使わない）。
- `Stubs/UnityStubs.cs`：UnityEngine の使用面だけ実装（`Mathf`/`Vector2`/`Color`/`ScriptableObject`/
  `JsonUtility`(System.Text.Json 代替)/属性/`InputSystem.Key` など）。
- `Stubs/GineiShims.cs`：MonoBehaviour ファイル内定義の enum を供給
  （`Formation`＝Squadron.cs／`ShipClass`＝EscortShip.cs。**本体定義を変えたらここも同期**）。

## このハーネスで検証できないもの（Unity エディタで確認）
- MonoBehaviour / UI / シーン挙動（csproj の Exclude 一覧＝約37ファイル）。
- `DamagePopupStyleTests`（MonoBehaviour 依存のため除外）。
- 実 `JsonUtility` の厳密なシリアライズ挙動（ここでは System.Text.Json 近似）。

## 運用
- 新しい純ロジック＋テストを足したら、そのまま `dotnet test` が拾う（glob で自動包含）。
- 新規ファイルが MonoBehaviour なら csproj の Exclude へ追記する。
- フォルダは `Assets/` 外なので Unity は読み込まない（.meta 不要）。
