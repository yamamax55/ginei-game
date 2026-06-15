using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 官僚制肥大＝パーキンソンの法則の純ロジック（唯一の窓口・test-first）。
    /// 定員は仕事量と無関係に年率で自己増殖し（<see cref="HeadcountTick"/>＝部下を増やすことが地位）、
    /// 人が増えるほど組織内調整＝会議が二乗的に増え（<see cref="AdminOverheadRatio"/>）、
    /// 実効産出はある規模を境に増員が逆効果になる山なりカーブを描く（<see cref="EffectiveOutput"/>）。
    /// 適正規模は解析値で出せるが（<see cref="OptimalHeadcount"/>）、肥大した組織ほど・前回の行革が遠いほど
    /// 改革に抵抗し（<see cref="ReformResistance"/>）、行革は削減と一時的混乱のトレードオフ（<see cref="ReformEffect"/>）
    /// ＝**組織は仕事のためでなく組織のために育つ・定期的な行革が要る**。
    /// 分担：`MinistryRules`＝省庁の編制ツリー（構造・大臣ポスト・配属台帳）／**本クラス＝人数の動態**
    /// （定員の自己増殖・管理コスト・行革。どの省に何人かは扱わない）。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（新しい値を返す）。調整値は <see cref="BureaucracyBloatParams"/>
    /// （既定 <see cref="BureaucracyBloatParams.Default"/>）。
    /// </summary>
    public static class BureaucracyBloatRules
    {
        /// <summary>官僚制肥大の調整値（増殖率・調整コスト感度・行革の効きと混乱）。ctor で全てクランプ。</summary>
        public readonly struct BureaucracyBloatParams
        {
            /// <summary>定員の自己増殖率（/年。パーキンソンの実測は5〜7%＝仕事量と無関係）。</summary>
            public readonly float growthRate;
            /// <summary>調整オーバーヘッド感度（(定員/仕事量)² に乗算＝会議は二乗的に増える）。</summary>
            public readonly float overheadScale;
            /// <summary>1人あたりの実務産出（仕事量と同じ単位/人）。</summary>
            public readonly float outputPerHead;
            /// <summary>行革強度1での最大削減割合（0..1。例 0.5＝最大で半減）。</summary>
            public readonly float maxCutRatio;
            /// <summary>行革強度→一時的混乱の感度（強く切るほど現場が止まる）。</summary>
            public readonly float disruptionScale;
            /// <summary>改革抵抗が0.5に達する定員（組織規模の飽和半値。≥1）。</summary>
            public readonly float resistanceHalfHeadcount;
            /// <summary>改革抵抗が満額に固まるまでの年数（行革からの経過で硬直化）。</summary>
            public readonly float ossifyYears;

            public BureaucracyBloatParams(
                float growthRate, float overheadScale, float outputPerHead, float maxCutRatio,
                float disruptionScale, float resistanceHalfHeadcount, float ossifyYears)
            {
                this.growthRate = Mathf.Max(0f, growthRate);
                this.overheadScale = Mathf.Max(0.0001f, overheadScale);     // 0だと適正規模が発散
                this.outputPerHead = Mathf.Max(0.0001f, outputPerHead);
                this.maxCutRatio = Mathf.Clamp01(maxCutRatio);
                this.disruptionScale = Mathf.Max(0f, disruptionScale);
                this.resistanceHalfHeadcount = Mathf.Max(1f, resistanceHalfHeadcount);
                this.ossifyYears = Mathf.Max(0.1f, ossifyYears);
            }

            /// <summary>
            /// 既定＝増殖率0.06/年（パーキンソン実測5〜7%）・調整感度1/3（適正規模＝仕事量1:1）・
            /// 1人産出1・最大削減0.5・混乱感度0.6・抵抗半値定員100・硬直化20年。
            /// </summary>
            public static BureaucracyBloatParams Default
                => new BureaucracyBloatParams(0.06f, 1f / 3f, 1f, 0.5f, 0.6f, 100f, 20f);
        }

        /// <summary>行革の結果（削減後の定員＋一時的混乱）。トレードオフを一括で返す。</summary>
        public readonly struct ReformResult
        {
            /// <summary>削減後の定員。</summary>
            public readonly float newHeadcount;
            /// <summary>一時的混乱（0..1。呼び出し側が産出・安定#109へ一時係数として掛ける想定）。</summary>
            public readonly float disruption;

            public ReformResult(float newHeadcount, float disruption)
            {
                this.newHeadcount = Mathf.Max(0f, newHeadcount);
                this.disruption = Mathf.Clamp01(disruption);
            }
        }

        /// <summary>定員の1tick更新（既定 Params）。</summary>
        public static float HeadcountTick(float headcount, float workload, float dt)
            => HeadcountTick(headcount, workload, dt, BureaucracyBloatParams.Default);

        /// <summary>
        /// 定員の1tick自己増殖＝**パーキンソン第一法則**。新定員＝定員×(1＋growthRate×dt)。
        /// workload は受け取るが**意図的に使わない**＝仕事量が増えても減ってもゼロでも定員は同率で育つ
        /// （部下を増やすことが地位＝組織は組織のために育つ）。dt は年単位・負は0扱い。新しい定員を返す（引数非破壊）。
        /// </summary>
        public static float HeadcountTick(float headcount, float workload, float dt, BureaucracyBloatParams p)
        {
            _ = workload; // 仕事量と無関係＝それがパーキンソンの法則
            float n = Mathf.Max(0f, headcount);
            return n * (1f + p.growthRate * Mathf.Max(0f, dt));
        }

        /// <summary>管理オーバーヘッド比（既定 Params）。</summary>
        public static float AdminOverheadRatio(float headcount, float workload)
            => AdminOverheadRatio(headcount, workload, BureaucracyBloatParams.Default);

        /// <summary>
        /// 管理オーバーヘッド比（0..1）＝実務に対する組織内調整の割合。overheadScale×(定員/仕事量)²
        /// ＝連絡経路は人数の二乗で増える（人が増えるほど会議が増える）。仕事量0以下でも定員がいれば1
        /// ＝仕事が無くても組織は回り続け全てが内向きの調整になる。
        /// </summary>
        public static float AdminOverheadRatio(float headcount, float workload, BureaucracyBloatParams p)
        {
            float n = Mathf.Max(0f, headcount);
            if (n <= 0f) return 0f;
            if (workload <= 0f) return 1f; // 仕事ゼロ＝全部オーバーヘッド
            float ratio = n / workload;
            return Mathf.Clamp01(p.overheadScale * ratio * ratio);
        }

        /// <summary>実効産出（既定 Params）。</summary>
        public static float EffectiveOutput(float headcount, float workload)
            => EffectiveOutput(headcount, workload, BureaucracyBloatParams.Default);

        /// <summary>
        /// 実効産出＝min(定員×1人産出, 仕事量)×(1−管理オーバーヘッド比)＝**山なりカーブ**。
        /// 適正規模（<see cref="OptimalHeadcount(float)"/>）までは増員が効くが、ピークを過ぎると
        /// 調整コストが実務を圧迫して逆に下がり、肥大の極みでは0（全員が会議をしている）。
        /// 仕事量0以下は0。呼び出し側が生産#93/行政効率の係数として消費する想定（基準非破壊）。
        /// </summary>
        public static float EffectiveOutput(float headcount, float workload, BureaucracyBloatParams p)
        {
            float n = Mathf.Max(0f, headcount);
            float w = Mathf.Max(0f, workload);
            if (n <= 0f || w <= 0f) return 0f;
            float gross = Mathf.Min(n * p.outputPerHead, w); // 実務は仕事量を超えて湧かない
            return gross * (1f - AdminOverheadRatio(n, w, p));
        }

        /// <summary>適正規模（既定 Params）。</summary>
        public static float OptimalHeadcount(float workload)
            => OptimalHeadcount(workload, BureaucracyBloatParams.Default);

        /// <summary>
        /// 適正規模の解析値＝実効産出を最大にする定員。仕事量×min(1/outputPerHead, 1/√(3×overheadScale))。
        /// 既定 Params では**仕事量と同数（1人1仕事）**＝これを超える定員は産出を下げるだけの肥大。
        /// 行革の削減目標として使う想定。仕事量0以下は0。
        /// </summary>
        public static float OptimalHeadcount(float workload, BureaucracyBloatParams p)
        {
            float w = Mathf.Max(0f, workload);
            float byCap = 1f / p.outputPerHead;                       // 実務が仕事量で頭打ちになる点
            float byOverhead = 1f / Mathf.Sqrt(3f * p.overheadScale); // 調整コストの限界が増員益と釣り合う点
            return w * Mathf.Min(byCap, byOverhead);
        }

        /// <summary>行革への抵抗（既定 Params）。</summary>
        public static float ReformResistance(float headcount, float yearsSinceReform)
            => ReformResistance(headcount, yearsSinceReform, BureaucracyBloatParams.Default);

        /// <summary>
        /// 行革への抵抗（0..1）＝組織規模の飽和項（定員/(定員＋半値)）×硬直化項（経過年/ossifyYears、上限1）。
        /// 肥大した組織ほど・前回の行革が遠いほど固く、行革直後は柔らかい（抵抗0）＝**定期的な行革が安い**。
        /// 呼び出し側が行革強度に (1−resistance) を掛けて実効強度を出す想定（基準非破壊）。
        /// </summary>
        public static float ReformResistance(float headcount, float yearsSinceReform, BureaucracyBloatParams p)
        {
            float n = Mathf.Max(0f, headcount);
            float size = n / (n + p.resistanceHalfHeadcount);
            float ossify = Mathf.Clamp01(Mathf.Max(0f, yearsSinceReform) / p.ossifyYears);
            return Mathf.Clamp01(size * ossify);
        }

        /// <summary>行革の実行（既定 Params）。</summary>
        public static ReformResult ReformEffect(float headcount, float reformIntensity)
            => ReformEffect(headcount, reformIntensity, BureaucracyBloatParams.Default);

        /// <summary>
        /// 行革の実行＝削減と混乱のトレードオフ。強度（0..1）に応じて定員を maxCutRatio×強度ぶん削減し、
        /// 同時に disruptionScale×強度の一時的混乱を返す（強く切るほど現場が止まる）。
        /// 抵抗は別計算（<see cref="ReformResistance(float,float)"/>）＝呼び出し側が強度に (1−抵抗) を
        /// 掛けてから渡す想定。引数非破壊・新しい定員を <see cref="ReformResult"/> で返す。
        /// </summary>
        public static ReformResult ReformEffect(float headcount, float reformIntensity, BureaucracyBloatParams p)
        {
            float n = Mathf.Max(0f, headcount);
            float intensity = Mathf.Clamp01(reformIntensity);
            float cut = p.maxCutRatio * intensity;
            return new ReformResult(n * (1f - cut), intensity * p.disruptionScale);
        }
    }
}
