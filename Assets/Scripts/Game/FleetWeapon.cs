using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 艦隊の武装制御および攻撃を管理するクラス。
    /// 射界内の敵を自動攻撃し、側背面からの攻撃にはボーナスを与えます。
    /// </summary>
    [RequireComponent(typeof(WeaponArc))]
    public class FleetWeapon : MonoBehaviour
    {
        [Header("攻撃設定")]
        public int damage = 100;
        public float fireInterval = 1.0f;
        [Tooltip("側背面攻撃時の最大ダメージ倍率 (真後ろで最大)")]
        public float flankMultiplier = 2.0f;
        [Tooltip("武器種（#2256）：長距離砲は対旗艦↑/対配下艦↓、対小型は対配下艦↑/対旗艦↓、点防御は両方控えめ")]
        public WeaponType weaponType = WeaponType.ビーム;

        [Header("ミサイル攻撃設定")]
        [Tooltip("ミサイルの残弾数（0で通常攻撃に移行）")]
        public int missileAmmo = 10;
        [Tooltip("ミサイルのダメージ倍率（通常攻撃比）")]
        public float missileDamageMultiplier = 3f;
        [Tooltip("ミサイル発射時のビーム色")]
        public Color missileBeamColor = new Color(1f, 0.5f, 0.1f);

        [Tooltip("交戦中の機動性倍率 (0.0〜1.0)")]
        public float combatMobilityRatio = 0.3f;

        [Tooltip("手動攻撃目標を追尾する際、射程のこの割合まで接近したら停止して交戦する")]
        public float pursuitStopRatio = 0.9f;

        [Header("ランチェスター集中効果（会戦ダメージ）")]
        [Tooltip("一定範囲内の局所火力差でダメージを増減する（ランチェスター二乗則）")]
        public bool enableLanchester = true;
        [Tooltip("局所火力を集計する半径（この範囲内の味方/敵旗艦の火力差で1発の重みが変わる）")]
        public float lanchesterRange = 10f;
        [Tooltip("局所火力集中倍率を再計算する間隔（秒・間引き）")]
        public float lanchesterUpdateInterval = 0.4f;

        /// <summary>直近に算出した局所火力集中倍率（部隊単位でキャッシュ＝配下艦も流用・終盤ラグ回避）。</summary>
        public float LanchesterFactor { get; private set; } = 1f;
        private float nextLanchesterTime;

        // ダメージ内訳サンプリング（#2252）：プレイヤー勢力のみ・間引きで再利用（ホットパス無確保）。
        private DamageBreakdown sampleBreakdown;
        private float nextBreakdownSampleTime;
        private const float BreakdownSampleInterval = 2.5f;

        [Header("演出設定")]
        public float beamWidth = 0.2f;
        public float beamDuration = 0.1f;
        public Color beamColor = Color.cyan;

        public bool IsInCombat { get; private set; }

        private WeaponArc weaponArc;
        private FleetStrength myStrength;
        private LineRenderer beamLine;
        private float nextFireTime;
        private float lastFireTime = -100f;
        private IShipTarget manualTarget;
        private Squadron manualTargetFleet; // 艦隊単位の攻撃目標（旗艦だけでなく艦隊全体を狙う）
        private FleetMorale moraleComponent;
        private FleetMovement movement;
        private FleetAI fleetAI;
        private Material beamMaterial; // 実行時生成。OnDestroyで破棄
        private bool useMissiles;      // 手動攻撃指示でミサイルを選んだか（弾切れで自動解除）
        private Color shotBeamColor;   // 直近ショットのビーム色（通常/ミサイルで切替）
        private Color gradientColor = new Color(-1f, -1f, -1f, -1f); // 適用済みビーム色（変化時のみグラデ再構築）

        private void Awake()
        {
            weaponArc = GetComponent<WeaponArc>();
            myStrength = GetComponent<FleetStrength>();
            moraleComponent = GetComponent<FleetMorale>();
            movement = GetComponent<FleetMovement>();
            fleetAI = GetComponent<FleetAI>();
            shotBeamColor = beamColor;
            
            // ビーム表示用のLineRenderer（既にあれば再利用。WeaponArcとの二重追加・プレハブ焼き込み対策）
            beamLine = GetComponent<LineRenderer>();
            if (beamLine == null) beamLine = gameObject.AddComponent<LineRenderer>();
            // 見た目は BeamFx に集約（白熱コア＋テーパー幅＋放電フェード）。描画ロジックを EscortShip と重複させない。
            BeamFx.ConfigureLine(beamLine, beamWidth);
            beamMaterial = BeamFx.CreateMaterial();
            beamLine.material = beamMaterial;
            EnsureBeamGradient(beamColor);
        }

        private void OnDestroy()
        {
            // 実行時生成したマテリアルを破棄（リーク防止）
            if (beamMaterial != null) Destroy(beamMaterial);
        }

        private void Update()
        {
            // 非戦闘艦（偵察/入植/輸送 #128）は攻撃しない（交戦状態も持たない）。
            if (myStrength != null && !myStrength.IsCombatant)
            {
                IsInCombat = false;
                return;
            }

            // 静観（#817 関ヶ原型・フリーライダー #820）：山上で動かない＝発砲しない
            if (myStrength != null && !myStrength.IsFighting)
            {
                IsInCombat = false;
                return;
            }

            // 旗艦喪失で退却中は発砲停止（交戦状態も解除して機動低下を起こさない）
            if (myStrength != null && myStrength.IsRetreating)
            {
                IsInCombat = false;
                return;
            }

            // ランチェスター集中倍率を間引きで再計算し部隊単位でキャッシュ（配下艦も流用＝終盤ラグ回避）。
            UpdateLanchesterFactor();

            // 交戦状態の判定
            // 1. 直近 fireInterval 秒以内に発砲したか
            // 2. 射界内に敵がいるか
            bool enemyInArc = CheckEnemyInArc();
            IsInCombat = (Time.time < lastFireTime + fireInterval) || enemyInArc;

            // 手動攻撃目標の追尾（プレイヤー指定時）。移動中の敵を追う。
            HandlePursuit();

            // 自動攻撃 または 指定ターゲット攻撃
            if (Time.time >= nextFireTime)
            {
                // 手動指定（艦隊/単艦）の射界内の艦を優先。無ければ自動攻撃。
                IShipTarget manualShot = ResolveManualShotTarget();
                if (manualShot != null)
                {
                    // 先にクールダウンを進める（PerformAttackが例外でも毎フレーム暴走しない）
                    nextFireTime = Time.time + fireInterval;
                    PerformAttack(manualShot);
                }
                else
                {
                    // 射程外・無効なら自動攻撃に切り替え
                    AttackNearestEnemyInArc();
                }
            }
        }

        /// <summary>
        /// 手動攻撃目標のうち、いま撃つべき射界内の艦を返す。
        /// 艦隊指定なら指定艦隊の射界内最寄り艦、単艦指定なら射界内のその艦。無ければ null。
        /// </summary>
        private IShipTarget ResolveManualShotTarget()
        {
            if (manualTargetFleet != null)
            {
                if (!IsFleetAlive(manualTargetFleet)) { manualTargetFleet = null; useMissiles = false; return null; }
                return ShipCombat.FindNearestEnemyInArcOfFleet(transform.position, transform.up,
                    weaponArc.range, weaponArc.halfAngle, manualTargetFleet);
            }

            if (ShipCombat.IsValidTarget(manualTarget) && weaponArc.IsInArc(manualTarget.Transform))
                return manualTarget;

            return null;
        }

        /// <summary>指定艦隊の旗艦が生存しているか（旗艦退却＝艦隊消滅）。</summary>
        private static bool IsFleetAlive(Squadron fleet)
        {
            if (fleet == null) return false;
            FleetStrength flag = fleet.GetComponent<FleetStrength>();
            return flag != null && flag.IsAlive;
        }

        /// <summary>
        /// 射程・射角内に敵（旗艦＋配下艦）がいるかのみを判定します（毎フレーム呼ぶため軽量化に注意）
        /// </summary>
        private bool CheckEnemyInArc()
        {
            return ShipCombat.AnyEnemyInArc(transform.position, transform.up, myStrength.factionData, myStrength.faction,
                EffectiveRange(), weaponArc.halfAngle);
        }

        /// <summary>地形（星雲/小惑星帯 #2181）による射程低下を反映した実効射程。</summary>
        private float EffectiveRange()
            => weaponArc.range * BattleTerrain.RangeFactorAt(transform.position);

        /// <summary>
        /// 攻撃ターゲットを手動で指定します（旗艦・配下艦のどちらも指定可）。null で解除。
        /// </summary>
        public void SetManualTarget(IShipTarget target)
        {
            manualTarget = target;
            manualTargetFleet = null;
        }

        /// <summary>攻撃目標を「敵艦隊(部隊)全体」として指定する（旗艦単艦ではなく艦隊を狙う）。</summary>
        public void SetManualTargetFleet(Squadron fleet)
        {
            manualTargetFleet = fleet;
            manualTarget = null;
        }

        /// <summary>手動攻撃目標（単艦・艦隊とも）を解除する。ミサイルモードも解除。</summary>
        public void ClearManualTarget()
        {
            manualTarget = null;
            manualTargetFleet = null;
            useMissiles = false;
        }

        /// <summary>
        /// 手動攻撃目標が指定艦隊（またはその所属個艦）なら解除する。
        /// 寝返り（#817）で陣営が変わった艦隊を、旧敵が手動目標のまま撃ち続けないための整理用。
        /// </summary>
        public void ClearManualTargetIfFleet(Squadron fleet)
        {
            if (fleet == null) return;
            if (manualTargetFleet == fleet) { ClearManualTarget(); return; }
            if (manualTarget != null && ShipCombat.GetSquadronOf(manualTarget) == fleet) ClearManualTarget();
        }

        /// <summary>ミサイル攻撃モードの切替（残弾がある間だけミサイルで撃つ。弾切れで自動的に通常攻撃へ）。</summary>
        public void SetMissileMode(bool on)
        {
            useMissiles = on && missileAmmo > 0;
        }

        /// <summary>現在の手動攻撃目標があるか（単艦または艦隊）。</summary>
        public bool HasManualTarget => ShipCombat.IsValidTarget(manualTarget) || IsFleetAlive(manualTargetFleet);

        /// <summary>ミサイルの残弾数（HUD等の表示用）。</summary>
        public int MissileAmmo => missileAmmo;

        /// <summary>
        /// 手動攻撃目標を追尾する。射程外なら接近、射程内なら停止して敵を向き続ける。
        /// AI 制御中の艦（敵・非プレイヤー）は対象外（FleetAI に移動を任せる）。
        /// 目標が撃沈・退却で無効になったら追尾と指定を解除する。
        /// </summary>
        private void HandlePursuit()
        {
            if (movement == null) return;
            if (fleetAI != null && fleetAI.enabled) return; // AI が動かしている艦は追尾しない

            // 艦隊指定：敵艦隊の旗艦位置を追尾の基準にする
            if (manualTargetFleet != null)
            {
                if (!IsFleetAlive(manualTargetFleet)) { manualTargetFleet = null; useMissiles = false; return; }
                FleetStrength enemyFlag = manualTargetFleet.GetComponent<FleetStrength>();
                if (enemyFlag != null) PursueToward(enemyFlag.transform.position);
                return;
            }

            // 単艦指定：目標が無効化（撃沈・退却）したら指定解除
            if (manualTarget != null && !ShipCombat.IsValidTarget(manualTarget))
            {
                manualTarget = null;
                return;
            }
            if (manualTarget == null) return;

            PursueToward(manualTarget.Transform.position);
        }

        /// <summary>射程外なら接近、射程内なら停止して目標方向を向き続ける。</summary>
        private void PursueToward(Vector3 targetPos)
        {
            float dist = Vector2.Distance(transform.position, targetPos);
            if (dist > weaponArc.range * pursuitStopRatio)
            {
                // 射程外：移動目標を毎フレーム更新して移動中の敵を追尾
                movement.SetDestination(targetPos);
            }
            else
            {
                // 射程内：前進を止め、その場で敵を向いて射界を維持
                movement.FaceTarget(targetPos);
            }
        }

        /// <summary>
        /// 射界内の最も近い敵個艦（旗艦 or 配下艦）を検索して攻撃します。
        /// </summary>
        private void AttackNearestEnemyInArc()
        {
            IShipTarget nearest = ShipCombat.FindNearestEnemyInArc(transform.position, transform.up,
                myStrength.factionData, myStrength.faction, EffectiveRange(), weaponArc.halfAngle);

            if (nearest != null)
            {
                // 先にクールダウンを進める（PerformAttackが例外でも毎フレーム暴走しない）
                nextFireTime = Time.time + fireInterval;
                PerformAttack(nearest);
            }
        }

        /// <summary>
        /// 一定範囲内の局所火力差からランチェスター集中倍率を間引きで再計算し、部隊単位でキャッシュする。
        /// 配下艦（EscortShip）はこの旗艦の値を流用するので、計算は旗艦1回／間隔ぶんに抑えられる（終盤ラグ回避）。
        /// </summary>
        private void UpdateLanchesterFactor()
        {
            if (!enableLanchester) { LanchesterFactor = 1f; return; }
            if (Time.time < nextLanchesterTime) return;
            nextLanchesterTime = Time.time + Mathf.Max(0.05f, lanchesterUpdateInterval);

            ShipCombat.LocalFirepower(transform.position,
                myStrength != null ? myStrength.factionData : null,
                myStrength != null ? myStrength.faction : Faction.帝国,
                lanchesterRange, out float friendly, out float enemy);
            LanchesterFactor = LanchesterRules.ConcentrationFactor(friendly, enemy);
        }

        /// <summary>ダメージ内訳を今サンプリングするか（プレイヤー勢力の旗艦・間引き間隔経過時のみ）。</summary>
        private bool ShouldSampleBreakdown()
        {
            if (myStrength == null || GameSettings.Instance == null) return false;
            if (myStrength.faction != GameSettings.Instance.playerFaction) return false;
            return Time.time >= nextBreakdownSampleTime;
        }

        /// <summary>
        /// 指定した個艦に攻撃を実行し、ダメージを計算します。
        /// </summary>
        private void PerformAttack(IShipTarget target)
        {
            lastFireTime = Time.time;

            // 士気による補正
            float moraleFactor = moraleComponent != null ? moraleComponent.GetMoraleFactor() : 1.0f;

            // ミサイル攻撃：残弾があれば威力を強化し1発消費。弾切れで通常攻撃へ移行。
            int baseDamage = damage;
            bool firedMissile = false;
            if (useMissiles && missileAmmo > 0)
            {
                baseDamage = Mathf.RoundToInt(damage * missileDamageMultiplier);
                missileAmmo--;
                firedMissile = true;
                if (missileAmmo <= 0) useMissiles = false; // 弾切れ→以降は通常攻撃
            }
            shotBeamColor = firedMissile ? missileBeamColor : beamColor;

            // 軍団長の能力バフ/デバフ（CSG）：基準ダメージに乗算（既定1.0で挙動不変・実効値パターン）。
            if (myStrength != null && myStrength.corpsAbilityFactor != 1f)
                baseDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Max(0.1f, myStrength.corpsAbilityFactor)));

            // 特殊指揮（#2175・一斉砲撃 等）：与ダメ倍率を乗算（既定1.0）。
            if (myStrength != null && myStrength.activeAttackFactor != 1f)
                baseDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Max(0.1f, myStrength.activeAttackFactor)));

            // 不意打ち（索敵 #2180）：敵に発見されていない攻撃側は先制の利（与ダメ増）。
            if (myStrength != null && myStrength.IsConcealed)
                baseDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * DetectionRules.AmbushDamageFactor));

            // 命中・回避（#2255）：攻撃側精度−防御側回避で命中判定。外れはかすり（揺らぎ）。
            float accuracy = myStrength != null && myStrength.admiralData != null
                ? (myStrength.admiralData.EffectiveIntelligence + myStrength.admiralData.EffectiveMobility) / 2f : 50f;
            float hitFactor = AccuracyRules.HitFactor(AccuracyRules.HitChance(accuracy, ShipCombat.EvasionOf(target)), Random.value);
            if (hitFactor < 1f) baseDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * hitFactor));

            // 武器種適性（#2256）：標的が旗艦か配下艦かで与ダメを補正する（実効値パターン・基準値非破壊）。
            bool targetIsFlagship = target is FleetStrength;
            float aptitude = WeaponTypeRules.TargetAptitude(weaponType, targetIsFlagship);
            if (aptitude != 1f) baseDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * aptitude));

            // ダメージ計算（提督攻撃・士気・側背面・陣形特性#72・陣形相性#2177・ランチェスター集中 を集約ヘルパーで算出）
            bool isFlank;
            Squadron mySquadron = ShipCombat.GetSquadronOf(myStrength);
            Formation myFormation = mySquadron != null ? mySquadron.currentFormation : Formation.紡錘陣;
            float fAtk = FormationTraitRules.AttackFactor(myFormation);
            // 陣形の相性（じゃんけん）：防御側の陣形に対する与ダメ補正を攻撃側陣形倍率に乗せる。
            Squadron targetSquadron = ShipCombat.GetSquadronOf(target);
            if (targetSquadron != null)
                fAtk *= FormationMatchupRules.AttackFactor(myFormation, targetSquadron.currentFormation);

            // ダメージ内訳のサンプリング（#2252・可視化）：プレイヤー勢力の注目ヒットだけ間引いて通知ログへ。
            DamageBreakdown bd = ShouldSampleBreakdown() ? (sampleBreakdown ?? (sampleBreakdown = new DamageBreakdown())) : null;
            int finalDamage = ShipCombat.ComputeDamage(baseDamage,
                myStrength != null ? myStrength.admiralData : null,
                moraleFactor, transform.position, target.Transform, flankMultiplier, out isFlank, fAtk, LanchesterFactor, bd);
            if (bd != null)
            {
                nextBreakdownSampleTime = Time.time + BreakdownSampleInterval;
                if (isFlank || bd.WasClamped) // 側背 or クランプ発生＝学びがあるヒットだけ出す
                    NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報, $"与ダメ内訳：{bd.Describe()}");
            }

            Vector3 targetPos = target.Transform.position; // TakeDamage前に取得
            target.TakeDamage(finalDamage);

            // MVP集計：与ダメージを攻撃元の旗艦に加算
            if (myStrength != null) myStrength.AddDamageDealt(finalDamage);

            // ダメージポップアップ（target が生死問わず position は有効）
            DamagePopup.Show(targetPos, finalDamage, isFlank);

            // ビーム演出
            FireBeam(targetPos);
            AudioManager.Instance.PlayBeam();
        }

        private void FireBeam(Vector3 targetPos)
        {
            if (beamLine == null) return;
            // 通常/ミサイルの色をグラデへ反映（色が変わった時だけ再構築＝GC節約）
            EnsureBeamGradient(shotBeamColor);
            StopAllCoroutines();
            // 終点は命中点。万一射程を超える点を渡されても射程端でクランプし、画面端まで伸びるのを防ぐ。
            Vector3 origin = transform.position;
            StartCoroutine(BeamFx.Play(beamLine, beamMaterial, beamWidth, beamDuration,
                origin, ClampBeamEnd(origin, targetPos)));
        }

        /// <summary>ビーム色（白熱コア→陣営/ミサイル色）のグラデを適用。色が変わった時だけ再構築する。</summary>
        private void EnsureBeamGradient(Color c)
        {
            if (beamLine == null || c == gradientColor) return;
            gradientColor = c;
            BeamFx.ApplyGradient(beamLine, c);
        }

        /// <summary>ビーム終点を射程内にクランプする（射程外まで線が伸びるのを防ぐ）。</summary>
        private Vector3 ClampBeamEnd(Vector3 origin, Vector3 target)
        {
            float maxRange = weaponArc != null ? weaponArc.range : 0f;
            if (maxRange <= 0f) return target;
            Vector3 dir = target - origin;
            if (dir.magnitude <= maxRange) return target;
            return origin + dir.normalized * maxRange;
        }
    }
}

