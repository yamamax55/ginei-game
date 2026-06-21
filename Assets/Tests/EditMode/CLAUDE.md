# EditMode テスト層メモリ（`Assets/Tests/EditMode` で作業する時に自動ロード）

> ルート `CLAUDE.md` が最上位。Core 純ロジックの担保はここ＋`TestHarness`（Unity無し `dotnet test`）。

## 流儀
- 1モジュール1テストファイル（`XxxTests.cs`）。`namespace Ginei.Tests`・`nunit.framework`。
- **境界・クランプ・全分岐・決定論（roll注入）・null安全**を網羅。
- **既定パラメータ（`XxxParams.Default`）の具体値で期待値を固定**（マジックな許容でなく厳密値＋`1e-4f` 程度の浮動小数許容）。
- 既存テストのスタイルに一致させる（近い系統のテストを2-3本参照）。

## 検証
`cd TestHarness && dotnet test -v q` で全 green。落ちたら直してからコミット。dotnet が無ければ `apt-get install -y dotnet-sdk-8.0`。

## TestHarness 同期（重要）
新規 MonoBehaviour を参照するテストは TestHarness の csproj Exclude に注意。`Formation`/`ShipClass` 等の enum を変えたら `TestHarness/Stubs/GineiShims.cs` も同期する（不一致はスタブ環境のみで落ちる）。Game層 MonoBehaviour 挙動はテスト対象外（Play で目視／本層は純ロジックのみ）。
