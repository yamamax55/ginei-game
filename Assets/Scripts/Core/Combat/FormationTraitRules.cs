namespace Ginei
{
    /// <summary>陣形の戦術特性（#72・会戦改善）。攻撃/被ダメージ/機動の倍率（実効値パターン）。</summary>
    public struct FormationTrait
    {
        public float attack;      // 与ダメージ倍率（>1＝火力高）
        public float damageTaken; // 被ダメージ倍率（<1＝堅い／>1＝脆い）
        public float mobility;    // 速度/回頭倍率（>1＝機動高）

        public FormationTrait(float attack, float damageTaken, float mobility)
        {
            this.attack = attack;
            this.damageTaken = damageTaken;
            this.mobility = mobility;
        }
    }

    /// <summary>
    /// 陣形ごとの戦術特性（#72・史実ベースのメリット/デメリット）。`CombatModifiers` 方針に沿った係数の単一窓口。
    /// 紡錘陣＝中央突破（攻撃・機動↑／薄く脆い）／鶴翼陣＝包囲（攻撃↑↑／中央薄く展開鈍い）／円陣＝全周防御（堅い／火力・機動↓）／
    /// 横陣＝全砲門の最大火力（攻撃↑↑↑／側背面に脆く回頭鈍い）／方陣＝均整の防御（堅い／火力控えめ・鈍重）。
    /// `ShipCombat.ComputeDamage`(攻撃)・`FleetStrength.TakeDamage`(防御)・`FleetMovement.GetMobilityFactor`(機動) が消費。test-first。
    /// </summary>
    public static class FormationTraitRules
    {
        /// <summary>陣形の戦術特性（史実ベース）。</summary>
        public static FormationTrait TraitOf(Formation f)
        {
            switch (f)
            {
                //                              攻撃   被ダメ 機動
                case Formation.紡錘陣: return new FormationTrait(1.15f, 1.10f, 1.15f); // 中央突破＝攻撃/機動↑・脆い
                case Formation.鶴翼陣: return new FormationTrait(1.20f, 1.15f, 0.95f); // 包囲＝攻撃↑↑・中央薄く鈍い
                case Formation.円陣:   return new FormationTrait(0.80f, 0.80f, 0.85f); // 全周防御＝堅い・火力/機動↓
                case Formation.横陣:   return new FormationTrait(1.25f, 1.10f, 0.90f); // 全砲門＝最大火力・側背面に脆く回頭鈍い
                case Formation.方陣:   return new FormationTrait(0.95f, 0.85f, 0.80f); // 均整の防御＝堅い・火力控えめ・鈍重
                default:               return new FormationTrait(1.00f, 1.00f, 1.00f);
            }
        }

        /// <summary>与ダメージ倍率（攻撃側陣形）。</summary>
        public static float AttackFactor(Formation f) => TraitOf(f).attack;

        /// <summary>被ダメージ倍率（防御側陣形・&lt;1で堅い）。</summary>
        public static float DamageTakenFactor(Formation f) => TraitOf(f).damageTaken;

        /// <summary>速度/回頭倍率（自陣形）。</summary>
        public static float MobilityFactor(Formation f) => TraitOf(f).mobility;
    }
}
