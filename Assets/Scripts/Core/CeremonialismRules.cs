using UnityEngine;

namespace Ginei
{
    /// <summary>制度の儀礼性の調整係数（威信・伝統の重み・形骸化ドリフト・維持費）。ctor で全てクランプ。</summary>
    public readonly struct CeremonialismParams
    {
        /// <summary>儀礼的価値における威信の重み（残りは伝統＝前例の古さ）。0..1。</summary>
        public readonly float prestigeWeight;
        /// <summary>総合存続力における儀礼性の重み（残りは機能）。0..1。高いほど機能ゼロでも残りやすい。</summary>
        public readonly float ceremonialSurvivalWeight;
        /// <summary>廃止抵抗における既得権の重み（残りは儀礼的威信）。0..1。</summary>
        public readonly float vestedWeight;
        /// <summary>形骸化ドリフト率（/年。機能が儀礼性の比重ぶん実質→形式へ流れる速さ）。</summary>
        public readonly float driftRate;
        /// <summary>儀礼的価値1あたりの維持費（機能なき格式の空費単価）。</summary>
        public readonly float upkeepPerCeremony;
        /// <summary>放置による機能衰退率（/年。neglect1で満額）。</summary>
        public readonly float decayRate;
        /// <summary>改革に要する政治資本の感度（抵抗→必要政治資本の倍率）。</summary>
        public readonly float reformCostScale;

        public CeremonialismParams(
            float prestigeWeight, float ceremonialSurvivalWeight, float vestedWeight,
            float driftRate, float upkeepPerCeremony, float decayRate, float reformCostScale)
        {
            this.prestigeWeight = Mathf.Clamp01(prestigeWeight);
            this.ceremonialSurvivalWeight = Mathf.Clamp01(ceremonialSurvivalWeight);
            this.vestedWeight = Mathf.Clamp01(vestedWeight);
            this.driftRate = Mathf.Max(0f, driftRate);
            this.upkeepPerCeremony = Mathf.Max(0f, upkeepPerCeremony);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.reformCostScale = Mathf.Max(0f, reformCostScale);
        }

        /// <summary>
        /// 既定＝威信重み0.6（残り0.4が伝統）・儀礼存続重み0.5（機能と儀礼性が半々で存続を決める）・
        /// 既得権重み0.4（残り0.6が儀礼的威信）・形骸化0.1/年・維持費50/儀礼的価値・放置衰退0.2/年・改革費感度1.5。
        /// </summary>
        public static CeremonialismParams Default
            => new CeremonialismParams(0.6f, 0.5f, 0.4f, 0.1f, 50f, 0.2f, 1.5f);
    }

    /// <summary>
    /// 制度の儀礼性の純ロジック（VEBL-4 #1603・ヴェブレン『有閑階級の理論』参考・test-first）。
    /// 組織は本来の機能を失っても「格式」「前例」「面子」で生き延び（<see cref="SurvivalDespiteDysfunction"/>＝
    /// 機能ゼロでも儀礼的価値だけで残りうる）、廃止しようとすると儀礼的威信＋既得権を盾に抵抗する
    /// （<see cref="AbolitionResistance"/>）。儀礼的価値は威信×伝統＝前例の古さで決まり（<see cref="CeremonialValue"/>）、
    /// 時間とともに機能が形骸化して儀礼性の比重が増し（<see cref="CeremonialDrift"/>＝実質→形式へのドリフト）、
    /// 機能なき格式の維持には空費がかかる（<see cref="PrestigeUpkeepCost"/>）。中身が空で儀礼だけになれば
    /// 形骸化した制度と判定でき（<see cref="IsHollowInstitution"/>）、改革には抵抗ぶんの政治資本が要る
    /// （<see cref="ReformDifficulty"/>）。放置すれば機能は衰える（<see cref="FunctionalDecayTick"/>）。
    /// ＝**制度は機能を失っても威信と前例で生き延び、廃止には儀礼的抵抗が立ちはだかる**。
    /// 分担：`BureaucracyBloatRules`＝パーキンソン的な人数の自己増殖（規模の動態）／`CeremonyRules`＝
    /// 一回性の儀礼イベント（戴冠・凱旋の演出損益）／`MinistryRules`＝省庁の編制ツリー（構造・配属台帳）／
    /// **本クラス＝儀礼的威信による存続慣性**（機能ゼロでも残る論理・廃止抵抗）。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（新しい値を返す）。調整値は <see cref="CeremonialismParams"/>
    /// （既定 <see cref="CeremonialismParams.Default"/>）。純ロジック（非 MonoBehaviour）。
    /// </summary>
    public static class CeremonialismRules
    {
        /// <summary>儀礼的価値（既定 Params）。</summary>
        public static float CeremonialValue(float prestige, float tradition)
            => CeremonialValue(prestige, tradition, CeremonialismParams.Default);

        /// <summary>
        /// 儀礼的価値（0..1）＝威信×伝統（前例の古さ）の加重ブレンド。
        /// prestigeWeight×威信 ＋ (1−prestigeWeight)×伝統。古く格式ある制度ほど儀礼的価値が高い。
        /// </summary>
        public static float CeremonialValue(float prestige, float tradition, CeremonialismParams p)
        {
            float pr = Mathf.Clamp01(prestige);
            float tr = Mathf.Clamp01(tradition);
            return Mathf.Clamp01(p.prestigeWeight * pr + (1f - p.prestigeWeight) * tr);
        }

        /// <summary>機能不全でも残る存続力（既定 Params）。</summary>
        public static float SurvivalDespiteDysfunction(float functionalValue, float ceremonialValue)
            => SurvivalDespiteDysfunction(functionalValue, ceremonialValue, CeremonialismParams.Default);

        /// <summary>
        /// 総合存続力（0..1）＝(1−ceremonialSurvivalWeight)×機能 ＋ ceremonialSurvivalWeight×儀礼的価値。
        /// **機能ゼロでも儀礼的価値が高ければ存続力は残る**＝制度は機能とは別の論理（儀礼性）で生き延びる。
        /// 呼び出し側が存続/廃止の閾値判定に使う想定（基準非破壊）。
        /// </summary>
        public static float SurvivalDespiteDysfunction(float functionalValue, float ceremonialValue, CeremonialismParams p)
        {
            float fn = Mathf.Clamp01(functionalValue);
            float ce = Mathf.Clamp01(ceremonialValue);
            return Mathf.Clamp01((1f - p.ceremonialSurvivalWeight) * fn + p.ceremonialSurvivalWeight * ce);
        }

        /// <summary>廃止への抵抗（既定 Params）。</summary>
        public static float AbolitionResistance(float ceremonialValue, float vestedInterest)
            => AbolitionResistance(ceremonialValue, vestedInterest, CeremonialismParams.Default);

        /// <summary>
        /// 廃止への抵抗（0..1）＝儀礼的威信＋既得権が盾。(1−vestedWeight)×儀礼的価値 ＋ vestedWeight×既得権。
        /// 格式の高い・既得権の厚い制度ほど、機能を失っていても廃止は難しい（面子と利権が反対する）。
        /// </summary>
        public static float AbolitionResistance(float ceremonialValue, float vestedInterest, CeremonialismParams p)
        {
            float ce = Mathf.Clamp01(ceremonialValue);
            float vi = Mathf.Clamp01(vestedInterest);
            return Mathf.Clamp01((1f - p.vestedWeight) * ce + p.vestedWeight * vi);
        }

        /// <summary>形骸化ドリフトの1tick（既定 Params）。</summary>
        public static float CeremonialDrift(float functionalValue, float ceremonialValue, float dt)
            => CeremonialDrift(functionalValue, ceremonialValue, dt, CeremonialismParams.Default);

        /// <summary>
        /// 形骸化ドリフトの1tick＝実質→形式へのドリフト後の**機能値**を返す（0..1）。
        /// 機能は driftRate×儀礼的価値×dt の割合で目減りする＝儀礼性が高い制度ほど機能が形式に置き換わり速く空洞化する
        /// （前例の維持が目的化し中身が抜ける）。引数非破壊・新しい機能値を返す。dt は年単位・負は0扱い。
        /// </summary>
        public static float CeremonialDrift(float functionalValue, float ceremonialValue, float dt, CeremonialismParams p)
        {
            float fn = Mathf.Clamp01(functionalValue);
            float ce = Mathf.Clamp01(ceremonialValue);
            float drift = p.driftRate * ce * Mathf.Max(0f, dt);
            return Mathf.Clamp01(fn * (1f - Mathf.Clamp01(drift)));
        }

        /// <summary>儀礼維持の空費（既定 Params）。</summary>
        public static float PrestigeUpkeepCost(float ceremonialValue)
            => PrestigeUpkeepCost(ceremonialValue, CeremonialismParams.Default);

        /// <summary>
        /// 儀礼を維持する空費＝儀礼的価値×単価。機能なき格式の維持費（観閲・儀仗・前例の踏襲）。
        /// 機能を生まないのにコストだけがかかる＝形骸化した制度が財政を食む分。
        /// </summary>
        public static float PrestigeUpkeepCost(float ceremonialValue, CeremonialismParams p)
        {
            return Mathf.Clamp01(ceremonialValue) * p.upkeepPerCeremony;
        }

        /// <summary>形骸化した制度か（既定 Params＝閾値0.2）。</summary>
        public static bool IsHollowInstitution(float functionalValue, float ceremonialValue)
            => IsHollowInstitution(functionalValue, ceremonialValue, 0.2f);

        /// <summary>
        /// 中身が空で儀礼だけの制度か＝機能が threshold 未満なのに儀礼的価値が機能を上回る
        /// （実態なき格式が制度を支えている）。機能の抜けた殻＝廃止対象だが <see cref="AbolitionResistance"/> で守られる。
        /// </summary>
        public static bool IsHollowInstitution(float functionalValue, float ceremonialValue, float threshold)
        {
            float fn = Mathf.Clamp01(functionalValue);
            float ce = Mathf.Clamp01(ceremonialValue);
            float th = Mathf.Clamp01(threshold);
            return fn < th && ce > fn;
        }

        /// <summary>改革に要する政治資本（既定 Params）。</summary>
        public static float ReformDifficulty(float abolitionResistance, float politicalCapital)
            => ReformDifficulty(abolitionResistance, politicalCapital, CeremonialismParams.Default);

        /// <summary>
        /// 改革（廃止・縮小）に要する政治資本（0..1）＝抵抗×reformCostScale を持ち手の政治資本で割った負荷。
        /// 抵抗が強いほど・政治資本が乏しいほど改革は遠い（1で改革は政治資本を使い切っても届かない）。
        /// 政治資本0でも抵抗があれば満額（=1）＝威信だけの制度は資本のない政権には手が出せない。
        /// </summary>
        public static float ReformDifficulty(float abolitionResistance, float politicalCapital, CeremonialismParams p)
        {
            float res = Mathf.Clamp01(abolitionResistance);
            float cap = Mathf.Clamp01(politicalCapital);
            float need = res * p.reformCostScale;       // 抵抗に応じた必要政治資本
            if (need <= 0f) return 0f;                   // 抵抗なし＝改革はタダ
            return Mathf.Clamp01(need / (cap + need));   // 資本が必要量に対しどれだけ足りないか（0..1）
        }

        /// <summary>放置による機能衰退の1tick（既定 Params）。</summary>
        public static float FunctionalDecayTick(float functionalValue, float neglect, float dt)
            => FunctionalDecayTick(functionalValue, neglect, dt, CeremonialismParams.Default);

        /// <summary>
        /// 放置で機能が衰える1tick＝機能 × (1 − decayRate×neglect×dt)。
        /// 手入れ（neglect=0）なら機能は保たれ、放置（neglect=1）が続くほど機能が抜けて
        /// やがて <see cref="IsHollowInstitution"/>＝儀礼だけの殻に近づく。引数非破壊・dt 年単位・負は0扱い。
        /// </summary>
        public static float FunctionalDecayTick(float functionalValue, float neglect, float dt, CeremonialismParams p)
        {
            float fn = Mathf.Clamp01(functionalValue);
            float ng = Mathf.Clamp01(neglect);
            float decay = p.decayRate * ng * Mathf.Max(0f, dt);
            return Mathf.Clamp01(fn * (1f - Mathf.Clamp01(decay)));
        }
    }
}
