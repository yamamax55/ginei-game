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

        /// <summary>政治家か（政党・選挙で出世＝政治任用役職の資格・GOV-6 #159）。文民の一職種。</summary>
        public bool isPolitician;

        // --- 人物ライフサイクル（LIFE-1/2/4 #151/#152/#154） ---
        public int birthYear;                                   // 生年（0=未設定＝加齢しない）
        public int deathYear;                                   // 没年（0=存命）
        public CaptiveStatus captiveStatus = CaptiveStatus.自由; // 拘留状態（LIFE-4）
        public Faction heldBy;                                   // 捕獲勢力（捕虜時）

        // --- 経歴（出自パイプライン LIFE-5/6/7 #155/#156/#157） ---
        public int hammockNumber; // 士官学校の卒業席次（1=首席。小さいほど上位。LIFE-5）
        public int graduationYear; // 卒業年/合格年（学閥=同期の判定）
        public int schoolId;       // 卒業/合格制度のID（学閥=同窓の判定）
        public int examRank;       // 登用試験の合格順位（文官版ハンモック。LIFE-6）
        public ExamDegree examDegree = ExamDegree.無資格; // 科挙の最高功名（生員/挙人/貢士/進士。LIFE-6 細分化・ImperialExamRules）

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
