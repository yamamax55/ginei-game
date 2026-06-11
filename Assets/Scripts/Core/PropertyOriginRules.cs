using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 労働財産論と先占権の数値解決（#1447 LOCK-1・ジョン・ロック『統治二論』の労働所有論・純ロジック test-first）。
    /// 「自然はもともと万人の共有物（コモンズ）だが、人が自らの労働を加えたものはその人の財産になる
    /// ＝開墾した土地・採取した果実は労働によって私有化される。ただし他人にも十分残されている限り（ロックの但し書き）
    /// かつ腐らせない限り正当」をモデル化する：労働の混合がコモンズから財産（請求権）を生み、但し書きと腐敗の制限が
    /// その正当性を縛り、貨幣の導入がその制限を超えて無制限蓄積を可能にする。
    /// 乱数は持たない（決定論）。調整値は <see cref="PropertyOriginParams"/> に集約（基準非破壊・実効値パターン）。
    /// 分担：<see cref="PropertyRightsRules"/>(財産の保護強度＝守られない財産は築かれない・既存)・
    /// <see cref="ColonizationRules"/>(無人惑星への入植＝1惑星→銀河へ拡張)・
    /// <see cref="InheritanceRules"/>(相続＝既に確立した財産の世代移転)・
    /// <see cref="TrustMandateRules"/>(同EPIC LOCK＝信託・統治権の委託)とは別＝こちらはロックの労働が財産を生む
    /// 先占権（コモンズ→私有の請求権の強さ＝財産権の起源）を扱う。
    /// </summary>
    public static class PropertyOriginRules
    {
        /// <summary>
        /// 労働の混合（0..1）＝労働を加えることで自然物に価値が付与される（労働の混合＝財産の源泉）。
        /// 自然物の価値（resourceValue）に労働の投入（laborApplied）を乗じる＝手つかずの自然はそのままでは
        /// 私有の根拠を持たず、労働を混ぜてはじめて価値（＝請求権の素）になる。労働ゼロなら混合ゼロ。
        /// </summary>
        public static float LaborMixing(float laborApplied, float resourceValue)
        {
            float labor = Mathf.Clamp01(laborApplied);
            float value = Mathf.Clamp01(resourceValue);
            return labor * value;
        }

        /// <summary>
        /// 私有の請求権の強さ（0..1）＝労働の投入×占有の継続で決まる（耕した者が持つ）。
        /// 労働の混合（laborMixing）に占有の継続（occupationDuration）の寄与を重みで合成する
        /// ＝労働だけでも占有だけでも弱く、耕し続けてはじめて強い請求権になる。
        /// </summary>
        public static float ClaimStrength(float laborMixing, float occupationDuration, PropertyOriginParams p)
        {
            float labor = Mathf.Clamp01(laborMixing);
            float occ = Mathf.Clamp01(occupationDuration);
            float w = p.OccupationWeight;
            return Mathf.Clamp01(labor * (1f - w) + labor * occ * w);
        }

        /// <summary>私有の請求権の強さ（既定パラメータ）。</summary>
        public static float ClaimStrength(float laborMixing, float occupationDuration)
            => ClaimStrength(laborMixing, occupationDuration, PropertyOriginParams.Default);

        /// <summary>
        /// ロックの但し書き（0..1）＝他人にも十分残されている限り私有が正当（独り占めは不当）。
        /// 自分の取り分（claimedShare）が小さく、他者に十分残っている（remainingForOthers）ほど正当性が高い。
        /// 残余が <see cref="PropertyOriginParams.ProvisoSufficiency"/> を満たせば満点、取り分過大・残余僅少なら正当性が崩れる。
        /// </summary>
        public static float LockeanProviso(float claimedShare, float remainingForOthers, PropertyOriginParams p)
        {
            float claimed = Mathf.Clamp01(claimedShare);
            float remaining = Mathf.Clamp01(remainingForOthers);
            // 残余が十分閾値に達していれば満点、足りないぶんだけ線形に減じ、取り分の過大さでさらに割り引く
            float sufficiency = Mathf.Clamp01(remaining / Mathf.Max(p.ProvisoSufficiency, 1e-4f));
            return Mathf.Clamp01(sufficiency * (1f - claimed));
        }

        /// <summary>ロックの但し書き（既定パラメータ）。</summary>
        public static float LockeanProviso(float claimedShare, float remainingForOthers)
            => LockeanProviso(claimedShare, remainingForOthers, PropertyOriginParams.Default);

        /// <summary>
        /// 腐敗の制限（0..1の正当性）＝腐らせるほど溜め込むのは不当（使い切れない私有の制限・貨幣以前）。
        /// 蓄積（accumulatedResource）が使用速度（useRate）で使い切れる範囲なら満点、使い切れず腐らせるぶんだけ
        /// 正当性が落ちる＝ロックの「腐らせない限り」。useRate が高いほど多くを正当に保持できる。
        /// </summary>
        public static float SpoilageLimit(float accumulatedResource, float useRate)
        {
            float acc = Mathf.Clamp01(accumulatedResource);
            float use = Mathf.Clamp01(useRate);
            float spoiled = Mathf.Max(0f, acc - use); // 使い切れず腐る分
            return Mathf.Clamp01(1f - spoiled);
        }

        /// <summary>
        /// コモンズの私有化（残るコモンズ割合 0..1）＝コモンズが労働によって私有地へ転化していく（囲い込み）。
        /// 共有地（commonsShare）から労働の投入（laborInput）ぶんが dt の時間で私有へ転化し、コモンズが痩せていく。
        /// 転化速度は <see cref="PropertyOriginParams.EnclosureRate"/>。戻り値は転化後に残ったコモンズ割合。
        /// </summary>
        public static float CommonsToPrivate(float commonsShare, float laborInput, float dt, PropertyOriginParams p)
        {
            float commons = Mathf.Clamp01(commonsShare);
            float labor = Mathf.Clamp01(laborInput);
            if (dt <= 0f) return commons;
            float converted = commons * labor * p.EnclosureRate * dt; // 労働が囲い込む分
            return Mathf.Clamp01(commons - converted);
        }

        /// <summary>コモンズの私有化（既定パラメータ）。</summary>
        public static float CommonsToPrivate(float commonsShare, float laborInput, float dt)
            => CommonsToPrivate(commonsShare, laborInput, dt, PropertyOriginParams.Default);

        /// <summary>
        /// 先占権（0..1）＝先に来て開墾した者の先占権（早い者勝ち＋改良）。
        /// 到着順（arrivalOrder＝早いほど1に近い）と改良（improvement）の重み合成＝早く来ただけでも弱く、
        /// 改良を加えてはじめて強い先占権になる。改良の寄与は <see cref="PropertyOriginParams.ImprovementWeight"/>。
        /// </summary>
        public static float FirstOccupancyRight(float arrivalOrder, float improvement, PropertyOriginParams p)
        {
            float arrival = Mathf.Clamp01(arrivalOrder);
            float improve = Mathf.Clamp01(improvement);
            float w = p.ImprovementWeight;
            return Mathf.Clamp01(arrival * (1f - w) + improve * w);
        }

        /// <summary>先占権（既定パラメータ）。</summary>
        public static float FirstOccupancyRight(float arrivalOrder, float improvement)
            => FirstOccupancyRight(arrivalOrder, improvement, PropertyOriginParams.Default);

        /// <summary>
        /// 貨幣と無制限蓄積（実効的に許容される蓄積 0..1超）＝貨幣の導入が腐敗の制限を超えて無制限蓄積を可能にする
        /// （ロック＝貨幣が不平等を正当化）。貨幣以前は腐る資源を溜め込めなかったが、腐らない貨幣に換えれば
        /// <see cref="SpoilageLimit"/> の制約を <paramref name="moneyIntroduced"/> ぶん免れる＝蓄積（accumulation）が
        /// 腐敗の制限を超えて正当化される。貨幣ゼロなら腐敗の制限がそのまま効く。
        /// </summary>
        public static float MoneyTranscendsSpoilage(float moneyIntroduced, float accumulation)
        {
            float money = Mathf.Clamp01(moneyIntroduced);
            float acc = Mathf.Max(0f, accumulation);
            // 貨幣が無ければ腐敗の制限内（最大1）に収まり、貨幣が入るほど蓄積が無制限へ解放される
            float capped = Mathf.Min(1f, acc);
            return Mathf.Lerp(capped, acc, money);
        }

        /// <summary>
        /// 正当な私有化の判定＝労働と但し書きを満たした正当な私有化か（ロックの労働所有論の最終ゲート）。
        /// 請求権の強さ（claimStrength＝労働を加えた）と但し書き（lockeanProviso＝他人に十分残した）の双方が
        /// 閾値（threshold）以上のときだけ正当＝労働だけ・取り分の控えめさだけでは不十分、両方を満たして正当な私有化。
        /// </summary>
        public static bool IsLegitimateAppropriation(float claimStrength, float lockeanProviso, float threshold)
        {
            float claim = Mathf.Clamp01(claimStrength);
            float proviso = Mathf.Clamp01(lockeanProviso);
            float th = Mathf.Clamp01(threshold);
            return claim >= th && proviso >= th;
        }
    }

    /// <summary>
    /// PropertyOriginRules の調整値（#1447・マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// ctor で全値をクランプ（重み・速度・閾値は 0..1）。
    /// </summary>
    public readonly struct PropertyOriginParams
    {
        /// <summary>請求権における占有の継続の寄与（0..1・労働の混合との重み）。</summary>
        public readonly float OccupationWeight;
        /// <summary>但し書きが満点になる残余の十分量（0..1・これ以上残れば正当性に十分）。</summary>
        public readonly float ProvisoSufficiency;
        /// <summary>コモンズが労働で私有へ転化する速度（囲い込みの速さ・0..1）。</summary>
        public readonly float EnclosureRate;
        /// <summary>先占権における改良の寄与（0..1・到着順との重み）。</summary>
        public readonly float ImprovementWeight;

        public PropertyOriginParams(
            float occupationWeight, float provisoSufficiency, float enclosureRate, float improvementWeight)
        {
            OccupationWeight = Mathf.Clamp01(occupationWeight);
            ProvisoSufficiency = Mathf.Clamp01(provisoSufficiency);
            EnclosureRate = Mathf.Clamp01(enclosureRate);
            ImprovementWeight = Mathf.Clamp01(improvementWeight);
        }

        /// <summary>
        /// 既定（占有の重み0.4／但し書きの十分量0.5／囲い込み速度0.5／改良の重み0.5）。
        /// 占有より労働の混合を重く（労働が財産の源泉）、残余が半分以上あれば但し書きは満たされる、
        /// 先占権は到着順と改良を等分（早い者勝ちだけでなく改良してこそ）。
        /// </summary>
        public static PropertyOriginParams Default => new PropertyOriginParams(
            occupationWeight: 0.4f, provisoSufficiency: 0.5f, enclosureRate: 0.5f, improvementWeight: 0.5f);
    }
}
