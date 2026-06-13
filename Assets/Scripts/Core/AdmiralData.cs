using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の能力データを保持する ScriptableObject。
    /// 銀河英雄伝説IV EX の能力値を踏襲します。
    ///
    /// 参謀（最大3名）を付けると、各能力を「参謀の最高値×staffBonusRatio」で補完した
    /// 実効能力を算出します（基準値=public フィールドは書き換えない＝実効値パターン）。
    /// ゲーム側は必ず Effectivexxx ゲッターを参照して実効能力を反映すること。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAdmiral", menuName = "Ginei/Admiral Data")]
    public class AdmiralData : ScriptableObject
    {
        [Header("基本情報")]
        public string admiralName = "提督名";
        public Faction faction;

        [Header("姓名（任意・未設定なら admiralName を使用＝後方互換）")]
        [Tooltip("名（カタカナ）。例：ラインハルト。名→姓の順で合成する")]
        public string givenName = "";

        [Tooltip("ミドルネーム（任意）。名と姓の間に入る")]
        public string middleName = "";

        [Tooltip("姓（カタカナ）。例：ローエングラム")]
        public string familyName = "";

        [Tooltip("貴族の前置詞（平民は空。貴族は「フォン」等）。姓の直前に挿入され、貴族を一目で示す")]
        public string nobleParticle = "";

        [Tooltip("世数（一世・二世…）。0＝無し。1〜99 を漢数字＋「世」で表示し、姓名末尾に区切り無しで付く")]
        public int regnalNumber = 0;

        [Tooltip("異名（例：疾風）。頭上ラベル用の短縮名(ShortName)の前に付く")]
        public string epithet = "";

        [Tooltip("呼称・愛称（短縮表示の最優先）。空なら 姓→名→admiralName の順でフォールバック")]
        public string callName = "";

        [Tooltip("専用旗艦名（#旗艦名・任意）。この提督が乗艦すると必ずこの名の旗艦になる（例：ヤン→ヒューベリオン）。" +
                 "空なら SignatureShipRegistry の既定表→世界遺産プールの順で解決。愛着を持てる専用艦の出所")]
        public string signatureShipName = "";

        [Header("主人公（アンカー・GON-6・任意）")]
        [Tooltip("この提督を主人公（動かない光源）とするか。true＝陣営に関わらず常にプレイヤー操作（FleetAI 非制御）。" +
                 "既定 false＝従来どおり（後方互換）。判定は ProtagonistRules を参照すること")]
        public bool isProtagonist = false;

        [Header("階級（#14・任意）")]
        [Tooltip("階級の序列 tier（所属勢力の階級表 FactionData.ranks から名称を解決）。" +
                 "0＝未設定＝HUDに階級を出さない（後方互換）。欠番tierは直近tierへ丸め（RankSystem.ResolveRankName）。" +
                 "既定ラダー：5准将/6少将/7中将/8大将/9上級大将(帝国)/10元帥")]
        public int rankTier = 0;

        [Header("能力値 (0-100)")]
        [Tooltip("兵力上限・士気に影響")]
        public int leadership = 80;    // 統率

        [Tooltip("攻撃力に影響")]
        public int attack = 80;        // 攻撃

        [Tooltip("被ダメージ軽減に影響")]
        public int defense = 80;       // 防御

        [Tooltip("移動速度・回頭速度に影響")]
        public int mobility = 80;      // 機動

        [Tooltip("補給・コスト等に影響（将来用）")]
        public int operation = 80;     // 運営

        [Tooltip("索敵・回避等に影響（将来用）")]
        public int intelligence = 80;  // 情報

        [Tooltip("部下への態度（謙虚さ 0..100・既定50＝中庸）。高い＝部下を尊重し信望厚い／低い＝尊大。" +
                 "統率と併せ、敗走時に配下艦が島津の捨てがまり（殿）で旗艦を守るか散り散りに逃げるかを決める（SutegamariRules）")]
        [Range(0, 100)]
        public int humility = 50;      // 部下への態度（謙虚さ）

        [Tooltip("功名心（手柄を求める気持ち 0..100・既定50）。高い提督は軍団陣形で前線を志願しやすい（史実＝功を焦る武将）。CorpsDeploymentRules が参照")]
        [Range(0, 100)]
        public int ambition = 50;      // 功名心（前線志願）

        [Tooltip("特殊作戦部隊（SOF）出身か（#SOF）。出身者は提督として常時+5%、艦隊単独の特殊作戦〔側背/包囲＝後方かく乱・周りこみ〕で+20%。SpecialForcesRules")]
        public bool isSpecialForces = false; // 特殊作戦部隊出身

        [Tooltip("軍神（限界突破型・#軍神＝上杉謙信型）。天地人（天の時/地の利/人の和）が揃ったときに限り能力上限100を超えて成長する。" +
                 "既定 false＝並の提督は100で頭打ち（後方互換）。TenchijinRules が解決")]
        public bool isTranscendent = false; // 軍神＝限界突破型

        [Tooltip("表裏比興の者（#表裏比興＝真田昌幸型）。主家滅亡を発動条件に覚醒し、寡兵で大軍を翻弄・自在に変節して生き残る。" +
                 "既定 false＝従来動作。HyoriHikyoRules が解決（発動条件＝主家滅亡）")]
        public bool isHyoriHikyo = false; // 表裏比興の者＝主家滅亡で覚醒する梟雄

        [Tooltip("革新者（#革新者＝織田信長型）。先見性・新技術の積極活用で時代を先取りし、若い頃は『うつけ』と侮られるが開花する。" +
                 "既定 false＝従来動作。InnovatorRules が解決")]
        public bool isInnovator = false; // 革新者＝織田信長型（先見性・新技術・若年うつけ）

        [Tooltip("立身出世型（#立身出世＝豊臣秀吉型）。門地を問わぬ実力本位の出世・人たらし（人心掌握）・戦略機動の妙（中国大返し）。" +
                 "既定 false＝従来動作。RisingHeroRules が解決")]
        public bool isRisingHero = false; // 立身出世型＝豊臣秀吉型（足軽から天下人）

        [Tooltip("日本一の兵（#日本一の兵＝真田幸村型）。とにかく強い武勇に加え、真田丸の堅守（寡兵の防衛）と決死の突撃（窮地ほど苛烈）。" +
                 "既定 false＝従来動作。PeerlessWarriorRules が解決")]
        public bool isPeerlessWarrior = false; // 日本一の兵＝真田幸村型（剛勇・真田丸・決死突撃）

        [Tooltip("三日天下型（#三日天下＝明智光秀型）。中央の事情にあかるく謀反（本能寺の変）は成功させるが、主殺しゆえ正統性を得られず短命に終わる。" +
                 "既定 false＝従来動作。ThreeDayReignRules が解決")]
        public bool isThreeDayRuler = false; // 三日天下型＝明智光秀型（中央通・謀反成功・短命）

        [Tooltip("寝返り型（#寝返り＝小早川秀秋型）。調略・圧力に屈し布陣後に寝返る。決定的だが布陣後の寝返りはご法度ゆえ名誉が大幅に下がる。" +
                 "既定 false＝従来動作。TurncoatRules が解決")]
        public bool isTurncoat = false; // 寝返り型＝小早川秀秋型（布陣後の寝返り・名誉大幅減）

        [Tooltip("徳望（0..100・#徳望＝劉備玄徳型の核）。高いほど人が慕い忠誠が集まる。徳望の主は限界突破して100超の実効徳になる（VirtueLordRules）。既定50")]
        [Range(0, 100)]
        public int virtue = 50;

        [Tooltip("漢室の末裔を自称するか（#徳望＝劉備玄徳型）。自称でも大義名分となり正統性を高める。既定false。VirtueLordRules が解決")]
        public bool claimsImperialDescent = false;

        [Tooltip("徳望の主（#徳望＝劉備玄徳型）。徳と人物審美眼が限界突破し、桃園/水魚/三顧で人を得るが、義兄弟を失うと諫言を無視し大敗する（夷陵）。" +
                 "既定false＝従来動作。VirtueLordRules が解決")]
        public bool isVirtuousLord = false; // 徳望の主＝劉備玄徳型

        [Tooltip("武聖（#武聖＝関羽型）。限界突破した武勇・一騎打ちの達人・千里行（主君への忠）。傲慢（虎の子はやれぬ）で同盟を失い孤立すると背後を突かれる（荊州陥落）。" +
                 "既定false＝従来動作。WarSaintRules が解決。※上杉謙信の軍神(TenchijinRules)とは別")]
        public bool isWarSaint = false; // 武聖＝関羽型（限界突破の武・一騎打ち・千里行・荊州の慢心）

        [Tooltip("猛将（#猛将＝張飛型）。猪突猛進・長坂の一喝・一騎打ちに強く正義感に厚いが、部下に厳しく酒癖が悪く暗殺されうる。" +
                 "既定false＝従来動作。FierceGeneralRules が解決")]
        public bool isFierceGeneral = false; // 猛将＝張飛型（猪突・一喝・正義感・酒癖・暗殺リスク）

        [Tooltip("大戦術家（#大戦術家＝ハンニバル型）。包囲殲滅・戦場の霧・心理戦・地形無効（アルプス越え）・宿敵特効・多国籍結束に長けるが、内政/兵站が苦手で研究されると包囲が破られる。" +
                 "既定false＝従来動作。GrandTacticianRules が解決")]
        public bool isGrandTactician = false; // 大戦術家＝ハンニバル型

        [Tooltip("覇王（#覇王＝ラインハルト型）。各個撃破の電撃戦・攻勢インフレ・黄金の獅子のカリスマ・門閥特効・好敵手で輝く。相棒喪失で暴走・短期決戦無敵だが持久戦に弱い。" +
                 "既定false＝従来動作。KaiserRules が解決（専用旗艦ブリュンヒルトは SignatureShipRegistry）")]
        public bool isKaiser = false; // 覇王＝ラインハルト型

        [Tooltip("半身（#半身＝キルヒアイス型）。万能・流血なき懐柔・無私の身代わり・主君の良心（暴走を止める）・精神攻撃無効・真のカリスマ。覇王と組むと倍化し、喪失で覇王が暴走する。" +
                 "既定false＝従来動作。RightHandRules が解決")]
        public bool isRightHand = false; // 半身＝キルヒアイス型（理想のナンバーツー）

        [Tooltip("魔術師（#魔術師＝ヤン・ウェンリー型）。やる気ゼロの最高知力。絶体絶命ほど逆転し、不敗の撤退・先読み無効化・民主主義の精神耐性・偉大なる教師。覇王の好敵手。" +
                 "既定false＝従来動作。MagicianRules が解決（専用旗艦ヒューベリオンは SignatureShipRegistry）")]
        public bool isMagician = false; // 魔術師＝ヤン・ウェンリー型（不敗・逆転特化）

        [Header("艦隊設定")]
        [Tooltip("【非推奨・RANKCMD-1 #1711】兵力は人物でなく艦隊が持つ（FleetUnitData.baseStrength／FleetStrength.baseStrength）。" +
                 "後方互換のフォールバック専用＝艦隊側に兵力が無いときだけ読まれる。人物は階級で『指揮できる規模』を持つ（CommandCapacityRules）。")]
        public int baseStrength = 10000;

        [Header("得意陣形（#104）")]
        [Tooltip("得意陣形ボーナスを有効にするか（false＝未設定＝従来どおりボーナス無し＝後方互換）")]
        public bool hasPreferredFormation = false;

        [Tooltip("この提督の得意陣形。部隊の現在陣形が一致する間、移動・防御にボーナス（hasPreferredFormation=true のとき）")]
        public Formation preferredFormation = Formation.紡錘陣;

        /// <summary>
        /// 部隊の現在陣形 current が、この提督の得意陣形と一致するか。
        /// hasPreferredFormation が false（未設定）なら常に false＝ボーナス無し（後方互換）。
        /// </summary>
        public bool IsPreferredFormation(Formation current)
        {
            return hasPreferredFormation && current == preferredFormation;
        }

        [Header("参謀（最大3名・能力補完）")]
        [Tooltip("能力を補完する参謀（最大3名・提督データを流用）。各能力は参謀の最高値×staffBonusRatio だけ底上げされる")]
        public AdmiralData[] staffOfficers = new AdmiralData[0];

        [Tooltip("参謀の能力が実効能力に寄与する割合 (0〜1)。例:0.2 なら参謀の能力の20%を補完")]
        [Range(0f, 1f)]
        public float staffBonusRatio = 0.2f;

        [Header("特技・戦法（本作オリジナル・信長の野望/三国志を参考）")]
        [Tooltip("この提督が所持する特技・戦法（TalentCatalog の id＋格）。空＝特技なし＝従来動作。" +
                 "効果は TalentRules が素養（実効能力）と格で解き、戦闘は AdmiralSkillRules#137-140 等へ橋渡しする")]
        public List<Talent> talents = new List<Talent>();

        [Header("提督能力の深化（ADM・#2301）")]
        [Tooltip("武名・名声（0..100・ADM-3 #2304）。戦功で上昇。高名な将は敵士気を削り味方を鼓舞し寝返りされにくい（RenownRules）。既定0＝無名")]
        [Range(0, 100)]
        public int fame = 0;

        [Tooltip("得意戦型（ADM-6 #2307）。状況一致でボーナス（SpecialtyRules）。既定=なし＝従来動作")]
        public CombatSpecialty specialty = CombatSpecialty.なし;

        [Tooltip("疲労（0..100・ADM-5 #2306）。連戦で蓄積し実効能力を一時低下、休養で回復（ConditionRules・基準非破壊）。既定0")]
        [Range(0, 100)]
        public int fatigue = 0;

        [Tooltip("負傷の重さ（0..100・ADM-5 #2306）。実効能力を一時低下（ConditionRules）。旗艦撃破時の運命#2260と地続き。既定0")]
        [Range(0, 100)]
        public int woundSeverity = 0;

        [Tooltip("成長状態（ADM-2 #2303・GrowthRules#537-543）。経験で実効能力が伸びる（軍神#軍神は天地人で100超）。null＝成長なし＝従来動作")]
        public Growth growth = null;

        [Header("提督の采配と人物ドラマ（CDR・#2310）")]
        [Tooltip("性格類型（CDR-1 #2311）。AI采配の傾向（交戦/撤退・特殊指揮・陣形選好）を決める＝CommandDoctrineRules。既定=冷静")]
        public CommanderPersonality personality = CommanderPersonality.冷静;

        [Tooltip("君主への個人忠誠（0..1・CDR-2 #2312・既定1＝忠臣）。論功行賞/不遇/功名心で動き、低忠誠×高功名心で下剋上＝AllegianceDriftRules")]
        [Range(0f, 1f)]
        public float personalLoyalty = 1f;

        /// <summary>参謀の最大人数。</summary>
        public const int MaxStaff = 3;

        /// <summary>能力値の上限（補完してもこれを超えない）。</summary>
        public const int MaxStatValue = 100;

        // ===== 実効能力（基準値＋参謀補完。基準フィールドは非破壊）=====
        public int EffectiveLeadership   => ComputeEffective(leadership,   s => s.leadership);
        public int EffectiveAttack       => ComputeEffective(attack,       s => s.attack);
        public int EffectiveDefense      => ComputeEffective(defense,      s => s.defense);
        public int EffectiveMobility     => ComputeEffective(mobility,     s => s.mobility);
        public int EffectiveOperation    => ComputeEffective(operation,    s => s.operation);
        public int EffectiveIntelligence => ComputeEffective(intelligence, s => s.intelligence);

        /// <summary>
        /// 基準値に「参謀（最大MaxStaff名）の当該能力の最高値×staffBonusRatio」を加えた実効値を返す。
        /// 上限は MaxStatValue。基準フィールドは変更しない（実効値パターン）。
        /// </summary>
        private int ComputeEffective(int baseValue, Func<AdmiralData, int> selector)
        {
            int best = 0;
            int counted = 0;
            if (staffOfficers != null)
            {
                for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
                {
                    AdmiralData s = staffOfficers[i];
                    if (s == null || s == this) continue; // 空き枠・自己参照は無視
                    counted++;
                    int v = selector(s);
                    if (v > best) best = v;
                }
            }
            int bonus = Mathf.RoundToInt(best * staffBonusRatio);
            return Mathf.Clamp(baseValue + bonus, 0, MaxStatValue);
        }

        /// <summary>有効な参謀（非null）が1名以上いるか。</summary>
        public bool HasStaff
        {
            get
            {
                if (staffOfficers == null) return false;
                int counted = 0;
                for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
                {
                    AdmiralData s = staffOfficers[i];
                    if (s == null || s == this) continue;
                    return true;
                }
                return false;
            }
        }

        /// <summary>参謀名を「、」区切りで返す（最大MaxStaff名・HUD表示用）。参謀が無ければ空文字。</summary>
        public string GetStaffNames()
        {
            if (staffOfficers == null) return string.Empty;
            string result = string.Empty;
            int counted = 0;
            for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
            {
                AdmiralData s = staffOfficers[i];
                if (s == null || s == this) continue;
                if (counted > 0) result += "、";
                result += s.ShortName;
                counted++;
            }
            return result;
        }

        // ===== 姓名の合成（#523・読み取り時合成・基準フィールド非破壊）=====

        /// <summary>姓名の区切り（中黒）。</summary>
        public const string NameSeparator = "・";

        /// <summary>貴族の前置詞の既定値（生成補助用）。</summary>
        public const string NobleParticleDefault = "フォン";

        /// <summary>世数の上限（これを超えると世数表記は付かない）。</summary>
        public const int RegnalMax = 99;

        /// <summary>世数の接尾語。</summary>
        public const string RegnalSuffixWord = "世";

        private static readonly string[] KanjiDigits =
            { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        /// <summary>
        /// 正式名。名→姓の順で「名・[ミドル]・[前置詞]・姓」を中黒連結し、末尾に世数（区切り無し）を付ける。
        /// 名も姓も空なら admiralName を返す（後方互換）。合成結果が空でも admiralName にフォールバック。
        /// </summary>
        public string FullName
        {
            get
            {
                bool hasGiven = !string.IsNullOrEmpty(givenName);
                bool hasFamily = !string.IsNullOrEmpty(familyName);
                if (!hasGiven && !hasFamily) return admiralName;

                var sb = new StringBuilder();
                AppendToken(sb, givenName);
                AppendToken(sb, middleName);
                AppendToken(sb, nobleParticle);
                AppendToken(sb, familyName);
                string result = sb.ToString() + RegnalSuffix(regnalNumber);
                return string.IsNullOrEmpty(result) ? admiralName : result;
            }
        }

        /// <summary>短縮名（コンパクト表示用）。呼称→姓→名→admiralName の順で最初に在るもの。</summary>
        public string ShortName
        {
            get
            {
                if (!string.IsNullOrEmpty(callName)) return callName;
                if (!string.IsNullOrEmpty(familyName)) return familyName;
                if (!string.IsNullOrEmpty(givenName)) return givenName;
                return admiralName;
            }
        }

        /// <summary>異名＋短縮名（頭上ラベル用）。異名が無ければ ShortName と同じ。例：疾風ウォルフ。</summary>
        public string EpithetName
        {
            get
            {
                string s = ShortName;
                return string.IsNullOrEmpty(epithet) ? s : epithet + s;
            }
        }

        /// <summary>中黒区切りでトークンを連結（空トークンはスキップ＝余分な中黒を出さない）。</summary>
        private static void AppendToken(StringBuilder sb, string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            if (sb.Length > 0) sb.Append(NameSeparator);
            sb.Append(token);
        }

        /// <summary>
        /// 世数の接尾辞を返す（例：1→「一世」, 14→「十四世」, 20→「二十世」）。
        /// 1〜RegnalMax(99) の範囲外は空文字。
        /// </summary>
        public static string RegnalSuffix(int n)
        {
            if (n < 1 || n > RegnalMax) return string.Empty;
            return KanjiNumber(n) + RegnalSuffixWord;
        }

        /// <summary>1〜99 を漢数字へ。範囲外は空文字（RegnalSuffix 専用の内部ヘルパ）。</summary>
        private static string KanjiNumber(int n)
        {
            if (n < 1 || n > 99) return string.Empty;
            if (n < 10) return KanjiDigits[n];
            int tens = n / 10, ones = n % 10;
            string s = (tens == 1) ? "十" : KanjiDigits[tens] + "十";
            if (ones > 0) s += KanjiDigits[ones];
            return s;
        }
    }
}
