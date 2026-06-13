namespace Ginei
{
    /// <summary>財産の所有形態（私有＝個人/資本家／国有＝国家）。経済資産（企業 <see cref="Enterprise"/> 等）に持たせる。</summary>
    public enum Ownership { 私有, 国有 }

    /// <summary>
    /// 私有財産と国有財産を分けるロジック（#17 共産非対称・#1032 所有・純ロジック・唯一の窓口）。
    /// <b>利潤の行き先</b>＝国有は国庫（#163・赤字は国庫の負担）／私有は所有者（資本家＝格差 #917）。
    /// <b>政体で既定が変わる</b>＝共産は国有・その他は私有。振る舞いの差（国有は雇用を守る）は <see cref="EnterpriseRules"/> が所有形態で分岐。test-first。
    /// </summary>
    public static class PropertyRules
    {
        public static bool IsState(Ownership o) => o == Ownership.国有;

        /// <summary>利潤のうち国庫へ入るぶん（国有＝全額・私有＝0。赤字なら国有は国庫の負担＝負値）。私有の課税は別系統（#163）。</summary>
        public static float ProfitToTreasury(float profit, Ownership o) => o == Ownership.国有 ? profit : 0f;

        /// <summary>利潤のうち所有者（資本家）へ入るぶん（私有＝全額・国有＝0。私有は富の集中＝格差 #917 の源）。</summary>
        public static float ProfitToPrivate(float profit, Ownership o) => o == Ownership.私有 ? profit : 0f;

        /// <summary>政体（住民/勢力の思想）に応じた既定の所有形態（共産＝国有・その他＝私有）。</summary>
        public static Ownership DefaultFor(string ideology)
            => !string.IsNullOrEmpty(ideology) && ideology.Contains("共産") ? Ownership.国有 : Ownership.私有;
    }
}
