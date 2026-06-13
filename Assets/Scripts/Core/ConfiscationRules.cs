using UnityEngine;

namespace Ginei
{
    /// <summary>財産没収の調整係数（門閥解体型）。</summary>
    public readonly struct ConfiscationParams
    {
        /// <summary>没収で実際に国庫へ入る割合（執行コスト・横領で目減りする）。</summary>
        public readonly float collectionEfficiency;
        /// <summary>資産家層の反発の最大量（全面没収のとき）。</summary>
        public readonly float backlashScale;
        /// <summary>予告から執行までの遅れ1あたりに逃げる資産の割合（資本は足が速い）。</summary>
        public readonly float flightPerDelay;
        /// <summary>没収が投資意欲を冷やす最大幅（「次は自分だ」と思った資本は動かない）。</summary>
        public readonly float investmentChillScale;

        public ConfiscationParams(float collectionEfficiency, float backlashScale, float flightPerDelay, float investmentChillScale)
        {
            this.collectionEfficiency = Mathf.Clamp01(collectionEfficiency);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.flightPerDelay = Mathf.Clamp01(flightPerDelay);
            this.investmentChillScale = Mathf.Clamp01(investmentChillScale);
        }

        /// <summary>既定＝回収効率0.7・反発0.5・逃避0.1/遅延・投資萎縮0.4。</summary>
        public static ConfiscationParams Default => new ConfiscationParams(0.7f, 0.5f, 0.1f, 0.4f);
    }

    /// <summary>
    /// 財産没収の純ロジック（門閥解体型＝一回性の収奪）。既得権の資産を国庫へ移す＝財政は一気に潤うが、
    /// ①資産家層の忠誠が崩れ（亡命・抵抗）、②予告から執行までの遅れの分だけ資産は逃げ（資本は足が速い）、
    /// ③「次は自分だ」と見た資本が投資を止める長期コストを払う。税による恒常的再分配
    /// （<see cref="RedistributionRules"/>）とは別系統＝一撃の収奪の損益。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ConfiscationRules
    {
        /// <summary>逃げ残った資産＝総資産×（1−逃避率×執行遅延 delay）。即日執行なら全額が残る。</summary>
        public static float RemainingAssets(float totalAssets, float delay, ConfiscationParams p)
        {
            float fled = Mathf.Clamp01(p.flightPerDelay * Mathf.Max(0f, delay));
            return Mathf.Max(0f, totalAssets) * (1f - fled);
        }

        public static float RemainingAssets(float totalAssets, float delay)
            => RemainingAssets(totalAssets, delay, ConfiscationParams.Default);

        /// <summary>
        /// 国庫の収入＝逃げ残った資産×没収範囲 scope(0..1)×回収効率。
        /// 「全部没収」を宣言しても入るのは効率分だけ（執行は漏れる）。
        /// </summary>
        public static float TreasuryGain(float totalAssets, float scope, float delay, ConfiscationParams p)
        {
            return RemainingAssets(totalAssets, delay, p) * Mathf.Clamp01(scope) * p.collectionEfficiency;
        }

        public static float TreasuryGain(float totalAssets, float scope, float delay)
            => TreasuryGain(totalAssets, scope, delay, ConfiscationParams.Default);

        /// <summary>
        /// 資産家層の反発（0..backlashScale）＝没収範囲×層の政治力 eliteStrength(0..1)。
        /// 強い層から取るほど抵抗・亡命・破壊工作が返る。
        /// </summary>
        public static float EliteBacklash(float scope, float eliteStrength, ConfiscationParams p)
        {
            return Mathf.Clamp01(scope) * Mathf.Clamp01(eliteStrength) * p.backlashScale;
        }

        public static float EliteBacklash(float scope, float eliteStrength)
            => EliteBacklash(scope, eliteStrength, ConfiscationParams.Default);

        /// <summary>
        /// 投資萎縮（0..investmentChillScale）＝没収範囲に比例。経済全体の投資・産出係数に
        /// 1−これ を掛ける長期コスト（恐怖は当事者の外へ伝染する）。
        /// </summary>
        public static float InvestmentChill(float scope, ConfiscationParams p)
        {
            return Mathf.Clamp01(scope) * p.investmentChillScale;
        }

        public static float InvestmentChill(float scope) => InvestmentChill(scope, ConfiscationParams.Default);

        /// <summary>
        /// 損益分岐の目安＝国庫収入（正規化＝総資産比）−反発−投資萎縮。
        /// 弱い門閥から速く狭く取るのが最も効率的、を数値で出す。
        /// </summary>
        public static float NetEffect(float totalAssets, float scope, float delay, float eliteStrength, ConfiscationParams p)
        {
            if (totalAssets <= 0f) return 0f;
            float gainRatio = TreasuryGain(totalAssets, scope, delay, p) / totalAssets;
            return gainRatio - EliteBacklash(scope, eliteStrength, p) - InvestmentChill(scope, p);
        }

        public static float NetEffect(float totalAssets, float scope, float delay, float eliteStrength)
            => NetEffect(totalAssets, scope, delay, eliteStrength, ConfiscationParams.Default);
    }
}
