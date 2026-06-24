# 開発ログ 2026-06-21 ── 会戦ドクトリン改修（Stage 1〜4 完走）／執務机の階級ピラミッドUI／簡略版BOMの正式採用と本格チェーン撤去／内政・外交の純ロジック大量増分

64 コミット・311 ファイル（+15,556 / −1,281）。終日かけて、(A) 会戦の戦術ドクトリン改修を 4 段で走り切り、(B) 執務机の「階級ピラミッド」をテキストから滑らかなグラフィック表示へ作り込み、(C) 経済モデルの方針を「簡略版 BOM 正式採用」に確定して重厚な本格生産チェーン／SAP-SCM の残党を撤去、(D) 早朝には内政・外交の Core 純ロジックを 22 種まとめて積み増した。CI（TestHarness）は最終 8,766 テスト合格。

## 1. 会戦ドクトリン改修 ── Stage 1〜4 を一気通貫
> 「お互い棒立ち or 団子で重なる」会戦を、隊列を組み・遠距離で撃ち合い・正面でぶつかったら突撃で崩し・決戦で押し切る、という流れに。すべて Core 純ロジック（test-first）へ幾何/数式を委譲し Game 側に二重実装しない方針。

- **Stage 1：隊列整流**（`FleetSpacingRules`）── 味方どうしの重なり回避（`SeparationPush`）＋前方の味方を追い越さない前進クランプ（`LimitOvertake`、横成分は保持）。`FleetMovement` が毎フレーム適用、`FleetAI` の接近にも追い越し制限。実機（アムリッツァ）で軍団長-隷下が距離 0.7 で重なっていたのが ~12 の間隔保持に。
- **Stage 2：突撃機構**（`ChargeRules`/`ConfusionRules`）── 基本を遠距離砲撃（`WeaponArc.preferredBand` を中→遠）にし、正面同士（`IsHeadOn`）＋間合い（`InChargeRange`）で指揮官が突撃を決めると突撃側がバフ・被突撃側は一時混乱。混乱は統率で軽減（統率 100 で効果半減）＝高統率の提督は崩れにくく早く立て直す。
- **Stage 3：会戦フロー**（`CorpsBattleFlowRules`/`ManeuverEnvelopmentRules`/`DecisiveBattleWindowRules`）── ①前線打ち合い→②後衛が側面回り込み・敵は遮蔽でカウンター→③決戦窓が開けば軍団長が決戦投入（`corpsHold` 解除＋全軍前進＋突撃）。
- **Stage 4 ＋ 体験バッチ**（983d05b）── 戦術決裁デスク（`BattleEventManager`）をドラッグ移動可＋ミニマップを避けて上配置、選択肢 0-1 の単なる通知はデスクに載せず `NotificationCenter`（左下フィード）へ分離、接敵カットイン平滑化、既定ペース約 0.7 倍（`maxSpeed5→3.5`/`accel2→1.4`/`rotation60→42`/`fireInterval1.2→1.7`、timeScale 系は不変）、ダメージポップアップ間引き（`DamageAccumulator` が同一標的の被ダメを ~0.35 秒で合算）。

### 会戦の指揮・退却まわり（cd8d37b / cfe9d12 ほか）
- **陣形変更コスト**（`FormationChangeCostRules`）── 陣形変更に指揮スキルポイントを消費（戦闘中は重い・多用抑制）。`Squadron.TryChangeFormation` を唯一の実体にし、プレイヤー（`FleetCommander`）も AI（`FleetAI`）もこの窓口経由＝AI も多用しない。
- **軍団指揮**── 軍団長が陣形を主導し隷下は持ち場を離れない（`corpsControlled`＋リーシュ `corpsLeashRange`）。会戦開始から軍団ごとに自動集結（`CorpsFormationRules` のスロット割当）、配下艦と重ならない間隔（`Squadron.FootprintRadius`）、陣形発令の変化時のみ通知（洪水回避）。プレイヤー軍団も開始時に自動集結し、命令で上書き可能。
- **追撃撃沈**── 退却中（捨てがまり等）でも追撃下なら残存を残して標的に留まり被弾・撃沈されうる。

### 会戦 UI の窓内帰属（WIN-4・d2ed7c7）
- additive Battle シーンが各自フルスクリーン Canvas を描いて複数窓で重なっていた会戦 UI（HUD/コマンド/ミニマップ）を、対応する `BattleWindow` の矩形内へ親替え＋`RectMask2D` クリップで帰属。新規 `BattleWindowUI`（static レジストリ）で配線。複数会戦同時表示でも取り違えないようシーンスコープ化。※ worktree のため Game 層の最終コンパイル＋2 会戦同時の Play 目視は取り込み後に要確認。

## 2. 執務机の「階級ピラミッド」をグラフィック化
> テキスト箇条書き → JSDF 階級体系図風の視覚ピラミッドへ。runtime ベクター（uGUI Graphic のメッシュ生成）なので人数・現在地ハイライト・クリックを動的に保ったまま形状を作れる。

- **グラフィカル刷新**（3a5a55e）── 頂点（元帥・細い）→底辺（兵卒・太い）へ色付き帯を積む。区分色（将官＝赤/青・士官＝青・准士官＝金・下士官＝鋼・兵卒＝紺）、各帯に階級名＋人数、現在地は金色＋「◀ 現在地」。
- **実在提督の反映**（d30fa61 / 556b6e8）── 帯の人数を名鑑（`AllAdmirals`）の実在提督に一致させ、帯クリックでその階級の人物一覧。
- **滑らかな三角形**（226cf46）── 階段状の矩形帯から `PyramidBand`（`MaskableGraphic`、`OnPopulateMesh` で台形描画）へ。隣り合う帯の辺幅を一致させ全体が連続した三角形シルエットに。
- **階級章スプライト**（f5206b7）── プレースホルダの Unicode 記号からコード生成スプライト（帝国軍・同盟軍）へ差し替え。※ アイコンは Gemini でなく PIL/SVG コード生成の方針に沿う。
- **不具合修正**── 帯が表示されない（e1b24fa）／`PyramidBand` の `CanvasRenderer` 欠如で `MissingComponentException`（acdc957）／階級章がはみ出す被り（898887e）／細い帯では階級章を画像でなくインライン記号にして潰れ防止（4a0fc36）。
- あわせて執務机を上メニューのコマンドバー専用ボタンへ格上げ（e3968b3）、岐路ボタン行が縦に巨大化する不具合修正（6875cf4）。

## 3. 経済モデル ── 簡略版 BOM の正式採用と本格チェーン／SAP-SCM 撤去
> 「ゲーム開発簡略化＝終盤ラグ／タイクン化回避」の判断。生産・調達・物流の新規は簡略版 BOM（`CommodityCatalog`/`RecipeBook`/`BomProductionRules`）へ寄せ、重厚な SAP/MRP/Ariba 型は `theme:凍結`。

- **方針確定**（9a4aa04・docs）── 簡略版 BOM を正式採用、SAP/MRP/Ariba サプライチェーン（#982/#1002 ほか）を当面凍結と明記。
- **本格生産チェーン削除**（50b8baa）── VCHAIN #2091 の森林→木材→建材→住宅チェーン 7 型（`SupplyChainGood`/`ChainStock`/`ForestryRules`/`SawmillRules`/`ConstructionChainRules`/`HousingDemandRules`/`SupplyChainTickRules`）を実装削除。住宅は簡略版 BOM の消費財品目（住宅←建材の 1 段）として `ConsumerDemandRules` で需給を扱い `RunBomConsumerTick` に統合。
- **SCM 残党整理**（d523296）── #2105 SCM 計画（MRP 所要量展開）の `RunScmPlanTick`／`ScmTickRules`/`MrpCoverageRules` を撤去（read-only 通知のみの形骸層）、#2112 域内供給配分は回廊トポロジ（`RegionReachabilityRules`）を廃し勢力単位の不足平滑化へ簡略化（通商破壊 #95 の封鎖孤立は維持）。#1109 化学 SCM は他ドメイン参照で孤立しておらずカスケードリスクのため凍結のみ。

## 4. 内政・外交の Core 純ロジック増分（DIPLO #2119 ほか・22 種）
> 自走シムへ内政・外交モデルを積み増す Core 増分群。いずれも自己完結 float/struct・既存窓口へ委譲し並行系を作らない・additive/基準非破壊/決定論・EditMode テスト併記。Game 層の暦境界 Tick 配線は Play 検証が要るため別段。

- **外交**：`WarWearinessModifiersRules`（厭戦修飾）／`CasusBelliScoringRules`（開戦理由スコア＋大義選択）／`AllianceChainRules`（同盟連鎖）／`EmbargoEffectRules`（通商制裁の打撃＋反作用）／`AllianceCohesionRules`（同盟結束度）／`BorderTensionRules`（国境緊張）／`IntelOperationRules`（諜報作戦）／`RefugeeFlowRules`（難民フロー）／`TradeDependencyRules`（貿易依存てこ）＋安全保障供与・代理戦争。
- **内政**：`TaxBracketRules`（累進課税）／`InfrastructureState`＋`InfrastructureTickRules`（社会基盤）／`WageIndexTickRules`（賃金指数）／`TradeNetworkRules`（交易収入）／`SubsidyRules`（産業補助）／`UnemploymentRules`（失業）／`PriceLevelRules`（物価）／`CorruptionRules`（汚職）／`WelfareProvisionRules`（福祉の逓減）／`MonetaryPolicyTradeoffRules`（金融政策トレードオフ）／`PublicHealthRules`（公衆衛生）／`ProvincialAutonomyRules`（中央集権↔自治）／`ShadowEconomyRules`（闇経済）＋農業生産・電力網。
- **配線**：銃後の厭戦↔講和の内政⇄外交フィードバックを `GalaxyView` 年次 Tick へ配線（6cc5a28）。

## 5. デモ・ドキュメント
- **テスト用 Battle デモ刷新**（fcbbbe2）── `SampleScenarioCreator` を現状機能で刷新、戦略デモメニューに Title/Result シーン登録（e4dd5d2）。
- **世界観ストーリー資料**（4bab0e6）── NotebookLM 読み込み用のソース文書として多勢力プレイのオリジナル設定『星骸の諸侯』（約 5,000 字の本編＋設定資料・勢力表・対立軸・用語集）を追加。銀英伝の二項対立／固有名詞を避け多極の諸侯制として再構築（著作権配慮）。既存実装（Corridor/FactionData/旗幟/多勢力対応）と整合。
- **開発日記の運用移設**（8183580）── 作業ログ（タスクでない）を Issue から `docs/dev-log/` の日付付きエントリへ移設。以後の作業ログは Issue 化せず dev-log に置く方針（roadmap §7）。
- `CoreStateInspector` の用語集を現状の Core state に追従（71883ed）。

## 検証
- Core 純ロジックは EditMode／TestHarness で担保（最終 8,766 テスト合格）。会戦各 Stage は実機 Play でも確認（突撃混乱の倍率・交戦間合い・軍団集結間隔）。
- 階級ピラミッド UI は Unity の ValidateScript ＋強制 Refresh でコンパイル 0 エラーを確認（見た目は Play 目視）。
- WIN-4 の会戦 UI 窓内帰属は worktree 制約で Game 層の最終コンパイル＋複数会戦 Play 目視がブランチ取り込み後に残課題。

## ドキュメント更新
- CLAUDE.md：簡略版 BOM の正式採用と SAP/MRP/Ariba 凍結、本格チェーン削除を反映。
- `docs/core-modules-catalog.md`：内政・外交の純ロジック増分 22 種を追記。
