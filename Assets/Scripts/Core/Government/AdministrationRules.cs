using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文官の行政＝在任する宰相（文官要職の保持者）の能力が内政（安定度）へ及ぼす寄与の純ロジック
    /// （日本の律令制・官僚制基盤・配線ロジック）。<b>名実の乖離</b>を核に置く：宰相の実効力は
    /// <b>朝廷の権威</b>（<see cref="RitsuryoFormalizationRules.OfficeAuthorityFactor"/>）で減衰する＝
    /// 権威を失った朝廷が任じた宰相は、いかに有能でも実権が伴わず内政を動かせない（封建の世）。
    /// 数値は <see cref="GovernanceRules"/> の安定度目標へ <c>adminBonus</c> として渡す（基準値非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AdministrationRules
    {
        /// <summary>行政寄与の調整値。</summary>
        public readonly struct AdminParams
        {
            public readonly float maxStabilityBonus; // 最高の宰相が朝廷の権威満点のとき与える安定度の上限

            public AdminParams(float maxStabilityBonus) { this.maxStabilityBonus = maxStabilityBonus; }

            /// <summary>既定＝最大+12（安定度ポイント）。</summary>
            public static AdminParams Default => new AdminParams(12f);
        }

        /// <summary>
        /// 行政能力（0..1）と朝廷の権威（0..1）から安定度への寄与を出す。
        /// ＝能力 × 上限 × 権威（名実の乖離＝権威0で寄与0＝官職は名誉職）。
        /// </summary>
        public static float StabilityContribution(float competence, float courtAuthority, AdminParams p)
            => Mathf.Clamp01(competence) * p.maxStabilityBonus
               * RitsuryoFormalizationRules.OfficeAuthorityFactor(courtAuthority);

        /// <summary>
        /// 宰相（<see cref="Person"/>）版。文才（<see cref="Person.CivilAptitude"/>）を行政能力に、考課（実績）で
        /// 実効を上下させる（考第が低い宰相は行政が回らない＝0.5〜1.0倍）。在任者が無ければ0（空席＝寄与なし）。
        /// </summary>
        public static float StabilityContribution(Person premier, float courtAuthority, AdminParams p)
        {
            if (premier == null) return 0f;
            float competence = Mathf.Clamp01(premier.CivilAptitude / 100f);
            float meritFactor = (premier.merit != null && premier.merit.HasRecord)
                ? Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(premier.merit.AverageScore / 9f))
                : 1f;
            return StabilityContribution(competence * meritFactor, courtAuthority, p);
        }
    }
}
