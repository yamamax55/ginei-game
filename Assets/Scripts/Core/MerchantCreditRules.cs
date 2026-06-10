using UnityEngine;

namespace Ginei
{
    /// <summary>商人の信用・レバレッジ（てこ）・破産の調整係数。</summary>
    public readonly struct MerchantCreditParams
    {
        /// <summary>取引成功で信用が増す速さ（成功実績が信用を作る）。</summary>
        public readonly float buildRate;
        /// <summary>取引失敗で信用が減る速さ（失敗は信用を速く損なう＝積むより崩れるが速い）。</summary>
        public readonly float erodeRate;
        /// <summary>与信枠の基礎倍率（信用×担保×これ＝借りられる額）。</summary>
        public readonly float creditScale;
        /// <summary>手形の信用割引の効き（信用が低いほど割引が深い＝信用なき手形は安く買い叩かれる）。</summary>
        public readonly float discountWeight;
        /// <summary>満期までの時間が手形価値を割り引く率（先の手形ほど現在価値が下がる）。</summary>
        public readonly float timeDiscountRate;
        /// <summary>破産が起き始めるレバレッジ閾値（これ以下のてこは一撃破産しない）。</summary>
        public readonly float safeLeverage;
        /// <summary>破産がほぼ確実になるレバレッジ（これ以上は変動次第で一撃）。</summary>
        public readonly float ruinLeverage;

        public MerchantCreditParams(float buildRate, float erodeRate, float creditScale,
            float discountWeight, float timeDiscountRate, float safeLeverage, float ruinLeverage)
        {
            this.buildRate = Mathf.Max(0f, buildRate);
            this.erodeRate = Mathf.Max(0f, erodeRate);
            this.creditScale = Mathf.Max(0f, creditScale);
            this.discountWeight = Mathf.Clamp01(discountWeight);
            this.timeDiscountRate = Mathf.Max(0f, timeDiscountRate);
            this.safeLeverage = Mathf.Max(1f, safeLeverage);
            this.ruinLeverage = Mathf.Max(this.safeLeverage + 0.01f, ruinLeverage);
        }

        /// <summary>既定＝信用構築0.1/失墜0.2（崩れる方が速い）・与信1.0・割引0.5・時間割引0.05・安全てこ2.0/破滅てこ5.0。</summary>
        public static MerchantCreditParams Default =>
            new MerchantCreditParams(0.1f, 0.2f, 1f, 0.5f, 0.05f, 2f, 5f);
    }

    /// <summary>
    /// 商人の信用・為替手形・レバレッジ・破産の純ロジック（#1077・狼と香辛料＝ネームド商人の信用取引）。
    /// 商人個人の<b>信用</b>は取引の成功で積み上がり失敗で崩れる（実績が信用を作る）。信用と担保が<b>与信枠</b>を生み、
    /// 信用ある商人の<b>為替手形</b>は額面に近い価値で通り（信用なき手形は割り引かれる＝信用割引）、信用を元手に借金して
    /// 自己資本を超える取引を張る＝<b>レバレッジ</b>（てこ）が儲けを倍増させるが損も倍にする（両刃）。過大なてこは一度の
    /// 取引の失敗（変動）で<b>破産</b>させる＝「信用は商人の元手＝手形を生みレバレッジを可能にするが、過大なてこは
    /// 一度の失敗で破産させる」。
    /// <see cref="BankRules"/>（銀行＝部分準備での信用<i>創造</i>・取り付け）とは別＝こちらは商人<b>個人</b>の信用と
    /// レバレッジ破産。財政・国家債務は <see cref="FiscalRules"/> が、買い占め投機は <c>CorneringRules</c>（同Wave並行）が
    /// 別系統で扱う。全入力クランプ・乱数は roll 引数で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MerchantCreditRules
    {
        /// <summary>
        /// 信用の更新後の値（0..1）＝取引成功で buildRate ぶん増し、失敗で erodeRate ぶん減る。
        /// 実績が信用を作り（成功の積み重ね）、一度の失敗は構築より速く信用を削る（erode＞build）。
        /// </summary>
        public static float CreditworthinessTick(float creditworthiness, bool dealSuccess, float dt, MerchantCreditParams p)
        {
            float c = Mathf.Clamp01(creditworthiness);
            float step = Mathf.Max(0f, dt);
            float delta = dealSuccess ? p.buildRate * step : -p.erodeRate * step;
            return Mathf.Clamp01(c + delta);
        }

        public static float CreditworthinessTick(float creditworthiness, bool dealSuccess, float dt)
            => CreditworthinessTick(creditworthiness, dealSuccess, dt, MerchantCreditParams.Default);

        /// <summary>
        /// 与信枠＝信用(0..1)×担保×基礎倍率＝借りられる上限。信用も担保もなければ借りられない（積＝両方が要る）。
        /// 信用ある商人ほど同じ担保で多く借りられる＝信用は元手。
        /// </summary>
        public static float CreditLimit(float creditworthiness, float collateral, MerchantCreditParams p)
        {
            float c = Mathf.Clamp01(creditworthiness);
            float col = Mathf.Max(0f, collateral);
            return c * col * p.creditScale;
        }

        public static float CreditLimit(float creditworthiness, float collateral)
            => CreditLimit(creditworthiness, collateral, MerchantCreditParams.Default);

        /// <summary>
        /// 為替手形の現在価値＝額面×信用割引×時間割引。信用ある商人(1.0)の手形は額面に近く通り、信用なき手形は
        /// discountWeight ぶん割り引かれる（信用割引）。満期が先（timeToMaturity 大）ほど時間割引で価値が下がる。
        /// ＝信用と満期が手形の通りを決める。
        /// </summary>
        public static float BillOfExchangeValue(float faceValue, float issuerCreditworthiness, float timeToMaturity, MerchantCreditParams p)
        {
            float face = Mathf.Max(0f, faceValue);
            float cred = Mathf.Clamp01(issuerCreditworthiness);
            float t = Mathf.Max(0f, timeToMaturity);
            // 信用割引：信用1.0で割引0・信用0で discountWeight ぶん満額から削る。
            float creditFactor = 1f - (1f - cred) * p.discountWeight;
            // 時間割引：満期が先ほど現在価値が逓減（線形・下限0）。
            float timeFactor = Mathf.Max(0f, 1f - p.timeDiscountRate * t);
            return face * creditFactor * timeFactor;
        }

        public static float BillOfExchangeValue(float faceValue, float issuerCreditworthiness, float timeToMaturity)
            => BillOfExchangeValue(faceValue, issuerCreditworthiness, timeToMaturity, MerchantCreditParams.Default);

        /// <summary>
        /// レバレッジ倍率＝(自己資本+借入)/自己資本＝てこ。自己資本に対しどれだけ借りて張ったか。
        /// 借入0で1.0（てこなし）、借入が自己資本と同額で2.0。自己資本0は破滅的なてことして大きな値を返す。
        /// </summary>
        public static float Leverage(float borrowed, float ownCapital)
        {
            float b = Mathf.Max(0f, borrowed);
            float own = Mathf.Max(0f, ownCapital);
            if (own <= 0f) return b > 0f ? float.PositiveInfinity : 1f;
            return (own + b) / own;
        }

        /// <summary>
        /// レバレッジ収益＝てこ×取引リターン率＝てこの両刃。取引が儲かれば(dealReturn＞0)てこで倍増し、損すれば
        /// (dealReturn＜0)てこで倍の損になる＝てこは儲けも損も拡大する。引数 dealReturn は自己資本に対する素のリターン率。
        /// </summary>
        public static float LeveragedReturn(float leverage, float dealReturn)
        {
            float lev = Mathf.Max(0f, leverage);
            return lev * dealReturn;
        }

        /// <summary>
        /// 破産リスク（0..1）＝過大なレバレッジ×取引の変動。安全てこ以下なら0（一撃破産しない）、破滅てこ以上は変動次第で
        /// 一撃（崖）。間はてこの深さ×変動(0..1)で上がる＝信用取引の崖。過大なてこは一度の失敗で破産させる。
        /// </summary>
        public static float BankruptcyRisk(float leverage, float dealVolatility, MerchantCreditParams p)
        {
            float vol = Mathf.Clamp01(dealVolatility);
            float lev = Mathf.Max(0f, leverage);
            if (lev <= p.safeLeverage) return 0f;
            // 安全→破滅の間でてこの深さを0..1へ写す（破滅以上は1）。
            float levExcess = Mathf.Clamp01((lev - p.safeLeverage) / (p.ruinLeverage - p.safeLeverage));
            // てこが深いほど・変動が大きいほど破産しやすい（積＝両方が要る＝穏やかな取引なら高てこでも耐える）。
            return Mathf.Clamp01(levExcess * vol);
        }

        public static float BankruptcyRisk(float leverage, float dealVolatility)
            => BankruptcyRisk(leverage, dealVolatility, MerchantCreditParams.Default);

        /// <summary>破産判定（決定論）＝破産リスクが roll(0..1)を上回れば破産。乱数は呼び出し側が roll で渡す。</summary>
        public static bool IsBankrupt(float bankruptcyRisk, float roll)
            => Mathf.Clamp01(bankruptcyRisk) > Mathf.Clamp01(roll);
    }
}
