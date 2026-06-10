using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 回廊要塞の純データ（イゼルローン型＝回廊を扼する移動要塞）。守備隊・反射シールド・主砲（広域砲）を持ち、
    /// 健在な間は回廊の通過を封じる。解決は <see cref="FortressRules"/> が唯一の窓口。攻城（惑星）は
    /// <see cref="PlanetSiegeRules"/> が担い、こちらは回廊上の要塞防御を扱う（別系統）。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class Fortress
    {
        public float garrisonStrength;   // 守備艦隊の戦力
        public float shieldIntegrity;    // 反射シールドの健全度 0..1（液体金属鏡）
        public float mainGunPower;       // 主砲（広域砲＝トゥール・ハンマー型）の威力
        public bool controlsCorridor;    // この要塞が回廊通過を扼すか

        public Fortress() { shieldIntegrity = 1f; controlsCorridor = true; }

        public Fortress(float garrisonStrength, float mainGunPower, float shieldIntegrity = 1f, bool controlsCorridor = true)
        {
            this.garrisonStrength = Mathf.Max(0f, garrisonStrength);
            this.mainGunPower = Mathf.Max(0f, mainGunPower);
            this.shieldIntegrity = Mathf.Clamp01(shieldIntegrity);
            this.controlsCorridor = controlsCorridor;
        }

        /// <summary>シールドが健在か（&gt;0）。</summary>
        public bool ShieldUp => shieldIntegrity > 0f;
    }

    /// <summary>回廊要塞の調整係数（イゼルローン型）。</summary>
    public readonly struct FortressParams
    {
        /// <summary>シールド満タン時の防御倍率の上乗せ（守備戦力に掛かる最大ボーナス）。</summary>
        public readonly float shieldDefenseBonus;
        /// <summary>主砲がシールド健在時のみ撃てる（ダウン中は撃てない）か。</summary>
        public readonly bool mainGunNeedsShield;
        /// <summary>力攻めで陥落させるのに必要な攻撃側/実効防御の戦力比。これ未満は難攻不落（策が要る）。</summary>
        public readonly float assaultRatio;

        public FortressParams(float shieldDefenseBonus, bool mainGunNeedsShield, float assaultRatio)
        {
            this.shieldDefenseBonus = Mathf.Max(0f, shieldDefenseBonus);
            this.mainGunNeedsShield = mainGunNeedsShield;
            this.assaultRatio = Mathf.Max(1f, assaultRatio);
        }

        /// <summary>既定＝シールド防御+200%・主砲はシールド要・力攻め比5.0（事実上難攻不落＝策略前提）。</summary>
        public static FortressParams Default => new FortressParams(2f, true, 5f);
    }

    /// <summary>
    /// 回廊要塞の純ロジック（イゼルローン型）。要塞はシールドの健全度に比例した防御倍率で守備戦力を底上げし、
    /// 主砲は健在なシールド下でのみ広域砲撃を放つ。力攻めには守備実効戦力の assaultRatio 倍が要り、満たない限り
    /// 難攻不落＝正面戦力でなく策略（潜入・補給遮断）でしか落ちない、を表す。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FortressRules
    {
        /// <summary>シールド健全度に比例した防御倍率（1..1+shieldDefenseBonus）。シールド0で素の1.0。</summary>
        public static float DefenseMultiplier(Fortress f, FortressParams p)
        {
            if (f == null) return 1f;
            return 1f + p.shieldDefenseBonus * Mathf.Clamp01(f.shieldIntegrity);
        }

        public static float DefenseMultiplier(Fortress f) => DefenseMultiplier(f, FortressParams.Default);

        /// <summary>守備の実効防御戦力＝守備戦力×防御倍率。</summary>
        public static float EffectiveDefense(Fortress f, FortressParams p)
        {
            if (f == null) return 0f;
            return f.garrisonStrength * DefenseMultiplier(f, p);
        }

        public static float EffectiveDefense(Fortress f) => EffectiveDefense(f, FortressParams.Default);

        /// <summary>
        /// 主砲（広域砲）の発射ダメージ。mainGunNeedsShield かつシールドダウン中は撃てず 0。
        /// 健在時はシールド健全度に比例した威力を返す（要塞のエネルギーはシールドと連動）。
        /// </summary>
        public static float MainGunDamage(Fortress f, FortressParams p)
        {
            if (f == null) return 0f;
            if (p.mainGunNeedsShield && !f.ShieldUp) return 0f;
            float scale = p.mainGunNeedsShield ? Mathf.Clamp01(f.shieldIntegrity) : 1f;
            return f.mainGunPower * scale;
        }

        public static float MainGunDamage(Fortress f) => MainGunDamage(f, FortressParams.Default);

        /// <summary>シールド被弾後の健全度（0..1）。incoming は健全度を削るダメージ割合（0..1）。</summary>
        public static float ShieldAfterHit(float shieldIntegrity, float incoming)
        {
            return Mathf.Clamp01(Mathf.Clamp01(shieldIntegrity) - Mathf.Max(0f, incoming));
        }

        /// <summary>回廊通過を封じているか＝controlsCorridor かつ守備が残存（garrison&gt;0）。</summary>
        public static bool BlocksPassage(Fortress f)
        {
            return f != null && f.controlsCorridor && f.garrisonStrength > 0f;
        }

        /// <summary>
        /// 力攻めで陥落可能か＝攻撃側戦力が実効防御の assaultRatio 倍以上。満たなければ難攻不落（策略が要る）。
        /// </summary>
        public static bool CaptureFeasibleByForce(Fortress f, float attackerStrength, FortressParams p)
        {
            if (f == null) return true;
            float need = EffectiveDefense(f, p) * p.assaultRatio;
            return Mathf.Max(0f, attackerStrength) >= need;
        }

        public static bool CaptureFeasibleByForce(Fortress f, float attackerStrength)
            => CaptureFeasibleByForce(f, attackerStrength, FortressParams.Default);

        /// <summary>難攻不落か（力攻めでは落ちない）＝守備が残り、力攻め条件を満たさない。</summary>
        public static bool IsImpregnable(Fortress f, float attackerStrength, FortressParams p)
        {
            return f != null && f.garrisonStrength > 0f && !CaptureFeasibleByForce(f, attackerStrength, p);
        }

        public static bool IsImpregnable(Fortress f, float attackerStrength)
            => IsImpregnable(f, attackerStrength, FortressParams.Default);
    }
}
