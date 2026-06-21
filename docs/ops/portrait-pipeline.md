---
type: ops
tags: [ops]
---

# 提督ポートレート パイプライン（③アート一貫性・無料〜少額課金）

> 多数の提督を**同一画風＋同一人物**で量産するための仕組み。ツール非依存（Gemini/Midjourney/SD いずれでも可）。
> コード側は実装済み＝あとは生成サービスを選び、ワークリストを回すだけ。

## 一貫性の3本柱（`PortraitPromptRules`・Core/test済）
1. **固定の画風アンカー**（`StylePreamble`）＝全肖像に同じ画風文を前置 → **作品全体の絵柄が揃う**。
2. **アーキタイプ→外見の固定写像**＝`AdmiralData` の人物アーキタイプ（`isKaiser`/`isMagician`/`isWarSaint`…）から外見記述を決定。覇王＝金髪碧眼、魔術師＝気だるい黒髪学者風…と**人物像が安定**。
3. **決定論シード**（`DeriveSeed`）＝名前ハッシュ（または `portraitSeed`）で固定 → **再生成しても同じ顔**。

加えて勢力（軍服）・階級（年齢/装飾）・能力（表情）・`appearanceNote`（髪型/性別/特徴の手動ヒント）を合成。

## データ側（実装済み）
- `AdmiralData.portrait`(Sprite)…割当した肖像。`appearanceNote`(string)…外見ヒント。`portraitSeed`(int)…固定シード（0=名前から導出）。
- `PortraitPromptRules.BuildPrompt(admiral)` / `DeriveSeed(admiral)`…生成プロンプトとシードの単一窓口。
- エディタ：**`Ginei/Export Portrait Prompts (CSV)`** で全提督のプロンプト＋シードを `portrait-prompts.csv` に書き出し（肖像未割当も集計）。

## 生成サービス（無料→少額課金・2026/06 時点）
| サービス | 一貫性 | 価格 | 帰属 | 備考 |
|---|---|---|---|---|
| **Gemini「Nano Banana (Pro)」** ★推奨 | 10枚超でも最良クラス | **$0.067/枚** or Gemini AI Plus **$19.99/月**(~50枚/日) | 不要 | 既に Gemini 利用中＝親和性◎・編集/multi-image に強い |
| **Midjourney v8** | 95%+（Omni Reference） | **$10/月**〜 | 不要 | 画風・美的センス最良。`--cref` は Omni Reference に統合 |
| **Leonardo AI** | Character Reference＋LoRA学習 | 無料(日次)/$12月〜 | 不要 | 無料枠で試せる・キャラ参照特化 |
| **ComfyUI + Stable Diffusion**（ローカル） | IP-Adapter/LoRA/ControlNet | **無料**(要GPU) | — | C満杯→D運用に注意。完全制御・コスト0 |

> **コスト感**：提督100人を Nano Banana で1枚ずつ≒**$6.7**。リテイク込みでも少額。まず無料の Leonardo か手持ちの Gemini で画風を固めてから量産が安全。
> **ローカル無料路線（推奨）**：RTX 3050 8GB で SDXL が回る＝**1枚ゼロ円・無制限・Claude Code からAPIバッチ**。導入手順は **docs/ops/comfyui-setup.md**（D:にポータブル版＋商用可SDXL＋IP-Adapter）。サブスク/APIキー不要。

## 顔の同一性を保つ（identity lock）
プロンプト＋シードだけでは「同じ画風の別人」になりがち。**参照画像**で人物を固定する：
- Midjourney＝**Omni Reference**（強度300–500）、Gemini＝**参照画像を添えて編集**、Leonardo＝**Character Reference**、ComfyUI＝**IP-Adapter/顔LoRA**。
- 運用：①主要提督の「基準顔」を1枚作る→②それを参照に表情/角度違いを生成→③`portraitSeed` を確定して CSV に記録。

## ワークフロー（最短）
1. 主要提督の `appearanceNote`（髪色/性別/年齢等）を埋める（任意・アーキタイプだけでも可）。
2. **`Ginei/Export Portrait Prompts (CSV)`** を実行 → `portrait-prompts.csv`。
3. 画風を1人で確定（StylePreamble は固定なので全員に効く）。基準顔を参照画像化。
4. CSV の prompt＋seed を生成サービスへ（バッチ）。勢力ごとにまとめると軍服が揃って効率的。
5. 出力を `512×512`〜`1024×1024` 程度で取り込み、Texture Type=Sprite で import。
6. 各 `AdmiralData.portrait` に割当（または Resources 命名規約で読む拡張は今後）。
7. CC-BY 系ツール/素材を併用したらクレジットへ。Nano Banana/MJ/Leonardo の生成物は各社規約で商用可（要最新確認）。

## ライセンス注意
- 各サービスの**商用利用条件と帰属要否**を最新の規約で確認（プランにより異なる）。生成物の権利は各社規約に従う。
- 実在人物・既存作品キャラの肖像を模倣しない（オリジナル提督として生成）。
