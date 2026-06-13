using System.Collections.Generic;

namespace Ginei
{
    /// <summary>陳情の出自（目安箱 MEYASU #1296）。建白＝下から注入↑／諮問＝上が箱へ問い返す↓／注入＝横から起草。</summary>
    public enum PetitionOrigin { 建白, 諮問, 注入 }

    /// <summary>
    /// 陳情の状態（MEYASU-3 #1299）。起案→伝播中（官僚機構を通過）→決裁待ち→承認/却下→執行済。
    /// 失敗は 却下（握り潰し）か 黙殺（無視）。黙殺は後に正しさが判明すれば 再浮上 しうる。
    /// </summary>
    public enum PetitionStatus { 起案, 伝播中, 決裁待ち, 承認, 却下, 黙殺, 執行済, 再浮上 }

    /// <summary>
    /// 目安箱に投じられた一件の陳情（MEYASU-3 #1299）。権力者の「箱」へ向かい、多エージェント官僚機構を
    /// 伝播する。文面（WHY）は LLM が後付けし、ここは構造化された決定（WHAT）だけを持つ＝決定論的に保存可能。
    /// 伝播・執行の解決は <see cref="PetitionFlowRules"/>（static）が唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Petition
    {
        public int id;
        public string title;
        public Faction faction;

        /// <summary>宛先の箱（国王/政治家/地方）。LLM が内容から振り分ける想定。</summary>
        public BoxKind box;
        /// <summary>地方箱のスコープキー（方面/星系。中央箱は空）。</summary>
        public string regionKey = "";

        public PetitionOrigin origin = PetitionOrigin.建白;

        /// <summary>執行で呼ぶ効果の識別子（WF の効果レジストリで解決＝直列化可・決定論保存）。</summary>
        public string effectKey = "";

        public PetitionStatus status = PetitionStatus.起案;

        /// <summary>現在この陳情を担いでいる中継者（ICharacter.id・0=なし）。</summary>
        public int carrierId;

        /// <summary>経た中継者の足跡（誰の手を渡ったか＝チ。リレー/列伝・無名の継承）。</summary>
        public readonly List<int> hops = new List<int>();

        /// <summary>伝播の過程で歪められたか（内容の歪みの実体は LLM 層が後付け）。</summary>
        public bool distorted;

        /// <summary>黙殺された後に「正しかった」と判明したか（再浮上＝信認回復のトリガ）。</summary>
        public bool vindicated;

        public Petition() { }

        public Petition(int id, string title, Faction faction, BoxKind box,
            PetitionOrigin origin = PetitionOrigin.建白, string effectKey = "", string regionKey = "")
        {
            this.id = id;
            this.title = title;
            this.faction = faction;
            this.box = box;
            this.origin = origin;
            this.effectKey = effectKey ?? "";
            this.regionKey = regionKey ?? "";
        }
    }
}
