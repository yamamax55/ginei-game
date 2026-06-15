using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 任期制限の数値解決（純ロジック test-first）。「権力の時間制約」を扱う唯一の窓口。
    /// <see cref="ConstitutionRules"/> が権力の「範囲」（何ができるか）を縛るのに対し、
    /// こちらは権力の「時間」（いつまで持てるか）を縛る＝別軸の制約で重複しない。
    /// モデル化するのは「非常時だから」の延長誘惑と、共和制の死：
    /// 任期制限は法でなく慣習でできており、最初の違反が最も高くつき、
    /// 一度破られた慣習は痩せて戻りにくい（二人目の独裁者は安く済む）。
    /// 乱数は持たない（決定論）。調整値は <see cref="TermLimitParams"/> に集約（基準非破壊・実効値パターン）。
    /// </summary>
    public static class TermLimitRules
    {
        /// <summary>NormStrength の違反回数の上限（これ以上は痩せ方が変わらない・ループ上限）。</summary>
        public const int MaxViolationHistory = 20;

        /// <summary>
        /// 延長誘惑（0..1）＝「余人をもって代えがたい」の強さ。
        /// 危機×人気の積を土台に、在任期数が長いほど増幅される（長期政権ほど居座りの理屈が立つ）。
        /// 危機か人気のどちらかがゼロなら誘惑は立たない（平時の不人気者は延長を言い出せない）。
        /// </summary>
        public static float ExtensionTemptation(float crisisLevel, float incumbentPopularity, int termsServed, TermLimitParams p)
        {
            float crisis = Mathf.Clamp01(crisisLevel);
            float popularity = Mathf.Clamp01(incumbentPopularity);
            int terms = Mathf.Max(0, termsServed);
            // 在任の長さで誘惑が増幅される＝「この危機を任せられるのは現職だけ」
            float tenureFactor = 1f + terms * p.TenureWeight;
            return Mathf.Clamp01(crisis * popularity * tenureFactor);
        }

        /// <summary>延長誘惑（既定パラメータ）。</summary>
        public static float ExtensionTemptation(float crisisLevel, float incumbentPopularity, int termsServed)
            => ExtensionTemptation(crisisLevel, incumbentPopularity, termsServed, TermLimitParams.Default);

        /// <summary>
        /// 慣習の強度（0..1）。無違反なら 1（完全な慣習）。
        /// 違反のたびに乗算で痩せ（1回で ViolationDecay 倍、2回でその二乗…）、
        /// 時間経過で僅かに回復するが、一度でも破られると <see cref="TermLimitParams.NormRecoveryCeiling"/>
        /// までしか戻らない＝破る方が速く戻る方が遅い非対称（割れた器は元の形に戻らない）。
        /// </summary>
        public static float NormStrength(int violationHistory, float yearsSinceViolation, TermLimitParams p)
        {
            int violations = Mathf.Clamp(violationHistory, 0, MaxViolationHistory);
            if (violations <= 0) return 1f;

            // 乗算で痩せる＝違反が重なるほど慣習は指数的に空洞化する
            float strength = 1f;
            for (int i = 0; i < violations; i++) strength *= p.ViolationDecay;

            // 時間で僅かに回復するが、天井までしか戻らない（非対称の核）
            float recovered = strength + Mathf.Max(0f, yearsSinceViolation) * p.NormRecoveryPerYear;
            return Mathf.Clamp01(Mathf.Min(recovered, p.NormRecoveryCeiling));
        }

        /// <summary>慣習の強度（既定パラメータ）。</summary>
        public static float NormStrength(int violationHistory, float yearsSinceViolation)
            => NormStrength(violationHistory, yearsSinceViolation, TermLimitParams.Default);

        /// <summary>
        /// 延長の正統性コスト（0..1）＝慣習の強度に比例。
        /// 強い慣習を最初に破る者が最も高い代償を払い、痩せた慣習の下では延長はタダ同然
        /// ＝「最初の違反が最も高くつく」を式に出す（だから二人目の独裁者は安く済む）。
        /// </summary>
        public static float ExtensionLegitimacyCost(float normStrength, TermLimitParams p)
        {
            return Mathf.Clamp01(normStrength) * p.MaxLegitimacyCost;
        }

        /// <summary>延長の正統性コスト（既定パラメータ）。</summary>
        public static float ExtensionLegitimacyCost(float normStrength)
            => ExtensionLegitimacyCost(normStrength, TermLimitParams.Default);

        /// <summary>
        /// 平和的政権交代の制度資本（0..1）＝交代が連続するほど対数的に逓増。
        /// 1回目の交代が最も大きく効き（前例ができる）、以後は逓減しつつ積み上がる
        /// ＝制度は反復で常識になるが、常識の上積みはだんだん小さくなる。
        /// </summary>
        public static float PeacefulTransferValue(int consecutiveTransfers, TermLimitParams p)
        {
            int transfers = Mathf.Max(0, consecutiveTransfers);
            return Mathf.Clamp01(p.TransferLogScale * Mathf.Log(1f + transfers));
        }

        /// <summary>平和的政権交代の制度資本（既定パラメータ）。</summary>
        public static float PeacefulTransferValue(int consecutiveTransfers)
            => PeacefulTransferValue(consecutiveTransfers, TermLimitParams.Default);

        /// <summary>
        /// 共和制の制度資本の更新（0..1 へクランプ）。
        /// 違反（任期延長の強行）は一撃で大きく削り（イベント＝dt 非依存）、
        /// 平時は dt 比例で僅かに積む＝壊すのは一瞬・築くのは何十年（既定値では1回の違反の回復に30年）。
        /// 基準値（呼び出し側の資本）は変えず、新しい値を返す（実効値パターン）。
        /// </summary>
        public static float RepublicDecayTick(float institutionalCapital, bool violation, float deltaTime, TermLimitParams p)
        {
            float capital = Mathf.Clamp01(institutionalCapital);
            if (violation) return Mathf.Clamp01(capital - p.ViolationDamage);
            if (deltaTime <= 0f) return capital;
            return Mathf.Clamp01(capital + p.AccrualRate * deltaTime);
        }

        /// <summary>共和制の制度資本の更新（既定パラメータ）。</summary>
        public static float RepublicDecayTick(float institutionalCapital, bool violation, float deltaTime)
            => RepublicDecayTick(institutionalCapital, violation, deltaTime, TermLimitParams.Default);
    }

    /// <summary>
    /// TermLimitRules の調整値（マジックナンバー集約・ctor で全値クランプ）。既定は <see cref="Default"/>。
    /// 違反の削り（ViolationDecay/ViolationDamage）が回復（NormRecoveryPerYear/AccrualRate）より
    /// 桁違いに大きい＝「破るのは一瞬・戻すのは一生」の非対称を既定値で担保する。
    /// </summary>
    public readonly struct TermLimitParams
    {
        /// <summary>在任1期あたりの延長誘惑の増幅（0..1）。</summary>
        public readonly float TenureWeight;
        /// <summary>違反1回ごとに慣習強度へ乗じる減衰率（0..1・小さいほど一撃で痩せる）。</summary>
        public readonly float ViolationDecay;
        /// <summary>違反後の慣習強度の年あたり回復量（0以上・僅か）。</summary>
        public readonly float NormRecoveryPerYear;
        /// <summary>一度破られた慣習が回復で戻れる上限（0..1・1未満＝完全には戻らない）。</summary>
        public readonly float NormRecoveryCeiling;
        /// <summary>慣習強度が満点のときの延長の正統性コスト（0..1）。</summary>
        public readonly float MaxLegitimacyCost;
        /// <summary>政権交代の制度資本の対数スケール（0以上）。</summary>
        public readonly float TransferLogScale;
        /// <summary>違反1回が制度資本を削る量（0..1・dt 非依存の一撃）。</summary>
        public readonly float ViolationDamage;
        /// <summary>平時の制度資本の年あたり蓄積量（0以上・僅か）。</summary>
        public readonly float AccrualRate;

        public TermLimitParams(
            float tenureWeight, float violationDecay, float normRecoveryPerYear, float normRecoveryCeiling,
            float maxLegitimacyCost, float transferLogScale, float violationDamage, float accrualRate)
        {
            TenureWeight = Mathf.Clamp01(tenureWeight);
            ViolationDecay = Mathf.Clamp01(violationDecay);
            NormRecoveryPerYear = Mathf.Max(0f, normRecoveryPerYear);
            NormRecoveryCeiling = Mathf.Clamp01(normRecoveryCeiling);
            MaxLegitimacyCost = Mathf.Clamp01(maxLegitimacyCost);
            TransferLogScale = Mathf.Max(0f, transferLogScale);
            ViolationDamage = Mathf.Clamp01(violationDamage);
            AccrualRate = Mathf.Max(0f, accrualRate);
        }

        /// <summary>
        /// 既定（在任増幅0.25/期・違反減衰0.4・慣習回復0.01/年・回復天井0.8・
        /// 最大正統性コスト0.5・交代対数スケール0.3・違反一撃0.3・平時蓄積0.01/年）。
        /// 1回の違反（−0.3）を平時の蓄積で取り戻すには30年かかる＝共和制は慣習でできている。
        /// </summary>
        public static TermLimitParams Default => new TermLimitParams(
            tenureWeight: 0.25f, violationDecay: 0.4f, normRecoveryPerYear: 0.01f, normRecoveryCeiling: 0.8f,
            maxLegitimacyCost: 0.5f, transferLogScale: 0.3f, violationDamage: 0.3f, accrualRate: 0.01f);
    }
}
