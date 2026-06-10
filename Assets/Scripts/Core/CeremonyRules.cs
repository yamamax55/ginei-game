using UnityEngine;

namespace Ginei
{
    /// <summary>儀礼・式典の調整係数（戴冠・凱旋・観艦式）。</summary>
    public readonly struct CeremonyParams
    {
        /// <summary>盛大さ最大の式典が正統性に返す最大ボーナス。</summary>
        public readonly float legitimacyScale;
        /// <summary>盛大さ最大の式典が士気に返す最大ボーナス。</summary>
        public readonly float moraleScale;
        /// <summary>盛大さ1あたりの財政コスト。</summary>
        public readonly float costPerGrandeur;
        /// <summary>空疎な式典と判定される情勢の閾値（warSituation がこれ未満＝敗勢での豪華な式典は逆効果）。</summary>
        public readonly float hollowThreshold;

        public CeremonyParams(float legitimacyScale, float moraleScale, float costPerGrandeur, float hollowThreshold)
        {
            this.legitimacyScale = Mathf.Max(0f, legitimacyScale);
            this.moraleScale = Mathf.Max(0f, moraleScale);
            this.costPerGrandeur = Mathf.Max(0f, costPerGrandeur);
            this.hollowThreshold = Mathf.Clamp01(hollowThreshold);
        }

        /// <summary>既定＝正統性0.15・士気0.2・費用100/盛大さ・空疎閾値0.3。</summary>
        public static CeremonyParams Default => new CeremonyParams(0.15f, 0.2f, 100f, 0.3f);
    }

    /// <summary>
    /// 儀礼・式典の純ロジック（戴冠・凱旋・観艦式）。盛大な演出は正統性と士気を買うが財政を食い、
    /// 効果は情勢（warSituation 0..1）に裏打ちされる＝勝っている政権の凱旋は輝き、敗勢下の豪華な式典は
    /// 空疎で逆効果（現実との落差が露呈する）。天命の実体は <see cref="DynastyRules"/>、殉教の動員は
    /// バックログ別テーマが扱い、ここは演出の損益のみ。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CeremonyRules
    {
        /// <summary>式典の財政コスト＝盛大さ(0..1)×単価。</summary>
        public static float Cost(float grandeur, CeremonyParams p)
        {
            return Mathf.Clamp01(grandeur) * p.costPerGrandeur;
        }

        public static float Cost(float grandeur) => Cost(grandeur, CeremonyParams.Default);

        /// <summary>空疎な式典か＝情勢が閾値未満なのに盛大さが情勢を上回る（実態なき演出）。</summary>
        public static bool IsHollow(float grandeur, float warSituation, CeremonyParams p)
        {
            float situation = Mathf.Clamp01(warSituation);
            return situation < p.hollowThreshold && Mathf.Clamp01(grandeur) > situation;
        }

        public static bool IsHollow(float grandeur, float warSituation)
            => IsHollow(grandeur, warSituation, CeremonyParams.Default);

        /// <summary>
        /// 式典の正統性効果。健全なら盛大さ×情勢×scale のプラス、空疎なら盛大さに比例した
        /// マイナス（豪華なほど落差が痛い）。
        /// </summary>
        public static float LegitimacyEffect(float grandeur, float warSituation, CeremonyParams p)
        {
            float g = Mathf.Clamp01(grandeur);
            if (IsHollow(g, warSituation, p))
                return -g * p.legitimacyScale; // 演出と現実の落差が露呈＝逆効果
            return g * Mathf.Clamp01(warSituation) * p.legitimacyScale;
        }

        public static float LegitimacyEffect(float grandeur, float warSituation)
            => LegitimacyEffect(grandeur, warSituation, CeremonyParams.Default);

        /// <summary>式典の士気効果（同じ形・士気スケール）。</summary>
        public static float MoraleEffect(float grandeur, float warSituation, CeremonyParams p)
        {
            float g = Mathf.Clamp01(grandeur);
            if (IsHollow(g, warSituation, p))
                return -g * p.moraleScale;
            return g * Mathf.Clamp01(warSituation) * p.moraleScale;
        }

        public static float MoraleEffect(float grandeur, float warSituation)
            => MoraleEffect(grandeur, warSituation, CeremonyParams.Default);

        /// <summary>
        /// 費用対効果（正統性＋士気の合計効果÷コスト）。コスト0（やらない）は0。
        /// 空疎な式典は負＝「やらないほうがまし」が数値で出る。
        /// </summary>
        public static float Efficiency(float grandeur, float warSituation, CeremonyParams p)
        {
            float cost = Cost(grandeur, p);
            if (cost <= 0f) return 0f;
            return (LegitimacyEffect(grandeur, warSituation, p) + MoraleEffect(grandeur, warSituation, p)) / cost;
        }

        public static float Efficiency(float grandeur, float warSituation)
            => Efficiency(grandeur, warSituation, CeremonyParams.Default);
    }
}
