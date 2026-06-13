# プレイアビリティ改善バックログ（playtest-findings）

> 「遊べるゲームか」をコード監査で洗い出し→改善→再監査するループの作業台帳。
> `/playtest-improve`（`.claude/commands/playtest-improve.md`）が1サイクルごとにここを読み、
> **[Core]** 項目（TestHarness で検証できる純ロジック）を実装してブランチ→PR→自動マージする。
> **[Game]** 項目（Unity 実行や UI 目視が要る＝この環境で自動検証不可）は実装せず記録だけ残し、人手レビュー用に蓄積する。
>
> 記法：`- [ ] [Core] …` 未着手／`- [x] [Core] …（cycleN）` 完了。
> ゲートは **TestHarness のみ（`cd TestHarness && dotnet test`）**。Game層の自動マージはしない（マージ安全弁の方針）。

## 現状サマリ（2026-06-13 初回監査）
- Core 純ロジック **776 本**、うち Game 層に配線済み **130 本**。残り ~646 本は盤面/UI 未配線＝プレイに効いていない。
- TestHarness ベースライン：**5317 テスト green**。
- 会戦は旗艦＋配下艦・陣形・士気・側背面・AI 撤退まで配線済み。戦略は GalaxyView に内政/造船/暦/通知まで配線済み。
- **最大の伸びしろ＝「シミュ層の厚みが play に効いていない」**：戦術 Core（伏兵/陽動/追撃/艦載機/白兵/機雷/電子戦/偵察/練度…）が未配線で、会戦の駆け引きが平板になりがち。

## [Core] 改善候補（このループが実装する＝test-first・TestHarness gate）
- [ ] [Core] 戦術ドクトリン統合：未配線の戦術 Core（`AmbushRules`/`FeintRules`/`PursuitRules`/`ReconRules`/`VeterancyRules` 等）を会戦 AI が読める単一窓口 `TacticalDoctrineRules`（仮）に束ね、`BattleAiRules`/`ForceQualityRules` と同じ実効値パターンで倍率を返す。まず純ロジック＋テストを足し、配線は Game 側の1箇所からに留める。
- [ ] [Core] 会戦バランス：決着が単調化しないよう Lanchester/士気/側背面の係数を `CombatModifiers` 経由で見直し、極端な雪崩を抑える調整を test-first で（従来式との差分をテストで固定）。
- [ ] [Core] 練度の play 反映：`VeterancyRules`（練度）を戦力比に効かせる実効倍率を `ForceQualityRules` 隣に橋渡し（基準非破壊）。歴戦艦隊が新兵より強い手応えを数値で作る。
- [ ] [Core] 偵察と戦場の霧：`ReconRules` の推定誤差を「敵戦力表示のブレ」に使える純関数 API に整え、AI の過大/過小評価（`OverconfidenceBiasRules`/`AvailabilityBiasRules`）と接続するブリッジ Rule を test-first で。
- [ ] [Core] 撤退・追撃の収支：`PursuitRules`（追撃戦）の損害解決を会戦終了時に使える形に整え、`BattleWithdrawalRules` と責務分担した薄い橋渡しを足す。

## [Game] 改善候補（Unity 実行/目視が要る＝記録のみ・人手対応）
- [ ] [Game] 会戦の操作フィードバック（攻撃/移動/陣形変更の手応え・通知）の充実。要 Unity 目視。
- [ ] [Game] 戦術 Core を実際の会戦挙動へ配線（AI が伏兵/陽動/追撃を「打つ」演出と判断）。`unity-test.yml`（実 Unity）で検証してから人手マージ。
- [ ] [Game] GalaxyView 2531：新任人材の性的指向は別軸で未実装（任意）。
- [ ] [Game] 初見プレイヤー向けの導線（チュートリアル/操作ヘルプの初回提示）。要目視。

## 完了ログ
<!-- - [x] [Core] … （cycleN・YYYY-MM-DD） -->
