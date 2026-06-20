using UnityEngine;

namespace Ginei
{
    /// <summary>敗走兵の再結集（rally・立て直し）の調整係数。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct RallyParams
    {
        /// <summary>再結集確率に統率が効く重み。</summary>
        public readonly float chanceLeadershipWeight;
        /// <summary>再結集確率に敵からの距離が効く重み。</summary>
        public readonly float chanceDistanceWeight;
        /// <summary>「敵から十分遠い」と見なす基準距離（これ以上は距離の効きが頭打ち）。</summary>
        public readonly float enemyReferenceDistance;
        /// <summary>立て直しに要る最短時間（混乱なし＝整然と下がれば速い）。</summary>
        public readonly float baseRallyTime;
        /// <summary>立て直しに要る最長時間（混乱極大・統率皆無）。</summary>
        public readonly float maxRallyTime;
        /// <summary>追撃の近さ1.0あたりが再結集を妨げる強さ。</summary>
        public readonly float pursuitDisruptionScale;
        /// <summary>安全な集結点の確保度に味方支配が効く重み（残りは敵からの距離）。</summary>
        public readonly float safeControlWeight;
        /// <summary>戦線復帰の固定遅れ（再編の段取りに最低限かかる時間）。</summary>
        public readonly float reintegrationBase;
        /// <summary>再結集兵力1あたりの戦線復帰の追加遅れ（大兵力ほど隊列を組み直すのに時間）。</summary>
        public readonly float reintegrationPerStrength;
        /// <summary>再結集できると判定する実効確率の既定しきい値。</summary>
        public readonly float rallyThreshold;

        public RallyParams(float chanceLeadershipWeight, float chanceDistanceWeight, float enemyReferenceDistance,
                           float baseRallyTime, float maxRallyTime, float pursuitDisruptionScale,
                           float safeControlWeight, float reintegrationBase, float reintegrationPerStrength,
                           float rallyThreshold)
        {
            this.chanceLeadershipWeight = Mathf.Clamp01(chanceLeadershipWeight);
            this.chanceDistanceWeight = Mathf.Clamp01(chanceDistanceWeight);
            this.enemyReferenceDistance = Mathf.Max(0.01f, enemyReferenceDistance); // 0除算回避
            this.baseRallyTime = Mathf.Max(0f, baseRallyTime);
            this.maxRallyTime = Mathf.Max(this.baseRallyTime, maxRallyTime);        // 最長 < 最短 を防ぐ
            this.pursuitDisruptionScale = Mathf.Max(0f, pursuitDisruptionScale);
            this.safeControlWeight = Mathf.Clamp01(safeControlWeight);
            this.reintegrationBase = Mathf.Max(0f, reintegrationBase);
            this.reintegrationPerStrength = Mathf.Max(0f, reintegrationPerStrength);
            this.rallyThreshold = Mathf.Clamp01(rallyThreshold);
        }

        /// <summary>
        /// 既定＝統率重み0.6/距離重み0.4/基準距離50/最短30/最長180/追撃妨害1.0/支配重み0.5/復帰固定5/兵力係数0.02/しきい値0.5。
        /// </summary>
        public static RallyParams Default =>
            new RallyParams(0.6f, 0.4f, 50f, 30f, 180f, 1f, 0.5f, 5f, 0.02f, 0.5f);
    }

    /// <summary>
    /// 敗走兵の再結集（rally・立て直し）の純ロジック（盤面非依存・決定論・乱数なし・test-first）。
    /// 崩れて敗走した部隊も、安全な後方で指揮官の下に再結集すれば戦線へ復帰できる＝時間・統率・安全な集結点が要り、追撃下では立て直せない。
    /// <see cref="SutegamariRules"/>（捨てがまり＝殿で旗艦が退却＝退却の<b>瞬間</b>の解決）とは別＝こちらは<b>退却後</b>の再結集。
    /// <see cref="FleetMorale"/>（士気そのものの増減）は read-only 相当＝本ルールは再結集の係数を算出するだけで士気を直接書かない。
    /// 追撃の妨害（PursuitBattleRules 等があればその追撃の近さ）を入力（pursuerProximity）に取れる。値は徹底クランプ。
    /// </summary>
    public static class RallyRules
    {
        /// <summary>
        /// 再結集できる確率 0..1：統率が高く・敵から遠いほど高い。leadership は 0..100、distanceFromEnemy は距離。
        /// 統率と距離を重み付き和で混ぜる（既定重みは合計1.0）。
        /// </summary>
        public static float RallyChance(float leadership, float distanceFromEnemy, RallyParams p)
        {
            float lead = Mathf.Clamp(leadership, 0f, 100f) / 100f;
            float dist = Mathf.Clamp01(Mathf.Max(0f, distanceFromEnemy) / p.enemyReferenceDistance);
            return Mathf.Clamp01(p.chanceLeadershipWeight * lead + p.chanceDistanceWeight * dist);
        }

        public static float RallyChance(float leadership, float distanceFromEnemy)
            => RallyChance(leadership, distanceFromEnemy, RallyParams.Default);

        /// <summary>
        /// 立て直しに要る時間（baseRallyTime〜maxRallyTime）：混乱が大きく・統率が低いほど長い。
        /// disorder は 0..1、leadership は 0..100。混乱0なら最短、混乱極大かつ統率皆無で最長。
        /// </summary>
        public static float RallyTime(float disorder, float leadership, RallyParams p)
        {
            float d = Mathf.Clamp01(disorder);
            float lead = Mathf.Clamp(leadership, 0f, 100f) / 100f;
            float span = p.maxRallyTime - p.baseRallyTime;
            return p.baseRallyTime + span * d * (1f - lead);
        }

        public static float RallyTime(float disorder, float leadership)
            => RallyTime(disorder, leadership, RallyParams.Default);

        /// <summary>
        /// 散った兵のうち再結集できる量：散兵力 × 再結集できた割合（rallyFraction 0..1）。
        /// 全部は戻らない＝崩れた部隊は一部が永久に散る。
        /// </summary>
        public static float RecoveredStrength(float scatteredStrength, float rallyFraction)
        {
            return Mathf.Max(0f, scatteredStrength) * Mathf.Clamp01(rallyFraction);
        }

        /// <summary>
        /// 再結集に伴う士気回復 0..1：再結集の進捗（rallyProgress 0..1）と指揮官の臨在（leaderPresence 0..1）の積。
        /// 旗の下に集まり指揮官が居てこそ士気が戻る（どちらか欠けると回復しない）。
        /// </summary>
        public static float MoraleRestore(float rallyProgress, float leaderPresence)
        {
            return Mathf.Clamp01(Mathf.Clamp01(rallyProgress) * Mathf.Clamp01(leaderPresence));
        }

        /// <summary>
        /// 追撃による再結集の妨害 0..1：追撃の近さ（pursuerProximity 0..1＝1で敵が背後に張りつく）に妨害強度を掛ける。
        /// 追われている間は隊列を組み直せない（CanRally の実効確率を割り引く入力）。
        /// </summary>
        public static float PursuitDisruption(float pursuerProximity, RallyParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(pursuerProximity) * p.pursuitDisruptionScale);
        }

        public static float PursuitDisruption(float pursuerProximity)
            => PursuitDisruption(pursuerProximity, RallyParams.Default);

        /// <summary>
        /// 安全な集結点の確保度 0..1：味方の制圧度（friendlyControl 0..1）と敵からの距離を重み付き和で混ぜる。
        /// 自勢力の支配下で・敵から遠いほど安心して集まれる。distanceFromEnemy は距離。
        /// </summary>
        public static float SafeRallyPoint(float distanceFromEnemy, float friendlyControl, RallyParams p)
        {
            float dist = Mathf.Clamp01(Mathf.Max(0f, distanceFromEnemy) / p.enemyReferenceDistance);
            float control = Mathf.Clamp01(friendlyControl);
            return Mathf.Clamp01(p.safeControlWeight * control + (1f - p.safeControlWeight) * dist);
        }

        public static float SafeRallyPoint(float distanceFromEnemy, float friendlyControl)
            => SafeRallyPoint(distanceFromEnemy, friendlyControl, RallyParams.Default);

        /// <summary>
        /// 戦線復帰までの遅れ（時間）：固定の段取り時間＋再結集兵力に比例した追加遅れ。
        /// 大兵力ほど隊列を組み直して前線へ戻すのに時間がかかる。
        /// </summary>
        public static float ReintegrationDelay(float recoveredStrength, RallyParams p)
        {
            return p.reintegrationBase + p.reintegrationPerStrength * Mathf.Max(0f, recoveredStrength);
        }

        public static float ReintegrationDelay(float recoveredStrength)
            => ReintegrationDelay(recoveredStrength, RallyParams.Default);

        /// <summary>
        /// 再結集できるか：実効確率＝再結集確率 ×（1−追撃妨害）が threshold 以上で true。
        /// 追撃が近いほど実効確率が削られ、追われきっていれば立て直せない。
        /// </summary>
        public static bool CanRally(float rallyChance, float pursuitDisruption, float threshold)
        {
            float effective = Mathf.Clamp01(rallyChance) * (1f - Mathf.Clamp01(pursuitDisruption));
            return effective >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定しきい値（<see cref="RallyParams.rallyThreshold"/>）での再結集可否判定。</summary>
        public static bool CanRally(float rallyChance, float pursuitDisruption)
            => CanRally(rallyChance, pursuitDisruption, RallyParams.Default.rallyThreshold);
    }
}
