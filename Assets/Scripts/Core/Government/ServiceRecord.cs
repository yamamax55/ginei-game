namespace Ginei
{
    /// <summary>
    /// 兵役・連邦役務の履歴（軍功市民制 STSH-1 #2353・純データ）。完遂した役務（軍/連邦）が
    /// <b>参政権（投票・就任の権利）</b>を授ける＝役務→市民権の契約を表す最小の状態。
    /// 服役政策（STSH-3 <c>ServiceObligationRules</c>）が supply 側でこのレコードを生成する前提。
    /// <see cref="CivicFranchiseRules"/> が判定の唯一の窓口（ここは状態の入れ物）。
    /// </summary>
    [System.Serializable]
    public class ServiceRecord
    {
        /// <summary>対象人物の id（<c>Person.id</c>）。</summary>
        public int personId;
        /// <summary>所属勢力 id。</summary>
        public int factionId;
        /// <summary>服役した年数（非負）。</summary>
        public int yearsServed;
        /// <summary>名誉除隊か（不名誉除隊・脱走は参政権を授けない）。</summary>
        public bool honorableDischarge;
        /// <summary>参政権を付与済みか（<see cref="CivicFranchiseRules"/> が確定する）。</summary>
        public bool isEnfranchised;

        public ServiceRecord() { }

        public ServiceRecord(int personId, int factionId, int yearsServed, bool honorableDischarge)
        {
            this.personId = personId;
            this.factionId = factionId;
            this.yearsServed = yearsServed < 0 ? 0 : yearsServed;
            this.honorableDischarge = honorableDischarge;
            this.isEnfranchised = false;
        }
    }
}
