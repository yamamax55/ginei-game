using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// <b>最高の軍自動編成</b>（純ロジック・test-first）。勢力の総艦艇プールから、
    /// 予備を控除 → 前線を標準艦隊規模で分割 → <b>能力適性</b>で司令を割付 → 大艦隊に<b>指揮班（副司令/参謀）</b>を補強 →
    /// 大将級以上で<b>梯団（軍団）</b>を編成、までを一括で組み上げ <see cref="ArmyPlan"/> を返す。
    /// 兵力分割は <see cref="FleetAutoOrganizeRules.RecommendFleetCount"/>/<see cref="FleetAutoOrganizeRules.AllocateStrength"/>、
    /// 指揮可能規模・梯団段は <see cref="CommandCapacityRules"/> へ委譲（数値を二重実装しない）。決定論・null/空安全。
    /// 階級だけで割る既存の <see cref="FleetAutoOrganizeRules"/>（3テスト維持）の<b>上位互換</b>＝こちらは能力・指揮班・梯団・予備・役割を扱う。
    /// </summary>
    public static class ArmyOrganizationRules
    {
        /// <summary>軍自動編成のパラメータ（予備率・標準艦隊規模・指揮班付与の下限・梯団あたり最大艦隊数・梯団有効）。</summary>
        public readonly struct ArmyOrgParams
        {
            /// <summary>総プールのうち予備に残す比率（0..1）。</summary>
            public readonly float reserveFraction;
            /// <summary>1艦隊あたりの標準兵力（隻・分割の基準）。</summary>
            public readonly int standardFleetSize;
            /// <summary>この兵力以上の艦隊にのみ副司令・参謀を付ける下限（隻）。</summary>
            public readonly int staffMinFleetStrength;
            /// <summary>1梯団に束ねる最大艦隊数。</summary>
            public readonly int corpsMaxFleets;
            /// <summary>梯団編成を行うか。</summary>
            public readonly bool enableCorps;

            public ArmyOrgParams(float reserveFraction, int standardFleetSize, int staffMinFleetStrength,
                int corpsMaxFleets, bool enableCorps)
            {
                this.reserveFraction = reserveFraction;
                this.standardFleetSize = standardFleetSize;
                this.staffMinFleetStrength = staffMinFleetStrength;
                this.corpsMaxFleets = corpsMaxFleets;
                this.enableCorps = enableCorps;
            }

            // 既定値（調整可）。
            public const float DefaultReserveFraction = 0.2f;
            public const int DefaultStandardFleetSize = 12000;     // CommandCapacityRules.Cap中将 ＝一個艦隊の下限。
            public const int DefaultStaffMinFleetStrength = 12000;
            public const int DefaultCorpsMaxFleets = 3;
            public const bool DefaultEnableCorps = true;

            /// <summary>既定パラメータ（予備20%・標準1.2万・指揮班下限1.2万・梯団3艦隊まで・梯団有効）。</summary>
            public static ArmyOrgParams Default => new ArmyOrgParams(
                DefaultReserveFraction, DefaultStandardFleetSize, DefaultStaffMinFleetStrength,
                DefaultCorpsMaxFleets, DefaultEnableCorps);
        }

        /// <summary>予備に残す兵力＝round(総プール×比率) を [0, 総プール] にクランプ。</summary>
        public static int ReserveFor(int totalPool, float reserveFraction)
        {
            if (totalPool <= 0) return 0;
            int reserve = Mathf.RoundToInt(totalPool * reserveFraction);
            return Mathf.Clamp(reserve, 0, totalPool);
        }

        /// <summary>その兵力に自然な梯団段＝必要 tier（<see cref="CommandCapacityRules.RequiredTierForStrength"/>）から導く段。</summary>
        public static EchelonType EchelonForStrength(int strength)
        {
            return CommandCapacityRules.EchelonForTier(CommandCapacityRules.RequiredTierForStrength(strength));
        }

        /// <summary>
        /// 各艦隊に<b>能力適性</b>で司令を割り付ける（兵力降順で処理＝大艦隊から良将を確保）。
        /// 候補は「指揮可能（<see cref="CommandCapacityRules.CanCommand"/>）な未使用の将」から、
        /// ①役割適性（<see cref="CommanderProfile.FitnessFor"/>）が最高 → ②同点なら<b>足りる範囲で最低の階級 tier</b>
        /// （高位の将を小艦隊で浪費せず梯団用に温存）→ ③同点なら id 最小、で1人選ぶ。適任なしは -1 のまま。
        /// </summary>
        public static void AssignCommandersByFitness(List<FleetOrder> fleets, IReadOnlyList<CommanderProfile> commanders)
        {
            if (fleets == null || fleets.Count == 0 || commanders == null || commanders.Count == 0) return;

            var used = new bool[commanders.Count];

            // 艦隊を兵力降順で処理（同兵力は元の index 昇順で決定論）。
            var order = BuildStrengthDescOrder(fleets);

            for (int oi = 0; oi < order.Count; oi++)
            {
                int fi = order[oi];
                var fleet = fleets[fi];
                int best = -1;
                for (int ci = 0; ci < commanders.Count; ci++)
                {
                    if (used[ci]) continue;
                    var c = commanders[ci];
                    if (!CommandCapacityRules.CanCommand(c.rankTier, fleet.strength)) continue;
                    if (best < 0 || IsBetterCommander(c, commanders[best], fleet.role)) best = ci;
                }
                if (best >= 0)
                {
                    fleet.commanderId = commanders[best].id;
                    used[best] = true;
                }
            }
        }

        /// <summary>司令候補 a が b より望ましいか（①役割適性高 ②同点なら低 tier ③同点なら id 小）。</summary>
        private static bool IsBetterCommander(CommanderProfile a, CommanderProfile b, ShipRole role)
        {
            float fa = a.FitnessFor(role), fb = b.FitnessFor(role);
            if (fa != fb) return fa > fb;
            if (a.rankTier != b.rankTier) return a.rankTier < b.rankTier;
            return a.id < b.id;
        }

        /// <summary>
        /// 大艦隊（兵力 ≥ <see cref="ArmyOrgParams.staffMinFleetStrength"/>）に<b>指揮班</b>（副司令・参謀）を補強する。
        /// 司令にすでに使われた将を除く残り候補から、兵力降順の艦隊ごとに：
        /// 副司令＝司令以下の tier で戦闘適性（<see cref="CommanderProfile.CombatFitness"/>）最高、
        /// 参謀＝運営＋情報が最高（幕僚向き）、を1人ずつ取り使用済みに（同一人を司令/副司令/参謀で二重起用しない）。
        /// 司令が空席の艦隊・小艦隊は対象外。
        /// </summary>
        public static void AssignStaff(List<FleetOrder> fleets, IReadOnlyList<CommanderProfile> commanders, ArmyOrgParams prm)
        {
            if (fleets == null || fleets.Count == 0 || commanders == null || commanders.Count == 0) return;

            // 司令として使われている将を使用済みに（司令割付の後に呼ばれる前提・冪等）。
            var used = new bool[commanders.Count];
            for (int ci = 0; ci < commanders.Count; ci++)
            {
                int id = commanders[ci].id;
                for (int fi = 0; fi < fleets.Count; fi++)
                    if (fleets[fi].commanderId == id) { used[ci] = true; break; }
            }

            var order = BuildStrengthDescOrder(fleets);
            for (int oi = 0; oi < order.Count; oi++)
            {
                var fleet = fleets[order[oi]];
                if (fleet.commanderId < 0) continue;
                if (fleet.strength < prm.staffMinFleetStrength) continue;

                int cmdTier = TierOf(commanders, fleet.commanderId);

                // 副司令＝司令以下の tier・戦闘適性最高。
                int vice = -1;
                for (int ci = 0; ci < commanders.Count; ci++)
                {
                    if (used[ci]) continue;
                    if (commanders[ci].rankTier > cmdTier) continue;
                    if (vice < 0 || IsBetterStaff(commanders[ci], commanders[vice], staffRoleVice: true)) vice = ci;
                }
                if (vice >= 0) { fleet.viceId = commanders[vice].id; used[vice] = true; }

                // 参謀＝運営＋情報（幕僚適性）最高。
                int chief = -1;
                for (int ci = 0; ci < commanders.Count; ci++)
                {
                    if (used[ci]) continue;
                    if (chief < 0 || IsBetterStaff(commanders[ci], commanders[chief], staffRoleVice: false)) chief = ci;
                }
                if (chief >= 0) { fleet.chiefId = commanders[chief].id; used[chief] = true; }
            }
        }

        /// <summary>幕僚適性比較（副司令＝戦闘適性／参謀＝運営＋情報。同点は id 小）。</summary>
        private static bool IsBetterStaff(CommanderProfile a, CommanderProfile b, bool staffRoleVice)
        {
            float sa = staffRoleVice ? a.CombatFitness : (a.operation + a.intelligence);
            float sb = staffRoleVice ? b.CombatFitness : (b.operation + b.intelligence);
            if (sa != sb) return sa > sb;
            return a.id < b.id;
        }

        /// <summary>
        /// <b>梯団（軍団）</b>を編成する。<see cref="ArmyOrgParams.enableCorps"/> が偽・艦隊が2未満なら空。
        /// 兵力降順に並べた艦隊を <see cref="ArmyOrgParams.corpsMaxFleets"/> 隻ずつの塊に切り、2艦隊以上の塊ごとに
        /// 未使用の将のうち <see cref="CommandCapacityRules.Tier軍団"/>(8) 以上で最高位（同位は戦闘適性高）を梯団司令に充てる。
        /// 充てられた塊だけが <see cref="CorpsOrder"/> となり、配下 <see cref="FleetOrder.corpsIndex"/> を設定。適任なしの塊は独立のまま。
        /// 司令割付・指揮班に使われた将は除く（残りの上級将で梯団を戴く）。
        /// </summary>
        public static List<CorpsOrder> AssembleCorps(List<FleetOrder> fleets, IReadOnlyList<CommanderProfile> commanders, ArmyOrgParams prm)
        {
            var result = new List<CorpsOrder>();
            if (!prm.enableCorps || fleets == null || fleets.Count < 2) return result;
            int maxPer = Mathf.Max(2, prm.corpsMaxFleets);

            // 既に司令/副司令/参謀で使われている将を除外。
            var used = (commanders != null) ? new bool[commanders.Count] : new bool[0];
            if (commanders != null)
            {
                for (int ci = 0; ci < commanders.Count; ci++)
                {
                    int id = commanders[ci].id;
                    for (int fi = 0; fi < fleets.Count; fi++)
                    {
                        var f = fleets[fi];
                        if (f.commanderId == id || f.viceId == id || f.chiefId == id) { used[ci] = true; break; }
                    }
                }
            }

            var order = BuildStrengthDescOrder(fleets);
            for (int start = 0; start < order.Count; start += maxPer)
            {
                int end = Mathf.Min(start + maxPer, order.Count);
                int chunk = end - start;
                if (chunk < 2) continue; // 単独艦隊は梯団化しない。

                // 塊の総兵力。
                int sum = 0;
                for (int k = start; k < end; k++) sum += fleets[order[k]].strength;

                // 梯団司令＝未使用・tier ≥ 軍団(8) の最高位（同位は戦闘適性高、なお同点は id 小）。
                int corpsCmd = -1;
                if (commanders != null)
                {
                    for (int ci = 0; ci < commanders.Count; ci++)
                    {
                        if (used[ci]) continue;
                        if (commanders[ci].rankTier < CommandCapacityRules.Tier軍団) continue;
                        if (corpsCmd < 0 || IsBetterCorpsCommander(commanders[ci], commanders[corpsCmd])) corpsCmd = ci;
                    }
                }
                if (corpsCmd < 0) continue; // 適任なし＝塊は独立艦隊のまま。

                var co = new CorpsOrder
                {
                    commanderId = commanders[corpsCmd].id,
                    echelon = EchelonForCorps(sum)
                };
                int corpsIndex = result.Count;
                for (int k = start; k < end; k++)
                {
                    int fi = order[k];
                    co.fleetIndices.Add(fi);
                    fleets[fi].corpsIndex = corpsIndex;
                }
                result.Add(co);
                used[corpsCmd] = true;
            }
            return result;
        }

        /// <summary>梯団の段＝総兵力が大将級の指揮限界を超えれば軍、それ未満は軍団（簡素）。</summary>
        private static EchelonType EchelonForCorps(int sumStrength)
        {
            return sumStrength >= CommandCapacityRules.MaxStrengthForTier(CommandCapacityRules.Tier軍団)
                ? EchelonType.軍
                : EchelonType.軍団;
        }

        /// <summary>梯団司令比較（高位 tier 優先・同位は戦闘適性高・なお同点は id 小）。</summary>
        private static bool IsBetterCorpsCommander(CommanderProfile a, CommanderProfile b)
        {
            if (a.rankTier != b.rankTier) return a.rankTier > b.rankTier;
            if (a.CombatFitness != b.CombatFitness) return a.CombatFitness > b.CombatFitness;
            return a.id < b.id;
        }

        /// <summary>
        /// 軍を一括編成する（最上位の窓口）。手順：予備控除 → 前線分割 → 司令割付 → 指揮班 → 梯団。
        /// commanders が null/空でも艦隊は組まれる（司令は空席 -1）。totalPool ≤ 0・前線 ≤ 0 は空プランに予備のみ。
        /// </summary>
        public static ArmyPlan Organize(int totalPool, IReadOnlyList<CommanderProfile> commanders, ArmyOrgParams prm)
        {
            var plan = new ArmyPlan();
            totalPool = Mathf.Max(0, totalPool);
            plan.reserve = ReserveFor(totalPool, prm.reserveFraction);
            int front = totalPool - plan.reserve;
            if (front <= 0) return plan;

            int count = FleetAutoOrganizeRules.RecommendFleetCount(front, prm.standardFleetSize);
            if (count <= 0) return plan;
            int[] sizes = FleetAutoOrganizeRules.AllocateStrength(front, count);

            for (int i = 0; i < count; i++)
            {
                plan.fleets.Add(new FleetOrder
                {
                    strength = sizes[i],
                    role = ShipRole.戦闘艦,
                    echelon = EchelonForStrength(sizes[i])
                });
            }

            if (commanders != null && commanders.Count > 0)
            {
                AssignCommandersByFitness(plan.fleets, commanders);
                AssignStaff(plan.fleets, commanders, prm);
                if (prm.enableCorps)
                {
                    var corps = AssembleCorps(plan.fleets, commanders, prm);
                    plan.corps.AddRange(corps);
                }
            }
            return plan;
        }

        /// <summary>既定パラメータでの一括編成。</summary>
        public static ArmyPlan Organize(int totalPool, IReadOnlyList<CommanderProfile> commanders)
            => Organize(totalPool, commanders, ArmyOrgParams.Default);

        // ===== 内部ヘルパ =====

        /// <summary>艦隊 index を兵力降順（同兵力は index 昇順）に並べた決定論的順序を返す。</summary>
        private static List<int> BuildStrengthDescOrder(List<FleetOrder> fleets)
        {
            var order = new List<int>(fleets.Count);
            for (int i = 0; i < fleets.Count; i++) order.Add(i);
            order.Sort((x, y) =>
            {
                int c = fleets[y].strength.CompareTo(fleets[x].strength);
                return c != 0 ? c : x.CompareTo(y);
            });
            return order;
        }

        /// <summary>その人物 id の階級 tier（見つからなければ 0）。</summary>
        private static int TierOf(IReadOnlyList<CommanderProfile> commanders, int id)
        {
            for (int i = 0; i < commanders.Count; i++)
                if (commanders[i].id == id) return commanders[i].rankTier;
            return 0;
        }
    }
}
