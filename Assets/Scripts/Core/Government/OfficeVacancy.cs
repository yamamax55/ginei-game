namespace Ginei
{
    /// <summary>
    /// 任命最適化（適材適所＝<see cref="AppointmentPlanRules"/>）の入力＝「埋めるべき空席」1件。
    /// <see cref="Office"/>（所掌・必要階級・専用ゲート）に<b>対象スコープキー</b>（星系総督なら星系ID等。国家=0）を
    /// 添えた純データ。資格判定は <see cref="OfficeRules.CanHold"/>・任命台帳は <see cref="GovernmentRegistry"/> に委譲し、
    /// ここでは「どの役職枠を埋めるか」だけを表す（並行レジストリを作らない）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class OfficeVacancy
    {
        /// <summary>埋める役職（所掌・必要階級・専用ゲートの出所）。</summary>
        public Office office;

        /// <summary>対象スコープキー（星系総督なら星系ID等。国家スコープは0）。<see cref="GovernmentRegistry"/> と整合。</summary>
        public int scopeKey;

        /// <summary>
        /// 任命の重み（複数空席を同時に最適化するとき、要職を優先したい場合の重み。既定1＝等価）。
        /// 適合度に乗じてから全体最大化する＝重要な席ほど良い人を取りやすくする。負値は0へクランプ。
        /// </summary>
        public float priority = 1f;

        public OfficeVacancy() { }

        public OfficeVacancy(Office office, int scopeKey = 0, float priority = 1f)
        {
            this.office = office;
            this.scopeKey = scopeKey;
            this.priority = priority;
        }
    }
}
