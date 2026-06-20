using UnityEngine;

namespace Ginei
{
    /// <summary>国教（国家宗教）の調整係数。聖俗の権力分担・正統化・国民統合・世俗化の効きを定める。</summary>
    public readonly struct StateReligionParams
    {
        /// <summary>宗教的権威と国民の信仰が統治を正統化する効き（幾何平均の重み＝両方揃って効く）。</summary>
        public readonly float legitimationWeight;
        /// <summary>正統化と宗教的同質性が国民統合を生む効き。</summary>
        public readonly float unificationWeight;
        /// <summary>聖職者権力が世俗改革と衝突して政教の緊張を生む強さ。</summary>
        public readonly float tensionScale;
        /// <summary>信仰と教義の呼びかけが宗教的動員（聖戦）を起こす効き。</summary>
        public readonly float mobilizationScale;
        /// <summary>近代化と教育が世俗化（信仰の後退）を進める強さ。</summary>
        public readonly float secularizationScale;
        /// <summary>政権の打算が正統化を政治利用に転じる効き（高いほど宗教を道具化）。</summary>
        public readonly float exploitationScale;
        /// <summary>政教の緊張が国教体制の安定を削る重み。</summary>
        public readonly float tensionPenalty;
        /// <summary>世俗化が国教体制の安定を削る重み（信仰後退で正統化の土台が痩せる）。</summary>
        public readonly float secularPenalty;

        public StateReligionParams(float legitimationWeight, float unificationWeight, float tensionScale,
            float mobilizationScale, float secularizationScale, float exploitationScale,
            float tensionPenalty, float secularPenalty)
        {
            this.legitimationWeight = Mathf.Clamp01(legitimationWeight);
            this.unificationWeight = Mathf.Clamp01(unificationWeight);
            this.tensionScale = Mathf.Clamp01(tensionScale);
            this.mobilizationScale = Mathf.Clamp01(mobilizationScale);
            this.secularizationScale = Mathf.Clamp01(secularizationScale);
            this.exploitationScale = Mathf.Clamp01(exploitationScale);
            this.tensionPenalty = Mathf.Clamp01(tensionPenalty);
            this.secularPenalty = Mathf.Clamp01(secularPenalty);
        }

        /// <summary>既定＝正統化重み1.0・統合重み1.0・緊張0.8・動員0.9・世俗化0.7・政治利用0.6・緊張罰0.6・世俗化罰0.4。</summary>
        public static StateReligionParams Default =>
            new StateReligionParams(1f, 1f, 0.8f, 0.9f, 0.7f, 0.6f, 0.6f, 0.4f);
    }

    /// <summary>
    /// 国教（国家と宗教の結びつき）の純ロジック。<b>国教による統治の正統化</b>（宗教的権威×国民の信仰）、
    /// <b>聖俗の権力分担</b>（世俗権力と聖職者権力のバランス）、<b>信仰による国民統合</b>、
    /// <b>宗教的権威と世俗権力の緊張</b>（聖職者権力×世俗改革＝政教の対立）、<b>政教一致と政教分離</b>（世俗化の進行）、
    /// <b>宗教の政治利用</b>（正統化×政権の打算）をまとめて扱う。正統化が国民統合を生むが、聖職者権力が強く世俗改革を進めれば
    /// 緊張が高まり、近代化と教育は世俗化で信仰の土台を痩せさせる＝統合−緊張−世俗化で国教体制の安定が決まる。
    /// 全入力クランプ・乱数なし・決定論・基準非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class StateReligionRules
    {
        /// <summary>
        /// 統治の正統化（0..1）＝宗教的権威と国民の信仰の幾何平均×legitimationWeight。
        /// どちらか一方だけ高くても正統化は低い（権威だけでは信徒なし／信仰だけでは制度なし）。
        /// </summary>
        public static float LegitimationPower(float religiousAuthority, float popularFaith, StateReligionParams p)
        {
            float auth = Mathf.Clamp01(religiousAuthority);
            float faith = Mathf.Clamp01(popularFaith);
            float core = Mathf.Sqrt(auth * faith);
            return Mathf.Clamp01(core * p.legitimationWeight);
        }

        public static float LegitimationPower(float religiousAuthority, float popularFaith)
            => LegitimationPower(religiousAuthority, popularFaith, StateReligionParams.Default);

        /// <summary>
        /// 国民統合（0..1）＝正統化×宗教的同質性×unificationWeight。同じ信仰で国がまとまる＝
        /// 正統化が高くても多宗派に割れていれば統合は弱い（同質性が橋渡し）。
        /// </summary>
        public static float NationalUnification(float legitimationPower, float religiousHomogeneity, StateReligionParams p)
        {
            float legit = Mathf.Clamp01(legitimationPower);
            float homo = Mathf.Clamp01(religiousHomogeneity);
            return Mathf.Clamp01(legit * homo * p.unificationWeight);
        }

        public static float NationalUnification(float legitimationPower, float religiousHomogeneity)
            => NationalUnification(legitimationPower, religiousHomogeneity, StateReligionParams.Default);

        /// <summary>
        /// 聖俗の権力バランス（-1..1）＝世俗権力と聖職者権力の差。正＝世俗優位（政教分離寄り）、
        /// 負＝聖職者優位（神権政治寄り）、0＝拮抗。tensionScale は使わず差分そのものを返す（均衡の指標）。
        /// </summary>
        public static float ChurchStateBalance(float secularPower, float clericalPower, StateReligionParams p)
        {
            float sec = Mathf.Clamp01(secularPower);
            float cler = Mathf.Clamp01(clericalPower);
            return Mathf.Clamp(sec - cler, -1f, 1f);
        }

        public static float ChurchStateBalance(float secularPower, float clericalPower)
            => ChurchStateBalance(secularPower, clericalPower, StateReligionParams.Default);

        /// <summary>
        /// 政教の緊張（0..1）＝聖職者権力×世俗改革×tensionScale。強い聖職者層が世俗改革に抵抗するほど対立が激化する
        /// （叙任権闘争・宗教改革型）。どちらか弱ければ緊張は低い。
        /// </summary>
        public static float TheocraticTension(float clericalPower, float secularReform, StateReligionParams p)
        {
            float cler = Mathf.Clamp01(clericalPower);
            float reform = Mathf.Clamp01(secularReform);
            return Mathf.Clamp01(cler * reform * p.tensionScale);
        }

        public static float TheocraticTension(float clericalPower, float secularReform)
            => TheocraticTension(clericalPower, secularReform, StateReligionParams.Default);

        /// <summary>
        /// 宗教的動員（0..1）＝信仰×教義の呼びかけ×mobilizationScale。厚い信仰へ強い教義の号令がかかると聖戦的動員が立つ
        /// （十字軍・ジハード型）。信仰が薄いか号令がなければ動員は弱い。
        /// </summary>
        public static float ReligiousMobilization(float popularFaith, float doctrinalCall, StateReligionParams p)
        {
            float faith = Mathf.Clamp01(popularFaith);
            float call = Mathf.Clamp01(doctrinalCall);
            return Mathf.Clamp01(faith * call * p.mobilizationScale);
        }

        public static float ReligiousMobilization(float popularFaith, float doctrinalCall)
            => ReligiousMobilization(popularFaith, doctrinalCall, StateReligionParams.Default);

        /// <summary>
        /// 世俗化の進行（0..1）＝近代化と教育の幾何平均×secularizationScale。近代化と教育水準が揃って上がると信仰が後退する
        /// （宗教の私事化）。どちらか欠ければ世俗化は進みにくい。
        /// </summary>
        public static float Secularization(float modernization, float educationLevel, StateReligionParams p)
        {
            float modern = Mathf.Clamp01(modernization);
            float edu = Mathf.Clamp01(educationLevel);
            float core = Mathf.Sqrt(modern * edu);
            return Mathf.Clamp01(core * p.secularizationScale);
        }

        public static float Secularization(float modernization, float educationLevel)
            => Secularization(modernization, educationLevel, StateReligionParams.Default);

        /// <summary>
        /// 宗教の政治利用（0..1）＝正統化×政権の打算×exploitationScale。正統化の資源を、信仰を真に奉じるのでなく
        /// 道具として動員する度合い（政権の打算が高いほど大きい）。正統化が無ければ利用する資源も無い。
        /// </summary>
        public static float PoliticalExploitation(float legitimationPower, float regimeCynicism, StateReligionParams p)
        {
            float legit = Mathf.Clamp01(legitimationPower);
            float cynic = Mathf.Clamp01(regimeCynicism);
            return Mathf.Clamp01(legit * cynic * p.exploitationScale);
        }

        public static float PoliticalExploitation(float legitimationPower, float regimeCynicism)
            => PoliticalExploitation(legitimationPower, regimeCynicism, StateReligionParams.Default);

        /// <summary>
        /// 国教体制の安定（-1..1）＝国民統合 −（政教の緊張×tensionPenalty）−（世俗化×secularPenalty）。
        /// 信仰で国がまとまるほど安定し、聖俗の緊張と信仰の後退が安定を削る。正＝安定／負＝動揺。
        /// </summary>
        public static float StateReligionStability(float nationalUnification, float theocraticTension,
            float secularization, StateReligionParams p)
        {
            float unif = Mathf.Clamp01(nationalUnification);
            float tension = Mathf.Clamp01(theocraticTension);
            float secular = Mathf.Clamp01(secularization);
            float stability = unif - tension * p.tensionPenalty - secular * p.secularPenalty;
            return Mathf.Clamp(stability, -1f, 1f);
        }

        public static float StateReligionStability(float nationalUnification, float theocraticTension, float secularization)
            => StateReligionStability(nationalUnification, theocraticTension, secularization, StateReligionParams.Default);
    }
}
