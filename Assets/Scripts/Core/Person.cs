using UnityEngine;

namespace Ginei
{
    /// <summary>人物の役割区分（人物システム）。軍人＝軍務に強い／文民＝政務に強い。</summary>
    public enum PersonRole { 軍人, 文民 }

    /// <summary>役職の種類。軍務＝艦隊指揮・会戦／政務＝内政・外交・統治。</summary>
    public enum PostType { 軍務, 政務 }

    /// <summary>人物の拘留状態（LIFE-4 #154・死亡 #152 と対の可逆ルート）。自由→捕虜→（解放で自由／処断で処断済＝死亡へ合流）。</summary>
    public enum CaptiveStatus { 自由, 捕虜, 処断済 }

    /// <summary>
    /// 人物（キャラクター）の純データ（人物システム）。役割(<see cref="role"/>＝軍人/文民)を持ち、
    /// 軍才（統率/攻撃/防御/機動）と文才（運営/情報＝行政/外交）の適性を持つ。役割と役職の一致で
    /// 実効力が決まる＝<b>適材適所</b>（正名 #866）。提督(<see cref="AdmiralData"/>)は軍人 Person に相当。
    /// 列伝/殿堂 #785/#784・主人公 #735・階級 #14 と接続。解決は <see cref="PersonRules"/>（static）。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Person : ICharacter
    {
        public int id;
        public string name;
        public Faction faction;
        public PersonRole role = PersonRole.軍人;
        public int rankTier; // 階級（#14・序列）
        public Sex sex = Sex.男性; // 性別（既定=男性＝後方互換）。性的指向は別軸の検討項目（未実装）

        /// <summary>政治家か（政党・選挙で出世＝政治任用役職の資格・GOV-6 #159）。文民の一職種。</summary>
        public bool isPolitician;

        /// <summary>君主・国家元首か（人物の職分＝君主。POP 職業分類#110 に載らない apex ゆえ別管理＝<see cref="PersonVocation"/>。継承#152/易姓革命で替わる）。既定 false＝後方互換。</summary>
        public bool isSovereign;

        /// <summary>王家の生まれか（#王家教育）。<b>生まれた瞬間にネームド化</b>＝POP→学校の昇格経路でなく出生で Named になる別格。
        /// 既存の教育システム（士官学校#155/科挙#156/大学）を経ず、家庭教師の帝王学で育つ＝<see cref="RoyalEducationRules"/>。既定 false。</summary>
        public bool isRoyal;

        /// <summary>特殊作戦部隊（SOF）出身か（#SOF・SEAL型選抜の認定者）。提督として能力上昇＝<see cref="SpecialForcesRules"/>。既定 false。</summary>
        public bool isSpecialForces;

        /// <summary>財産行動の特性（PFIN-1・#2056・既定=貯金＝堅実・後方互換）。貯金/投資/浪費で可処分所得の使い方が変わる。</summary>
        public FinancialTrait financialTrait = FinancialTrait.貯金;

        /// <summary>個人の財産（PFIN-4・#2056・既定0）。俸給#1969 から特性に応じて貯金/投資で積み上がる（<see cref="PersonFinanceTickRules.TickYear"/>）。</summary>
        public float wealth = 0f;

        // --- 人物ライフサイクル（LIFE-1/2/4 #151/#152/#154） ---
        public int birthYear;                                   // 生年（0=未設定＝加齢しない）
        public int deathYear;                                   // 没年（0=存命）
        public CaptiveStatus captiveStatus = CaptiveStatus.自由; // 拘留状態（LIFE-4）
        public Faction heldBy;                                   // 捕獲勢力（捕虜時）

        // --- 家族（結婚と出産・血縁。-1=なし） ---
        public int spouseId = -1; // 配偶者（PersonMarriageRules）
        public int motherId = -1; // 母（ChildbirthRules）
        public int fatherId = -1; // 父（ChildbirthRules）

        // --- 経歴（出自パイプライン LIFE-5/6/7 #155/#156/#157） ---
        public int hammockNumber; // 士官学校の卒業席次（1=首席。小さいほど上位。LIFE-5）
        public MilitaryDegree militaryDegree = MilitaryDegree.無資格; // 軍学歴の最高（幼年学校卒/士官学校卒/大学校卒。LIFE-5 細分化・MilitaryAcademyRules）
        public int graduationYear; // 卒業年/合格年（学閥=同期の判定）
        public int schoolId;       // 卒業/合格制度のID（学閥=同窓の判定）
        public int examRank;       // 登用試験の合格順位（文官版ハンモック。LIFE-6）
        public int schoolPostingUntilYear; // 在学（学校配属）の終了年。0=非在学。>currentYear の間は艦隊配属不可（#SCHOOL-AGE）
        public int warCollegeRank; // 陸軍大学校内の卒業席次（1=首席。0=なし）。上位 SwordQuota が恩賜の軍刀組（MilitarySwordHonorRules）
        public ServiceStatus serviceStatus = ServiceStatus.現役; // 在役状態（現役→予備役→退役。RetirementRules・#530-536）
        public ExamDegree examDegree = ExamDegree.無資格; // 科挙の最高功名（生員/挙人/貢士/進士。LIFE-6 細分化・ImperialExamRules）

        // 官僚制（律令の位階・考課・官僚制基盤）。文官のネームド化＝官位を帯び考課で叙位される。既定は無位/未評定＝後方互換。
        public CourtRank courtRank = CourtRank.無位; // 位階（律令の身分序列。JapaneseCourtRankRules で叙位）
        public OfficialMerit merit;                  // 考課記録（OfficialMerit・null=未評定。BureaucracyCareerRules が起こす）

        // 専門能力（テクノクラート LIFE-7・既存の戦闘/政治能力とは別軸）
        public int research;     // 研究
        public int engineering;  // 技術
        public int planning;     // 計画
        public int production;   // 生産

        // 適性（0..100・AdmiralData と同枠）
        public int leadership;   // 統率（軍）
        public int attack;       // 攻撃（軍）
        public int defense;      // 防御（軍）
        public int mobility;     // 機動（軍）
        public int operation;    // 運営/行政（文）
        public int intelligence; // 情報/外交（文）

        public Person() { }

        public Person(int id, string name, Faction faction, PersonRole role)
        {
            this.id = id;
            this.name = name;
            this.faction = faction;
            this.role = role;
        }

        /// <summary>軍才＝統率・攻撃・防御・機動の平均（0..100）。</summary>
        public float MilitaryAptitude => (leadership + attack + defense + mobility) / 4f;

        /// <summary>文才＝運営・情報（行政/外交）の平均（0..100）。</summary>
        public float CivilAptitude => (operation + intelligence) / 2f;

        // --- ICharacter（役職保持の共通窓口・GOV-1 #142） ---
        public int Id => id;
        public string CharacterName => name;
        public Faction Faction => faction;
        public int RankTier => rankTier;
        public bool IsMilitary => role == PersonRole.軍人;
        public bool IsPolitician => isPolitician;
        public int BirthYear => birthYear;
        public bool IsDeceased => deathYear > 0;
        public bool IsAvailable => deathYear <= 0 && captiveStatus == CaptiveStatus.自由;

        /// <summary>専門才＝研究・技術・計画・生産の平均（0..100・テクノクラート LIFE-7）。</summary>
        public float TechnicalAptitude => (research + engineering + planning + production) / 4f;
    }
}
