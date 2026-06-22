#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""勢力ノートに『名鑑（所属の人物・組織）』を自動生成・注入＝逆リンク強化。
人物/組織ノートの frontmatter を集計し、勢力→人物/組織/星系/会戦/回廊 への逆リンクを張る。
再実行可（AUTO:roster マーカー間を置換）。正は各ノート。"""
import os, glob, re

ROOT="/home/user/ginei-game/docs/lore"
PEOPLE=os.path.join(ROOT,"人物"); ORG=os.path.join(ROOT,"組織"); FAC=os.path.join(ROOT,"勢力")

FAC_ORDER=["碧晶公国","自由港同位体","黎明評議会","鉄環兵団","灯心自由市ファロス"]
CAP={"碧晶公国":"エベレスト星系","自由港同位体":"モンブラン星系","黎明評議会":"アコンカグア星系",
     "鉄環兵団":"マッターホルン星系","灯心自由市ファロス":"カンチェンジュンガ星系"}
FOUNDING={"碧晶公国":"アマテラス継承戦","自由港同位体":"ワタツミ港湾防衛戦","黎明評議会":"アコンカグア連邦結成戦",
          "鉄環兵団":"スサノオ攻略戦","灯心自由市ファロス":"ツクヨミ自由市防衛戦"}
CORRIDORS={"碧晶公国":["ジブラルタル回廊","ダーダネルス回廊","ドーバー回廊"],
           "自由港同位体":["ジブラルタル回廊","ボスポラス回廊","ホルムズ回廊"],
           "黎明評議会":["ボスポラス回廊","マラッカ回廊"],
           "鉄環兵団":["マラッカ回廊","ダーダネルス回廊"],
           "灯心自由市ファロス":["ホルムズ回廊","ドーバー回廊"]}
# 人物カテゴリ順（表示順）
PCAT=["君主","提督","文官","政治家","技術者","商人"]

def parse(path):
    txt=open(path,encoding="utf-8").read()
    m=re.match(r"^---\n(.*?)\n---",txt,re.S)
    if not m: return None
    fm={}
    for line in m.group(1).splitlines():
        mm=re.match(r"^([a-zA-Z_]+):\s*(.*?)\s*(#.*)?$",line)
        if mm:
            v=mm.group(2).strip()
            if v and not v.startswith("#"): fm[mm.group(1)]=v
    fm["_name"]=os.path.basename(path)[:-3]
    return fm

def category(fm):
    v=fm.get("vocation","")
    if v=="君主": return "君主"
    if v=="政治家": return "政治家"
    if v=="文官": return "文官"
    if v=="技術者": return "技術者"
    if v=="その他":
        cr=fm.get("civil_role","")
        if "商" in cr or "金融" in cr or "船主" in cr or "両替" in cr: return "商人"
        return "商人"
    return "提督"  # vocation 無し＝主要提督（武官）

# 収集
people={f:[] for f in FAC_ORDER}
for p in glob.glob(os.path.join(PEOPLE,"*.md")):
    if os.path.basename(p).startswith("_"): continue
    fm=parse(p)
    if fm and fm.get("faction") in people:
        people[fm["faction"]].append(fm)

orgs={f:{"政党":[],"家門":[],"企業":[]} for f in FAC_ORDER}
for p in glob.glob(os.path.join(ORG,"*.md")):
    if os.path.basename(p).startswith("_"): continue
    fm=parse(p)
    if fm and fm.get("faction") in orgs and fm.get("subtype") in orgs[fm["faction"]]:
        orgs[fm["faction"]][fm["subtype"]].append(fm)

def links(items): return "・".join(f"[[{x['_name']}]]" for x in sorted(items,key=lambda y:y.get("id","")))

for fac in FAC_ORDER:
    ppl=people[fac]
    bycat={c:[] for c in PCAT}
    for fm in ppl: bycat[category(fm)].append(fm)
    plines=[]
    for c in PCAT:
        if bycat[c]:
            plines.append(f"- **{c}**（{len(bycat[c])}）：{links(bycat[c])}")
    pblock="\n".join(plines)

    o=orgs[fac]
    org_lines=[]
    if o["政党"]: org_lines.append(f"- 政党（{len(o['政党'])}）：{links(o['政党'])}")
    if o["家門"]: org_lines.append(f"- 家門（{len(o['家門'])}）：{links(o['家門'])}")
    if o["企業"]: org_lines.append(f"- 商会・企業（{len(o['企業'])}）：{links(o['企業'])}")
    oblock="\n".join(org_lines) if org_lines else "- （なし）"

    cor="・".join(f"[[{c}]]" for c in CORRIDORS[fac])
    section=f"""<!-- AUTO:roster:start ＝ Tools/gen_faction_rosters.py が生成。手で編集しない -->
## 名鑑（所属の人物・組織）
> 各ノートの frontmatter から自動生成した逆リンク（正は各ノート）。人物計 {len(ppl)} 名。

### 中枢
- 首都星系：[[{CAP[fac]}]]
- 建国会戦：[[{FOUNDING[fac]}]]
- 接続回廊：{cor}
- 役職体系：[[{fac}の役職]]
- 軍編成：[[{fac}の軍編成]]

### 組織
{oblock}

### 人物（職分別）
{pblock}
<!-- AUTO:roster:end -->"""

    path=os.path.join(FAC,f"{fac}.md")
    txt=open(path,encoding="utf-8").read()
    if "<!-- AUTO:roster:start" in txt:
        txt=re.sub(r"<!-- AUTO:roster:start.*?AUTO:roster:end -->", section, txt, flags=re.S)
    else:
        # 「## 関連」の前に挿入（無ければ末尾）
        if "\n## 関連" in txt:
            txt=txt.replace("\n## 関連", "\n"+section+"\n\n## 関連", 1)
        else:
            txt=txt.rstrip()+"\n\n"+section+"\n"
    open(path,"w",encoding="utf-8").write(txt)
    print(f"{fac}: 人物{len(ppl)} 政党{len(o['政党'])} 家門{len(o['家門'])} 企業{len(o['企業'])}")
