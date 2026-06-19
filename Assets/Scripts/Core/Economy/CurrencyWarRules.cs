using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 通貨戦争の調整値（#通貨戦争）。切り下げの輸出てこ・資本規制の防衛上積み・資本逃避の増幅・基軸特権の効きのパラメータ。
    /// 安定性は符号付き -1..1。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct CurrencyWarParams
    {
        /// <summary>通貨切り下げが輸出競争力に化けるてこ（切り下げ率×輸出比率に乗算）。</summary>
        public readonly float exportLeverage;
        /// <summary>資本規制が外貨準備の防衛力へ上積みする重み（規制0..1あたりの上積み）。</summary>
        public readonly float controlWeight;
        /// <summary>資本逃避の増幅（攻撃成功×信認喪失に乗算）。</summary>
        public readonly float flightMultiplier;
        /// <summary>基軸通貨特権が通貨安定へ寄与する重み。</summary>
        public readonly float privilegeWeight;

        public CurrencyWarParams(float exportLeverage, float controlWeight, float flightMultiplier, float privilegeWeight)
        {
            this.exportLeverage = Mathf.Clamp(exportLeverage, 0f, 4f);
            this.controlWeight = Mathf.Clamp01(controlWeight);
            this.flightMultiplier = Mathf.Clamp(flightMultiplier, 0f, 4f);
            this.privilegeWeight = Mathf.Clamp01(privilegeWeight);
        }

        /// <summary>既定：輸出てこ1.0・規制上積み0.5・逃避増幅1.0・基軸特権重み0.5。</summary>
        public static CurrencyWarParams Default => new CurrencyWarParams(
            DefaultExportLeverage, DefaultControlWeight, DefaultFlightMultiplier, DefaultPrivilegeWeight);

        public const float DefaultExportLeverage = 1.0f;
        public const float DefaultControlWeight = 0.5f;
        public const float DefaultFlightMultiplier = 1.0f;
        public const float DefaultPrivilegeWeight = 0.5f;
    }

    /// <summary>
    /// 通貨戦争の純ロジック（#通貨戦争・唯一の窓口）＝<b>通貨・為替・金融を武器にした経済攻防</b>。
    /// 通貨切り下げで輸出競争力を奪い、投機資本で相手通貨を攻撃し、外貨準備と資本規制で防衛し、基軸通貨は法外な特権を持ち、
    /// 攻撃が通れば資本逃避が起き、決済システム支配が金融制裁の打撃になる。通常の通貨運営（<see cref="CurrencyRules"/>＝
    /// インフレ/為替の平時進行）とは別＝こちらは<b>通貨を武器にした攻防</b>。盤面非依存の plain 引数・実効値パターン・test-first。
    /// </summary>
    public static class CurrencyWarRules
    {
        /// <summary>既定パラメータで通貨切り下げの輸出競争力獲得。</summary>
        public static float DevaluationAdvantage(float devaluationRate, float exportShare)
            => DevaluationAdvantage(devaluationRate, exportShare, CurrencyWarParams.Default);

        /// <summary>
        /// 通貨切り下げによる輸出競争力の獲得（0..1）。切り下げ率×輸出比率×輸出てこ。
        /// 切り下げても輸出が乏しければ効かず、輸出依存国ほど自国通貨安が効く（近隣窮乏化）。
        /// </summary>
        public static float DevaluationAdvantage(float devaluationRate, float exportShare, CurrencyWarParams p)
        {
            float rate = Mathf.Clamp01(devaluationRate);
            float share = Mathf.Clamp01(exportShare);
            return Mathf.Clamp01(rate * share * p.exportLeverage);
        }

        /// <summary>既定パラメータで通貨攻撃の威力。</summary>
        public static float CurrencyAttack(float speculativeCapital, float targetReserves)
            => CurrencyAttack(speculativeCapital, targetReserves, CurrencyWarParams.Default);

        /// <summary>
        /// 投機資本による通貨攻撃の威力（0..1）＝spec/(spec＋target外貨準備)。
        /// 投機資本が相手の準備を上回るほど攻撃が強く、準備が厚いと攻撃が薄まる。両者0は威力0。
        /// </summary>
        public static float CurrencyAttack(float speculativeCapital, float targetReserves, CurrencyWarParams p)
        {
            float spec = Mathf.Max(0f, speculativeCapital);
            float reserves = Mathf.Max(0f, targetReserves);
            float denom = spec + reserves;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(spec / denom);
        }

        /// <summary>既定パラメータで通貨防衛力。</summary>
        public static float ReserveDefense(float foreignReserves, float capitalControls)
            => ReserveDefense(foreignReserves, capitalControls, CurrencyWarParams.Default);

        /// <summary>
        /// 外貨準備と資本規制による通貨防衛力（0..1）。準備が土台、資本規制が残余ぶんを上積み（資本流出を堰き止める）。
        /// 防衛＝準備＋規制×重み×(1-準備)。準備が薄くても規制で底上げできるが、完全には防げない。
        /// </summary>
        public static float ReserveDefense(float foreignReserves, float capitalControls, CurrencyWarParams p)
        {
            float reserves = Mathf.Clamp01(foreignReserves);
            float controls = Mathf.Clamp01(capitalControls);
            return Mathf.Clamp01(reserves + controls * p.controlWeight * (1f - reserves));
        }

        /// <summary>既定パラメータで通貨攻撃の成否。</summary>
        public static float AttackSuccess(float currencyAttack, float reserveDefense)
            => AttackSuccess(currencyAttack, reserveDefense, CurrencyWarParams.Default);

        /// <summary>
        /// 通貨攻撃が通る度合い（0..1）＝攻撃×(1-防衛)。防衛が厚いほど攻撃を撥ね返し、防衛が崩れると攻撃がそのまま通る。
        /// </summary>
        public static float AttackSuccess(float currencyAttack, float reserveDefense, CurrencyWarParams p)
        {
            float attack = Mathf.Clamp01(currencyAttack);
            float defense = Mathf.Clamp01(reserveDefense);
            return Mathf.Clamp01(attack * (1f - defense));
        }

        /// <summary>既定パラメータで基軸通貨の特権。</summary>
        public static float ReserveCurrencyPrivilege(float globalUsage, float trustInCurrency)
            => ReserveCurrencyPrivilege(globalUsage, trustInCurrency, CurrencyWarParams.Default);

        /// <summary>
        /// 基軸通貨の法外な特権（0..1）＝国際的利用×信認の幾何平均。どちらか欠ければ崩れる（利用されても信認が無ければ基軸たりえない）。
        /// 高ければ通貨攻撃に強く資本逃避が起きにくい（決済・準備で世界に組み込まれた特権）。
        /// </summary>
        public static float ReserveCurrencyPrivilege(float globalUsage, float trustInCurrency, CurrencyWarParams p)
        {
            float usage = Mathf.Clamp01(globalUsage);
            float trust = Mathf.Clamp01(trustInCurrency);
            return Mathf.Clamp01(Mathf.Sqrt(usage * trust));
        }

        /// <summary>既定パラメータで資本逃避。</summary>
        public static float CapitalFlight(float attackSuccess, float confidenceLoss)
            => CapitalFlight(attackSuccess, confidenceLoss, CurrencyWarParams.Default);

        /// <summary>
        /// 攻撃成功と信認喪失による資本逃避（0..1）＝攻撃成功×信認喪失×逃避増幅。
        /// 攻撃が通り、かつ信認が崩れると資本が一斉に国外へ逃げる（為替・株・債券の同時崩落）。
        /// </summary>
        public static float CapitalFlight(float attackSuccess, float confidenceLoss, CurrencyWarParams p)
        {
            float success = Mathf.Clamp01(attackSuccess);
            float loss = Mathf.Clamp01(confidenceLoss);
            return Mathf.Clamp01(success * loss * p.flightMultiplier);
        }

        /// <summary>既定パラメータで金融制裁の打撃。</summary>
        public static float FinancialSanctionImpact(float paymentSystemControl, float targetIntegration)
            => FinancialSanctionImpact(paymentSystemControl, targetIntegration, CurrencyWarParams.Default);

        /// <summary>
        /// 金融制裁の打撃（0..1）＝決済システム支配×対象の金融統合。決済網を握る側が制裁を打てるが、
        /// 対象が国際金融に組み込まれているほど効く（自給自足・デカップリングした相手には効きにくい）。
        /// </summary>
        public static float FinancialSanctionImpact(float paymentSystemControl, float targetIntegration, CurrencyWarParams p)
        {
            float control = Mathf.Clamp01(paymentSystemControl);
            float integration = Mathf.Clamp01(targetIntegration);
            return Mathf.Clamp01(control * integration);
        }

        /// <summary>既定パラメータで通貨の安定性。</summary>
        public static float MonetaryStability(float reserveDefense, float capitalFlight, float reserveCurrencyPrivilege)
            => MonetaryStability(reserveDefense, capitalFlight, reserveCurrencyPrivilege, CurrencyWarParams.Default);

        /// <summary>
        /// 通貨の総合的な安定性（-1崩壊..+1盤石）＝防衛−逃避＋基軸特権×重み。防衛と基軸特権が安定を支え、
        /// 資本逃避が崩す。プラスなら通貨は持ちこたえ、マイナスなら通貨危機（崩落）。
        /// </summary>
        public static float MonetaryStability(float reserveDefense, float capitalFlight, float reserveCurrencyPrivilege, CurrencyWarParams p)
        {
            float defense = Mathf.Clamp01(reserveDefense);
            float flight = Mathf.Clamp01(capitalFlight);
            float privilege = Mathf.Clamp01(reserveCurrencyPrivilege);
            float value = defense - flight + privilege * p.privilegeWeight;
            return Mathf.Clamp(value, -1f, 1f);
        }
    }
}
