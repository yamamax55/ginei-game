using System.Collections.Generic;
using System.Text;

namespace Ginei
{
    /// <summary>固定会戦QAで開始前に個別に ON/OFF できる機能（SPEED-08）。</summary>
    public enum BattleQaFeature
    {
        /// <summary>軍団長AIの会戦フロー②＝後衛への回り込み（包囲）の自動発意（BattlefieldCommandManager.autoEnvelopment）。
        /// ★FleetAI.joinEncirclement（旗艦の取り囲み参加）は現状どこからも読まれない＝切替先にしない。</summary>
        自動包囲,
        /// <summary>援軍＝BattleSetup の時限増援（QA所有の BattleSetup に1件だけ予約）。戦略の援軍台帳 StrategySession.Reinforcements は使わない。</summary>
        援軍,
        /// <summary>会戦の戦況イベント（BattleEventManager の自然抽選）。</summary>
        戦況イベント,
    }

    /// <summary>
    /// 固定会戦QAの<b>検証用機能スイッチ</b>（SPEED-08・純ロジック）。原因の切り分けのため、開始前にだけ適用する。
    ///
    /// ★既定は「先行の固定QAと同じ状態」＝自動包囲ON（QAの軍団長AIが通常どおり判断）・援軍OFF（予約しない）・
    /// 戦況イベントOFF（隔離）。全部ONではない。既定から外した設定は固定合格との比較対象外として分類する。
    /// ★ゲーム既定・アセット・セーブは変えない（適用先はQAが作った使い捨てのコンポーネントだけ）。
    /// </summary>
    public sealed class BattleQaFeatureSwitches
    {
        /// <summary>設定の版（項目や解釈を変えたら上げる）。2＝援軍を BattleSetup の時限増援へ接続。</summary>
        public const int CurrentVersion = 2;

        /// <summary>援軍の接続先の説明（ログ・画面用）。</summary>
        public const string ReinforcementConnectionNote =
            "BattleSetup の時限増援（QA所有の BattleSetup に1件予約→Update の経過判定→SpawnReinforcement→SpawnFleet の実生成）。" +
            "テンプレート・提督・予約はQA所有の一時オブジェクト。戦略の援軍台帳 StrategySession.Reinforcements は触らない";

        public readonly string name;
        public readonly int version;
        private readonly bool[] values;

        private static readonly int FeatureCount = System.Enum.GetValues(typeof(BattleQaFeature)).Length;

        public BattleQaFeatureSwitches(string name, int version, bool envelopment, bool reinforcement, bool battleEvents)
        {
            this.name = string.IsNullOrEmpty(name) ? "無名" : name;
            this.version = version;
            values = new bool[FeatureCount];
            values[(int)BattleQaFeature.自動包囲] = envelopment;
            values[(int)BattleQaFeature.援軍] = reinforcement;
            values[(int)BattleQaFeature.戦況イベント] = battleEvents;
        }

        /// <summary>先行の固定QAと同じ状態（自動包囲ON・援軍OFF・戦況イベントOFF）。</summary>
        public static BattleQaFeatureSwitches FixedDefault => new BattleQaFeatureSwitches("既定", CurrentVersion, true, false, false);

        /// <summary>固定QAの既定値（項目ごと）。</summary>
        public static bool FixedDefaultOf(BattleQaFeature feature) => feature == BattleQaFeature.自動包囲;

        public bool Get(BattleQaFeature feature)
        {
            int i = (int)feature;
            return i >= 0 && i < FeatureCount && values[i];
        }

        /// <summary>この項目だけ変えたコピー（他項目は不変）。名前が空なら <see cref="AutoName"/>。</summary>
        public BattleQaFeatureSwitches With(string newName, BattleQaFeature feature, bool on)
        {
            bool env = feature == BattleQaFeature.自動包囲 ? on : Get(BattleQaFeature.自動包囲);
            bool rei = feature == BattleQaFeature.援軍 ? on : Get(BattleQaFeature.援軍);
            bool evt = feature == BattleQaFeature.戦況イベント ? on : Get(BattleQaFeature.戦況イベント);
            string n = string.IsNullOrEmpty(newName) ? AutoName(env, rei, evt) : newName;
            return new BattleQaFeatureSwitches(n, version, env, rei, evt);
        }

        /// <summary>既定と違う項目だけを並べた名前（全部既定なら「既定」）。</summary>
        public static string AutoName(bool envelopment, bool reinforcement, bool battleEvents)
        {
            var parts = new List<string>();
            if (envelopment != FixedDefaultOf(BattleQaFeature.自動包囲)) parts.Add("自動包囲" + OnOff(envelopment));
            if (reinforcement != FixedDefaultOf(BattleQaFeature.援軍)) parts.Add("援軍" + OnOff(reinforcement));
            if (battleEvents != FixedDefaultOf(BattleQaFeature.戦況イベント)) parts.Add("戦況イベント" + OnOff(battleEvents));
            return parts.Count == 0 ? "既定" : string.Join("+", parts);
        }

        public static string OnOff(bool on) => on ? "ON" : "OFF";

        /// <summary>QAから実経路へ接続済みか（未接続の項目は ON を拒否する）。版2で全項目接続済み。</summary>
        public static bool IsConnected(BattleQaFeature feature)
            => feature == BattleQaFeature.自動包囲 || feature == BattleQaFeature.援軍 || feature == BattleQaFeature.戦況イベント;

        /// <summary>適用先（ログ用）。</summary>
        public static string TargetName(BattleQaFeature feature)
        {
            switch (feature)
            {
                case BattleQaFeature.自動包囲: return "BattlefieldCommandManager.autoEnvelopment（QAの軍団長AIのみ）";
                case BattleQaFeature.援軍: return "BattleSetup の時限増援（QA所有の BattleSetup・予約1件）";
                case BattleQaFeature.戦況イベント: return "BattleEventManager（QAシーン所有・自然抽選）";
                default: return "（不明な項目）";
            }
        }

        /// <summary>既定と違う項目の数。</summary>
        public int ChangedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < FeatureCount; i++)
                    if (values[i] != FixedDefaultOf((BattleQaFeature)i)) n++;
                return n;
            }
        }

        /// <summary>先行の固定QAと同じ状態＝既存の固定合格と比較してよい。</summary>
        public bool IsFixedComparable => ChangedCount == 0;

        /// <summary>結果の分類（固定合格の比較対象かどうか）。</summary>
        public string Classification
        {
            get
            {
                if (IsFixedComparable) return "固定（既存の固定合格と比較可）";
                if (Get(BattleQaFeature.戦況イベント)) return "自然イベントONモード（固定合格の比較対象外・自然会戦の合格にも使わない）";
                return "切り分け（既定と異なる隔離＝固定合格の比較対象外）";
            }
        }

        /// <summary>指定の矛盾を列挙する（空＝整合）。準備前に呼び、矛盾があれば準備失敗にする。</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (version != CurrentVersion) errors.Add("版が違う（設定=" + version + " / 対応=" + CurrentVersion + "）");
            for (int i = 0; i < FeatureCount; i++)
            {
                var f = (BattleQaFeature)i;
                if (values[i] && !IsConnected(f))
                    errors.Add(f + "=ON：未接続");
            }
            return errors;
        }

        /// <summary>設定名・版・各スイッチ・分類の要約（ログ・画面用）。</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("機能スイッチ「").Append(name).Append("」版").Append(version).Append("：");
            for (int i = 0; i < FeatureCount; i++)
            {
                var f = (BattleQaFeature)i;
                if (i > 0) sb.Append("／");
                sb.Append(f).Append('=').Append(OnOff(values[i]));
                if (values[i] != FixedDefaultOf(f)) sb.Append("（既定から変更）");
            }
            sb.Append("　分類＝").Append(Classification);
            return sb.ToString();
        }
    }
}
