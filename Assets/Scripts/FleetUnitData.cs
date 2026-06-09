using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊ユニットの在役状態（#146）。現役のみ編成可・永久欠番は番号再利用不可。</summary>
    public enum FleetStatus { 現役, 解隊, 永久欠番 }

    /// <summary>
    /// 艦隊ユニット（#146 フリートナンバー）。「提督＝艦隊」を
    /// 「艦隊ユニット(番号・勢力・編制) ＋ 配属された提督(指揮官)」へ分離する核となる永続データ。
    /// 番号払い出し・配属・解隊・永久欠番の管理は <see cref="FleetRoster"/>(static) が唯一の窓口。
    /// ★会戦中の艦在庫 <see cref="FleetRegistry"/>（ランタイム）とは別物。流用・拡張しない。
    /// 番号未指定の既存シナリオは従来どおり提督名のみで動作（後方互換）。
    /// </summary>
    public class FleetUnitData : ScriptableObject
    {
        [Tooltip("艦隊番号（勢力内で一意。例 13＝第13艦隊）")]
        public int fleetNumber;
        [Tooltip("艦隊の固有名（無ければ「第N艦隊」を自動表示）")]
        public string fleetName = "";
        [Tooltip("所属勢力（旧 enum。番号体系は勢力ごとに独立）")]
        public Faction faction = Faction.帝国;
        [Tooltip("所属勢力データ（多勢力対応・任意）")]
        public FactionData factionData;
        [Tooltip("運用区分（#128/#883・戦闘/非戦闘）。既定=戦闘艦。梯団は戦闘艦隊と非戦闘艦隊を混成できない（ShipRoleRules）")]
        public ShipRole shipRole = ShipRole.戦闘艦;
        [Tooltip("配属中の指揮官（null＝空席）。提督が代わってもユニットは残る")]
        public AdmiralData assignedAdmiral;
        [Tooltip("ユニット既定兵力（0＝提督側 baseStrength を使う＝後方互換）")]
        public int baseStrength;
        [Tooltip("初期陣形")]
        public Formation formation = Formation.紡錘陣;
        [Tooltip("功績の累積（永久欠番判定に使用）")]
        public int meritScore;
        [Tooltip("在役状態（現役/解隊/永久欠番）")]
        public FleetStatus status = FleetStatus.現役;

        public bool IsActive => status == FleetStatus.現役;
        public bool HasAdmiral => assignedAdmiral != null;

        /// <summary>表示名（固有名があればそれ、無ければ「第N艦隊」）。</summary>
        public string DisplayName => !string.IsNullOrEmpty(fleetName) ? fleetName : $"第{fleetNumber}艦隊";
    }
}
