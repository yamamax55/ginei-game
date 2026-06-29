#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""商人 lore を50人分生成（_商人テンプレート.md 準拠・諸侯5勢力に10人ずつ）。
商人は PersonVocation 外＝その他(民間/在野)。財産・所有資産・事業を経済モデルにひも付ける。
史実 archetype は単一割当台帳を侵さないよう空。"""
import os, glob, random

ROOT = "/home/user/ginei-game/docs/lore"
PEOPLE = os.path.join(ROOT, "人物")
START_ID = 263  # PER-263 から

NAMES = {
    "german": {
        "m": ["ハインリヒ","フリードリヒ","ヴィルヘルム","オットー","ルートヴィヒ","カール","エルンスト","ヘルムート","グスタフ","アルブレヒト",
              "ヴェルナー","クラウス","ヨアヒム","ディーター","ゲオルク","アントン","フェルディナント","ブルーノ","マクシミリアン","ラインホルト",
              "ウルリヒ","ゴットフリート","ヴァルター","ロタール","ジギスムント","オスカー","エーリヒ","クルト","フランツ","ハンス"],
        "f": ["ヒルデガルト","イングリット","ザビーネ","ブリギッテ","ヘートヴィヒ","クリスタ","エルフリーデ","ゲルトルート","ヨハンナ","マルガレーテ",
              "ローゼマリー","アンネリーゼ","ゾフィー","エディト","ベアトリクス","クラーラ","レギーナ","フランツィスカ","テオドラ","アマーリエ"],
        "s": ["ローテンブルク","ファルケンベルク","ヴァイセンフェルス","グリュンヴァルト","アイヒホルン","ブランデンシュタイン","リンデンベルク","シュヴァルツェンベルク",
              "モルゲンシュテルン","ヴィンターフェルト","ホーエンタール","アイゼンハルト","ローゼンクランツ","シュタールベルク","ノイハウス","ヴァッセルマン",
              "デューリンク","クレーフェルト","アーレンスドルフ","ビルケンフェルト","ドルンブルク","ヘルムスドルフ","ライヒェナウ","ザールフェルト","クヴェルフルト",
              "ヴァインガルト","ゴルトシュミット","ジルバーマン","ハンデルスマン","コフマン"],
        "particle": "フォン",
    },
    "italian": {
        "m": ["ロレンツォ","ジュリオ","エンツォ","マルコ","アレッサンドロ","ニコロ","ダンテ","レオナルド","フェデリコ","ステファノ",
              "ジョヴァンニ","パオロ","ヴィットリオ","エミリオ","ルカ","アンドレア","グリエルモ","ベルナルド","チェーザレ","オラツィオ",
              "ジャコモ","ドメニコ","リッカルド","サルヴァトーレ","トンマーゾ"],
        "f": ["ビアンカ","ジュリア","フランチェスカ","イザベラ","ルチア","ヴァレンティナ","シモネッタ","アンジェラ","ロザリア","ベアトリーチェ",
              "カテリーナ","ヴィオラ","エレオノーラ","フィオレンツァ","ジネヴラ","ルクレツィア","アドリアーナ","セラフィーナ","オリンピア","カミラ"],
        "s": ["モンテヴェルディ","コロンナ","ファルネーゼ","ヴィスコンティ","グリマルディ","バルバリーゴ","コンタリーニ","モチェニーゴ","ダンドロ","ベンボ",
              "マラテスタ","ゴンザーガ","ピッコローミニ","サンセヴェリーノ","カッラーラ","オルシーニ","フォスカリ","ロレダン","ティエポロ",
              "グリッティ","ヴェニエール","ソランツォ","モリーニ","ザッカリア","スピノラ","ドーリア","フィエスキ","フレゴーゾ","アドルノ"],
        "particle": "",
    },
    "english": {
        "m": ["エドガー","ギルバート","ハロルド","ローレンス","セシル","ハワード","クライヴ","デズモンド","ネヴィル","アシュリー",
              "ゴドフリー","アルフレッド","レジナルド","バーナビー","ウィルフレッド","エドモンド","ハンフリー","アンブローズ","セオドア","ロデリック",
              "オズワルド","パーシヴァル","モンタギュー","クエンティン","エルロイ"],
        "f": ["エレノア","ベアトリス","ロザリンド","ヴィヴィアン","コーデリア","イモージェン","ウィニフレッド","オードリー","ガートルード","フェリシティ",
              "ハリエット","ジェマイマ","ロウィーナ","シビル","タムシン","ブライス","アラミンタ","クレメンタイン","マーセラ","ペネロペ"],
        "s": ["ホイットモア","ペンブルック","カヴェンディッシュ","アシュビー","ラングフォード","モーティマー","フェアファクス","ハルワース","ウェストブルック","ソーンベリー",
              "アバークロンビー","ブラックウッド","カーマイケル","ダンモア","エルズワース","フェザストン","グレンヴィル","ヘイズルウッド","キンケイド","ロックハート",
              "マーチバンクス","ノースコート","オークハースト","レイヴンズクロフト","フェアウェザー","ホールデン","ウィッカム","ブロードハースト","シェルドレイク","アトウォーター"],
        "particle": "",
    },
    "french": {
        "m": ["アンリ","ジャン","フィリップ","エミール","ガスパール","ティエリ","オリヴィエ","レオン","マルセル","ベルトラン",
              "ギヨーム","ジェローム","パスカル","エドゥアール","アルマン","ローラン","ヴァンサン","ユーグ","バティスト","セバスティアン",
              "グザヴィエ","ティボー","レミ","アドリアン","モーリス"],
        "f": ["アデル","マルゴ","エロイーズ","ジュヌヴィエーヴ","ソランジュ","オデット","マチルド","シルヴィ","コゼット","ブランシュ",
              "アガット","ヴィオレーヌ","クロチルド","フロランス","ミレーヌ","ノエミ","ローズ","セシル","アンリエット","マドレーヌ"],
        "s": ["デュフォール","ラフィット","ボーモン","ルメートル","ドラクロワ","フォンテーヌ","ロシュフォール","モンフォール","ヴァランクール","ベルフォール",
              "ダルジャンソン","クレルモン","ボーフレモン","シャストネ","ヴィルヌーヴ","グランヴィル","セニュレー","モンモランシー","ラ・ロシュ","デュボワ",
              "ラフォンテーヌ","ベルナール","コルベール","ヴェルニエ","ダンジュー","ロクロワ","メルキュール","フォンタネル","ボードリー","シャンソー"],
        "particle": "ド・",
    },
}

FACTIONS = [
    {"name":"碧晶公国","culture":"german","capital":"アマテラス","system":"エベレスト星系",
     "ideology":"正統なる権威による統一の再建","noble_ratio":0.25,
     "flavor":"古い公領の宮廷と門閥に食い込む御用商人が幅を利かせる"},
    {"name":"自由港同位体","culture":"italian","capital":"ワタツミ","system":"モンブラン星系",
     "ideology":"交易の自由・武装中立","noble_ratio":0.08,
     "flavor":"商業ギルド連合そのもの＝銀河有数の自由市場に豪商がひしめく"},
    {"name":"黎明評議会","culture":"english","capital":"オオクニヌシ","system":"アコンカグア星系",
     "ideology":"自治と合意・反専制","noble_ratio":0.04,
     "flavor":"植民星の開拓と物流を担う新興の交易商が台頭する"},
    {"name":"鉄環兵団","culture":"german","capital":"スサノオ","system":"マッターホルン星系",
     "ideology":"実力主義の征服","noble_ratio":0.02,
     "flavor":"傭兵軍閥の兵站と戦費を握る死の商人・調達商が暗躍する"},
    {"name":"灯心自由市ファロス","culture":"french","capital":"ツクヨミ","system":"カンチェンジュンガ星系",
     "ideology":"武装中立・市民自治（都市国家）","noble_ratio":0.04,
     "flavor":"港湾都市の交易と金融を回す市民商人が市政を支える"},
]

ROLE_W=[("商人",5),("商会主",4),("大商人",3),("仲買人",2),("船主",2),("金融家",2),("両替商",1),("豪商",1)]
def wpick(pairs):
    bag=[];
    for v,w in pairs: bag+=[v]*w
    return random.choice(bag)

BIZ_SUFFIX=["商会","商会","商事","通商","交易商会","海運商会","貿易商会"]
FIN_SUFFIX=["銀行","financier→財団","両替商会","金融商会"]
WEALTH_W=[("豪商",2),("富裕",4),("中堅",4),("新興",3)]
WEALTH_RANGE={"豪商":(800,2200),"富裕":(320,800),"中堅":(110,320),"新興":(30,120)}

YOUSHI_M=["上等な交易商の装いに如才ない笑み","羽振りのよい恰幅、指輪が光る","質素な身なりの倹約家、目だけが鋭い","若く精力的な新興商人の面構え","老獪で貫禄ある商家の主の風格"]
YOUSHI_F=["華やかな装いと抜け目ない眼差し","落ち着いた商家の女主人の佇まい","質素ながら品のよい身なり","若く野心的な女商人の面構え","貫禄ある女傑、宝飾を抑えめに"]
SEIKAKU=["堅実第一の倹約家で、確実な利しか追わない。","山師気質で、危険な投機に身代を賭ける。","義商を任じ、信用と人望を何より重んじる。","強欲で、買い占めも暴利も辞さない。","革新的な商法で新市場を切り開く野心家。","政商として要人に取り入り、利権を漁る。"]
RAIREKI=["行商から身を起こし","商家の{n}代目として","一代で元手を築き","植民星の交易で頭角を現し","船一隻から始めて"]
ASSET_HINTS=["旗艦級の豪邸","美術品の蒐集","複数の商船団","採掘権益","工廠の持分","名のある宝物"]

random.seed(20260624)

used=set()
for p in glob.glob(os.path.join(PEOPLE,"*.md")):
    b=os.path.basename(p)[:-3]
    if not b.startswith("_"): used.add(b)

def uniq_name(cul, sex, noble_ratio):
    pool=NAMES[cul]
    for _ in range(300):
        given=random.choice(pool["f"] if sex=="女性" else pool["m"])
        surname=random.choice(pool["s"])
        is_noble=random.random()<noble_ratio
        particle=pool["particle"] if (is_noble and pool["particle"]) else ""
        if particle=="ド・": full=f"{given}・{particle}{surname}"
        elif particle: full=f"{given}・{particle}・{surname}"
        else: full=f"{given}・{surname}"
        if full not in used:
            used.add(full); return given,surname,particle,is_noble,full
    raise RuntimeError("exhausted")

rows=[]; pid=START_ID
for fac in FACTIONS:
    cul=fac["culture"]
    for _ in range(10):
        sex="女性" if random.random()<0.34 else "男性"
        given,surname,particle,is_noble,full = uniq_name(cul,sex,fac["noble_ratio"])
        role=wpick(ROLE_W)
        if role in ("金融家","両替商"):
            biz=f"{surname}{random.choice(['銀行','両替商会','金融商会','財団'])}"
        elif role=="船主":
            biz=f"{surname}{random.choice(['海運','海運商会','船団'])}"
        else:
            biz=f"{surname}{random.choice(BIZ_SUFFIX)}"
        ftrait=wpick([("投資",5),("貯金",4),("浪費",2)])
        wtier=wpick(WEALTH_W)
        lo,hi=WEALTH_RANGE[wtier]; wealth=random.randint(lo,hi)
        age=random.randint(30,66)
        operation=random.randint(60,93); intelligence=random.randint(60,94)
        charisma=random.randint(52,93); leadership=random.randint(36,70)
        attack=random.randint(12,40); defense=random.randint(15,45); mobility=random.randint(15,45)
        humility=random.randint(22,72); virtue=random.randint(22,80); ambition=random.randint(50,95)
        # 商人は自由交易寄り＝能力主義/共和/中道、政商は門閥寄り
        if is_noble:
            creed=random.choice(["門閥主義","帝政擁護","中道"]); origin="門閥貴族"
        else:
            creed=random.choice(["能力主義","共和主義","中道","能力主義"]); origin=random.choice(["平民","植民星","平民"])
        youshi=random.choice(YOUSHI_F if sex=="女性" else YOUSHI_M)
        seikaku=random.choice(SEIKAKU)
        raireki=random.choice(RAIREKI).format(n=random.choice(["二","三","四","五"]))
        a1=random.choice(ASSET_HINTS); a2=random.choice([a for a in ASSET_HINTS if a!=a1])
        infamy = random.randint(10,55) if "強欲" in seikaku or "政商" in seikaku or "買い占め" in seikaku else random.randint(0,25)
        popr = random.randint(20,70) if "義商" in seikaku else random.randint(0,35)
        hobby=random.choice(["蒐集","美食","蒐集","美食","茶","音楽","読書","なし"])
        vice=random.choice(["なし","賭博","吝嗇","好色","大酒","吝嗇","なし"])

        body=f"""---
type: 人物
id: PER-{pid:03d}
faction: {fac['name']}
sex: {sex}
vocation: その他
civil_role: {role}
business: {biz}
financial_trait: {ftrait}
wealth_tier: {wtier}
role: {role}
status: 存命
age_start: {age}
former_name:
asset:
portrait:
archetype:
tags: [人物, {fac['name']}, 商人]
---

# {full}

> {fac['name']}の{role}。{biz}を率いる。

## 概要
[[{fac['name']}]]の**{role}**。{age}歳。{biz}を営み、{fac['flavor']}なかで身代を築く。財を以て補給・建艦・戦費を裏から動かす「銭」の層の一人。

## 容姿
{youshi}。{age}歳。

## 来歴
- {raireki}、{biz}を興した。{('門閥に連なり御用商人として要人に食い込む。' if is_noble else '自由交易の波に乗って身代を広げた。')}
- 現在は**{wtier}**と目される財力を持ち、財産は**{ftrait}**で回す。

## 性格・信条
- {seikaku}
- 信条は**{creed}**。{fac['name']}の理念「{fac['ideology']}」とは、利の向く先で結びもし離れもする。

## 関係
- 本拠：[[{fac['name']}]]
- 事業：{biz}

## ゲームデータ参照（正は runtime 側＝Person / 経済シミュ）

| 項目 | 値 |
|---|---|
| 性別（`sex`） | {sex} |
| 初期年齢 | {age} |
| 職分（`PersonVocation`） | その他（民間・在野層） |
| 民間の肩書き | {role} |
| 営む事業 | {biz}（TradingHouse／Enterprise 私有） |
| 財産行動（`financialTrait`） | {ftrait} |
| 財産規模（目安） | {wtier}（概算 {wealth}・正は runtime の `wealth`／NetWorthRules） |

### 所有資産（`NamedAsset` ほか・NetWorthRules が集計／観測 Alt+W）
| 種別 | 内容 |
|---|---|
| 邸宅 | {fac['capital']}の{a1} |
| 事業・権益 | {a2} |
| 金融資産 | 株式・債券（{ftrait}で運用） |

### 能力値（商人は **商才＝運営(経営)・情報(商機)** と **人望(交渉)** が核）
| 統率 | 運営（経営） | 情報（商機） | 人望（charisma） | 攻撃 | 防御 | 機動 | 謙虚 | 功名心 | 徳 |
|---|---|---|---|---|---|---|---|---|---|
| {leadership} | **{operation}** | **{intelligence}** | **{charisma}** | {attack} | {defense} | {mobility} | {humility} | {ambition} | {virtue} |

### 氏名構成（構造化命名）
| givenName | familyName | nobleParticle | callName |
|---|---|---|---|
| {given} | {surname} | {particle.rstrip('・') if particle else '―'} | {given} |

### 出自・信条・人となり（PER-PROPS）
| 項目 | 値 |
|---|---|
| 信条（`creed`） | {creed} |
| 出自・階層（`socialOrigin`） | {origin} |
| 出身地（`birthPlace`） | {fac['capital']}（{fac['system']}） |
| 人望（`charisma`） | {charisma} |
| 趣味（`hobby`） | {hobby} |
| 悪癖（`vice`） | {vice} |
| 民望（`popularRenown`） | {popr} |
| 悪名（`infamy`） | {infamy} |

## 物語上の役割
- 武の提督・文の官僚に対する「銭」の層。[[回廊|交易路]]と相場を握り、戦費と兵站を金で左右して[[{fac['name']}]]の戦争の前提条件を裏から動かす。

---
[[星骸の諸侯]]（総覧へ）
"""
        with open(os.path.join(PEOPLE,f"{full}.md"),"w",encoding="utf-8") as fh:
            fh.write(body)
        rows.append(f"| PER-{pid:03d} | [[{full}]] | [[{fac['name']}]] |")
        pid+=1

print(f"generated {pid-START_ID} merchant notes. next id PER-{pid:03d}")
open("/tmp/merchant_rows.txt","w",encoding="utf-8").write("\n".join(rows))
open("/tmp/merchant_next.txt","w",encoding="utf-8").write(f"PER-{pid:03d}")
