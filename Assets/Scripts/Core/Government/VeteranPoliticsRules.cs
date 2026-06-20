using UnityEngine;

namespace Ginei
{
    /// <summary>退役軍人政治の調整係数。</summary>
    public readonly struct VeteranPoliticsParams
    {
        /// <summary>未組織でも残る政治的重みの下限（0..1）。戦友会が無くても退役兵は消えない。</summary>
        public readonly float organizationFloor;
        /// <summary>恩給負担のスケール（人口比1.0×満額支給での財政負担）。</summary>
        public readonly float pensionCostScale;
        /// <summary>冷遇不満の基礎重み（0..1）。健常な退役兵でも冷遇は不満になる。</summary>
        public readonly float neglectBase;
        /// <summary>傷痍軍人比による不満の加重（0..1）。傷痍軍人を見捨てるほど信を失う。</summary>
        public readonly float woundedGrievanceWeight;
        /// <summary>満額恩給での忠誠配当の上限（現役の士気・忠誠への加算分）。</summary>
        public readonly float maxLoyaltyDividend;

        public VeteranPoliticsParams(float organizationFloor, float pensionCostScale, float neglectBase,
            float woundedGrievanceWeight, float maxLoyaltyDividend)
        {
            this.organizationFloor = Mathf.Clamp01(organizationFloor);
            this.pensionCostScale = Mathf.Max(0f, pensionCostScale);
            this.neglectBase = Mathf.Clamp01(neglectBase);
            this.woundedGrievanceWeight = Mathf.Clamp01(woundedGrievanceWeight);
            this.maxLoyaltyDividend = Mathf.Max(0f, maxLoyaltyDividend);
        }

        /// <summary>既定＝組織下限0.2・恩給スケール0.5・不満基礎0.5・傷痍加重0.5・忠誠配当上限0.2。</summary>
        public static VeteranPoliticsParams Default => new VeteranPoliticsParams(0.2f, 0.5f, 0.5f, 0.5f, 0.2f);
    }

    /// <summary>
    /// 退役軍人の政治力の純ロジック。兵士は除隊しても消えない＝処遇が次の政治を作る。
    /// 戦友会（在郷軍人会）に組織された退役兵は圧力団体となり、手厚い恩給は財政を食い（<see cref="PensionBurden"/>）、
    /// 冷遇は街頭の不満（<see cref="NeglectGrievance"/>→<see cref="StreetPower"/>＝ボーナスアーミー型）になる。
    /// 武勇の記憶は好戦ロビー（<see cref="MilitarismLobby"/>）として文民統制を圧迫し、
    /// 一方で「兵を捨てない国」の評判は現役の忠誠に跳ね返る（<see cref="LoyaltyDividend"/>）。
    /// <see cref="RetirementRules"/>（個人の退役年限・恩給率）とは別＝こちらは退役兵**集団**の政治力を扱う。
    /// 倍率・係数は基準値に掛けて/足して使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class VeteranPoliticsRules
    {
        /// <summary>
        /// 圧力団体としての実効規模（0..1）＝退役兵人口比×組織率。
        /// 組織率0でも <see cref="VeteranPoliticsParams.organizationFloor"/> の重みは残る
        /// （組織されない退役兵も票と街頭には居る）。
        /// </summary>
        public static float VeteranBlocSize(float veteranShare, float organizationRate, VeteranPoliticsParams p)
        {
            float share = Mathf.Clamp01(veteranShare);
            float org = Mathf.Clamp01(organizationRate);
            return share * Mathf.Lerp(p.organizationFloor, 1f, org);
        }

        public static float VeteranBlocSize(float veteranShare, float organizationRate)
            => VeteranBlocSize(veteranShare, organizationRate, VeteranPoliticsParams.Default);

        /// <summary>
        /// 恩給の財政負担＝退役兵人口比×支給の手厚さ×スケール。
        /// 歳出側の係数として <c>FiscalRules</c> 系の expenditure へ掛け込む想定（厚遇はタダではない）。
        /// </summary>
        public static float PensionBurden(float veteranShare, float pensionGenerosity, VeteranPoliticsParams p)
        {
            return Mathf.Clamp01(veteranShare) * Mathf.Clamp01(pensionGenerosity) * p.pensionCostScale;
        }

        public static float PensionBurden(float veteranShare, float pensionGenerosity)
            => PensionBurden(veteranShare, pensionGenerosity, VeteranPoliticsParams.Default);

        /// <summary>
        /// 冷遇の不満（0..1）＝(1−支給の手厚さ)×(基礎重み＋傷痍加重×傷痍軍人比)。
        /// 満額支給なら0。傷痍軍人を見捨てる国ほど不満は深い＝信を失う。
        /// </summary>
        public static float NeglectGrievance(float pensionGenerosity, float woundedShare, VeteranPoliticsParams p)
        {
            float neglect = 1f - Mathf.Clamp01(pensionGenerosity);
            float weight = p.neglectBase + p.woundedGrievanceWeight * Mathf.Clamp01(woundedShare);
            return Mathf.Clamp01(neglect * weight);
        }

        public static float NeglectGrievance(float pensionGenerosity, float woundedShare)
            => NeglectGrievance(pensionGenerosity, woundedShare, VeteranPoliticsParams.Default);

        /// <summary>
        /// 街頭動員力（0..1）＝実効規模×不満。組織された不満だけが街頭に出る（ボーナスアーミー型）。
        /// 支持・安定度系の減点係数として使う想定。
        /// </summary>
        public static float StreetPower(float blocSize, float grievance)
        {
            return Mathf.Clamp01(blocSize) * Mathf.Clamp01(grievance);
        }

        /// <summary>
        /// 好戦ロビー圧力（0..1）＝実効規模×武勇の記憶（戦勝ノスタルジア）。
        /// 武勇の記憶が政治力を持つ＝文民統制（<c>CivilianControlRules</c>）への圧力として読む想定。
        /// </summary>
        public static float MilitarismLobby(float blocSize, float warNostalgia)
        {
            return Mathf.Clamp01(blocSize) * Mathf.Clamp01(warNostalgia);
        }

        /// <summary>
        /// 厚遇の配当（0..maxLoyaltyDividend）＝支給の手厚さに比例した現役の士気・忠誠ボーナス。
        /// 「あの国は兵を捨てない」という評判は今戦う兵を強くする（加算で使う・基準非破壊）。
        /// </summary>
        public static float LoyaltyDividend(float pensionGenerosity, VeteranPoliticsParams p)
        {
            return Mathf.Clamp01(pensionGenerosity) * p.maxLoyaltyDividend;
        }

        public static float LoyaltyDividend(float pensionGenerosity)
            => LoyaltyDividend(pensionGenerosity, VeteranPoliticsParams.Default);
    }
}
