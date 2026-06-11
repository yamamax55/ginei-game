using UnityEngine;

namespace Ginei
{
    /// <summary>足による投票＝仁政の質に応じた星系間人口移動の調整係数（孟子・MENC-2 #1566）。</summary>
    public readonly struct PopulationMigrationParams
    {
        /// <summary>統治の魅力に占める仁政（善政）の重み。</summary>
        public readonly float benevolenceWeight;
        /// <summary>仁政が隣国の苛政から民を吸引する基礎の流入速度（dt1・最大時）。</summary>
        public readonly float influxRate;
        /// <summary>苛政が民を流出させる基礎の流出速度（dt1・最大時）。</summary>
        public readonly float exodusRate;
        /// <summary>引力最大・移動全自由のとき1tickで動く人口の割合の上限。</summary>
        public readonly float maxFlowRatio;
        /// <summary>好循環＝流入人口を国力ボーナスに換える係数（人が宝）。</summary>
        public readonly float virtuousCycleScale;
        /// <summary>苛政が有能な民を先に失う頭脳流出の偏りの強さ。</summary>
        public readonly float brainDrainBias;
        /// <summary>過疎化と判定する累積流出の閾値（既定）。</summary>
        public readonly float depopulationThreshold;

        public PopulationMigrationParams(float benevolenceWeight, float influxRate, float exodusRate,
                                         float maxFlowRatio, float virtuousCycleScale, float brainDrainBias,
                                         float depopulationThreshold)
        {
            this.benevolenceWeight = Mathf.Clamp01(benevolenceWeight);
            this.influxRate = Mathf.Max(0f, influxRate);
            this.exodusRate = Mathf.Max(0f, exodusRate);
            this.maxFlowRatio = Mathf.Clamp01(maxFlowRatio);
            this.virtuousCycleScale = Mathf.Max(0f, virtuousCycleScale);
            this.brainDrainBias = Mathf.Clamp01(brainDrainBias);
            this.depopulationThreshold = Mathf.Max(0f, depopulationThreshold);
        }

        /// <summary>
        /// 既定＝仁政重み0.6・流入率0.1/秒・流出率0.1/秒・最大流量率0.05・好循環0.3・頭脳偏り0.5・過疎化閾値0.3。
        /// </summary>
        public static PopulationMigrationParams Default =>
            new PopulationMigrationParams(0.6f, 0.1f, 0.1f, 0.05f, 0.3f, 0.5f, 0.3f);
    }

    /// <summary>
    /// 足による投票＝孟子の徳治版（MENC-2 #1566）。「仁政を行えば天下の民がその国に移り住みたがる
    /// （民の帰すること水の下きに就くが如し）」＝<b>苛政の国からは民が逃げ、仁政の国へ民が集まる</b>を式にする。
    /// 統治の質（仁政か苛政か）に応じて星系間で人口が移動する純ロジック：仁政×（1−重税）×治安で統治の魅力を出し
    /// （<see cref="GovernanceAttractiveness"/>）、出身地と移住先の魅力差が移住の引力を生む（<see cref="MigrationPull"/>）。
    /// 仁政は隣国の苛政から民を水のように吸引し（<see cref="BenevolenceInflux"/>＝孟子の徳が民を集める）、
    /// 苛政は民を（特に流動性の高い有能な者から）先に流出させる（<see cref="MisruleExodus"/>／<see cref="BrainDrainFromMisrule"/>）。
    /// 引力勾配×移動の自由が実際の人口移動量を決め（<see cref="PopulationFlow"/>）、民が集まれば国力が増す好循環が回る
    /// （<see cref="VirtuousCycleBonus"/>＝人が宝の人本主義）。苛政で流出が続けば過疎化する（<see cref="IsDepopulating"/>）。
    /// <b>MigrationRules</b>（平時移民の経済的引力＝足で投票の汎用版）とは別＝こちらは仁政の質に応じた徳治版の人口移動。
    /// <b>GovernanceRules</b>（内政の安定度収束）とも別＝統治の質が人口を吸引/流出させる動態を扱う。
    /// <b>GovernanceStyleRules</b>（仁政vs覇道の時間動態・同EPIC MENC）とも分担し、こちらは統治の質→人口の流れ。
    /// 戦火による強制移動（<see cref="RefugeeRules"/>＝追い立てられる難民）とも別系統で、こちらは仁政/苛政への自発的移動。
    /// 全入力クランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PopulationMigrationRules
    {
        /// <summary>
        /// 統治の魅力＝民が住みたい度（0..1）。仁政(0..1)×（1−重税(0..1)）×治安(0..1)。
        /// 仁政重みで仁政だけ底上げし（benevolenceWeight×benevolent ＋ (1−weight)）、重税と治安難で削る＝
        /// <b>仁政・軽税・治安が揃うほど民を惹きつける</b>。重税1（全部召し上げ）や治安0（無法）は魅力を殺す。
        /// </summary>
        public static float GovernanceAttractiveness(float benevolentGovernance, float taxBurden, float security,
                                                     PopulationMigrationParams p)
        {
            float benevolent = Mathf.Clamp01(benevolentGovernance);
            float tax = Mathf.Clamp01(taxBurden);
            float sec = Mathf.Clamp01(security);
            // 仁政を重みで底上げ（重み分は仁政依存・残りは無条件の素地）。
            float govern = p.benevolenceWeight * benevolent + (1f - p.benevolenceWeight);
            return Mathf.Clamp01(govern * (1f - tax) * sec);
        }

        public static float GovernanceAttractiveness(float benevolentGovernance, float taxBurden, float security)
            => GovernanceAttractiveness(benevolentGovernance, taxBurden, security, PopulationMigrationParams.Default);

        /// <summary>
        /// 移住の引力（-1..1）＝移住先と出身地の統治の魅力の差（destination − origin）。
        /// 正＝出身地より移住先が魅力的（出ていきたい＝水が低きに就くように善政へ）・負＝逆流・0＝動機なし。
        /// 民は魅力の高い側へ流れる＝<b>足で投票する</b>。
        /// </summary>
        public static float MigrationPull(float originAttractiveness, float destinationAttractiveness)
        {
            float origin = Mathf.Clamp01(originAttractiveness);
            float dest = Mathf.Clamp01(destinationAttractiveness);
            return Mathf.Clamp(dest - origin, -1f, 1f);
        }

        /// <summary>
        /// 仁政が隣国の苛政から吸引する人口の流入率（0..1＝民の割合）。仁政の質(0..1)×隣国の苛政(0..1)×流入率×dt＝
        /// <b>徳が高く隣が暴政なほど民が水のように流れ込む（孟子＝仁者は敵なし、民が帰す）</b>。
        /// 仁政が低い（自国も苛政）か隣が善政なら吸引できない。
        /// </summary>
        public static float BenevolenceInflux(float governanceQuality, float neighborMisrule, float dt,
                                              PopulationMigrationParams p)
        {
            float quality = Mathf.Clamp01(governanceQuality);
            float misrule = Mathf.Clamp01(neighborMisrule);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(quality * misrule * p.influxRate * step);
        }

        public static float BenevolenceInflux(float governanceQuality, float neighborMisrule, float dt)
            => BenevolenceInflux(governanceQuality, neighborMisrule, dt, PopulationMigrationParams.Default);

        /// <summary>
        /// 苛政が民を流出させる流出率（0..1＝民の割合）。苛政(0..1)×移動の自由(0..1)×流出率×dt＝
        /// <b>暴政が酷いほど・逃げ道があるほど民が逃散する（足で投票して去る）</b>。
        /// 苛政0（善政）なら流出しない。移動の自由0（封鎖）なら逃げられない（が出たい圧は残る＝別系統）。
        /// </summary>
        public static float MisruleExodus(float tyranny, float mobility, float dt, PopulationMigrationParams p)
        {
            float tyr = Mathf.Clamp01(tyranny);
            float mob = Mathf.Clamp01(mobility);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(tyr * mob * p.exodusRate * step);
        }

        public static float MisruleExodus(float tyranny, float mobility, float dt)
            => MisruleExodus(tyranny, mobility, dt, PopulationMigrationParams.Default);

        /// <summary>
        /// 1tick で実際に動く人口の割合（符号付き 0..maxFlowRatio・正は流入・負は流出）。
        /// 引力勾配(-1..1)×移動の自由(openness 0..1)×最大流量率×dt＝<b>引力があっても移動の自由がなければ動けない</b>。
        /// 勾配正なら流入・負なら流出。openness0＝誰も動けない。
        /// </summary>
        public static float PopulationFlow(float pullGradient, float openness, float dt, PopulationMigrationParams p)
        {
            float grad = Mathf.Clamp(pullGradient, -1f, 1f);
            float open = Mathf.Clamp01(openness);
            float step = Mathf.Max(0f, dt);
            float flow = grad * open * p.maxFlowRatio * step;
            return Mathf.Clamp(flow, -p.maxFlowRatio, p.maxFlowRatio);
        }

        public static float PopulationFlow(float pullGradient, float openness, float dt)
            => PopulationFlow(pullGradient, openness, dt, PopulationMigrationParams.Default);

        /// <summary>
        /// 民が集まることで国力が増す好循環ボーナス（&gt;=0）＝流入人口×係数。
        /// <b>仁政で民が集まり、民が集まれば国が富み、さらに民を呼ぶ（人が宝＝孟子の人本主義）</b>。
        /// 流入が負（流出）なら好循環は回らず0。
        /// </summary>
        public static float VirtuousCycleBonus(float populationInflux, PopulationMigrationParams p)
        {
            return Mathf.Max(0f, populationInflux) * p.virtuousCycleScale;
        }

        public static float VirtuousCycleBonus(float populationInflux)
            => VirtuousCycleBonus(populationInflux, PopulationMigrationParams.Default);

        /// <summary>
        /// 苛政が失う民に占める有能層の比率（0..1）。苛政(0..1)×talentMobility(0..1)×偏り＝
        /// <b>暴政では特に有能な民（流動性が高く移れる者）が先に去る（頭脳流出）</b>＝
        /// 残るのは逃げられない者ばかり。苛政0なら有能層も去らない（0）。
        /// </summary>
        public static float BrainDrainFromMisrule(float tyranny, float talentMobility, PopulationMigrationParams p)
        {
            float tyr = Mathf.Clamp01(tyranny);
            float mob = Mathf.Clamp01(talentMobility);
            return Mathf.Clamp01(tyr * mob * p.brainDrainBias);
        }

        public static float BrainDrainFromMisrule(float tyranny, float talentMobility)
            => BrainDrainFromMisrule(tyranny, talentMobility, PopulationMigrationParams.Default);

        /// <summary>
        /// 苛政で人口が流出し続け過疎化する判定（true＝過疎化）。累積流出(misruleExodus)が閾値を超えたら
        /// <b>民が逃げ続けて国が空になる＝苛政の末路</b>。閾値以下ならまだ持ちこたえている。
        /// </summary>
        public static bool IsDepopulating(float misruleExodus, float threshold)
        {
            return Mathf.Max(0f, misruleExodus) > Mathf.Max(0f, threshold);
        }

        public static bool IsDepopulating(float misruleExodus)
            => IsDepopulating(misruleExodus, PopulationMigrationParams.Default.depopulationThreshold);
    }
}
