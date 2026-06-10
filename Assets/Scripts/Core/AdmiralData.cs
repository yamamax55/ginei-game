using System;
using System.Text;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の能力データを保持する ScriptableObject。
    /// 銀河英雄伝説IV EX の能力値を踏襲します。
    ///
    /// 参謀（最大3名）を付けると、各能力を「参謀の最高値×staffBonusRatio」で補完した
    /// 実効能力を算出します（基準値=public フィールドは書き換えない＝実効値パターン）。
    /// ゲーム側は必ず Effectivexxx ゲッターを参照して実効能力を反映すること。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAdmiral", menuName = "Ginei/Admiral Data")]
    public class AdmiralData : ScriptableObject
    {
        [Header("基本情報")]
        public string admiralName = "提督名";
        public Faction faction;

        [Header("姓名（任意・未設定なら admiralName を使用＝後方互換）")]
        [Tooltip("名（カタカナ）。例：ラインハルト。名→姓の順で合成する")]
        public string givenName = "";

        [Tooltip("ミドルネーム（任意）。名と姓の間に入る")]
        public string middleName = "";

        [Tooltip("姓（カタカナ）。例：ローエングラム")]
        public string familyName = "";

        [Tooltip("貴族の前置詞（平民は空。貴族は「フォン」等）。姓の直前に挿入され、貴族を一目で示す")]
        public string nobleParticle = "";

        [Tooltip("世数（一世・二世…）。0＝無し。1〜99 を漢数字＋「世」で表示し、姓名末尾に区切り無しで付く")]
        public int regnalNumber = 0;

        [Tooltip("異名（例：疾風）。頭上ラベル用の短縮名(ShortName)の前に付く")]
        public string epithet = "";

        [Tooltip("呼称・愛称（短縮表示の最優先）。空なら 姓→名→admiralName の順でフォールバック")]
        public string callName = "";

        [Header("主人公（アンカー・GON-6・任意）")]
        [Tooltip("この提督を主人公（動かない光源）とするか。true＝陣営に関わらず常にプレイヤー操作（FleetAI 非制御）。" +
                 "既定 false＝従来どおり（後方互換）。判定は ProtagonistRules を参照すること")]
        public bool isProtagonist = false;

        [Header("階級（#14・任意）")]
        [Tooltip("階級の序列 tier（所属勢力の階級表 FactionData.ranks から名称を解決）。" +
                 "0＝未設定＝HUDに階級を出さない（後方互換）。欠番tierは直近tierへ丸め（RankSystem.ResolveRankName）。" +
                 "既定ラダー：5准将/6少将/7中将/8大将/9上級大将(帝国)/10元帥")]
        public int rankTier = 0;

        [Header("能力値 (0-100)")]
        [Tooltip("兵力上限・士気に影響")]
        public int leadership = 80;    // 統率

        [Tooltip("攻撃力に影響")]
        public int attack = 80;        // 攻撃

        [Tooltip("被ダメージ軽減に影響")]
        public int defense = 80;       // 防御

        [Tooltip("移動速度・回頭速度に影響")]
        public int mobility = 80;      // 機動

        [Tooltip("補給・コスト等に影響（将来用）")]
        public int operation = 80;     // 運営

        [Tooltip("索敵・回避等に影響（将来用）")]
        public int intelligence = 80;  // 情報

        [Header("艦隊設定")]
        [Tooltip("この提督が率いる際の基準兵力")]
        public int baseStrength = 10000;

        [Header("得意陣形（#104）")]
        [Tooltip("得意陣形ボーナスを有効にするか（false＝未設定＝従来どおりボーナス無し＝後方互換）")]
        public bool hasPreferredFormation = false;

        [Tooltip("この提督の得意陣形。部隊の現在陣形が一致する間、移動・防御にボーナス（hasPreferredFormation=true のとき）")]
        public Formation preferredFormation = Formation.紡錘陣;

        /// <summary>
        /// 部隊の現在陣形 current が、この提督の得意陣形と一致するか。
        /// hasPreferredFormation が false（未設定）なら常に false＝ボーナス無し（後方互換）。
        /// </summary>
        public bool IsPreferredFormation(Formation current)
        {
            return hasPreferredFormation && current == preferredFormation;
        }

        [Header("参謀（最大3名・能力補完）")]
        [Tooltip("能力を補完する参謀（最大3名・提督データを流用）。各能力は参謀の最高値×staffBonusRatio だけ底上げされる")]
        public AdmiralData[] staffOfficers = new AdmiralData[0];

        [Tooltip("参謀の能力が実効能力に寄与する割合 (0〜1)。例:0.2 なら参謀の能力の20%を補完")]
        [Range(0f, 1f)]
        public float staffBonusRatio = 0.2f;

        /// <summary>参謀の最大人数。</summary>
        public const int MaxStaff = 3;

        /// <summary>能力値の上限（補完してもこれを超えない）。</summary>
        public const int MaxStatValue = 100;

        // ===== 実効能力（基準値＋参謀補完。基準フィールドは非破壊）=====
        public int EffectiveLeadership   => ComputeEffective(leadership,   s => s.leadership);
        public int EffectiveAttack       => ComputeEffective(attack,       s => s.attack);
        public int EffectiveDefense      => ComputeEffective(defense,      s => s.defense);
        public int EffectiveMobility     => ComputeEffective(mobility,     s => s.mobility);
        public int EffectiveOperation    => ComputeEffective(operation,    s => s.operation);
        public int EffectiveIntelligence => ComputeEffective(intelligence, s => s.intelligence);

        /// <summary>
        /// 基準値に「参謀（最大MaxStaff名）の当該能力の最高値×staffBonusRatio」を加えた実効値を返す。
        /// 上限は MaxStatValue。基準フィールドは変更しない（実効値パターン）。
        /// </summary>
        private int ComputeEffective(int baseValue, Func<AdmiralData, int> selector)
        {
            int best = 0;
            int counted = 0;
            if (staffOfficers != null)
            {
                for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
                {
                    AdmiralData s = staffOfficers[i];
                    if (s == null || s == this) continue; // 空き枠・自己参照は無視
                    counted++;
                    int v = selector(s);
                    if (v > best) best = v;
                }
            }
            int bonus = Mathf.RoundToInt(best * staffBonusRatio);
            return Mathf.Clamp(baseValue + bonus, 0, MaxStatValue);
        }

        /// <summary>有効な参謀（非null）が1名以上いるか。</summary>
        public bool HasStaff
        {
            get
            {
                if (staffOfficers == null) return false;
                int counted = 0;
                for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
                {
                    AdmiralData s = staffOfficers[i];
                    if (s == null || s == this) continue;
                    return true;
                }
                return false;
            }
        }

        /// <summary>参謀名を「、」区切りで返す（最大MaxStaff名・HUD表示用）。参謀が無ければ空文字。</summary>
        public string GetStaffNames()
        {
            if (staffOfficers == null) return string.Empty;
            string result = string.Empty;
            int counted = 0;
            for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
            {
                AdmiralData s = staffOfficers[i];
                if (s == null || s == this) continue;
                if (counted > 0) result += "、";
                result += s.ShortName;
                counted++;
            }
            return result;
        }

        // ===== 姓名の合成（#523・読み取り時合成・基準フィールド非破壊）=====

        /// <summary>姓名の区切り（中黒）。</summary>
        public const string NameSeparator = "・";

        /// <summary>貴族の前置詞の既定値（生成補助用）。</summary>
        public const string NobleParticleDefault = "フォン";

        /// <summary>世数の上限（これを超えると世数表記は付かない）。</summary>
        public const int RegnalMax = 99;

        /// <summary>世数の接尾語。</summary>
        public const string RegnalSuffixWord = "世";

        private static readonly string[] KanjiDigits =
            { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        /// <summary>
        /// 正式名。名→姓の順で「名・[ミドル]・[前置詞]・姓」を中黒連結し、末尾に世数（区切り無し）を付ける。
        /// 名も姓も空なら admiralName を返す（後方互換）。合成結果が空でも admiralName にフォールバック。
        /// </summary>
        public string FullName
        {
            get
            {
                bool hasGiven = !string.IsNullOrEmpty(givenName);
                bool hasFamily = !string.IsNullOrEmpty(familyName);
                if (!hasGiven && !hasFamily) return admiralName;

                var sb = new StringBuilder();
                AppendToken(sb, givenName);
                AppendToken(sb, middleName);
                AppendToken(sb, nobleParticle);
                AppendToken(sb, familyName);
                string result = sb.ToString() + RegnalSuffix(regnalNumber);
                return string.IsNullOrEmpty(result) ? admiralName : result;
            }
        }

        /// <summary>短縮名（コンパクト表示用）。呼称→姓→名→admiralName の順で最初に在るもの。</summary>
        public string ShortName
        {
            get
            {
                if (!string.IsNullOrEmpty(callName)) return callName;
                if (!string.IsNullOrEmpty(familyName)) return familyName;
                if (!string.IsNullOrEmpty(givenName)) return givenName;
                return admiralName;
            }
        }

        /// <summary>異名＋短縮名（頭上ラベル用）。異名が無ければ ShortName と同じ。例：疾風ウォルフ。</summary>
        public string EpithetName
        {
            get
            {
                string s = ShortName;
                return string.IsNullOrEmpty(epithet) ? s : epithet + s;
            }
        }

        /// <summary>中黒区切りでトークンを連結（空トークンはスキップ＝余分な中黒を出さない）。</summary>
        private static void AppendToken(StringBuilder sb, string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            if (sb.Length > 0) sb.Append(NameSeparator);
            sb.Append(token);
        }

        /// <summary>
        /// 世数の接尾辞を返す（例：1→「一世」, 14→「十四世」, 20→「二十世」）。
        /// 1〜RegnalMax(99) の範囲外は空文字。
        /// </summary>
        public static string RegnalSuffix(int n)
        {
            if (n < 1 || n > RegnalMax) return string.Empty;
            return KanjiNumber(n) + RegnalSuffixWord;
        }

        /// <summary>1〜99 を漢数字へ。範囲外は空文字（RegnalSuffix 専用の内部ヘルパ）。</summary>
        private static string KanjiNumber(int n)
        {
            if (n < 1 || n > 99) return string.Empty;
            if (n < 10) return KanjiDigits[n];
            int tens = n / 10, ones = n % 10;
            string s = (tens == 1) ? "十" : KanjiDigits[tens] + "十";
            if (ones > 0) s += KanjiDigits[ones];
            return s;
        }
    }
}
