namespace Ginei
{
    /// <summary>
    /// ネームド資産が所有者に及ぼす効果の純ロジック（NASSET-5・#2063・集約＝二重計上回避）。
    /// 人物の純収益は <see cref="Person.wealth"/>#2056 へ、国家の純収益は国庫#163 へ流す入力を提供。
    /// 威信は支持#113/正統性へ。<b>液状財産（wealth）と資産時価（value）を分けて持ち、純収益のみ流す＝二重計上しない</b>。test-first。
    /// </summary>
    public static class NamedAssetEffectRules
    {
        /// <summary>人物の全資産の年間純収益合計（wealth#2056 へ流す入力）。</summary>
        public static float PersonAnnualIncome(int personId)
        {
            var list = NamedAssetRegistry.OwnedByPerson(personId);
            float sum = 0f;
            for (int i = 0; i < list.Count; i++) sum += NamedAssetRules.NetAnnualIncome(list[i]);
            return sum;
        }

        /// <summary>国家の全資産の年間純収益合計（国庫#163 へ流す入力）。</summary>
        public static float FactionAnnualIncome(Faction faction)
        {
            var list = NamedAssetRegistry.OwnedByFaction(faction);
            float sum = 0f;
            for (int i = 0; i < list.Count; i++) sum += NamedAssetRules.NetAnnualIncome(list[i]);
            return sum;
        }

        /// <summary>人物の威信合計（支持#113/正統性への寄与）。</summary>
        public static float PersonPrestige(int personId)
        {
            var list = NamedAssetRegistry.OwnedByPerson(personId);
            float sum = 0f;
            for (int i = 0; i < list.Count; i++) sum += NamedAssetRules.PrestigeContribution(list[i]);
            return sum;
        }

        /// <summary>国家の威信合計（正統性への寄与）。</summary>
        public static float FactionPrestige(Faction faction)
        {
            var list = NamedAssetRegistry.OwnedByFaction(faction);
            float sum = 0f;
            for (int i = 0; i < list.Count; i++) sum += NamedAssetRules.PrestigeContribution(list[i]);
            return sum;
        }

        /// <summary>人物の総資産＝液状財産（wealth#2056）＋資産時価合計（二重計上しない別軸の合算）。</summary>
        public static float TotalNetWorthOfPerson(int personId, float liquidWealth)
            => liquidWealth + NamedAssetRegistry.TotalValueOfPerson(personId);
    }
}
