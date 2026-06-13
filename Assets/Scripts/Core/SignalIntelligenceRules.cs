using UnityEngine;

namespace Ginei
{
    /// <summary>通信傍受・信号諜報（SIGINT）の調整係数。マジックナンバー禁止＝ここに集約。全フィールド ctor で Clamp。</summary>
    public readonly struct SignalIntelligenceParams
    {
        /// <summary>敵電波量×傍受網から実際に傍受できる量への倍率（0..1）。</summary>
        public readonly float interceptScale;
        /// <summary>解読量を事前察知度へ写すときの基準倍率（0..1）。</summary>
        public readonly float forewarnScale;
        /// <summary>敵テンポが事前察知の価値に効く重み（0..1・テンポが速い敵の意図を読むほど価値が大きい）。</summary>
        public readonly float tempoWeight;
        /// <summary>欺瞞通信に騙されるリスクの最大幅（0..1・読んでいる通信ほど偽情報を掴まされる）。</summary>
        public readonly float deceptionWeight;
        /// <summary>正味諜報価値を先手有利へ写すときの基準倍率（0..1）。</summary>
        public readonly float preemptScale;
        /// <summary>自軍の通信秘匿（被傍受抑制）における通信統制（EMCON）の重み（0..1・残りは暗号強度）。</summary>
        public readonly float emconWeight;
        /// <summary>自軍通信が筒抜けと判定する解読率の既定閾値（0..1）。</summary>
        public readonly float compromiseThreshold;

        public SignalIntelligenceParams(float interceptScale, float forewarnScale, float tempoWeight,
                                        float deceptionWeight, float preemptScale, float emconWeight,
                                        float compromiseThreshold)
        {
            this.interceptScale = Mathf.Clamp01(interceptScale);
            this.forewarnScale = Mathf.Clamp01(forewarnScale);
            this.tempoWeight = Mathf.Clamp01(tempoWeight);
            this.deceptionWeight = Mathf.Clamp01(deceptionWeight);
            this.preemptScale = Mathf.Clamp01(preemptScale);
            this.emconWeight = Mathf.Clamp01(emconWeight);
            this.compromiseThreshold = Mathf.Clamp01(compromiseThreshold);
        }

        /// <summary>
        /// 既定＝傍受倍率1.0/事前察知倍率1.0/テンポ重み0.5/欺瞞重み0.7/先手倍率1.0/EMCON重み0.6/筒抜け閾値0.5。
        /// </summary>
        public static SignalIntelligenceParams Default =>
            new SignalIntelligenceParams(1f, 1f, 0.5f, 0.7f, 1f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 通信傍受・信号諜報（SIGINT）の純ロジック。敵の通信を傍受・解析すると動きを事前に察知し先手を打てる。
    /// だが暗号化で読みにくくなり、欺瞞通信（偽情報）に騙されるリスクもある＝読めば読むほど偽を掴まされうる。
    /// <see cref="ReconRules"/>/<see cref="FogOfWarRules"/>（物理的な可視性・斥候の目視）とは別レイヤー＝
    /// こちらは通信由来の諜報（電波・暗号・欺瞞）。本ルールは欺瞞リスク込みで正味の諜報価値を出す唯一の窓口。
    /// 盤面非依存の plain 引数。値は徹底して 0..1 に clamp・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SignalIntelligenceRules
    {
        /// <summary>
        /// 傍受量 0..1：敵の電波量 emissionLevel × 自軍の傍受網カバレッジ listeningCoverage。
        /// 敵が無線封止していれば（emission低）拾えず、傍受網が薄ければ（coverage低）取りこぼす。
        /// </summary>
        public static float Interception(float emissionLevel, float listeningCoverage, SignalIntelligenceParams p)
        {
            float e = Mathf.Clamp01(emissionLevel);
            float c = Mathf.Clamp01(listeningCoverage);
            return Mathf.Clamp01(e * c * p.interceptScale);
        }

        public static float Interception(float emissionLevel, float listeningCoverage)
            => Interception(emissionLevel, listeningCoverage, SignalIntelligenceParams.Default);

        /// <summary>
        /// 解読量 0..1：傍受した通信のうち暗号を破って読み解ける割合。暗号強度 cipherStrength が高いほど読めない。
        /// interception × (1 − cipherStrength)。Params 非依存（純粋な内訳）。
        /// </summary>
        public static float Decryption(float interception, float cipherStrength)
        {
            float i = Mathf.Clamp01(interception);
            float cipher = Mathf.Clamp01(cipherStrength);
            return Mathf.Clamp01(i * (1f - cipher));
        }

        /// <summary>
        /// 事前察知度 0..1：解読量から敵の次の行動をどれだけ先読みできるか。敵テンポ enemyTempo が速いほど
        /// 同じ解読でも先読みの価値が増す（速い敵の意図を読めるのは大きい）。
        /// decryption × (1 − tempoWeight + tempoWeight × enemyTempo) × forewarnScale。
        /// </summary>
        public static float Forewarning(float decryption, float enemyTempo, SignalIntelligenceParams p)
        {
            float d = Mathf.Clamp01(decryption);
            float tempo = Mathf.Clamp01(enemyTempo);
            float tempoFactor = (1f - p.tempoWeight) + p.tempoWeight * tempo;
            return Mathf.Clamp01(d * tempoFactor * p.forewarnScale);
        }

        public static float Forewarning(float decryption, float enemyTempo)
            => Forewarning(decryption, enemyTempo, SignalIntelligenceParams.Default);

        /// <summary>
        /// 欺瞞被害リスク 0..1：敵の欺瞞通信（偽情報）に騙される度合い。解読している通信ほど（decryption高）
        /// 仕込まれた偽情報を掴まされる＝読まなければ騙されない。decryption × enemyDeceptionEffort × deceptionWeight。
        /// </summary>
        public static float DeceptionRisk(float decryption, float enemyDeceptionEffort, SignalIntelligenceParams p)
        {
            float d = Mathf.Clamp01(decryption);
            float effort = Mathf.Clamp01(enemyDeceptionEffort);
            return Mathf.Clamp01(d * effort * p.deceptionWeight);
        }

        public static float DeceptionRisk(float decryption, float enemyDeceptionEffort)
            => DeceptionRisk(decryption, enemyDeceptionEffort, SignalIntelligenceParams.Default);

        /// <summary>
        /// 正味諜報価値 0..1：事前察知度から欺瞞被害リスクを差し引いた、信頼できる諜報の価値。
        /// forewarning × (1 − deceptionRisk)。偽情報リスクが高いと察知できても価値が割り引かれる。Params 非依存。
        /// </summary>
        public static float NetIntelValue(float forewarning, float deceptionRisk)
        {
            float f = Mathf.Clamp01(forewarning);
            float risk = Mathf.Clamp01(deceptionRisk);
            return Mathf.Clamp01(f * (1f - risk));
        }

        /// <summary>
        /// 自軍通信秘匿 0..1：自軍が傍受されにくくする度合い。暗号強度 cipherStrength と通信統制 emconDiscipline
        /// （無線封止・発信規律）の加重和。emconWeight ぶんを EMCON、残りを暗号が担う。
        /// </summary>
        public static float CounterIntelHardening(float cipherStrength, float emconDiscipline, SignalIntelligenceParams p)
        {
            float cipher = Mathf.Clamp01(cipherStrength);
            float emcon = Mathf.Clamp01(emconDiscipline);
            return Mathf.Clamp01(cipher * (1f - p.emconWeight) + emcon * p.emconWeight);
        }

        public static float CounterIntelHardening(float cipherStrength, float emconDiscipline)
            => CounterIntelHardening(cipherStrength, emconDiscipline, SignalIntelligenceParams.Default);

        /// <summary>
        /// 先手有利 0..1：正味諜報価値を実際の行動に変える有利。反応速度 reactionSpeed が速いほど察知を機動へ転化できる。
        /// 知っていても動きが鈍ければ先手を取れない。netIntelValue × reactionSpeed × preemptScale。
        /// </summary>
        public static float PreemptiveAdvantage(float netIntelValue, float reactionSpeed, SignalIntelligenceParams p)
        {
            float net = Mathf.Clamp01(netIntelValue);
            float reaction = Mathf.Clamp01(reactionSpeed);
            return Mathf.Clamp01(net * reaction * p.preemptScale);
        }

        public static float PreemptiveAdvantage(float netIntelValue, float reactionSpeed)
            => PreemptiveAdvantage(netIntelValue, reactionSpeed, SignalIntelligenceParams.Default);

        /// <summary>
        /// 筒抜け判定：解読率 decryption が threshold 以上なら通信が筒抜け（true）。視点を変え、自軍の通信が
        /// 敵に解読されている度合いとして使えば「自軍が筒抜けか」、敵通信なら「敵を読み切ったか」の判定になる。
        /// </summary>
        public static bool IsCompromised(float decryption, float threshold)
        {
            return Mathf.Clamp01(decryption) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="SignalIntelligenceParams.compromiseThreshold"/>）での筒抜け判定。</summary>
        public static bool IsCompromised(float decryption)
            => IsCompromised(decryption, SignalIntelligenceParams.Default.compromiseThreshold);
    }
}
