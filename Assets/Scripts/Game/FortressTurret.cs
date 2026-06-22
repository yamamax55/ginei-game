using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術要塞（<see cref="FortressUnit"/>）の外周固定砲台（#76）。配下艦(EscortShip)の固定版。
    /// 自分の位置・外向き(transform.up)を基準に射界(range/halfAngle)内の最寄り敵を撃つ。
    /// 個別に破壊可能で、沈黙すると**その方角の応射が止まる＝攻め口（死角）が生まれる**。
    /// 陣営・勝敗カウントは要塞コアに従う（砲台は IShipTarget だが旗艦ではない）。
    /// </summary>
    public class FortressTurret : MonoBehaviour, IShipTarget
    {
        [Header("砲台設定")]
        [Tooltip("この砲台の耐久（艦艇数相当。被弾で減算、0以下で沈黙）")]
        public int shipCount = 400;

        [Tooltip("固さ係数：被弾ダメージをこの値で割って受ける（要塞砲台は固い）")]
        public float durabilityFactor = 4f;

        [Tooltip("被弾判定用コライダーの半径")]
        public float colliderRadius = 0.5f;

        [Header("射撃")]
        [Tooltip("1発の基準ダメージ")]
        public int damage = 28;

        [Tooltip("発砲間隔（秒・game-time）")]
        public float fireInterval = 1.1f;

        [Tooltip("射程（ワールド単位）")]
        public float range = 14f;

        [Tooltip("射界の半角（度）。外向きの扇。砲台ごとに方角を分担＝破壊で死角ができる")]
        public float halfAngle = 55f;

        [Tooltip("側背面攻撃時の最大ダメージ倍率（要塞砲台は控えめ）")]
        public float flankMultiplier = 1.2f;

        [Header("ビーム演出")]
        public float beamWidth = 0.16f;
        public float beamDuration = 0.18f;

        // 所属要塞（陣営・通知の出所）
        private FortressUnit owner;
        private SpriteRenderer body;
        private Color liveColor = Color.white;
        private bool silenced = false;
        private float nextFireTime;

        // ビーム（自前 LineRenderer・OnDestroy で破棄）
        private LineRenderer beamLine;
        private Material beamMaterial;
        private Color gradientColor = new Color(-1f, -1f, -1f, -1f);

        // ── IShipTarget 実装（陣営は要塞コアに従う）──
        public Transform Transform => transform;
        public Faction Faction => owner != null ? owner.Faction : Faction.帝国;
        public FactionData FactionData => owner != null ? owner.FactionData : null;
        public bool IsAlive => !silenced && shipCount > 0;

        /// <summary>砲台が稼働中か（要塞コアのシールド計算用）。</summary>
        public bool IsActive => !silenced && shipCount > 0;

        private void Awake()
        {
            if (GetComponent<Collider2D>() == null)
            {
                CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = colliderRadius;
            }
        }

        /// <summary>
        /// 要塞から初期化される。所属・見た目・ビームを準備し、索敵レジストリへ登録する。
        /// transform.up（外向き）の向きは要塞が配置時に設定する。
        /// </summary>
        public void Setup(FortressUnit fortress, SpriteRenderer renderer, Color factionColor)
        {
            owner = fortress;
            body = renderer;
            liveColor = factionColor;
            SetupBeam(factionColor);

            FleetRegistry.Register(this);
            // 全砲台が同フレームに索敵・発砲しないよう初回タイミングをばらけさせる
            nextFireTime = Time.time + Random.Range(0f, fireInterval);
        }

        private void Update()
        {
            if (silenced) return;
            if (Time.time < nextFireTime) return;
            nextFireTime = Time.time + fireInterval;

            // 地形（星雲/小惑星帯 #2181）による射程低下を自分の位置で反映。
            float effRange = range * BattleTerrain.RangeFactorAt(transform.position);
            IShipTarget target = ShipCombat.FindNearestEnemyInArc(transform.position, transform.up,
                FactionData, Faction, effRange, halfAngle);
            if (target != null) PerformAttack(target);
        }

        private void PerformAttack(IShipTarget target)
        {
            // 提督補正なし（要塞砲台は素の火力）・士気1.0。陣形特性は要塞には無し（円形・側背面の影響は自然に相殺）。
            bool isFlank;
            int finalDamage = ShipCombat.ComputeDamage(damage, null, 1.0f,
                transform.position, target.Transform, flankMultiplier, out isFlank);

            Vector3 targetPos = target.Transform.position;
            target.TakeDamage(finalDamage);
            DamageAccumulator.Add(target.Transform, finalDamage, isFlank, targetPos);
            FireBeam(targetPos);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBeam();
        }

        /// <summary>ダメージ（耐久の減少）を受ける。0以下で沈黙＝その方角が死角になる。</summary>
        public void TakeDamage(int damage)
        {
            if (silenced) return;
            if (damage > 0 && durabilityFactor > 1f)
                damage = Mathf.Max(1, Mathf.RoundToInt(damage / durabilityFactor));
            shipCount -= damage;
            if (shipCount <= 0) Silence();
        }

        /// <summary>砲台を沈黙させる（応射停止＋索敵から除外＋見た目を残骸化）。本体は残してコア露出の手掛かりにする。</summary>
        private void Silence()
        {
            if (silenced) return;
            silenced = true;
            shipCount = 0;
            FleetRegistry.Unregister(this);
            if (body != null) body.color = new Color(0.25f, 0.25f, 0.28f, 0.85f); // 焼け落ちた残骸
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            if (owner != null) owner.OnTurretSilenced();
        }

        // ── ビーム演出（EscortShip と同等）──
        private void SetupBeam(Color color)
        {
            beamLine = GetComponent<LineRenderer>();
            if (beamLine == null) beamLine = gameObject.AddComponent<LineRenderer>();
            BeamFx.ConfigureLine(beamLine, beamWidth);
            beamMaterial = BeamFx.CreateMaterial();
            beamLine.material = beamMaterial;
            EnsureBeamGradient(color);
        }

        private void FireBeam(Vector3 targetPos)
        {
            if (beamLine == null) return;
            StopAllCoroutines();
            Vector3 origin = transform.position;
            StartCoroutine(BeamFx.Play(beamLine, beamMaterial, beamWidth, beamDuration,
                origin, ClampBeamEnd(origin, targetPos)));
        }

        private void EnsureBeamGradient(Color c)
        {
            if (beamLine == null || c == gradientColor) return;
            gradientColor = c;
            BeamFx.ApplyGradient(beamLine, c);
        }

        private Vector3 ClampBeamEnd(Vector3 origin, Vector3 target)
        {
            Vector3 dir = target - origin;
            if (dir.magnitude <= range) return target;
            return origin + dir.normalized * range;
        }

        private void OnDestroy()
        {
            FleetRegistry.Unregister(this);
            if (beamMaterial != null) Destroy(beamMaterial);
        }
    }
}
