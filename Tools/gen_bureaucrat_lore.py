#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""官僚 lore ノートを 100 人分生成（_官僚テンプレート.md 準拠・諸侯5勢力に20人ずつ）。
正＝ゲームデータの原則に従い、frontmatter は二官八省・位階(CourtRank)・官職(Office)を使う。
史実 archetype は単一割当台帳(史実人物録)を侵さないよう空のまま（支援キャラ）。"""
import os, random, re

ROOT = "/home/user/ginei-game/docs/lore"
PEOPLE = os.path.join(ROOT, "人物")

START_ID = 13  # PER-013 から

# ---- 文化別の名前プール ----
NAMES = {
    "german": {
        "m": ["コンラート2","ハインリヒ","フリードリヒ","ヴィルヘルム","オットー","ルートヴィヒ","カール","エルンスト","ヘルムート","グスタフ",
              "アルブレヒト","ヴェルナー","クラウス","ヨアヒム","ラルフ","ディーター","ゲオルク","アントン","フェルディナント","ブルーノ",
              "マクシミリアン","ラインホルト","ウルリヒ","ゴットフリート","ヴァルター"],
        "f": ["ヒルデガルト","イングリット","ザビーネ","ブリギッテ","ヘートヴィヒ","クリスタ","エルフリーデ","ゲルトルート","ヨハンナ","マルガレーテ",
              "ローゼマリー","アンネリーゼ","ヴィルヘルミーネ","ゾフィー","エディト","ベアトリクス","クラーラ","レギーナ","フランツィスカ","テオドラ"],
        "s": ["ローテンブルク","ファルケンベルク","ヴァイセンフェルス","グリュンヴァルト","アイヒホルン","ブランデンシュタイン","リンデンベルク","シュヴァルツェンベルク",
              "モルゲンシュテルン","ヴィンターフェルト","ホーエンタール","アイゼンハルト","ローゼンクランツ","シュタールベルク","ノイハウス","ヴァッセルマン",
              "デューリンク","クレーフェルト","アーレンスドルフ","ビルケンフェルト","ドルンブルク","ヘルムスドルフ","ライヒェナウ","ザールフェルト","クヴェルフルト"],
        "particle": "フォン",
    },
    "italian": {
        "m": ["ロレンツォ","ジュリオ","エンツォ","マルコ","アレッサンドロ","ニコロ","ダンテ","レオナルド","フェデリコ","ステファノ",
              "ジョヴァンニ","パオロ","ヴィットリオ","エミリオ","ルカ","アンドレア","グリエルモ","ベルナルド","チェーザレ","オラツィオ"],
        "f": ["ビアンカ","ジュリア","フランチェスカ","イザベラ","ルチア","ヴァレンティナ","シモネッタ","アンジェラ","ロザリア","ベアトリーチェ",
              "カテリーナ","ヴィオラ","エレオノーラ","フィオレンツァ","ジネヴラ","ルクレツィア","アドリアーナ","セラフィーナ","オリンピア","カミラ"],
        "s": ["モンテヴェルディ","コロンナ","ファルネーゼ","ヴィスコンティ","グリマルディ","バルバリーゴ","コンタリーニ","モチェニーゴ","ダンドロ","ベンボ",
              "マラテスタ","ゴンザーガ","デッラ・スカラ","ピッコローミニ","サンセヴェリーノ","カッラーラ","オルシーニ","フォスカリ","ロレダン","ティエポロ",
              "グリッティ","ヴェニエール","ソランツォ","モリーニ","ザッカリア"],
        "particle": "",
    },
    "english": {
        "m": ["エドガー","ギルバート","ハロルド","ローレンス","セシル","ハワード","クライヴ","デズモンド","ネヴィル","アシュリー",
              "ゴドフリー","アルフレッド","レジナルド","バーナビー","ウィルフレッド","エドモンド","ハンフリー","アンブローズ","セオドア","ロデリック"],
        "f": ["エレノア","ベアトリス","ロザリンド","ヴィヴィアン","コーデリア","イモージェン","ウィニフレッド","オードリー","ガートルード","フェリシティ",
              "ハリエット","ジェマイマ","ペトゥラ","ロウィーナ","シビル","タムシン","ブライス","アラミンタ","クレメンタイン","マーセラ"],
        "s": ["ホイットモア","ペンブルック","カヴェンディッシュ","アシュビー","ラングフォード","モーティマー","フェアファクス","ハルワース","ウェストブルック","ソーンベリー",
              "アバークロンビー","ブラックウッド","カーマイケル","ダンモア","エルズワース","フェザストン","グレンヴィル","ヘイズルウッド","キンケイド","ロックハート",
              "マーチバンクス","ノースコート","オークハースト","プリンスリー","レイヴンズクロフト"],
        "particle": "",
    },
    "french": {
        "m": ["アンリ","ジャン","フィリップ","エミール","ガスパール","ティエリ","オリヴィエ","レオン","マルセル","ベルトラン",
              "ギヨーム","ジェローム","パスカル","エドゥアール","アルマン","ローラン","ヴァンサン","ユーグ","バティスト","セバスティアン"],
        "f": ["アデル","マルゴ","エロイーズ","ジュヌヴィエーヴ","ソランジュ","オデット","マチルド","シルヴィ","コゼット","ブランシュ",
              "アガット","ヴィオレーヌ","クロチルド","フロランス","レアンドル","ミレーヌ","ノエミ","ローズ","セシル","アンリエット"],
        "s": ["デュフォール","ラフィット","ボーモン","ルメートル","ドラクロワ","フォンテーヌ","ロシュフォール","モンフォール","ヴァランクール","ベルフォール",
              "ダルジャンソン","クレルモン","ラ・トゥール","ボーフレモン","シャストネ","モンテスパン","ヴィルヌーヴ","ロアン","グランヴィル","セニュレー"],
        "particle": "ド・",
    },
}

# ---- 勢力定義 ----
FACTIONS = [
    {"name":"碧晶公国","fac_id":"FAC-001","culture":"german","capital":"アマテラス","system":"エベレスト星系",
     "ideology":"正統なる権威による統一の再建","creeds":["帝政擁護","門閥主義","中道"],"origins":["門閥貴族","下級貴族","下級貴族"],"noble_ratio":0.6,
     "flavor":"旧連合貴族の世襲公領。古い官制と門閥の格式が政務を律する"},
    {"name":"自由港同位体","fac_id":"FAC-002","culture":"italian","capital":"ワタツミ","system":"モンブラン星系",
     "ideology":"交易の自由・武装中立","creeds":["能力主義","共和主義","中道"],"origins":["平民","植民星","平民"],"noble_ratio":0.1,
     "flavor":"商業ギルド連合。帳簿と算盤が物を言う実務本位の官房"},
    {"name":"黎明評議会","fac_id":"FAC-003","culture":"english","capital":"オオクニヌシ","system":"アコンカグア星系",
     "ideology":"自治と合意・反専制","creeds":["共和主義","能力主義","中道"],"origins":["平民","植民星","平民"],"noble_ratio":0.05,
     "flavor":"植民星の民選議会連邦。合議と手続きを重んじる文官機構"},
    {"name":"鉄環兵団","fac_id":"FAC-004","culture":"german","capital":"スサノオ","system":"マッターホルン星系",
     "ideology":"実力主義の征服","creeds":["能力主義","能力主義","中道"],"origins":["平民","軍人家系","植民星"],"noble_ratio":0.0,
     "flavor":"傭兵あがりの軍閥。兵站と金庫を握る者が実権を持つ叩き上げの官房"},
    {"name":"灯心自由市ファロス","fac_id":"FAC-005","culture":"french","capital":"ツクヨミ","system":"カンチェンジュンガ星系",
     "ideology":"武装中立・市民自治（都市国家）","creeds":["共和主義","中道","能力主義"],"origins":["平民","平民","植民星"],"noble_ratio":0.05,
     "flavor":"都市国家。市民自治のもと、港と財庫を回す実務官僚が屋台骨"},
]

# ---- 省庁・官職・位階（官位相当でひも付け） ----
MINISTRIES = {
    "太政官": ("内政・統括","宰相府"),
    "式部省": ("人事・選叙","式部"),
    "民部省": ("内政・戸籍","民部"),
    "大蔵省": ("財政・出納","大蔵"),
    "兵部省": ("軍政・兵站","兵部"),
}
# 官職テンプレ：(役職名候補, 位階レンジ, 官職カテゴリ)
GRADES = [
    # 最上位：宰相・公卿
    {"offices":["宰相"],"ranks":["従三位","正四位上","正四位下"],"weight":1,"tier":"宰相"},
    {"offices":["{m}卿"],"ranks":["正四位下","従四位上","従四位下"],"weight":2,"tier":"卿"},
    {"offices":["{cap}総督","{sys}総督"],"ranks":["正五位上","正五位下","従五位上"],"weight":2,"tier":"総督"},
    {"offices":["{stem}大輔","{stem}少輔"],"ranks":["従五位下","正六位上"],"weight":3,"tier":"次官"},
    {"offices":["{stem}大丞","{stem}少丞","{stem}大録"],"ranks":["正六位上","正六位下","従六位上","従六位下"],"weight":6,"tier":"判官"},
]

CALL_NIN = ["清廉の士","数字に強い能吏","古格の番人","合議の調停者","物静かな実務家","峻烈な改革者","老練の調整役","几帳面な記録官",
            "弁の立つ官房長","堅実な金庫番","人事を握る男","人事を握る才女","現場主義の官吏","机上で会戦の勝敗を分ける男","条文の鬼"]

GAISHO_PRE = ["", "", "", "", "「賢相」", "「鉄面皮」", "「算盤侍」", "「夜の太政官」", "「赤筆」", "「能吏」"]

YOUSHI_M = ["細身で姿勢正しく、感情を表に出さない","角張った顔に鋭い目、官服を隙なく着こなす","白髪まじりの実直な風貌、書類の束を手放さない",
            "長身痩躯で物腰は柔らかいが眼光は鋭い","小柄ながら声に重みがあり、人を動かす","若く野心的な面構え、仕立ての良い文官正装"]
YOUSHI_F = ["落ち着いた佇まいに理知的な眼差し","髪をきっちり結い上げ、隙のない官服姿","柔和な笑みの下に切れ者の気配を漂わせる",
            "背筋の伸びた長身、簡素だが上質な正装","小柄で快活、しかし数字の前では容赦がない","冷静沈着、感情を読ませない静かな美貌"]

SEIKAKU = ["清廉潔白で賄賂を憎み、規定を盾に権門とも渡り合う。","事なかれを嫌い、必要とあれば上官にも諫言する。",
           "数字と前例に忠実な実務家。情よりも帳尻を重んじる。","温厚篤実で人望に厚く、省内の対立を和らげる調整役。",
           "野心的で出世に貪欲だが、仕事の精度は高い。","旧来の格式を重んじる保守派。改革には慎重を期す。",
           "改革志向で硬直した官制に苛立ち、新制度を持ち込もうとする。","慎重で口数少なく、機を見て確実に動く。"]

RAIREKI_EDU = ["大学を{rank_grad}で卒え","科挙の殿試に{rank_grad}で及第し進士となり","高官の推挙（蔭位）を得て","地方の書記から叩き上げ","名門の家塾に学んだのち"]
RAIREKI_POST = ["{ministry}に任官した。","{ministry}に配され頭角を現した。","下級官から累進して{ministry}に入った。","若くして{ministry}に抜擢された。"]

random.seed(20260622)

def grade_name(g): return g

def pick_grade():
    bag=[]
    for g in GRADES:
        bag += [g]*g["weight"]
    return random.choice(bag)

used_names=set()  # フルネームで重複回避（姓は文化内で再利用可）
# 既存12人のフルネームを予約
for n in ["ミレイア・セルウィン","アウレリア・ファルケンハイト","コンラート・フォン・メーアブルク",
          "ジークムント・フォン・エーレンフェルス","キアラ・ヴェントゥーラ","アマーリア・ヴァレンティ",
          "ヴォルフ・アッカーマン","セオ・アシュフォード","レオンハルト・フォン・ツェルニー",
          "マッテオ・サルヴィアーティ","セリーヌ・マルロー","ディートリヒ・モーザー"]:
    used_names.add(n)

rows=[]   # ID一覧 追記用
pid = START_ID
per_faction = 20

for fac in FACTIONS:
    cul = NAMES[fac["culture"]]
    made=0
    while made < per_faction:
        sex = "女性" if random.random()<0.42 else "男性"
        given = random.choice(cul["f"] if sex=="女性" else cul["m"]).replace("2","")
        surname = random.choice(cul["s"])
        # 貴族前置詞
        is_noble = random.random() < fac["noble_ratio"]
        particle = cul["particle"] if (is_noble and cul["particle"]) else ""
        if particle == "ド・":
            fullname = f"{given}・{particle}{surname}"
        elif particle:
            fullname = f"{given}・{particle}・{surname}"
        else:
            fullname = f"{given}・{surname}"
        if fullname in used_names:
            continue
        used_names.add(fullname)

        ministry = random.choice(list(MINISTRIES.keys()))
        mdesc, stem = MINISTRIES[ministry]
        g = pick_grade()
        office = random.choice(g["offices"]).format(m=ministry[:-1] if ministry.endswith("省") else ministry,
                                                     cap=fac["capital"], sys=fac["system"].replace("星系",""),
                                                     stem=stem)
        # 宰相は太政官、卿は各省に整合
        if g["tier"]=="宰相": ministry="太政官"; mdesc,stem=MINISTRIES[ministry]
        court_rank = random.choice(g["ranks"])
        kugyo = court_rank in ("従三位","正三位","正四位上") or g["tier"]=="宰相"

        # 能力（文才＝運営/情報が核）
        operation = random.randint(58, 92)
        intelligence = random.randint(52, 90)
        if g["tier"] in ("宰相","卿"):
            operation = max(operation,72); intelligence=max(intelligence,66)
        leadership = random.randint(40, 74)
        attack = random.randint(15, 42); defense=random.randint(20,48); mobility=random.randint(18,46)
        research=random.randint(20,55); engineering=random.randint(18,52)
        planning=random.randint(35,72); production=random.randint(25,60)
        humility = random.randint(30, 82); virtue=random.randint(34,82)
        bunsai = round((operation+intelligence)/2)

        creed = random.choice(fac["creeds"])
        origin = "門閥貴族" if is_noble else random.choice(fac["origins"])
        age = random.randint(31, 62) if g["tier"] in ("宰相","卿","総督") else random.randint(26, 52)
        charisma = random.randint(38, 80); constitution=random.randint(40,82)
        hobby = random.choice(["読書","歴史","棋譜","茶","蒐集","美食","園芸","音楽","なし","読書"])
        vice = random.choice(["なし","なし","なし","吝嗇","傲慢","好色","大酒","短気"])

        callname = given
        gaisho = random.choice(GAISHO_PRE)
        nin = random.choice(CALL_NIN)
        youshi = random.choice(YOUSHI_F if sex=="女性" else YOUSHI_M)
        seikaku = random.choice(SEIKAKU)
        edu = random.choice(RAIREKI_EDU).format(rank_grad=random.choice(["首席","次席","上位","優等"]))
        post = random.choice(RAIREKI_POST).format(ministry=f"{fac['name']}{ministry}")

        per = f"PER-{pid:03d}"
        ship = ""  # 官僚は専用旗艦なし
        nendai = f"{age}歳"

        title_disp = f"{fullname}"
        body = f"""---
type: 人物
id: {per}
faction: {fac['name']}
sex: {sex}
vocation: 文官
court_rank: {court_rank}
office: {office}
ministry: {ministry}
role: 官僚
status: 存命
age_start: {age}
former_name:
asset:
portrait:
archetype:
tags: [人物, {fac['name']}, 官僚, 文官]
---

# {title_disp}

> {gaisho+'――' if gaisho else ''}{nin}。{fac['name']}{ministry}に在って、{mdesc}を司る。

## 概要
[[{fac['name']}]]の[[官僚|文官]]。{nendai}。**{ministry}**（{mdesc}）に属し、官職は**{office}**、位階は**{court_rank}**{'（公卿）' if kugyo else ''}。文才（運営・情報）を本領とし、{fac['flavor']}なかで実務の屋台骨を担う。

## 容姿
{youshi}。{fac['name']}の文官正装に{court_rank}の位色をまとう。{nendai}。

## 来歴
- {fac['name']}の{('門閥に連なる家' if is_noble else '市井')}に生まれ、{edu}{post}
- 考課を重ねて{court_rank}に叙され、現在は{office}として{mdesc}にあたる。

## 性格・信条
- {seikaku}
- 信条は**{creed}**。{fac['name']}の理念「{fac['ideology']}」との距離が、時に去就を左右する。

## 関係
- 所属：[[{fac['name']}]]
- 配属：{fac['name']}{ministry}（{mdesc}）

## ゲームデータ参照（正は runtime 側＝Person 生成）

| 項目 | 値 |
|---|---|
| 性別（`sex`） | {sex} |
| 初期年齢 | {age} |
| 職分（`PersonVocation`） | 文官 |
| 位階（`courtRank`） | {court_rank}{'（従三位以上＝公卿）' if kugyo else ''} |
| 官職（`Office`） | {office} |
| 所属省庁（二官八省） | {ministry}（{mdesc}） |
| 文才（`CivilAptitude`） | {bunsai}（運営{operation}・情報{intelligence}の平均） |

### 能力値（文官は **文才＝運営・情報** が核）
| 統率 | 運営（行政） | 情報（外交） | 研究 | 工学 | 企画 | 生産 | 謙虚 | 徳 |
|---|---|---|---|---|---|---|---|---|
| {leadership} | **{operation}** | **{intelligence}** | {research} | {engineering} | {planning} | {production} | {humility} | {virtue} |

### 氏名構成（構造化命名）
| givenName | familyName | nobleParticle | callName |
|---|---|---|---|
| {given} | {surname} | {particle.rstrip('・') if particle else '―'} | {callname} |

### 出自・信条・人となり（PER-PROPS）
| 項目 | 値 |
|---|---|
| 信条（`creed`） | {creed} |
| 出自・階層（`socialOrigin`） | {origin} |
| 出身地（`birthPlace`） | {fac['capital']}（{fac['system']}） |
| 人望（`charisma`） | {charisma} |
| 体質（`constitution`） | {constitution} |
| 趣味（`hobby`） | {hobby} |
| 悪癖（`vice`） | {vice} |

## 物語上の役割
- 武の提督に対する「文」の支え手。{mdesc}を通じて[[{fac['name']}]]の戦いを後方から成立させる、名もなき屋台骨の一人。

---
[[星骸の諸侯]]（総覧へ）
"""
        fname = os.path.join(PEOPLE, f"{fullname}.md")
        with open(fname, "w", encoding="utf-8") as fh:
            fh.write(body)
        rows.append(f"| {per} | [[{fullname}]] | [[{fac['name']}]] | {ministry}／{office} |")
        pid += 1
        made += 1

print(f"generated {pid-START_ID} notes, next id PER-{pid:03d}")
# rows をファイルへ（ID一覧 追記用）
with open("/tmp/per_rows.txt","w",encoding="utf-8") as fh:
    fh.write("\n".join(rows))
with open("/tmp/next_id.txt","w",encoding="utf-8") as fh:
    fh.write(f"PER-{pid:03d}")
