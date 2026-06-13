using UnityEngine;

namespace Ginei
{
    /// <summary>恐慌の空間カスケード（CRWD-4 #1823・ル・ボン参考）の調整係数（純構造体・既定 .Default）。</summary>
    public readonly struct PanicCascadeParams
    {
        /// <summary>士気崩壊1のときの発火源パニックスケール（敗走の種の大きさ）。</summary>
        public readonly float seedScale;
        /// <summary>距離減衰の係数 k（1/(1+kd)＝大きいほど近くしか巻き込まない）。</summary>
        public readonly float distanceDecayK;
        /// <summary>隣接伝染のスケール（距離減衰×隣の士気の低さに掛ける）。</summary>
        public readonly float contagionScale;
        /// <summary>連鎖長の基準（initialPanic1・結束0で何部隊まで倒れるか）。</summary>
        public readonly float chainBaseLength;
        /// <summary>防火帯の効き＝この間隔で伝染がほぼ止まる（1/(1+gap/firebreakGap) の基準間隔）。</summary>
        public readonly float firebreakGap;

        public PanicCascadeParams(float seedScale, float distanceDecayK, float contagionScale,
                                  float chainBaseLength, float firebreakGap)
        {
            this.seedScale = Mathf.Clamp01(seedScale);
            this.distanceDecayK = Mathf.Max(0f, distanceDecayK);
            this.contagionScale = Mathf.Clamp01(contagionScale);
            this.chainBaseLength = Mathf.Max(0f, chainBaseLength);
            this.firebreakGap = Mathf.Max(0.0001f, firebreakGap);
        }

        /// <summary>既定＝種スケール1・距離係数1・伝染スケール1・連鎖基準8部隊・防火帯基準間隔2。</summary>
        public static PanicCascadeParams Default => new PanicCascadeParams(1f, 1f, 1f, 8f, 2f);
    }

    /// <summary>
    /// 恐慌の空間カスケード（CRWD-4 #1823・ル・ボン『群衆心理』参考・純ロジック test-first）。
    /// 一部隊の敗走（士気崩壊）が恐慌の種となり、隣接部隊へ距離減衰しながら伝染する＝群衆的パニックが
    /// 戦線を端から将棋倒しに崩していく。伝染は距離で減衰し（近い部隊ほど巻き込まれる）、指導者の威光
    /// （prestige）が伝播を抑え（名将がいると崩れにくい）、戦線の結束と部隊間隔（防火帯）が連鎖を止める。
    /// 役割分担：<see cref="PsychologicalSiegeMoraleRules"/>（四面楚歌＝包囲の心理的孤立で戦わず崩れる）とは別＝
    /// こちらは空間的な敗走伝染の将棋倒し。<see cref="EncirclementRules"/>（包囲度→降伏）とも別。
    /// <see cref="DislocationRules"/>とは別＝距離減衰のカスケードに特化。<see cref="CrowdContagionRules"/>の
    /// crowdIntensity や <see cref="LoyaltyRules"/>.ResolveCascade(#817) の流儀を参考にしてよいが独立実装。
    /// FleetMorale（Game層の士気）は read-only 相当＝係数算出のみ。乱数は roll 引数で決定論・盤面非依存の plain 引数。
    /// </summary>
    public static class PanicCascadeRules
    {
        /// <summary>
        /// 発火源パニック（0..1）＝士気崩壊度 unitMoraleCollapse(0..1)×種スケール。
        /// 士気が崩れた部隊ほど大きな恐慌の種になる＝敗走の発火源。
        /// </summary>
        public static float PanicSeed(float unitMoraleCollapse, PanicCascadeParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(unitMoraleCollapse) * p.seedScale);
        }

        public static float PanicSeed(float unitMoraleCollapse)
            => PanicSeed(unitMoraleCollapse, PanicCascadeParams.Default);

        /// <summary>
        /// 距離減衰倍率（0..1）＝1/(1+k×distance)。近い部隊ほど1に近く、遠いほど0へ。
        /// log/exp を使わない代数式の減衰＝群衆パニックは近接ほど強く巻き込む。
        /// </summary>
        public static float DistanceDecay(float distance, PanicCascadeParams p)
        {
            float d = Mathf.Max(0f, distance);
            return Mathf.Clamp01(1f / (1f + p.distanceDecayK * d));
        }

        public static float DistanceDecay(float distance)
            => DistanceDecay(distance, PanicCascadeParams.Default);

        /// <summary>
        /// 隣接部隊への恐慌伝染（0..1）＝パニック度 panicLevel×距離減衰×隣の士気の低さ(1−neighborMorale)×スケール。
        /// 近く（距離減衰大）て、隣が士気低い（崩れやすい）ほど強く伝染する。
        /// </summary>
        public static float ContagionToNeighbor(float panicLevel, float distance, float neighborMorale, PanicCascadeParams p)
        {
            float panic = Mathf.Clamp01(panicLevel);
            float decay = DistanceDecay(distance, p);
            float fragility = 1f - Mathf.Clamp01(neighborMorale);   // 士気が低いほど崩れやすい
            return Mathf.Clamp01(panic * decay * fragility * p.contagionScale);
        }

        public static float ContagionToNeighbor(float panicLevel, float distance, float neighborMorale)
            => ContagionToNeighbor(panicLevel, distance, neighborMorale, PanicCascadeParams.Default);

        /// <summary>
        /// 威光による抑制後の伝染（0..1）＝伝染 contagion×(1−指導者の威光 leaderPrestige(0..1))。
        /// 名将（prestige→1）がいるほど伝播が殺される＝威光が戦線を踏みとどまらせる。Params不要（純係数）。
        /// </summary>
        public static float PrestigeSuppression(float contagion, float leaderPrestige)
        {
            float c = Mathf.Clamp01(contagion);
            float pr = Mathf.Clamp01(leaderPrestige);
            return Mathf.Clamp01(c * (1f - pr));
        }

        /// <summary>
        /// 1ステップの伝播後の最大パニック（0..1）。発火源（panicLevels の最大）から各部隊へ
        /// distances[i] ぶん距離減衰しつつ伝染させ、威光 prestige で抑制し、最も強く巻き込まれた値を代表として返す。
        /// panicLevels/distances が null・空・長さ不一致でも安全（短い方に揃える）。隣の士気は不明なため最も崩れやすい
        /// （fragility=1）と仮定し、距離減衰のみで伝播の上界を見る。新規の発火が無ければ既存最大を保つ。
        /// </summary>
        public static float CascadeStep(float[] panicLevels, float[] distances, float prestige, PanicCascadeParams p)
        {
            if (panicLevels == null || panicLevels.Length == 0) return 0f;

            // 発火源＝現在のパニックの最大値。
            float source = 0f;
            for (int i = 0; i < panicLevels.Length; i++)
            {
                float v = Mathf.Clamp01(panicLevels[i]);
                if (v > source) source = v;
            }

            int n = panicLevels.Length;
            if (distances != null && distances.Length < n) n = distances.Length;

            float maxPanic = source;   // 発火源を下回らない（既存のパニックは消えない）
            for (int i = 0; i < n; i++)
            {
                float dist = (distances != null) ? distances[i] : 0f;
                // 隣の士気は不明＝最も崩れやすい(neighborMorale=0)前提で伝播の上界を取る。
                float spread = ContagionToNeighbor(source, dist, 0f, p);
                float suppressed = PrestigeSuppression(spread, prestige);
                // 各部隊は自分の既存パニックと、伝播してきたパニックの大きい方になる。
                float here = Mathf.Max(Mathf.Clamp01(panicLevels[i]), suppressed);
                if (here > maxPanic) maxPanic = here;
            }
            return Mathf.Clamp01(maxPanic);
        }

        public static float CascadeStep(float[] panicLevels, float[] distances, float prestige)
            => CascadeStep(panicLevels, distances, prestige, PanicCascadeParams.Default);

        /// <summary>
        /// 将棋倒しの連鎖長（部隊数・0..chainBaseLength）＝初期パニック initialPanic×基準長×(1−戦線結束 lineCohesion)。
        /// 初期の恐慌が強く、戦線の結束が弱いほど多くの部隊が将棋倒しに倒れる＝結束が連鎖を止める。
        /// </summary>
        public static float RoutChainLength(float initialPanic, float lineCohesion, PanicCascadeParams p)
        {
            float panic = Mathf.Clamp01(initialPanic);
            float cohesion = Mathf.Clamp01(lineCohesion);
            return Mathf.Max(0f, panic * p.chainBaseLength * (1f - cohesion));
        }

        public static float RoutChainLength(float initialPanic, float lineCohesion)
            => RoutChainLength(initialPanic, lineCohesion, PanicCascadeParams.Default);

        /// <summary>
        /// 防火帯倍率（0..1）＝1/(1+gapDistance/firebreakGap)。部隊間の間隔 gapDistance が広いほど0へ＝伝染が止まる。
        /// 間隔ゼロなら1（素通し）、基準間隔 firebreakGap で 0.5。広く空けた戦線はパニックが飛び火しにくい。
        /// </summary>
        public static float FirebreakEffect(float gapDistance, PanicCascadeParams p)
        {
            float gap = Mathf.Max(0f, gapDistance);
            return Mathf.Clamp01(1f / (1f + gap / p.firebreakGap));
        }

        public static float FirebreakEffect(float gapDistance)
            => FirebreakEffect(gapDistance, PanicCascadeParams.Default);

        /// <summary>
        /// 実際に敗走へ転じるか（決定論 roll∈[0,1)）＝パニック度が閾値 threshold を超え、かつ roll を上回れば敗走。
        /// 閾値未満なら踏みとどまる＝閾値とパニックの大きさで将棋倒しに加わるかが決まる。
        /// </summary>
        public static bool PanicTrigger(float panicLevel, float roll, float threshold)
        {
            float panic = Mathf.Clamp01(panicLevel);
            if (panic <= Mathf.Clamp01(threshold)) return false;
            return panic > Mathf.Clamp01(roll);
        }

        /// <summary>戦線が崩壊しつつあるか＝カスケード規模 cascadeMagnitude が閾値 threshold を超えた状態。</summary>
        public static bool IsLineCollapsing(float cascadeMagnitude, float threshold)
        {
            return Mathf.Clamp01(cascadeMagnitude) > Mathf.Clamp01(threshold);
        }
    }
}
