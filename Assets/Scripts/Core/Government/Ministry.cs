using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 行政機構＝省庁の純データ（GOV-5 #158・日本の省庁制を参考）。役職(<see cref="Office"/> #142)を束ねる
    /// institution＝軍の「軍団⊃艦隊(#147/#146)」の<b>文民版（行政の編制ツリー）</b>。省 ⊃ 庁/局 ⊃ 課 の入れ子で、
    /// 官僚（文民 #143）を配属する。戦時・危機には臨時省庁（<see cref="isTemporary"/>）を新設できる。
    /// 省益（<see cref="institutionalInterest"/>＝縦割り・横断政策への抵抗）を持つ＝内部勢力 #113 の一種。
    /// ツリー操作・配属の解決は <see cref="MinistryRules"/> が唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class Ministry
    {
        public int id;
        public string ministryName;

        /// <summary>所掌（軍事/内政/外交/財政…・<see cref="OfficeDomain"/>）。</summary>
        public OfficeDomain domain = OfficeDomain.内政;

        /// <summary>上位省庁の id（-1＝最上位＝省）。単一親（中身流動・司令部固定と同方針）。</summary>
        public int parentId = -1;

        /// <summary>下位庁/局の id。</summary>
        public List<int> childIds = new List<int>();

        /// <summary>長＝大臣/長官ポスト（<see cref="Office.id"/>。-1＝未設定）。</summary>
        public int headOfficeId = -1;

        /// <summary>配属定員。</summary>
        public int staffSlots = 8;

        /// <summary>臨時官庁か（軍需省・復興院等。存続条件・寿命を持ち役目を終えたら廃止）。</summary>
        public bool isTemporary;

        /// <summary>省益（0..1・縦割り強度）。高いほど横断政策に抵抗し主導権を争う＝政治ドラマの源。</summary>
        public float institutionalInterest = 0.5f;

        /// <summary>配属官僚（<see cref="Person.id"/>）。</summary>
        public List<int> staffIds = new List<int>();

        public Ministry() { }

        public Ministry(int id, string ministryName, OfficeDomain domain)
        {
            this.id = id;
            this.ministryName = ministryName;
            this.domain = domain;
        }

        /// <summary>最上位の省か（親なし）。</summary>
        public bool IsTopLevel => parentId < 0;

        /// <summary>配属に空きがあるか。</summary>
        public bool HasVacancy => staffIds.Count < staffSlots;
    }
}
