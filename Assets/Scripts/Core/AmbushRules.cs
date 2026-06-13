using UnityEngine;

namespace Ginei
{
    /// <summary>伏兵・奇襲の調整係数。</summary>
    public readonly struct AmbushParams
    {
        /// <summary>奇襲成立時の初撃ダメージ倍率（1超）。</summary>
        public readonly float firstStrikeMultiplier;
        /// <summary>被奇襲側の隊形不備が回復するまでの時間。</summary>
        public readonly float disarrayDuration;
        /// <summary>奇襲を受けた瞬間の士気ショック量。</summary>
        public readonly float moraleShock;

        public AmbushParams(float firstStrikeMultiplier, float disarrayDuration, float moraleShock)
        {
            this.firstStrikeMultiplier = Mathf.Max(1f, firstStrikeMultiplier);
            this.disarrayDuration = Mathf.Max(0f, disarrayDuration);
            this.moraleShock = Mathf.Max(0f, moraleShock);
        }

        /// <summary>既定＝初撃1.5倍・隊形回復10・士気ショック0.3。</summary>
        public static AmbushParams Default => new AmbushParams(1.5f, 10f, 0.3f);
    }

    /// <summary>
    /// 伏兵・奇襲の純ロジック。奇襲の成立は伏側の秘匿度と受側の警戒度の綱引きで決まり（探知そのものは
    /// <see cref="ReconRules"/> が担う＝その結果を入力に取る）、成立すれば初撃倍率・隊形不備・士気ショックが
    /// 受側を襲う。隊形不備は時間で回復する＝奇襲の窓は最初だけ。乱数は roll∈[0,1) で決定論。
    /// 倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AmbushRules
    {
        /// <summary>
        /// 奇襲成功率（0..1）＝伏側の秘匿度(0..1)×（1−受側の警戒度(0..1)）。
        /// 警戒が完全なら奇襲はない（探知に成功した待ち伏せはただの会戦）。
        /// </summary>
        public static float AmbushChance(float concealment, float alertness)
        {
            return Mathf.Clamp01(Mathf.Clamp01(concealment) * (1f - Mathf.Clamp01(alertness)));
        }

        /// <summary>奇襲判定。roll∈[0,1) が成功率未満なら成立＝true（決定論）。</summary>
        public static bool IsSprung(float concealment, float alertness, float roll)
        {
            return roll < AmbushChance(concealment, alertness);
        }

        /// <summary>奇襲側の初撃ダメージ倍率。奇襲不成立なら1.0（通常戦）。</summary>
        public static float FirstStrikeFactor(bool surprised, AmbushParams p)
        {
            return surprised ? p.firstStrikeMultiplier : 1f;
        }

        public static float FirstStrikeFactor(bool surprised) => FirstStrikeFactor(surprised, AmbushParams.Default);

        /// <summary>
        /// 被奇襲側の隊形不備係数（0..1、1=回復済み）。奇襲からの経過時間 timeSinceAmbush が
        /// disarrayDuration に達するまで線形に回復する＝奇襲の優位は時間で消える。
        /// </summary>
        public static float DisarrayRecovery(float timeSinceAmbush, AmbushParams p)
        {
            if (p.disarrayDuration <= 0f) return 1f;
            return Mathf.Clamp01(Mathf.Max(0f, timeSinceAmbush) / p.disarrayDuration);
        }

        public static float DisarrayRecovery(float timeSinceAmbush)
            => DisarrayRecovery(timeSinceAmbush, AmbushParams.Default);

        /// <summary>
        /// 被奇襲側の実効戦闘倍率（0..1）＝回復係数そのもの（回復0で半減以下にならないよう下限0.5）。
        /// 基準値に掛けて使う。
        /// </summary>
        public static float VictimCombatFactor(float timeSinceAmbush, AmbushParams p)
        {
            const float FloorFactor = 0.5f; // 不意を突かれても半分は戦える
            return Mathf.Lerp(FloorFactor, 1f, DisarrayRecovery(timeSinceAmbush, p));
        }

        public static float VictimCombatFactor(float timeSinceAmbush)
            => VictimCombatFactor(timeSinceAmbush, AmbushParams.Default);

        /// <summary>奇襲を受けた瞬間の士気ショック（0..moraleShock）＝奇襲規模 surpriseScale(0..1) に比例。</summary>
        public static float MoraleShock(float surpriseScale, AmbushParams p)
        {
            return Mathf.Clamp01(surpriseScale) * p.moraleShock;
        }

        public static float MoraleShock(float surpriseScale) => MoraleShock(surpriseScale, AmbushParams.Default);
    }
}
