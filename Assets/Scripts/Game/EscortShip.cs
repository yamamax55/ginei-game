using UnityEngine;
using System.Collections;

namespace Ginei
{
    // ShipClass enum は Core の ShipClass.cs へ切り出し（#496・ShipyardRules 等の Core 参照のため）

    /// <summary>
    /// 配下艦（旗艦の周囲に従う個艦）の戦闘単位。
    /// 自前の艦艇数(shipCount)を持ち、被弾で減算、0以下で消滅する。
    /// 陣営・攻撃性能は所属する旗艦(FleetStrength/FleetWeapon)に従う。
    /// 攻撃：旗艦の WeaponArc(range/halfAngle) を流用し、自分の位置・向きを基準に
    /// 射界内の最寄り敵 IShipTarget を撃つ（各艦が自分の1発を撃つ＝二重計算なし）。
    /// </summary>
    public class EscortShip : MonoBehaviour, IShipTarget
    {
        [Header("艦艇設定")]
        [Tooltip("この配下艦が持つ艦艇数（旗艦より少なめ。通常は Squadron.escortShipCount が設定）")]
        public int shipCount = 200;

        [Tooltip("被弾判定用コライダーの半径")]
        public float colliderRadius = 0.3f;

        [Header("艦種（#80・Squadron が編成時に設定）")]
        [Tooltip("この配下艦の艦種（戦艦/巡航艦/駆逐艦）")]
        public ShipClass shipClass = ShipClass.巡航艦;

        [Tooltip("火力倍率（艦種で変わる。旗艦の基準ダメージを実効値として乗算＝基準は非破壊）")]
        public float firepowerMultiplier = 1f;

        [Tooltip("速度倍率（艦種で変わる。Squadron がスロット追従の上限速度に乗算。FleetMovement は変更しない）")]
        public float speedMultiplier = 1f;

        [Tooltip("陣営色に乗せる艦種の色味（識別用・微差）。FactionColor が陣営色×この色で塗る＝再着色でも維持")]
        public Color classTint = Color.white;

        /// <summary>スロット追従の速度倍率（Squadron が移動計算で参照）。</summary>
        public float SpeedMultiplier => speedMultiplier;

        // 所属する旗艦まわり。陣営・攻撃性能はここから取得する（生成順に依存しないよう実行時に参照）。
        private FleetStrength flagship;
        private FleetWeapon flagshipWeapon;
        private WeaponArc flagshipArc;
        private FleetMorale flagshipMorale;
        private Squadron parentSquadron;
        private bool isDead = false;

        // 散り散りに逃散中か（旗艦撃墜で主君を見捨てて逃げる）。逃散中は戦闘除外＋一定方向へ退避し時間で消滅。
        private bool scattering = false;
        private Vector2 scatterDir;
        private const float ScatterSpeed = 4f;
        private const float ScatterLifetime = 4f;

        // 攻撃クールダウン
        private float nextFireTime;

        // ビーム演出（旗艦と独立した自前の LineRenderer）。実行時生成、OnDestroyで破棄。
        private LineRenderer beamLine;
        private Material beamMaterial;
        private Color gradientColor = new Color(-1f, -1f, -1f, -1f); // 適用済みビーム色（変化時のみグラデ再構築）

        /// <summary>所属する旗艦の部隊（艦隊単位の攻撃指示・標的判定用）。</summary>
        public Squadron ParentSquadron => parentSquadron;

        // IShipTarget 実装。旗艦が退却したら部隊ごと戦闘除外（標的にならない）。
        // ただし捨てがまり中は配下艦が殿として戦い続けるので、旗艦退却中でも生存（標的）扱いにする。
        public Transform Transform => transform;
        public Faction Faction => flagship != null ? flagship.faction : Faction.帝国;
        public FactionData FactionData => flagship != null ? flagship.factionData : null;
        public bool IsAlive => !isDead && !scattering && shipCount > 0
            && (flagship == null || !flagship.IsRetreating || (parentSquadron != null && parentSquadron.SutegamariActive));

        /// <summary>旗艦の状態に依らず、この配下艦自体が生存しているか（敗走解決の頭数判定用）。</summary>
        public bool IsLiving => !isDead && !scattering && shipCount > 0;

        private void Awake()
        {
            // 被弾判定用のコライダーを用意（無ければ追加）。
            if (GetComponent<Collider2D>() == null)
            {
                CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = colliderRadius;
            }
        }

        /// <summary>
        /// 旗艦(Squadron)から初期化される。所属旗艦・攻撃性能を紐付け、ビーム演出を準備する。
        /// </summary>
        public void Setup(Squadron squadron, FleetStrength flagshipStrength)
        {
            parentSquadron = squadron;
            flagship = flagshipStrength;
            if (flagshipStrength != null)
            {
                flagshipWeapon = flagshipStrength.GetComponent<FleetWeapon>();
                flagshipArc = flagshipStrength.GetComponent<WeaponArc>();
                flagshipMorale = flagshipStrength.GetComponent<FleetMorale>();
            }
            SetupBeam();

            // 索敵レジストリに登録（陣営は旗艦に従う＝この時点で確定済み）
            FleetRegistry.Register(this);

            // 全配下艦が同フレームに索敵・発砲しないよう、初回タイミングをばらけさせる
            float interval = flagshipWeapon != null ? flagshipWeapon.fireInterval : 1.0f;
            nextFireTime = Time.time + Random.Range(0f, interval);
        }

        private void Update()
        {
            // 逃散中（旗艦撃墜で散り散り）：一定方向へ退避し続け、時間経過で消滅する（発砲しない）。
            if (scattering)
            {
                transform.position += (Vector3)(scatterDir * ScatterSpeed * Time.deltaTime);
                return;
            }

            // 攻撃に必要な旗艦情報が無ければ撃たない
            if (flagshipWeapon == null || flagshipArc == null) return;

            // 非戦闘艦（#128）の配下艦も攻撃しない（旗艦の役割に従う）
            if (flagship != null && !flagship.IsCombatant) return;

            // 静観（#817）の配下艦も発砲しない（旗艦の旗幟に従う）
            if (flagship != null && !flagship.IsFighting) return;

            // 旗艦喪失で部隊退却中は索敵・発砲停止し、レジストリからも外して以降は休止。
            // ただし捨てがまり（殿）中は配下艦が踏みとどまって戦い続ける＝ここでは停止しない。
            bool sutegamari = parentSquadron != null && parentSquadron.SutegamariActive;
            if (flagship != null && flagship.IsRetreating && !sutegamari)
            {
                FleetRegistry.Unregister(this);
                enabled = false;
                return;
            }

            if (Time.time < nextFireTime) return;

            // 敵検索は依然コストがあるため、命中の有無に関わらず fireInterval 間隔に制限する
            nextFireTime = Time.time + flagshipWeapon.fireInterval;

            // 標的優先度：第一＝射線の通る敵旗艦、第二＝敵配下艦（射線上の配下艦は旗艦を遮蔽する）
            // 地形（星雲/小惑星帯 #2181）による射程低下を自分の位置で反映。
            float effRange = flagshipArc.range * BattleTerrain.RangeFactorAt(transform.position);
            IShipTarget target = ShipCombat.FindPrioritizedEnemyInArc(transform.position, transform.up,
                FactionData, Faction, effRange, flagshipArc.halfAngle);

            if (target != null)
            {
                PerformAttack(target);
            }
        }

        /// <summary>
        /// 自分の1発を撃つ。ダメージ式・士気・側背面は旗艦のものを流用する。
        /// </summary>
        private void PerformAttack(IShipTarget target)
        {
            float moraleFactor = flagshipMorale != null ? flagshipMorale.GetMoraleFactor() : 1.0f;

            // 艦種の火力倍率を実効値として基準ダメージに乗算（旗艦の基準値は非破壊）。
            float corpsAbility = flagship != null ? flagship.corpsAbilityFactor : 1f; // 軍団長の能力バフ/デバフ（CSG）
            float activeAtk = flagship != null ? flagship.activeAttackFactor : 1f;     // 特殊指揮（#2175）
            float ambush = (flagship != null && flagship.IsConcealed) ? DetectionRules.AmbushDamageFactor : 1f; // 不意打ち（#2180）
            // 命中・回避（#2255）：攻撃側精度は旗艦提督、回避は標的。外れはかすり。
            float acc = (flagship != null && flagship.admiralData != null)
                ? (flagship.admiralData.EffectiveIntelligence + flagship.admiralData.EffectiveMobility) / 2f : 50f;
            float hit = AccuracyRules.HitFactor(AccuracyRules.HitChance(acc, ShipCombat.EvasionOf(target)), Random.value);
            int baseDamage = Mathf.Max(1, Mathf.RoundToInt(flagshipWeapon.damage * firepowerMultiplier
                * Mathf.Max(0.1f, corpsAbility) * Mathf.Max(0.1f, activeAtk) * ambush * Mathf.Max(0.1f, hit)));

            bool isFlank;
            Formation myFormation = parentSquadron != null ? parentSquadron.currentFormation : Formation.紡錘陣;
            float fAtk = FormationTraitRules.AttackFactor(myFormation);
            // 陣形の相性（じゃんけん #2177）：防御側陣形に対する与ダメ補正。
            Squadron targetSquadron = ShipCombat.GetSquadronOf(target);
            if (targetSquadron != null)
                fAtk *= FormationMatchupRules.AttackFactor(myFormation, targetSquadron.currentFormation);
            // ランチェスター集中倍率は旗艦が部隊単位で算出した値を流用（配下艦ごとに再計算しない＝終盤ラグ回避）。
            float lanchester = flagshipWeapon != null ? flagshipWeapon.LanchesterFactor : 1f;
            int finalDamage = ShipCombat.ComputeDamage(baseDamage,
                flagship != null ? flagship.admiralData : null,
                moraleFactor, transform.position, target.Transform, flagshipWeapon.flankMultiplier, out isFlank, fAtk, lanchester);

            Vector3 targetPos = target.Transform.position; // TakeDamage前に取得
            target.TakeDamage(finalDamage);

            // MVP集計：配下艦の与ダメージも所属旗艦の戦果に加算
            if (flagship != null) flagship.AddDamageDealt(finalDamage);

            DamagePopup.Show(targetPos, finalDamage, isFlank);
            FireBeam(targetPos);
            AudioManager.Instance.PlayBeam();
        }

        /// <summary>
        /// ダメージ（艦艇数の減少）を受ける。0以下で消滅。
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (isDead) return;

            // 挟撃／包囲（#2178）：所属部隊が囲まれているほど配下艦も被ダメ増。
            if (flagship != null && flagship.EnvelopmentFactor > 0f)
                damage = Mathf.RoundToInt(damage * EnvelopmentRules.DamageFactor(flagship.EnvelopmentFactor));

            shipCount -= damage;
            if (shipCount <= 0)
            {
                isDead = true;
                Die();
            }
        }

        private void Die()
        {
            // 索敵レジストリから除外（破棄前に即解除して標的に残らないように）
            FleetRegistry.Unregister(this);
            // 旗艦の配下艦リストからも自分を外す（陣形計算の対象から除外）
            if (parentSquadron != null) parentSquadron.RemoveMember(transform);
            Destroy(gameObject);
        }

        /// <summary>
        /// 散り散りに逃散する（旗艦撃墜時・主君を見捨てて逃げる＝捨てがまりが立たなかった）。
        /// 旗艦の破棄に道連れにされないよう切り離し、戦闘から外れて一定方向へ退避し、時間経過で消滅する。
        /// </summary>
        public void Scatter()
        {
            if (isDead || scattering) return;
            scattering = true;
            FleetRegistry.Unregister(this);        // 標的・索敵から外す（逃げ散る）
            transform.SetParent(null, true);        // 旗艦本体の破棄から切り離す
            float ang = Random.Range(0f, Mathf.PI * 2f);
            scatterDir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)); // 散り散り＝てんでに逃げる
            enabled = true;                         // 退避移動のため Update を回す
            Destroy(gameObject, ScatterLifetime);   // しばらく逃げてから消える
        }

        // ---- ビーム演出（FleetWeapon と同等。配下艦は自前の LineRenderer を持つ）----

        private void SetupBeam()
        {
            if (flagshipWeapon == null) return;

            beamLine = GetComponent<LineRenderer>();
            if (beamLine == null) beamLine = gameObject.AddComponent<LineRenderer>();
            // 見た目は BeamFx に集約（旗艦と同じ質感）。描画ロジックを FleetWeapon と重複させない。
            BeamFx.ConfigureLine(beamLine, flagshipWeapon.beamWidth);
            beamMaterial = BeamFx.CreateMaterial();
            beamLine.material = beamMaterial;
            EnsureBeamGradient(flagshipWeapon.beamColor);
        }

        private void FireBeam(Vector3 targetPos)
        {
            if (beamLine == null) return;
            // 陣営色の実行時変更に追従（色が変わった時だけグラデ再構築＝GC節約）
            EnsureBeamGradient(flagshipWeapon.beamColor);
            StopAllCoroutines();
            // 終点は命中点。万一射程を超える点を渡されても射程端でクランプし、画面端まで伸びるのを防ぐ。
            Vector3 origin = transform.position;
            StartCoroutine(BeamFx.Play(beamLine, beamMaterial, flagshipWeapon.beamWidth, flagshipWeapon.beamDuration,
                origin, ClampBeamEnd(origin, targetPos)));
        }

        /// <summary>ビーム色のグラデを適用。色が変わった時だけ再構築する（GC節約）。</summary>
        private void EnsureBeamGradient(Color c)
        {
            if (beamLine == null || c == gradientColor) return;
            gradientColor = c;
            BeamFx.ApplyGradient(beamLine, c);
        }

        /// <summary>ビーム終点を射程内にクランプする（射程外まで線が伸びるのを防ぐ）。</summary>
        private Vector3 ClampBeamEnd(Vector3 origin, Vector3 target)
        {
            float maxRange = flagshipArc != null ? flagshipArc.range : 0f;
            if (maxRange <= 0f) return target;
            Vector3 dir = target - origin;
            if (dir.magnitude <= maxRange) return target;
            return origin + dir.normalized * maxRange;
        }

        private void OnDestroy()
        {
            // レジストリ取りこぼし防止（Die 経由でなくても確実に解除）
            FleetRegistry.Unregister(this);
            // 実行時生成したマテリアルを破棄（リーク防止）
            if (beamMaterial != null) Destroy(beamMaterial);
        }
    }
}
