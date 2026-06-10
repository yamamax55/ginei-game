using UnityEngine;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 個艦戦闘の共通処理（射界判定・最寄り敵検索・ダメージ計算）をまとめた静的ヘルパー。
    /// FleetWeapon（旗艦）と EscortShip（配下艦）の双方が利用し、ダメージ式を一箇所に集約する。
    /// 攻撃対象は IShipTarget（旗艦＝FleetStrength／配下艦＝EscortShip）。
    /// 敵探索は FleetRegistry.AllTargets を走査し、FactionRelations.IsHostile で敵味方を判定する
    /// （多勢力対応＝3勢力以上でもコード変更不要）。
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
        /// 射界内の最寄りの敵 IShipTarget を返す（敵対勢力の旗艦＋配下艦すべてが対象）。いなければ null。
        /// </summary>
        public static IShipTarget FindNearestEnemyInArc(Vector3 from, Vector3 forward, FactionData myData, Faction myLegacy, float range, float halfAngle)
        {
            IReadOnlyList<IShipTarget> all = FleetRegistry.AllTargets;
            IShipTarget nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                IShipTarget t = all[i];
                if (!IsValidTarget(t)) continue;
                if (!FactionRelations.IsHostile(myData, myLegacy, t)) continue;

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
        /// 配下艦用の標的選定。第一優先＝射界内かつ射線の通る敵旗艦（最寄り）、
        /// 旗艦が選べなければ第二優先＝射界内の敵配下艦（最寄り）。
        /// 射線上に（標的以外の）敵配下艦がいる旗艦は「飛び越えて当たらない」ため第一候補から除外する。
        /// </summary>
        public static IShipTarget FindPrioritizedEnemyInArc(Vector3 from, Vector3 forward, FactionData myData, Faction myLegacy, float range, float halfAngle, float blockRadius = 0.4f)
        {
            IReadOnlyList<IShipTarget> all = FleetRegistry.AllTargets;

            IShipTarget nearestFlagship = null; float minFlagDist = float.MaxValue;
            IShipTarget nearestEscort = null; float minEscDist = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                IShipTarget t = all[i];
                if (!IsValidTarget(t)) continue;
                if (!FactionRelations.IsHostile(myData, myLegacy, t)) continue;

                Vector3 pos = t.Transform.position;
                if (!IsInArc(from, forward, pos, range, halfAngle)) continue;

                float dist = Vector2.Distance(from, pos);

                if (t is FleetStrength)
                {
                    // 旗艦：射線が敵配下艦に遮られていない場合のみ第一候補にする
                    if (!HasClearShot(from, pos, t, myData, myLegacy, blockRadius)) continue;
                    if (dist < minFlagDist) { minFlagDist = dist; nearestFlagship = t; }
                }
                else
                {
                    if (dist < minEscDist) { minEscDist = dist; nearestEscort = t; }
                }
            }

            return nearestFlagship != null ? nearestFlagship : nearestEscort;
        }

        /// <summary>
        /// from→targetPos の射線上に（target 以外の）敵配下艦がいないかを判定する。
        /// いれば射線が遮られている（旗艦に当たらない）とみなして false を返す。
        /// </summary>
        private static bool HasClearShot(Vector3 from, Vector3 targetPos, IShipTarget target, FactionData myData, Faction myLegacy, float blockRadius)
        {
            Vector2 a = from;
            Vector2 b = targetPos;
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 0.0001f) return true;

            IReadOnlyList<IShipTarget> all = FleetRegistry.AllTargets;
            for (int i = 0; i < all.Count; i++)
            {
                IShipTarget e = all[i];
                if (e == target) continue;
                if (!(e is EscortShip)) continue;      // 遮蔽物は配下艦のみ（旗艦は遮蔽しない）
                if (!IsValidTarget(e)) continue;
                if (!FactionRelations.IsHostile(myData, myLegacy, e)) continue; // 味方配下艦は遮蔽に数えない

                Vector2 p = e.Transform.position;
                // 線分 a-b への射影位置 t（0=始点, 1=標的）。始点側・標的近傍は遮蔽に数えない。
                float proj = Vector2.Dot(p - a, ab) / abLenSq;
                if (proj <= 0.05f || proj >= 0.95f) continue;

                Vector2 closest = a + ab * proj;
                if (Vector2.Distance(p, closest) < blockRadius) return false;
            }
            return true;
        }

        /// <summary>
        /// 指定した艦隊(Squadron)に属する個艦のうち、射界内・最寄りのものを返す。
        /// 艦隊単位の攻撃指示（旗艦だけでなく艦隊全体を標的にする）で使用。いなければ null。
        /// </summary>
        public static IShipTarget FindNearestEnemyInArcOfFleet(Vector3 from, Vector3 forward, float range, float halfAngle, Squadron fleet)
        {
            if (fleet == null) return null;

            IReadOnlyList<IShipTarget> all = FleetRegistry.AllTargets;
            IShipTarget nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                IShipTarget t = all[i];
                if (!IsValidTarget(t)) continue;
                if (GetSquadronOf(t) != fleet) continue;   // 指定艦隊に属する艦のみ

                Vector3 pos = t.Transform.position;
                if (!IsInArc(from, forward, pos, range, halfAngle)) continue;

                float dist = Vector2.Distance(from, pos);
                if (dist < minDist) { minDist = dist; nearest = t; }
            }
            return nearest;
        }

        /// <summary>個艦(旗艦/配下艦)が属する部隊(Squadron)を返す。判別できなければ null。</summary>
        public static Squadron GetSquadronOf(IShipTarget t)
        {
            MonoBehaviour mb = t as MonoBehaviour;
            if (mb == null) return null;
            if (t is EscortShip es) return es.ParentSquadron;  // 配下艦は所属旗艦の部隊
            return mb.GetComponent<Squadron>();                // 旗艦は自身の Squadron
        }

        /// <summary>
        /// 射界内に敵対する IShipTarget がいるかだけを判定（交戦状態の判定用、軽量）。
        /// </summary>
        public static bool AnyEnemyInArc(Vector3 from, Vector3 forward, FactionData myData, Faction myLegacy, float range, float halfAngle)
        {
            IReadOnlyList<IShipTarget> all = FleetRegistry.AllTargets;
            for (int i = 0; i < all.Count; i++)
            {
                IShipTarget t = all[i];
                if (!IsValidTarget(t)) continue;
                if (!FactionRelations.IsHostile(myData, myLegacy, t)) continue;
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
            // 参謀補完を反映した実効攻撃を使用（基準値は非破壊）
            float attackBonus = 1.0f;
            if (admiral != null)
            {
                attackBonus = 1.0f + (admiral.EffectiveAttack - 50) / 100f;
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
