using UnityEngine;

namespace Ginei
{
    /// <summary>未知宙域探査の調整係数（G-2 #119 戦略版）。</summary>
    public readonly struct ExplorationParams
    {
        /// <summary>探査の基準速度（能力1.0×平易な宙域での進捗/時間）。</summary>
        public readonly float surveyRate;
        /// <summary>能力ゼロでも出る最低速度の割合（能力係数の下限＝雑な観測でも白地は少しずつ埋まる）。</summary>
        public readonly float minCapabilityFactor;
        /// <summary>難所による減速の強さ（difficulty=1 で速度が 1-difficultyPenalty 倍まで落ちる）。</summary>
        public readonly float difficultyPenalty;
        /// <summary>探索済みと見なす進捗閾値（progress がこれ以上で surveyed）。</summary>
        public readonly float surveyedThreshold;
        /// <summary>発見イベントの基礎率（progress=1 の不毛宙域でもこれだけは出る）。</summary>
        public readonly float baseDiscoveryChance;
        /// <summary>宙域の豊かさが発見率に足す重み（richness=1 で基礎率＋これ）。</summary>
        public readonly float richnessWeight;
        /// <summary>探査喪失リスクの上限（difficulty=1×能力0 の最悪ケースの喪失率）。</summary>
        public readonly float maxHazard;
        /// <summary>探査データの陳腐化速度（猶予超過1年あたりの進捗減/時間）。</summary>
        public readonly float decayRate;
        /// <summary>陳腐化が始まるまでの猶予（年。これ以内の新鮮なデータは劣化しない）。</summary>
        public readonly float decayGraceYears;

        public ExplorationParams(float surveyRate, float minCapabilityFactor, float difficultyPenalty,
            float surveyedThreshold, float baseDiscoveryChance, float richnessWeight,
            float maxHazard, float decayRate, float decayGraceYears)
        {
            this.surveyRate = Mathf.Max(0f, surveyRate);
            this.minCapabilityFactor = Mathf.Clamp01(minCapabilityFactor);
            this.difficultyPenalty = Mathf.Clamp01(difficultyPenalty);
            this.surveyedThreshold = Mathf.Clamp01(surveyedThreshold);
            this.baseDiscoveryChance = Mathf.Clamp01(baseDiscoveryChance);
            this.richnessWeight = Mathf.Clamp01(richnessWeight);
            this.maxHazard = Mathf.Clamp01(maxHazard);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.decayGraceYears = Mathf.Max(0f, decayGraceYears);
        }

        /// <summary>既定＝探査速度0.1・能力下限0.2・難所減速0.8・探索済み閾値1.0・基礎発見0.05・豊かさ重み0.45・最悪喪失0.3・陳腐化0.005・猶予1年。</summary>
        public static ExplorationParams Default
            => new ExplorationParams(0.1f, 0.2f, 0.8f, 1f, 0.05f, 0.45f, 0.3f, 0.005f, 1f);
    }

    /// <summary>
    /// 未知宙域探査の純ロジック（G-2 #119 の戦略版＝偵察艦 <see cref="ShipRole.偵察艦"/> の固有機能）。
    /// 未探索星系の発見進捗（progress 0..1）を探査艦の能力（scoutCapability）と宙域の難しさ
    /// （sectorDifficulty）で進め、探索済み判定 <see cref="IsSurveyed(float, ExplorationParams)"/> が
    /// <see cref="ColonizationRules.CanColonize(StarSystem, bool)"/> の explored 引数の<b>供給源</b>になる。
    /// 「地図の白地は無ではなく未知＝埋めた者が先に選べる」を式に出す：発見イベント（居住可能星系・
    /// 資源・遺物の母数）は進捗×豊かさで増え、難所×低能力は帰らぬ探査艦（喪失リスク）、
    /// 古い地図は陳腐化して再探査が要る。分担＝<see cref="ReconRules"/> は会戦の霧（戦術＝敵戦力の推定）、
    /// <see cref="ColonizationRules"/> は探索<b>後</b>の入植（こちらが explored を作り、入植はその先）。
    /// 乱数なし＝判定は外から与える roll で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ExplorationRules
    {
        /// <summary>
        /// 探査の1tick後の進捗（0..1）。能力が高いほど速く（下限 minCapabilityFactor＝能力ゼロでも
        /// 白地は少しずつ埋まる）、難所（sectorDifficulty）ほど遅い（difficultyPenalty で減速）。
        /// </summary>
        public static float SurveyTick(float progress, float scoutCapability, float sectorDifficulty, float dt, ExplorationParams p)
        {
            float prog = Mathf.Clamp01(progress);
            float cap = Mathf.Clamp01(scoutCapability);
            float diff = Mathf.Clamp01(sectorDifficulty);
            float time = Mathf.Max(0f, dt);
            float capabilityFactor = p.minCapabilityFactor + (1f - p.minCapabilityFactor) * cap;
            float difficultyFactor = 1f - p.difficultyPenalty * diff;
            return Mathf.Clamp01(prog + p.surveyRate * capabilityFactor * difficultyFactor * time);
        }

        public static float SurveyTick(float progress, float scoutCapability, float sectorDifficulty, float dt)
            => SurveyTick(progress, scoutCapability, sectorDifficulty, dt, ExplorationParams.Default);

        /// <summary>
        /// 探索済みか＝進捗が閾値（surveyedThreshold）以上。
        /// <see cref="ColonizationRules.CanColonize(StarSystem, bool)"/> の explored 引数へ渡す想定の供給源。
        /// </summary>
        public static bool IsSurveyed(float progress, ExplorationParams p)
        {
            return Mathf.Clamp01(progress) >= p.surveyedThreshold;
        }

        public static bool IsSurveyed(float progress) => IsSurveyed(progress, ExplorationParams.Default);

        /// <summary>
        /// 発見イベント率（0..1）。居住可能星系・資源・遺物などの発見の母数＝
        /// （基礎率＋豊かさ×重み）×進捗。まだ見ていない宙域（progress=0）からは何も出ず、
        /// 豊かな宙域ほど・深く調べるほど見つかる＝埋めた者が先に選べる。
        /// </summary>
        public static float DiscoveryChance(float progress, float sectorRichness, ExplorationParams p)
        {
            float prog = Mathf.Clamp01(progress);
            float rich = Mathf.Clamp01(sectorRichness);
            return Mathf.Clamp01((p.baseDiscoveryChance + p.richnessWeight * rich) * prog);
        }

        public static float DiscoveryChance(float progress, float sectorRichness)
            => DiscoveryChance(progress, sectorRichness, ExplorationParams.Default);

        /// <summary>発見の決定論判定（roll は [0,1) を外から与える＝同じ入力なら同じ結果）。</summary>
        public static bool Discovers(float chance, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 探査喪失リスク（0..1）＝難所×（1−能力）×上限。難所へ低能力の探査艦を出すと帰らない。
        /// 熟練（能力1）はどんな難所でも喪失ゼロ、平易な宙域（difficulty=0）も喪失ゼロ。
        /// 判定は <see cref="Discovers(float, float)"/> と同じく roll との比較で決定論に行う想定。
        /// </summary>
        public static float HazardChance(float sectorDifficulty, float scoutCapability, ExplorationParams p)
        {
            float diff = Mathf.Clamp01(sectorDifficulty);
            float cap = Mathf.Clamp01(scoutCapability);
            return Mathf.Clamp01(p.maxHazard * diff * (1f - cap));
        }

        public static float HazardChance(float sectorDifficulty, float scoutCapability)
            => HazardChance(sectorDifficulty, scoutCapability, ExplorationParams.Default);

        /// <summary>
        /// 古い探査データの陳腐化の1tick（宙域は変わる＝再探査の必要）。最終訪問からの経過年が
        /// 猶予（decayGraceYears）以内なら新鮮＝劣化しない。超過分×陳腐化速度×dt のぶん進捗が
        /// 巻き戻る（古い地図ほど速く腐る・0で下限クランプ）＝探索済みが未探索へ戻りうる。
        /// </summary>
        public static float SurveyDecayTick(float progress, float yearsSinceVisit, float dt, ExplorationParams p)
        {
            float prog = Mathf.Clamp01(progress);
            float stale = Mathf.Max(0f, yearsSinceVisit) - p.decayGraceYears;
            if (stale <= 0f) return prog;
            return Mathf.Clamp01(prog - p.decayRate * stale * Mathf.Max(0f, dt));
        }

        public static float SurveyDecayTick(float progress, float yearsSinceVisit, float dt)
            => SurveyDecayTick(progress, yearsSinceVisit, dt, ExplorationParams.Default);

        /// <summary>
        /// 探索済み比率（surveyedShare 0..1）の戦略価値（0..1）＝√share の逓減カーブ。
        /// 最初の地図ほど価値が大きい（白地だらけの中の一枚は選択肢を倍増させる）＝
        /// 未知は機会でもリスクでもあり、先に埋めた者が入植先・資源・航路を先に選べる。
        /// </summary>
        public static float FrontierValue(float surveyedShare)
        {
            return Mathf.Sqrt(Mathf.Clamp01(surveyedShare));
        }
    }
}
