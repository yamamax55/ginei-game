using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// プレイヤー艦隊の標準命令（#85）の継続的な振る舞いを担うコンポーネント。
    /// ・アタックムーブ：目標地点へ進みつつ、捕捉範囲内の敵を見つけたら交戦（撃破後に再進撃）。
    /// ・その場保持：移動せず、射界内の敵には FleetWeapon が自動発砲し続ける。
    /// FleetCommander が命令時に AddComponent して stance を設定する（敵/AI艦には付けない）。
    /// 既存の追尾（FleetWeapon.SetManualTargetFleet）と移動（FleetMovement）を流用し、
    /// 新たな索敵・戦闘ロジックは作らない。
    /// </summary>
    [RequireComponent(typeof(FleetMovement))]
    [RequireComponent(typeof(FleetWeapon))]
    public class FleetStandardOrder : MonoBehaviour
    {
        public enum Stance { なし, アタックムーブ, 保持 }

        [Tooltip("現在の標準命令")]
        public Stance stance = Stance.なし;

        [Tooltip("アタックムーブで敵を捕捉して交戦する半径（この距離内の最寄り敵を狙う）")]
        public float acquireRange = 8f;

        private Vector2 attackMoveTarget;

        private FleetMovement movement;
        private FleetWeapon weapon;
        private FleetStrength strength;
        private FleetAI ai;

        private void Awake()
        {
            movement = GetComponent<FleetMovement>();
            weapon = GetComponent<FleetWeapon>();
            strength = GetComponent<FleetStrength>();
            ai = GetComponent<FleetAI>();
        }

        /// <summary>アタックムーブ命令（目標地点へ進撃しつつ、捕捉した敵と交戦）。</summary>
        public void SetAttackMove(Vector2 target)
        {
            stance = Stance.アタックムーブ;
            attackMoveTarget = target;
            if (weapon != null) weapon.ClearManualTarget();
        }

        /// <summary>その場保持命令（移動せず防御）。</summary>
        public void SetHold()
        {
            stance = Stance.保持;
            if (weapon != null) weapon.ClearManualTarget();
            if (movement != null) movement.Stop();
        }

        /// <summary>標準命令を解除（通常の手動移動に戻す）。</summary>
        public void ClearOrder()
        {
            stance = Stance.なし;
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return;
            if (ai != null && ai.enabled) return;                 // AI/敵艦には適用しない（プレイヤー艦のみ）
            if (strength != null && !strength.IsAlive) return;    // 退却中は無効

            switch (stance)
            {
                case Stance.アタックムーブ: TickAttackMove(); break;
                case Stance.保持: TickHold(); break;
            }
        }

        private void TickAttackMove()
        {
            if (weapon == null || movement == null) return;

            // 交戦中（手動目標あり）は FleetWeapon の追尾に任せる（敵の撃破/退却で自動解除される）
            if (weapon.HasManualTarget) return;

            // 捕捉：acquireRange 内の最寄り敵を交戦目標にする（全周＝halfAngle 180°で索敵）
            FactionData md = strength != null ? strength.factionData : null;
            Faction ml = strength != null ? strength.faction : default;
            IShipTarget enemy = ShipCombat.FindNearestEnemyInArc(
                transform.position, transform.up, md, ml, acquireRange, 180f);
            if (enemy != null)
            {
                Squadron sq = ShipCombat.GetSquadronOf(enemy);
                if (sq != null)
                {
                    weapon.SetManualTargetFleet(sq);   // 既存の追尾＋交戦に委ねる
                    return;
                }
            }

            // 周囲に敵が居なければ目標地点へ前進
            movement.SetDestination(attackMoveTarget);
        }

        private void TickHold()
        {
            // 追尾はさせず、その場に留まる。射界内の敵には FleetWeapon が自動発砲する。
            if (weapon != null && weapon.HasManualTarget) weapon.ClearManualTarget();
            if (movement != null) movement.Stop();
        }
    }
}
