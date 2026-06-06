# docs/ — 設計ドキュメント目次

> 本作（銀英伝“風”オリジナルの戦術艦隊戦→戦略/政治レイヤー拡張）の設計書を一覧する。
> 実装の最上位ルールはリポジトリ直下の **`CLAUDE.md`**（プロジェクトメモリ）。本 `docs/` は各機能の**設計（なぜ・何を・どう既存に接続するか）**を補う。
> 各設計書は実装済みの土台（`FactionData`/`RankSystem`/`FleetRegistry`/`BattleSetup`/`BlackHole` 等）への接続まで具体化してある。

## 設計書一覧

| ドキュメント | 概要 | 対応Issue | 状態 |
|---|---|---|---|
| [`worldbuilding-bible.md`](./worldbuilding-bible.md) | 世界観バイブル（数百年後設定／4勢力／命名／年表／未発見宙域／階級ラダー） | #15 | PR #89 |
| [`phase-c-strategy.md`](./phase-c-strategy.md) | Phase C 戦略レイヤー **上位方針**（シームレス戦略↔戦術・銀河グラフ・回廊戦闘） | EPIC #33 | master 済 |
| [`phase-c-core-design.md`](./phase-c-core-design.md) | Phase C **コア実装設計**（C-1 グラフ＋時間制ワープ／C-2 ズーム切替／C-3 有界回廊戦闘） | #34 #35 #36 | PR #97 |
| [`phase-c-late-design.md`](./phase-c-late-design.md) | Phase C **後半設計**（C-4 リアルタイム並行・複数戦線／C-7 要塞／C-8 自動解決） | #37〜#41 | PR #103 |
| [`wartime-logistics-design.md`](./wartime-logistics-design.md) | **戦時兵站**（L-1 資源／L-2 補給線／L-3 通商破壊／L-4 人口・徴募）＋**ZOC定義** | EPIC #92・#93〜#96・ZOC #100 | PR #98 |
| [`communist-faction-design.md`](./communist-faction-design.md) | **共産勢力の非対称設計**（物量／政治将校／不退転＋内部対立。エンジン非フォーク） | #17 | PR #99 |
| [`politics-prototype-design.md`](./politics-prototype-design.md) | **政治プロト：階級と提案**（青=可決可能/赤=不相応・戦果→merit→昇進） | EPIC #14 | PR #101 |
| [`emergent-campaign-design.md`](./emergent-campaign-design.md) | **創発キャンペーン**（モード併存・地理/歴史/地政学から政体創発・1惑星/フォグ・チョーク保証） | EPIC #117・#118〜#121／内政 #109 | PR #122 |
| [`roadmap.md`](./roadmap.md) | **ロードマップ**（現Issueベースの実装計画・依存・推奨スプリント） | — | PR #105 |

> 「PR #NN」は本目次作成時点で未マージのもの。各 PR がマージされるとリンクが解決する。

## 推奨の読む順
1. **世界観**：`worldbuilding-bible.md`（勢力・命名・年表の前提）
2. **戦略レイヤー（上位→コア→後半）**：`phase-c-strategy.md` → `phase-c-core-design.md` → `phase-c-late-design.md`
3. **兵站**：`wartime-logistics-design.md`（C-6補給の拡張・ZOC）
4. **勢力の深掘り**：`communist-faction-design.md`
5. **政治**：`politics-prototype-design.md`
6. **創発キャンペーン（統合・併存）**：`emergent-campaign-design.md`（戦略＋内政＋政体創発を1枚に。内政は EPIC #109）
- **計画全体**：`roadmap.md`（依存関係・推奨スプリント）

## 実装の土台（既に master にある主要システム）
> 設計書はこれらに接続する。詳細は `CLAUDE.md` の「既存コンポーネント」表を参照。
- **多勢力**：`FactionData`（色/思想/`ranks`/`nonHostileFactions`/`legacyFaction`）＋敵対判定の唯一の窓口 `FactionRelations.IsHostile`。
- **階級**：`RankSystem`（tier の同列/比較/昇進/欠番丸め）＋ `FactionData.ranks`。
- **在庫/索敵**：`FleetRegistry`（全艦単一リスト）＋ `ShipCombat`（射界・最寄り敵・ダメージ）。
- **会戦起動**：`BattleSetup`（`ScenarioData` から生成）／勝敗 `BattleManager`（多勢力・勝利条件）。
- **地形ハザード**：`BlackHole`（A-5）。
- **提督能力**：`AdmiralData`（6能力＋参謀の実効値）。

## Issue 体系の対応（マイルストーン）
- **Phase C（戦略レイヤー）**：EPIC #33 ＋ C-1〜C-8（#34〜#41）。ビルド順 C-1→C-2→C-3→C-7→C-4→C-5/C-6/C-8。
- **戦時兵站（Phase C/D）**：EPIC #92 ＋ L-1〜L-3（#93〜#95, Phase C）／L-4（#96, Phase D）＋ ZOC #100。着手順 L-1→ZOC→L-2→L-3→L-4。
- **世界観/勢力**：#15（バイブル）／#16（4勢力定義・済PR）／#17（共産非対称）。
- **政治**：EPIC #14（階級と提案）。RankSystem の上に乗る。

## メモ
- 各設計書の **【要・作者判断】** は作者の確定待ち（数値・創作裁定）。
- 大物（Phase C / 兵站 / 政治）は **Git ブランチ＋段階実装**、各段でコンパイル＆既存単体会戦の無事を確認してから次へ（CLAUDE.md の運用方針）。
