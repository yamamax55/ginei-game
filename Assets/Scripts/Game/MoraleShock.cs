using UnityEngine;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 士気の連鎖崩壊／高揚（#2176）の会戦配線（static ヘルパー）。撃墜・敗走・捨てがまり成功の発生点から、
    /// 近傍の旗艦（部隊単位）の士気を `MoraleShockRules`（Core）に従って増減する。
    /// 旗艦撃墜・敗走＝近傍の味方がパニック（撃墜は敵が高揚）、捨てがまり成功＝近傍の味方が奮い立つ。
    /// 敵味方は `FactionRelations`、列挙は `FleetRegistry.AllFlagships`（個艦へ降りない＝終盤ラグ回避）。
    /// </summary>
    public static class MoraleShock
    {
        /// <summary>士気波及の半径（この距離まで効く）。</summary>
        public const float Radius = 16f;
        /// <summary>撃墜時に敵が高揚する割合（味方パニックに対する比）。</summary>
        public const float EnemyElationRatio = 0.5f;

        /// <summary>
        /// center で起きた ev を近傍へ波及させる。sourceFaction＝事象を起こした/被った側の勢力。
        /// </summary>
        public static void Propagate(Vector2 center, FactionData sourceData, Faction sourceFaction, MoraleEvent ev)
        {
            IReadOnlyList<FleetStrength> flags = FleetRegistry.AllFlagships;
            if (flags == null) return;

            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength f = flags[i];
                if (f == null || !f.IsAlive) continue;

                float dist = Vector2.Distance(center, f.transform.position);
                float amount = MoraleShockRules.ShockAt(ev, dist, Radius);
                if (amount <= 0f) continue;

                bool hostile = FactionRelations.IsHostile(sourceData, sourceFaction, f.FactionData, f.Faction);
                float delta = SignedAmount(ev, hostile, amount);
                if (delta == 0f) continue;

                FleetMorale mo = f.GetComponent<FleetMorale>();
                // 原因の札は観測台帳（MoraleAuditLog）用＝増減の計算には関与しない。
                // 撃墜の高揚と会戦イベントはどちらも士気を上げるので、後から言い分けられるようにする。
                if (mo != null) mo.ApplyMoraleDelta(delta, AuditSourceOf(ev, delta), ev.ToString());
            }
        }

        /// <summary>観測台帳へ残す原因の種別（高揚か衝撃かは符号で決まる）。</summary>
        private static MoraleChangeSource AuditSourceOf(MoraleEvent ev, float delta)
        {
            if (ev == MoraleEvent.捨てがまり成功) return MoraleChangeSource.捨てがまり高揚;
            if (ev == MoraleEvent.旗艦撃墜) return delta > 0f
                ? MoraleChangeSource.撃墜高揚      // 敵が落ちた＝高揚
                : MoraleChangeSource.敗走衝撃;     // 味方が落ちた＝パニック
            return MoraleChangeSource.敗走衝撃;
        }

        /// <summary>事象と敵味方から符号付き士気増減を決める。</summary>
        private static float SignedAmount(MoraleEvent ev, bool hostile, float amount)
        {
            switch (ev)
            {
                case MoraleEvent.旗艦撃墜:
                    // 味方はパニック（負）、敵は高揚（正・小さめ）。
                    return hostile ? amount * EnemyElationRatio : -amount;
                case MoraleEvent.敗走:
                    // 味方のみ衝撃（負）。
                    return hostile ? 0f : -amount;
                case MoraleEvent.捨てがまり成功:
                    // 味方のみ高揚（正）。殿の奮戦が味方を鼓舞する。
                    return hostile ? 0f : amount;
                default:
                    return 0f;
            }
        }
    }
}
