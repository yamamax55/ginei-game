using UnityEngine;
using System.Collections;

namespace Ginei
{
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

        // 所属する旗艦まわり。陣営・攻撃性能はここから取得する（生成順に依存しないよう実行時に参照）。
        private FleetStrength flagship;
        private FleetWeapon flagshipWeapon;
        private WeaponArc flagshipArc;
        private FleetMorale flagshipMorale;
        private Squadron parentSquadron;
        private bool isDead = false;

        // 攻撃クールダウン
        private float nextFireTime;

        // ビーム演出（旗艦と独立した自前の LineRenderer）。実行時生成、OnDestroyで破棄。
        private LineRenderer beamLine;
        private Material beamMaterial;

        /// <summary>所属する旗艦の部隊（艦隊単位の攻撃指示・標的判定用）。</summary>
        public Squadron ParentSquadron => parentSquadron;

        // IShipTarget 実装。旗艦が退却したら部隊ごと戦闘除外（標的にならない）。
        public Transform Transform => transform;
        public Faction Faction => flagship != null ? flagship.faction : Faction.帝国;
        public FactionData FactionData => flagship != null ? flagship.factionData : null;
        public bool IsAlive => !isDead && shipCount > 0 && (flagship == null || !flagship.IsRetreating);

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
            // 攻撃に必要な旗艦情報が無ければ撃たない
            if (flagshipWeapon == null || flagshipArc == null) return;

            // 旗艦喪失で部隊退却中は索敵・発砲停止し、レジストリからも外して以降は休止
            if (flagship != null && flagship.IsRetreating)
            {
                FleetRegistry.Unregister(this);
                enabled = false;
                return;
            }

            if (Time.time < nextFireTime) return;

            // 敵検索は依然コストがあるため、命中の有無に関わらず fireInterval 間隔に制限する
            nextFireTime = Time.time + flagshipWeapon.fireInterval;

            // 標的優先度：第一＝射線の通る敵旗艦、第二＝敵配下艦（射線上の配下艦は旗艦を遮蔽する）
            IShipTarget target = ShipCombat.FindPrioritizedEnemyInArc(transform.position, transform.up,
                Faction, flagshipArc.range, flagshipArc.halfAngle);

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

            bool isFlank;
            int finalDamage = ShipCombat.ComputeDamage(flagshipWeapon.damage,
                flagship != null ? flagship.admiralData : null,
                moraleFactor, transform.position, target.Transform, flagshipWeapon.flankMultiplier, out isFlank);

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

        // ---- ビーム演出（FleetWeapon と同等。配下艦は自前の LineRenderer を持つ）----

        private void SetupBeam()
        {
            if (flagshipWeapon == null) return;

            beamLine = GetComponent<LineRenderer>();
            if (beamLine == null) beamLine = gameObject.AddComponent<LineRenderer>();
            beamLine.positionCount = 2;
            beamLine.startWidth = flagshipWeapon.beamWidth;
            beamLine.endWidth = flagshipWeapon.beamWidth;
            beamLine.useWorldSpace = true;
            beamLine.numCapVertices = 2;
            beamLine.alignment = LineAlignment.View;
            beamLine.sortingOrder = 20;
            beamLine.enabled = false;

            beamMaterial = new Material(Shader.Find("Sprites/Default"));
            beamMaterial.color = flagshipWeapon.beamColor;
            beamLine.material = beamMaterial;
        }

        private void FireBeam(Vector3 targetPos)
        {
            if (beamLine == null) return;
            StopAllCoroutines();
            StartCoroutine(ShowBeamCoroutine(targetPos));
        }

        private IEnumerator ShowBeamCoroutine(Vector3 targetPos)
        {
            // 発射時に旗艦の beamColor を反映（陣営色の実行時変更に追従）
            beamLine.material.color = flagshipWeapon.beamColor;

            beamLine.enabled = true;
            beamLine.SetPosition(0, transform.position);
            beamLine.SetPosition(1, targetPos);

            yield return new WaitForSeconds(flagshipWeapon.beamDuration);

            beamLine.enabled = false;
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
