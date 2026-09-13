using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ginei
{
    /// <summary>準備完了時点の1艦隊ぶんの実測値（盤面から読んだ値。プリセットの写しではない）。</summary>
    public readonly struct BattleQaFleetSnapshot
    {
        public readonly int fleetId;
        public readonly string corpsName;
        public readonly bool isCorpsCommander;
        public readonly int shipCount;
        public readonly int maxShipCount;
        public readonly float morale;
        public readonly Vector2 position;
        public readonly float headingDeg;
        public readonly Formation formation;
        public readonly bool aiEnabled;
        public readonly Faction faction;

        public BattleQaFleetSnapshot(int fleetId, string corpsName, bool isCorpsCommander, int shipCount,
            int maxShipCount, float morale, Vector2 position, float headingDeg, Formation formation,
            bool aiEnabled, Faction faction)
        {
            this.fleetId = fleetId;
            this.corpsName = corpsName ?? "";
            this.isCorpsCommander = isCorpsCommander;
            this.shipCount = shipCount;
            this.maxShipCount = maxShipCount;
            this.morale = morale;
            this.position = position;
            this.headingDeg = headingDeg;
            this.formation = formation;
            this.aiEnabled = aiEnabled;
            this.faction = faction;
        }
    }

    /// <summary>初期スナップショット比較の許容誤差。</summary>
    public readonly struct BattleQaTolerance
    {
        public readonly float position;
        public readonly float headingDeg;
        public readonly float morale;

        public BattleQaTolerance(float position, float headingDeg, float morale)
        {
            this.position = Mathf.Max(0f, position);
            this.headingDeg = Mathf.Max(0f, headingDeg);
            this.morale = Mathf.Max(0f, morale);
        }

        /// <summary>既定（浮動小数の丸め程度だけ許す）。</summary>
        public static BattleQaTolerance Default => new BattleQaTolerance(0.001f, 0.01f, 0.001f);
    }

    /// <summary>
    /// 固定会戦QAの<b>初期スナップショット</b>（準備完了の瞬間に盤面から読む）。
    ///
    /// ★「同じプリセットを2回初期化して主要初期値が一致する」を確かめるための物で、
    /// 戦闘の途中経過・結果の一致は扱わない（そちらは許容誤差つきの観測判定で読む）。
    /// </summary>
    public sealed class BattleQaSnapshot
    {
        /// <summary>主要初期値の比較で見る項目数の目安（ログ用）。</summary>
        public const string ComparedFields = "艦隊ID/軍団/軍団長/艦艇数/定数/士気/位置/向き/陣形/AI/陣営/seed";

        public readonly string presetName;
        public readonly int seed;
        private readonly List<BattleQaFleetSnapshot> fleets;

        public BattleQaSnapshot(string presetName, int seed, IList<BattleQaFleetSnapshot> entries)
        {
            this.presetName = presetName ?? "";
            this.seed = seed;
            fleets = new List<BattleQaFleetSnapshot>();
            if (entries != null)
                for (int i = 0; i < entries.Count; i++) fleets.Add(entries[i]);
            fleets.Sort((a, b) => a.fleetId.CompareTo(b.fleetId));
        }

        /// <summary>固定ID昇順。</summary>
        public IReadOnlyList<BattleQaFleetSnapshot> Fleets => fleets;

        /// <summary>角度差（-180..180 に畳んだ絶対値）。</summary>
        public static float AngleDelta(float a, float b)
        {
            float d = (a - b) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return Mathf.Abs(d);
        }

        /// <summary>
        /// 2つのスナップショットの差を列挙する（空＝主要初期値が一致）。
        /// seed の違いも差として必ず出す＝「異なる seed を区別する」。null は差として報告する。
        /// </summary>
        public static List<string> Compare(BattleQaSnapshot a, BattleQaSnapshot b, BattleQaTolerance tol)
        {
            var diffs = new List<string>();
            if (a == null || b == null) { diffs.Add("スナップショットが無い"); return diffs; }
            if (a.presetName != b.presetName) diffs.Add("プリセット " + a.presetName + "≠" + b.presetName);
            if (a.seed != b.seed) diffs.Add("seed " + a.seed + "≠" + b.seed);
            if (a.fleets.Count != b.fleets.Count)
            {
                diffs.Add("艦隊数 " + a.fleets.Count + "≠" + b.fleets.Count);
                return diffs;
            }
            for (int i = 0; i < a.fleets.Count; i++)
            {
                BattleQaFleetSnapshot x = a.fleets[i], y = b.fleets[i];
                string p = "艦隊" + x.fleetId + " ";
                if (x.fleetId != y.fleetId) { diffs.Add("艦隊ID " + x.fleetId + "≠" + y.fleetId); continue; }
                if (x.corpsName != y.corpsName) diffs.Add(p + "軍団 " + x.corpsName + "≠" + y.corpsName);
                if (x.isCorpsCommander != y.isCorpsCommander) diffs.Add(p + "軍団長 " + x.isCorpsCommander + "≠" + y.isCorpsCommander);
                if (x.shipCount != y.shipCount) diffs.Add(p + "艦艇数 " + x.shipCount + "≠" + y.shipCount);
                if (x.maxShipCount != y.maxShipCount) diffs.Add(p + "定数 " + x.maxShipCount + "≠" + y.maxShipCount);
                if (Mathf.Abs(x.morale - y.morale) > tol.morale) diffs.Add(p + "士気 " + x.morale + "≠" + y.morale);
                if ((x.position - y.position).magnitude > tol.position) diffs.Add(p + "位置 " + x.position + "≠" + y.position);
                if (AngleDelta(x.headingDeg, y.headingDeg) > tol.headingDeg) diffs.Add(p + "向き " + x.headingDeg + "≠" + y.headingDeg);
                if (x.formation != y.formation) diffs.Add(p + "陣形 " + x.formation + "≠" + y.formation);
                if (x.aiEnabled != y.aiEnabled) diffs.Add(p + "AI " + x.aiEnabled + "≠" + y.aiEnabled);
                if (x.faction != y.faction) diffs.Add(p + "陣営 " + x.faction + "≠" + y.faction);
            }
            return diffs;
        }

        /// <summary>
        /// 盤面から読んだ初期値がプリセットの明細どおりか（空＝一致）。
        /// 準備処理が明細を取りこぼしていないかの確認（準備失敗の判定に使う）。
        /// </summary>
        public static List<string> CompareWithPreset(BattleQaPreset preset, BattleQaSnapshot snap, BattleQaTolerance tol)
        {
            var diffs = new List<string>();
            if (preset == null || snap == null) { diffs.Add("プリセットかスナップショットが無い"); return diffs; }
            if (preset.seed != snap.seed) diffs.Add("seed " + preset.seed + "≠" + snap.seed);
            if (preset.Fleets.Count != snap.fleets.Count)
            {
                diffs.Add("艦隊数 " + preset.Fleets.Count + "≠" + snap.fleets.Count);
                return diffs;
            }
            for (int i = 0; i < snap.fleets.Count; i++)
            {
                BattleQaFleetSnapshot s = snap.fleets[i];
                if (!preset.TryGetFleet(s.fleetId, out BattleQaFleetSpec f)) { diffs.Add("明細に無い艦隊ID " + s.fleetId); continue; }
                string p = "艦隊" + s.fleetId + " ";
                if (f.corpsName != s.corpsName) diffs.Add(p + "軍団");
                if ((f.role == BattleQaCommandRole.軍団長) != s.isCorpsCommander) diffs.Add(p + "指揮権限");
                if (f.shipCount != s.shipCount) diffs.Add(p + "艦艇数 " + f.shipCount + "≠" + s.shipCount);
                if (f.maxShipCount != s.maxShipCount) diffs.Add(p + "定数 " + f.maxShipCount + "≠" + s.maxShipCount);
                if (Mathf.Abs(f.initialMorale - s.morale) > tol.morale) diffs.Add(p + "士気 " + f.initialMorale + "≠" + s.morale);
                if ((f.position - s.position).magnitude > tol.position) diffs.Add(p + "位置");
                if (AngleDelta(f.headingDeg, s.headingDeg) > tol.headingDeg) diffs.Add(p + "向き " + f.headingDeg + "≠" + s.headingDeg);
                if (f.formation != s.formation) diffs.Add(p + "陣形 " + f.formation + "≠" + s.formation);
                if (f.aiEnabled != s.aiEnabled) diffs.Add(p + "AI設定");
                if (f.faction != s.faction) diffs.Add(p + "陣営");
            }
            return diffs;
        }

        /// <summary>ログ用の1艦隊1行の表。</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("初期スナップショット（").Append(presetName).Append(" seed=").Append(seed).Append("）\n");
            for (int i = 0; i < fleets.Count; i++)
            {
                BattleQaFleetSnapshot f = fleets[i];
                sb.Append("  ID").Append(f.fleetId)
                  .Append(" 陣営=").Append(f.faction)
                  .Append(" 軍団=").Append(f.corpsName.Length > 0 ? f.corpsName : "(なし)")
                  .Append(f.isCorpsCommander ? "［軍団長］" : "")
                  .Append(" 艦艇数=").Append(f.shipCount).Append('/').Append(f.maxShipCount)
                  .Append(" 士気=").Append(f.morale.ToString("0.0"))
                  .Append(" 位置=(").Append(f.position.x.ToString("0.0")).Append(',').Append(f.position.y.ToString("0.0")).Append(')')
                  .Append(" 向き=").Append(f.headingDeg.ToString("0.0"))
                  .Append(" 陣形=").Append(f.formation)
                  .Append(" AI=").Append(f.aiEnabled ? "有効" : "停止")
                  .Append('\n');
            }
            return sb.ToString();
        }
    }
}
