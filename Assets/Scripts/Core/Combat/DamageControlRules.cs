using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ダメージコントロール（応急処置）の調整値。延焼/浸水の拡大係数、ダメコン班の効力、被害局限の効きを束ねる。
    /// 全フィールドは ctor で Clamp 済み（負値・暴走を防ぐ）。実効値パターン（基準値非破壊）。
    /// </summary>
    public readonly struct DamageControlParams
    {
        /// <summary>延焼/浸水が時間で拡大する速さ（経過1単位あたりの倍率の伸び）。0で時間無関係。</summary>
        public readonly float spreadRate;
        /// <summary>危険種別（火災/浸水/誘爆等）の危険度倍率。1で標準。</summary>
        public readonly float hazardScale;
        /// <summary>乗員1人あたりのダメコン基礎効力。</summary>
        public readonly float effortPerCrew;
        /// <summary>隔壁による被害局限の最大カット率（0..1）。健全な隔壁で被害をどこまで封じ込められるか。</summary>
        public readonly float sealMaxReduction;
        /// <summary>予備系統1基あたりが補える重要区画被弾の数。</summary>
        public readonly float redundancyPerBackup;
        /// <summary>戦闘能力喪失で冗長が効く重み（重要区画損失をどれだけ和らげるか）。</summary>
        public readonly float redundancyWeight;

        public DamageControlParams(float spreadRate, float hazardScale, float effortPerCrew,
            float sealMaxReduction, float redundancyPerBackup, float redundancyWeight)
        {
            this.spreadRate = Mathf.Max(0f, spreadRate);
            this.hazardScale = Mathf.Max(0f, hazardScale);
            this.effortPerCrew = Mathf.Max(0f, effortPerCrew);
            this.sealMaxReduction = Mathf.Clamp01(sealMaxReduction);
            this.redundancyPerBackup = Mathf.Max(0f, redundancyPerBackup);
            this.redundancyWeight = Mathf.Clamp01(redundancyWeight);
        }

        /// <summary>既定：拡大0.5/危険1.0/乗員効力1.0/隔壁最大カット0.8/予備1基=1区画/冗長重み0.5。</summary>
        public static DamageControlParams Default => new DamageControlParams(
            DefaultSpreadRate, DefaultHazardScale, DefaultEffortPerCrew,
            DefaultSealMaxReduction, DefaultRedundancyPerBackup, DefaultRedundancyWeight);

        public const float DefaultSpreadRate = 0.5f;
        public const float DefaultHazardScale = 1.0f;
        public const float DefaultEffortPerCrew = 1.0f;
        public const float DefaultSealMaxReduction = 0.8f;
        public const float DefaultRedundancyPerBackup = 1.0f;
        public const float DefaultRedundancyWeight = 0.5f;
    }

    /// <summary>
    /// 被弾後のダメージコントロール（応急処置と生存）の純ロジック。
    /// 延焼/浸水の拡大、ダメコン班の鎮火、区画隔壁による被害局限、重要区画の冗長性、
    /// 戦闘継続能力の低下、轟沈と航行不能の分岐を決定論で扱う（乱数NG・入力clamp・実効値パターン）。
    /// 各メソッドは Params 明示版＋既定委譲版を持つ。test-first。
    /// </summary>
    public static class DamageControlRules
    {
        /// <summary>戦闘能力喪失と浸水が「艦喪失」とみなされる既定閾値。</summary>
        public const float DefaultLossThreshold = 0.85f;

        /// <summary>
        /// 延焼/浸水の拡大量。初期被害×危険度×(1＋拡大速さ×経過)。
        /// containmentTime が長いほど（鎮圧が遅いほど）拡大する。返り値は >=0。
        /// </summary>
        public static float DamageSpread(float initialDamage, float hazardType, float containmentTime)
            => DamageSpread(initialDamage, hazardType, containmentTime, DamageControlParams.Default);

        public static float DamageSpread(float initialDamage, float hazardType, float containmentTime, DamageControlParams p)
        {
            float init = Mathf.Max(0f, initialDamage);
            float hazard = Mathf.Max(0f, hazardType) * p.hazardScale;
            float time = Mathf.Max(0f, containmentTime);
            float growth = 1f + p.spreadRate * time;
            return Mathf.Max(0f, init * hazard * growth);
        }

        /// <summary>
        /// ダメコン班の効力。乗員技量(0..1)×人手×乗員効力。人手が多く技量が高いほど効力が上がる。返り値は >=0。
        /// </summary>
        public static float DamageControlEffort(float crewSkill, float crewAvailable)
            => DamageControlEffort(crewSkill, crewAvailable, DamageControlParams.Default);

        public static float DamageControlEffort(float crewSkill, float crewAvailable, DamageControlParams p)
        {
            float skill = Mathf.Clamp01(crewSkill);
            float crew = Mathf.Max(0f, crewAvailable);
            return Mathf.Max(0f, skill * crew * p.effortPerCrew);
        }

        /// <summary>
        /// 被害の鎮圧度合い（0..1）。効力/(効力+拡大)。効力が拡大を上回るほど1へ近づく。
        /// 拡大0なら完全鎮圧（1）、効力0なら鎮圧不能（0）。
        /// </summary>
        public static float Containment(float damageControlEffort, float damageSpread)
            => Containment(damageControlEffort, damageSpread, DamageControlParams.Default);

        public static float Containment(float damageControlEffort, float damageSpread, DamageControlParams p)
        {
            float effort = Mathf.Max(0f, damageControlEffort);
            float spread = Mathf.Max(0f, damageSpread);
            if (spread <= 0f) return 1f;          // 拡大なし＝完全鎮圧
            if (effort <= 0f) return 0f;          // 効力なし＝鎮圧不能
            return Mathf.Clamp01(effort / (effort + spread));
        }

        /// <summary>
        /// 区画隔壁による被害局限率（0..1）。隔壁健全性(0..1)×最大カット率。
        /// 健全な隔壁ほど被害をその区画に封じ込め、艦全体への波及を抑える。
        /// </summary>
        public static float CompartmentSealing(float bulkheadIntegrity)
            => CompartmentSealing(bulkheadIntegrity, DamageControlParams.Default);

        public static float CompartmentSealing(float bulkheadIntegrity, DamageControlParams p)
        {
            float integrity = Mathf.Clamp01(bulkheadIntegrity);
            return Mathf.Clamp01(integrity * p.sealMaxReduction);
        }

        /// <summary>
        /// 重要区画被弾を予備系統がどれだけ補えるか（0..1）。補える数=予備基数×予備1基あたり能力。
        /// 補填/被弾数 で「冗長で救えた割合」を返す。被弾0なら欠損なし＝1。
        /// </summary>
        public static float SystemRedundancy(float criticalHits, float backupSystems)
            => SystemRedundancy(criticalHits, backupSystems, DamageControlParams.Default);

        public static float SystemRedundancy(float criticalHits, float backupSystems, DamageControlParams p)
        {
            float hits = Mathf.Max(0f, criticalHits);
            float backups = Mathf.Max(0f, backupSystems);
            if (hits <= 0f) return 1f;            // 重要区画無傷＝完全冗長
            float covered = backups * p.redundancyPerBackup;
            return Mathf.Clamp01(covered / hits);
        }

        /// <summary>
        /// 戦闘能力の喪失（0..1）。拡大被害を正規化し、鎮圧と重要区画冗長で差し引く。
        /// (1-containment) で鎮圧しきれぬ被害、(1-redundancy)×redundancyWeight で重要区画損失を上乗せ。
        /// 0＝健在、1＝戦闘不能。
        /// </summary>
        public static float CombatCapabilityLoss(float damageSpread, float containment, float systemRedundancy)
            => CombatCapabilityLoss(damageSpread, containment, systemRedundancy, DamageControlParams.Default);

        public static float CombatCapabilityLoss(float damageSpread, float containment, float systemRedundancy, DamageControlParams p)
        {
            float spread = Mathf.Max(0f, damageSpread);
            float contained = Mathf.Clamp01(containment);
            float redundancy = Mathf.Clamp01(systemRedundancy);

            // 拡大被害を 0..1 へ正規化（spread が大きいほど 1 へ漸近・Mathf.Log/Exp 禁止のため有理式）。
            float normalized = spread / (spread + 1f);
            float unfought = normalized * (1f - contained);                       // 鎮圧しきれぬ被害
            float criticalLoss = (1f - redundancy) * p.redundancyWeight;          // 重要区画損失
            return Mathf.Clamp01(unfought + criticalLoss);
        }

        /// <summary>
        /// 浸水の進行（0..1）。破孔×(1-排水能力)を正規化。排水が破孔に追いつかぬほど沈没へ。
        /// 破孔0なら浸水なし（0）、排水能力>=1なら完全排水（0）。
        /// </summary>
        public static float FloodingProgression(float hullBreaches, float pumpCapacity)
            => FloodingProgression(hullBreaches, pumpCapacity, DamageControlParams.Default);

        public static float FloodingProgression(float hullBreaches, float pumpCapacity, DamageControlParams p)
        {
            float breaches = Mathf.Max(0f, hullBreaches);
            float pump = Mathf.Clamp01(pumpCapacity);
            if (breaches <= 0f) return 0f;        // 破孔なし＝浸水なし
            float net = breaches * (1f - pump);   // 排水で打ち消した実効破孔
            if (net <= 0f) return 0f;
            return Mathf.Clamp01(net / (net + 1f));
        }

        /// <summary>戦闘能力喪失か浸水進行が閾値を超えたら艦は失われる（轟沈/航行不能）。既定閾値版。</summary>
        public static bool IsShipLost(float combatCapabilityLoss, float floodingProgression)
            => IsShipLost(combatCapabilityLoss, floodingProgression, DefaultLossThreshold);

        public static bool IsShipLost(float combatCapabilityLoss, float floodingProgression, float threshold)
        {
            float loss = Mathf.Clamp01(combatCapabilityLoss);
            float flood = Mathf.Clamp01(floodingProgression);
            float th = Mathf.Clamp01(threshold);
            return loss >= th || flood >= th;     // 戦闘不能 もしくは 沈没
        }
    }
}
