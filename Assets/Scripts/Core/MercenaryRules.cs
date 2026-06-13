using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 傭兵団の純データ（金で雇う戦力＝忠誠は給与に連動）。給与の遅配（payArrears）が忠誠を蝕み、未払いが続けば
    /// 離反・解散しうる。解決は <see cref="MercenaryRules"/> が唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class MercenaryBand
    {
        public float strength;          // 兵力
        public float loyalty;           // 忠誠 0..1（給与で維持）
        public float payArrears;        // 給与遅配の累積 0..1（1＝完全未払い）
        public int contractTurnsLeft;   // 契約残（0で契約満了）

        public MercenaryBand() { loyalty = 1f; }

        public MercenaryBand(float strength, float loyalty = 1f, float payArrears = 0f, int contractTurnsLeft = 0)
        {
            this.strength = Mathf.Max(0f, strength);
            this.loyalty = Mathf.Clamp01(loyalty);
            this.payArrears = Mathf.Clamp01(payArrears);
            this.contractTurnsLeft = Mathf.Max(0, contractTurnsLeft);
        }
    }

    /// <summary>傭兵団の調整係数。</summary>
    public readonly struct MercenaryParams
    {
        /// <summary>未払いが忠誠を削る強さ（payArrears×これだけ忠誠が下がる）。</summary>
        public readonly float arrearsLoyaltyHit;
        /// <summary>支払いで忠誠が回復する強さ。</summary>
        public readonly float payLoyaltyGain;
        /// <summary>1兵力あたりの維持費（給与）。</summary>
        public readonly float upkeepPerStrength;
        /// <summary>離反が起こりうる忠誠の閾値（これ未満で離反判定）。</summary>
        public readonly float defectThreshold;
        /// <summary>戦闘信頼性の下限（低忠誠でも最低これだけは戦う）。</summary>
        public readonly float minReliability;

        public MercenaryParams(float arrearsLoyaltyHit, float payLoyaltyGain, float upkeepPerStrength, float defectThreshold, float minReliability)
        {
            this.arrearsLoyaltyHit = Mathf.Max(0f, arrearsLoyaltyHit);
            this.payLoyaltyGain = Mathf.Max(0f, payLoyaltyGain);
            this.upkeepPerStrength = Mathf.Max(0f, upkeepPerStrength);
            this.defectThreshold = Mathf.Clamp01(defectThreshold);
            this.minReliability = Mathf.Clamp01(minReliability);
        }

        /// <summary>既定＝未払い忠誠減0.5・支払い回復0.3・維持費0.1/戦力・離反閾値0.3・信頼下限0.4。</summary>
        public static MercenaryParams Default => new MercenaryParams(0.5f, 0.3f, 0.1f, 0.3f, 0.4f);
    }

    /// <summary>
    /// 傭兵団の純ロジック。傭兵の忠誠は理念でなく給与で買われる＝支払えば回復し、遅配が続けば蝕まれて離反・解散へ向かう。
    /// 低忠誠の傭兵は戦闘でも手を抜く（信頼性低下＝実効値パターン）。乱数は外から与える roll で決定論的に解決する。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MercenaryRules
    {
        /// <summary>1兵力あたりの給与×兵力＝維持費。</summary>
        public static float Upkeep(float strength, MercenaryParams p) => Mathf.Max(0f, strength) * p.upkeepPerStrength;
        public static float Upkeep(float strength) => Upkeep(strength, MercenaryParams.Default);

        /// <summary>
        /// 給与状況を反映した忠誠（0..1）。paidFraction(0..1)＝今期に支払った割合。満額(1)なら payLoyaltyGain で回復、
        /// 未払いぶん(1−paidFraction)が arrearsLoyaltyHit で忠誠を削る。
        /// </summary>
        public static float LoyaltyAfterPay(float loyalty, float paidFraction, MercenaryParams p)
        {
            float l = Mathf.Clamp01(loyalty);
            float paid = Mathf.Clamp01(paidFraction);
            float arrears = 1f - paid;
            l += paid * p.payLoyaltyGain;
            l -= arrears * p.arrearsLoyaltyHit;
            return Mathf.Clamp01(l);
        }

        public static float LoyaltyAfterPay(float loyalty, float paidFraction)
            => LoyaltyAfterPay(loyalty, paidFraction, MercenaryParams.Default);

        /// <summary>
        /// 離反確率（0..1）。忠誠が defectThreshold 未満のとき、不足分・給与遅配・敵の好条件 enemyOffer(0..1) で上がる。
        /// 閾値以上は 0（離反しない）。
        /// </summary>
        public static float DefectChance(float loyalty, float payArrears, float enemyOffer, MercenaryParams p)
        {
            float l = Mathf.Clamp01(loyalty);
            if (l >= p.defectThreshold) return 0f;
            float shortfall = (p.defectThreshold - l) / Mathf.Max(1e-4f, p.defectThreshold); // 0..1
            float chance = shortfall * (0.5f + 0.25f * Mathf.Clamp01(payArrears) + 0.25f * Mathf.Clamp01(enemyOffer));
            return Mathf.Clamp01(chance);
        }

        public static float DefectChance(float loyalty, float payArrears, float enemyOffer)
            => DefectChance(loyalty, payArrears, enemyOffer, MercenaryParams.Default);

        /// <summary>離反判定。roll∈[0,1) が離反確率未満なら離反＝true（決定論）。</summary>
        public static bool WillDefect(float loyalty, float payArrears, float enemyOffer, float roll, MercenaryParams p)
        {
            return roll < DefectChance(loyalty, payArrears, enemyOffer, p);
        }

        public static bool WillDefect(float loyalty, float payArrears, float enemyOffer, float roll)
            => WillDefect(loyalty, payArrears, enemyOffer, roll, MercenaryParams.Default);

        /// <summary>
        /// 契約満了で解散するか＝契約残0以下、または忠誠が閾値未満（給与で繋ぎ止められず去る）。
        /// </summary>
        public static bool WillDisband(int contractTurnsLeft, float loyalty, MercenaryParams p)
        {
            return contractTurnsLeft <= 0 || Mathf.Clamp01(loyalty) < p.defectThreshold;
        }

        public static bool WillDisband(int contractTurnsLeft, float loyalty)
            => WillDisband(contractTurnsLeft, loyalty, MercenaryParams.Default);

        /// <summary>
        /// 戦闘信頼性（minReliability..1）＝低忠誠ほど手を抜く実効倍率。戦闘戦力に掛けて使う（基準兵力は非破壊）。
        /// </summary>
        public static float CombatReliability(float loyalty, MercenaryParams p)
        {
            return Mathf.Lerp(p.minReliability, 1f, Mathf.Clamp01(loyalty));
        }

        public static float CombatReliability(float loyalty) => CombatReliability(loyalty, MercenaryParams.Default);
    }
}
