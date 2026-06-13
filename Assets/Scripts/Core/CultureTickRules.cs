namespace Ginei
{
    /// <summary>
    /// 文化・民族の年次 Tick を惑星内政（<see cref="Province"/>）へ接続する純ロジック（#194・配線層）。
    /// 既存の <see cref="CultureRules"/>（同化・分離独立・ナショナリズムの数値モデル）を
    /// <see cref="Province.culture"/> フィールドへ Ensure → 年次 Tick する唯一の窓口。
    /// <see cref="Province.stability"/> 等の基準値は変更しない（実効値パターン・基準非破壊）。
    /// GalaxyView の年境界（onYear）で全 Province を回す想定。test-first。
    /// </summary>
    public static class CultureTickRules
    {
        /// <summary>
        /// 惑星に <see cref="Culture"/> が無ければ <paramref name="nativeCulture"/> 名で初期化する（冪等）。
        /// 既に設定済みなら何もしない。
        /// </summary>
        /// <param name="prov">対象惑星。null なら何もしない。</param>
        /// <param name="nativeCulture">住民の本来の文化名。</param>
        public static void EnsureCulture(Province prov, string nativeCulture)
        {
            if (prov == null) return;
            if (prov.culture != null) return; // 冪等＝既に設定済みなら変えない

            prov.culture = new Culture(nativeCulture ?? "");
        }

        /// <summary>
        /// 惑星の文化状態を1年ぶん進める。EnsureCulture を内部で呼ぶので呼び出し側は省略可。
        /// <see cref="CultureRules.Tick"/> に deltaTime=1（年）を渡すだけ＝数値ロジックを重複実装しない。
        /// <see cref="Province.stability"/> などの基準値は変更しない（読み取り専用で利用）。
        /// </summary>
        /// <param name="prov">対象惑星。null なら何もしない。</param>
        /// <param name="dominantCultureMatch">
        /// true＝この惑星の住民文化が支配勢力の多数派文化と一致 → 同化が進む。
        /// false＝異文化支配下 → 同化が進まない（分離独立リスクは prov.stability で別途判定）。
        /// </param>
        /// <param name="atWar">戦時は同化速度が鈍る（<see cref="CultureParams.warAssimilationPenalty"/>）。</param>
        public static void TickYear(Province prov, bool dominantCultureMatch, bool atWar)
        {
            if (prov == null) return;
            EnsureCulture(prov, prov.nativeIdeology); // nativeIdeology を文化名の既定として流用
            CultureRules.Tick(prov.culture, dominantCultureMatch, atWar, 1f);
        }

        /// <summary>
        /// 惑星の少数民族・分離独立リスク(0..1)。read-only（<see cref="Province"/> を変更しない）。
        /// <see cref="Province.culture"/> が null なら 0（リスクなし）。
        /// </summary>
        /// <param name="prov">対象惑星。null なら 0。</param>
        public static float SeparatismRisk(Province prov)
        {
            if (prov == null || prov.culture == null) return CultureRules.NoSeparatism;
            return CultureRules.SeparatismRisk(prov.culture, prov.stability);
        }

        /// <summary>
        /// 惑星住民のナショナリズムが結束/士気に与える実効係数（基準 1.0 = 中立）。read-only。
        /// 同化が低い少数民族ほど 1 を超える（防衛結束ボーナス）。
        /// <see cref="Province.culture"/> が null（未配線）なら 0（係数無効・未配線を明示）。
        /// </summary>
        /// <param name="prov">対象惑星。null または culture 未配線なら 0。</param>
        public static float NationalismFactor(Province prov)
        {
            if (prov == null || prov.culture == null) return 0f;
            return CultureRules.NationalismFactor(prov.culture);
        }
    }
}
