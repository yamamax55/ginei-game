using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>固定会戦QAのプリセット種別（再現可能な会戦QA・仕様1）。</summary>
    public enum BattleQaPresetKind
    {
        退却,
        不退転,
        陣形変更,
    }

    /// <summary>
    /// プリセット全体の AI の使い方。
    /// ★「AIを止めた試験」と「通常AIを使う試験」を取り違えないため、艦隊ごとの設定から<b>導出</b>する（手で書かない）。
    /// </summary>
    public enum BattleQaAiMode
    {
        AI停止,
        通常AI,
        混在,
    }

    /// <summary>艦隊の指揮権限（軍団内の立場）。</summary>
    public enum BattleQaCommandRole
    {
        軍団長,
        隷下,
        独立,
        敵,
    }

    /// <summary>
    /// 1艦隊ぶんの初期条件（固定QAの明細）。
    /// 艦隊ID・軍団所属・指揮権限・艦艇数・提督・初期士気・位置・向きを<b>すべて明示</b>する。
    /// UI表記は「兵力」でなく「艦艇数」。
    /// </summary>
    public readonly struct BattleQaFleetSpec
    {
        /// <summary>固定ID（小さい順に生成・記録する＝順序を固定）。</summary>
        public readonly int fleetId;
        public readonly string label;
        public readonly Faction faction;
        /// <summary>軍団名（空＝軍団に属さない）。</summary>
        public readonly string corpsName;
        public readonly BattleQaCommandRole role;
        /// <summary>現在の艦艇数。</summary>
        public readonly int shipCount;
        /// <summary>定数（最大艦艇数）。現在/定数の比が撤退判断に効く。</summary>
        public readonly int maxShipCount;
        public readonly string admiralName;
        /// <summary>軍団長の統率（軍団長のみ使用）。</summary>
        public readonly int leadership;
        /// <summary>軍団長の功名心（軍団長のみ使用）。</summary>
        public readonly int ambition;
        public readonly float initialMorale;
        public readonly Vector2 position;
        /// <summary>向き（度・0＝+Y＝Transform.up、反時計回りが正）。</summary>
        public readonly float headingDeg;
        public readonly Formation formation;
        /// <summary>開始時に FleetAI を有効にするか。</summary>
        public readonly bool aiEnabled;

        public BattleQaFleetSpec(int fleetId, string label, Faction faction, string corpsName,
            BattleQaCommandRole role, int shipCount, int maxShipCount, string admiralName,
            int leadership, int ambition, float initialMorale, Vector2 position, float headingDeg,
            Formation formation, bool aiEnabled)
        {
            this.fleetId = fleetId;
            this.label = label ?? "";
            this.faction = faction;
            this.corpsName = corpsName ?? "";
            this.role = role;
            this.shipCount = Mathf.Max(1, shipCount);
            this.maxShipCount = Mathf.Max(this.shipCount, maxShipCount);
            this.admiralName = admiralName ?? "";
            this.leadership = Mathf.Clamp(leadership, 0, 100);
            this.ambition = Mathf.Clamp(ambition, 0, 100);
            this.initialMorale = Mathf.Max(0f, initialMorale);
            this.position = position;
            this.headingDeg = headingDeg;
            this.formation = formation;
            this.aiEnabled = aiEnabled;
        }

        /// <summary>現在/定数の艦艇数比（0..1）。</summary>
        public float ShipRatio => maxShipCount > 0 ? Mathf.Clamp01((float)shipCount / maxShipCount) : 1f;

        /// <summary>向きの単位ベクトル（Transform.up と同じ規約）。</summary>
        public Vector2 Forward
        {
            get
            {
                float r = headingDeg * Mathf.Deg2Rad;
                return new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
            }
        }
    }

    /// <summary>
    /// 固定会戦QAのプリセット（初期条件＋実行する命令の段取りの名札）。
    ///
    /// ★固定するのは<b>初期条件と命令</b>だけ。フレーム単位の完全な決定論は保証しない
    /// （seed が同じでも戦闘結果が一致するとは扱わない）。結果は許容誤差つきの判定で読む。
    /// </summary>
    public sealed class BattleQaPreset
    {
        public readonly BattleQaPresetKind kind;
        public readonly string name;
        public readonly int seed;
        /// <summary>開始後の Time.timeScale（PauseManager 経由で適用）。</summary>
        public readonly float timeScale;
        /// <summary>BattlefieldCommandManager（軍団長AI）を置くか。</summary>
        public readonly bool useCorpsCommandManager;
        /// <summary>実行する命令の要約（ログ・画面に出す）。</summary>
        public readonly string commandPlan;

        private readonly List<BattleQaFleetSpec> fleets;

        public BattleQaPreset(BattleQaPresetKind kind, string name, int seed, float timeScale,
            bool useCorpsCommandManager, string commandPlan, IList<BattleQaFleetSpec> fleetSpecs)
        {
            this.kind = kind;
            this.name = name ?? kind.ToString();
            this.seed = seed;
            this.timeScale = Mathf.Max(0.1f, timeScale);
            this.useCorpsCommandManager = useCorpsCommandManager;
            this.commandPlan = commandPlan ?? "";
            fleets = new List<BattleQaFleetSpec>();
            if (fleetSpecs != null)
                for (int i = 0; i < fleetSpecs.Count; i++) fleets.Add(fleetSpecs[i]);
            // ★固定ID順に並べる＝生成順・記録順を入力の並びに依存させない。
            fleets.Sort((a, b) => a.fleetId.CompareTo(b.fleetId));
        }

        /// <summary>固定ID昇順の艦隊明細。</summary>
        public IReadOnlyList<BattleQaFleetSpec> Fleets => fleets;

        /// <summary>艦隊ごとの AI 設定から導出した AI の使い方（艦隊0件は AI停止）。</summary>
        public BattleQaAiMode AiMode
        {
            get
            {
                int on = 0;
                for (int i = 0; i < fleets.Count; i++) if (fleets[i].aiEnabled) on++;
                if (on == 0) return BattleQaAiMode.AI停止;
                return on == fleets.Count ? BattleQaAiMode.通常AI : BattleQaAiMode.混在;
            }
        }

        /// <summary>固定IDで明細を探す。</summary>
        public bool TryGetFleet(int fleetId, out BattleQaFleetSpec spec)
        {
            for (int i = 0; i < fleets.Count; i++)
                if (fleets[i].fleetId == fleetId) { spec = fleets[i]; return true; }
            spec = default;
            return false;
        }

        /// <summary>
        /// 明細の矛盾を列挙する（空＝整合）。準備前に呼び、矛盾があれば<b>準備失敗</b>にする。
        /// ID重複・軍団長なしの軍団・軍団名のない軍団長/隷下・敵に軍団長の立場 など。
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (fleets.Count == 0) errors.Add("艦隊が0件");

            var ids = new HashSet<int>();
            var corpsWithCommander = new HashSet<string>();
            var corpsNames = new HashSet<string>();
            for (int i = 0; i < fleets.Count; i++)
            {
                BattleQaFleetSpec f = fleets[i];
                if (!ids.Add(f.fleetId)) errors.Add("艦隊ID重複: " + f.fleetId);
                bool hasCorps = f.corpsName.Length > 0;
                if ((f.role == BattleQaCommandRole.軍団長 || f.role == BattleQaCommandRole.隷下) && !hasCorps)
                    errors.Add("軍団名のない" + f.role + ": " + f.fleetId);
                if ((f.role == BattleQaCommandRole.独立 || f.role == BattleQaCommandRole.敵) && hasCorps)
                    errors.Add(f.role + " なのに軍団所属あり: " + f.fleetId);
                if (hasCorps)
                {
                    corpsNames.Add(f.faction + "/" + f.corpsName);
                    if (f.role == BattleQaCommandRole.軍団長)
                    {
                        if (!corpsWithCommander.Add(f.faction + "/" + f.corpsName))
                            errors.Add("軍団長が複数: " + f.corpsName);
                    }
                }
            }
            foreach (string c in corpsNames)
                if (!corpsWithCommander.Contains(c)) errors.Add("軍団長のいない軍団: " + c);
            return errors;
        }
    }
}
