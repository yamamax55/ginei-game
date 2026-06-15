using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 遊撃戦の作戦モード（#1396・毛沢東の遊撃戦十六字訣）。
    /// 「敵進めば我退き（交戦回避）、敵駐すれば我擾し（回廊妨害）、敵疲れれば我打ち（奇襲打撃）、敵退けば我追う／我退く（退避）」。
    /// 弱者が正面決戦を避け、機動と奇襲で強敵を消耗させる作戦様式の局面を表す。
    /// </summary>
    public enum OperationalMode
    {
        /// <summary>交戦回避＝敵が進めば退いて捕捉されず、決戦を強いられない（敵進めば我退き）。</summary>
        交戦回避,
        /// <summary>回廊妨害＝敵の回廊・補給線を擾乱して疲弊させる（敵駐すれば我擾し）。</summary>
        回廊妨害,
        /// <summary>奇襲打撃＝敵が疲れた隙に打撃を与える（敵疲れれば我打つ）。</summary>
        奇襲打撃,
        /// <summary>退避＝打撃の後に素早く退いて損害を避ける（捕まる前に消える）。</summary>
        退避,
    }

    /// <summary>
    /// 遊撃戦ドクトリン＝交戦回避＋回廊妨害の作戦様式の純ロジック（SPW-3 #1396・スペイン内戦/ゲリラ戦）。
    /// 「正面からの決戦を避け、機動と奇襲で敵を消耗させる遊撃戦（ゲリラ戦）。弱者は強者と正面で戦わず、
    /// 交戦を回避しつつ敵の補給線・回廊を妨害し、打撃を与えては退く＝『敵進めば我退き、敵疲れれば我打つ』
    /// （十六字訣）」を式に出す。<see cref="EvasionEffectiveness"/> が機動×地形遮蔽で捕捉されずに退く有効性、
    /// <see cref="SelectModeForSituation"/> が状況に応じた作戦モードの選択、<see cref="CorridorHarassment"/> が回廊・補給線の妨害効果、
    /// <see cref="HitAndRun"/> が打撃して退く奇襲、<see cref="AttritionOnStronger"/> が強敵を時間で消耗させる効果、
    /// <see cref="ForceConcentrationDenial"/> が分散して決戦の的を与えない度合い、<see cref="PopularBaseReliance"/> が住民支持への依存、
    /// <see cref="IsGuerrillaPosture"/> が遊撃戦の構えの機能判定を返す。
    /// 分担：<see cref="FleetDoctrineRules"/>(生成済み) は海軍ドクトリン（決戦/漸減/通商破壊/現存艦隊）の選択、
    /// <see cref="AmbushRules"/> は伏兵の初撃（成功率・隊形不備・士気ショック）、
    /// <see cref="InsurgencyRules"/>(反乱・同EPIC SPW) は反乱の蜂起そのもの＝住民の海、
    /// <see cref="BlockadeRules"/> は回廊封鎖の通過率を担う。ここは「遊撃戦の作戦様式（交戦回避と回廊妨害）」のみ。
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GuerrillaDoctrineRules
    {
        /// <summary>
        /// 交戦回避の有効性(0..1)＝機動 mobility(0..1) と地形の遮蔽 terrainCover(0..1) の相乗。
        /// 機動が高いほど振り切れ、遮蔽が高いほど捕捉されず退ける＝決戦を強いられない。
        /// どちらか一方では足りず両者の調和で効く（重み付き：機動×evasionMobilityWeight＋遮蔽×残り、を相乗に近づける）。
        /// </summary>
        public static float EvasionEffectiveness(float mobility, float terrainCover, GuerrillaDoctrineParams p)
        {
            float mob = Mathf.Clamp01(mobility);
            float cover = Mathf.Clamp01(terrainCover);
            // 重み付き相加と相乗の混合＝両輪（機動と遮蔽）が揃うほど跳ね、片方欠けると相乗ぶんが効かず落ちる。
            float additive = mob * p.EvasionMobilityWeight + cover * (1f - p.EvasionMobilityWeight);
            float multiplicative = mob * cover;
            return Mathf.Clamp01(additive * (1f - p.EvasionSynergyShare) + multiplicative * p.EvasionSynergyShare);
        }

        public static float EvasionEffectiveness(float mobility, float terrainCover)
            => EvasionEffectiveness(mobility, terrainCover, GuerrillaDoctrineParams.Default);

        /// <summary>
        /// 状況に応じた作戦モードの選択（十六字訣）。
        /// enemyStrength＝敵の戦力(0..1)、ownStrength＝自軍の戦力(0..1)、opportunity＝打撃好機(0..1・敵の疲弊/隙)。
        /// ・敵が進む（敵が自軍を上回り opportunity 低）＝交戦回避（我退く）。
        /// ・敵が疲れ好機がある（opportunity 高）＝奇襲打撃（我打つ）。打撃後の局面は <see cref="HitAndRun"/>/退避が担う。
        /// ・拮抗で好機がまだ薄い＝回廊妨害（敵を擾乱して疲れさせる＝敵駐すれば我擾し）。
        /// </summary>
        public static OperationalMode SelectModeForSituation(float enemyStrength, float ownStrength, float opportunity, GuerrillaDoctrineParams p)
        {
            float enemy = Mathf.Clamp01(enemyStrength);
            float own = Mathf.Clamp01(ownStrength);
            float opp = Mathf.Clamp01(opportunity);

            // 敵が疲れて好機が立つ＝打つ（敵疲れれば我打つ）。
            if (opp >= p.StrikeOpportunityThreshold)
                return OperationalMode.奇襲打撃;

            // 戦力差で劣勢＝退く（敵進めば我退き）。劣勢の余白が閾値を超えたら交戦回避。
            float disadvantage = enemy - own; // 正＝劣勢
            if (disadvantage >= p.EvasionDisadvantageThreshold)
                return OperationalMode.交戦回避;

            // 拮抗で好機が薄い＝擾乱して敵を疲れさせる（敵駐すれば我擾し）。
            return OperationalMode.回廊妨害;
        }

        public static OperationalMode SelectModeForSituation(float enemyStrength, float ownStrength, float opportunity)
            => SelectModeForSituation(enemyStrength, ownStrength, opportunity, GuerrillaDoctrineParams.Default);

        /// <summary>
        /// 回廊・補給線の妨害効果(0..1)＝妨害の激しさ harassmentIntensity(0..1) と敵の補給露出 enemySupplyExposure(0..1) の積。
        /// 妨害が激しくとも敵が補給線を晒していなければ効かず、露出が大きいほど補給を断って疲弊させられる。
        /// </summary>
        public static float CorridorHarassment(float harassmentIntensity, float enemySupplyExposure)
        {
            return Mathf.Clamp01(Mathf.Clamp01(harassmentIntensity) * Mathf.Clamp01(enemySupplyExposure));
        }

        /// <summary>
        /// 打撃して退く（ヒットアンドラン）の成果(0..1)＝打撃の窓 strikeWindow(0..1) と退却速度 withdrawalSpeed(0..1) の調和。
        /// 窓があっても素早く退けねば反撃で捕まり、速くても窓が無ければ打てない＝両者が要る（相乗）。
        /// </summary>
        public static float HitAndRun(float strikeWindow, float withdrawalSpeed)
        {
            return Mathf.Clamp01(Mathf.Clamp01(strikeWindow) * Mathf.Clamp01(withdrawalSpeed));
        }

        /// <summary>
        /// 正面決戦せず強敵を時間で消耗させる累積(0..1)。
        /// harassment＝回廊妨害効果(0..1)、evasion＝交戦回避の有効性(0..1)、dt＝経過時間。
        /// 妨害で敵を削り、回避で自軍は決戦を避けて損なわれない＝弱者の消耗戦は時間に比例して積む（飽和して1へ近づく）。
        /// </summary>
        public static float AttritionOnStronger(float harassment, float evasion, float dt, GuerrillaDoctrineParams p)
        {
            float h = Mathf.Clamp01(harassment);
            float e = Mathf.Clamp01(evasion);
            float seconds = Mathf.Max(0f, dt);
            // 1tickの消耗増分＝妨害×回避×レート×dt（回避が高いほど自軍が削られず消耗戦を持続できる）。
            return Mathf.Clamp01(h * e * p.AttritionRate * seconds);
        }

        public static float AttritionOnStronger(float harassment, float evasion, float dt)
            => AttritionOnStronger(harassment, evasion, dt, GuerrillaDoctrineParams.Default);

        /// <summary>
        /// 戦力集中の拒否(0..1)＝分散作戦 dispersedOperations(0..1) が大きいほど敵に決戦の的を与えない（捕捉困難＝神出鬼没）。
        /// 集中すれば一挙撃滅されるが、分散すれば敵は戦力を集めても叩く相手が見つからない。
        /// </summary>
        public static float ForceConcentrationDenial(float dispersedOperations)
        {
            return Mathf.Clamp01(dispersedOperations);
        }

        /// <summary>
        /// 住民支持への依存(0..1)＝localSupport(0..1) が遊撃戦の補給・情報・隠れ場所を支える（民心が海・魚は水で泳ぐ）。
        /// 支持が低いほど遊撃戦は立ち行かず、下限 popularBaseFloor を割らない（最低限の自力ぶんは残す）。
        /// <see cref="InsurgencyRules"/>(反乱) が住民の海そのものを担い、ここはその支持への依存度の写像。
        /// </summary>
        public static float PopularBaseReliance(float localSupport, GuerrillaDoctrineParams p)
        {
            return Mathf.Clamp01(Mathf.Lerp(p.PopularBaseFloor, 1f, Mathf.Clamp01(localSupport)));
        }

        public static float PopularBaseReliance(float localSupport)
            => PopularBaseReliance(localSupport, GuerrillaDoctrineParams.Default);

        /// <summary>
        /// 遊撃戦の構えが機能している判定＝決戦回避（evasionEffectiveness）と妨害（harassment）の調和が閾値以上。
        /// 回避できても妨害しなければただ逃げるだけ、妨害できても回避できねば捕まって決戦になる＝両者の相乗が要る。
        /// </summary>
        public static bool IsGuerrillaPosture(float evasionEffectiveness, float harassment, float threshold)
        {
            float posture = Mathf.Clamp01(evasionEffectiveness) * Mathf.Clamp01(harassment);
            return posture >= Mathf.Clamp01(threshold);
        }
    }

    /// <summary>
    /// GuerrillaDoctrineRules の調整値（#1396・マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// </summary>
    public readonly struct GuerrillaDoctrineParams
    {
        /// <summary>交戦回避で機動が占める重み（残りは地形遮蔽）。</summary>
        public readonly float EvasionMobilityWeight;
        /// <summary>交戦回避の相乗ぶんの割合（機動×遮蔽の積を混ぜる比＝両輪が揃うほど跳ねる）。</summary>
        public readonly float EvasionSynergyShare;
        /// <summary>奇襲打撃へ転じる好機の閾値（opportunity がこれ以上で我打つ）。</summary>
        public readonly float StrikeOpportunityThreshold;
        /// <summary>交戦回避へ退く劣勢の閾値（敵−自軍の差がこれ以上で我退く）。</summary>
        public readonly float EvasionDisadvantageThreshold;
        /// <summary>強敵への消耗の蓄積レート（妨害×回避×dt に掛ける）。</summary>
        public readonly float AttritionRate;
        /// <summary>住民支持依存の下限（支持0でも残る最低限の自力ぶん）。</summary>
        public readonly float PopularBaseFloor;

        public GuerrillaDoctrineParams(
            float evasionMobilityWeight, float evasionSynergyShare,
            float strikeOpportunityThreshold, float evasionDisadvantageThreshold,
            float attritionRate, float popularBaseFloor)
        {
            EvasionMobilityWeight = Mathf.Clamp01(evasionMobilityWeight);
            EvasionSynergyShare = Mathf.Clamp01(evasionSynergyShare);
            StrikeOpportunityThreshold = Mathf.Clamp01(strikeOpportunityThreshold);
            EvasionDisadvantageThreshold = Mathf.Clamp01(evasionDisadvantageThreshold);
            AttritionRate = Mathf.Max(0f, attritionRate);
            PopularBaseFloor = Mathf.Clamp01(popularBaseFloor);
        }

        /// <summary>
        /// 既定＝機動重み0.5・相乗割合0.5、好機閾値0.6（敵疲れれば我打つ）、劣勢閾値0.3（敵進めば我退き）、
        /// 消耗レート0.2、住民支持依存の下限0.2。
        /// </summary>
        public static GuerrillaDoctrineParams Default => new GuerrillaDoctrineParams(
            evasionMobilityWeight: 0.5f, evasionSynergyShare: 0.5f,
            strikeOpportunityThreshold: 0.6f, evasionDisadvantageThreshold: 0.3f,
            attritionRate: 0.2f, popularBaseFloor: 0.2f);
    }
}
