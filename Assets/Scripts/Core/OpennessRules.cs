using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 開放度スペクトルの純データ（ポパー型・POPR-1 #1511）。開かれた社会(<see cref="openness"/>＝1で完全に
    /// 開かれた)は批判の自由(<see cref="criticismFreedom"/>)と適応力(<see cref="adaptability"/>)を持ち、誤りを
    /// 認め試行錯誤で自己修正できる。閉じた社会は教義・タブーで固まり批判を許さず適応できない。
    /// 解決は <see cref="OpennessRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public struct OpennessState
    {
        public float openness;          // 開放度 0..1（1で完全に開かれた社会）
        public float criticismFreedom;  // 批判の自由 0..1（批判・反論が許される度合い）
        public float adaptability;      // 適応力 0..1（環境変化へ自己修正で適応できる度合い）

        public OpennessState(float openness = 0.5f, float criticismFreedom = 0.5f, float adaptability = 0.5f)
        {
            this.openness = Mathf.Clamp01(openness);
            this.criticismFreedom = Mathf.Clamp01(criticismFreedom);
            this.adaptability = Mathf.Clamp01(adaptability);
        }
    }

    /// <summary>開放度スペクトルの調整係数（ポパー型・POPR-1 #1511）。</summary>
    public readonly struct OpennessParams
    {
        /// <summary>自己修正能力の下駄（開放度0でも残る最低修正能力＝この値〜1.0を開放度で線形補間）。</summary>
        public readonly float selfCorrectionFloor;
        /// <summary>適応速度の基礎/秒（開放度×環境変化が満点でこの率で適応＝開かれた社会ほど速い）。</summary>
        public readonly float adaptationRate;
        /// <summary>誤り蓄積率の最大/秒（閉じた社会＝開放度0でこの率で歪みが溜まる）。</summary>
        public readonly float errorAccumulationMax;
        /// <summary>社会が開く速さ/秒の基礎（改革圧力が満点でこの率で開放化＝民主化・自由化）。</summary>
        public readonly float openingRate;
        /// <summary>社会が閉ざす速さ/秒の基礎（権威主義圧力が満点でこの率で閉鎖化＝開くより速い非対称）。</summary>
        public readonly float closingRate;
        /// <summary>イノベーション下駄（開放度0でも才能ぶんは残る最低イノベーション＝この値〜1.0を開放度で補間）。</summary>
        public readonly float innovationFloor;

        public OpennessParams(float selfCorrectionFloor, float adaptationRate, float errorAccumulationMax,
            float openingRate, float closingRate, float innovationFloor)
        {
            this.selfCorrectionFloor = Mathf.Clamp01(selfCorrectionFloor);
            this.adaptationRate = Mathf.Max(0f, adaptationRate);
            this.errorAccumulationMax = Mathf.Max(0f, errorAccumulationMax);
            this.openingRate = Mathf.Max(0f, openingRate);
            this.closingRate = Mathf.Max(0f, closingRate);
            this.innovationFloor = Mathf.Clamp01(innovationFloor);
        }

        /// <summary>
        /// 既定＝自己修正下駄0.1・適応0.05/秒・誤り蓄積最大0.04/秒・開く0.03/秒・閉ざす0.06/秒・イノベ下駄0.2。
        /// 閉ざす0.06≫開く0.03＝<b>社会は閉ざすのが速く開くのが遅い</b>非対称を数値に固定（自由化は時間がかかる）。
        /// </summary>
        public static OpennessParams Default
            => new OpennessParams(0.1f, 0.05f, 0.04f, 0.03f, 0.06f, 0.2f);
    }

    /// <summary>
    /// 開放度スペクトルの純ロジック（ポパー型・POPR-1 #1511・『開かれた社会とその敵』参考）。開かれた社会
    /// （open society＝批判・試行錯誤で自己修正でき、誤りを認め適応できる）と閉じた社会（closed society＝教義・
    /// タブーで固まり、批判を許さず適応できない）のスペクトルを扱う。<b>開かれた社会は批判と試行錯誤で自己修正でき
    /// 環境変化に速く適応するが、閉じた社会は教義で固まり誤りを溜めて適応できない</b>＝この適応力の非対称が核。
    /// 開放度は批判の自由×多元性×(1−教条主義)で決まり、自己修正能力・適応速度・誤り蓄積の速さ・イノベーションを
    /// 左右する。改革圧力が社会を開き（民主化・自由化）、権威主義・教条が社会を閉ざす（閉ざす方が速い非対称）。
    /// <see cref="SpontaneousOrderRules"/>（自生的秩序＝設計されない秩序の侵食）／<see cref="FreePressRules"/>
    /// （報道による腐敗発見）とは分担し、ここは<b>開かれた社会vs閉じた社会の適応力スペクトル</b>を扱う
    /// （秩序の創発でも腐敗の可視化でもなく、自己修正能力と適応速度のスペクトルが主役）。同EPIC POPR の
    /// <c>InstitutionalCorrectionRules</c>（制度による誤り修正＝<see cref="SelfCorrectionCapacity"/> が修正能力を
    /// 供給）／<c>PiecemealEngineeringRules</c>（漸進的社会工学＝小さな改革の試行錯誤）とも分担し、ここは開放度の
    /// 計測・自己修正能力・適応速度・誤り蓄積・開閉のドリフトに専念する。<see cref="OpennessState"/> と
    /// <see cref="SelfCorrectionCapacity"/> が他 POPR モジュールの基盤。
    /// すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OpennessRules
    {
        /// <summary>
        /// 開放度（0..1）＝批判の自由×多元性×(1−教条主義)。批判が許され多元的で教条が薄いほど開かれる
        /// ＝<b>どれか一つでも欠けると社会は閉じる</b>（積＝批判封殺・一元支配・教条のどれか一つで0へ）。
        /// </summary>
        public static float OpennessLevel(float criticismFreedom, float pluralism, float dogmatism)
        {
            float cf = Mathf.Clamp01(criticismFreedom);
            float pl = Mathf.Clamp01(pluralism);
            float dg = Mathf.Clamp01(dogmatism);
            return Mathf.Clamp01(cf * pl * (1f - dg));
        }

        /// <summary>
        /// 自己修正能力（0..1）＝開かれた社会ほど誤りを自己修正できる。selfCorrectionFloor〜1.0 を開放度で線形補間
        /// ＝開放度0でも下駄ぶんは残り、開放度1で満点＝<b>批判と試行錯誤が誤りを正す</b>
        /// （<c>InstitutionalCorrectionRules</c> の制度的修正能力へ供給する基盤）。
        /// </summary>
        public static float SelfCorrectionCapacity(float openness, OpennessParams p)
        {
            float o = Mathf.Clamp01(openness);
            return Mathf.Lerp(p.selfCorrectionFloor, 1f, o);
        }

        public static float SelfCorrectionCapacity(float openness)
            => SelfCorrectionCapacity(openness, OpennessParams.Default);

        /// <summary>
        /// 適応速度（dt後に進む適応量 0..1）＝開放度が高いほど環境変化に速く適応する。適応量＝適応率×開放度×
        /// 環境変化×dt＝<b>開かれた社会は変化に速く合わせる</b>（閉じた社会は変化があっても動けない）。
        /// 開放度か環境変化のどちらかが0なら適応しない（変化がない／適応できない）。
        /// </summary>
        public static float AdaptationSpeed(float openness, float environmentalChange, float dt, OpennessParams p)
        {
            float o = Mathf.Clamp01(openness);
            float ec = Mathf.Clamp01(environmentalChange);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(p.adaptationRate * o * ec * step);
        }

        public static float AdaptationSpeed(float openness, float environmentalChange, float dt)
            => AdaptationSpeed(openness, environmentalChange, dt, OpennessParams.Default);

        /// <summary>
        /// 誤り蓄積率（/秒 0..errorAccumulationMax）＝閉じた社会ほど誤りが溜まりやすい。errorAccumulationMax×
        /// (1−開放度)＝<b>批判できないので歪みが残る</b>（開放度1なら0＝誤りは即正される／開放度0なら最大＝
        /// 教条で固まり誤りが堆積する）。呼び出し側が誤りプール／安定度低下へ積む。
        /// </summary>
        public static float ErrorAccumulationRate(float openness, OpennessParams p)
        {
            float o = Mathf.Clamp01(openness);
            return p.errorAccumulationMax * (1f - o);
        }

        public static float ErrorAccumulationRate(float openness)
            => ErrorAccumulationRate(openness, OpennessParams.Default);

        /// <summary>
        /// 社会が開く（dt後の openness 0..1）＝改革圧力が社会を開く（民主化・自由化）。開放量＝開く率×改革圧力×
        /// (1−現開放度)×dt＝伸びしろに比例して<b>ゆっくり開く</b>（開く率＜閉ざす率＝自由化は時間がかかる非対称）。
        /// 既に開ききっていれば伸びない。改革圧力0なら不変。
        /// </summary>
        public static float OpeningTick(float openness, float reformPressure, float dt, OpennessParams p)
        {
            float o = Mathf.Clamp01(openness);
            float rp = Mathf.Clamp01(reformPressure);
            float step = Mathf.Max(0f, dt);
            float opening = p.openingRate * rp * (1f - o) * step;
            return Mathf.Clamp01(o + opening);
        }

        public static float OpeningTick(float openness, float reformPressure, float dt)
            => OpeningTick(openness, reformPressure, dt, OpennessParams.Default);

        /// <summary>
        /// 社会が閉ざす（dt後の openness 0..1）＝権威主義・教条が社会を閉ざす。閉鎖量＝閉ざす率×権威主義圧力×
        /// 現開放度×dt＝<b>開いた社会ほど閉ざされる余地が大きい</b>（閉ざす率＞開く率＝閉鎖は速い非対称）。
        /// 既に閉じきっていれば下がらない。権威主義圧力0なら不変。
        /// </summary>
        public static float ClosingTick(float openness, float authoritarianPressure, float dt, OpennessParams p)
        {
            float o = Mathf.Clamp01(openness);
            float ap = Mathf.Clamp01(authoritarianPressure);
            float step = Mathf.Max(0f, dt);
            float closing = p.closingRate * ap * o * step;
            return Mathf.Clamp01(o - closing);
        }

        public static float ClosingTick(float openness, float authoritarianPressure, float dt)
            => ClosingTick(openness, authoritarianPressure, dt, OpennessParams.Default);

        /// <summary>
        /// 開放のイノベーション（0..1）＝開かれた社会ほど多様な発想が生まれイノベーションが進む。innovationFloor〜1.0
        /// を開放度で補間した倍率に才能を掛ける＝<b>同じ才能でも開かれた社会の方が活きる</b>（閉じた社会では
        /// 才能があっても下駄ぶんしか発揮されない＝多様な発想が許されない）。
        /// </summary>
        public static float InnovationFromOpenness(float openness, float talent, OpennessParams p)
        {
            float o = Mathf.Clamp01(openness);
            float t = Mathf.Clamp01(talent);
            float multiplier = Mathf.Lerp(p.innovationFloor, 1f, o);
            return Mathf.Clamp01(t * multiplier);
        }

        public static float InnovationFromOpenness(float openness, float talent)
            => InnovationFromOpenness(openness, talent, OpennessParams.Default);

        /// <summary>
        /// 閉じた社会か（true＝教義で固まり適応できない閉鎖社会）。開放度が threshold 以下なら閉じた社会と判定
        /// ＝<b>批判を許さず誤りを溜め環境に適応できない</b>。開放度が threshold を上回る間は開かれた社会
        /// （試行錯誤で自己修正できる）。
        /// </summary>
        public static bool IsClosedSociety(float openness, float threshold)
        {
            float o = Mathf.Clamp01(openness);
            float th = Mathf.Clamp01(threshold);
            return o <= th;
        }
    }
}
