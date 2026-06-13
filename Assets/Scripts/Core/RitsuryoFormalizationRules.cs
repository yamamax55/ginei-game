using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 律令制の形骸化＝名実の乖離の純ロジック（日本の律令制・官僚制基盤・史実参考）。
    /// 本作の前提は<b>封建制のみが実効</b>で、律令の官職・位階（<see cref="CourtRank"/>/<see cref="Office"/>）は
    /// その上に被さる<b>形式の層</b>。<b>朝廷の権威（<see cref="CourtAuthority"/>）が下がるほど、役職（名）と
    /// 実際の役割（実）は乖離していく</b>＝高い官位ほど名誉職と化し、実権は在地の封建領主（<see cref="Fief"/>）に残る
    /// （史実：摂関政治→院政→武家政権→戦国の官職の空洞化＝官位は権威付けに使われ続けた）。
    /// 武官の <see cref="RankSystem"/>・封建の <see cref="FeudalRules"/> と接続。基準値非破壊（実効値パターン）・
    /// 入力に対し決定的（乱数なし）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RitsuryoFormalizationRules
    {
        /// <summary>名実の乖離の調整値。</summary>
        public readonly struct FormalizationParams
        {
            public readonly float honoraryThreshold; // この権威を下回ると官職は名誉職化（実権ほぼ無し）
            public readonly float prestigeFloor;       // 権威0でも官位が保つ威信の下限（戦国大名も官位を欲した）
            public readonly float officePowerScale;    // 官職tier→実権への換算（封建兵力と足し合わせる単位）
            public readonly int maxTier;               // 乖離正規化の基準tier（位階の最高位＝公卿級）

            public FormalizationParams(float honoraryThreshold, float prestigeFloor, float officePowerScale, int maxTier)
            {
                this.honoraryThreshold = Mathf.Clamp01(honoraryThreshold);
                this.prestigeFloor = Mathf.Clamp01(prestigeFloor);
                this.officePowerScale = officePowerScale;
                this.maxTier = Mathf.Max(1, maxTier);
            }

            /// <summary>既定＝名誉職化0.4・威信下限0.5・官職換算10・基準tier12（公卿級）。</summary>
            public static FormalizationParams Default => new FormalizationParams(0.4f, 0.5f, 10f, 12);
        }

        /// <summary>官職が今なお伴う実権の割合（0..1）＝朝廷の権威に比例。0で官職は実権を持たない。</summary>
        public static float OfficeAuthorityFactor(float courtAuthority) => Mathf.Clamp01(courtAuthority);

        /// <summary>官職（位階 tier）に伴う<b>実際の役割＝実権</b>＝名目 tier×権威。権威が落ちるほど実権は痩せる。</summary>
        public static float EffectiveOfficePower(int nominalTier, float courtAuthority)
            => Mathf.Max(0, nominalTier) * OfficeAuthorityFactor(courtAuthority);

        /// <summary>位階版＝<see cref="CourtRank"/> を tier へ橋渡しして実権を出す。</summary>
        public static float EffectiveOfficePower(CourtRank rank, float courtAuthority)
            => EffectiveOfficePower(JapaneseCourtRankRules.Tier(rank), courtAuthority);

        /// <summary>
        /// 名実の乖離（0..1）＝役職（名）と実際の役割（実）の隔たり。高い官位ほど・朝廷の権威が低いほど大きい。
        /// 1＝高位なのに実権皆無（戦国の名誉官職）、0＝名と実が一致（律令が機能）。
        /// </summary>
        public static float TitleRealityGap(int nominalTier, float courtAuthority, FormalizationParams p)
        {
            float titleNorm = Mathf.Clamp01((float)Mathf.Max(0, nominalTier) / p.maxTier);
            float lostAuthority = 1f - OfficeAuthorityFactor(courtAuthority);
            return Mathf.Clamp01(titleNorm * lostAuthority);
        }

        /// <summary>位階版の名実の乖離。</summary>
        public static float TitleRealityGap(CourtRank rank, float courtAuthority, FormalizationParams p)
            => TitleRealityGap(JapaneseCourtRankRules.Tier(rank), courtAuthority, p);

        /// <summary>官職が名誉職化したか＝朝廷の権威が閾値を下回った（実権が伴わず権威付けのみ）。</summary>
        public static bool IsHonorary(float courtAuthority, FormalizationParams p)
            => courtAuthority < p.honoraryThreshold;

        /// <summary>
        /// 官位の威信（権威付けの価値・0..maxTier×1）。実権（<see cref="EffectiveOfficePower"/>）と違い、
        /// 権威が落ちても下限（<see cref="FormalizationParams.prestigeFloor"/>）まで残る＝実権を失った官位も
        /// 正統性の飾りとして求められ続ける（戦国大名の任官）。
        /// </summary>
        public static float PrestigeValue(int nominalTier, float courtAuthority, FormalizationParams p)
        {
            float retain = p.prestigeFloor + (1f - p.prestigeFloor) * OfficeAuthorityFactor(courtAuthority);
            return Mathf.Max(0, nominalTier) * retain;
        }

        /// <summary>
        /// 実権の総量＝<b>封建の実力（常に有効）＋官職由来の実権（権威で減衰）</b>。
        /// 朝廷の権威が0なら実権＝封建兵力のみ（<b>封建制のみ有効</b>）、権威が高いほど官職が上乗せされる。
        /// </summary>
        public static float RealPower(int officeTier, float feudalStrength, float courtAuthority, FormalizationParams p)
        {
            float feudal = Mathf.Max(0f, feudalStrength);
            float office = p.officePowerScale * EffectiveOfficePower(officeTier, courtAuthority);
            return feudal + office;
        }

        /// <summary>封建領主（<see cref="Fief"/>）の実力に官職の権威を足した実権。位階で官職の格を渡す。</summary>
        public static float RealPower(Fief fief, CourtRank courtRank, float courtAuthority, FormalizationParams p)
        {
            float feudalStrength = fief != null ? Mathf.Max(0, fief.levySize) : 0f;
            return RealPower(JapaneseCourtRankRules.Tier(courtRank), feudalStrength, courtAuthority, p);
        }

        /// <summary>朝廷の権威から形骸化の段階を導く（律令制→…→戦国）。</summary>
        public static RitsuryoPhase PhaseOf(float courtAuthority)
        {
            float a = Mathf.Clamp01(courtAuthority);
            if (a >= 0.8f) return RitsuryoPhase.律令制;
            if (a >= 0.6f) return RitsuryoPhase.摂関政治;
            if (a >= 0.4f) return RitsuryoPhase.院政;
            if (a >= 0.2f) return RitsuryoPhase.武家政権;
            return RitsuryoPhase.戦国;
        }
    }
}
