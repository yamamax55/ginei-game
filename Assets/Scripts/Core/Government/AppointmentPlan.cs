using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 任命最適化（適材適所＝<see cref="AppointmentPlanRules.Plan"/>）の出力＝「誰を・どの空席に就けるか」の一括計画。
    /// 一人一職・一席一人を満たす割当（<see cref="Assignment"/>）の集合と、達成した総適合度を持つ純データ。
    /// 実際の任命（台帳更新）は <see cref="GovernmentRegistry.TryAppoint"/> に委譲し、本型は計画だけを表す
    /// （Core は read-only＝状態を持たない／Game 層が計画を見て任命を実行する）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class AppointmentPlan
    {
        /// <summary>1件の割当（人物→役職枠）。</summary>
        public readonly struct Assignment
        {
            /// <summary>就任する人物。</summary>
            public readonly Person person;

            /// <summary>就く空席（役職＋スコープキー）。</summary>
            public readonly OfficeVacancy vacancy;

            /// <summary>この割当の適合度（0..1＋重み込み＝<see cref="AppointmentPlanRules.FitForOffice"/>×priority）。</summary>
            public readonly float fitness;

            public Assignment(Person person, OfficeVacancy vacancy, float fitness)
            {
                this.person = person;
                this.vacancy = vacancy;
                this.fitness = fitness;
            }
        }

        /// <summary>確定した割当一覧（順序は決定的＝適合度降順→人物 id 昇順で安定）。</summary>
        public readonly List<Assignment> assignments = new List<Assignment>();

        /// <summary>達成した総適合度（割当の <see cref="Assignment.fitness"/> 合計＝計画の良さの目安）。</summary>
        public float totalFitness;

        /// <summary>割り当てられた席の数。</summary>
        public int FilledCount => assignments.Count;
    }
}
