using UnityEngine;

namespace Ginei
{
    /// <summary>近代化プログラムの調整係数（SKUN-1 #1431・マジックナンバー禁止＝Params＋Default に集約）。</summary>
    public readonly struct ModernizationProgramParams
    {
        /// <summary>後発の利益の最大ボーナス（差が最大のとき模倣加速がどれだけ乗るか）。</summary>
        public readonly float backnessAdvantageScale;
        /// <summary>三分野同時投資の多面加速の最大寄与（総合投資が満点のときの加速度）。</summary>
        public readonly float multiFrontScale;
        /// <summary>国家主導の動員が近代化を後押しする最大倍率（後発国は国家が引っ張る）。</summary>
        public readonly float statePushScale;
        /// <summary>近代化が時間で進む基準速度（per dt・加速度1のとき不足分へ向けて漸近）。</summary>
        public readonly float modernizationRate;
        /// <summary>急激な近代化が社会の吸収能力を超えたときの歪みの強さ。</summary>
        public readonly float overstretchScale;

        public ModernizationProgramParams(float backnessAdvantageScale, float multiFrontScale,
                                          float statePushScale, float modernizationRate, float overstretchScale)
        {
            this.backnessAdvantageScale = Mathf.Max(0f, backnessAdvantageScale);
            this.multiFrontScale = Mathf.Max(0f, multiFrontScale);
            this.statePushScale = Mathf.Max(0f, statePushScale);
            this.modernizationRate = Mathf.Max(0f, modernizationRate);
            this.overstretchScale = Mathf.Max(0f, overstretchScale);
        }

        /// <summary>既定＝後発利益0.5・多面加速1.0・国家動員0.5・近代化速度0.05・過伸張0.6。</summary>
        public static ModernizationProgramParams Default
            => new ModernizationProgramParams(0.5f, 1f, 0.5f, 0.05f, 0.6f);
    }

    /// <summary>
    /// 近代化プログラムの純ロジック（SKUN-1 #1431・坂の上の雲型の後発国の富国強兵）。明治日本のように
    /// 後れて出発した国家が、研究（技術導入）・造船（軍備）・人材育成（教育・留学）を同時並行で多面的に
    /// 加速し列強に追いつこうとする国家プロジェクト。核心＝「後発国は先進国との差ゆえ模倣で速く追いつけ
    /// （<see cref="BacknessAdvantage"/>＝ガーシェンクロンの後発性の利益）、研究・造船・人材を同時に多面
    /// 加速して（<see cref="MultiFrontAcceleration"/>）富国強兵するが、偏ると一分野が他の足を引っ張り
    /// （<see cref="BalancedDevelopment"/>＝リービッヒの最小律）、急激すぎると社会の吸収能力を超えて歪む
    /// （<see cref="OverstretchStrain"/>）」。国家主導の動員が後押しする（<see cref="StatePushFactor"/>）。
    /// 改革（<see cref="DynastyRules.Reform"/>／<see cref="Regime"/>）と連動する。
    /// 研究の進捗（<see cref="ResearchRules"/>）／造船（<see cref="ShipyardRules"/>）／人材育成（<see cref="EducationRules"/>）
    /// とは別系統＝後発国の富国強兵の多面加速（研究×造船×人材の同時推進）の合成のみを扱う。
    /// 全入力クランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ModernizationProgramRules
    {
        /// <summary>
        /// 後発の利益（1..1+backnessAdvantageScale）。先進国との発展差(developmentGap 0..1)が大きいほど
        /// 模倣で速く追いつける＝ガーシェンクロンの後発性の利益。差ゼロ（追いついた国）はボーナスなし。
        /// </summary>
        public static float BacknessAdvantage(float developmentGap, ModernizationProgramParams p)
        {
            float gap = Mathf.Clamp01(developmentGap);
            return 1f + p.backnessAdvantageScale * gap;
        }

        public static float BacknessAdvantage(float developmentGap)
            => BacknessAdvantage(developmentGap, ModernizationProgramParams.Default);

        /// <summary>
        /// 三分野（研究・造船・人材）への同時投資による多面加速（0..multiFrontScale）。一点でなく総合＝
        /// 三分野の総合投資水準に <see cref="BalancedDevelopment"/> を掛けて、偏りで割り引く。
        /// 各 invest は 0..1。
        /// </summary>
        public static float MultiFrontAcceleration(float researchInvest, float shipyardInvest, float talentInvest,
                                                   ModernizationProgramParams p)
        {
            float r = Mathf.Clamp01(researchInvest);
            float s = Mathf.Clamp01(shipyardInvest);
            float t = Mathf.Clamp01(talentInvest);
            float total = (r + s + t) / 3f;                                   // 総合投資水準
            float balance = BalancedDevelopment(researchInvest, shipyardInvest, talentInvest);
            return p.multiFrontScale * total * balance;
        }

        public static float MultiFrontAcceleration(float researchInvest, float shipyardInvest, float talentInvest)
            => MultiFrontAcceleration(researchInvest, shipyardInvest, talentInvest, ModernizationProgramParams.Default);

        /// <summary>
        /// 三分野のバランス（0..1）。偏ると一分野が他の足を引っ張る＝リービッヒの最小律。
        /// 最弱分野/最強分野の比で測る（全て均等なら1.0、一分野ゼロなら0）。全ゼロは0。
        /// </summary>
        public static float BalancedDevelopment(float researchInvest, float shipyardInvest, float talentInvest)
        {
            float r = Mathf.Clamp01(researchInvest);
            float s = Mathf.Clamp01(shipyardInvest);
            float t = Mathf.Clamp01(talentInvest);
            float min = Mathf.Min(r, Mathf.Min(s, t));
            float max = Mathf.Max(r, Mathf.Max(s, t));
            if (max <= 0f) return 0f;                                          // 全分野ゼロ＝バランスなし
            return Mathf.Clamp01(min / max);                                  // 最小律＝最弱が全体を縛る
        }

        /// <summary>
        /// 国家主導の動員が近代化を後押しする倍率（1..1+statePushScale）。後発国は国家が引っ張る＝
        /// 国家の関与(stateCommitment 0..1)と財政動員(fiscalMobilization 0..1)の積で効く
        /// （関与だけでも財源だけでも足りない＝両輪）。
        /// </summary>
        public static float StatePushFactor(float stateCommitment, float fiscalMobilization,
                                            ModernizationProgramParams p)
        {
            float commit = Mathf.Clamp01(stateCommitment);
            float fiscal = Mathf.Clamp01(fiscalMobilization);
            return 1f + p.statePushScale * commit * fiscal;
        }

        public static float StatePushFactor(float stateCommitment, float fiscalMobilization)
            => StatePushFactor(stateCommitment, fiscalMobilization, ModernizationProgramParams.Default);

        /// <summary>
        /// 近代化水準の1tick後の値（0..1）。多面加速(multiFrontAcceleration)に応じて、上限1へ向けて
        /// 不足分を漸近的に埋める（rate×加速度×(1-level)×dt）。非正の dt は据え置き。
        /// 後発の利益・国家の後押しは加速度の側に掛け込んで渡す想定（実効値パターン・基準非破壊）。
        /// </summary>
        public static float ModernizationTick(float modernizationLevel, float multiFrontAcceleration, float dt,
                                              ModernizationProgramParams p)
        {
            float level = Mathf.Clamp01(modernizationLevel);
            float accel = Mathf.Max(0f, multiFrontAcceleration);
            float d = Mathf.Max(0f, dt);
            level += p.modernizationRate * accel * (1f - level) * d;          // 上限1へ漸近
            return Mathf.Clamp01(level);
        }

        public static float ModernizationTick(float modernizationLevel, float multiFrontAcceleration, float dt)
            => ModernizationTick(modernizationLevel, multiFrontAcceleration, dt, ModernizationProgramParams.Default);

        /// <summary>
        /// 過伸張の歪み（0..overstretchScale）。急激な近代化のペース(modernizationPace 0..1)が社会の
        /// 吸収能力(socialAbsorption 0..1)を超えた分だけ歪みが生じる＝伝統との軋轢・財政負担。
        /// ペースが吸収能力以下なら歪みゼロ（社会が消化できる範囲）。
        /// </summary>
        public static float OverstretchStrain(float modernizationPace, float socialAbsorption,
                                              ModernizationProgramParams p)
        {
            float pace = Mathf.Clamp01(modernizationPace);
            float absorb = Mathf.Clamp01(socialAbsorption);
            float excess = Mathf.Max(0f, pace - absorb);                      // 吸収を超えた分のみ歪む
            return p.overstretchScale * excess;
        }

        public static float OverstretchStrain(float modernizationPace, float socialAbsorption)
            => OverstretchStrain(modernizationPace, socialAbsorption, ModernizationProgramParams.Default);

        /// <summary>
        /// 列強にどれだけ近づいたか（0..1）。自国の近代化水準(modernizationLevel)を先進国の水準
        /// (leaderLevel 0..1)で割った相対位置。leaderLevel が0以下なら1（比較相手なし＝追いついた扱い）、
        /// 追い越しても1でクランプ。
        /// </summary>
        public static float CatchUpProximity(float modernizationLevel, float leaderLevel)
        {
            float level = Mathf.Clamp01(modernizationLevel);
            float leader = Mathf.Clamp01(leaderLevel);
            if (leader <= 0f) return 1f;                                      // 比較相手なし＝追いついた
            return Mathf.Clamp01(level / leader);
        }

        /// <summary>
        /// バランスよく近代化に成功したか。近代化水準とバランス発展がともに閾値(threshold)以上＝
        /// 偏らず多面的に列強の域へ達した判定。一分野偏重の見かけの近代化は成功と数えない。
        /// </summary>
        public static bool IsSuccessfulModernization(float modernizationLevel, float balancedDevelopment,
                                                     float threshold = 0.7f)
        {
            float th = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(modernizationLevel) >= th
                && Mathf.Clamp01(balancedDevelopment) >= th;
        }
    }
}
