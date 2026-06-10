using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 銀行・信用（CAP-2 #186）の純データ。預金・貸出・準備率（0..1）・信認（0..1）を持つ。
    /// 部分準備銀行制の<b>信用創造</b>と、信認低下による<b>取り付け（bank run）</b>を扱う。
    /// 計算は <see cref="BankRules"/> が唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class Bank
    {
        [Tooltip("預金残高")]
        public float deposits;

        [Tooltip("貸出残高")]
        public float loans;

        [Tooltip("準備率（0..1。準備として手元に残す預金の割合）")]
        public float reserveRatio = 0.1f;

        [Tooltip("信認（0..1。預金者の信頼。低いほど取り付けリスク）")]
        public float confidence = 1f;

        public Bank() { }

        public Bank(float deposits, float loans, float reserveRatio = 0.1f, float confidence = 1f)
        {
            this.deposits = Mathf.Max(0f, deposits);
            this.loans = Mathf.Max(0f, loans);
            this.reserveRatio = Mathf.Clamp01(reserveRatio);
            this.confidence = Mathf.Clamp01(confidence);
        }
    }
}
