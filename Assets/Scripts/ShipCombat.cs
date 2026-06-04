using UnityEngine;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 個艦戦闘の共通処理（射界判定・最寄り敵検索・ダメージ計算）をまとめた静的ヘルパー。
    /// FleetWeapon（旗艦）と EscortShip（配下艦）の双方が利用し、ダメージ式を一箇所に集約する。
    /// 攻撃対象は IShipTarget（旗艦＝FleetStrength／配下艦＝EscortShip）。
    /// 敵探索は FleetRegistry（陣営別リスト）に問い合わせる。敵リストには自分・同陣営は含まれない。
    /// </summary>
    public static class ShipCombat
    {
        /// <summary>
        /// from 位置・forward 向きを基準に、target が射界（扇形：range/halfAngle）内かを判定。
        /// </summary>
        public static bool IsInArc(Vector3 from, Vector3 forward, Vector3 targetPos, float range, float halfAngle)
        {
            Vector2 to = (Vector2)(targetPos - from);
            float dist = to.magnitude;
            if (dist > range) return false;
            float angle = Vector2.Angle(forward, to);
            return angle <= halfAngle;
        }

        /// <summary>
        /// 射界内の最寄りの敵 IShipTarget を返す（旗艦＋配下艦すべてが対象）。いなければ null。
        /// </summary>
        public static IShipTarget FindNearestEnemyInArc(Vector3 from, Vector3 forward, Faction myFaction, float range, float halfAngle)
        {
            IReadOnlyList<IShipTarget> enemies = FleetRegistry.GetEnemies(myFaction);
            IShipTarget nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                IShipTarget t = enemies[i];
                if (!IsValidTarget(t)) continue;

                Vector3 targetPos = t.Transform.position;
                if (!IsInArc(from, forward, targetPos, range, halfAngle)) continue;

                float dist = Vector2.Distance(from, targetPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = t;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 射界内に敵 IShipTarget がいるかだけを判定（交戦状態の判定用、軽量）。
        /// </summary>
        public static bool AnyEnemyInArc(Vector3 from, Vector3 forward, Faction myFaction, float range, float halfAngle)
        {
            IReadOnlyList<IShipTarget> enemies = FleetRegistry.GetEnemies(myFaction);
            for (int i = 0; i < enemies.Count; i++)
            {
                IShipTarget t = enemies[i];
                if (!IsValidTarget(t)) continue;
                if (IsInArc(from, forward, t.Transform.position, range, halfAngle)) return true;
            }
            return false;
        }

        /// <summary>
        /// 攻撃側・士気・側背面を加味した最終ダメージを計算する（ダメージ式の集約点）。
        /// </summary>
        /// <param name="baseDamage">基本ダメージ</param>
        /// <param name="admiral">攻撃側の提督データ（攻撃補正用、null可）</param>
        /// <param name="moraleFactor">士気補正倍率</param>
        /// <param name="attackerPos">攻撃側の位置（側背面判定用）</param>
        /// <param name="targetTf">被弾側の Transform（向きで側背面判定）</param>
        /// <param name="flankMultiplier">真後ろでの最大倍率</param>
        /// <param name="isFlank">側背面ヒットだったか</param>
        public static int ComputeDamage(int baseDamage, AdmiralData admiral, float moraleFactor, Vector3 attackerPos, Transform targetTf, float flankMultiplier, out bool isFlank)
        {
            // 提督の攻撃力補正（攻撃50で1.0倍, 100で1.5倍, 0で0.5倍）
            float attackBonus = 1.0f;
            if (admiral != null)
            {
                attackBonus = 1.0f + (admiral.attack - 50) / 100f;
            }

            // 側背面ボーナス：被弾側の正面(up)と攻撃者方向の内積で倍率を補間
            Vector2 toAttacker = ((Vector2)(attackerPos - targetTf.position)).normalized;
            float dot = Vector2.Dot(targetTf.up, toAttacker);
            float multiplier = Mathf.Lerp(flankMultiplier, 1.0f, (dot + 1.0f) / 2.0f);
            isFlank = multiplier >= 1.3f;

            return Mathf.RoundToInt(baseDamage * attackBonus * moraleFactor * multiplier);
        }

        /// <summary>
        /// IShipTarget が有効（破棄されておらず生存）かを判定。
        /// 破棄済み MonoBehaviour は Unity の偽 null で検出する。
        /// </summary>
        public static bool IsValidTarget(IShipTarget t)
        {
            if (t == null) return false;
            MonoBehaviour mb = t as MonoBehaviour;
            if (mb == null) return false; // 破棄済み or 非MonoBehaviour
            return t.IsAlive;
        }
    }
}
