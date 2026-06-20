using UnityEngine;

namespace Ginei
{
    /// <summary>政略結婚（婚姻同盟と血統政治）の調整係数。</summary>
    public readonly struct DynasticMarriageParams
    {
        /// <summary>縁組価値で政治的有用性に寄せる重み（0=血統格のみ／1=有用性のみ）。</summary>
        public readonly float utilityWeight;
        /// <summary>血統格の差が釣り合いを崩す強さ（差×これだけ釣り合いが下がる）。</summary>
        public readonly float imbalancePenalty;
        /// <summary>釣り合いが最低でも残す同盟の絆の床（格差婚でも完全には消えない）。</summary>
        public readonly float balanceFloor;
        /// <summary>世継ぎ正統性で宮廷の承認に寄せる重み（0=血の結合のみ／1=承認のみ）。</summary>
        public readonly float recognitionWeight;
        /// <summary>外戚の台頭が野心で増幅される幅（正統な世継ぎ×野心ほど外戚が伸びる）。</summary>
        public readonly float ambitionScale;
        /// <summary>不和で同盟の絆が蝕まれる強さ（不和×これだけ絆が削れる）。</summary>
        public readonly float discordErosion;

        public DynasticMarriageParams(float utilityWeight, float imbalancePenalty, float balanceFloor,
            float recognitionWeight, float ambitionScale, float discordErosion)
        {
            this.utilityWeight = Mathf.Clamp01(utilityWeight);
            this.imbalancePenalty = Mathf.Max(0f, imbalancePenalty);
            this.balanceFloor = Mathf.Clamp01(balanceFloor);
            this.recognitionWeight = Mathf.Clamp01(recognitionWeight);
            this.ambitionScale = Mathf.Max(0f, ambitionScale);
            this.discordErosion = Mathf.Max(0f, discordErosion);
        }

        /// <summary>
        /// 既定＝有用性重み0.5・格差ペナルティ1.0・釣り合い床0.4・承認重み0.5・野心増幅0.6・不和侵食0.8。
        /// 血統と政略を半々で量り、格差婚でも絆は4割残り、世継ぎは血と承認を半々で正統化、
        /// 外戚は正統な世継ぎの後ろ盾で台頭し、不和は同盟の絆を強く蝕む。
        /// </summary>
        public static DynasticMarriageParams Default
            => new DynasticMarriageParams(0.5f, 1.0f, 0.4f, 0.5f, 0.6f, 0.8f);
    }

    /// <summary>
    /// 政略結婚（婚姻同盟と血統政治の力学）の純ロジック。王侯貴族の縁組は<b>血統の格</b>と<b>政治的有用性</b>で
    /// 価値が決まり、両家の格の<b>釣り合い</b>が崩れると一方が格下扱いとなって絆が痩せる。縁組の価値×釣り合いが
    /// <b>婚姻同盟の絆</b>を生み、両家の血の結合×宮廷の承認が世継ぎの<b>継承正統性</b>を与える。正統な世継ぎは
    /// 母方の一族＝<b>外戚</b>を台頭させ（野心が大きいほど）、個人の不和×政治的軋轢は<b>夫婦の不和</b>を募らせて
    /// 同盟の絆を蝕む（離縁・破綻のリスク）。最後に絆が不和を上回るかで縁組の損得を判ずる。
    /// すべて plain な float で受け渡す。乱数なし・決定論・実効値パターン（入力は壊さずローカルで算出）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DynasticMarriageRules
    {
        /// <summary>
        /// 縁組の価値（0..1）＝両家の血統格の平均と政治的有用性を <see cref="DynasticMarriageParams.utilityWeight"/> で
        /// 混ぜる。名門同士でも政略に乏しければ価値は中庸、低格でも政治的有用性が高ければ価値は上がる。
        /// </summary>
        public static float MatchValue(float lineagePrestigeA, float lineagePrestigeB, float politicalUtility,
            DynasticMarriageParams p)
        {
            float a = Mathf.Clamp01(lineagePrestigeA);
            float b = Mathf.Clamp01(lineagePrestigeB);
            float util = Mathf.Clamp01(politicalUtility);
            float avgPrestige = (a + b) * 0.5f;
            return Mathf.Clamp01(Mathf.Lerp(avgPrestige, util, p.utilityWeight));
        }

        public static float MatchValue(float lineagePrestigeA, float lineagePrestigeB, float politicalUtility)
            => MatchValue(lineagePrestigeA, lineagePrestigeB, politicalUtility, DynasticMarriageParams.Default);

        /// <summary>
        /// 格の釣り合い（0..1）＝両家の血統格が近いほど1、差が大きいほど低い（一方が格下扱い）。
        /// 1 − |格差| × <see cref="DynasticMarriageParams.imbalancePenalty"/>、下限0。
        /// </summary>
        public static float PrestigeBalance(float lineagePrestigeA, float lineagePrestigeB, DynasticMarriageParams p)
        {
            float a = Mathf.Clamp01(lineagePrestigeA);
            float b = Mathf.Clamp01(lineagePrestigeB);
            float gap = Mathf.Abs(a - b);
            return Mathf.Clamp01(1f - gap * p.imbalancePenalty);
        }

        public static float PrestigeBalance(float lineagePrestigeA, float lineagePrestigeB)
            => PrestigeBalance(lineagePrestigeA, lineagePrestigeB, DynasticMarriageParams.Default);

        /// <summary>
        /// 婚姻同盟の絆（0..1）＝縁組の価値 × 釣り合い係数。釣り合いが悪くても床
        /// <see cref="DynasticMarriageParams.balanceFloor"/> までしか落ちない（格差婚でも同盟は一応成る）。
        /// </summary>
        public static float AllianceBond(float matchValue, float prestigeBalance, DynasticMarriageParams p)
        {
            float mv = Mathf.Clamp01(matchValue);
            float bal = Mathf.Clamp01(prestigeBalance);
            float balFactor = Mathf.Lerp(p.balanceFloor, 1f, bal);
            return Mathf.Clamp01(mv * balFactor);
        }

        public static float AllianceBond(float matchValue, float prestigeBalance)
            => AllianceBond(matchValue, prestigeBalance, DynasticMarriageParams.Default);

        /// <summary>
        /// 世継ぎの継承正統性（0..1）＝両家の血の結合と宮廷の承認を
        /// <see cref="DynasticMarriageParams.recognitionWeight"/> で混ぜた値。承認に寄せるほど宮廷の認知が物を言い、
        /// 血に寄せるほど純血が物を言う。どちらかが0でも他方が支えれば完全には潰れない（線形混合）。
        /// </summary>
        public static float HeirLegitimacy(float bloodlineUnion, float recognitionByCourt, DynasticMarriageParams p)
        {
            float blood = Mathf.Clamp01(bloodlineUnion);
            float court = Mathf.Clamp01(recognitionByCourt);
            return Mathf.Clamp01(Mathf.Lerp(blood, court, p.recognitionWeight));
        }

        public static float HeirLegitimacy(float bloodlineUnion, float recognitionByCourt)
            => HeirLegitimacy(bloodlineUnion, recognitionByCourt, DynasticMarriageParams.Default);

        /// <summary>
        /// 外戚の台頭（0..1）＝世継ぎの正統性 × （野心で増幅した後ろ盾）。正統な世継ぎを擁し、母方の一族の野心が
        /// 大きいほど外戚は権勢を伸ばす。世継ぎに正統性が無ければ後ろ盾も無く台頭しない（積形）。
        /// </summary>
        public static float MaternalKinRise(float heirLegitimacy, float maternalAmbition, DynasticMarriageParams p)
        {
            float legit = Mathf.Clamp01(heirLegitimacy);
            float ambition = Mathf.Clamp01(maternalAmbition);
            float ambitionFactor = Mathf.Clamp01(ambition * p.ambitionScale);
            return Mathf.Clamp01(legit * ambitionFactor);
        }

        public static float MaternalKinRise(float heirLegitimacy, float maternalAmbition)
            => MaternalKinRise(heirLegitimacy, maternalAmbition, DynasticMarriageParams.Default);

        /// <summary>
        /// 夫婦の不和（0..1）＝個人の不和と政治的軋轢の悪い方に引っ張られる結合（1−(1−個)(1−政)＝確率的OR）。
        /// どちらか一方でも高ければ不和は募り、両方が低いときだけ静穏に保たれる。
        /// </summary>
        public static float MaritalDiscord(float personalIncompatibility, float politicalStrain, DynasticMarriageParams p)
        {
            float personal = Mathf.Clamp01(personalIncompatibility);
            float political = Mathf.Clamp01(politicalStrain);
            // 確率的 OR：いずれかが高ければ不和。p は将来の調整用に受けるが既定式は素のOR。
            return Mathf.Clamp01(1f - (1f - personal) * (1f - political));
        }

        public static float MaritalDiscord(float personalIncompatibility, float politicalStrain)
            => MaritalDiscord(personalIncompatibility, politicalStrain, DynasticMarriageParams.Default);

        /// <summary>
        /// 不和に蝕まれた残りの絆（0..1）＝同盟の絆 − 絆 × 不和 × <see cref="DynasticMarriageParams.discordErosion"/>。
        /// 強い絆ほど削られる量も大きいが、不和が無ければ絆はそのまま残る（破綻リスクの目安）。
        /// </summary>
        public static float BondDecay(float allianceBond, float maritalDiscord, DynasticMarriageParams p)
        {
            float bond = Mathf.Clamp01(allianceBond);
            float discord = Mathf.Clamp01(maritalDiscord);
            float erosion = bond * discord * p.discordErosion;
            return Mathf.Clamp01(bond - erosion);
        }

        public static float BondDecay(float allianceBond, float maritalDiscord)
            => BondDecay(allianceBond, maritalDiscord, DynasticMarriageParams.Default);

        /// <summary>
        /// 縁組が有利か（true＝絆が不和を上回る）。同盟の絆が夫婦の不和を threshold ぶん超えていれば縁組は得。
        /// 既定 threshold＝0（絆≧不和で有利）。
        /// </summary>
        public static bool IsMarriageAdvantageous(float allianceBond, float maritalDiscord, float threshold)
        {
            float bond = Mathf.Clamp01(allianceBond);
            float discord = Mathf.Clamp01(maritalDiscord);
            return bond - discord >= threshold;
        }

        public static bool IsMarriageAdvantageous(float allianceBond, float maritalDiscord)
            => IsMarriageAdvantageous(allianceBond, maritalDiscord, 0f);
    }
}
