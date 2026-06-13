using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 作戦線（line of operations）の脆弱性評価の調整係数（JOM-3 #1350・ジョミニ）。
    /// 作戦線＝基地（補給源）から目標へ至る軍の進攻線。長く伸びた作戦線は、その経路上で
    /// 敵の接触・襲撃に晒されるほど脆弱になる（側面を突かれ、連絡線を断たれる）。
    /// </summary>
    public readonly struct LineOfOperationsParams
    {
        /// <summary>作戦線長が脆弱性に効く重み（0..1・長いほど晒される距離が増える）。</summary>
        public readonly float lengthWeight;
        /// <summary>経路上の敵接触脅威が脆弱性に効く重み（0..1・襲撃・側面）。</summary>
        public readonly float exposureWeight;
        /// <summary>側面を突かれるリスクに対する掩護兵力の軽減上限（0..1）。</summary>
        public readonly float screenMitigationMax;
        /// <summary>作戦線沿いの守備が安全度を買う効き（0..1・長いほど割高で薄まる）。</summary>
        public readonly float garrisonEfficacy;
        /// <summary>単一作戦線（集中）の戦力ボーナス重み（0..1・集中の利）。</summary>
        public readonly float concentrationWeight;
        /// <summary>複数作戦線（分散）の冗長性ボーナス重み（0..1・断たれても残る）。</summary>
        public readonly float redundancyWeight;

        public LineOfOperationsParams(float lengthWeight, float exposureWeight, float screenMitigationMax,
            float garrisonEfficacy, float concentrationWeight, float redundancyWeight)
        {
            this.lengthWeight = Mathf.Clamp01(lengthWeight);
            this.exposureWeight = Mathf.Clamp01(exposureWeight);
            this.screenMitigationMax = Mathf.Clamp01(screenMitigationMax);
            this.garrisonEfficacy = Mathf.Clamp01(garrisonEfficacy);
            this.concentrationWeight = Mathf.Clamp01(concentrationWeight);
            this.redundancyWeight = Mathf.Clamp01(redundancyWeight);
        }

        /// <summary>
        /// 既定＝長さ重み0.5・晒され重み0.6・掩護軽減上限0.7・守備効き0.5・集中重み0.4・冗長重み0.5。
        /// </summary>
        public static LineOfOperationsParams Default =>
            new LineOfOperationsParams(0.5f, 0.6f, 0.7f, 0.5f, 0.4f, 0.5f);
    }

    /// <summary>
    /// 作戦線（line of operations）の脆弱性評価の純ロジック（JOM-3 #1350・ジョミニ）。
    /// 作戦線＝基地（補給源）から目標へ至る軍の進攻線。ジョミニは作戦線の選定を重視した。
    /// 長く伸びた作戦線は、その経路上で敵の接触・襲撃に晒されるほど脆弱になる＝側面を突かれ、
    /// 連絡線を断たれる。長さと晒され度で脆弱性が増し、脆弱な作戦線は補給が細り側面を突かれる。
    /// 掩護兵力（スクリーン）と作戦線沿いの守備で安全度を買える（長いほど割高）。
    /// <see cref="SupplyRules"/>（補給源から所有回廊で面が到達するか・ZOC遮断）とは別＝こちらは
    /// 進攻線そのもの（一本の線）の脆弱性評価。
    /// <see cref="HomelandResistanceRules"/>（侵攻軍が敵国深部へ進むほど抵抗が自動増幅）とも別＝
    /// こちらは自軍の作戦線が敵接触に晒される脆弱性に特化。
    /// TurningMovementRules（同EPIC JOM・敵の連絡線を脅かす迂回機動＝攻め手）とは別＝こちらは
    /// 自軍作戦線の脆弱性（守り手の評価）。
    /// <see cref="LogisticsBurdenRules"/>（大軍を遠くへ運ぶほど兵站が超線形に膨らむ）とも別＝
    /// こちらは消費量でなく作戦線が敵に晒される度合いの評価。
    /// 盤面非依存の plain 引数（距離・脅威近接配列）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class LineOfOperationsRules
    {
        /// <summary>
        /// 作戦線の長さ（0..1）。基地→目標の距離（0..1）を長さ重みで効かせて正規化＝
        /// 遠い目標を狙うほど作戦線が長く伸びる。距離0なら長さ0。
        /// </summary>
        public static float LineLength(float baseToObjectiveDistance, LineOfOperationsParams p)
        {
            float dist = Mathf.Clamp01(baseToObjectiveDistance);
            // 長さ重みで線形に効かせつつ、半分は素の距離を残す（重み0でも距離が反映される）
            return Mathf.Clamp01(dist * (0.5f + 0.5f * p.lengthWeight));
        }

        public static float LineLength(float baseToObjectiveDistance)
            => LineLength(baseToObjectiveDistance, LineOfOperationsParams.Default);

        /// <summary>
        /// 経路上の敵接触脅威の集計（0..1）。脅威の近さ配列（各 0..1・1＝至近で危険）を平均し、
        /// 作戦線長（0..1）で重み付け＝長い線ほど同じ脅威でも多くの区間が晒される。
        /// 配列が null/空なら脅威0（敵接触なし）。
        /// </summary>
        public static float ExposureThreats(float[] threatProximities, float lineLength)
        {
            float len = Mathf.Clamp01(lineLength);
            if (threatProximities == null || threatProximities.Length == 0) return 0f;
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < threatProximities.Length; i++)
            {
                sum += Mathf.Clamp01(threatProximities[i]);
                n++;
            }
            float avg = n > 0 ? sum / n : 0f;
            // 長い線ほど晒される＝（0.5＋0.5×線長）で長さを掛ける（線長0でも半分は晒される）
            return Mathf.Clamp01(avg * (0.5f + 0.5f * len));
        }

        /// <summary>
        /// 作戦線の脆弱性（0..1）。線長（0..1）と晒され度（0..1）を各重みで合成＝長く伸びて
        /// 敵接触に晒されるほど脆弱。長さも晒され度も低ければ堅実な作戦線。
        /// </summary>
        public static float LineVulnerability(float lineLength, float exposureThreats,
            LineOfOperationsParams p)
        {
            float len = Mathf.Clamp01(lineLength);
            float exp = Mathf.Clamp01(exposureThreats);
            float wl = p.lengthWeight;
            float we = p.exposureWeight;
            float denom = wl + we;
            if (denom <= 0.0001f) return 0f;
            return Mathf.Clamp01((len * wl + exp * we) / denom);
        }

        public static float LineVulnerability(float lineLength, float exposureThreats)
            => LineVulnerability(lineLength, exposureThreats, LineOfOperationsParams.Default);

        /// <summary>
        /// 脆弱な作戦線による補給の細り（0..1＝喪失割合）。脆弱性（0..1）がそのまま補給の遮断・
        /// 細りに直結＝連絡線を断たれるほど補給が届かない。脆弱性0なら損失0、1なら全断。
        /// </summary>
        public static float SupplyThroughputLoss(float lineVulnerability)
        {
            return Mathf.Clamp01(lineVulnerability);
        }

        /// <summary>
        /// 側面を突かれるリスク（0..1）。経路上の敵接触脅威（0..1）が側面攻撃のリスク源で、
        /// 自軍の掩護兵力（スクリーン・0..1）が軽減上限まで下げる＝掩護で守る。
        /// 掩護が厚いほどリスクは下がるが、軽減には上限がある（掩護だけでは0にできない）。
        /// </summary>
        public static float FlankingRisk(float exposureThreats, float ownScreenForce,
            LineOfOperationsParams p)
        {
            float exp = Mathf.Clamp01(exposureThreats);
            float screen = Mathf.Clamp01(ownScreenForce);
            float mitigation = screen * p.screenMitigationMax;
            return Mathf.Clamp01(exp * (1f - mitigation));
        }

        public static float FlankingRisk(float exposureThreats, float ownScreenForce)
            => FlankingRisk(exposureThreats, ownScreenForce, LineOfOperationsParams.Default);

        /// <summary>
        /// 作戦線沿いの守備が買う安全度（0..1）。守備兵力（0..1）が安全度を上げるが、作戦線が
        /// 長い（0..1）ほど同じ兵力でも薄まり割高＝長い線は守りにくい。
        /// 守備兵力÷（1＋線長）で薄め、守備効きを掛ける。線が短ければ少ない守備で安全を買える。
        /// </summary>
        public static float LineSecurityInvestment(float garrisonAlongLine, float lineLength,
            LineOfOperationsParams p)
        {
            float garr = Mathf.Clamp01(garrisonAlongLine);
            float len = Mathf.Clamp01(lineLength);
            float perUnit = garr / (1f + len); // 長い線ほど守備が薄まる
            return Mathf.Clamp01(perUnit * p.garrisonEfficacy * 2f);
        }

        public static float LineSecurityInvestment(float garrisonAlongLine, float lineLength)
            => LineSecurityInvestment(garrisonAlongLine, lineLength, LineOfOperationsParams.Default);

        /// <summary>
        /// 単一作戦線（集中）vs複数作戦線（分散）の適性（-1..1）。正＝単一が有利（集中の利・全戦力を
        /// 一本に乗せて打撃力最大、ただし断たれると全滅）、負＝複数が有利（分散だが冗長＝一本断たれても残る）。
        /// 作戦線本数（1=単一・2以上=複数）と自軍戦力（0..1）から、集中ボーナスと冗長ボーナスの差で出す。
        /// 戦力が大きいほど複数線に分けても各線が成立し冗長が活きる。
        /// </summary>
        public static float SingleVsDoubleLine(int lineCount, float ownForce, LineOfOperationsParams p)
        {
            float force = Mathf.Clamp01(ownForce);
            int lines = Mathf.Max(1, lineCount);
            if (lines <= 1)
            {
                // 単一線＝集中の利（全戦力を一本に乗せる）
                return Mathf.Clamp(force * p.concentrationWeight, -1f, 1f);
            }
            // 複数線＝冗長（本数で冗長が増すが、戦力が薄れると各線が弱る）
            float perLineForce = force / lines; // 分散して各線が薄くなる
            float redundancy = (1f - 1f / lines) * p.redundancyWeight; // 本数が多いほど冗長度↑
            float concentration = perLineForce * p.concentrationWeight; // 各線の集中度は薄れる
            return Mathf.Clamp(concentration - redundancy, -1f, 1f);
        }

        public static float SingleVsDoubleLine(int lineCount, float ownForce)
            => SingleVsDoubleLine(lineCount, ownForce, LineOfOperationsParams.Default);

        /// <summary>
        /// 作戦線が危険な脆弱性に達したか＝脆弱性（0..1）が閾値を超えたら true。
        /// 危険な作戦線は補給を断たれ側面を突かれる前に、短縮・守備強化・撤退などの是正を要する。
        /// </summary>
        public static bool IsVulnerableLine(float lineVulnerability, float threshold = 0.6f)
        {
            float vuln = Mathf.Clamp01(lineVulnerability);
            float thr = Mathf.Clamp01(threshold);
            return vuln > thr;
        }
    }
}
