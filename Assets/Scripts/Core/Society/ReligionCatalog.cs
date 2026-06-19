using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宗教の系統（教義の家族）。改宗親和や聖戦傾向の素地に使う粗い分類。
    /// アブラハム系（ユダヤ/キリスト/イスラム）／ダルマ系（仏教/ヒンドゥー）／その他。
    /// </summary>
    public enum ReligionFamily { その他, アブラハム系, ダルマ系 }

    /// <summary>
    /// 宗教の教義データ1行（#172-175・カタログ駆動）。名前付き宗教は専用ロジックを書かず
    /// このデータ行として登録する（TalentCatalog/CommodityCatalog と同じ「カタログに登録・個別ルール禁止」の作法）。
    /// 各特性は <see cref="ReligionDoctrineRules"/> が実効係数へ写像し、数式の核は <see cref="ReligionRules"/> へ委譲する
    /// （ここは純データ＝数式を持たない）。すべて読み取り専用＝決定論。
    /// </summary>
    public readonly struct ReligionDef
    {
        /// <summary>宗教名（<see cref="Religion.faithName"/> と突き合わせる識別子）。</summary>
        public readonly string name;
        /// <summary>布教熱（0＝民族宗教で布教しない .. 1＝積極的に改宗を進める）。改宗圧力を増幅。</summary>
        public readonly float proselytism;
        /// <summary>戦闘性・聖戦傾向（0＝非暴力 .. 1＝聖戦を辞さない）。聖戦圧力を増幅。</summary>
        public readonly float militancy;
        /// <summary>社会的結束への寄与（0＝中立 .. 1＝強い共同体）。安定/士気の社会効果を底上げ。</summary>
        public readonly float socialCohesion;
        /// <summary>寛容度（0＝排他・異端を弾圧 .. 1＝多宗教を許容）。神権の異端弾圧/異教民への penalty を緩和。</summary>
        public readonly float tolerance;
        /// <summary>思想親和タグ（FactionData.ideology 文字列。一致勢力の改宗が進みやすい）。</summary>
        public readonly string ideologyAffinity;
        /// <summary>教義の系統（家族）。</summary>
        public readonly ReligionFamily family;

        public ReligionDef(string name, float proselytism, float militancy, float socialCohesion,
            float tolerance, string ideologyAffinity, ReligionFamily family)
        {
            this.name = name ?? "";
            this.proselytism = Mathf.Clamp01(proselytism);
            this.militancy = Mathf.Clamp01(militancy);
            this.socialCohesion = Mathf.Clamp01(socialCohesion);
            this.tolerance = Mathf.Clamp01(tolerance);
            this.ideologyAffinity = ideologyAffinity ?? "";
            this.family = family;
        }
    }

    /// <summary>
    /// 宗教の静的カタログ（#172-175・カタログ駆動の単一窓口）。名前付き伝統＝ユダヤ教/キリスト教
    /// （カトリック/プロテスタント/正教を宗派エントリ）/イスラム教/仏教/ヒンドゥー教を「教義特性で差別化された
    /// データ行」として登録する＝宗教ごとに専用ルールを書かない（CommodityCatalog/TalentCatalog の作法）。
    /// 新しい宗教/宗派は <see cref="Entries"/> に1行足すだけ＝ロジック追加不要。
    /// Game層非依存＝Core 純データ。<see cref="For"/> は未登録名に null安全な無信仰フォールバックを返す。
    /// </summary>
    public static class ReligionCatalog
    {
        /// <summary>未登録名・空名のフォールバック（無信仰＝中立特性）。改宗も聖戦も最小、寛容最大。</summary>
        public static readonly ReligionDef None =
            new ReligionDef("", 0f, 0f, 0f, 1f, "", ReligionFamily.その他);

        // --- カタログ本体（名前付き伝統＝データ行。特性で差別化＝各宗教の「最良」はチューニングされた1行で達成） ---
        private static readonly ReligionDef[] Entries =
        {
            // ユダヤ教：民族宗教＝布教は控えめ・強固な共同体・聖地を持ち戦闘性は中庸・排他寄り。
            new ReligionDef("ユダヤ教",        proselytism: 0.10f, militancy: 0.45f, socialCohesion: 0.85f, tolerance: 0.35f, ideologyAffinity: "伝統主義", family: ReligionFamily.アブラハム系),
            // キリスト教（総称）：強い布教・中程度の結束・中庸の戦闘性。
            new ReligionDef("キリスト教",      proselytism: 0.80f, militancy: 0.45f, socialCohesion: 0.60f, tolerance: 0.55f, ideologyAffinity: "博愛",     family: ReligionFamily.アブラハム系),
            // カトリック：普遍教会＝最も強い布教・高い結束・聖戦の前歴で戦闘性高め・権威主義寄りで寛容低め。
            new ReligionDef("カトリック",      proselytism: 0.90f, militancy: 0.60f, socialCohesion: 0.75f, tolerance: 0.40f, ideologyAffinity: "権威主義", family: ReligionFamily.アブラハム系),
            // プロテスタント：個人信仰＝布教は強いが結束は緩め・戦闘性低め・寛容高め。
            new ReligionDef("プロテスタント",  proselytism: 0.75f, militancy: 0.35f, socialCohesion: 0.50f, tolerance: 0.70f, ideologyAffinity: "自由主義", family: ReligionFamily.アブラハム系),
            // ロシア正教：国家と一体の伝統＝布教は中庸・極めて高い結束・戦闘性中庸・排他寄り。
            new ReligionDef("ロシア正教",      proselytism: 0.45f, militancy: 0.50f, socialCohesion: 0.90f, tolerance: 0.30f, ideologyAffinity: "国家主義", family: ReligionFamily.アブラハム系),
            // イスラム教：強い布教と共同体・聖戦概念で戦闘性高め・啓典の民への寛容は中庸。
            new ReligionDef("イスラム教",      proselytism: 0.85f, militancy: 0.65f, socialCohesion: 0.88f, tolerance: 0.45f, ideologyAffinity: "共同体",   family: ReligionFamily.アブラハム系),
            // 仏教：布教は穏やか・戦闘性は最も低い（非暴力）・結束は中程度・最も寛容。
            new ReligionDef("仏教",            proselytism: 0.55f, militancy: 0.15f, socialCohesion: 0.55f, tolerance: 0.90f, ideologyAffinity: "調和",     family: ReligionFamily.ダルマ系),
            // ヒンドゥー教：民族宗教＝布教せず・戦闘性は中庸・結束は高い・多神で寛容寄り。
            new ReligionDef("ヒンドゥー教",    proselytism: 0.15f, militancy: 0.40f, socialCohesion: 0.80f, tolerance: 0.65f, ideologyAffinity: "伝統主義", family: ReligionFamily.ダルマ系),
        };

        /// <summary>登録済み宗教の全件（読み取り専用・登録順）。</summary>
        public static IReadOnlyList<ReligionDef> All => Entries;

        /// <summary>登録件数。</summary>
        public static int Count => Entries.Length;

        /// <summary>
        /// 名前で教義を引く（カタログの唯一の窓口）。未登録・null・空名は <see cref="None"/>（無信仰）を返す＝null安全。
        /// </summary>
        public static ReligionDef For(string faithName)
        {
            if (string.IsNullOrEmpty(faithName)) return None;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].name == faithName) return Entries[i];
            return None;
        }

        /// <summary>その名の宗教が登録済みか（フォールバックでなく実在エントリか）。</summary>
        public static bool IsKnown(string faithName)
        {
            if (string.IsNullOrEmpty(faithName)) return false;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].name == faithName) return true;
            return false;
        }

        /// <summary>Religion インスタンスから教義を引く（faithName 解決のショートカット・null安全）。</summary>
        public static ReligionDef ForReligion(Religion r) => r == null ? None : For(r.faithName);
    }
}
