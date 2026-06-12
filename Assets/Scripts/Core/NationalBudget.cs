using UnityEngine;

namespace Ginei
{
    /// <summary>国家予算の歳出分野（国家予算の基盤）。軍事/建艦/内政/社会保障/研究/外交。配分の読み書きは <see cref="BudgetRules"/>。</summary>
    public enum BudgetCategory { 軍事, 建艦, 内政, 社会保障, 研究, 外交 }

    /// <summary>
    /// 国家予算＝歳出を分野ごとに配分した計画（国家予算の基盤）。<see cref="FiscalState"/> の単一歳出 baseExpenditure を
    /// 分野（軍事/建艦/内政/社会保障/研究/外交）へ分解する＝歳入（国庫/税収）をどこへ振り向けるかという高位の決断の器。
    /// 各分野は /ターン の支出率で、細かい品目管理は持たない（タイクン化回避）。解決は <see cref="BudgetRules"/> が
    /// 唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class NationalBudget
    {
        [Tooltip("軍事（艦隊維持・即応。/ターン）")] public float military;
        [Tooltip("建艦（造船供給 #884。/ターン）")] public float shipbuilding;
        [Tooltip("内政（統治・安定 #109。/ターン）")] public float administration;
        [Tooltip("社会保障（希望 #852。/ターン）")] public float welfare;
        [Tooltip("研究（#123。/ターン）")] public float research;
        [Tooltip("外交（opinion #189。/ターン）")] public float diplomacy;

        public NationalBudget() { }

        public NationalBudget(float military, float shipbuilding, float administration,
                              float welfare, float research, float diplomacy)
        {
            this.military = Mathf.Max(0f, military);
            this.shipbuilding = Mathf.Max(0f, shipbuilding);
            this.administration = Mathf.Max(0f, administration);
            this.welfare = Mathf.Max(0f, welfare);
            this.research = Mathf.Max(0f, research);
            this.diplomacy = Mathf.Max(0f, diplomacy);
        }
    }
}
