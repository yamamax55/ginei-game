#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""組織の実ノートを生成（政党PTY/家門HOU/企業COM）。人物ノートの frontmatter から所属を拾い、
メンバーを相互リンクで列挙する。正＝runtime（PoliticsState/王朝シミュ/経済シミュ）。"""
import os, glob, re

ROOT="/home/user/ginei-game/docs/lore"
PEOPLE=os.path.join(ROOT,"人物")
ORG=os.path.join(ROOT,"組織")

FAC_ORDER=["碧晶公国","自由港同位体","黎明評議会","鉄環兵団","灯心自由市ファロス"]

# 政党の立場（綱領）
PARTY_STANCE={
    "正統派":"帝政の正統と現状の権威を擁護する保守本流",
    "門閥連合":"門閥貴族の特権と格式を守る既得権擁護",
    "宮廷改革派":"公領の近代化と官制改革を志す改革派",
    "自由貿易党":"交易の自由と規制撤廃を掲げる自由主義",
    "商権同盟":"商業ギルドの利権と商権を擁護する組織型",
    "中立堅持派":"武装中立の堅持を最優先する現実路線",
    "自治連合":"植民星の自治拡大を求める分権派",
    "進歩評議派":"反専制・改革を急ぐ進歩派",
    "連邦統一会":"連邦の結束強化を説く統一派",
    "主戦派":"積極的な征服と版図拡大を唱える強硬派",
    "兵団評議会":"軍団の合議と実力主義を重んじる主流派",
    "実利派":"理念より実利を採る是々非々の現実派",
    "市民自治党":"市民自治の拡充を掲げる自治派",
    "灯台同盟":"灯台利権と港湾防衛を基盤とする現業派",
    "中道市民連合":"中道・現実路線で市政を回す穏健派",
}
FAC_PARTY_ORDER={
    "碧晶公国":["正統派","門閥連合","宮廷改革派"],
    "自由港同位体":["自由貿易党","商権同盟","中立堅持派"],
    "黎明評議会":["自治連合","進歩評議派","連邦統一会"],
    "鉄環兵団":["主戦派","兵団評議会","実利派"],
    "灯心自由市ファロス":["市民自治党","灯台同盟","中道市民連合"],
}
FAC_CAP={"碧晶公国":"アマテラス（エベレスト星系）","自由港同位体":"ワタツミ（モンブラン星系）",
         "黎明評議会":"オオクニヌシ（アコンカグア星系）","鉄環兵団":"スサノオ（マッターホルン星系）",
         "灯心自由市ファロス":"ツクヨミ（カンチェンジュンガ星系）"}

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

people=[]
for p in glob.glob(os.path.join(PEOPLE,"*.md")):
    b=os.path.basename(p)
    if b.startswith("_"): continue
    fm=parse(p)
    if fm: people.append(fm)

rows_pty=[]; rows_hou=[]; rows_com=[]
pty=1; hou=1; com=1

# ---------- 政党（PTY） ----------
for fac in FAC_ORDER:
    for party in FAC_PARTY_ORDER[fac]:
        members=[x for x in people if x.get("faction")==fac and x.get("vocation")=="政治家" and x.get("party")==party]
        if not members: continue
        members.sort(key=lambda x:x.get("id",""))
        stance=PARTY_STANCE.get(party,"")
        # 党首＝党首 faction_role 優先、なければ最上位公職
        leaders=[m for m in members if m.get("faction_role")=="党首"]
        head=leaders[0] if leaders else members[0]
        mlines="\n".join(f"- [[{m['_name']}]]（{m.get('id','')}・{m.get('faction_role','')}／{m.get('office','')}）" for m in members)
        body=f"""---
type: 組織
subtype: 政党
id: PTY-{pty:03d}
faction: {fac}
ideology: {stance}
leader: {head['_name']}
status: {('与党' if party in ('正統派','兵団評議会','中道市民連合','自治連合','自由貿易党') else '野党')}
tags: [組織, 政党, {fac}]
---

# {party}

> {fac}の政党。{stance}。

## 概要
[[{fac}]]の政党。**{stance}**を掲げ、{fac}の国政で{('与党として政権を担う' if party in ('正統派','兵団評議会','中道市民連合','自治連合','自由貿易党') else '野党として論陣を張る')}。党首は[[{head['_name']}]]。

## 綱領・主張
- {stance}。国家理念「{('—')}」との距離をめぐり、他党と論戦を交わす。

## 所属議員（人物ノートより）
{mlines}

## 関係
- 所属勢力：[[{fac}]]
- 党首：[[{head['_name']}]]

## ゲームデータ参照（正は runtime 側＝`PoliticsState`/`Party`）
| 項目 | 値 |
|---|---|
| 立場（`ideology`） | {stance} |
| 党首（`PartyRules.RulingParty`＝与党は首班） | [[{head['_name']}]] |
| 所属議員数 | {len(members)}（lore 上） |
| 支持率／議席 | runtime（`PartyRules`・選挙 `ElectionRules`） |

## 物語上の役割
- {fac}の世論・予算・講和を握る政治の母体。政権交代・分極化（`PartySystemRules`）が盤面を動かす。

---
[[星骸の諸侯]]（総覧へ）
"""
        open(os.path.join(ORG,f"{party}.md"),"w",encoding="utf-8").write(body)
        rows_pty.append(f"| PTY-{pty:03d} | [[{party}]] | [[{fac}]] | {len(members)}名 |")
        pty+=1

# ---------- 家門（HOU） ----------
houses={}
for x in people:
    if x.get("vocation")=="君主" and x.get("dynasty"):
        houses.setdefault(x["dynasty"],[]).append(x)
for house in sorted(houses, key=lambda h:(-len(houses[h]),h)):
    members=sorted(houses[house], key=lambda x:x.get("id",""))
    fac=members[0].get("faction","碧晶公国")
    # 当主＝reigning（大公/女大公）かつ世数>0、なければ世数最大
    def reign_key(m):
        t=m.get("title",""); rn=int(m.get("regnal_number","0") or 0)
        return (1 if t in ("大公","女大公") and rn>0 else 0, rn)
    head=max(members,key=reign_key)
    mlines="\n".join(f"- [[{m['_name']}]]（{m.get('id','')}・{m.get('title','')}{('・'+m['regnal_number']+'世' if m.get('regnal_number','0') not in ('0','') else '')}）" for m in members)
    body=f"""---
type: 組織
subtype: 家門
id: HOU-{hou:03d}
faction: {fac}
house_type: 門閥貴族（公領の世襲家）
head: {head['_name']}
status: 存続
tags: [組織, 家門, {fac}]
---

# {house}

> [[{fac}]]の門閥。公領の世襲を担う名家のひとつ。

## 概要
[[{fac}]]に連なる**門閥貴族の家門**。旧連合貴族の流れを汲み、公領の格式ある世襲家として政務・軍務に人を出す。現当主は[[{head['_name']}]]。

## 家史・家風
- 旧[[大環状連合]]の貴族連合に出自を持ち、[[碧晶公国]]の「正統なる権威」を支える側に立つ。正統性と徳が家運を左右する（`DynastyRules`）。

## 主要人物（人物ノートより）
{mlines}

## 関係
- 仕える勢力：[[{fac}]]
- 当主：[[{head['_name']}]]

## ゲームデータ参照（正は runtime 側＝`Person`/王朝シミュ）
| 項目 | 値 |
|---|---|
| 種別（`house_type`） | 門閥貴族（世襲家） |
| 当主（`SuccessionRules`） | [[{head['_name']}]] |
| 出自の格（`SocialOrigin`） | 門閥貴族 |
| 所属人物数 | {len(members)}（lore 上・血縁グラフで解決） |

## 物語上の役割
- 君主・貴族の母体。継承・閨閥・お家騒動が[[{fac}]]の正統性と政争を動かす。

---
[[星骸の諸侯]]（総覧へ）
"""
    open(os.path.join(ORG,f"{house}.md"),"w",encoding="utf-8").write(body)
    rows_hou.append(f"| HOU-{hou:03d} | [[{house}]] | [[{fac}]] | {len(members)}名 |")
    hou+=1

# ---------- 企業・商会（COM） ----------
def sector_of(biz):
    if any(s in biz for s in ["銀行","両替","金融","財団"]): return "金融（銀行・両替）","Bank/BankRules"
    if any(s in biz for s in ["海運","船団"]): return "海運","ShippingCompany"
    return "総合商社・交易","TradingHouse/Enterprise"

used_com=set()
merchants=[x for x in people if x.get("vocation")=="その他" and x.get("business")]
merchants.sort(key=lambda x:x.get("id",""))
for m in merchants:
    biz=m["business"]; fname=biz
    if fname in used_com:
        fname=f"{biz}（{m['_name'].split('・')[0]}）"
    used_com.add(fname)
    fac=m.get("faction",""); sector,impl=sector_of(biz)
    tier=m.get("wealth_tier",""); ft=m.get("financial_trait","")
    role=m.get("civil_role","商人")
    listed = "true（上場・株式#185）" if tier in ("豪商","富裕") else "false（非上場）"
    body=f"""---
type: 組織
subtype: 企業
id: COM-{com:03d}
faction: {fac}
sector: {sector}
ownership: 私有
owner: {m['_name']}
listed: {'true' if tier in ('豪商','富裕') else 'false'}
status: 営業
tags: [組織, 企業, {fac}]
---

# {biz}

> [[{fac}]]の{sector}。{role}[[{m['_name']}]]が率いる。

## 概要
[[{fac}]]を本拠とする**{sector}**。{role}の[[{m['_name']}]]が興した商会で、財力は**{tier}**と目される。{('港と航路を回し' if '海運' in sector else '預金と貸出で信用を回し' if '金融' in sector else '交易と相場で身代を回し')}、{fac}の経済を裏から支える。

## 事業・経営
- 主力＝{sector}。財産は**{ft}**で運用。利潤は法人税ぶん国庫へ（私有企業）。

## 関係
- 本拠勢力：[[{fac}]]
- 主・創業者：[[{m['_name']}]]（{role}）

## ゲームデータ参照（正は runtime 側＝経済シミュ）
| 項目 | 値 |
|---|---|
| 業種（`sector`） | {sector}（{impl}） |
| 所有形態 | 私有（`Ownership.私有`） |
| 主・持分（`NamedAsset` 企業） | [[{m['_name']}]] |
| 財力（目安） | {tier}（正は runtime の `wealth`／NetWorthRules） |
| 上場（`Listing`・株式#185） | {listed} |

## 物語上の役割
- 補給・建艦・戦費を金で動かす経済の一翼。{fac}の戦争遂行を商域から支える。

---
[[星骸の諸侯]]（総覧へ）
"""
    open(os.path.join(ORG,f"{fname}.md"),"w",encoding="utf-8").write(body)
    rows_com.append(f"| COM-{com:03d} | [[{fname}]] | [[{fac}]] | {sector} |")
    com+=1

os.makedirs(ORG,exist_ok=True)
open("/tmp/org_pty.txt","w",encoding="utf-8").write("\n".join(rows_pty))
open("/tmp/org_hou.txt","w",encoding="utf-8").write("\n".join(rows_hou))
open("/tmp/org_com.txt","w",encoding="utf-8").write("\n".join(rows_com))
print(f"政党 {pty-1} / 家門 {hou-1} / 企業 {com-1} 生成")
