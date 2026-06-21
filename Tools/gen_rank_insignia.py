# -*- coding: utf-8 -*-
"""
階級章ジェネレータ（#階級章・SVGでなくPILで手続き生成）。
帝国軍（金・月桂樹＋星章＋冠＝荘厳）／同盟軍（銀青・星章＋環＋翼＝近代的）の
18階級ぶんを描き分け、透過PNGで出力する。Gemini生成の代替＝一貫性・調整可能・再生成可能。

出力: <out>/帝国/<階級>.png, <out>/同盟/<階級>.png （既定 128px・4x スーパーサンプリング）

階級体系（MilitaryGrade 列挙順）:
  兵卒:   二等兵 一等兵 上等兵 兵長        … 縦棒の本数 1..4
  下士官: 伍長 軍曹 曹長                  … 山形(シェブロン)の数 1..3
  准士官: 准尉                            … 1本の指揮棒＋菱形
  尉佐官: 少尉 大尉 少佐 大佐             … 星章 1..4（佐官は下線帯つき）
  将官:   准将 少将 中将 大将 上級大将 元帥 … 月桂樹/環に星章 1..5、元帥は交差指揮棒＋冠/翼
"""
import math, os, sys

from PIL import Image, ImageDraw

SS = 4               # スーパーサンプリング倍率
OUT_PX = 128         # 最終出力サイズ
S = OUT_PX * SS      # 作業キャンバスサイズ

# ----- パレット -----
PAL = {
    "帝国": dict(main=(232, 196, 78), hi=(245, 223, 130), dk=(176, 138, 44)),   # 金
    "同盟": dict(main=(196, 214, 236), hi=(232, 240, 250), dk=(120, 150, 196)), # 銀青
}

GRADES = [
    ("二等兵", "兵卒"), ("一等兵", "兵卒"), ("上等兵", "兵卒"), ("兵長", "兵卒"),
    ("伍長", "下士官"), ("軍曹", "下士官"), ("曹長", "下士官"),
    ("准尉", "准士官"),
    ("少尉", "尉佐官"), ("大尉", "尉佐官"), ("少佐", "尉佐官"), ("大佐", "尉佐官"),
    ("准将", "将官"), ("少将", "将官"), ("中将", "将官"), ("大将", "将官"),
    ("上級大将", "将官"), ("元帥", "将官"),
]


def star_points(cx, cy, r_out, r_in, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        ang = math.radians(rot + i * 180.0 / n)
        r = r_out if i % 2 == 0 else r_in
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    return pts


def draw_star(d, cx, cy, r, col, edge=None):
    pts = star_points(cx, cy, r, r * 0.42)
    d.polygon(pts, fill=col, outline=edge, width=max(1, SS))


def leaf(d, cx, cy, length, width, angle, col, edge):
    """とがった木の葉（アーモンド）を多角形で。angle=葉の向き(度)。"""
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)
    pts_local = [(-length / 2, 0), (0, -width / 2), (length / 2, 0), (0, width / 2)]
    # ベジェ風にふくらませた6点
    pts_local = [(-length/2,0),(-length*0.18,-width/2),(length*0.18,-width/2),
                 (length/2,0),(length*0.18,width/2),(-length*0.18,width/2)]
    pts = [(cx + x * ca - y * sa, cy + x * sa + y * ca) for (x, y) in pts_local]
    d.polygon(pts, fill=col, outline=edge, width=max(1, SS // 2))


def draw_wreath(d, cx, cy, R, pal, full=1.0, ring=False):
    """月桂樹の環（左右対称の葉列）。ring=True なら単純な環（同盟向け）。"""
    col, edge = pal["main"], pal["dk"]
    if ring:
        w = int(7 * SS * (0.7 + 0.5 * full))
        d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=col, width=w)
        # 環の上端に小さな星
        return
    n = int(8 + 4 * full)          # 片側の葉数
    a0, a1 = 95, 265               # 下を開けた弧（度）。左右に展開
    llen = R * 0.46
    lwid = R * 0.22
    for side in (-1, 1):
        for i in range(n):
            t = i / (n - 1)
            ang = math.radians(a0 + t * (a1 - a0))
            # side=-1 は左弧、+1 は右弧（ミラー）
            ax = cx + side * (R * math.sin(math.radians(180) * 0)) * 0  # placeholder
            px = cx + side * R * math.sin(math.radians(180 - (a0 + t*(a1-a0)))) * 0
            # 円周座標
            ex = cx + side * R * math.cos(ang - math.radians(90))
            ey = cy - R * math.sin(ang - math.radians(90))
            # 葉は接線方向に傾ける
            tang = math.degrees(ang) + 90 * side
            leaf(d, ex, ey, llen, lwid, tang, col, edge)


def draw_bars(d, pal, n):
    col, edge = pal["main"], pal["dk"]
    bw = S * 0.10
    gap = S * 0.07
    total = n * bw + (n - 1) * gap
    x0 = S / 2 - total / 2
    h = S * 0.34
    y0 = S / 2 - h / 2
    for i in range(n):
        x = x0 + i * (bw + gap)
        d.rounded_rectangle([x, y0, x + bw, y0 + h], radius=bw * 0.3,
                            fill=col, outline=edge, width=max(1, SS))


def draw_chevrons(d, pal, n):
    col, edge = pal["main"], pal["dk"]
    cx = S / 2
    th = S * 0.085          # 山形の太さ
    span = S * 0.30         # 片腕の水平幅
    drop = S * 0.20         # V の落差
    gap = S * 0.135
    base_y = S * 0.66
    for i in range(n):
        y = base_y - i * gap
        pts = [
            (cx - span, y - drop), (cx - span + th, y - drop),
            (cx, y), (cx + span - th, y - drop), (cx + span, y - drop),
            (cx, y + th * 1.4),
        ]
        d.polygon(pts, fill=col, outline=edge, width=max(1, SS))


def draw_warrant(d, pal):
    col, edge = pal["main"], pal["dk"]
    cx = S / 2
    w = S * 0.13
    d.rounded_rectangle([cx - w / 2, S * 0.20, cx + w / 2, S * 0.80],
                        radius=w * 0.4, fill=col, outline=edge, width=max(1, SS))
    # 中央の菱形
    r = S * 0.12
    d.polygon([(cx, S/2 - r), (cx + r*0.7, S/2), (cx, S/2 + r), (cx - r*0.7, S/2)],
              fill=pal["hi"], outline=edge, width=max(1, SS))


def star_cluster(d, cx, cy, r, pal, n):
    col, edge = pal["main"], pal["dk"]
    if n <= 1:
        draw_star(d, cx, cy, r, pal["hi"], edge); return
    if n == 2:
        for dx in (-1, 1): draw_star(d, cx + dx * r * 1.05, cy, r, pal["hi"], edge)
        return
    if n == 3:
        draw_star(d, cx, cy - r * 0.95, r, pal["hi"], edge)
        for dx in (-1, 1): draw_star(d, cx + dx * r * 1.0, cy + r * 0.7, r, pal["hi"], edge)
        return
    if n == 4:
        for dx in (-1, 1):
            draw_star(d, cx + dx * r * 1.05, cy - r * 0.95, r, pal["hi"], edge)
            draw_star(d, cx + dx * r * 1.05, cy + r * 0.95, r, pal["hi"], edge)
        return
    # 5: 中央＋四隅
    draw_star(d, cx, cy, r, pal["hi"], edge)
    for dx in (-1, 1):
        draw_star(d, cx + dx * r * 1.25, cy - r * 1.1, r * 0.92, pal["hi"], edge)
        draw_star(d, cx + dx * r * 1.25, cy + r * 1.1, r * 0.92, pal["hi"], edge)


def crossed_batons(d, pal):
    col, edge = pal["dk"], pal["main"]
    cx, cy = S / 2, S / 2
    L = S * 0.40
    for ang in (35, -35):
        a = math.radians(ang)
        dx, dy = math.cos(a) * L, math.sin(a) * L
        d.line([(cx - dx, cy - dy), (cx + dx, cy + dy)], fill=col, width=int(S * 0.045))
        for sgn in (-1, 1):
            ex, ey = cx + sgn * dx, cy + sgn * dy
            d.ellipse([ex - S*0.035, ey - S*0.035, ex + S*0.035, ey + S*0.035],
                      fill=pal["main"], outline=edge, width=max(1, SS))


def crown(d, cx, cy, w, pal):
    col, edge = pal["main"], pal["dk"]
    h = w * 0.7
    base = cy + h / 2
    pts = [(cx - w/2, base), (cx - w/2, cy), (cx - w*0.25, cy + h*0.18),
           (cx, cy - h*0.35), (cx + w*0.25, cy + h*0.18), (cx + w/2, cy),
           (cx + w/2, base)]
    d.polygon(pts, fill=col, outline=edge, width=max(1, SS))
    for px in (cx - w/2, cx, cx + w/2):
        d.ellipse([px - w*0.07, cy - h*0.5 - w*0.07, px + w*0.07, cy - h*0.5 + w*0.07],
                  fill=pal["hi"], outline=edge, width=max(1, SS))


def wings(d, cx, cy, w, pal):
    col, edge = pal["main"], pal["dk"]
    for side in (-1, 1):
        pts = [(cx, cy), (cx + side*w*0.5, cy - w*0.12),
               (cx + side*w*0.42, cy + w*0.04), (cx + side*w*0.32, cy + w*0.16),
               (cx + side*w*0.16, cy + w*0.10), (cx, cy + w*0.06)]
        d.polygon(pts, fill=col, outline=edge, width=max(1, SS))


def render(grade, cat, faction):
    pal = PAL[faction]
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    idx = [g for g, _ in GRADES].index(grade)
    ring = (faction == "同盟")

    if cat == "兵卒":
        draw_bars(d, pal, idx - 0 + 1)               # 1..4
    elif cat == "下士官":
        draw_chevrons(d, pal, idx - 4 + 1)           # 1..3
    elif cat == "准士官":
        draw_warrant(d, pal)
    elif cat == "尉佐官":
        n = idx - 7                                  # 少尉1..大佐4
        star_cluster(d, S/2, S/2 - (S*0.04 if n >= 3 else 0), S*0.13, pal, n)
        if n >= 3:                                   # 佐官は下線帯
            d.rounded_rectangle([S*0.30, S*0.70, S*0.70, S*0.76], radius=S*0.02,
                                fill=pal["main"], outline=pal["dk"], width=max(1, SS))
    elif cat == "将官":
        n = idx - 11                                 # 准将1..上級大将5, 元帥6
        flag_n = min(n, 5)
        marshal = (grade == "元帥")
        full = (n - 1) / 5.0
        if marshal:
            crossed_batons(d, pal)
        draw_wreath(d, S/2, S/2, S*0.34, pal, full=full, ring=ring)
        star_cluster(d, S/2, S/2, S*0.115, pal, flag_n)
        if grade in ("上級大将", "元帥"):
            if ring:
                wings(d, S/2, S*0.20, S*0.40, pal)
            else:
                crown(d, S/2, S*0.18, S*0.26, pal)

    out = img.resize((OUT_PX, OUT_PX), Image.LANCZOS)
    return out


def main():
    out_root = sys.argv[1] if len(sys.argv) > 1 else "."
    contact = (len(sys.argv) > 2 and sys.argv[2] == "contact")
    for faction in ("帝国", "同盟"):
        fdir = os.path.join(out_root, faction)
        os.makedirs(fdir, exist_ok=True)
        for grade, cat in GRADES:
            im = render(grade, cat, faction)
            im.save(os.path.join(fdir, grade + ".png"))
    print("generated 36 insignia into", out_root)

    if contact:
        cell = OUT_PX
        cols = len(GRADES)
        sheet = Image.new("RGBA", (cols * cell, 2 * cell), (24, 27, 36, 255))
        for r, faction in enumerate(("帝国", "同盟")):
            for c, (grade, cat) in enumerate(GRADES):
                im = Image.open(os.path.join(out_root, faction, grade + ".png"))
                sheet.alpha_composite(im, (c * cell, r * cell))
        sheet.convert("RGB").save(os.path.join(out_root, "_contact.png"))
        print("contact sheet:", os.path.join(out_root, "_contact.png"))


if __name__ == "__main__":
    main()
