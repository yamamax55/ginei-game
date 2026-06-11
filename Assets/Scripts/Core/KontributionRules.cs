using UnityEngine;

namespace Ginei
{
    /// <summary>コントリビューション制（軍税徴発）の調整係数。</summary>
    public readonly struct KontributionParams
    {
        /// <summary>抽出効率＝組織化が抽出量を押し上げる強さ（組織的なほど効率的に搾り取る）。</summary>
        public readonly float organizationGain;
        /// <summary>組織化なしの素の抽出比（無組織でも略奪はできるが取りこぼす）。</summary>
        public readonly float baseExtractionRatio;
        /// <summary>抽出が軍の自活へ寄与する強さ（抽出量1で本国財政から完全独立できる係数）。</summary>
        public readonly float selfSufficiencyScale;
        /// <summary>徴発し続けたとき占領地が枯渇する速さ（搾り尽くすほど速く涸れる）。</summary>
        public readonly float depletionRate;
        /// <summary>占領地枯渇が前進圧力を生む強さ（涸れるほど新たな占領地を求めて前進する）。</summary>
        public readonly float advancePressureScale;
        /// <summary>過酷な徴発が占領地を荒廃させる速さ（住民の窮乏・人口流出）。</summary>
        public readonly float devastationRate;
        /// <summary>軍の自活が将軍の政治的自律へ転じる強さ（軍が国家を超える）。</summary>
        public readonly float fiscalIndependenceScale;

        public KontributionParams(float organizationGain, float baseExtractionRatio, float selfSufficiencyScale,
            float depletionRate, float advancePressureScale, float devastationRate, float fiscalIndependenceScale)
        {
            this.organizationGain = Mathf.Clamp01(organizationGain);
            this.baseExtractionRatio = Mathf.Clamp01(baseExtractionRatio);
            this.selfSufficiencyScale = Mathf.Max(0f, selfSufficiencyScale);
            this.depletionRate = Mathf.Max(0f, depletionRate);
            this.advancePressureScale = Mathf.Max(0f, advancePressureScale);
            this.devastationRate = Mathf.Max(0f, devastationRate);
            this.fiscalIndependenceScale = Mathf.Clamp01(fiscalIndependenceScale);
        }

        /// <summary>既定＝組織化寄与0.7・素の抽出比0.3・自活係数1.2・枯渇速度0.25・前進圧力1.0・荒廃速度0.3・財政独立0.8。</summary>
        public static KontributionParams Default => new KontributionParams(0.7f, 0.3f, 1.2f, 0.25f, 1f, 0.3f, 0.8f);
    }

    /// <summary>
    /// コントリビューション制（軍税徴発＝Kontribution）の純ロジック（TYW-1 #1420・三十年戦争）。
    /// 「戦争は戦争を養う（Bellum se ipsum alet）」＝軍隊が占領地から組織的に物資・金銭を強制徴発して自軍を維持する制度
    /// （ヴァレンシュタインが確立）。「軍隊が占領地から組織的に徴発して自活し（<see cref="Extraction"/>→<see cref="ArmySelfSufficiency"/>）、
    /// 徴発し続けると占領地が枯渇し（<see cref="DepletionTick"/>）、枯渇すると新たな占領地を求めて前進する圧力が生まれ
    /// （<see cref="AdvancePressure"/>）、抽出と前進圧力が戦争を自己永続させる（<see cref="WarSelfPerpetuation"/>／<see cref="IsSelfFeedingWar"/>）」
    /// ＝戦争は戦争を養う、を式に出す。過酷な徴発は占領地を荒廃させ（<see cref="OccupiedDevastation"/>）、軍の本国財政からの独立は
    /// 将軍の政治的自律を増す（<see cref="FiscalIndependenceFromState"/>）。
    /// 前線の自律的「現地調達（糧を敵に因る）」は <see cref="ForageRules"/> が、平時の経済戦としての制裁は <see cref="SanctionsRules"/> が
    /// 扱う＝こちらは<b>占領地の組織的搾取が前進圧力を生み戦争を自己永続させる</b>制度のみ。軍事請負将軍が軍の自活ゆえに国家を超える
    /// 力学は <see cref="KriegsherrRules"/>（同 EPIC TYW）が、荒廃が生む難民は <see cref="RefugeeRules"/> が対になって扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class KontributionRules
    {
        /// <summary>
        /// 占領地からの抽出量（0..occupiedWealth）＝占領地の富×（素の抽出比＋組織化×組織化寄与）。
        /// 占領地が豊かなほど・徴発が組織化されているほど多く搾り取れる（ヴァレンシュタインの組織的徴発）。
        /// 組織化0でも素の抽出比ぶんは略奪で得られ、組織化1で 素の抽出比＋組織化寄与 まで効率が上がる。
        /// </summary>
        public static float Extraction(float occupiedWealth, float organizationLevel, KontributionParams p)
        {
            float wealth = Mathf.Clamp01(occupiedWealth);
            float org = Mathf.Clamp01(organizationLevel);
            float ratio = Mathf.Clamp01(p.baseExtractionRatio + org * p.organizationGain);
            return wealth * ratio;
        }

        public static float Extraction(float occupiedWealth, float organizationLevel)
            => Extraction(occupiedWealth, organizationLevel, KontributionParams.Default);

        /// <summary>
        /// 軍の自活度（0..1）＝抽出量×自活係数÷軍規模。抽出で軍が本国財政から独立して自活できる度合い
        /// ＝<b>戦争が戦争を養う</b>。抽出が軍規模に見合えば1（本国に頼らず自活）、足りなければ本国財政が要る。
        /// 軍規模0は養うべき軍がない＝1（自活問題なし）として扱う。
        /// </summary>
        public static float ArmySelfSufficiency(float extraction, float armySize, KontributionParams p)
        {
            float army = Mathf.Clamp01(armySize);
            if (army <= 0f) return 1f; // 養うべき軍がなければ常に自活
            float supply = Mathf.Max(0f, extraction) * p.selfSufficiencyScale;
            return Mathf.Clamp01(supply / army);
        }

        public static float ArmySelfSufficiency(float extraction, float armySize)
            => ArmySelfSufficiency(extraction, armySize, KontributionParams.Default);

        /// <summary>
        /// 占領地の枯渇（徴発後に残る富、0..occupiedWealth）＝富−富×徴発率×枯渇速度×dt。
        /// 徴発し続けると占領地が枯渇する＝搾り尽くすと次の占領地が要る（前進圧力の源）。
        /// </summary>
        public static float DepletionTick(float occupiedWealth, float extractionRate, float dt, KontributionParams p)
        {
            float wealth = Mathf.Clamp01(occupiedWealth);
            if (dt <= 0f) return wealth;
            float rate = Mathf.Clamp01(extractionRate);
            float drain = wealth * rate * p.depletionRate * dt;
            return Mathf.Max(0f, wealth - drain);
        }

        public static float DepletionTick(float occupiedWealth, float extractionRate, float dt)
            => DepletionTick(occupiedWealth, extractionRate, dt, KontributionParams.Default);

        /// <summary>
        /// 前進圧力（0..advancePressureScale）＝軍の自活度×現在の枯渇度×前進圧力係数。
        /// 占領地が枯渇すると新たな占領地を求めて前進する＝<b>止まれない</b>。自活した軍ほど（本国に頼れないぶん）、
        /// 占領地が涸れるほど、前進せずにいられない（戦争の自己永続の駆動）。currentDepletion は枯渇度（1で完全に涸れた）。
        /// </summary>
        public static float AdvancePressure(float armySelfSufficiency, float currentDepletion, KontributionParams p)
        {
            float selfSuff = Mathf.Clamp01(armySelfSufficiency);
            float depletion = Mathf.Clamp01(currentDepletion);
            return selfSuff * depletion * p.advancePressureScale;
        }

        public static float AdvancePressure(float armySelfSufficiency, float currentDepletion)
            => AdvancePressure(armySelfSufficiency, currentDepletion, KontributionParams.Default);

        /// <summary>
        /// 戦争の自己永続度（0..1）＝前進圧力×抽出量。抽出と前進圧力がともに高いとき戦争は自己永続する
        /// ＝<b>終われない戦争</b>。抽出で自活し、枯渇で前進し、前進先でまた抽出する循環が回り続ける。
        /// どちらかが0なら自己永続しない（抽出できなければ自活できず、前進圧力がなければ止まれる）。
        /// </summary>
        public static float WarSelfPerpetuation(float advancePressure, float extraction)
        {
            float pressure = Mathf.Clamp01(advancePressure);
            float ext = Mathf.Clamp01(extraction);
            return pressure * ext;
        }

        /// <summary>
        /// 占領地の荒廃（0..1）＝徴発率×荒廃速度×dt の累積（残らず増える方向の増分を返す）。
        /// 過酷な徴発が占領地を荒廃させる＝住民の窮乏・人口流出。<see cref="RefugeeRules"/> の流出入力になる。
        /// 現在荒廃度 currentDevastation に増分を足して 0..1 にクランプした新しい荒廃度を返す。
        /// </summary>
        public static float OccupiedDevastation(float currentDevastation, float extractionRate, float dt, KontributionParams p)
        {
            float devastation = Mathf.Clamp01(currentDevastation);
            if (dt <= 0f) return devastation;
            float rate = Mathf.Clamp01(extractionRate);
            float increase = rate * p.devastationRate * dt;
            return Mathf.Clamp01(devastation + increase);
        }

        public static float OccupiedDevastation(float currentDevastation, float extractionRate, float dt)
            => OccupiedDevastation(currentDevastation, extractionRate, dt, KontributionParams.Default);

        /// <summary>
        /// 本国財政からの独立度（0..1）＝軍の自活度×財政独立係数。軍が本国財政から独立すると将軍の政治的自律が増す
        /// ＝<b>軍が国家を超える</b>。<see cref="KriegsherrRules"/>（軍事請負将軍）の自律入力になる。
        /// </summary>
        public static float FiscalIndependenceFromState(float armySelfSufficiency, KontributionParams p)
        {
            return Mathf.Clamp01(armySelfSufficiency) * p.fiscalIndependenceScale;
        }

        public static float FiscalIndependenceFromState(float armySelfSufficiency)
            => FiscalIndependenceFromState(armySelfSufficiency, KontributionParams.Default);

        /// <summary>
        /// 戦争が戦争を養い自己永続する状態の判定＝自己永続度が閾値以上。
        /// 抽出と前進圧力が噛み合い、戦争が自力で燃料を調達し続ける＝外から止めない限り終わらない。
        /// </summary>
        public static bool IsSelfFeedingWar(float warSelfPerpetuation, float threshold)
        {
            return Mathf.Clamp01(warSelfPerpetuation) >= Mathf.Clamp01(threshold);
        }
    }
}
