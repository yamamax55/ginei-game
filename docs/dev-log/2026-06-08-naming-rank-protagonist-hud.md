# 作業ログ 2026-06-08 — 命名/階級/主人公・HUD刷新・選択改善・詳細パネル・テスト基盤

> 旧 GitHub Issue #746（priority:low / area:combat / design）から dev-log へ移設（2026-06-21）。タスクではなく作業ログ。
> （同日 06-08 の大規模実装ログは `2026-06-08-strategy-layer-planet-siege.md`、ビーム/音は `2026-06-08-beam-visual-audio.md` を参照）

ブランチ `claude/beam-display-issue-fNvtQ` で本日（2026-06-08）実装した内容の整理。**テスト基盤を新設**し、戦術画面まわり（命名/階級/主人公/HUD/選択/詳細）を一通り実装。**EditMode テスト計68件 緑**、各表示系は実機確認済み。

## コミット一覧（古い順）
| commit | 内容 |
|---|---|
| `73ccf0b` | test: EditMode テスト基盤（asmdef 3点＋既存純ロジックの特性テスト） |
| `5b2f9e4` | feat(#523): 命名システムの姓名データ＋合成API（NAME-1〜4・test-first） |
| `11cd73e` | feat(#523): 命名の表示配線（NAME-5：頭上=異名／HUD=正式名／勝因・MVP=正式名） |
| `5a60d16` | feat(#523): RosterCreator に構造化姓名と実例3種（NAME-6） |
| `32a01a8` | chore: Unity 生成の .meta を追加（ユーザー） |
| `e457826` | feat(GON-6 #735): 主人公アンカーのフラグと AI 非制御 |
| `0065633` | feat(#14): 階級のHUD表示 |
| `c4c5d6f` | fix(#745): HUD重なり緩和（暫定：PAUSE/SPEED 上端中央・陣営行追従） |
| `298914b` | refactor(#745): HUDをコード生成 VerticalLayoutGroup へ刷新＋階級フォールバック(#14) |
| `4c771cd` | feat(#744): 側背面の表現を文字連呼から色＋大きさへ（引き算） |
| `2d49d2a` | feat: 艦艇選択を選びやすく（クリック近傍の最寄り自艦隊を選択） |
| `f153def` | feat: 艦隊を選択したらコマンドメニューを自動で開く |
| `bff79d0` | feat: 艦隊詳細パネル（情報）を追加・表示中はポーズ |

## 機能別ステータス
- ✅ **テスト基盤**：`Ginei.Runtime`/`Ginei.Editor`/`Ginei.Tests.EditMode` asmdef ＋ RankSystem/FactionData/FactionRelations/AdmiralData/命名/主人公/階級/側背面の特性テスト（**計68**）。
- ✅ **#523 提督命名システム**：`AdmiralData` 任意姓名フィールド＋`FullName`/`ShortName`/`EpithetName`/`RegnalSuffix`、表示配線、ロスター実例。後方互換（未設定は `admiralName`）。
- 🟡 **GON-6（#735）主人公アンカー**：第一スライス完了（`isProtagonist`＋`ProtagonistRules`＋AI非制御＋HUD★）。**フル（HXH-2 光源ロール／GON-1純粋さ・GON-3直感型の既定付与／専用演出）は未着手**。
- ✅ **#14 階級HUD表示**：`AdmiralData.rankTier`＋`RankSystem.ResolveRankName`/`ResolveRankNameOrDefault`。FactionData 未割当でも既定ラダー（准将5〜元帥10）で表示。
- ✅ **#745 HUD刷新**：`FleetHUDManager` をコード生成 `VerticalLayoutGroup`＋`ContentSizeFitter` 化。行数増（★/階級/異名/参謀）でも重ならない。PAUSE/SPEED を上端中央へ分離。→ **クローズ予定**。
- ✅ **#744 側背面表現**：`DamagePopup` から「側背面!」文字を撤去、色（濃赤橙）＋大きさで区別（純関数 `GetStyle`＋テスト固定）。→ **クローズ予定**。
- ✅ **艦艇選択改善**：`FleetCommander` にクリック近傍トレランス（`clickSelectPixelTolerance`=36px）。小さな艦でも選べる。
- ✅ **選択でメニュー自動表示**：`autoOpenMenuOnSelect`（既定 true）。
- ✅ **艦隊詳細パネル**：`FleetDetailPanel`（HUDとは別のモーダル）。提督正式名/階級/異名/呼称/陣営/主人公・実効能力6種/参謀・兵力/士気/配下艦/ミサイル・現在/得意陣形。**表示中は Time.timeScale=0 でポーズ**、閉じる/Esc/背景クリックで復帰。

## 検証
- EditMode テスト **68 緑**（Unity Test Runner で確認）。
- 実機確認済み：命名/階級/★主人公の HUD 表示、HUD刷新（重なり解消）、側背面の連呼解消、選択の選びやすさ、メニュー自動表示、詳細パネル＋ポーズ。
- 実行環境（リモート）は Unity 不在のため、表示・入力系は手元 Unity での目視確認に依存。

## 残課題 / 次の候補
- [ ] #744 / #745 のクローズ（GitHub API レート制限が解け次第）。
- [ ] GON-2 誓約暴走の最小形（実効値の一時ブースト→恒久弱体・純ロジック test-first）。
- [ ] GON-6 フル（光源ロール／性格束／演出）。
- [ ] 詳細パネルの微調整（サイズ・項目・必要なら ScrollRect 化）。
- [ ] 側背面の任意改善（フォント非依存の小さな形マーカー・さらなる間引き）。
- [ ] CLAUDE.md「既存コンポーネント」表へ `ProtagonistRules`/`FleetDetailPanel` を1行追記。

## 関連
#523・#14・GON-6 #735・#744・#745
