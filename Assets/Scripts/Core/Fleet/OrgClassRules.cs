namespace Ginei
{
    /// <summary>
    /// 部隊の戦略階層区分（ORBAT-4 #1720）。戦略・作戦単位は後方支援を備え<b>自己完結</b>、
    /// 戦術単位は上級へ配属しないと補給を賄えず<b>継戦不可</b>（禁止でなく依存で現実を出す）。
    /// </summary>
    public enum UnitEchelonClass { 戦略, 作戦, 戦術 }

    /// <summary>
    /// 戦略/作戦/戦術単位の区分ロジック（ORBAT-4 #1720・純ロジック・test-first）。
    /// 梯団種別（<see cref="EchelonType"/>）から区分を導出し、戦術単位の継戦依存（上級配属が要る）を表す。
    /// 兵站（#94 <see cref="SupplyRules"/>）と接続する想定＝戦術単位は親梯団の補給に依存する。
    /// ペナルティは<b>実効値パターン</b>（倍率を返すだけ＝基準値は書き換えない）。各所のインライン判定を増やさずここへ集約。
    /// </summary>
    public static class OrgClassRules
    {
        /// <summary>継戦不可（補給を賄えない孤立した戦術単位）の実効ペナルティ倍率（基準値非破壊）。</summary>
        public const float UnsustainedPenaltyFactor = 0.5f;

        /// <summary>
        /// その梯団の戦略階層区分。分艦隊以下＝戦術／艦隊・軍団＝作戦／軍以上＝戦略（現実準拠）。
        /// </summary>
        public static UnitEchelonClass ClassOf(EchelonType echelon)
        {
            switch (echelon)
            {
                case EchelonType.戦隊:
                case EchelonType.分艦隊:
                    return UnitEchelonClass.戦術;
                case EchelonType.艦隊:
                case EchelonType.軍団:
                    return UnitEchelonClass.作戦;
                default: // 軍 / 軍集団 / 宇宙艦隊
                    return UnitEchelonClass.戦略;
            }
        }

        /// <summary>戦略・作戦単位は自己完結（後方支援を備える）。戦術単位は false。</summary>
        public static bool IsSelfSufficient(EchelonType echelon) => ClassOf(echelon) != UnitEchelonClass.戦術;

        /// <summary>戦術単位は上級梯団へ配属しないと継戦不可（補給を賄えない）。</summary>
        public static bool RequiresParentForSustainment(EchelonType echelon)
            => ClassOf(echelon) == UnitEchelonClass.戦術;

        /// <summary>
        /// 継戦できるか＝自己完結（戦略/作戦単位）か、上級梯団へ配属済み（戦術単位）。
        /// #94 兵站の補給可否は配線時に上乗せ（親が補給切れなら別途枯れる）。
        /// </summary>
        public static bool CanSustain(EchelonType echelon, bool hasParentFormation)
            => IsSelfSufficient(echelon) || hasParentFormation;

        /// <summary>梯団ノードの継戦可否（<see cref="MilitaryFormation.parentId"/>＝0 は親なし）。</summary>
        public static bool CanSustain(MilitaryFormation formation)
            => formation != null && CanSustain(formation.echelon, formation.parentId != 0);

        /// <summary>
        /// 継戦ペナルティ倍率（実効値パターン）。賄えていれば 1.0、孤立した戦術単位は <see cref="UnsustainedPenaltyFactor"/>。
        /// 士気・兵力など実効能力へ乗算する想定（基準値は書き換えない）。
        /// </summary>
        public static float SustainmentFactor(EchelonType echelon, bool hasParentFormation)
            => CanSustain(echelon, hasParentFormation) ? 1f : UnsustainedPenaltyFactor;

        /// <summary>梯団ノードの継戦ペナルティ倍率。</summary>
        public static float SustainmentFactor(MilitaryFormation formation)
            => formation == null ? 1f : SustainmentFactor(formation.echelon, formation.parentId != 0);
    }
}
