using UnityEngine;

namespace Ginei
{
    /// <summary>人物の役割区分（人物システム）。軍人＝軍務に強い／文民＝政務に強い。</summary>
    public enum PersonRole { 軍人, 文民 }

    /// <summary>役職の種類。軍務＝艦隊指揮・会戦／政務＝内政・外交・統治。</summary>
    public enum PostType { 軍務, 政務 }

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
    }
}
