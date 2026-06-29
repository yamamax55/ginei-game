# -*- coding: utf-8 -*-
"""
キャラ別「勢力紋章＋階級章」合成参照シート生成器（肖像生成の添付高速化・#紋章/#階級章）。

各キャラについて 勢力紋章(emblem) と 該当階級章(rank insignia) を1枚に横並び合成し、
キャラ名・勢力・階級のラベルを焼き込む。これを1ファイル選ぶだけで Gemini 画像モードに
両参照を添付できる（ピッカーのフォルダ往復・複数選択・スクロールが不要＝高速化）。

入力：docs/lore/_assets/emblems/<KEY>.png ＋ docs/lore/_assets/rank/<勢力>/<階級>.png
出力：docs/lore/_assets/refs/ref_<EnName>.png（横並び合成・ラベル付き）

新キャラを足すときは CHARS に1行追加して再実行するだけ。
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "docs", "lore", "_assets")
OUT = os.path.join(ASSETS, "refs")

EMBLEM_KEY = {
    "黎明評議会": "DawnCouncil", "碧晶公国": "JadePrincipality", "自由港同位体": "FreePort",
    "灯心自由市ファロス": "Pharos", "鉄環兵団": "IronRing",
}

# EnName : (勢力, 階級)
CHARS = {
    "Lorenzo":  ("自由港同位体",     "中将"),
    "Enrico":   ("灯心自由市ファロス", "中将"),
    "Brigitte": ("鉄環兵団",         "准将"),
    "Hedwig":   ("鉄環兵団",         "少将"),
}

S = 360  # 各章の表示サイズ（大きめ＝Geminiが読みやすい）
PAD = 24
LABEL_H = 56


def _font(size):
    for p in [r"C:\Windows\Fonts\msgothic.ttc", r"C:\Windows\Fonts\meiryo.ttc",
              os.path.join(ROOT, "Assets", "Fonts", "msgothic.ttc")]:
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


def build(enname, faction, rank):
    em_key = EMBLEM_KEY[faction]
    em = Image.open(os.path.join(ASSETS, "emblems", em_key + ".png")).convert("RGBA").resize((S, S))
    rk = Image.open(os.path.join(ASSETS, "rank", faction, rank + ".png")).convert("RGBA").resize((S, S))
    W = PAD * 3 + S * 2
    H = PAD * 2 + S + LABEL_H
    canvas = Image.new("RGBA", (W, H), (255, 255, 255, 255))
    canvas.alpha_composite(em, (PAD, PAD))
    canvas.alpha_composite(rk, (PAD * 2 + S, PAD))
    d = ImageDraw.Draw(canvas)
    f = _font(28)
    fs = _font(22)
    d.text((PAD, PAD + S + 8), f"{enname}  ({faction} / {rank})", fill=(20, 20, 20), font=f)
    d.text((PAD + S // 2 - 40, PAD - 2), "紋章", fill=(120, 120, 120), font=fs)
    d.text((PAD * 2 + S + S // 2 - 40, PAD - 2), "階級章", fill=(120, 120, 120), font=fs)
    out = os.path.join(OUT, f"ref_{enname}.png")
    canvas.convert("RGB").save(out)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    for en, (fac, rk) in CHARS.items():
        p = build(en, fac, rk)
        print("wrote", os.path.relpath(p, ROOT), os.path.getsize(p), "bytes")


if __name__ == "__main__":
    main()
