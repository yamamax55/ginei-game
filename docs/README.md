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
| [`planetary-battle-design.md`](./planetary-battle-design.md) | **惑星の戦い**（回廊突破→惑星領域→侵略値で占領・軌道防衛=アルテミスの首飾り・航行不能領域） | EPIC #131・#132〜#135 | PR #136 |
| [`fleet-organization-design.md`](./fleet-organization-design.md) | **艦隊編制基盤**（艦隊番号=第13艦隊・提督配属・永久欠番／軍団→軍集団梯団・直轄部隊集中投資・任務戦術） | EPIC #148・#146/#147／政府 #141 | PR 新規 |
| [`corporate-workflow-strategy-ui-design.md`](./corporate-workflow-strategy-ui-design.md) | **社内ワークフロー風 戦略UI**（稟議＝起案→合議→決裁→執行→通知。UITK第一・埋め込みブラウザは最後の手段。政体で稟議が変わる） | EPIC #14・#142/#144/#145/#158/#116 | 設計メモ |
| [`roadmap.md`](./roadmap.md) | **ロードマップ**（現Issueベースの実装計画・依存・推奨スプリント） | — | PR #105 |

> 「PR #NN」は本目次作成時点で未マージのもの。各 PR がマージされるとリンクが解決する。

## 参考EPIC（世界観のイシュー化・連続パイプライン）
- [`reference-epic-pipeline.md`](./reference-epic-pipeline.md) — **標準手順**（調査→欠落軸分析→設計書→EPIC＋子issue起票→roadmap追記→コミットの7ステップ）。スキル `/worldview-epic` で実行。
- [`reference-epic-backlog.md`](./reference-epic-backlog.md) — 候補キュー（思いついたら1行追記、`/worldview-epic 次` が上から処理）。
- 出荷実例：[`almagest-reference-design.md`](./almagest-reference-design.md)（EPIC #1054・ALM-1〜16）／[`spice-and-wolf-reference-design.md`](./spice-and-wolf-reference-design.md)（EPIC #1071・SAW-1〜9）。

## 開発ログ（dev-log/）
- [`2026-06-08-beam-visual-audio.md`](./dev-log/2026-06-08-beam-visual-audio.md) — ビーム演出・音の一元化（`BeamFx`）。
- [`2026-06-10-time-fleet-notification.md`](./dev-log/2026-06-10-time-fleet-notification.md) — **統一時間 TIME-1〜7（#946/#959）／艦隊編成プール（#148/#884）／通知システム（#964）** を配線。詳細は `CLAUDE.md` の「時間・暦・通知システム」「艦隊編成プール」節。

## 推奨の読む順
1. **世界観**：`worldbuilding-bible.md`（勢力・命名・年表の前提）
2. **戦略レイヤー（上位→コア→後半）**：`phase-c-strategy.md` → `phase-c-core-design.md` → `phase-c-late-design.md`
3. **兵站**：`wartime-logistics-design.md`（C-6補給の拡張・ZOC）
4. **勢力の深掘り**：`communist-faction-design.md`
5. **政治**：`politics-prototype-design.md`
6. **創発キャンペーン（統合・併存）**：`emergent-campaign-design.md`（戦略＋内政＋政体創発を1枚に。内政は EPIC #109）
7. **艦隊編制**：`fleet-organization-design.md`（艦隊番号・提督配属・永久欠番／軍団梯団・直轄・任務戦術。政府#141・文民統制#145 と接続）
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
- **政府役職・統治機構（Phase D）**：EPIC #141 ＋ GOV-1〜7（役職#142／文民#143／既存接続#144／文民統制#145／省庁#158／政党・政治家#159／党内政治#165）。役職＝階級(#14)と別軸の権限スコープ。`ICharacter` で軍人/文民を共通化。官僚の役職上限＝政治任用（民主国家）。
- **権力の実相（Phase D）**：EPIC #167（形式権力 #141 の裏面）＝ 実権/黒幕#164（ラング型・政体を越える生存）／ 国家保安・秘密警察#166（監視・抑圧。独裁度#117/#145 で国民管理が強まる＝シュタージ型）。
- **群像：人物ライフサイクル（Phase D）**：EPIC #150 ＝ 年齢#151／死亡と継承#152／生成・人口動態#153（人口ボーナス/オーナス）／捕虜・解放・処断#154／人材供給3経路（士官教育#155・文民登用#156・テクノクラート#157）。人物は `ICharacter` #142、加齢はキャンペーン暦#117 のみ。
- **政体の質：封建・憲法（Phase D）**：封建制・貴族制#168（爵位・封土・家系図と相続#152・貴族の軍人兼任）／門地開放#169（封建でも平民を士官に＝実力主義改革）／憲法#170（権力分立・法の支配・立憲君主制）。政体#117/#145 で切替。
- **財政・経済（Phase D）**：EPIC #163 ＝ プライマリーバランス財政・国債・為替#161（フェザーン#160=金融ハブ）／税・社会保障#162（再分配・人口オーナス#153 連動）。タイクン化回避＝少数の決断と創発的帰結。
- **艦隊編制（Phase D）**：EPIC #148 ＋ 艦隊番号#146（提督配属・永久欠番）／軍団#147（梯団・直轄・任務戦術）。**ランタイム在庫 `FleetRegistry` とは別の `OrderOfBattle` 台帳**に乗る。任免は #141/#145 に従う。設計書 `fleet-organization-design.md`。
- **都市国家（Phase D）**：#160 ＝ 勢力とは別の中立小勢力（フェザーン型）。銀河#34/#118 に配置・交易#93-95・宗主化・征服#131。`FactionRelations` で中立既定。
- > 政治/群像/経済クラスタの共有窓口＝人物 `ICharacter`#142・係数#106・イベント#116・SaveData#108・政体差#117/#145。いずれも**シナリオ会戦には無影響（後方互換）**。

## メモ
- 各設計書の **【要・作者判断】** は作者の確定待ち（数値・創作裁定）。
- 大物（Phase C / 兵站 / 政治）は **Git ブランチ＋段階実装**、各段でコンパイル＆既存単体会戦の無事を確認してから次へ（CLAUDE.md の運用方針）。
