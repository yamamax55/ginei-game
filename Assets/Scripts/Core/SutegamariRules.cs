using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 島津の捨てがまり（敗走時の殿〔しんがり〕戦術・史実準拠）の純ロジック。
    /// 旗艦が撃破されかけた（敗走）とき、配下艦が身を捨てて殿を務め旗艦（提督）の離脱を助けるか、
    /// それとも散り散りに逃げるかは<b>提督と部下の関係性</b>で決まる。
    /// 関ヶ原の退き口で島津義弘の配下が次々と座り込んで死兵となり主君を逃がした史実が下敷き。
    ///
    /// 関係性＝部下の献身は、提督の<b>能力（統率）</b>と<b>部下への態度（humility＝謙虚さ／尊大さの逆）</b>の両方を要する。
    /// <b>無能な提督</b>（統率低）でも<b>尊大な提督</b>（humility低）でも献身は崩れ、配下は捨てがまりをせず散る。
    /// 純ロジック・test-first。
    /// </summary>
    public static class SutegamariRules
    {
        /// <summary>捨てがまりが発動する献身度の閾値。</summary>
        public const float SacrificeThreshold = 0.45f;

        /// <summary>
        /// 部下の献身度（0..1）＝捨てがまりを行う意志。統率と部下への態度(humility)の<b>幾何平均</b>。
        /// どちらかが低いと大きく下がる（無能 or 尊大なら散る）＝両方を要する。入力は 0..100。
        /// </summary>
        public static float Devotion(float leadership, float humility)
        {
            float L = Mathf.Clamp01(leadership / 100f);
            float H = Mathf.Clamp01(humility / 100f);
            return Mathf.Sqrt(Mathf.Max(0f, L * H));
        }

        /// <summary>配下艦が捨てがまり（殿）を行うか＝献身が閾値以上。</summary>
        public static bool WillPerformSutegamari(float devotion, float threshold = SacrificeThreshold)
            => devotion >= threshold;

        /// <summary>散り散りに逃げる配下艦の割合（0..1）。献身が低いほど多くが逃散する。</summary>
        public static float ScatterFraction(float devotion) => Mathf.Clamp01(1f - Mathf.Clamp01(devotion));

        /// <summary>捨てがまりが旗艦の離脱を成功させる度合い（0..1・献身に比例）。被ダメ軽減等に転用可。</summary>
        public static float CoverFactor(float devotion) => Mathf.Clamp01(devotion);
    }
}
