using UnityEngine;

namespace Ginei
{
    /// <summary>決勝点分析の調整係数。ctor で全値をクランプする。</summary>
    public readonly struct DecisivePointParams
    {
        /// <summary>接続次数を正規化する基準（この本数で次数寄与が満額＝1.0）。1以上。</summary>
        public readonly float degreeNorm;
        /// <summary>切断点（cut vertex）の地理的レバレッジへの跳ね上げ倍率（1以上）。切断点はそこを失うと連結が割れる要点。</summary>
        public readonly float cutVertexBoost;
        /// <summary>到達可能性における敵防備の効き（0..1の防備をこの冪で非線形に）。1以上。</summary>
        public readonly float garrisonExponent;
        /// <summary>決勝点とみなすスコアの既定閾値（0..1）。これ以上なら決勝点。</summary>
        public readonly float decisiveThreshold;
        /// <summary>同時打撃へ切り替える戦力余裕の閾値（0..1）。戦力がこれ以上あれば複数決勝点を同時に狙える。</summary>
        public readonly float simultaneousThreshold;

        public DecisivePointParams(float degreeNorm, float cutVertexBoost, float garrisonExponent,
            float decisiveThreshold, float simultaneousThreshold)
        {
            this.degreeNorm = Mathf.Max(1f, degreeNorm);
            this.cutVertexBoost = Mathf.Max(1f, cutVertexBoost);
            this.garrisonExponent = Mathf.Max(1f, garrisonExponent);
            this.decisiveThreshold = Mathf.Clamp01(decisiveThreshold);
            this.simultaneousThreshold = Mathf.Clamp01(simultaneousThreshold);
        }

        /// <summary>既定＝次数基準4本／切断点跳ね上げ1.5／敵防備冪1.5／決勝点閾値0.6／同時打撃閾値0.6。</summary>
        public static DecisivePointParams Default =>
            new DecisivePointParams(4f, 1.5f, 1.5f, 0.6f, 0.6f);
    }

    /// <summary>
    /// 決勝点（decisive point）の識別＝ジョミニ『戦争概論』（JOM-2・#1347）。決勝点とは
    /// <b>戦域において占領・確保すれば決定的な優位をもたらす地理的・戦略的な点</b>で、ジョミニは
    /// 「作戦線を決勝点に向けよ」と説いた。切断点（cut vertex＝そこを失うと連結が割れる結節）や
    /// チョークポイント性が決勝点候補になる。<b>地理的レバレッジ（接続次数×切断点性）→戦略的重み
    /// （地理×経済×前線近接）→到達可能性で割り引き→決勝点スコア</b>を導き、複数候補をスコア比較して
    /// AIの優先目標化（どこへ作戦線を向けるか）に用いる。届かない要点は決勝点でない＝到達可能性が核。
    /// <see cref="CenterOfGravityRules"/>（重心＝敵の力が集中する一点。地理に限らず叩けば全体が崩れる源泉）
    /// とは別＝こちらは<b>地理的に確保すべき要点</b>の同定。<see cref="ChokepointValueRules"/>
    /// （要衝の希少性/経済/前線価値）とも別＝こちらは<b>切断点解析＋到達可能性でAIの優先目標化に特化</b>。
    /// 同EPIC JOM の <see cref="InteriorLineRules"/>（内線の利＝中央位置で各個撃破）とも別。
    /// 盤面非依存の plain 引数（connectivityDegree, isCutVertex 等）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DecisivePointRules
    {
        /// <summary>
        /// 地理的レバレッジ（0..1）＝その点が戦域グラフ上でどれだけ要所か。接続次数（connectivityDegree＝
        /// その点に集まる回廊の本数）を <see cref="DecisivePointParams.degreeNorm"/> で正規化した値を基礎とし、
        /// 切断点（isCutVertex＝そこを失えば連結が割れる結節）なら <see cref="DecisivePointParams.cutVertexBoost"/>
        /// で跳ね上げる（上限1）。多くの回廊が交わり、かつ切断点である点ほど地理的レバレッジが大きい。
        /// </summary>
        public static float GeographicLeverage(int connectivityDegree, bool isCutVertex, DecisivePointParams p)
        {
            float degree = Mathf.Max(0, connectivityDegree);
            float baseLeverage = Mathf.Clamp01(degree / p.degreeNorm);
            float cut = isCutVertex ? 1f : 0f;
            float boosted = baseLeverage * Mathf.Lerp(1f, p.cutVertexBoost, cut); // 切断点なら跳ね上げ
            return Mathf.Clamp01(boosted);
        }

        /// <summary>既定係数での地理的レバレッジ（0..1）。</summary>
        public static float GeographicLeverage(int connectivityDegree, bool isCutVertex)
            => GeographicLeverage(connectivityDegree, isCutVertex, DecisivePointParams.Default);

        /// <summary>
        /// 戦略的重み（0..1）＝地理的レバレッジ × 経済価値 × 前線近接の積。要所であり（geographicLeverage）、
        /// 押さえれば実利がある（economicValue 0..1）、かつ前線に近く決戦に効く（frontProximity 0..1）ほど
        /// 戦略的に重い。どれか一つでも0なら重みは0＝後方の無価値な要所は決勝点にならない。
        /// </summary>
        public static float StrategicWeight(float geographicLeverage, float economicValue, float frontProximity)
        {
            float geo = Mathf.Clamp01(geographicLeverage);
            float econ = Mathf.Clamp01(economicValue);
            float front = Mathf.Clamp01(frontProximity);
            return Mathf.Clamp01(geo * econ * front);
        }

        /// <summary>
        /// 到達可能性（0..1）＝そこへ兵を届けて確保できるか。距離（distance 0..1＝遠いほど大）と、
        /// その点の敵防備（enemyStrengthAtPoint 0..1）を <see cref="DecisivePointParams.garrisonExponent"/>
        /// の冪で非線形に効かせて差し引き、自軍機動（ownMobility 0..1）で押し戻す。近く・敵が薄く・自軍が
        /// 機動的なほど到達可能性が高い＝遠く重防備の要所は届かない（後段で決勝点スコアを0へ削る）。
        /// </summary>
        public static float Attainability(float distance, float enemyStrengthAtPoint, float ownMobility, DecisivePointParams p)
        {
            float dist = Mathf.Clamp01(distance);
            float garrison = Mathf.Pow(Mathf.Clamp01(enemyStrengthAtPoint), p.garrisonExponent); // 防備は非線形に重い
            float mob = Mathf.Clamp01(ownMobility);
            // 距離と防備が届きにくさを作り、自軍機動が押し戻す（機動で距離を相殺）。
            float reach = (1f - dist) * (1f - garrison);
            float mobilityBoost = Mathf.Lerp(reach, 1f, mob * (1f - garrison)); // 機動は防備を越えられない
            return Mathf.Clamp01(mobilityBoost);
        }

        /// <summary>既定係数での到達可能性（0..1）。</summary>
        public static float Attainability(float distance, float enemyStrengthAtPoint, float ownMobility)
            => Attainability(distance, enemyStrengthAtPoint, ownMobility, DecisivePointParams.Default);

        /// <summary>
        /// 決勝点スコア（0..1）＝戦略的重み × 到達可能性の積。価値ある要所でも届かなければ決勝点でない
        /// （ジョミニ＝作戦線は到達できる決勝点に向ける）。重く・かつ届く点ほどスコアが高い。
        /// </summary>
        public static float DecisivePointScore(float strategicWeight, float attainability)
        {
            float w = Mathf.Clamp01(strategicWeight);
            float reach = Mathf.Clamp01(attainability);
            return Mathf.Clamp01(w * reach);
        }

        /// <summary>
        /// 占領した時の戦域への波及（0..1）＝地理的レバレッジを冪で増幅（切断点ほど跳ねる）。切断点を
        /// 占領すれば敵の連結が割れて分断され、低い結節点を取っても波及は小さい＝確保の戦略的見返り。
        /// </summary>
        public static float CaptureImpact(float geographicLeverage, DecisivePointParams p)
        {
            float geo = Mathf.Clamp01(geographicLeverage);
            // レバレッジが高いほど波及が加速（切断点の分断効果を非線形に表現）。
            float curve = geo * geo; // 二乗＝高レバレッジ点の占領が分断を生む
            return Mathf.Clamp01(curve);
        }

        /// <summary>既定係数での占領波及（0..1）。</summary>
        public static float CaptureImpact(float geographicLeverage)
            => CaptureImpact(geographicLeverage, DecisivePointParams.Default);

        /// <summary>
        /// 2候補のスコア比較でAIの優先目標化（-1/0/1）。scoreA がより高ければ -1（A を先に狙う）、
        /// scoreB がより高ければ 1（B を先に）、ほぼ同点（差が tol 未満）なら 0。作戦線をどちらの決勝点へ
        /// 向けるかの裁定。返り値は「先に狙うべき側がA=-1, B=1」（小さいほど優先＝ソート用）。
        /// </summary>
        public static int PriorityRank(float scoreA, float scoreB)
        {
            float a = Mathf.Clamp01(scoreA);
            float b = Mathf.Clamp01(scoreB);
            float tol = 0.0001f;
            if (a - b > tol) return -1; // A が優先
            if (b - a > tol) return 1;  // B が優先
            return 0;
        }

        /// <summary>
        /// 決勝点が複数あるとき逐次か同時かの適性（0..1＝同時打撃の適性。0に近いほど逐次が適）。
        /// pointCount（狙うべき決勝点の数）が多いほど一括処理は難しく、自軍戦力（ownForce 0..1）が
        /// 余るほど同時に複数を叩ける。戦力が <see cref="DecisivePointParams.simultaneousThreshold"/> に
        /// 満たなければ逐次（一点ずつ＝兵力集中）が無難＝戦力を点数で割った余裕が同時打撃の可否を決める。
        /// </summary>
        public static float SequentialVsSimultaneous(int pointCount, float ownForce, DecisivePointParams p)
        {
            int count = Mathf.Max(1, pointCount);
            float force = Mathf.Clamp01(ownForce);
            // 1点あたりに割ける戦力＝force / count。閾値を超えた分だけ同時打撃が成り立つ。
            float perPoint = force / count;
            float thr = p.simultaneousThreshold;
            if (perPoint <= 0f) return 0f;
            // 1点なら常に「同時（=その一点へ集中）」が成立。複数なら perPoint が閾値以上で同時適性が立つ。
            if (count == 1) return Mathf.Clamp01(force);
            float fitness = perPoint / Mathf.Max(0.0001f, thr);
            return Mathf.Clamp01(fitness);
        }

        /// <summary>既定係数での逐次/同時適性（0..1＝同時打撃の適性）。</summary>
        public static float SequentialVsSimultaneous(int pointCount, float ownForce)
            => SequentialVsSimultaneous(pointCount, ownForce, DecisivePointParams.Default);

        /// <summary>決勝点と呼べるか＝スコアが閾値以上（占領すれば決定的優位をもたらす点）。</summary>
        public static bool IsDecisivePoint(float decisivePointScore, float threshold)
        {
            return Mathf.Clamp01(decisivePointScore) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="DecisivePointParams.decisiveThreshold"/>）での決勝点判定。</summary>
        public static bool IsDecisivePoint(float decisivePointScore)
            => IsDecisivePoint(decisivePointScore, DecisivePointParams.Default.decisiveThreshold);
    }
}
