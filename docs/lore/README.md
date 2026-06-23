---
type: index
tags: [index]
---

# 設定資料集（Lore Vault）

このフォルダは **Obsidian の Vault** として開く設定・物語のバイブル。
中身は普通の Markdown なので、リポジトリと一緒に git で管理する。

> ⚠️ **数値の正（canon）はゲームデータ側が持つ**。提督の能力・会戦定義は
> `Assets/Data/Admirals/*.asset`（`AdmiralData`）と `Resources` の `ScenarioData` が唯一の出所。
> ここには「人物像・背景・物語上の役割・関係性」を書き、数値は二重管理しない
> （参考として転記する場合は "ゲームデータ参照" と明記して、ズレたらアセット側を正とする）。

## フォルダ構成
- `星骸の諸侯.md` … 多勢力設定の**総覧（ハブ／MOC）**。まずここから辿る
- `物語/` … 本編のストーリー（章ごとに分割・前後リンクで読み進める）
- `設定/` … 世界設定の用語ノート（回廊・灯台・環暦・封土・旗幟・諸侯・職分・提督・官僚・商人・陣形・信仰／文化＝語録・祝祭・軍歌・勲章・艦級・食／知識機関＝名門校・諜報機関・正史と稗史・水先案内人／統治軍制＝政体・階級・要塞・稟議／会戦戦略＝士気・練度・通商破壊・兵站 ほか）。索引 → [[用語集]]
- `人物/` … 提督・主要キャラ（1人1ノート）。**人物の職分（`PersonVocation`）ごとにテンプレートを使い分ける**（下表）。複製して `id` を [[ID一覧]] で採番

| 職分（PersonVocation） | テンプレート | 軸（核となる能力／データ） |
|---|---|---|
| 武官（提督・軍人） | `_テンプレート.md` | 軍才（統率/攻撃/防御/機動）・階級・専用旗艦・得意陣形 |
| 文官（官僚・宰相） | `_官僚テンプレート.md` | 文才（運営/情報）・位階・官職（宰相/総督）・省庁（二官八省） |
| 君主（皇帝・元首） | `_君主テンプレート.md` | 統率・政務・徳（王朝の正統性 `DynastyRules`）・継承・帝王学 |
| 政治家（議員・党人） | `_政治家テンプレート.md` | 人望・弁舌・政党・支持率（`PoliticianRules`）・選挙 |
| 技術者（テクノクラート） | `_技術者テンプレート.md` | 専門才（研究/工学/企画/生産）・学歴・研究（`ResearchRules`/`TechCatalog`） |
| 商人（民間・在野＝その他） | `_商人テンプレート.md` | 商才（運営=経営/情報=商機）・人望（交渉）・財産（`wealth`/`financialTrait`/`NamedAsset`）・事業（`TradingHouse`/`Enterprise`） |
| 聖職者（宗教・別格＝その他） | `_聖職者テンプレート.md` | 統率（説教）・文才（神学/教学）・徳・信仰（`Religion`/`ReligionCatalog`/`TheocracyRules`）→ [[設定/信仰]] |
| 開拓者・探検家（辺境＝その他） | `_開拓者テンプレート.md` | 情報（観測・探査）・機動・体質・回廊開削/植民（`ExplorationRules`/`ColonizationRules`/`FrontierRules`） |
| 傭兵・海賊（不正規＝その他） | `_傭兵テンプレート.md` | 攻撃・機動（奇襲）・統率・金で動く忠誠（`MercenaryBand`/`PiracyRules`/`FreebooterRules`/`CommerceRaidingRules`） |

- `勢力/` … 帝国・同盟・諸侯などの勢力設定
- `組織/` … 勢力の中の集団（**政党**＝PTY／**企業・商会**＝COM／**家門・王朝**＝HOU）。`_政党/_企業/_家門テンプレート.md` を複製
- `回廊/` … 名前付き回廊（COR＝星系を結ぶ航路。死んだ回廊を開く設定の背骨）。`_テンプレート.md` を複製。総説は `設定/回廊.md`
- `通貨/` … 各勢力の通貨（CUR＝`CurrencyState`／為替・物価・信認）。`_テンプレート.md` を複製
- `役職/` … 各勢力の役職体系（官職・指揮系統＝`Office`/`GovernmentRegistry`/`RankSystem`）。`_テンプレート.md` を複製し勢力ごとに `◯◯の役職.md`
- `軍編成/` … 各勢力の軍編成（梯団・order of battle＝`OrderOfBattle`/`MilitaryFormation`/`FleetRoster`/`FleetPool`）。勢力ごとに `◯◯の軍編成.md`
- `内政/` … 各勢力の内政（財政・経済・民心・法秩序＝`CampaignRules`/`FiscalRules`/`GovernanceRules`/`Province`）。勢力ごとに `◯◯の内政.md`
- `外交/` … 各勢力の外交姿勢・対他勢力関係（`DiplomacyState`/`DiplomacyRules`/`DiplomacyAiRules`/`WarGoalRules`）。勢力ごとに `◯◯の外交.md`
- `法令/` … 各勢力の法令（憲法・法律・政令＝`LawCatalog`/`RuleOfLawRules`/`LawTickRules`）。内政の「法と秩序」を条文まで降りる層。勢力ごとに `◯◯の法令.md`
- `星系/` … 星系（SYS＝名峰）。回廊の交点・要衝
- `惑星/` … 惑星（PLA＝日本神話名。攻城・内政の単位＝`Planet`/`Province`/`PlanetSiegeRules`）。`_テンプレート.md` を複製
- `会戦/` … 各会戦のシナリオ・経緯・結果
- `選挙/` … 各勢力の選挙（ELC＝衆参/評議会/党首選＝`ElectionRules`/`ElectoralSystemRules`/`PartyRules`/`PoliticsState`）。`_テンプレート.md` を複製。観測＝O（政治）
- `旗艦/` … 名のある旗艦（1艦1ノート）。`_テンプレート.md` を複製して使う
- `用語集.md` … 用語の索引（各設定ノートへのリンク表）
- `年表.md` … 環暦／宇宙暦／帝国暦の出来事を時系列で
- `ID一覧.md` … エンティティ識別子（PER/FAC/BAT/SYS/SHP）の採番台帳。**新規ノートはここで採番**

## 識別子（ID）
- 人物・勢力・会戦・星系・旗艦の各ノートは frontmatter に一意の **`id`**（`PER-001` 等）を持つ。
- 形式・採番・既発番は **[[ID一覧]]** で一元管理（重複防止）。新規ノートはまずそこで次番を採る。
- ゲームデータの数値ID（`StarSystem.id`/`Person.id` 等）とは別の lore 内安定識別子（突き合わせは名前/IDで・二重管理しない）。

## 書き方の基本
- **相互リンク**：本文で `[[` と打つと候補が出る。例 `[[ミレイア・セルウィン]]`、`[[黎明評議会]]`
- **グラフ表示**：左の Graph view アイコンで人物相関を俯瞰
- **タグ**：`#帝国` `#提督` のように付けると後で絞り込める
- **テンプレート**：各フォルダの `_テンプレート.md` をコピーして新規ノートに

## 画像（立ち絵）の表示
> Obsidian の埋め込み `![[画像名]]` は **vault（この `docs/lore/`）の中の画像しか解決できない**。
> ゲーム素材の立ち絵は vault 外（`Assets/Art/Portraits/`）にあるため、**vault 内 `_assets/portraits/` に同名で複製**して表示する。

- **置き場所**：`docs/lore/_assets/portraits/`（vault 内の添付フォルダ）。ファイル名はゲーム素材と**同名**にする。
- **埋め込み**：ノート本文に `![[portrait_female001.png|240]]` と書く（`|240` は表示幅px）。Obsidian は vault 内をファイル名で探すのでサブフォルダでも解決する。
- **正（canon）はゲーム素材**：`Assets/Art/Portraits/◯◯.png` が正。vault のコピーは**表示専用**。素材を差し替えたら同名で `_assets/portraits/` も更新する（人物ノートの `portrait:` にはゲーム素材パスを記す）。
- **新規貼り付け先の固定（任意）**：設定 → ファイルとリンク → 添付ファイルの保存先を「指定フォルダ `_assets`」にすると、貼り付け画像がそこへ集まる。
- ※`_assets/` 配下の画像は Unity の `Assets/` 外なので `.meta` は不要（Obsidian 専用）。

## おすすめプラグイン（任意・設定→コミュニティプラグイン）
- **Dataview** … 各ノート先頭の `---` メタ情報から「帝国の提督一覧」等を自動表生成
- **Templater** … テンプレ流し込みの自動化

## ゲームデータとの対応
| Lore（ここ） | ゲームデータ（正） |
|---|---|
| `人物/◯◯.md` | `Assets/Data/Admirals/◯◯.asset`（`AdmiralData`） |
| `会戦/◯◯.md` | `Resources` 配下の `ScenarioData` |
| `勢力/◯◯.md` | `Resources/Factions/◯◯`（`FactionData`） |
| `星系/◯◯.md` | `StarSystem`（`GalaxyMap`・手続き生成） |
| `旗艦/◯◯.md` | `signatureShipName`（`AdmiralData`）/ `ShipNameRegistry` |
