using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 歩兵戦闘（地上歩兵の交戦）の調整パラメータ。
    /// すべて ctor で Clamp。基準値は非破壊（実効値パターン）。
    /// </summary>
    public readonly struct InfantryCombatParams
    {
        /// <summary>武器の質が火力に乗る重み（質の効き）。0..1。</summary>
        public readonly float weaponWeight;
        /// <summary>野戦技術が遮蔽活用に乗る重み（技量の効き）。0..1。</summary>
        public readonly float fieldcraftWeight;
        /// <summary>制圧射撃の弾薬依存度（弾切れで制圧が落ちる効き）。0..1。</summary>
        public readonly float ammoWeight;
        /// <summary>火力と運動の機動寄与（撃ちながら前進する機動の重み）。0..1。</summary>
        public readonly float maneuverWeight;
        /// <summary>白兵戦の技量寄与（士気に技術が乗る重み）。0..1。</summary>
        public readonly float meleeWeight;
        /// <summary>分隊結束の下士官統率寄与（訓練に統率が乗る重み）。0..1。</summary>
        public readonly float ncoWeight;
        /// <summary>損耗の苛烈さ（敵火力が遮蔽外の兵を削る率）。</summary>
        public readonly float attritionSeverity;

        public InfantryCombatParams(float weaponWeight, float fieldcraftWeight, float ammoWeight,
            float maneuverWeight, float meleeWeight, float ncoWeight, float attritionSeverity)
        {
            this.weaponWeight = Mathf.Clamp01(weaponWeight);
            this.fieldcraftWeight = Mathf.Clamp01(fieldcraftWeight);
            this.ammoWeight = Mathf.Clamp01(ammoWeight);
            this.maneuverWeight = Mathf.Clamp01(maneuverWeight);
            this.meleeWeight = Mathf.Clamp01(meleeWeight);
            this.ncoWeight = Mathf.Clamp01(ncoWeight);
            this.attritionSeverity = Mathf.Clamp(attritionSeverity, 0f, 2f);
        }

        public static InfantryCombatParams Default =>
            new InfantryCombatParams(0.6f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 1f);
    }

    /// <summary>
    /// 地上歩兵（陸戦隊・装甲擲弾兵）の交戦の純ロジック（盤面非依存・決定論）。
    /// 火力と運動、遮蔽の利用、制圧射撃と機動、士気と練度、白兵戦、分隊戦術、損耗を扱う。
    /// 艦対艦の白兵（<see cref="BoardingActionRules"/>）とは別＝こちらは地上歩兵の地物戦闘。
    /// 0..1 入力で受け、最終戦闘力 <see cref="InfantryEffectiveness"/> は 0..1。
    /// </summary>
    public static class InfantryCombatRules
    {
        /// <summary>
        /// 火力＝武器の質×兵数。質は 0..1、兵数は非負（規模）。
        /// 質0でも頭数ぶんは火を吐く＝(基礎＋重み×質)×兵数。
        /// </summary>
        public static float Firepower(float weaponQuality, float soldierCount, InfantryCombatParams p)
        {
            float q = Mathf.Clamp01(weaponQuality);
            float n = Mathf.Max(0f, soldierCount);
            return n * ((1f - p.weaponWeight) + p.weaponWeight * (1f + q));
        }

        public static float Firepower(float weaponQuality, float soldierCount)
            => Firepower(weaponQuality, soldierCount, InfantryCombatParams.Default);

        /// <summary>
        /// 遮蔽の活用 0..1＝地形遮蔽×野戦技術。地形が良くても技量が要る（掛け算で効く）。
        /// 野戦技術は基礎＋重みで効かせ、地形遮蔽（0..1）を上限とする。
        /// </summary>
        public static float CoverUtilization(float terrainCover, float fieldcraft, InfantryCombatParams p)
        {
            float cover = Mathf.Clamp01(terrainCover);
            float craft = Mathf.Clamp01(fieldcraft);
            float skill = (1f - p.fieldcraftWeight) + p.fieldcraftWeight * craft;
            return Mathf.Clamp01(cover * skill);
        }

        public static float CoverUtilization(float terrainCover, float fieldcraft)
            => CoverUtilization(terrainCover, fieldcraft, InfantryCombatParams.Default);

        /// <summary>
        /// 制圧射撃＝火力×弾薬。弾薬は 0..1（残弾の充足度）。弾切れで制圧が落ちる。
        /// 弾薬は基礎＋重みで効かせる＝枯渇でも完全には止まらない。
        /// </summary>
        public static float SuppressiveFire(float firepower, float ammunition, InfantryCombatParams p)
        {
            float fire = Mathf.Max(0f, firepower);
            float ammo = Mathf.Clamp01(ammunition);
            return fire * ((1f - p.ammoWeight) + p.ammoWeight * ammo);
        }

        public static float SuppressiveFire(float firepower, float ammunition)
            => SuppressiveFire(firepower, ammunition, InfantryCombatParams.Default);

        /// <summary>
        /// 火力と運動＝制圧射撃×機動（撃ちながら前進）。機動要素は 0..1。
        /// 制圧で敵頭を下げ、機動要素ぶん前進力に変える。機動0でも制圧の基礎ぶんは働く。
        /// </summary>
        public static float FireAndMovement(float suppressiveFire, float maneuverElement, InfantryCombatParams p)
        {
            float supp = Mathf.Max(0f, suppressiveFire);
            float mnv = Mathf.Clamp01(maneuverElement);
            return supp * ((1f - p.maneuverWeight) + p.maneuverWeight * (1f + mnv));
        }

        public static float FireAndMovement(float suppressiveFire, float maneuverElement)
            => FireAndMovement(suppressiveFire, maneuverElement, InfantryCombatParams.Default);

        /// <summary>
        /// 白兵戦力＝士気×白兵技術。両者 0..1。塹壕や市街の最終局面で効く接近戦の強さ 0..1。
        /// 士気を基盤に技術が乗る（meleeWeight）。
        /// </summary>
        public static float CloseAssault(float soldierMorale, float meleeSkill, InfantryCombatParams p)
        {
            float morale = Mathf.Clamp01(soldierMorale);
            float skill = Mathf.Clamp01(meleeSkill);
            return Mathf.Clamp01(morale * ((1f - p.meleeWeight) + p.meleeWeight * (1f + skill)));
        }

        public static float CloseAssault(float soldierMorale, float meleeSkill)
            => CloseAssault(soldierMorale, meleeSkill, InfantryCombatParams.Default);

        /// <summary>
        /// 分隊の結束 0..1＝分隊の訓練×下士官の統率。両者 0..1。
        /// 訓練を基盤に下士官統率が乗る（ncoWeight）。結束が高いほど崩れず連携する。
        /// </summary>
        public static float SquadCohesion(float unitTraining, float ncoLeadership, InfantryCombatParams p)
        {
            float train = Mathf.Clamp01(unitTraining);
            float nco = Mathf.Clamp01(ncoLeadership);
            return Mathf.Clamp01(train * ((1f - p.ncoWeight) + p.ncoWeight * (1f + nco)));
        }

        public static float SquadCohesion(float unitTraining, float ncoLeadership)
            => SquadCohesion(unitTraining, ncoLeadership, InfantryCombatParams.Default);

        /// <summary>
        /// 歩兵の損耗 0..1＝敵火力×(1-遮蔽)。遮蔽の外に居るぶんだけ削られる。
        /// 敵火力は severity でならし飽和（x/(1+x)）させて 0..1 に収める。
        /// </summary>
        public static float CombatAttrition(float enemyFirepower, float coverUtilization, InfantryCombatParams p)
        {
            float exposure = 1f - Mathf.Clamp01(coverUtilization);
            float incoming = Mathf.Max(0f, enemyFirepower) * p.attritionSeverity * exposure;
            return Mathf.Clamp01(incoming / (1f + incoming));
        }

        public static float CombatAttrition(float enemyFirepower, float coverUtilization)
            => CombatAttrition(enemyFirepower, coverUtilization, InfantryCombatParams.Default);

        /// <summary>
        /// 歩兵戦闘力 0..1＝火力運動×結束−損耗。
        /// 火力と運動を結束（0..1）で割り引き、損耗（0..1）ぶん差し引く。
        /// 火力運動は飽和（x/(1+x)）で 0..1 に写してから合成する。
        /// </summary>
        public static float InfantryEffectiveness(float fireAndMovement, float squadCohesion,
            float combatAttrition, InfantryCombatParams p)
        {
            float fm = Mathf.Max(0f, fireAndMovement);
            float fmNorm = fm / (1f + fm); // 0..1
            float cohesion = Mathf.Clamp01(squadCohesion);
            float loss = Mathf.Clamp01(combatAttrition);
            return Mathf.Clamp01(fmNorm * cohesion - loss);
        }

        public static float InfantryEffectiveness(float fireAndMovement, float squadCohesion, float combatAttrition)
            => InfantryEffectiveness(fireAndMovement, squadCohesion, combatAttrition, InfantryCombatParams.Default);
    }
}
