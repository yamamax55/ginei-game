# 2026-06-21 開発効率化基盤＋手触り/音声/提督ポートレート（ローカル生成）

PR [#2638](https://github.com/yamamax55/ginei-game/pull/2638)（master マージ済み・merge `0f4d559b`）。TestHarness 全8801緑。

## 1. 開発効率化インフラ（恒常）
「配線・修正・検証」を構造的に速くする常設ツール群を導入。詳細は各ファイル＋ memory `dev-efficiency-infra`。
- **`/wire-audit`**（`.claude/commands/`）＝serena 参照グラフで `*Rules` を {Game参照あり/Core島/真の孤児} に分類し「届くべき未配線」を Tier 付け。`/core-wave`（作る）の対＝届ける。
- **`Tools/serialized-value-check.sh <Class> [field]`**＝直列化トラップ(#2548)検出（`.unity`/`.prefab` の値が script 既定に勝つのを guid 照合で発見・exit1）。
- **自動ゲート `Tools/hooks/`**：PostToolUse=Game .cs 編集時に直列化警告／Stop=このセッションで追加変更した Assets の .meta 欠落警告。配線は `.claude/settings.local.json`（対話のみ・CIは別途TestHarness）。手順=`Tools/hooks/README.md`。
- **カスタムサブエージェント `.claude/agents/`**：`core-wave-worker`／`orphan-classifier`／`wirer`（ファンアウトで規約を貼り直さない）。
- **階層 CLAUDE.md**：`Assets/Scripts/Core|Game`・`Assets/Tests/EditMode` に nested（サブツリー作業時のみ自動ロード）＋ルートに serena-first 動線を明文化。
- **検証の二層化**：`BalanceRegressionTests`（ランチェスター二乗則・陣形三すくみ＝均衡を不変量化）／`GameSmokeTests`（PlayMode 起動スモーク＝GameCI）。dotnet では見えない Game 層の穴を埋める。

## 2. ① 手触り（juice・無料・外部依存なし＝DOTween不要）
- **`Easing`(Core・test済)**：SmoothStep/OutCubic/OutBack/OutElastic/OutBounce/Pulse。散在する手書き Lerp/SmoothStep の集約先。
- **`Juice`(Game)＋`JuiceRunner`**：`ScalePunch`/`PopIn`/`Shake`/`Flash`/`Fade`/`HitStop`。timeScale 規約準拠（UI=unscaled・盤面=scaled）。
- **`DamagePopup`** の出現に `Easing.OutBack` でポップ追加（`LabelZoomScaler` が localScale を占有するため baseScale を動かして協調）。
- `com.unity.visualeffectgraph 17.4.0` を manifest 追加（2Dビーム/爆発の選択肢）。

## 3. ② 音楽・音声
- **`AudioManager` 実用化**：Resources 自動ロード規約（`bgm_title/battle/strategy/result`・`se_*`）＝ファイルを置くだけで鳴る／BGM クロスフェード（`Easing.SmoothStep`・実時間）／Strategy・Result の BGM 枠と配線（`GalaxyView`・`ResultManager` の Start）。後方互換。
- **`docs/audio-sourcing.md`**：無料かつ商用可のソース（Pixabay/Kenney/Sonniss=帰属不要、Musopen/IMSLP/FreePD=PDクラシック要ライセンス確認、incompetech=CC-BY帰属必須）＋銀英伝向けPD作品＋順守チェックリスト。**素材はまだ未投入**（置けば鳴る状態）。

## 4. ③ 提督ポートレート（ローカル生成パイプライン）
- **`PortraitPromptRules`(Core・test済)**＝`AdmiralData`→生成プロンプト＋決定論シード。一貫性の核＝固定 `StylePreamble`（**承認画風＝明るいセル塗り・瞳の艶・VN風**）＋人物アーキタイプ→外見の固定写像＋名前ハッシュ(FNV-1a)シード。`AdmiralData` に `portrait`(Sprite)/`appearanceNote`/`portraitSeed` 追加（additive）。エディタ `Ginei/Export Portrait Prompts (CSV)`。
- **ローカル環境（実機検証済み）**：ComfyUI ポータブル版を `D:\ComfyUI_windows_portable`、SDXL ベース（OpenRAIL・商用可）。導入手順=`docs/comfyui-setup.md`。RTX 3050 8GB は **`--lowvram`＋768×1024 が安定**（標準モードは OOM・`CUDA unknown error` は再起動で回復）。
- **バッチ生成 `Tools/portrait/comfyui_batch.py`**：CSV→ComfyUI API(`/prompt`)→PNG。単発 `--test`／CSV一括。
- **背景透過**：`rembg`（`uvx --from "rembg[cpu,cli]" rembg p in out`）。
- **取り込み**：`Assets/Editor/PortraitTextureImporter.cs`（AssetPostprocessor＝`Assets/Art/Portraits/` を自動で透過Sprite化）。
- **成果**：承認画風で **男5・女5＝10体**を生成・透過し `Assets/Art/Portraits/portrait_{male001-005,female001-005}.png` として資産化（female001 は既存の良画像を採用）。`Tools/portrait/sample_cast.csv` に再現用キャスト定義。

## 5. つまずき・学び
- ComfyUI 8GB：標準モードで SDXL は OOM → **`--lowvram` 必須・768×1024**。`CUDA unknown error` は **再起動で回復**（DL中から動かし続けると起きやすい）。
- `rembg` は **`[cpu,cli]` extras** が必要（bare はモデル/CLI を含まない）。
- GitHub プッシュは **SSH(443) banner timeout で稀に失敗→再実行で通る**（memory `github-ssh-over-443`）。バイナリPNG込みで顕著。
- `git pull --rebase` を作業途中でやると CLAUDE.md 衝突でリベース地獄＝**`git rebase --abort` で復帰**。マージは PR 側で（master 分岐の CLAUDE.md/catalog 衝突は「双方の追記を両取り」で解消）。

## 6. 次回の課題
- **★背景透過の品質改善（最優先）**：rembg(u2net) は**抜きすぎ／甘い**箇所がある（特に髪の生え際・細部）。別手段を検討：
  - **rembg のモデル変更＝`isnet-anime`**（アニメ特化・最有力）／`u2netp`／`--alpha-matting`（髪エッジを柔らかく）。
  - **ComfyUI 統合ノード**で生成と同時に抜く：`BRIA RMBG-2.0`／`InspyrenetRembg`／`ComfyUI-RMBG`（u2netより高品位なことが多い）。
  - SAM(Segment Anything) ベース、または**単色クロマキー背景で生成→色キー除去**、最後に手作業補正。
  - 評価：同一10体で u2net vs isnet-anime vs RMBG-2.0 を比較し、髪・肩・装飾の抜けで決める。
- Game層/Editor の C# 変更は **Unity を開いてコンパイル確認**（dotnet 非対象）→ 通れば AssetPostprocessor で透過Sprite化・`AdmiralData.portrait` へ割当可能。
- 音声素材の投入（Kenney=SE／Pixabay・Musopen=BGM）。残り juice 移行（`FleetStrength.Flash` は多レンダラ＝据え置き中）。実提督への一括ポートレート割当。
