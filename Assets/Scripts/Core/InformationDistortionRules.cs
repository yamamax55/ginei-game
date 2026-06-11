using UnityEngine;

namespace Ginei
{
    /// <summary>階層的情報歪曲（悪報の圧縮・大本営発表）の調整係数。</summary>
    public readonly struct InformationDistortionParams
    {
        /// <summary>1階層ごとの悪報圧縮の基礎率（責任回避・面子が無くてもこれだけは削られる）。</summary>
        public readonly float baseCompressionPerLevel;
        /// <summary>責任回避が圧縮を増幅する最大寄与（上官に悪報を出したくない圧力）。</summary>
        public readonly float blameAvoidanceWeight;
        /// <summary>歪みが累積する速度（per dt・報告が階層を上るうちに歪みが膨らむ）。</summary>
        public readonly float distortionAccumRate;
        /// <summary>認識ギャップが破裂するとみなす既定しきい値（乖離がこれ以上で崩壊しうる）。</summary>
        public readonly float ruptureThreshold;
        /// <summary>幻想の指揮とみなす既定しきい値（認識と現実の乖離がこれ以上で妄想指揮）。</summary>
        public readonly float delusionThreshold;

        public InformationDistortionParams(float baseCompressionPerLevel, float blameAvoidanceWeight,
                                           float distortionAccumRate, float ruptureThreshold, float delusionThreshold)
        {
            this.baseCompressionPerLevel = Mathf.Clamp01(baseCompressionPerLevel);
            this.blameAvoidanceWeight = Mathf.Max(0f, blameAvoidanceWeight);
            this.distortionAccumRate = Mathf.Max(0f, distortionAccumRate);
            this.ruptureThreshold = Mathf.Clamp01(ruptureThreshold);
            this.delusionThreshold = Mathf.Clamp01(delusionThreshold);
        }

        /// <summary>既定＝基礎圧縮0.2/責任回避重み0.5/累積速度0.1/破裂閾0.6/妄想閾0.5。</summary>
        public static InformationDistortionParams Default
            => new InformationDistortionParams(0.2f, 0.5f, 0.1f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 階層的情報歪曲の純ロジック＝『失敗の本質』型（#1383・SHP-4）。
    /// 「組織の階層を上るほど悪い報告（悪報）が圧縮・希薄化され、各階層で都合の悪い真実が削られて
    /// トップに届く頃には現実と乖離する＝下級者は上官に都合の悪い報告をしたくないため報告がポジティブに
    /// 歪み、トップは『勝っている』と信じたまま現実が崩壊し、ある時点で認識ギャップが破裂する（大本営発表）」。
    /// 悪い報告が罰される組織ほど歪み、悪報を罰しない心理的安全が正確な報告を通す。
    /// <see cref="AdvisorCandorRules"/>（佞臣＝君主周辺の追従フィルター・真実が君主に届くか）／
    /// <see cref="CommunicationsRules"/>（指揮の遅延＝距離と妨害で命令が腐る）とは別系統＝こちらは
    /// 「階層を上るほど悪報が薄まる垂直方向の歪み（階層的情報歪曲）」を扱う。組織の空気そのものは
    /// <see cref="AtmosphereRules"/>（空気・同EPIC SHP）／前線と銃後の認識差は
    /// <see cref="HomeFrontRules"/>（前線銃後の乖離）が担当＝本クラスは報告が上るほど歪む機構だけを扱う。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InformationDistortionRules
    {
        // ===== 悪報の圧縮（階層を上るごとに薄まる） =====

        /// <summary>
        /// 悪報の圧縮率（0..1）＝悪報(badNews 0..1)が階層(hierarchyLevels 0..1＝階層の深さ)を上るごとに
        /// 圧縮・希薄化される。階層が多く責任回避(blameAvoidance 0..1)が強いほど、各階層で都合の悪い真実が
        /// 削られてトップに届く悪報が薄まる＝戻り値はトップに残る悪報の割合（0なら全消し・1なら無傷で到達）。
        /// </summary>
        public static float BadNewsCompression(float badNews, float hierarchyLevels, float blameAvoidance,
                                               InformationDistortionParams p)
        {
            float news = Mathf.Clamp01(badNews);
            float levels = Mathf.Clamp01(hierarchyLevels);
            float blame = Mathf.Clamp01(blameAvoidance);
            // 1階層あたりの削り＝基礎圧縮＋責任回避ぶん（上官に悪報を出したくない圧力で多く削れる）。
            float perLevelLoss = Mathf.Clamp01(p.baseCompressionPerLevel + blame * p.blameAvoidanceWeight);
            // 階層を上るほど指数的に薄まる（各階層が同率で削る連鎖）。
            float retention = Mathf.Pow(1f - perLevelLoss, levels * MaxLevels);
            // トップに残る悪報＝元の悪報×残存率（残った悪報の量を返す）。
            return Mathf.Clamp01(news * retention);
        }

        public static float BadNewsCompression(float badNews, float hierarchyLevels, float blameAvoidance)
            => BadNewsCompression(badNews, hierarchyLevels, blameAvoidance, InformationDistortionParams.Default);

        /// <summary>正規化された hierarchyLevels(0..1) を実効的な階層数へ写すスケール（深さ1で約5階層相当）。</summary>
        private const float MaxLevels = 5f;

        // ===== 1階層ごとの歪み（上に出したくない圧力） =====

        /// <summary>
        /// 1階層ごとの歪み（0..1）＝報告の否定性(reportNegativity 0..1＝悪い報告ほど大）が、上官に悪い報告を
        /// したくない圧力(upwardPressure 0..1)でポジティブ方向に歪む。否定的な報告ほど・上申圧力が強いほど
        /// 大きく歪む＝悪い報告だけが選択的に楽観へ書き換えられる。
        /// </summary>
        public static float DistortionPerLevel(float reportNegativity, float upwardPressure)
        {
            float negativity = Mathf.Clamp01(reportNegativity);
            float pressure = Mathf.Clamp01(upwardPressure);
            // 悪い報告ほど（負の内容ほど）上官に出しにくく、圧力ぶんポジティブに盛られる。
            return Mathf.Clamp01(negativity * pressure);
        }

        // ===== 認識と現実の乖離（上に行くほど離れる） =====

        /// <summary>
        /// トップが認識する状況と現実の乖離（0..1）＝累積歪み(cumulativeDistortion 0..1)のぶんだけ、トップの
        /// 認識が現実(groundTruth 0..1＝実際の戦況・高いほど良好)から楽観方向へ離れる。乖離＝認識−現実の絶対量。
        /// 歪み0なら乖離0（認識＝現実）・歪み1なら現実がどうあれ最大に乖離＝上に行くほど現実から離れる。
        /// </summary>
        public static float PerceivedVsReality(float groundTruth, float cumulativeDistortion)
        {
            float truth = Mathf.Clamp01(groundTruth);
            float distortion = Mathf.Clamp01(cumulativeDistortion);
            // 認識＝現実を歪みのぶんだけ楽観（=1方向）へ持ち上げた値。
            float perceived = truth + (1f - truth) * distortion;
            // 乖離＝楽観に持ち上げられた量（認識−現実）。
            return Mathf.Clamp01(perceived - truth);
        }

        // ===== 歪みの累積（階層を上るうちに膨らむ） =====

        /// <summary>
        /// 歪みの累積（0..1の時間発展）＝報告が階層を上る(levelsTraversed 0..1＝この区間で上った階層の量)うちに
        /// 歪みが積み上がる。上った階層が多いほど速く累積し、1へ漸近する＝報告が長い指揮系統を上るほど歪む。
        /// dt で時間積分・0..1にクランプ。
        /// </summary>
        public static float CumulativeDistortionTick(float distortion, float levelsTraversed, float dt,
                                                     InformationDistortionParams p)
        {
            float d = Mathf.Clamp01(distortion);
            float levels = Mathf.Clamp01(levelsTraversed);
            // 上った階層×累積速度ぶんだけ 1（完全歪曲）へ向けて膨らむ。
            float step = levels * p.distortionAccumRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.MoveTowards(d, 1f, step));
        }

        public static float CumulativeDistortionTick(float distortion, float levelsTraversed, float dt)
            => CumulativeDistortionTick(distortion, levelsTraversed, dt, InformationDistortionParams.Default);

        // ===== 楽観バイアス（責任回避と面子） =====

        /// <summary>
        /// 楽観バイアス（0..1）＝責任回避(blameAvoidance 0..1)と面子(faceSaving 0..1)が報告を楽観方向へ歪める。
        /// どちらかでも歪むが、両方が高いと最も楽観に振れる（責任を負いたくない×面子を保ちたい）。
        /// 1−(1−回避)(1−面子)＝いずれかが効けば歪み、両立で最大＝都合の悪い真実が組織的に楽観へ書き換わる。
        /// </summary>
        public static float OptimismBias(float blameAvoidance, float faceSaving)
        {
            float blame = Mathf.Clamp01(blameAvoidance);
            float face = Mathf.Clamp01(faceSaving);
            // 責任回避と面子はどちらも独立に楽観へ寄せる（和集合的）。
            return Mathf.Clamp01(1f - (1f - blame) * (1f - face));
        }

        // ===== 認識ギャップの破裂（大本営発表の破綻） =====

        /// <summary>
        /// 認識ギャップの破裂判定＝乖離(perceivedVsReality 0..1)が しきい値(threshold 0..1)以上で、突然の露見
        /// (suddenRevelation 0..1＝大敗・現実の急な突きつけ)が乖離を一気に超過させると true（崩壊イベント）。
        /// 積み上がった乖離が限界を超え、現実が一気に露呈する＝大本営発表の破綻（<see cref="EventEngine"/> へ）。
        /// </summary>
        public static bool PerceptionGapRupture(float perceivedVsReality, float suddenRevelation, float threshold)
        {
            float gap = Mathf.Clamp01(perceivedVsReality);
            float reveal = Mathf.Clamp01(suddenRevelation);
            // 乖離が閾を超え、かつ露見が乖離を表面化させたとき破裂する。
            return gap >= Mathf.Clamp01(threshold) && reveal >= (1f - gap);
        }

        /// <summary>認識ギャップの破裂判定（既定しきい値 ruptureThreshold を使用）。</summary>
        public static bool PerceptionGapRupture(float perceivedVsReality, float suddenRevelation,
                                                InformationDistortionParams p)
            => PerceptionGapRupture(perceivedVsReality, suddenRevelation, p.ruptureThreshold);

        public static bool PerceptionGapRupture(float perceivedVsReality, float suddenRevelation)
            => PerceptionGapRupture(perceivedVsReality, suddenRevelation, InformationDistortionParams.Default);

        // ===== 正直な報告チャネル（悪報を罰しない文化） =====

        /// <summary>
        /// 正直な報告が通る度合い（0..1）＝悪報を罰しない心理的安全(psychologicalSafety 0..1)と、悪い報告への
        /// 寛容(badNewsTolerance 0..1)が正確な報告を通す。両者が高いほど真実が歪まず届く（真実が届く組織）＝
        /// 悪報を罰する組織ほどこの値が低く、報告が楽観へ歪む。両者の積＝どちらも要る。
        /// </summary>
        public static float HonestReportingChannel(float psychologicalSafety, float badNewsTolerance)
        {
            float safety = Mathf.Clamp01(psychologicalSafety);
            float tolerance = Mathf.Clamp01(badNewsTolerance);
            // 安全に悪報を出せ、かつ悪報が許される＝両方揃って初めて真実が通る。
            return Mathf.Clamp01(safety * tolerance);
        }

        // ===== 幻想の指揮判定 =====

        /// <summary>
        /// 幻想の指揮（トップが現実から乖離した認識で指揮している）の判定＝乖離(perceivedVsReality 0..1)が
        /// しきい値(threshold 0..1)以上なら true。トップが『勝っている』と信じたまま現実が崩壊する妄想指揮。
        /// </summary>
        public static bool IsDelusionalCommand(float perceivedVsReality, float threshold)
            => Mathf.Clamp01(perceivedVsReality) >= Mathf.Clamp01(threshold);

        /// <summary>幻想の指揮判定（既定しきい値 delusionThreshold を使用）。</summary>
        public static bool IsDelusionalCommand(float perceivedVsReality, InformationDistortionParams p)
            => IsDelusionalCommand(perceivedVsReality, p.delusionThreshold);

        public static bool IsDelusionalCommand(float perceivedVsReality)
            => IsDelusionalCommand(perceivedVsReality, InformationDistortionParams.Default);
    }
}
