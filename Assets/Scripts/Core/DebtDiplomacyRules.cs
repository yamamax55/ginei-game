using UnityEngine;

namespace Ginei
{
    /// <summary>債務外交の調整係数。</summary>
    public readonly struct DebtDiplomacyParams
    {
        /// <summary>レバレッジが生じ始める債務比率（債務/借り手経済規模）。これ以下は「返せる借金」＝力にならない。</summary>
        public readonly float leverageThreshold;
        /// <summary>レバレッジが最大(1)に達する債務比率。閾値より必ず大きい。</summary>
        public readonly float leverageSaturation;
        /// <summary>罠融資の規模スケール（この融資額で罠度の規模成分が飽和する）。</summary>
        public readonly float trapLoanScale;
        /// <summary>レバレッジそのものが生む反発の係数（首根っこを掴まれている自覚）。</summary>
        public readonly float resentmentLeverageScale;
        /// <summary>差し押さえ1件あたりの反発の増分（取り立てるほど嫌われる）。</summary>
        public readonly float resentmentPerSeizure;
        /// <summary>逆カードの債務スケール（この債務額でデフォルトカードの規模成分が飽和する）。</summary>
        public readonly float cardDebtScale;

        public DebtDiplomacyParams(float leverageThreshold, float leverageSaturation, float trapLoanScale,
            float resentmentLeverageScale, float resentmentPerSeizure, float cardDebtScale)
        {
            this.leverageThreshold = Mathf.Max(0f, leverageThreshold);
            this.leverageSaturation = Mathf.Max(this.leverageThreshold + 0.001f, leverageSaturation);
            this.trapLoanScale = Mathf.Max(0.001f, trapLoanScale);
            this.resentmentLeverageScale = Mathf.Max(0f, resentmentLeverageScale);
            this.resentmentPerSeizure = Mathf.Max(0f, resentmentPerSeizure);
            this.cardDebtScale = Mathf.Max(0.001f, cardDebtScale);
        }

        /// <summary>既定＝レバレッジ閾値0.5・飽和2.0・罠融資スケール100・反発係数0.3・差押反発0.2・逆カードスケール100。</summary>
        public static DebtDiplomacyParams Default => new DebtDiplomacyParams(0.5f, 2f, 100f, 0.3f, 0.2f, 100f);
    }

    /// <summary>
    /// 債務外交の純ロジック＝対外債権の武器化。貸し込んだ債権が政治的レバレッジになり、返せない借り手は
    /// 港や基地を差し出す（99年租借型）。一方で大きすぎる債務は貸し手自身の問題になる＝借り手は
    /// デフォルトという逆カードを握る（「銀行に1兆借りれば銀行が人質」）＝債務は双方向の武器。
    /// <see cref="BankRules"/>（国内の信用創造・取付け）・<see cref="FiscalRules"/>（自国財政・国債・金利）
    /// とは別系統＝ここは勢力間の債権がもたらす政治力のみを扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DebtDiplomacyRules
    {
        /// <summary>
        /// 債権者が握る政治レバレッジ（0..1）。債務比率＝債務/借り手経済規模が閾値以下なら0
        /// （返せる借金は力にならない）、閾値から飽和点まで線形に増え、超えれば1（返せない借金が力になる）。
        /// 経済規模が0以下で債務があれば即レバレッジ最大。
        /// </summary>
        public static float DebtLeverage(float debtToCreditor, float debtorEconomy, DebtDiplomacyParams p)
        {
            float debt = Mathf.Max(0f, debtToCreditor);
            if (debt <= 0f) return 0f;
            if (debtorEconomy <= 0f) return 1f;
            float ratio = debt / debtorEconomy;
            return Mathf.Clamp01((ratio - p.leverageThreshold) / (p.leverageSaturation - p.leverageThreshold));
        }

        public static float DebtLeverage(float debtToCreditor, float debtorEconomy)
            => DebtLeverage(debtToCreditor, debtorEconomy, DebtDiplomacyParams.Default);

        /// <summary>
        /// 差し押さえ要求の通り具合（0..1）＝レバレッジ×戦略資産価値(0..1)。
        /// 返済不能の債務国に価値ある港・基地があるとき、債務減免と引き換えの租借が成立する。
        /// レバレッジが無ければ何も取れず、資産が無ければ取る物がない。
        /// </summary>
        public static float AssetSeizureValue(float leverage, float strategicAssets)
        {
            return Mathf.Clamp01(leverage) * Mathf.Clamp01(strategicAssets);
        }

        /// <summary>
        /// 罠としての融資設計度（0..1）＝融資規模(trapLoanScaleで飽和)×（1−事業採算性(0..1)）。
        /// 採算の取れない事業への大型融資ほど「返せない借金」を意図的に作る罠（ハンバントタ型）。
        /// 健全な事業への融資は罠度0。
        /// </summary>
        public static float DebtTrapDesign(float loanSize, float projectViability, DebtDiplomacyParams p)
        {
            float scale = Mathf.Clamp01(Mathf.Max(0f, loanSize) / p.trapLoanScale);
            return scale * (1f - Mathf.Clamp01(projectViability));
        }

        public static float DebtTrapDesign(float loanSize, float projectViability)
            => DebtTrapDesign(loanSize, projectViability, DebtDiplomacyParams.Default);

        /// <summary>
        /// 借り手側の民族主義的反発（0..1）＝レバレッジ×係数＋差し押さえ件数×増分。
        /// 首根っこを掴むだけでも嫌われ、実際に取り立てる（港を取る）たびに跳ね上がる。
        /// 支持・反乱圧力（#113/#109）への係数として使う想定。
        /// </summary>
        public static float DebtorResentment(float leverage, int seizures, DebtDiplomacyParams p)
        {
            float fromLeverage = Mathf.Clamp01(leverage) * p.resentmentLeverageScale;
            float fromSeizures = Mathf.Max(0, seizures) * p.resentmentPerSeizure;
            return Mathf.Clamp01(fromLeverage + fromSeizures);
        }

        public static float DebtorResentment(float leverage, int seizures)
            => DebtorResentment(leverage, seizures, DebtDiplomacyParams.Default);

        /// <summary>
        /// 借り手が握るデフォルトカードの強さ（0..1）＝債務規模(cardDebtScaleで飽和)×貸し手の自己資本比露出(0..1)。
        /// 債務が貸し手の自己資本に対して大きいほど、踏み倒しは貸し手自身の破綻になる＝借り手の逆カード。
        /// 小口の債務や、損を吸収できる厚い資本の貸し手にはカードにならない＝債務は双方向の武器。
        /// </summary>
        public static float DefaultCardStrength(float debtToCreditor, float creditorExposure, DebtDiplomacyParams p)
        {
            float scale = Mathf.Clamp01(Mathf.Max(0f, debtToCreditor) / p.cardDebtScale);
            return scale * Mathf.Clamp01(creditorExposure);
        }

        public static float DefaultCardStrength(float debtToCreditor, float creditorExposure)
            => DefaultCardStrength(debtToCreditor, creditorExposure, DebtDiplomacyParams.Default);
    }
}
