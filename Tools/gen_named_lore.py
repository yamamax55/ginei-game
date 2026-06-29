#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""君主50・政治家50・技術者50 の lore ノートを生成（各テンプレ準拠・諸侯5勢力に10人ずつ/種別）。
正＝ゲームデータ(runtime Person 生成)の原則。史実 archetype は単一割当台帳を侵さないよう空。"""
import os, glob, random

ROOT = "/home/user/ginei-game/docs/lore"
PEOPLE = os.path.join(ROOT, "人物")
START_ID = 113  # PER-113 から

NAMES = {
    "german": {
        "m": ["ハインリヒ","フリードリヒ","ヴィルヘルム","オットー","ルートヴィヒ","カール","エルンスト","ヘルムート","グスタフ","アルブレヒト",
              "ヴェルナー","クラウス","ヨアヒム","ディーター","ゲオルク","アントン","フェルディナント","ブルーノ","マクシミリアン","ラインホルト",
              "ウルリヒ","ゴットフリート","ヴァルター","ロタール","ジギスムント","オスカー","エーリヒ","クルト","フランツ","ハンス"],
        "f": ["ヒルデガルト","イングリット","ザビーネ","ブリギッテ","ヘートヴィヒ","クリスタ","エルフリーデ","ゲルトルート","ヨハンナ","マルガレーテ",
              "ローゼマリー","アンネリーゼ","ゾフィー","エディト","ベアトリクス","クラーラ","レギーナ","フランツィスカ","テオドラ","アマーリエ",
              "ヘルガ","ウルリケ","コルネリア","ドロテア","イルゼ"],
        "s": ["ローテンブルク","ファルケンベルク","ヴァイセンフェルス","グリュンヴァルト","アイヒホルン","ブランデンシュタイン","リンデンベルク","シュヴァルツェンベルク",
              "モルゲンシュテルン","ヴィンターフェルト","ホーエンタール","アイゼンハルト","ローゼンクランツ","シュタールベルク","ノイハウス","ヴァッセルマン",
              "デューリンク","クレーフェルト","アーレンスドルフ","ビルケンフェルト","ドルンブルク","ヘルムスドルフ","ライヒェナウ","ザールフェルト","クヴェルフルト",
              "アルテンブルク","ヴェルニゲローデ","シェーンベルク","リヒテンシュタイン","エッゲブレヒト"],
        "particle": "フォン",
        "house": ["ベルンシュタイン","フォーゲルヴァイデ","ローテンベルク","アイゼンブルク","グラーフェンベルク","ヴァルデック","シュテルンハイム"],
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
        "house": ["メディチ","コルナーロ","モロジーニ","グリマルディ","スピノラ"],
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
        "house": ["ハミルトン","アシュワース","ペンドルトン","ウェントワース","カーライル"],
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
        "house": ["ヴァロワ","ブルボン","ロレーヌ","サヴォワ","モンパンシエ"],
    },
}

FACTIONS = [
    {"name":"碧晶公国","culture":"german","capital":"アマテラス","system":"エベレスト星系",
     "ideology":"正統なる権威による統一の再建","monarchy":True,
     "creeds":["帝政擁護","門閥主義","中道"],"origins":["門閥貴族","下級貴族","下級貴族"],"noble_ratio":0.6,
     "sov_titles_m":["大公","公子","公弟","辺境伯"],"sov_titles_f":["女大公","公女","大公妃","辺境伯爵夫人"],
     "parties":["正統派","門閥連合","宮廷改革派"],
     "flavor":"旧連合貴族の世襲公領"},
    {"name":"自由港同位体","culture":"italian","capital":"ワタツミ","system":"モンブラン星系",
     "ideology":"交易の自由・武装中立","monarchy":False,
     "creeds":["能力主義","共和主義","中道"],"origins":["平民","植民星","平民"],"noble_ratio":0.1,
     "sov_titles_m":["元首","門閥総代","商権長","執政"],"sov_titles_f":["元首","門閥総代","商権長","執政"],
     "parties":["自由貿易党","商権同盟","中立堅持派"],
     "flavor":"商業ギルド連合"},
    {"name":"黎明評議会","culture":"english","capital":"オオクニヌシ","system":"アコンカグア星系",
     "ideology":"自治と合意・反専制","monarchy":False,
     "creeds":["共和主義","能力主義","中道"],"origins":["平民","植民星","平民"],"noble_ratio":0.05,
     "sov_titles_m":["評議会議長","連邦元首","執政官","次席議長"],"sov_titles_f":["評議会議長","連邦元首","執政官","次席議長"],
     "parties":["自治連合","進歩評議派","連邦統一会"],
     "flavor":"植民星の民選議会連邦"},
    {"name":"鉄環兵団","culture":"german","capital":"スサノオ","system":"マッターホルン星系",
     "ideology":"実力主義の征服","monarchy":False,
     "creeds":["能力主義","能力主義","中道"],"origins":["平民","軍人家系","植民星"],"noble_ratio":0.0,
     "sov_titles_m":["総統","盟主","軍団長","副統領"],"sov_titles_f":["総統","盟主","軍団長","副統領"],
     "parties":["主戦派","兵団評議会","実利派"],
     "flavor":"傭兵あがりの軍閥"},
    {"name":"灯心自由市ファロス","culture":"french","capital":"ツクヨミ","system":"カンチェンジュンガ星系",
     "ideology":"武装中立・市民自治（都市国家）","monarchy":False,
     "creeds":["共和主義","中道","能力主義"],"origins":["平民","平民","植民星"],"noble_ratio":0.05,
     "sov_titles_m":["統領","市政長官","執政","参事会長"],"sov_titles_f":["統領","市政長官","執政","参事会長"],
     "parties":["市民自治党","灯台同盟","中道市民連合"],
     "flavor":"都市国家・市民自治"},
]

TECH_FIELDS = ["艦艇工学","兵器開発","機関・推進","情報通信","装甲・材料","都市・インフラ","補給・生産技術","観測・センサー"]
TECH_EDU = ["大学（テクノクラート）","高等専門学校（実技重視）","士官学校工兵科","専門学校","大学院（研究職）"]
TECH_OFFICE = ["技術省卿","開発主任","主席設計士","研究所長","技監","（現場技師）"]
TECH_WORK = ["新型主力艦の設計","艦載ビーム砲の改良","機関出力の増強","長距離通信網の構築","装甲材の開発",
             "工廠の生産効率化","索敵センサーの高性能化","補給艦の量産設計"]

random.seed(20260623)

# 既存ノートのフルネームを予約（衝突回避）
used=set()
for p in glob.glob(os.path.join(PEOPLE,"*.md")):
    base=os.path.basename(p)[:-3]
    if base.startswith("_"): continue
    used.add(base)

rows=[]
pid=START_ID

def make_name(cul, sex, noble_ratio, fac):
    pool = NAMES[cul]
    given = random.choice(pool["f"] if sex=="女性" else pool["m"])
    is_noble = random.random() < noble_ratio
    surname = random.choice(pool["s"])
    particle = pool["particle"] if (is_noble and pool["particle"]) else ""
    if particle=="ド・":
        full=f"{given}・{particle}{surname}"
    elif particle:
        full=f"{given}・{particle}・{surname}"
    else:
        full=f"{given}・{surname}"
    return given, surname, particle, is_noble, full

def uniq_name(cul, sex, noble_ratio, fac, force_house=None):
    for _ in range(200):
        if force_house:
            given = random.choice(NAMES[cul]["f"] if sex=="女性" else NAMES[cul]["m"])
            full = f"{given}・フォン・{force_house}"
            if full not in used:
                used.add(full); return given, force_house, "フォン", True, full
        else:
            g,s,p,n,full = make_name(cul,sex,noble_ratio,fac)
            if full not in used:
                used.add(full); return g,s,p,n,full
    raise RuntimeError("name pool exhausted")

def ability_row(vals):
    return " | ".join(str(v) for v in vals)

# ============ 君主 ============
SOV_YOUSHI_M=["威厳ある面構えに鋭い眼光","若く精悍、覇気を漂わせる","老練で重厚、玉座が板につく","端正だが冷徹な美貌","堂々たる体躯と低い声"]
SOV_YOUSHI_F=["凛とした気品と理知的な眼差し","若く華やかだが芯の強い佇まい","威厳に満ちた老貴婦人の風格","冷たい美貌に隠れた野心","気高く穏やか、しかし譲らぬ意志"]
SOV_SEIKAKU=["果断にして冷徹、目的のためには手段を選ばない。","寛仁大度で人望に厚く、臣下に慕われる。","猜疑心が強く、粛清を厭わない。","理想主義者で、旧弊の打破を志す。","保守的で伝統と格式を何より重んじる。","怜悧な現実主義者で、損得で動く。"]

def gen_sovereign(fac):
    cul=fac["culture"]; sex="女性" if random.random()<0.35 else "男性"
    titles = fac["sov_titles_f"] if sex=="女性" else fac["sov_titles_m"]
    title = random.choice(titles)
    monarchy=fac["monarchy"]
    is_reigning = title in (fac["sov_titles_m"][0], fac["sov_titles_f"][0])
    if monarchy:
        house=random.choice(NAMES[cul]["house"])
        given,surname,particle,is_noble,full = uniq_name(cul,sex,1.0,fac,force_house=house)
        dynasty=f"{house}家"; is_royal=True
    else:
        given,surname,particle,is_noble,full = uniq_name(cul,sex,fac["noble_ratio"],fac)
        dynasty=""; is_royal=False
    regnal = random.randint(1,5) if (is_reigning and monarchy) else 0
    age = random.randint(40,68) if is_reigning else random.randint(19,55)
    leadership=random.randint(58,93); operation=random.randint(50,86); intelligence=random.randint(48,84)
    warlike = fac["name"]=="鉄環兵団" or title in ("軍団長","辺境伯")
    attack=random.randint(45,80) if warlike else random.randint(20,55)
    defense=random.randint(40,72) if warlike else random.randint(22,52)
    mobility=random.randint(35,68) if warlike else random.randint(20,50)
    virtue=random.randint(28,92); humility=random.randint(20,70); ambition=random.randint(45,95)
    creed=fac["creeds"][0] if monarchy else random.choice(fac["creeds"])
    origin="門閥貴族" if is_royal else random.choice(fac["origins"])
    youshi=random.choice(SOV_YOUSHI_F if sex=="女性" else SOV_YOUSHI_M)
    seikaku=random.choice(SOV_SEIKAKU)
    regnal_disp = f"{regnal}" if regnal else ""
    title_full = f"{full}" + (f"（{regnal}世）" if regnal else "")
    rule_word = "統治" if is_reigning else ("王朝を支える" if monarchy else "国政に連なる")
    body=f"""---
type: 人物
id: PER-{pid:03d}
faction: {fac['name']}
sex: {sex}
vocation: 君主
title: {title}
dynasty: {dynasty}
regnal_number: {regnal}
is_royal: {'true' if is_royal else 'false'}
role: {title}
status: 存命
age_start: {age}
former_name:
asset:
portrait:
archetype:
tags: [人物, {fac['name']}, 君主]
---

# {title_full}

> {fac['name']}の{title}。{('正統を背負う者' if monarchy else '民に選ばれし元首')}。

## 概要
[[{fac['name']}]]の**{title}**。{age}歳。{fac['flavor']}の頂点に立ち、{rule_word}にあたる。{'王朝（'+dynasty+'）の血を引く。' if is_royal else '世襲によらず、実力と信認で位に就いた。'}

## 容姿
{youshi}。{fac['name']}の元首にふさわしい礼装をまとう。{age}歳。

## 統治・信条
- {seikaku}
- 信条は**{creed}**。国家理念「{fac['ideology']}」を体現する立場にある。{'徳と正統性が王朝の寿命を左右する（DynastyRules）。' if monarchy else '民意と信認がその座を支える。'}

## 関係
- 王朝／勢力：[[{fac['name']}]]
{'- 王家：'+dynasty if dynasty else '- 後ろ盾：（議会・有力者）'}

## ゲームデータ参照（正は runtime 側＝Person 生成）

| 項目 | 値 |
|---|---|
| 性別（`sex`） | {sex} |
| 初期年齢 | {age} |
| 君主（`isSovereign`） | true |
| 王家の生まれ（`isRoyal`） | {'true' if is_royal else 'false'} |
| 世数（`regnalNumber`） | {regnal if regnal else '0（なし）'} |
| 階級（`rankTier`） | 10（頂点） |
| 文民統制型 | {'君主統帥' if monarchy else '文民統制'} |

### 能力値（君主は **統率・政務・徳** が要）
| 統率 | 運営（政務） | 情報（外交） | 攻撃 | 防御 | 機動 | 謙虚 | 功名心 | 徳 |
|---|---|---|---|---|---|---|---|---|
| **{leadership}** | {operation} | {intelligence} | {attack} | {defense} | {mobility} | {humility} | {ambition} | **{virtue}** |

### 氏名構成（構造化命名）
| givenName | familyName | nobleParticle | regnalNumber | callName |
|---|---|---|---|---|
| {given} | {surname} | {particle.rstrip('・') if particle else '―'} | {regnal} | {given} |

### 出自・信条・人となり（PER-PROPS）
| 項目 | 値 |
|---|---|
| 信条（`creed`） | {creed} |
| 出自・階層（`socialOrigin`） | {origin} |
| 出身地（`birthPlace`） | {fac['capital']}（{fac['system']}） |
| 功名心（`ambition`） | {ambition} |
| 徳（`virtue`） | {virtue} |

## 物語上の役割
- [[{fac['name']}]]の意志そのもの。その決断が会戦・外交・{'王朝の興亡' if monarchy else '連邦の存亡'}を左右する。

---
[[星骸の諸侯]]（総覧へ）
"""
    return full, body, f"{title}"

# ============ 政治家 ============
POL_YOUSHI_M=["如才ない笑みと隙のない物腰","熱弁を得意とする押しの強い風貌","誠実そうな佇まいの下に計算を秘める","老獪で貫禄ある党人の風格","若く弁の立つ新進の面構え"]
POL_YOUSHI_F=["明朗で人を惹きつける笑顔","理知的で隙のない弁士の佇まい","柔和だが芯の強い物腰","貫禄ある女傑の風格","清新で誠実そうな印象"]
POL_SEIKAKU=["大衆人気を背に強引に押し切るポピュリスト型。","党内基盤を固めて実を取る党人型。","理想を掲げ清廉を売りにする改革者。","調整に長け、対立を呑み込む寝業師。","主戦論で世論を煽る強硬派。","和平と妥協を説く穏健派。"]
POL_OFFICE=["評議会議長","首班","国務委員","議員","幹事長","院内総務","（一年生議員）"]

def gen_politician(fac):
    cul=fac["culture"]; sex="女性" if random.random()<0.38 else "男性"
    given,surname,particle,is_noble,full = uniq_name(cul,sex,fac["noble_ratio"],fac)
    party=random.choice(fac["parties"])
    office=random.choice(POL_OFFICE)
    frole=random.choice(["党首","幹部","派閥領袖","一般議員","幹部"])
    age=random.randint(33,66)
    popularity=round(random.uniform(0.28,0.78),2)
    oratory=random.randint(48,92)
    scandal=round(random.uniform(0.0,0.45),2)
    base=random.choice(["厚い","中程度","薄い"])
    leadership=random.randint(45,82); operation=random.randint(48,84); intelligence=random.randint(50,86)
    charisma=random.randint(50,92); humility=random.randint(20,70); virtue=random.randint(25,80)
    creed=random.choice(fac["creeds"])
    origin=random.choice(fac["origins"]) if not is_noble else "門閥貴族"
    youshi=random.choice(POL_YOUSHI_F if sex=="女性" else POL_YOUSHI_M)
    seikaku=random.choice(POL_SEIKAKU)
    body=f"""---
type: 人物
id: PER-{pid:03d}
faction: {fac['name']}
sex: {sex}
vocation: 政治家
party: {party}
faction_role: {frole}
office: {office}
role: 政治家
status: 存命
age_start: {age}
former_name:
asset:
portrait:
archetype:
tags: [人物, {fac['name']}, 政治家]
---

# {full}

> {fac['name']}の政治家。{party}の{frole}。

## 概要
[[{fac['name']}]]の**政治家**。{age}歳。{party}に属し、{frole}として国政を動かす。公職は**{office}**。人望と弁舌を武器に、民意を背に立ち回る。

## 容姿
{youshi}。{fac['name']}の文民正装に党章をつける。{age}歳。

## 政見・信条
- {seikaku}
- 信条は**{creed}**。国家理念「{fac['ideology']}」をめぐる論戦の最前線に立つ。

## 関係
- 政党／勢力：[[{fac['name']}]]（{party}）
- 立場：{frole}

## ゲームデータ参照（正は runtime 側＝Person 生成）

| 項目 | 値 |
|---|---|
| 性別（`sex`） | {sex} |
| 初期年齢 | {age} |
| 政治家（`isPolitician`） | true |
| 公職（`Office`） | {office} |
| 所属政党 | {party} |

### 政治家プロフィール（`Politician`・選挙で効く）
| 項目 | 値 |
|---|---|
| 人気（`popularity` 0..1） | {popularity} |
| 弁舌（`oratory`） | {oratory} |
| 党内基盤 | {base} |
| スキャンダル（`scandalLevel`） | {scandal} |

### 能力値（政治家は **人望・弁舌・文才** が核）
| 統率 | 運営（行政） | 情報（外交） | 人望（charisma） | 謙虚 | 徳 |
|---|---|---|---|---|---|
| {leadership} | {operation} | {intelligence} | **{charisma}** | {humility} | {virtue} |

### 出自・信条・人となり（PER-PROPS）
| 項目 | 値 |
|---|---|
| 信条（`creed`） | {creed} |
| 出自・階層（`socialOrigin`） | {origin} |
| 出身地（`birthPlace`） | {fac['capital']}（{fac['system']}） |
| 人望（`charisma`） | {charisma} |

## 物語上の役割
- 軍の外で勝敗を決める層。世論・予算・講和を握り、[[{fac['name']}]]の提督の戦いを縛りもし支えもする。

---
[[星骸の諸侯]]（総覧へ）
"""
    return full, body, f"{party}／{office}"

# ============ 技術者（テクノクラート） ============
TEC_YOUSHI_M=["白衣を羽織った理知的な風貌","油染みの作業着が似合う現場肌","痩身に分厚い眼鏡、思索的な目","若く精力的な技師の面構え","寡黙で職人気質の佇まい"]
TEC_YOUSHI_F=["きびきびとした理系の佇まい","白衣姿に鋭い観察眼","落ち着いた知性を感じさせる風貌","若く熱意あふれる技師の表情","物静かだが芯の通った職人気質"]
TEC_SEIKAKU=["実証主義者で、データと試験結果しか信じない。","天才肌で、常識外れの発想を持ち込む。","完璧主義の職人で、妥協を許さない。","野心的で、自らの設計を歴史に残そうとする。","堅実な実務家で、量産と信頼性を重んじる。"]

def gen_technician(fac):
    cul=fac["culture"]; sex="女性" if random.random()<0.36 else "男性"
    given,surname,particle,is_noble,full = uniq_name(cul,sex,fac["noble_ratio"]*0.4,fac)
    field=random.choice(TECH_FIELDS); edu=random.choice(TECH_EDU); office=random.choice(TECH_OFFICE)
    work=random.choice(TECH_WORK)
    age=random.randint(27,60)
    research=random.randint(55,93); engineering=random.randint(52,92)
    planning=random.randint(45,84); production=random.randint(45,86)
    leadership=random.randint(28,60); operation=random.randint(35,66); intelligence=random.randint(35,66)
    humility=random.randint(30,78); virtue=random.randint(35,78)
    tech=round((research+engineering+planning+production)/4)
    creed=random.choice(fac["creeds"])
    origin=random.choice(fac["origins"])
    youshi=random.choice(TEC_YOUSHI_F if sex=="女性" else TEC_YOUSHI_M)
    seikaku=random.choice(TEC_SEIKAKU)
    body=f"""---
type: 人物
id: PER-{pid:03d}
faction: {fac['name']}
sex: {sex}
vocation: 技術者
field: {field}
education: {edu}
office: {office}
role: 技術者
status: 存命
age_start: {age}
former_name:
asset:
portrait:
archetype:
tags: [人物, {fac['name']}, 技術者]
---

# {full}

> {fac['name']}の技術者（テクノクラート）。専門は{field}。

## 概要
[[{fac['name']}]]の**技術者**。{age}歳。{field}を専門とし、{office}として{work}にあたる。戦わずして戦力を生む、勢力の技術的屋台骨。

## 容姿
{youshi}。{fac['name']}の技官の制服に身を包む。{age}歳。

## 性格・信条
- {seikaku}
- 信条は**{creed}**。研究と開発を通じて国家理念「{fac['ideology']}」を技術面から支える。

## 関係
- 所属：[[{fac['name']}]]（{office}）
- 専門：{field}

## ゲームデータ参照（正は runtime 側＝Person 生成）

| 項目 | 値 |
|---|---|
| 性別（`sex`） | {sex} |
| 初期年齢 | {age} |
| 職分（`PersonVocation`） | 技術者 |
| 専門分野 | {field} |
| 学歴 | {edu} |
| 役職（`Office`） | {office} |
| 専門才（`TechnicalAptitude`） | {tech}（研究/工学/企画/生産の平均） |

### 能力値（技術者は **専門才＝研究・工学・企画・生産** が核）
| 研究 | 工学 | 企画 | 生産 | 統率 | 運営 | 情報 | 謙虚 | 徳 |
|---|---|---|---|---|---|---|---|---|
| **{research}** | **{engineering}** | {planning} | {production} | {leadership} | {operation} | {intelligence} | {humility} | {virtue} |

### 出自・信条・人となり（PER-PROPS）
| 項目 | 値 |
|---|---|
| 信条（`creed`） | {creed} |
| 出自・階層（`socialOrigin`） | {origin} |
| 出身地（`birthPlace`） | {fac['capital']}（{fac['system']}） |

## 物語上の役割
- 新型艦・新兵器・技術優位が会戦の前提条件を書き換える。[[{fac['name']}]]の戦いを設計図の段階から左右する一人。

---
[[星骸の諸侯]]（総覧へ）
"""
    return full, body, f"{field}／{office}"

# ---- 生成（種別ごとに10人/勢力） ----
BUILDERS=[("君主",gen_sovereign),("政治家",gen_politician),("技術者",gen_technician)]
for kind,builder in BUILDERS:
    for fac in FACTIONS:
        for _ in range(10):
            full, body, note = builder(fac)
            with open(os.path.join(PEOPLE, f"{full}.md"),"w",encoding="utf-8") as fh:
                fh.write(body)
            rows.append(f"| PER-{pid:03d} | [[{full}]] | [[{fac['name']}]] |")
            pid+=1

print(f"generated {pid-START_ID} notes ({len(BUILDERS)} kinds × 5 factions × 10). next id PER-{pid:03d}")
open("/tmp/named_rows.txt","w",encoding="utf-8").write("\n".join(rows))
open("/tmp/named_next.txt","w",encoding="utf-8").write(f"PER-{pid:03d}")
