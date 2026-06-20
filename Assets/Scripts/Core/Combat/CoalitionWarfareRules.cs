using UnityEngine;

namespace Ginei
{
    /// <summary>連合作戦（同盟軍の共同作戦）の調整係数。</summary>
    public readonly struct CoalitionWarfareParams
    {
        /// <summary>合算戦力に掛ける基準スケール（1.0で素の合計）。</summary>
        public readonly float combinedScale;
        /// <summary>各国の留保（national caveats）が指揮統一を削る重み（0で無効・大きいほど足を引く）。</summary>
        public readonly float caveatWeight;
        /// <summary>同盟勢力が1つ増えるごとに連携効率を削る係数（多国籍ほど調整が難しい）。</summary>
        public readonly float coordinationPenaltyPerAlly;
        /// <summary>連携効率の下限（足並みが乱れても完全には0にしない素地）。</summary>
        public readonly float minCoordination;
        /// <summary>負担の偏りが不満に変わる強さ（一国が割を食うほど摩擦）。</summary>
        public readonly float frictionScale;
        /// <summary>戦争目標のズレが結束を削る重み（思惑の食い違いが結束を蝕む）。</summary>
        public readonly float divergenceWeight;
        /// <summary>共通の脅威が結束を底上げする強さ（共通の敵が連合を固める）。</summary>
        public readonly float threatBonus;

        public CoalitionWarfareParams(float combinedScale, float caveatWeight, float coordinationPenaltyPerAlly,
            float minCoordination, float frictionScale, float divergenceWeight, float threatBonus)
        {
            this.combinedScale = Mathf.Max(0f, combinedScale);
            this.caveatWeight = Mathf.Clamp01(caveatWeight);
            this.coordinationPenaltyPerAlly = Mathf.Clamp01(coordinationPenaltyPerAlly);
            this.minCoordination = Mathf.Clamp01(minCoordination);
            this.frictionScale = Mathf.Max(0f, frictionScale);
            this.divergenceWeight = Mathf.Clamp01(divergenceWeight);
            this.threatBonus = Mathf.Clamp01(threatBonus);
        }

        /// <summary>既定＝合算スケール1.0・留保重み0.6・勢力ごと連携減0.1・連携下限0.3・摩擦1.0・ズレ重み0.7・脅威ボーナス0.3。</summary>
        public static CoalitionWarfareParams Default =>
            new CoalitionWarfareParams(1.0f, 0.6f, 0.1f, 0.3f, 1.0f, 0.7f, 0.3f);
    }

    /// <summary>
    /// 連合作戦（同盟軍の共同作戦・指揮統一の難しさ）の純ロジック。複数の同盟勢力が連携して戦うとき、
    /// 戦力は合算される（<see cref="CombinedStrength"/>）が、指揮系統が分かれ各国の思惑・足並みの乱れで
    /// 連携が落ちる。**統一指揮できれば強い**（<see cref="CommandUnityFactor"/>）が各国の留保が足を引き、
    /// **勢力が多いほど調整が難しい**（<see cref="CoordinationEfficiency"/>）。負担の偏りは摩擦を生み
    /// （<see cref="BurdenSharingFriction"/>）、戦争目標のズレ（<see cref="DivergentObjectives"/>）は結束を
    /// 蝕むが、**共通の脅威が連合を固める**（<see cref="CoalitionCohesion"/>）。連携を加味した実効戦力が
    /// <see cref="EffectiveCombatPower"/>＝合算しても連携が崩れれば額面どおりには戦えない。
    ///
    /// 分担：<see cref="CoalitionFaultlineRules"/>（部族連合の亀裂・連合内部の離反/分断の素地）とは別＝
    /// こちらは同盟軍の**作戦面の連携**（指揮統一・調整効率・共同戦力）。<see cref="DiplomacyRules"/>
    /// （外交状態の遷移）とは別＝こちらは**共同作戦の戦力面**の係数を返すのみ。盤面非依存の plain 引数。
    /// 乱数なし・入力クランプ・配列 null/空安全・基準値非破壊（実効値パターン）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CoalitionWarfareRules
    {
        /// <summary>
        /// 同盟軍の合算戦力（≧0）＝各勢力の戦力（負は0扱い）の総和×合算スケール。配列 null/空は0。
        /// </summary>
        public static float CombinedStrength(float[] allyStrengths, CoalitionWarfareParams p)
        {
            if (allyStrengths == null || allyStrengths.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < allyStrengths.Length; i++)
            {
                float s = allyStrengths[i];
                if (s > 0f) sum += s;   // 負の戦力は無効
            }
            return Mathf.Max(0f, sum * p.combinedScale);
        }

        /// <summary>既定パラメータでの合算戦力。</summary>
        public static float CombinedStrength(float[] allyStrengths)
            => CombinedStrength(allyStrengths, CoalitionWarfareParams.Default);

        /// <summary>
        /// 指揮統一の度合い（0..1）＝統一指揮 unifiedCommand(0..1)×（1−各国の留保 nationalCaveats(0..1)×留保重み）。
        /// 統一指揮が確立しても各国が自国の都合（留保）を残すほど一体の指揮が削られる。
        /// </summary>
        public static float CommandUnityFactor(float unifiedCommand, float nationalCaveats, CoalitionWarfareParams p)
        {
            float unified = Mathf.Clamp01(unifiedCommand);
            float caveats = Mathf.Clamp01(nationalCaveats);
            return Mathf.Clamp01(unified * (1f - caveats * p.caveatWeight));
        }

        /// <summary>既定パラメータでの指揮統一度。</summary>
        public static float CommandUnityFactor(float unifiedCommand, float nationalCaveats)
            => CommandUnityFactor(unifiedCommand, nationalCaveats, CoalitionWarfareParams.Default);

        /// <summary>
        /// 連携効率（minCoordination..1）＝指揮統一度×（1−（勢力数−1）×勢力ごと連携減）。勢力が多いほど
        /// 調整が難しく効率が落ちる。下限 minCoordination で底打ち（烏合でも素地は残す）。allyCount≤1 は減衰なし。
        /// </summary>
        public static float CoordinationEfficiency(float commandUnityFactor, int allyCount, CoalitionWarfareParams p)
        {
            float cuf = Mathf.Clamp01(commandUnityFactor);
            int n = Mathf.Max(1, allyCount);
            float multiplicityPenalty = 1f - (n - 1) * p.coordinationPenaltyPerAlly;   // 多国籍ほど調整難
            float eff = cuf * multiplicityPenalty;
            return Mathf.Clamp(eff, p.minCoordination, 1f);
        }

        /// <summary>既定パラメータでの連携効率。</summary>
        public static float CoordinationEfficiency(float commandUnityFactor, int allyCount)
            => CoordinationEfficiency(commandUnityFactor, allyCount, CoalitionWarfareParams.Default);

        /// <summary>
        /// 連携を加味した実効戦力（≧0）＝合算戦力×連携効率。合算しても連携が崩れれば額面どおり戦えない。
        /// </summary>
        public static float EffectiveCombatPower(float combinedStrength, float coordinationEfficiency)
            => Mathf.Max(0f, combinedStrength) * Mathf.Clamp01(coordinationEfficiency);

        /// <summary>
        /// 負担の偏りによる不満（0..1）＝負担の偏り contributionImbalance(0..1)×摩擦スケール。一国が割を食う
        /// （損害・出兵が偏る）ほど連合内に不満が募る。
        /// </summary>
        public static float BurdenSharingFriction(float contributionImbalance, CoalitionWarfareParams p)
            => Mathf.Clamp01(Mathf.Clamp01(contributionImbalance) * p.frictionScale);

        /// <summary>既定パラメータでの負担摩擦。</summary>
        public static float BurdenSharingFriction(float contributionImbalance)
            => BurdenSharingFriction(contributionImbalance, CoalitionWarfareParams.Default);

        /// <summary>
        /// 各国の戦争目標のズレ（0..1）＝1−目標の一致度 allyObjectiveAlignment(0..1)。一致度が高いほどズレは小さい。
        /// </summary>
        public static float DivergentObjectives(float allyObjectiveAlignment)
            => Mathf.Clamp01(1f - Mathf.Clamp01(allyObjectiveAlignment));

        /// <summary>
        /// 連合の結束（0..1）＝指揮統一度×（1−目標のズレ×ズレ重み）＋共通の脅威 sharedThreat(0..1)×脅威ボーナス。
        /// 思惑の食い違いが結束を蝕むが、共通の敵が連合を固める＝外圧で空中分解を食い止める。
        /// </summary>
        public static float CoalitionCohesion(float commandUnityFactor, float divergentObjectives,
            float sharedThreat, CoalitionWarfareParams p)
        {
            float cuf = Mathf.Clamp01(commandUnityFactor);
            float divergent = Mathf.Clamp01(divergentObjectives);
            float threat = Mathf.Clamp01(sharedThreat);
            float baseCohesion = cuf * (1f - divergent * p.divergenceWeight);   // ズレが結束を蝕む
            return Mathf.Clamp01(baseCohesion + threat * p.threatBonus);        // 共通の脅威が固める
        }

        /// <summary>既定パラメータでの連合結束。</summary>
        public static float CoalitionCohesion(float commandUnityFactor, float divergentObjectives, float sharedThreat)
            => CoalitionCohesion(commandUnityFactor, divergentObjectives, sharedThreat, CoalitionWarfareParams.Default);

        /// <summary>
        /// 連合軍が機能しているか＝連携効率 coordinationEfficiency が閾値以上なら true。共同作戦を成立とみなす目安。
        /// </summary>
        public static bool IsCoalitionEffective(float coordinationEfficiency, float threshold)
            => Mathf.Clamp01(coordinationEfficiency) >= Mathf.Clamp01(threshold);
    }
}
