# 並列ファンアウト標準手順（CCX-1 #1044）

> 独立した純ロジック Core を **Workflow / 複数Agent で同時生成**し、**TestHarness(dotnet test) で一括検証**する標準。
> 関連：EPIC #1043（20xプラン超活用）／メモリ `parallel-task-execution`・`prefer-claude-flow`。
> 原則：**Core生成は並列・Game配線は直列**。速度を「生産」でなく「検証」に使う。

## 1. いつ使うか（適用条件）
**並列ファンアウトに乗せてよいのは、互いに依存しない純ロジックだけ**：
- `Assets/Scripts/Core/` の `*Rules`（static 純ロジック）／SO 型／enum。
- 他モジュールの新規シンボルに依存しない（＝同時に作っても互いを参照しない）。
- MonoBehaviour/シーン/シングルトン非依存（`UnityEngine` 参照は可）。

**乗せてはいけない**（＝直列でやる）：
- `Assets/Scripts/Game/`（MonoBehaviour・UI・シーン配線）。Unityコンパイルの落とし穴（1ファイルでもエラーだと旧アセンブリを黙って保持）があり、**編集後は必ず Unity コンソール確認**が要る＝直列。→ CCX-7 #1050。
- 互いに新規シンボルを参照し合う Core 群（A が B の新規型を使う等）。依存順に直列、または依存の浅い順で段階ファンアウト。

> 置き場所が所属を決める：純ロジック→`Core/`／SOアクセス・IO→`Data/`／MonoBehaviour・UI→`Game/`。Core から Game 型（`GameSettings`/`AudioManager`/`FleetRegistry` 等）を参照しない（依存は Core←Data←Game の一方向）。

## 2. 手順

### Step 0 — 分解（直列・人間/親エージェント）
作る Core を**独立な単位に割る**。各単位は「1ファイル1クラス＋対のEditModeテスト1ファイル」。単位間に新規シンボル依存が無いことを確認する。依存があるものは束ねるか後段へ。

### Step 1 — ファンアウト（並列・1単位=1エージェント）
複数Agent（または Workflow）を**同時起動**。各エージェントへの指示に必ず含める**ガードレール**（本デモで有効性を確認済み）：
- 担当は**1モジュールだけ**。**指定ファイル以外は絶対に編集しない**（他エージェントと衝突するため）。
- **test-first**：`Assets/Tests/EditMode/<Module>Tests.cs` を `using NUnit.Framework; using Ginei;` ＋ `namespace Ginei.Tests` ＋ public class ＋ `[Test]` で作る（既存テストの様式に倣う）。
- **決定論的**（乱数APIは seed/roll を固定値で渡す）。
- **`dotnet test` をエージェント内で実行しない**（並列ビルドが bin/obj を奪い合う＝親が最後に1回回す）。
- **`.meta` は作らない**（Unity が次回起動時に自動生成。TestHarness は不要）。
- 既存と**重複しない**こと。Core ソースの挙動が変でも**直さずテストに TODO** を残す（修正は直列レビューで）。

### Step 2 — 検証（直列・親が1回）
全エージェント完了後、**親が一度だけ** TestHarness を回す：
```bash
cd TestHarness && dotnet test -v q
```
- csproj は `../Assets/Scripts/Core/**/*.cs` と `../Assets/Tests/EditMode/*.cs` を **glob 自動包含**＝置いたファイルは設定変更なしで拾われる。
- 緑なら採用。赤なら**その単位だけ**を直列で修正（他は独立なので巻き込まれない＝ファンアウトの利点）。

### Step 3 — Unity 側の取り込み（任意・後段）
Unity エディタを開くと `.meta` が自動生成され、Test Runner にも現れる。Game 配線が絡む場合のみ CCX-7 の直列手順へ。

## 3. Workflow スクリプト雛形（再利用テンプレ）
複数Agent の代わりに Workflow で回す場合の最小形（pipeline で各単位を independent に流す）：
```js
export const meta = {
  name: 'core-fanout',
  description: '独立Coreを並列生成しTestHarnessで検証',
  phases: [{ title: 'Generate' }],
}
const UNITS = [
  { module: 'FooRules', domain: '…', existing: 'Assets/Tests/EditMode/BarTests.cs' },
  // …独立単位を並べる
]
await parallel(UNITS.map(u => () => agent(
  `Assets/Scripts/Core/${u.module}.cs に対し EditMode テストを ` +
  `Assets/Tests/EditMode/${u.module}Tests.cs に新規作成。` +
  `規約: using NUnit.Framework; using Ginei; namespace Ginei.Tests; [Test]; 決定論的。` +
  `担当ファイル以外を編集しない／dotnet testしない／.meta作らない／既存(${u.existing})と重複しない。`,
  { label: `fanout:${u.module}`, phase: 'Generate' }
)))
// 検証は親が直列で: cd TestHarness && dotnet test -v q
```

## 4. 実証（このデモの結果）
3モジュール（`CommerceRaidingRules` / `DisclosureRules` / `SupplyRules`）へ**3エージェントを並列起動**し、各々が未アサートの境界・null安全・エッジケースの新規テストファイルを生成。衝突ゼロ・Core 無改変。

| | テスト数 |
|---|---|
| ファンアウト前 | 1020 |
| **ファンアウト後** | **1037**（+17・全緑） |

並列生成 → glob 自動包含 → `dotnet test` 一括緑、が成立。**「Core生成は並列・検証は直列1回」**が標準として機能することを確認した。
