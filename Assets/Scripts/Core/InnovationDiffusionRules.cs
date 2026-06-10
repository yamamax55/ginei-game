using UnityEngine;

namespace Ginei
{
    /// <summary>技術伝播の調整係数。</summary>
    public readonly struct DiffusionParams
    {
        /// <summary>機密保持で絞れる漏出の上限割合（完全遮断は不可能＝独占は時限、の保証）。</summary>
        public const float MaxSecrecyBlockCap = 0.95f;

        /// <summary>基準漏出速度（格差1・接触1・機密0のとき per dt）。</summary>
        public readonly float baseLeakRate;
        /// <summary>交易量が接触度へ寄与する重み。</summary>
        public readonly float tradeWeight;
        /// <summary>諜報が接触度へ寄与する重み。</summary>
        public readonly float espionageWeight;
        /// <summary>機密保持1のとき漏出を絞れる最大割合（0..MaxSecrecyBlockCap＝1未満を強制）。</summary>
        public readonly float maxSecrecyBlock;
        /// <summary>模倣コスト係数（自前研究=1に対する倍率。1未満＝後発のコスト優位）。</summary>
        public readonly float imitationCostFactor;
        /// <summary>リープフロッグ可能性のスケール（格差→跳躍可能性への変換率）。</summary>
        public readonly float leapfrogScale;

        public DiffusionParams(float baseLeakRate, float tradeWeight, float espionageWeight,
                               float maxSecrecyBlock, float imitationCostFactor, float leapfrogScale)
        {
            this.baseLeakRate = Mathf.Max(0f, baseLeakRate);
            this.tradeWeight = Mathf.Max(0f, tradeWeight);
            this.espionageWeight = Mathf.Max(0f, espionageWeight);
            // 1.0 を許すと「完全機密＝永久独占」になり設計思想（接触があれば必ず漏れる）が崩れるため上限を切る。
            this.maxSecrecyBlock = Mathf.Clamp(maxSecrecyBlock, 0f, MaxSecrecyBlockCap);
            this.imitationCostFactor = Mathf.Clamp01(imitationCostFactor);
            this.leapfrogScale = Mathf.Max(0f, leapfrogScale);
        }

        /// <summary>既定＝基準漏出0.1・交易重み0.6・諜報重み0.4・機密最大遮断0.8・模倣コスト0.5・跳躍スケール0.5。</summary>
        public static DiffusionParams Default => new DiffusionParams(0.1f, 0.6f, 0.4f, 0.8f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 技術伝播の純ロジック。先進技術は交易・諜報・模倣で他国へ漏れる＝技術独占は時限。
    /// 格差が大きいほど・接触が多いほど速く漏れ、機密保持は漏出を絞るが完全には止められない
    /// （接触がある限り漏出速度は必ず正＝独占の余命は有限）。後発は差に比例して追い上げ
    /// （後発者利益）、模倣は自前研究より安く、レガシー資産が少ないほど最新だけ拾って跳べる
    /// （リープフロッグ）。<see cref="ResearchRules"/>（自前研究＝研究力からの産出）とは別系統で、
    /// ここは「他国からの流入」のみを扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InnovationDiffusionRules
    {
        /// <summary>
        /// 交易・諜報から接触度（0..1）を合成する。交易は広く浅く、諜報は狭く深く漏らす経路で、
        /// 重み付き和を1でクランプする。
        /// </summary>
        public static float ContactLevel(float tradeVolume, float espionage, DiffusionParams p)
        {
            return Mathf.Clamp01(p.tradeWeight * Mathf.Clamp01(tradeVolume)
                                + p.espionageWeight * Mathf.Clamp01(espionage));
        }

        public static float ContactLevel(float tradeVolume, float espionage)
            => ContactLevel(tradeVolume, espionage, DiffusionParams.Default);

        /// <summary>
        /// 技術が漏れる速度（per dt）。格差 techGap(0..1) と接触（交易+諜報）に比例し、機密保持
        /// secrecy(0..1) が最大 maxSecrecyBlock まで絞る。接触ゼロか格差ゼロなら0、接触と格差が
        /// あれば機密全力でも必ず正＝独占は時限。
        /// </summary>
        public static float DiffusionRate(float techGap, float tradeVolume, float espionage, float secrecy, DiffusionParams p)
        {
            float gap = Mathf.Clamp01(techGap);
            float contact = ContactLevel(tradeVolume, espionage, p);
            float pass = 1f - p.maxSecrecyBlock * Mathf.Clamp01(secrecy); // 機密をすり抜ける割合（常に正）
            return p.baseLeakRate * gap * contact * pass;
        }

        public static float DiffusionRate(float techGap, float tradeVolume, float espionage, float secrecy)
            => DiffusionRate(techGap, tradeVolume, espionage, secrecy, DiffusionParams.Default);

        /// <summary>
        /// 後発の技術水準の1tick後の値（0..1）。先進との差に比例して追い上げる（差×rate×dt）＝
        /// 格差が大きいほど一歩が大きい後発者利益。伝播では先進を超えない（リーダー水準でクランプ）。
        /// </summary>
        public static float CatchUpTick(float followerTech, float leaderTech, float rate, float dt)
        {
            float follower = Mathf.Clamp01(followerTech);
            float leader = Mathf.Clamp01(leaderTech);
            if (follower >= leader) return follower; // 流入元が無い＝伝播しない
            float gain = (leader - follower) * Mathf.Max(0f, rate) * Mathf.Max(0f, dt);
            return Mathf.Min(leader, follower + gain);
        }

        /// <summary>
        /// 技術独占の余命目安（漏出の時定数＝格差が約63%埋まるまでの時間）。接触ゼロなら無限大
        /// （永久独占）だが、接触が少しでもあれば機密全力でも有限＝必ず漏れる。
        /// </summary>
        public static float MonopolyDuration(float secrecy, float contact, DiffusionParams p)
        {
            float c = Mathf.Clamp01(contact);
            float pass = 1f - p.maxSecrecyBlock * Mathf.Clamp01(secrecy);
            float rate = p.baseLeakRate * c * pass;
            if (rate <= 0f) return float.PositiveInfinity; // 接触ゼロ（または漏出ゼロ）のみ永続
            return 1f / rate;
        }

        public static float MonopolyDuration(float secrecy, float contact)
            => MonopolyDuration(secrecy, contact, DiffusionParams.Default);

        /// <summary>
        /// 模倣のコスト係数（自前研究=1に対する倍率・1未満＝安い）。既知の解を写すだけなので
        /// 試行錯誤の費用を払わない＝後発のコスト優位。研究コストに掛けて使う（基準非破壊）。
        /// </summary>
        public static float ImitationDiscount(DiffusionParams p)
        {
            return p.imitationCostFactor;
        }

        public static float ImitationDiscount() => ImitationDiscount(DiffusionParams.Default);

        /// <summary>
        /// リープフロッグ可能性（0..1）＝後発が中間世代を飛ばして最新だけ拾って跳ぶ余地。
        /// レガシー資産（既存技術への投資）が少ない＝格差が大きいほど身軽で跳びやすい。
        /// 格差ゼロ以下（並走・先行）なら0。
        /// </summary>
        public static float LeapfrogPotential(float followerTech, float leaderTech, DiffusionParams p)
        {
            float gap = Mathf.Clamp01(leaderTech) - Mathf.Clamp01(followerTech);
            if (gap <= 0f) return 0f;
            return Mathf.Clamp01(gap * p.leapfrogScale);
        }

        public static float LeapfrogPotential(float followerTech, float leaderTech)
            => LeapfrogPotential(followerTech, leaderTech, DiffusionParams.Default);
    }
}
