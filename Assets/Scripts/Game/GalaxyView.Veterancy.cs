using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略艦隊の練度（#練度・<see cref="VeterancyRules"/> の戦略層配線）。
    /// 会戦を重ねた艦隊は経験値が積み上がって精強化し、放置された艦隊は緩やかに練度が鈍る。
    /// 経験値は <see cref="StrategicFleet.id"/> をキーに本パーシャル側で保持し（艦隊本体に状態を足さない）、
    /// 戦闘倍率は基準兵力へ掛けて使う（実効値パターン・基準非破壊）。観測は read-only アクセサで読む。
    /// </summary>
    public partial class GalaxyView
    {
        /// <summary>艦隊ごとの蓄積経験値（<see cref="StrategicFleet.id"/> → XP）。練度の唯一の保管所。</summary>
        private readonly System.Collections.Generic.Dictionary<int, float> fleetXp
            = new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>放置艦隊の練度減衰率（1年ごとに経験値へ掛ける＝使わない艦隊は錆びる）。</summary>
        private const float VeterancyIdleDecay = 0.99f;
        /// <summary>練度経験値の上限（青天井を防ぐ。段階閾値で自然に頭打ちだが念のためキャップ）。</summary>
        private const float VeterancyXpCap = 100f;

        /// <summary>
        /// 練度を1年ぶん進める（#練度）。交戦中（<see cref="StrategicFleet.engaged"/>）の艦隊は
        /// <see cref="VeterancyRules.GainFromBattle"/> で激戦ぶんの経験値を得て精鋭化し、非交戦の艦隊は
        /// 緩やかに減衰（0未満にはしない・<see cref="VeterancyXpCap"/> で上限クランプ）。盤面に居なくなった艦隊の
        /// 残存エントリは年次で掃除する。年次（<see cref="RunAnnualLifecycleTick"/>）から呼ぶ。
        /// </summary>
        private void RunVeterancyTick()
        {
            if (reg == null || reg.fleets == null) return;

            // 現存する艦隊の経験値を更新。
            var present = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null) continue;
                present.Add(f.id);

                fleetXp.TryGetValue(f.id, out float xp);
                if (f.engaged)
                    xp = VeterancyRules.GainFromBattle(xp, 1f); // 激戦＝最大の学び（生き残った者だけが強くなる）
                else
                    xp *= VeterancyIdleDecay;                   // 放置＝緩やかに鈍る
                fleetXp[f.id] = Mathf.Clamp(xp, 0f, VeterancyXpCap);
            }

            // 盤面から消えた艦隊（壊滅・解隊）の残存エントリを掃除（無制限増加の回避・PERF）。
            if (fleetXp.Count > present.Count)
            {
                System.Collections.Generic.List<int> stale = null;
                foreach (var kv in fleetXp)
                    if (!present.Contains(kv.Key)) (stale ??= new System.Collections.Generic.List<int>()).Add(kv.Key);
                if (stale != null)
                    for (int i = 0; i < stale.Count; i++) fleetXp.Remove(stale[i]);
            }
        }

        /// <summary>
        /// 艦隊の練度による戦闘倍率（1..1+最大ボーナス・#練度）。基準兵力へ掛けて使う（基準非破壊）。
        /// 親はこれを <see cref="CombatFactorOf"/> へ折り込む。未知IDは XP0＝等倍。
        /// </summary>
        public float FleetVeterancyFactor(int fleetId)
        {
            fleetXp.TryGetValue(fleetId, out float xp);
            return VeterancyRules.CombatFactor(xp);
        }

        /// <summary>艦隊の練度戦闘倍率（<see cref="StrategicFleet"/> オーバーロード・null は等倍）。</summary>
        public float FleetVeterancyFactor(StrategicFleet f)
            => f == null ? 1f : FleetVeterancyFactor(f.id);

        /// <summary>艦隊の練度段階（新兵/一般/精鋭/古参・#練度）。観測層専用＝read-only。</summary>
        public ExperienceLevel FleetExperience(int fleetId)
        {
            fleetXp.TryGetValue(fleetId, out float xp);
            return VeterancyRules.LevelOf(xp);
        }

        /// <summary>艦隊の蓄積経験値（#練度）。観測層専用＝read-only。</summary>
        public float FleetXp(int fleetId)
        {
            fleetXp.TryGetValue(fleetId, out float xp);
            return xp;
        }

        /// <summary>盤面の戦略艦隊一覧（観測層が練度/補給を read-only で読む）。未生成は null。</summary>
        public System.Collections.Generic.IReadOnlyList<StrategicFleet> StrategicFleets
            => reg != null ? reg.fleets : null;
    }
}
