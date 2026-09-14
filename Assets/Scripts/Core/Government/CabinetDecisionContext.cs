using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 閣僚の決裁権限を判定するための盤面の束（<see cref="CabinetDecisionAuthorityRules"/> へ渡す）。
    /// 在任・委任は <see cref="PoliticsState.cabinet"/> が単一の出所＝ここは参照を束ねるだけで状態を持たない。
    /// 決裁の時点で毎回組み直す（権限を握ったまま持ち歩かない）。純データ。
    /// </summary>
    public sealed class CabinetDecisionContext
    {
        /// <summary>その勢力の政治状態（内閣・政府・党）。</summary>
        public readonly PoliticsState politics;
        public readonly Faction faction;
        /// <summary>その勢力の省庁ツリー（大臣を置く省＝最上位の直下）。</summary>
        public readonly IList<Ministry> ministries;
        /// <summary>最上位の省 id（負＝最上位の省を大臣の省とみなす）。</summary>
        public readonly int topMinistryId;
        /// <summary>在任者の実在・生存・勢力を確かめる人物名簿（軍人＋文民）。</summary>
        public readonly IList<Person> roster;
        /// <summary>決裁時点の暦年（委任・職務執行の期限判定）。</summary>
        public readonly int year;

        public CabinetDecisionContext(PoliticsState politics, Faction faction, IList<Ministry> ministries, int topMinistryId,
            IList<Person> roster, int year)
        {
            this.politics = politics;
            this.faction = faction;
            this.ministries = ministries;
            this.topMinistryId = topMinistryId;
            this.roster = roster;
            this.year = year;
        }

        /// <summary>内閣が置かれているか（判定に使える状態か）。</summary>
        public bool HasCabinet => politics != null && politics.cabinet != null && politics.cabinet.posts != null;
    }
}
