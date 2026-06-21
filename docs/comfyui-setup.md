# ComfyUI ローカル画像生成セットアップ（③アート一貫性・無料・D:導入）

> 提督ポートレートをローカルで無料・無制限・Claude Code から自動バッチ生成するための環境。
> 実機確認済：**RTX 3050 8GB / D: 空き50GB / git・uv・Python3.13 あり**。出力先 `Assets/Art/Portraits/` は既存。
> 8GB VRAM では **SDXL がスイートスポット**（SD1.5は余裕・FLUXは重い）。`PortraitPromptRules` の固定シードで顔を安定。

## なぜポータブル版か
ComfyUI 公式の **Windows ポータブル版**は CUDA 対応 PyTorch を同梱＝**Python 環境構築不要・torch のCUDA地雷を回避**（system Python 3.13 で wheel が無い問題も無関係）。**Program Files 以外＝D: に展開**すること（権限問題回避）。

## 手順1：ComfyUI 本体（D:）
1. [ComfyUI Releases](https://github.com/comfyanonymous/ComfyUI/releases) から最新の **`ComfyUI_windows_portable_nvidia.7z`** を入手。
2. **`D:\ComfyUI_windows_portable\`** へ展開（7-Zip）。
3. `run_nvidia_gpu.bat` を実行 → ブラウザで **http://127.0.0.1:8188**（初回は起動に少し時間）。
4. **8GB VRAM の保険**：OOM が出たら `run_nvidia_gpu.bat` の `main.py` 行末に **`--lowvram`**（または `--medvram-sdxl`）を追記。SDXLでも回るが速度優先なら後述の Turbo/Lightning を使う。
   - API は既定で `127.0.0.1:8188` に開いている（`/prompt` エンドポイント＝後段のバッチ生成が叩く）。外部から叩くなら `--listen`。

## 手順2：ComfyUI Manager（ノード/モデル導入を楽に）
1. ComfyUI を停止。`D:\ComfyUI_windows_portable\ComfyUI\custom_nodes\` で Git Bash/コマンドプロンプトを開く。
2. `git clone https://github.com/ltdrdata/ComfyUI-Manager`
3. ComfyUI 再起動 → 右下/上部に **Manager** ボタンが出る。以後ノードとモデルは Manager から導入できる。

## 手順3：モデル（**商用可**を選ぶ）
> 配置先は `D:\ComfyUI_windows_portable\ComfyUI\models\` 配下。Manager の「Install Models」を使うと正しいフォルダへ自動DLされる。

| 種類 | ファイル/入手元 | 置き場所 | ライセンス（商用） |
|---|---|---|---|
| **SDXL ベース** | `sd_xl_base_1.0.safetensors`（[stabilityai/stable-diffusion-xl-base-1.0](https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0)） | `models/checkpoints/` | ✅ OpenRAIL-M |
| SDXL VAE（任意） | `sdxl_vae.safetensors` | `models/vae/` | ✅ |
| **IP-Adapter (SDXL)** | `ip-adapter-plus_sdxl_vit-h.safetensors`（[h94/IP-Adapter](https://huggingface.co/h94/IP-Adapter)） | `models/ipadapter/` | ✅ |
| **CLIP Vision エンコーダ** | `CLIP-ViT-H-14`（plus_vit-h 用の image encoder） | `models/clip_vision/` | ✅ |
| IP-Adapter ノード | `git clone https://github.com/cubiq/ComfyUI_IPAdapter_plus`（custom_nodes/）or Manager | — | — |

- **品質志向の代替チェックポイント**：DreamShaper XL 等（Civitai）。**ただしモデルごとに商用可否が異なる**＝各 Civitai ページのライセンスを確認（DreamShaper は概ね商用可だが要確認）。
- **速度志向（8GB向け）**：SDXL **Turbo/Lightning** 系（数ステップで生成＝1枚数秒）。ポートレート量産に有効。
- **顔の同一性（商用安全）**：IP-Adapter（参照画像）＋ `PortraitPromptRules` の固定シード、または主要提督だけ **LoRA** 学習。**InstantID / IP-Adapter FaceID は顔解析 InsightFace が非商用の懸念**＝商用なら避けるか可否確認。

## 手順4：動作確認
1. Manager →「Install Custom Nodes」で **ComfyUI_IPAdapter_plus**（cubiq）を入れて再起動。
2. デフォルトの SDXL ワークフローで1枚生成 → `Assets` 外（ComfyUI の `output/`）に出ることを確認。
3. 画風（`PortraitPromptRules.StylePreamble`）を1人で固めて「基準顔」を作る → それを IP-Adapter の参照に。

## 次段：Claude Code からのバッチ生成（ステップA）
ComfyUI 起動中（`127.0.0.1:8188`）に、`Ginei/Export Portrait Prompts (CSV)` で出した `portrait-prompts.csv`（prompt＋seed）を `/prompt` API へ流し込み、`Assets/Art/Portraits/portrait_<提督名>.png` を量産するスクリプトを用意できる（このリポジトリの `Tools/` に Python）。導入が済んだら声をかけてください。

## ディスク目安
ComfyUI ポータブル ~2–3GB＋SDXLベース ~6.5GB＋VAE/IPAdapter/clip_vision ~3–4GB ＝ 計 ~12–15GB（D: 空き50GBで十分）。

## 参考
- [ComfyUI 公式 System Requirements](https://docs.comfy.org/installation/system_requirements) / [Windows導入(2026)](https://runaihome.com/blog/comfyui-windows-setup-guide/)
- [cubiq/ComfyUI_IPAdapter_plus](https://github.com/cubiq/ComfyUI_IPAdapter_plus)（モデル表・配置はここが一次情報）
