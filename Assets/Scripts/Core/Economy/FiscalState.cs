using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の財政状態（#163 EPIC・#161/#162）。歳入・歳出（利払い除く）・国債残高と、再分配の決断（税率・社会保障水準）を持つ。
    /// 細かい通貨管理は持たず（タイクン化回避）、<b>高位の決断と創発的帰結</b>（債務スパイラル・通貨安・緊縮↔積極財政）を扱う。
    /// 計算は <see cref="FiscalRules"/> が唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class FiscalState
    {
        [Tooltip("歳入（税＋交易。/ターン）")]
        public float revenue;

        [Tooltip("歳出（軍事・内政・社会保障＝利払いを除く。/ターン）")]
        public float baseExpenditure;

        [Tooltip("国債残高（債務）")]
        public float debt;

        [Tooltip("税率（再分配軸・0..1。高税高福祉↔低税低福祉）")]
        public float taxRate = 0.3f;

        [Tooltip("社会保障水準（再分配軸・0..1）")]
        public float welfareLevel = 0.3f;

        public FiscalState() { }

        public FiscalState(float revenue, float baseExpenditure, float debt = 0f)
        {
            this.revenue = revenue;
            this.baseExpenditure = baseExpenditure;
            this.debt = Mathf.Max(0f, debt);
        }
    }
}
