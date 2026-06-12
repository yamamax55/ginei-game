namespace Ginei
{
    /// <summary>
    /// 銀行の業態（#2010 BTYP）。規模・営業地域・組織形態・顧客層が異なる：都市銀行（メガバンク＝全国・大企業）／地方銀行
    /// （地域密着・中小企業）／信用金庫（狭域・零細/個人・協同組織）。プロファイルは <see cref="BankTypeRules"/> の一表。
    /// </summary>
    public enum BankType { 都市銀行, 地方銀行, 信用金庫 }
}
