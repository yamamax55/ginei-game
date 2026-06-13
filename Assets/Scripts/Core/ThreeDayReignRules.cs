using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 三日天下の純ロジック（#三日天下・史実＝明智光秀）。教養ある吏僚にして中央（朝廷・幕府）に通じた光秀が、
    /// 主君信長を本能寺で討つ謀反を<b>成功させながら</b>、主殺しゆえ正統性を得られず山崎で秀吉に敗れ<b>三日天下</b>に終わる悲劇を再現：
    /// ①<b>中央の事情にあかるい</b>（運営/情報による政務・外交・朝廷工作の冴え）、
    /// ②<b>謀反の成就</b>（主君が手薄＝本能寺の隙を突けば高確率で成功）、
    /// ③<b>三日天下</b>（主殺しは正統性を失い、与党が応じず＝細川/筒井の不参、急速に瓦解）。
    /// 謀反の成否・帰結は既存 `CoupRules`#215-219 へ橋渡しし、本クラスは光秀型の修正子だけを返す（二重実装しない）。
    /// 実効値パターン・決定論・test-first。
    /// </summary>
    public static class ThreeDayReignRules
    {
        /// <summary>中央の事情にあかるいことによる政務・外交の最大上乗せ（運営/情報100で）。</summary>
        public const float CentralAffairsMax = 0.5f;
        /// <summary>主君が手薄なときの謀反成功率の上乗せ（本能寺の隙）。</summary>
        public const float CoupOpportunityBonus = 0.3f;
        /// <summary>主殺しが失う正統性の割合（三日天下の根因）。</summary>
        public const float RegicideLegitimacyPenalty = 0.8f;
        /// <summary>簒奪した政権の正統性の上限（どれだけ取り繕っても三日天下）。</summary>
        public const float ThreeDayLegitimacyCeiling = 0.2f;

        /// <summary>中央の事情にあかるい＝政務/外交の倍率（運営・情報の平均で上がる）。並は1.0。</summary>
        public static float CentralAffairsFactor(int operation, int intelligence)
        {
            float avg = (Mathf.Clamp(operation, 0, 100) + Mathf.Clamp(intelligence, 0, 100)) / 2f;
            return 1f + avg / 100f * CentralAffairsMax;
        }

        /// <summary>謀反成功率の上乗せ（光秀型かつ主君が手薄＝本能寺の隙）。`CoupRules.CoupSuccessChance` に加える。</summary>
        public static float CoupSuccessBonus(bool isThreeDayRuler, bool lordExposed)
            => (isThreeDayRuler && lordExposed) ? CoupOpportunityBonus : 0f;

        /// <summary>
        /// 謀反後の正統性（光秀型は主殺しゆえ激減し上限でクランプ＝三日天下）。並はそのまま。
        /// `CoupRules.PostCoupLegitimacy` の素値を渡し、光秀型の報いを適用する。
        /// </summary>
        public static float PostCoupLegitimacy(bool isThreeDayRuler, float baseLegitimacy)
        {
            float b = Mathf.Clamp01(baseLegitimacy);
            if (!isThreeDayRuler) return b;
            return Mathf.Min(b * (1f - RegicideLegitimacyPenalty), ThreeDayLegitimacyCeiling);
        }

        /// <summary>三日天下か（光秀型で簒奪後の正統性が上限以下＝与党が応じず短命に瓦解する運命）。</summary>
        public static bool IsThreeDayReign(bool isThreeDayRuler, float postCoupLegitimacy)
            => isThreeDayRuler && postCoupLegitimacy <= ThreeDayLegitimacyCeiling;
    }
}
