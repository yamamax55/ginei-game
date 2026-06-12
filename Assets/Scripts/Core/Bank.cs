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

        // ===== バランスシート拡張（BANK #1976・additive・既定0で従来挙動） =====

        [Tooltip("準備金（手元現金＝資産。流動性の源）")]
        public float reserves = 0f;

        [Tooltip("保有有価証券（国債等＝資産。リスク重みは貸出より低い）")]
        public float securities = 0f;

        [Tooltip("不良債権（貸出のうち焦げ付き＝損失の源）")]
        public float nonPerformingLoans = 0f;

        [Tooltip("借入（中央銀行/市場からの調達＝負債）")]
        public float borrowings = 0f;

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
