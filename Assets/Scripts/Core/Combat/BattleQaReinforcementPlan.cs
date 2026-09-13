using System.Collections.Generic;
using System.Text;

namespace Ginei
{
    /// <summary>
    /// 固定会戦QAの<b>QA所有の時限援軍</b>の明細（SPEED-08・純ロジック）。
    ///
    /// 実際の生成は製品の時限増援（BattleSetup の予約→経過判定→戦場端から出現）を使い、ここは「何を・いつ・どの陣営で」だけを決める。
    /// ★陣営はプリセットの味方（役割が敵でない艦隊）に合わせる。★固定IDはプリセットの艦隊と重ねない。
    /// ★戦略の援軍台帳（StrategySession.Reinforcements）は扱わない。
    /// </summary>
    public sealed class BattleQaReinforcementPlan
    {
        /// <summary>援軍の固定ID（プリセットの艦隊IDと重ならない値）。</summary>
        public const int DefaultFleetId = 90;
        /// <summary>援軍の艦艇数（提督の統率50＝補正なしでこの数になる）。</summary>
        public const int DefaultShipCount = 1000;
        /// <summary>開始から到着までのゲーム秒（どのプリセットの観測より短く）。</summary>
        public const float DefaultArrivalSeconds = 2f;
        /// <summary>出現する戦場端の半径（QAの盤面は狭いので製品既定135より近く）。</summary>
        public const float DefaultEdgeRadius = 20f;
        /// <summary>出現位置の高さ（味方の後方寄り）。</summary>
        public const float DefaultSpawnY = -10f;
        /// <summary>到着時刻の下限（0以下は製品側で「開戦時から在場」＝時限増援にならない）。</summary>
        public const float MinArrivalSeconds = 0.1f;
        /// <summary>「動けた」とみなす出現位置からの最小変位（ワールド単位）。</summary>
        public const float MinMoveDisplacement = 0.1f;
        /// <summary>動けたかを判定するのに要る出現後の観測時間（ゲーム秒）。これ未満は判定しない。</summary>
        public const float MinMoveObserveSeconds = 2f;
        /// <summary>援軍の提督の能力値（中立＝補正なし）。</summary>
        public const int NeutralStat = 50;

        public readonly int fleetId;
        public readonly Faction faction;
        /// <summary>所属軍団（空＝軍団なし）。</summary>
        public readonly string corpsName;
        public readonly int shipCount;
        public readonly float arrivalSeconds;
        public readonly float edgeRadius;
        public readonly float spawnY;
        public readonly Formation formation;

        public BattleQaReinforcementPlan(int fleetId, Faction faction, string corpsName, int shipCount,
            float arrivalSeconds, float edgeRadius, float spawnY, Formation formation)
        {
            this.fleetId = fleetId;
            this.faction = faction;
            this.corpsName = corpsName ?? "";
            this.shipCount = shipCount;
            this.arrivalSeconds = arrivalSeconds;
            this.edgeRadius = edgeRadius;
            this.spawnY = spawnY;
            this.formation = formation;
        }

        /// <summary>プリセットの味方陣営（固定ID順で最初の、役割が敵でない艦隊の陣営）。無ければ false。</summary>
        public static bool TryResolveAlliedFaction(BattleQaPreset preset, out Faction faction)
        {
            faction = Faction.同盟;
            if (preset == null) return false;
            IReadOnlyList<BattleQaFleetSpec> specs = preset.Fleets;
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].role == BattleQaCommandRole.敵) continue;
                faction = specs[i].faction;
                return true;
            }
            return false;
        }

        /// <summary>
        /// プリセットに合わせた既定の明細：陣営＝味方、軍団＝味方で最初に軍団を持つ艦隊の軍団（無ければ軍団なし）。
        /// 味方がいなければ陣営は同盟のまま（<see cref="Validate"/> で拒否される）。
        /// </summary>
        public static BattleQaReinforcementPlan ForPreset(BattleQaPreset preset, float arrivalSeconds)
        {
            TryResolveAlliedFaction(preset, out Faction allied);
            string corps = "";
            if (preset != null)
            {
                IReadOnlyList<BattleQaFleetSpec> specs = preset.Fleets;
                for (int i = 0; i < specs.Count; i++)
                {
                    if (specs[i].role == BattleQaCommandRole.敵 || specs[i].faction != allied) continue;
                    if (string.IsNullOrEmpty(specs[i].corpsName)) continue;
                    corps = specs[i].corpsName;
                    break;
                }
            }
            return new BattleQaReinforcementPlan(DefaultFleetId, allied, corps, DefaultShipCount,
                arrivalSeconds, DefaultEdgeRadius, DefaultSpawnY, Formation.紡錘陣);
        }

        /// <summary>プリセットとの矛盾を列挙する（空＝整合）。</summary>
        public List<string> Validate(BattleQaPreset preset)
        {
            var errors = new List<string>();
            if (preset == null) { errors.Add("プリセットが無い"); return errors; }
            if (fleetId <= 0) errors.Add("援軍の固定IDが正でない（" + fleetId + "）");
            else if (preset.TryGetFleet(fleetId, out _)) errors.Add("援軍の固定ID " + fleetId + " がプリセットの艦隊と重なる");
            if (shipCount <= 0) errors.Add("援軍の艦艇数が正でない（" + shipCount + "）");
            if (float.IsNaN(arrivalSeconds) || float.IsInfinity(arrivalSeconds) || arrivalSeconds < MinArrivalSeconds)
                errors.Add("援軍の到着時刻が不正（" + arrivalSeconds + "・下限 " + MinArrivalSeconds + " ゲーム秒）");
            if (float.IsNaN(edgeRadius) || float.IsInfinity(edgeRadius) || edgeRadius <= 0f)
                errors.Add("援軍の出現半径が不正（" + edgeRadius + "）");
            if (float.IsNaN(spawnY) || float.IsInfinity(spawnY)) errors.Add("援軍の出現高さが有限でない");

            if (!TryResolveAlliedFaction(preset, out Faction allied)) errors.Add("プリセットに味方の艦隊が無い（援軍の陣営を合わせられない）");
            else if (allied != faction) errors.Add("援軍の陣営 " + faction + " がプリセットの味方 " + allied + " と違う");

            if (corpsName.Length > 0)
            {
                bool found = false;
                IReadOnlyList<BattleQaFleetSpec> specs = preset.Fleets;
                for (int i = 0; i < specs.Count; i++)
                    if (specs[i].role != BattleQaCommandRole.敵 && specs[i].faction == faction && specs[i].corpsName == corpsName) { found = true; break; }
                if (!found) errors.Add("援軍の軍団「" + corpsName + "」がプリセットの味方に無い");
            }
            return errors;
        }

        /// <summary>明細の要約（ログ用）。seed はプリセットのもの。</summary>
        public string Describe(int seed)
        {
            return new StringBuilder("援軍の明細：固定ID=").Append(fleetId)
                .Append(" 陣営=").Append(faction)
                .Append(" 軍団=").Append(corpsName.Length > 0 ? corpsName : "(なし)")
                .Append(" 艦艇数=").Append(shipCount)
                .Append(" 到着=開始から ").Append(arrivalSeconds.ToString("0.##")).Append(" ゲーム秒")
                .Append(" 出現半径=").Append(edgeRadius.ToString("0.##"))
                .Append(" 出現高さ=").Append(spawnY.ToString("0.##"))
                .Append(" 陣形=").Append(formation)
                .Append(" seed=").Append(seed)
                .ToString();
        }
    }
}
