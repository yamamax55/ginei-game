namespace Ginei
{
    /// <summary>役職の権限スコープ（地理的範囲）。国家 ⊇ 方面 ⊇ 星系（上位は下位を包含）。</summary>
    public enum OfficeScope { 国家, 方面, 星系 }

    /// <summary>役職の所掌（何を扱うか）。元首は全所掌を包含する特別枠。</summary>
    public enum OfficeDomain { 軍事, 内政, 外交, 財政, 元首 }

    /// <summary>
    /// 政府の役職（オフィス）の純データ（GOV-1 #142）。「誰が・どこまでの範囲を・何を所掌するか」を表す。
    /// 元首/各大臣/星系総督/官僚など。就任資格（<see cref="civilianOnly"/>/<see cref="militaryOnly"/>/
    /// <see cref="politicalAppointmentOnly"/>/<see cref="requiredTier"/>）を持ち、保持は <see cref="GovernmentRegistry"/>、
    /// 資格・権限の判定は <see cref="OfficeRules"/> が唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Office
    {
        public int id;
        public string officeName;

        /// <summary>権限の地理的範囲（国家/方面/星系）。</summary>
        public OfficeScope scope = OfficeScope.国家;

        /// <summary>所掌（軍事/内政/外交/財政/元首）。</summary>
        public OfficeDomain domain = OfficeDomain.内政;

        /// <summary>
        /// 軍事所掌の役職がどちらの指揮系統か（ゴールドウォーター゠ニコルズの二系統分離・MILGOV-US §3-A）。
        /// 作戦＝部隊を動かす（統合軍司令官型）／管理＝organize-train-equip（参謀総長/軍政型）。
        /// 既定＝管理（＝作戦指揮権を含意しない後方互換）。非軍事所掌では無意味（<see cref="CommandChainRules.ChainOf"/> が管理を返す）。
        /// </summary>
        public CommandChain commandChain = CommandChain.管理;

        /// <summary>文民専用（軍人は就けない）。既定 false＝兼任可。</summary>
        public bool civilianOnly;

        /// <summary>軍人専用（文民は就けない）。既定 false＝兼任可。</summary>
        public bool militaryOnly;

        /// <summary>政治任用専用（政治家＝<see cref="ICharacter.IsPolitician"/> のみ。GOV-6 #159 大臣/元首/議員）。</summary>
        public bool politicalAppointmentOnly;

        /// <summary>任に必要な最小階級 tier（0＝不問。<see cref="RankSystem"/> の tier と整合）。</summary>
        public int requiredTier;

        /// <summary>職業官僚が就ける上限 tier（例＝事務次官級。0＝制限なし。GOV-6 #159）。</summary>
        public int careerCeiling;

        /// <summary>同時に何人就けるか（元首=1、官僚=N）。</summary>
        public int slots = 1;

        public Office() { }

        public Office(int id, string officeName, OfficeScope scope, OfficeDomain domain)
        {
            this.id = id;
            this.officeName = officeName;
            this.scope = scope;
            this.domain = domain;
        }
    }
}
