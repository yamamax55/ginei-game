using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POP の職業（就労）ロジック（#110 職業版／#96 徴募・#93 生産連携・純ロジック・唯一の窓口）。
    /// 生産年齢人口（<see cref="Population.working"/>／コホート未設定なら population×既定比）を職業へ割り振り、
    /// <b>適所度</b>（惑星の類型に合った職に就いているか）・<b>徴募源</b>（軍属）・<b>失業圧</b>（無職→不満）を導く。
    /// 既定構成は惑星の <see cref="Province.systemType"/> でバイアス（工業惑星は工員が多い等）。タイクン回避＝職業は少数。test-first。
    /// </summary>
    public static class OccupationRules
    {
        // 類型別の既定職業構成（合計1・行＝SystemType 順 工業/農業/鉱業/居住、列＝Occupation 順 農民/工員/鉱員/官吏/軍属/無職）。
        // 唯一の出所（二重定義しない）。
        private static readonly float[][] table =
        {
            new[] { 0.10f, 0.50f, 0.10f, 0.15f, 0.10f, 0.05f }, // 工業＝工員主
            new[] { 0.55f, 0.10f, 0.05f, 0.15f, 0.10f, 0.05f }, // 農業＝農民主
            new[] { 0.10f, 0.10f, 0.50f, 0.15f, 0.10f, 0.05f }, // 鉱業＝鉱員主
            new[] { 0.15f, 0.15f, 0.05f, 0.30f, 0.20f, 0.15f }, // 居住＝官吏/サービス・無職やや多
        };

        /// <summary>惑星の類型に応じた既定の労働力構成（合計1）。</summary>
        public static Workforce Default(SystemType type)
        {
            var w = new Workforce(table[(int)type]);
            w.Normalize();
            return w;
        }

        /// <summary>その類型の基幹職業（工業＝工員/農業＝農民/鉱業＝鉱員/居住＝官吏）。適所度の基準。</summary>
        public static Occupation PrimaryOccupation(SystemType type)
        {
            switch (type)
            {
                case SystemType.工業: return Occupation.工員;
                case SystemType.農業: return Occupation.農民;
                case SystemType.鉱業: return Occupation.鉱員;
                default: return Occupation.官吏; // 居住
            }
        }

        /// <summary>生産年齢人口（コホートがあれば working、無ければ population×既定生産年齢比）。</summary>
        public static float WorkingAge(Province p)
        {
            if (p == null) return 0f;
            if (p.demographics != null) return p.demographics.working;
            return p.population * PopulationDynamicsRules.DefaultWorkingShare;
        }

        /// <summary>その職業に就いている実数（人）＝生産年齢×職業シェア。Province.workforce 未設定なら類型既定で見積る。</summary>
        public static float Workers(Province p, Occupation o)
        {
            if (p == null) return 0f;
            Workforce w = p.workforce ?? Default(p.systemType);
            return WorkingAge(p) * w.Share(o);
        }

        /// <summary>就業率（1−無職シェア）。</summary>
        public static float EmploymentRate(Province p)
        {
            if (p == null) return 0f;
            Workforce w = p.workforce ?? Default(p.systemType);
            return Mathf.Clamp01(1f - w.Share(Occupation.無職));
        }

        /// <summary>徴募源＝軍属に就いている実数（#96 兵力の素）。</summary>
        public static float RecruitablePool(Province p) => Workers(p, Occupation.軍属);

        /// <summary>失業圧（無職シェア・0..1）。高いと不満#113 の火種。</summary>
        public static float UnemploymentPressure(Province p)
        {
            if (p == null) return 0f;
            Workforce w = p.workforce ?? Default(p.systemType);
            return Mathf.Clamp01(w.Share(Occupation.無職));
        }

        /// <summary>
        /// 適所度（0..1）＝惑星の類型に合った基幹職に就いている割合。高いほど産出が効率的（正名・適材適所）。
        /// 将来 #93 産出効率へ係数として接続できる（現状は観測・見積り）。
        /// </summary>
        public static float AlignmentFactor(Province p)
        {
            if (p == null) return 0f;
            Workforce w = p.workforce ?? Default(p.systemType);
            return Mathf.Clamp01(w.Share(PrimaryOccupation(p.systemType)));
        }
    }
}
