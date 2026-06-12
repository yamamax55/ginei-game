using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 職業の標準分類ロジック（<b>日本標準職業分類 JSOC の大分類を参考</b>・#110 職業版の標準化・純ロジック・唯一の窓口）。
    /// POP の少数6種（<see cref="Occupation"/>）と人物（<see cref="Person"/>）の双方を JSOC 大分類（<see cref="OccupationCategory"/>）へ写像し、
    /// 惑星類型（<see cref="SystemType"/>）ごとの標準構成を返す。既存の <see cref="OccupationRules"/>（ゲーム駆動の6種・#96/#93）は不変＝
    /// これは<b>その上に被せる標準分類ビュー</b>（集約・観測層の思想／タイクン回避＝大分類どまり）。test-first。
    /// </summary>
    public static class OccupationClassificationRules
    {
        // 惑星類型別の JSOC 大分類 既定構成（合計1・行＝SystemType 順 工業/農業/鉱業/居住、
        // 列＝OccupationCategory 順 管理/専門技術/事務/販売/サービス/保安/農林漁業/生産工程/輸送機械運転/建設採掘/運搬清掃包装/無職）。
        // 唯一の出所（二重定義しない）。三次産業（居住）は管理・専門・事務・販売・サービスへ広がる＝大分類の意義が出る。
        private static readonly float[][] table =
        {
            //         管理   専門   事務   販売   ｻｰﾋﾞｽ  保安   農林   生産   輸送   建採   運搬   無職
            new[] { 0.03f,0.08f,0.10f,0.05f,0.05f,0.05f,0.03f,0.40f,0.08f,0.03f,0.05f,0.05f }, // 工業＝生産工程主
            new[] { 0.02f,0.04f,0.05f,0.05f,0.05f,0.04f,0.50f,0.06f,0.04f,0.03f,0.07f,0.05f }, // 農業＝農林漁業主
            new[] { 0.03f,0.06f,0.07f,0.03f,0.04f,0.05f,0.03f,0.08f,0.06f,0.40f,0.05f,0.10f }, // 鉱業＝建設採掘主
            new[] { 0.05f,0.12f,0.20f,0.15f,0.13f,0.06f,0.03f,0.05f,0.05f,0.03f,0.03f,0.10f }, // 居住＝三次産業が広がる
        };

        // JSOC 大分類記号 A〜K（無職は職業外＝記号なし）。OccupationCategory 順。
        private static readonly string[] codes =
        { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "—" };

        // 大分類の正式名（JSOC 準拠）。OccupationCategory 順。
        private static readonly string[] names =
        {
            "管理的職業従事者", "専門的・技術的職業従事者", "事務従事者", "販売従事者",
            "サービス職業従事者", "保安職業従事者", "農林漁業従事者", "生産工程従事者",
            "輸送・機械運転従事者", "建設・採掘従事者", "運搬・清掃・包装等従事者", "無職"
        };

        /// <summary>POP の職業（少数6種）→ JSOC 大分類。農民→農林漁業/工員→生産工程/鉱員→建設採掘（採掘）/官吏→事務/軍属→保安/無職→無職。</summary>
        public static OccupationCategory MajorGroupOf(Occupation o)
        {
            switch (o)
            {
                case Occupation.農民: return OccupationCategory.農林漁業;
                case Occupation.工員: return OccupationCategory.生産工程;
                case Occupation.鉱員: return OccupationCategory.建設採掘;
                case Occupation.官吏: return OccupationCategory.事務;
                case Occupation.軍属: return OccupationCategory.保安;
                default:             return OccupationCategory.無職;
            }
        }

        /// <summary>
        /// 人物 → JSOC 大分類。政治家＝管理（議員・大臣）／軍人＝保安（自衛官は階級に依らず保安職業）／
        /// 文民は技術才が文才以上で専門技術（技術者・研究者）、それ以外は事務（一般事務官）。
        /// </summary>
        public static OccupationCategory MajorGroupOf(Person p)
        {
            if (p == null) return OccupationCategory.無職;
            if (p.isPolitician) return OccupationCategory.管理;
            if (p.role == PersonRole.軍人) return OccupationCategory.保安;
            // 文民
            if (p.TechnicalAptitude > 0f && p.TechnicalAptitude >= p.CivilAptitude)
                return OccupationCategory.専門技術;
            return OccupationCategory.事務;
        }

        /// <summary>惑星類型に応じた JSOC 大分類の既定構成（合計1）。三次産業惑星（居住）は管理・専門・事務・販売・サービスへ広がる。</summary>
        public static OccupationProfile Default(SystemType type)
        {
            var prof = new OccupationProfile(table[(int)type]);
            prof.Normalize();
            return prof;
        }

        /// <summary>その類型の基幹大分類（工業＝生産工程/農業＝農林漁業/鉱業＝建設採掘/居住＝事務）。</summary>
        public static OccupationCategory PrimaryGroup(SystemType type)
        {
            switch (type)
            {
                case SystemType.工業: return OccupationCategory.生産工程;
                case SystemType.農業: return OccupationCategory.農林漁業;
                case SystemType.鉱業: return OccupationCategory.建設採掘;
                default:              return OccupationCategory.事務; // 居住
            }
        }

        /// <summary>既存の少数6種 <see cref="Workforce"/> を JSOC 大分類構成へ写像（実データの標準分類ビュー）。合計は元と等しい。</summary>
        public static OccupationProfile Classify(Workforce w)
        {
            var prof = new OccupationProfile();
            if (w == null) return prof;
            for (int i = 0; i < Workforce.Count; i++)
                prof.AddShare(MajorGroupOf((Occupation)i), w.shares[i]);
            return prof;
        }

        /// <summary>JSOC 大分類記号（A〜K・無職は "—"）。表示・分類の照合に使う。</summary>
        public static string JsocCode(OccupationCategory c) => codes[(int)c];

        /// <summary>JSOC 大分類の正式名。</summary>
        public static string GroupName(OccupationCategory c) => names[(int)c];

        /// <summary>保安職業従事者か（#96 徴募源＝軍属に対応する JSOC 群）。</summary>
        public static bool IsSecurityForce(OccupationCategory c) => c == OccupationCategory.保安;

        /// <summary>専門的・技術的職業か（技術者・研究者＝テクノクラート LIFE-7 の母集団）。</summary>
        public static bool IsProfessional(OccupationCategory c) => c == OccupationCategory.専門技術;

        /// <summary>管理的職業か（役員・管理的公務員・議員＝指導層）。</summary>
        public static bool IsManagerial(OccupationCategory c) => c == OccupationCategory.管理;

        /// <summary>徴募源シェア＝保安職業のシェア（#96 兵力の素・JSOC 大分類版）。</summary>
        public static float RecruitableShare(OccupationProfile prof)
            => prof == null ? 0f : Mathf.Clamp01(prof.Share(OccupationCategory.保安));
    }
}
