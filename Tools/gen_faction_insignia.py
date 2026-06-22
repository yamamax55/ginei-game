# -*- coding: utf-8 -*-
"""
勢力の紋章＋階級章ジェネレータ（SVGコード生成・#紋章/#階級章）。

新5勢力（黎明評議会／碧晶公国／自由港同位体／灯心自由市ファロス／鉄環兵団）について
- 紋章（emblem）を1枚ずつ
- 階級章（rank insignia）を共通ラダー（全18階級）＋勢力色/モチーフ替えで
SVGで生成し、resvg で透過PNGへもラスタライズする。さらに勢力ごとの一覧チャートPNGも出す。

「共通ラダー＋勢力色/モチーフ替え」＝階級の符号化（兵卒=縦棒/下士官=山形/将官=星＋環…）は全勢力共通、
パレットと将官の環(frame)・中央モチーフだけ勢力ごとに替える。既存 gen_rank_insignia.py(帝国/同盟・PIL)の
視覚文法をSVGへ移植したもの。

出力（Unityのmeta氾濫を避けるため Assets/ でなく lore/Drive用の _assets/ へ）:
  docs/lore/_assets/emblems/<faction>.svg / .png
  docs/lore/_assets/rank/<faction>/<grade>.svg / .png
  docs/lore/_assets/rank/<faction>_chart.png   （18階級の一覧）
"""
import math, os
import resvg_py

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "lore", "_assets")
VB = 128                      # SVG viewBox（正方）
PNG_PX = 256                 # ラスタPNG解像度

# ---- 勢力パレット＋モチーフ（main=主色 hi=明 dk=暗 accent=差し色 field=台地色 frame=将官の環 motif=紋章の意匠）----
FACTIONS = {
    "黎明評議会":          dict(main="#E8C44E", hi="#F5DF82", dk="#B08A2C", accent="#1F2E5A", field="#FFFFFF", frame="wreath", motif="dawn"),
    "碧晶公国":            dict(main="#E8C44E", hi="#F5DF82", dk="#B08A2C", accent="#2E8B7A", field="#F2FBF7", frame="ring",   motif="crystal"),
    "自由港同位体":        dict(main="#E8C44E", hi="#F5DF82", dk="#B08A2C", accent="#C0392B", field="#FFFFFF", frame="rope",   motif="compass"),
    "灯心自由市ファロス":  dict(main="#E8C44E", hi="#F5DF82", dk="#B08A2C", accent="#1F3A6E", field="#FFFFFF", frame="ring",   motif="lighthouse"),
    "鉄環兵団":            dict(main="#C2C9D1", hi="#E6EAEF", dk="#6B7480", accent="#B11E2A", field="#2A2E33", frame="iron",   motif="ironcross"),
}
# 紋章ファイル名（英語・既存 ImperialEmblem/AllianceEmblem 命名に合わせる）
EMBLEM_KEY = {
    "黎明評議会": "DawnCouncil", "碧晶公国": "JadePrincipality", "自由港同位体": "FreePort",
    "灯心自由市ファロス": "Pharos", "鉄環兵団": "IronRing",
}

GRADES = [
    ("二等兵", "兵卒"), ("一等兵", "兵卒"), ("上等兵", "兵卒"), ("兵長", "兵卒"),
    ("伍長", "下士官"), ("軍曹", "下士官"), ("曹長", "下士官"),
    ("准尉", "准士官"),
    ("少尉", "尉官"), ("大尉", "尉官"), ("少佐", "佐官"), ("大佐", "佐官"),
    ("准将", "将官"), ("少将", "将官"), ("中将", "将官"), ("大将", "将官"),
    ("上級大将", "将官"), ("元帥", "将官"),
]

# ---------- SVG プリミティブ（文字列を返す） ----------
def _star(cx, cy, r, fill, edge, rin_ratio=0.42, rot=-90):
    pts = []
    for i in range(10):
        ang = math.radians(rot + i * 36)
        rr = r if i % 2 == 0 else r * rin_ratio
        pts.append(f"{cx+rr*math.cos(ang):.2f},{cy+rr*math.sin(ang):.2f}")
    return f'<polygon points="{" ".join(pts)}" fill="{fill}" stroke="{edge}" stroke-width="1.2" stroke-linejoin="round"/>'

def _stars(cx, cy, r, p, n):
    f, e = p["hi"], p["dk"]
    if n <= 1: return _star(cx, cy, r, f, e)
    if n == 2: return _star(cx-r*1.05, cy, r, f, e)+_star(cx+r*1.05, cy, r, f, e)
    if n == 3: return (_star(cx, cy-r*0.95, r, f, e)+_star(cx-r, cy+r*0.7, r, f, e)+_star(cx+r, cy+r*0.7, r, f, e))
    if n == 4: return "".join(_star(cx+dx*r*1.05, cy+dy*r*0.95, r, f, e) for dy in (-1,1) for dx in (-1,1))
    return (_star(cx, cy, r, f, e)+"".join(_star(cx+dx*r*1.25, cy+dy*r*1.1, r*0.92, f, e) for dy in (-1,1) for dx in (-1,1)))

def _bars(p, n):
    col, edge = p["main"], p["dk"]; bw, gap = VB*0.10, VB*0.07
    total = n*bw + (n-1)*gap; x0 = VB/2 - total/2; h = VB*0.34; y0 = VB/2 - h/2
    s = ""
    for i in range(n):
        x = x0 + i*(bw+gap)
        s += f'<rect x="{x:.2f}" y="{y0:.2f}" width="{bw:.2f}" height="{h:.2f}" rx="{bw*0.3:.2f}" fill="{col}" stroke="{edge}" stroke-width="1.2"/>'
    return s

def _chevrons(p, n):
    col, edge = p["main"], p["dk"]; cx = VB/2
    th, span, drop, gap, base = VB*0.085, VB*0.30, VB*0.20, VB*0.135, VB*0.66
    s = ""
    for i in range(n):
        y = base - i*gap
        pts = f"{cx-span:.2f},{y-drop:.2f} {cx-span+th:.2f},{y-drop:.2f} {cx:.2f},{y:.2f} {cx+span-th:.2f},{y-drop:.2f} {cx+span:.2f},{y-drop:.2f} {cx:.2f},{y+th*1.4:.2f}"
        s += f'<polygon points="{pts}" fill="{col}" stroke="{edge}" stroke-width="1.2" stroke-linejoin="round"/>'
    return s

def _warrant(p):
    col, edge, hi = p["main"], p["dk"], p["hi"]; cx = VB/2; w = VB*0.13
    s = f'<rect x="{cx-w/2:.2f}" y="{VB*0.20:.2f}" width="{w:.2f}" height="{VB*0.60:.2f}" rx="{w*0.4:.2f}" fill="{col}" stroke="{edge}" stroke-width="1.2"/>'
    r = VB*0.12
    s += f'<polygon points="{cx:.2f},{VB/2-r:.2f} {cx+r*0.7:.2f},{VB/2:.2f} {cx:.2f},{VB/2+r:.2f} {cx-r*0.7:.2f},{VB/2:.2f}" fill="{hi}" stroke="{edge}" stroke-width="1.2"/>'
    return s

def _underline(p):
    col = p["main"]
    return f'<rect x="{VB*0.30:.2f}" y="{VB*0.70:.2f}" width="{VB*0.40:.2f}" height="{VB*0.05:.2f}" rx="2" fill="{col}"/>'

def _leaf(cx, cy, ln, wd, ang, col, edge):
    a = math.radians(ang); ca, sa = math.cos(a), math.sin(a)
    loc = [(-ln/2,0),(-ln*0.18,-wd/2),(ln*0.18,-wd/2),(ln/2,0),(ln*0.18,wd/2),(-ln*0.18,wd/2)]
    pts = " ".join(f"{cx+x*ca-y*sa:.2f},{cy+x*sa+y*ca:.2f}" for x,y in loc)
    return f'<polygon points="{pts}" fill="{col}" stroke="{edge}" stroke-width="0.6" stroke-linejoin="round"/>'

def _frame(p, R=VB*0.42):
    """将官の環。frame種別で見た目を替える。"""
    cx = cy = VB/2; col, edge, acc = p["main"], p["dk"], p["accent"]; kind = p["frame"]
    if kind == "wreath":
        s = ""; n = 9
        for side in (-1, 1):
            for i in range(n):
                t = i/(n-1); ang = math.radians(95 + t*170)
                ex = cx + side*R*math.cos(ang-math.radians(90)); ey = cy - R*math.sin(ang-math.radians(90))
                s += _leaf(ex, ey, R*0.40, R*0.18, math.degrees(ang)+90*side, col, edge)
        return s
    if kind == "ring":
        return f'<circle cx="{cx}" cy="{cy}" r="{R:.2f}" fill="none" stroke="{col}" stroke-width="{VB*0.045:.2f}"/><circle cx="{cx}" cy="{cy}" r="{R*0.86:.2f}" fill="none" stroke="{acc}" stroke-width="{VB*0.015:.2f}"/>'
    if kind == "rope":
        dash = f'stroke-dasharray="{VB*0.05:.2f} {VB*0.03:.2f}"'
        return f'<circle cx="{cx}" cy="{cy}" r="{R:.2f}" fill="none" stroke="{col}" stroke-width="{VB*0.05:.2f}" {dash} stroke-linecap="round"/>'
    if kind == "iron":
        return f'<circle cx="{cx}" cy="{cy}" r="{R:.2f}" fill="none" stroke="{p["dk"]}" stroke-width="{VB*0.075:.2f}"/><circle cx="{cx}" cy="{cy}" r="{R:.2f}" fill="none" stroke="{col}" stroke-width="{VB*0.03:.2f}"/>'
    return ""

def _crossed_batons(p):
    col, edge = p["dk"], p["main"]; cx = cy = VB/2; L = VB*0.40; s = ""
    for ang in (35, -35):
        a = math.radians(ang); dx, dy = math.cos(a)*L, math.sin(a)*L
        s += f'<line x1="{cx-dx:.2f}" y1="{cy-dy:.2f}" x2="{cx+dx:.2f}" y2="{cy+dy:.2f}" stroke="{col}" stroke-width="{VB*0.045:.2f}" stroke-linecap="round"/>'
        for sgn in (-1,1):
            ex, ey = cx+sgn*dx, cy+sgn*dy
            s += f'<circle cx="{ex:.2f}" cy="{ey:.2f}" r="{VB*0.035:.2f}" fill="{p["main"]}" stroke="{edge}" stroke-width="1"/>'
    return s

def _crown(cx, cy, w, p):
    col, edge, hi = p["main"], p["dk"], p["hi"]; h = w*0.7; base = cy+h/2
    pts = f"{cx-w/2:.2f},{base:.2f} {cx-w/2:.2f},{cy:.2f} {cx-w*0.25:.2f},{cy+h*0.18:.2f} {cx:.2f},{cy-h*0.35:.2f} {cx+w*0.25:.2f},{cy+h*0.18:.2f} {cx+w/2:.2f},{cy:.2f} {cx+w/2:.2f},{base:.2f}"
    return f'<polygon points="{pts}" fill="{col}" stroke="{edge}" stroke-width="1.2" stroke-linejoin="round"/>'

# ---------- 紋章モチーフ ----------
def _motif(p, cx, cy, R):
    kind = p["motif"]; col, hi, edge, acc = p["main"], p["hi"], p["dk"], p["accent"]
    if kind == "dawn":   # 黎明＝地平からの旭光＋翼
        s = f'<line x1="{cx-R:.2f}" y1="{cy+R*0.35:.2f}" x2="{cx+R:.2f}" y2="{cy+R*0.35:.2f}" stroke="{col}" stroke-width="{R*0.10:.2f}"/>'
        for k in range(-3, 4):
            ang = math.radians(-90 + k*22)
            s += f'<line x1="{cx:.2f}" y1="{cy+R*0.35:.2f}" x2="{cx+R*0.9*math.cos(ang):.2f}" y2="{cy+R*0.35+R*0.9*math.sin(ang):.2f}" stroke="{hi}" stroke-width="{R*0.05:.2f}"/>'
        s += f'<circle cx="{cx:.2f}" cy="{cy+R*0.35:.2f}" r="{R*0.22:.2f}" fill="{acc}" stroke="{col}" stroke-width="2"/>'
        return s
    if kind == "crystal":  # 碧晶＝多面結晶
        pts = f"{cx:.2f},{cy-R*0.85:.2f} {cx+R*0.55:.2f},{cy-R*0.2:.2f} {cx+R*0.32:.2f},{cy+R*0.8:.2f} {cx-R*0.32:.2f},{cy+R*0.8:.2f} {cx-R*0.55:.2f},{cy-R*0.2:.2f}"
        s = f'<polygon points="{pts}" fill="{acc}" stroke="{col}" stroke-width="2.5" stroke-linejoin="round"/>'
        s += f'<line x1="{cx:.2f}" y1="{cy-R*0.85:.2f}" x2="{cx:.2f}" y2="{cy+R*0.8:.2f}" stroke="{hi}" stroke-width="1.5"/>'
        s += f'<line x1="{cx-R*0.55:.2f}" y1="{cy-R*0.2:.2f}" x2="{cx+R*0.55:.2f}" y2="{cy-R*0.2:.2f}" stroke="{hi}" stroke-width="1.5"/>'
        return s
    if kind == "compass":  # 自由港＝羅針盤
        s = f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{R*0.78:.2f}" fill="none" stroke="{col}" stroke-width="{R*0.07:.2f}"/>'
        s += f'<polygon points="{cx:.2f},{cy-R*0.7:.2f} {cx+R*0.16:.2f},{cy:.2f} {cx:.2f},{cy+R*0.7:.2f} {cx-R*0.16:.2f},{cy:.2f}" fill="{acc}" stroke="{col}" stroke-width="1.5"/>'
        s += f'<polygon points="{cx-R*0.7:.2f},{cy:.2f} {cx:.2f},{cy-R*0.16:.2f} {cx+R*0.7:.2f},{cy:.2f} {cx:.2f},{cy+R*0.16:.2f}" fill="{hi}" stroke="{col}" stroke-width="1.5"/>'
        return s
    if kind == "lighthouse":  # ファロス＝灯台＋光
        s = f'<polygon points="{cx-R*0.18:.2f},{cy+R*0.7:.2f} {cx+R*0.18:.2f},{cy+R*0.7:.2f} {cx+R*0.12:.2f},{cy-R*0.45:.2f} {cx-R*0.12:.2f},{cy-R*0.45:.2f}" fill="{hi}" stroke="{col}" stroke-width="1.5"/>'
        s += f'<rect x="{cx-R*0.16:.2f}" y="{cy-R*0.62:.2f}" width="{R*0.32:.2f}" height="{R*0.2:.2f}" fill="{acc}" stroke="{col}" stroke-width="1.5"/>'
        for k in (-1,1):
            s += f'<polygon points="{cx:.2f},{cy-R*0.52:.2f} {cx+k*R*0.85:.2f},{cy-R*0.82:.2f} {cx+k*R*0.85:.2f},{cy-R*0.32:.2f}" fill="{col}" opacity="0.85"/>'
        return s
    if kind == "ironcross":  # 鉄環＝鉄環＋鉄十字
        s = f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{R*0.8:.2f}" fill="none" stroke="{col}" stroke-width="{R*0.14:.2f}"/>'
        a = R*0.16; b = R*0.5
        s += f'<polygon points="{cx-a:.2f},{cy-b:.2f} {cx+a:.2f},{cy-b:.2f} {cx+a:.2f},{cy-a:.2f} {cx+b:.2f},{cy-a:.2f} {cx+b:.2f},{cy+a:.2f} {cx+a:.2f},{cy+a:.2f} {cx+a:.2f},{cy+b:.2f} {cx-a:.2f},{cy+b:.2f} {cx-a:.2f},{cy+a:.2f} {cx-b:.2f},{cy+a:.2f} {cx-b:.2f},{cy-a:.2f} {cx-a:.2f},{cy-a:.2f}" fill="{acc}" stroke="{hi}" stroke-width="1.5" stroke-linejoin="round"/>'
        return s
    return ""

# ---------- 組み立て ----------
def _svg_wrap(inner):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {VB} {VB}" width="{VB}" height="{VB}">'
            f'{inner}</svg>')

def rank_svg(p, grade, cat):
    body = ""
    if cat == "兵卒":
        body = _bars(p, ["二等兵","一等兵","上等兵","兵長"].index(grade)+1)
    elif cat == "下士官":
        body = _chevrons(p, ["伍長","軍曹","曹長"].index(grade)+1)
    elif cat == "准士官":
        body = _warrant(p)
    elif cat == "尉官":
        n = ["少尉","大尉"].index(grade)+1
        body = _stars(VB/2, VB/2, VB*0.13, p, n)
    elif cat == "佐官":
        n = ["少佐","大佐"].index(grade)+1
        body = _stars(VB/2, VB*0.46, VB*0.13, p, n) + _underline(p)
    elif cat == "将官":
        if grade == "元帥":
            body = _frame(p) + _crossed_batons(p) + _crown(VB/2, VB*0.20, VB*0.26, p)
        else:
            n = ["准将","少将","中将","大将","上級大将"].index(grade)+1
            body = _frame(p) + _stars(VB/2, VB/2, VB*0.11, p, n)
    return _svg_wrap(body)

def emblem_svg(name, p):
    cx = cy = VB/2; R = VB*0.40
    # 台＝勢力色の盾形ディスク
    disc = (f'<circle cx="{cx}" cy="{cy}" r="{VB*0.46:.2f}" fill="{p["field"]}" stroke="{p["dk"]}" stroke-width="2.5"/>'
            f'<circle cx="{cx}" cy="{cy}" r="{VB*0.46:.2f}" fill="none" stroke="{p["main"]}" stroke-width="3"/>'
            f'<circle cx="{cx}" cy="{cy}" r="{VB*0.40:.2f}" fill="none" stroke="{p["accent"]}" stroke-width="1.5" opacity="0.8"/>')
    return _svg_wrap(disc + _motif(p, cx, cy, R))

def rasterize(svg, path_png):
    data = resvg_py.svg_to_bytes(svg_string=svg, width=PNG_PX, height=PNG_PX)
    open(path_png, "wb").write(bytes(data))

def main():
    em_dir = os.path.join(OUT, "emblems"); os.makedirs(em_dir, exist_ok=True)
    montage_cells = {}
    for fac, p in FACTIONS.items():
        # 紋章
        key = EMBLEM_KEY[fac]
        esvg = emblem_svg(fac, p)
        open(os.path.join(em_dir, key+".svg"), "w", encoding="utf-8").write(esvg)
        rasterize(esvg, os.path.join(em_dir, key+".png"))
        # 階級章
        rdir = os.path.join(OUT, "rank", fac); os.makedirs(rdir, exist_ok=True)
        cells = []
        for grade, cat in GRADES:
            svg = rank_svg(p, grade, cat)
            open(os.path.join(rdir, grade+".svg"), "w", encoding="utf-8").write(svg)
            rasterize(svg, os.path.join(rdir, grade+".png"))
            cells.append((grade, svg))
        montage_cells[fac] = cells
        # 一覧チャート（6列×3行のSVG→PNG）
        cols = 6; cw = 150; ch = 168
        rows = (len(GRADES)+cols-1)//cols
        parts = [f'<rect width="{cols*cw}" height="{rows*ch}" fill="{p["field"] if p["field"]!="#FFFFFF" else "#0d0f12"}" opacity="0.04"/>']
        for i,(grade,svg) in enumerate(cells):
            cxp = (i%cols)*cw; cyp=(i//cols)*ch
            inner = svg.split('>',1)[1].rsplit('</svg>',1)[0]
            parts.append(f'<g transform="translate({cxp+11},{cyp+8}) scale({(cw-22)/VB:.4f})">{inner}</g>')
            parts.append(f'<text x="{cxp+cw/2:.0f}" y="{cyp+ch-14}" font-family="sans-serif" font-size="20" fill="#888" text-anchor="middle">{grade}</text>')
        chart = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {cols*cw} {rows*ch}" width="{cols*cw}" height="{rows*ch}">'
                 + "".join(parts) + '</svg>')
        open(os.path.join(OUT, "rank", fac+"_chart.svg"), "w", encoding="utf-8").write(chart)
        data = resvg_py.svg_to_bytes(svg_string=chart, width=cols*cw, height=rows*ch)
        open(os.path.join(OUT, "rank", fac+"_chart.png"), "wb").write(bytes(data))
        print("done:", fac)

if __name__ == "__main__":
    main()
