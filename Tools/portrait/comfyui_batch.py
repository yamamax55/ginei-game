#!/usr/bin/env python3
# Tools/portrait/comfyui_batch.py
# 提督ポートレートを ComfyUI(ローカル) で一括生成する（③アート一貫性・ステップA）。
# 依存なし（標準ライブラリのみ）。ComfyUI が 127.0.0.1:8188 で起動している前提。
#
# 使い方:
#   単発テスト:  python comfyui_batch.py --test --prompt "..." --seed 123 --name test
#   CSV一括:     python comfyui_batch.py --csv portrait-prompts.csv
#   主な任意引数: --server 127.0.0.1:8188 --ckpt sd_xl_base_1.0.safetensors
#                 --steps 30 --cfg 7.0 --width 832 --height 1216
#                 --out "Assets/Art/Portraits" --overwrite
#
# CSV は `Ginei/Export Portrait Prompts (CSV)` の出力（列: admiralName,fullName,faction,
# rankTier,seed,hasPortrait,prompt）。出力は <out>/portrait_<admiralName>.png。

import argparse, csv, json, os, sys, time, urllib.request, urllib.parse, urllib.error

NEGATIVE = ("lowres, bad anatomy, bad hands, extra fingers, deformed, blurry, "
            "watermark, signature, text, jpeg artifacts, multiple people, nsfw")

def build_graph(prompt, seed, ckpt, steps, cfg, width, height, prefix):
    """SDXL text2img の API グラフ（標準コアノードのみ）。"""
    return {
        "4": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": ckpt}},
        "6": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["4", 1]}},
        "7": {"class_type": "CLIPTextEncode", "inputs": {"text": NEGATIVE, "clip": ["4", 1]}},
        "5": {"class_type": "EmptyLatentImage", "inputs": {"width": width, "height": height, "batch_size": 1}},
        "3": {"class_type": "KSampler", "inputs": {
            "seed": seed, "steps": steps, "cfg": cfg,
            "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0,
            "model": ["4", 0], "positive": ["6", 0], "negative": ["7", 0], "latent_image": ["5", 0]}},
        "8": {"class_type": "VAEDecode", "inputs": {"samples": ["3", 0], "vae": ["4", 2]}},
        "9": {"class_type": "SaveImage", "inputs": {"filename_prefix": prefix, "images": ["8", 0]}},
    }

def http_json(url, payload=None):
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode())

def queue_prompt(server, graph):
    return http_json(f"http://{server}/prompt", {"prompt": graph})["prompt_id"]

def wait_outputs(server, prompt_id, timeout=300):
    """/history をポーリングして SaveImage の出力ファイル情報を返す。実行エラーは即時に上げる。"""
    deadline = time.time() + timeout
    while time.time() < deadline:
        hist = http_json(f"http://{server}/history/{prompt_id}")
        if prompt_id in hist:
            rec = hist[prompt_id]
            status = rec.get("status", {})
            if status.get("status_str") == "error":
                raise RuntimeError("ComfyUI 実行エラー: " + _error_text(status))
            imgs = [im for node in rec.get("outputs", {}).values() for im in node.get("images", [])]
            if imgs:
                return imgs
        time.sleep(1.0)
    raise TimeoutError(f"prompt {prompt_id} の出力待ちがタイムアウト")

def _error_text(status):
    for m in status.get("messages", []):
        if m and m[0] == "execution_error" and len(m) > 1:
            info = m[1]
            return f"{info.get('node_type','')}: {info.get('exception_message','')}".strip()
    return "詳細不明（ComfyUI コンソール参照）"

def fetch_image(server, im):
    q = urllib.parse.urlencode({"filename": im["filename"],
                                "subfolder": im.get("subfolder", ""),
                                "type": im.get("type", "output")})
    with urllib.request.urlopen(f"http://{server}/view?{q}", timeout=60) as r:
        return r.read()

def generate(server, prompt, seed, name, args):
    prefix = "ginei_portrait/" + safe(name)
    graph = build_graph(prompt, seed, args.ckpt, args.steps, args.cfg, args.width, args.height, prefix)
    pid = queue_prompt(server, graph)
    imgs = wait_outputs(server, pid, args.timeout)
    png = fetch_image(server, imgs[0])
    os.makedirs(args.out, exist_ok=True)
    path = os.path.join(args.out, f"portrait_{safe(name)}.png")
    with open(path, "wb") as f:
        f.write(png)
    return path

def safe(s):
    return "".join(c if (c.isalnum() or c in "-_") else "_" for c in (s or "noname"))

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--server", default="127.0.0.1:8188")
    ap.add_argument("--ckpt", default="sd_xl_base_1.0.safetensors")
    ap.add_argument("--csv")
    ap.add_argument("--test", action="store_true")
    ap.add_argument("--prompt", default="")
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--name", default="test")
    ap.add_argument("--steps", type=int, default=30)
    ap.add_argument("--cfg", type=float, default=7.0)
    ap.add_argument("--width", type=int, default=768)   # 8GB VRAM 安全値（832x1216 は OOM しがち）
    ap.add_argument("--height", type=int, default=1024)
    ap.add_argument("--out", default="Assets/Art/Portraits")
    ap.add_argument("--timeout", type=int, default=300)
    ap.add_argument("--overwrite", action="store_true")
    args = ap.parse_args()

    # 疎通確認
    try:
        http_json(f"http://{args.server}/system_stats")
    except Exception as e:
        print(f"[ERROR] ComfyUI に接続できません（{args.server}）。起動を確認してください: {e}")
        sys.exit(2)

    if args.test:
        p = args.prompt or "anime portrait of a stern fleet admiral, imperial uniform, bust shot"
        print(f"[test] seed={args.seed} name={args.name}")
        print("  ->", generate(args.server, p, args.seed, args.name, args))
        return

    if not args.csv:
        print("--csv か --test のどちらかを指定してください。")
        sys.exit(1)

    with open(args.csv, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))
    print(f"[batch] {len(rows)} 件")
    ok = 0
    for i, row in enumerate(rows, 1):
        name = row.get("admiralName") or row.get("fullName") or f"admiral{i}"
        out_path = os.path.join(args.out, f"portrait_{safe(name)}.png")
        if os.path.exists(out_path) and not args.overwrite:
            print(f"  [{i}/{len(rows)}] skip（既存）: {name}")
            continue
        prompt = row.get("prompt", "")
        try:
            seed = int(row.get("seed") or 0)
        except ValueError:
            seed = 0
        try:
            path = generate(args.server, prompt, seed, name, args)
            ok += 1
            print(f"  [{i}/{len(rows)}] OK: {path}")
        except Exception as e:
            print(f"  [{i}/{len(rows)}] FAIL {name}: {e}")
    print(f"[done] {ok}/{len(rows)} 生成。出力: {args.out}")

if __name__ == "__main__":
    main()
