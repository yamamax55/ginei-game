using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 幕僚団（参謀本部）の編制データ（参謀本部基盤）。部隊参謀（艦隊長〜軍団長）と大本営参謀本部の共通の器。
    /// 参謀長（chief of staff）＋機能セクション（人事/情報/作戦/兵站/計画/通信）ごとの担当参謀(id)を持つ。
    /// 数値ロジックは持たず（<see cref="StaffRules"/> が担う）、ここは「誰がどのセクションに就いているか」だけ。
    /// </summary>
    public class Staff
    {
        public Faction faction;
        public StaffLevel level;
        /// <summary>部隊参謀のときの梯団段（艦隊/軍団等）。大本営では未使用。</summary>
        public EchelonType echelon;
        /// <summary>部隊参謀の所属ポスト鍵（例「帝国/第1艦隊」）。大本営は勢力名。</summary>
        public string postKey = "";

        /// <summary>参謀長(chief of staff)の人物id。-1＝空席。幕僚団を統べ協調させる。</summary>
        public int chiefOfStaffId = -1;

        private readonly Dictionary<StaffSection, int> sections = new Dictionary<StaffSection, int>();

        public Staff() { }
        public Staff(Faction faction, StaffLevel level, EchelonType echelon = EchelonType.艦隊, string postKey = "")
        {
            this.faction = faction; this.level = level; this.echelon = echelon; this.postKey = postKey;
        }

        /// <summary>セクションに担当参謀(id)を配属する（-1 で解任）。</summary>
        public void Assign(StaffSection section, int officerId) => sections[section] = officerId;

        /// <summary>セクション担当の人物id（未配属/空席は -1）。</summary>
        public int Officer(StaffSection section)
            => sections.TryGetValue(section, out int id) ? id : -1;

        /// <summary>そのセクションに担当参謀が就いているか。</summary>
        public bool HasSection(StaffSection section) => Officer(section) >= 0;

        /// <summary>担当参謀が就いているセクション数（0〜6）。</summary>
        public int FilledSections
        {
            get
            {
                int n = 0;
                foreach (var kv in sections) if (kv.Value >= 0) n++;
                return n;
            }
        }

        /// <summary>参謀長が就いているか。</summary>
        public bool HasChief => chiefOfStaffId >= 0;
    }
}
