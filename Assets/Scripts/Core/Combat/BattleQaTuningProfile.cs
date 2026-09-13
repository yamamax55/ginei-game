using System.Collections.Generic;
using System.Text;

namespace Ginei
{
    /// <summary>固定会戦QAの調整プリセットで比較できる項目（SPEED-07）。値の正本は各コンポーネントの既存 public 項目。</summary>
    public enum BattleQaTuningField
    {
        /// <summary>FleetMovement.maxSpeed</summary>
        移動速度,
        /// <summary>FleetMovement.rotationSpeed</summary>
        回頭速度,
        /// <summary>FleetMorale.recoveryRate（通常時と敗走回復中の両方の回復量）</summary>
        士気回復量,
        /// <summary>FleetMorale.routedRecoveryDelay（敗走時だけの回復待ち。通常時の回復には待ちが無い）</summary>
        敗走回復待ち,
        /// <summary>BattlefieldCommandManager.corpsMinSpacing（軍団隊形の<b>最小</b>間隔。実間隔＝Max(最小, 2×最大占有半径＋3)）。
        /// 軍団長AIを使うプリセットだけに適用先がある（無ければ準備失敗）。</summary>
        軍団隊形間隔,
    }

    /// <summary>上書きの方式。</summary>
    public enum BattleQaTuningMode
    {
        /// <summary>上書きしない＝コンポーネントの実効値のまま。</summary>
        未指定,
        絶対値,
        /// <summary>コンポーネントの実効値（基準）に掛ける。</summary>
        倍率,
    }

    /// <summary>1項目の上書き指定。</summary>
    public readonly struct BattleQaTuningOverride
    {
        public readonly BattleQaTuningMode mode;
        public readonly float value;

        public BattleQaTuningOverride(BattleQaTuningMode mode, float value)
        {
            this.mode = mode;
            this.value = value;
        }

        public static BattleQaTuningOverride None => new BattleQaTuningOverride(BattleQaTuningMode.未指定, 0f);
        public static BattleQaTuningOverride Absolute(float v) => new BattleQaTuningOverride(BattleQaTuningMode.絶対値, v);
        public static BattleQaTuningOverride Scale(float s) => new BattleQaTuningOverride(BattleQaTuningMode.倍率, s);

        public bool IsSet => mode != BattleQaTuningMode.未指定;

        /// <summary>基準値から適用値を求める（未指定は基準のまま）。妥当性は呼び出し側で <see cref="BattleQaTuningProfile.ValidateResolved"/>。</summary>
        public float Resolve(float baseline)
        {
            switch (mode)
            {
                case BattleQaTuningMode.絶対値: return value;
                case BattleQaTuningMode.倍率: return baseline * value;
                default: return baseline;
            }
        }

        public string Describe()
        {
            switch (mode)
            {
                case BattleQaTuningMode.絶対値: return "=" + value.ToString("0.####");
                case BattleQaTuningMode.倍率: return "×" + value.ToString("0.####");
                default: return "未指定（実効値のまま）";
            }
        }
    }

    /// <summary>
    /// 固定会戦QAの<b>検証用調整プリセット</b>（SPEED-07・純ロジック）。
    ///
    /// ★値の正本は持たない：既定プリセットは全項目「未指定」＝実コンポーネントの実効値をそのまま使う
    /// （スクリプト既定を二重に書き写さない）。比較用は「1項目だけ倍率/絶対値」を指定する。
    /// ★適用先はQAが組んだ使い捨て艦隊のコンポーネントだけ（アセット・セーブ・ゲーム既定は変えない）。
    /// </summary>
    public sealed class BattleQaTuningProfile
    {
        /// <summary>プリセットの版（ログで特定するため。項目や解釈を変えたら上げる）。
        /// 2＝軍団隊形間隔を未接続から「最小間隔の実接続」へ変更。</summary>
        public const int CurrentVersion = 2;

        public readonly string name;
        public readonly int version;
        private readonly BattleQaTuningOverride[] overrides;

        private static readonly int FieldCount = System.Enum.GetValues(typeof(BattleQaTuningField)).Length;

        public BattleQaTuningProfile(string name, int version, IDictionary<BattleQaTuningField, BattleQaTuningOverride> values)
        {
            this.name = string.IsNullOrEmpty(name) ? "無名" : name;
            this.version = version;
            overrides = new BattleQaTuningOverride[FieldCount];
            if (values != null)
                foreach (KeyValuePair<BattleQaTuningField, BattleQaTuningOverride> kv in values)
                {
                    int i = (int)kv.Key;
                    if (i >= 0 && i < FieldCount) overrides[i] = kv.Value;
                }
        }

        /// <summary>全項目未指定（先行QAと同じ実効値）。</summary>
        public static BattleQaTuningProfile Default => new BattleQaTuningProfile("既定", CurrentVersion, null);

        public BattleQaTuningOverride Get(BattleQaTuningField field)
        {
            int i = (int)field;
            return i >= 0 && i < FieldCount ? overrides[i] : BattleQaTuningOverride.None;
        }

        /// <summary>指定した項目の数。</summary>
        public int OverrideCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < overrides.Length; i++) if (overrides[i].IsSet) n++;
                return n;
            }
        }

        public bool IsDefault => OverrideCount == 0;

        /// <summary>この項目を上書きしたコピー（名前は指定、他項目は不変）。</summary>
        public BattleQaTuningProfile With(string newName, BattleQaTuningField field, BattleQaTuningOverride value)
        {
            var d = new Dictionary<BattleQaTuningField, BattleQaTuningOverride>();
            for (int i = 0; i < overrides.Length; i++) if (overrides[i].IsSet) d[(BattleQaTuningField)i] = overrides[i];
            d[field] = value;
            return new BattleQaTuningProfile(newName, version, d);
        }

        /// <summary>適用先コンポーネントの項目名（ログ用）。</summary>
        public static string ComponentFieldName(BattleQaTuningField field)
        {
            switch (field)
            {
                case BattleQaTuningField.移動速度: return "FleetMovement.maxSpeed";
                case BattleQaTuningField.回頭速度: return "FleetMovement.rotationSpeed";
                case BattleQaTuningField.士気回復量: return "FleetMorale.recoveryRate";
                case BattleQaTuningField.敗走回復待ち: return "FleetMorale.routedRecoveryDelay";
                case BattleQaTuningField.軍団隊形間隔: return "BattlefieldCommandManager.corpsMinSpacing";
                default: return "（不明な項目）";
            }
        }

        /// <summary>適用先が艦隊でなく軍団長AI（BattlefieldCommandManager）の項目か。</summary>
        public static bool RequiresCorpsCommandManager(BattleQaTuningField field) => field == BattleQaTuningField.軍団隊形間隔;

        /// <summary>0 を許す項目か（回復量・回復待ちは0が有効な条件。速度は0だと動けず比較にならない）。</summary>
        public static bool AllowsZero(BattleQaTuningField field)
            => field == BattleQaTuningField.士気回復量 || field == BattleQaTuningField.敗走回復待ち;

        /// <summary>適用値の妥当性（有限・範囲）。null＝妥当。</summary>
        public static string ValidateResolved(BattleQaTuningField field, float resolved)
        {
            if (float.IsNaN(resolved) || float.IsInfinity(resolved)) return field + "：適用値が有限でない（" + resolved + "）";
            if (AllowsZero(field) ? resolved < 0f : resolved <= 0f)
                return field + "：適用値 " + resolved.ToString("0.####") + " は範囲外（" + (AllowsZero(field) ? "0以上" : "0より大") + "）";
            return null;
        }

        /// <summary>指定の矛盾を列挙する（空＝整合）。準備前に呼び、矛盾があれば準備失敗にする。</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (version != CurrentVersion) errors.Add("版が違う（プリセット=" + version + " / 対応=" + CurrentVersion + "）");
            for (int i = 0; i < overrides.Length; i++)
            {
                BattleQaTuningOverride o = overrides[i];
                if (!o.IsSet) continue;
                var field = (BattleQaTuningField)i;
                if (float.IsNaN(o.value) || float.IsInfinity(o.value)) { errors.Add(field + "：指定値が有限でない（" + o.value + "）"); continue; }
                if (o.mode == BattleQaTuningMode.倍率)
                {
                    if (AllowsZero(field) ? o.value < 0f : o.value <= 0f)
                        errors.Add(field + "：倍率 " + o.value.ToString("0.####") + " は範囲外（" + (AllowsZero(field) ? "0以上" : "0より大") + "）");
                }
                else
                {
                    string e = ValidateResolved(field, o.value);
                    if (e != null) errors.Add(e);
                }
            }
            return errors;
        }

        /// <summary>設定名・版・指定項目の要約（ログ・画面用）。</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("調整プリセット「").Append(name).Append("」版").Append(version);
            if (IsDefault) { sb.Append("（全項目未指定＝実効値のまま）"); return sb.ToString(); }
            sb.Append("：");
            bool first = true;
            for (int i = 0; i < overrides.Length; i++)
            {
                if (!overrides[i].IsSet) continue;
                if (!first) sb.Append("／");
                first = false;
                var field = (BattleQaTuningField)i;
                sb.Append(field).Append('(').Append(ComponentFieldName(field)).Append(')').Append(overrides[i].Describe());
            }
            return sb.ToString();
        }
    }
}
