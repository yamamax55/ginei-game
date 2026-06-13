namespace Ginei
{
    /// <summary>
    /// 宗教の惑星年次Tick窓口（純ロジック・stateless・実効値パターン）。
    /// <see cref="PopulationDynamicsRules"/> と同型：EnsureReligion→TickYear→SocialFactor の3メソッド構成。
    /// 数値解決は <see cref="ReligionRules"/> に委譲（二重実装しない）。test-first（#172-175）。
    /// </summary>
    public static class ReligionTickRules
    {
        /// <summary>
        /// 惑星に宗教データが無ければ <paramref name="nativeFaith"/> で初期化する（冪等）。
        /// 既に設定されている場合は何もしない＝TickYear 前に必ず呼ぶ。
        /// </summary>
        public static void EnsureReligion(Province prov, string nativeFaith)
        {
            if (prov == null) return;
            if (prov.religion == null)
                prov.religion = new Religion(nativeFaith ?? "");
        }

        /// <summary>
        /// 惑星の住民信仰を1年ぶん進める。
        /// <paramref name="rulerFaithDevotion"/>＝支配勢力の信仰の強さ（0..1）。
        /// <paramref name="affinityMatch"/>＝支配勢力の思想と <see cref="Religion.ideologyAffinity"/> が一致するか。
        /// <see cref="Province.stability"/> 等は変更しない（宗教効果は SocialFactor 経由で読む）。
        /// </summary>
        public static void TickYear(Province prov, float rulerFaithDevotion, bool affinityMatch)
        {
            if (prov == null) return;
            EnsureReligion(prov, "");
            // 1年=deltaTime 1f として改宗を1ステップ進める
            ReligionRules.Tick(prov.religion, rulerFaithDevotion, affinityMatch, 1f, ReligionParams.Default);
        }

        /// <summary>
        /// 社会効果係数（read-only）：信仰の強さが安定度/士気へ与える実効倍率。
        /// <see cref="Province.religion"/> が null の場合は中立値（<see cref="ReligionParams.Default.socialBase"/>）を返す。
        /// </summary>
        public static float SocialFactor(Province prov)
        {
            if (prov == null) return ReligionRules.SocialEffect(null, ReligionParams.Default);
            return ReligionRules.SocialEffect(prov.religion, ReligionParams.Default);
        }
    }
}
