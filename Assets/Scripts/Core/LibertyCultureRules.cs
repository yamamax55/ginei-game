using UnityEngine;

namespace Ginei
{
    /// <summary>自由文化（個性と生活の実験）の調整係数（MILL-5 #1487・マジックナンバー禁止＝Params＋Default に集約）。</summary>
    public readonly struct LibertyCultureParams
    {
        /// <summary>革新の配当の最大倍率（生活の実験が満点でこの倍率＝社会の実験室の上限）。</summary>
        public readonly float innovationDividendMax;
        /// <summary>革新の配当の下駄（実験ゼロでも残る最低係数＝この値〜innovationDividendMax を実験度で補間）。</summary>
        public readonly float innovationFloor;
        /// <summary>適応力ボーナスの最大（多様性が満点で適応力にこの率を上乗せ＝選択肢の幅）。</summary>
        public readonly float adaptabilityBonusMax;
        /// <summary>画一化の停滞率の最大/秒（同調圧力が満点でこの率で個性が削られ停滞する）。</summary>
        public readonly float stagnationRateMax;
        /// <summary>奇人への寛容が生む革新価値の最大（寛容が満点でこの値＝天才と革新の余地）。</summary>
        public readonly float eccentricityValueMax;

        public LibertyCultureParams(float innovationDividendMax, float innovationFloor,
            float adaptabilityBonusMax, float stagnationRateMax, float eccentricityValueMax)
        {
            this.innovationDividendMax = Mathf.Max(1f, innovationDividendMax);
            this.innovationFloor = Mathf.Clamp01(innovationFloor);
            this.adaptabilityBonusMax = Mathf.Max(0f, adaptabilityBonusMax);
            this.stagnationRateMax = Mathf.Max(0f, stagnationRateMax);
            this.eccentricityValueMax = Mathf.Max(0f, eccentricityValueMax);
        }

        /// <summary>
        /// 既定＝革新配当最大1.5倍・革新下駄0.7・適応力ボーナス最大0.4・停滞率最大0.05/秒・奇人寛容価値最大0.3。
        /// 革新配当は実験ゼロでも0.7倍を保ち、生活の実験が満点で1.5倍へ＝<b>自由な実験が社会の実験室になる</b>。
        /// </summary>
        public static LibertyCultureParams Default
            => new LibertyCultureParams(1.5f, 0.7f, 0.4f, 0.05f, 0.3f);
    }

    /// <summary>
    /// 自由文化（個性と生活の実験）の純ロジック（MILL-5 #1487・ミル『自由論』第3章参考）。意見・生き方の
    /// 多様度が研究・適応力の係数を高める。<b>個性（individuality）の自由な発露と生活の実験（experiments of
    /// living）＝人々が自由に多様な生き方を試すことが社会の進歩と適応力の源泉で、画一性（conformity）は停滞を
    /// 生む＝自由は社会の実験室</b>がこのモジュールの核。個人の自由×(1−同調圧力)で個性が発露し（<see
    /// cref="Individuality"/>）、個性と多様性が生活の実験を生み（<see cref="ExperimentsOfLiving"/>）、その実験が
    /// 新しい発見・改善をもたらして研究へ係数を供給し（<see cref="InnovationDividend"/>）、多様な生き方のストックが
    /// 環境変化への適応力を高める（<see cref="AdaptabilityBonus"/>）。逆に同調圧力は個性を削り社会を停滞させ
    /// （<see cref="ConformityStagnation"/>）、奇人・変人への寛容が天才と革新を許す（<see cref="EccentricityValue"/>）。
    /// <see cref="OpennessRules"/>（開かれた社会の自己修正＝批判と試行錯誤で誤りを正す適応力スペクトル）／
    /// <see cref="ResearchRules"/>（研究の進捗そのもの＝ここはその係数を供給する側）とは分担し、ここは<b>自由な個性
    /// と多様性が社会の実験室になり研究・適応力の係数を高める</b>を扱う（自己修正でも研究進捗でもなく、個性と生活の
    /// 実験という多様度が主役）。<c>CultureRules</c>（文化の同化・分離独立）／同EPIC MILL の <c>PublicOpinionRules</c>
    /// （世論・多様な意見の集約）とも分担し、ここは個性の発露・生活の実験・革新の配当・適応力・画一化の停滞に専念する。
    /// すべて plain な float で受け渡す。乱数なし・決定論・基準値非破壊（倍率・係数を返すだけ）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class LibertyCultureRules
    {
        /// <summary>
        /// 個性の発露（0..1）＝個人の自由×(1−同調圧力)。自由に自分らしく生きられ、同調圧力が薄いほど個性が出る
        /// ＝<b>自由があっても同調圧力が満点なら個性は出ない</b>（積＝自由ゼロでも同調圧力満点でも0へ）。
        /// </summary>
        public static float Individuality(float personalFreedom, float conformityPressure)
        {
            float pf = Mathf.Clamp01(personalFreedom);
            float cp = Mathf.Clamp01(conformityPressure);
            return Mathf.Clamp01(pf * (1f - cp));
        }

        /// <summary>
        /// 生活の実験（0..1）＝個性×多様性。個性が発露し、生き方の多様性があるほど多様な生き方を試す実験が生まれる
        /// ＝<b>個性と多様性の両方が要る</b>（積＝個性ゼロでも多様性ゼロでも実験は生まれない）。
        /// </summary>
        public static float ExperimentsOfLiving(float individuality, float diversity)
        {
            float ind = Mathf.Clamp01(individuality);
            float div = Mathf.Clamp01(diversity);
            return Mathf.Clamp01(ind * div);
        }

        /// <summary>
        /// 革新の配当（倍率 innovationFloor〜innovationDividendMax）＝生活の実験が新しい発見・改善をもたらす。
        /// innovationFloor〜innovationDividendMax を実験度で線形補間＝<b>自由な実験が社会の実験室になる</b>
        /// （実験ゼロでも下駄ぶんは残り、実験が満点で最大倍率＝<see cref="ResearchRules"/> の研究産出へ掛ける係数）。
        /// </summary>
        public static float InnovationDividend(float experimentsOfLiving, LibertyCultureParams p)
        {
            float e = Mathf.Clamp01(experimentsOfLiving);
            return Mathf.Lerp(p.innovationFloor, p.innovationDividendMax, e);
        }

        public static float InnovationDividend(float experimentsOfLiving)
            => InnovationDividend(experimentsOfLiving, LibertyCultureParams.Default);

        /// <summary>
        /// 適応力ボーナス（0..adaptabilityBonusMax）＝多様な生き方のストックが環境変化への適応力を高める。
        /// adaptabilityBonusMax×多様性＝<b>選択肢の幅が広いほど変化に合わせられる</b>（画一的な社会は一つの型に
        /// 賭けて変化に弱い／多様な社会は手持ちの選択肢から最適を選べる）。呼び出し側が適応力へ上乗せする。
        /// </summary>
        public static float AdaptabilityBonus(float diversity, LibertyCultureParams p)
        {
            float div = Mathf.Clamp01(diversity);
            return p.adaptabilityBonusMax * div;
        }

        public static float AdaptabilityBonus(float diversity)
            => AdaptabilityBonus(diversity, LibertyCultureParams.Default);

        /// <summary>
        /// 画一化の停滞（dt後に削られる個性量 0..1）＝同調圧力が個性を抑え社会を停滞させる。停滞量＝停滞率×
        /// 同調圧力×dt＝<b>画一化が進むほど個性が削られ進歩が止まる</b>（同調圧力0なら停滞しない）。
        /// 呼び出し側が個性・多様性ストックから差し引く。
        /// </summary>
        public static float ConformityStagnation(float conformityPressure, float dt, LibertyCultureParams p)
        {
            float cp = Mathf.Clamp01(conformityPressure);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(p.stagnationRateMax * cp * step);
        }

        public static float ConformityStagnation(float conformityPressure, float dt)
            => ConformityStagnation(conformityPressure, dt, LibertyCultureParams.Default);

        /// <summary>
        /// 奇人への寛容が生む革新価値（0..eccentricityValueMax）＝奇人・変人への寛容が天才と革新を許す。
        /// eccentricityValueMax×寛容度＝<b>奇行を許す社会ほど活力がある</b>（ミル＝個性的・型破りな人物を排除しない
        /// 社会こそ天才を育み新しい発見を生む）。寛容ゼロなら0＝画一化を強いる社会は革新の芽を摘む。
        /// </summary>
        public static float EccentricityValue(float eccentricToleration, LibertyCultureParams p)
        {
            float et = Mathf.Clamp01(eccentricToleration);
            return p.eccentricityValueMax * et;
        }

        public static float EccentricityValue(float eccentricToleration)
            => EccentricityValue(eccentricToleration, LibertyCultureParams.Default);

        /// <summary>
        /// モノカルチャー（画一文化）のリスク（0..1）＝画一的な文化が革新を枯らすリスク。多様性が threshold 以下で
        /// 立ち上がり、不足ぶんを threshold で正規化＝<b>多様性が乏しいほど一つの型に固まり革新が枯れる</b>
        /// （多様性が threshold を上回れば0＝活力ある文化／多様性0で最大1＝完全な画一化）。
        /// </summary>
        public static float MonocultureRisk(float diversity, float threshold)
        {
            float div = Mathf.Clamp01(diversity);
            float th = Mathf.Clamp01(threshold);
            if (th <= 0f) return 0f;          // 閾値0＝モノカルチャーを問わない
            if (div >= th) return 0f;         // 閾値以上の多様性＝リスクなし
            return Mathf.Clamp01((th - div) / th);
        }

        /// <summary>
        /// 活力ある文化か（true＝個性と多様性に富む活力ある文化）。個性と多様性がともに threshold を上回るとき
        /// ＝<b>自由な個性の発露と多様な生き方の両方が揃って初めて社会は実験室になる</b>（どちらか一方が
        /// threshold 以下なら停滞へ傾く）。
        /// </summary>
        public static bool IsVibrantCulture(float individuality, float diversity, float threshold)
        {
            float ind = Mathf.Clamp01(individuality);
            float div = Mathf.Clamp01(diversity);
            float th = Mathf.Clamp01(threshold);
            return ind > th && div > th;
        }
    }
}
