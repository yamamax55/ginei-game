using UnityEngine;

namespace Ginei
{
    /// <summary>宮廷派閥（朝廷内の党派抗争と勢力均衡）の調整係数。</summary>
    public readonly struct CourtFactionParams
    {
        /// <summary>構成員数の正規化基準（この人数で寄与1.0に飽和）。</summary>
        public readonly float memberNormalize;
        /// <summary>要職掌握数の正規化基準（この数で寄与1.0に飽和）。</summary>
        public readonly float positionNormalize;
        /// <summary>派閥勢力に対する構成員の重み。</summary>
        public readonly float memberWeight;
        /// <summary>派閥勢力に対する要職の重み。</summary>
        public readonly float positionWeight;
        /// <summary>派閥勢力に対する君寵の重み。</summary>
        public readonly float favorWeight;
        /// <summary>抗争の激しさのスケール（拮抗×争点に掛ける）。</summary>
        public readonly float rivalryScale;
        /// <summary>一強の危険のスケール（一強勢力×(1-均衡)に掛ける）。</summary>
        public readonly float hegemonyScale;
        /// <summary>中間派の決定力のスケール（規模×接戦度に掛ける）。</summary>
        public readonly float swingScale;
        /// <summary>政務停滞のスケール（抗争×拮抗に掛ける）。</summary>
        public readonly float paralysisScale;
        /// <summary>君主の派閥操縦のスケール（手腕×均衡に掛ける）。</summary>
        public readonly float manipulationScale;

        public CourtFactionParams(float memberNormalize, float positionNormalize,
                                  float memberWeight, float positionWeight, float favorWeight,
                                  float rivalryScale, float hegemonyScale, float swingScale,
                                  float paralysisScale, float manipulationScale)
        {
            // 0 除算回避のため正規化基準は下限を持たせる
            this.memberNormalize = Mathf.Max(0.0001f, memberNormalize);
            this.positionNormalize = Mathf.Max(0.0001f, positionNormalize);
            this.memberWeight = Mathf.Max(0f, memberWeight);
            this.positionWeight = Mathf.Max(0f, positionWeight);
            this.favorWeight = Mathf.Max(0f, favorWeight);
            this.rivalryScale = Mathf.Max(0f, rivalryScale);
            this.hegemonyScale = Mathf.Max(0f, hegemonyScale);
            this.swingScale = Mathf.Max(0f, swingScale);
            this.paralysisScale = Mathf.Max(0f, paralysisScale);
            this.manipulationScale = Mathf.Max(0f, manipulationScale);
        }

        /// <summary>既定＝構成員基準10/要職基準5・重み各1・各スケール1。</summary>
        public static CourtFactionParams Default
            => new CourtFactionParams(10f, 5f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 宮廷派閥（朝廷内の党派抗争と勢力均衡）の純ロジック。派閥ブロックの勢力・君寵を巡る党派対立・
    /// 勢力均衡と一強の危険・中間派（キャスティングボート）の去就・抗争が政務に与える停滞・
    /// 君主による派閥操縦（分割統治で漁夫の利）を式に出す（銀英伝の門閥貴族／帝国宮廷の党派抗争型）。
    /// 将官個人の確執は OfficerRivalryRules（こちらは宮廷の党派ブロックのマクロ力学）。
    /// 倍率・係数は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし決定論・全入力クランプ。
    /// 純ロジック（非 MonoBehaviour・test-first）。人数は count（≥0）、君寵・各比率は 0..1 を想定。
    /// </summary>
    public static class CourtFactionRules
    {
        /// <summary>
        /// 派閥の勢力（0..1）。構成員 memberCount／要職掌握 keyPositions（各正規化）と君寵 sovereignFavor（0..1）の
        /// 重み付き平均＝頭数だけでなく要職を握り君主に寵されるほど派閥は強い。
        /// </summary>
        public static float FactionPower(float memberCount, float keyPositions, float sovereignFavor, CourtFactionParams p)
        {
            float members = Mathf.Clamp01(Mathf.Max(0f, memberCount) / p.memberNormalize);
            float positions = Mathf.Clamp01(Mathf.Max(0f, keyPositions) / p.positionNormalize);
            float favor = Mathf.Clamp01(sovereignFavor);
            float wSum = p.memberWeight + p.positionWeight + p.favorWeight;
            if (wSum <= 0f) return 0f; // 全重みゼロなら勢力なし
            float raw = (members * p.memberWeight + positions * p.positionWeight + favor * p.favorWeight) / wSum;
            return Mathf.Clamp01(raw);
        }

        public static float FactionPower(float memberCount, float keyPositions, float sovereignFavor)
            => FactionPower(memberCount, keyPositions, sovereignFavor, CourtFactionParams.Default);

        /// <summary>
        /// 抗争の激しさ（0..1）。両派の勢力 factionPowerA/factionPowerB の拮抗（1-|差|）×争点の大きさ stakeContested＝
        /// 勢力が伯仲し争点が大きいほど激しく対立し、一方が圧倒するか争点が小さければ抗争は鎮まる。
        /// </summary>
        public static float RivalryHeat(float factionPowerA, float factionPowerB, float stakeContested, CourtFactionParams p)
        {
            float a = Mathf.Clamp01(factionPowerA);
            float b = Mathf.Clamp01(factionPowerB);
            float parity = 1f - Mathf.Abs(a - b);   // 拮抗度（差が小さいほど1）
            float stake = Mathf.Clamp01(stakeContested);
            return Mathf.Clamp01(parity * stake * p.rivalryScale);
        }

        public static float RivalryHeat(float factionPowerA, float factionPowerB, float stakeContested)
            => RivalryHeat(factionPowerA, factionPowerB, stakeContested, CourtFactionParams.Default);

        /// <summary>
        /// 勢力均衡度（0..1）。両派の勢力差から 1-|差|＝拮抗で1・一強で0へ。
        /// 二大勢力が伯仲すれば均衡が保たれ、片方が突出すれば均衡は崩れる。
        /// </summary>
        public static float BalanceOfPower(float factionPowerA, float factionPowerB, CourtFactionParams p)
        {
            float a = Mathf.Clamp01(factionPowerA);
            float b = Mathf.Clamp01(factionPowerB);
            return Mathf.Clamp01(1f - Mathf.Abs(a - b));
        }

        public static float BalanceOfPower(float factionPowerA, float factionPowerB)
            => BalanceOfPower(factionPowerA, factionPowerB, CourtFactionParams.Default);

        /// <summary>
        /// 一強支配の危険（0..1）。突出した派閥の勢力 dominantFactionPower×(1-均衡 balanceOfPower)＝
        /// 強い派閥が均衡を崩して君権に迫るほど一党支配の危険が高まる（一強の専横・簒奪の芽）。
        /// </summary>
        public static float HegemonyRisk(float dominantFactionPower, float balanceOfPower, CourtFactionParams p)
        {
            float power = Mathf.Clamp01(dominantFactionPower);
            float imbalance = 1f - Mathf.Clamp01(balanceOfPower);
            return Mathf.Clamp01(power * imbalance * p.hegemonyScale);
        }

        public static float HegemonyRisk(float dominantFactionPower, float balanceOfPower)
            => HegemonyRisk(dominantFactionPower, balanceOfPower, CourtFactionParams.Default);

        /// <summary>
        /// 中間派の決定力（0..1）。中間派の規模 neutralBlocSize×接戦度 contestCloseness＝
        /// 二大派閥が接戦であるほど中立ブロックの去就が勝敗を決める（キャスティングボート＝去就が情勢を左右）。
        /// </summary>
        public static float SwingFactionLeverage(float neutralBlocSize, float contestCloseness, CourtFactionParams p)
        {
            float size = Mathf.Clamp01(neutralBlocSize);
            float close = Mathf.Clamp01(contestCloseness);
            return Mathf.Clamp01(size * close * p.swingScale);
        }

        public static float SwingFactionLeverage(float neutralBlocSize, float contestCloseness)
            => SwingFactionLeverage(neutralBlocSize, contestCloseness, CourtFactionParams.Default);

        /// <summary>
        /// 政務の停滞（0..1）。抗争 rivalryHeat×拮抗 balanceOfPower＝
        /// 激しい抗争が互角のまま膠着するほど政務が止まる（決着がつかず党争に明け暮れ国政が動かない）。
        /// </summary>
        public static float GovernanceParalysis(float rivalryHeat, float balanceOfPower, CourtFactionParams p)
        {
            float heat = Mathf.Clamp01(rivalryHeat);
            float balance = Mathf.Clamp01(balanceOfPower);
            return Mathf.Clamp01(heat * balance * p.paralysisScale);
        }

        public static float GovernanceParalysis(float rivalryHeat, float balanceOfPower)
            => GovernanceParalysis(rivalryHeat, balanceOfPower, CourtFactionParams.Default);

        /// <summary>
        /// 君主の派閥操縦（0..1）。君主の手腕 sovereignSkill×均衡 balanceOfPower＝
        /// 諸派が拮抗しているほど名君は分割統治で漁夫の利を得て君権を保つ（一強なら君主も操縦できず呑まれる）。
        /// </summary>
        public static float SovereignManipulation(float sovereignSkill, float balanceOfPower, CourtFactionParams p)
        {
            float skill = Mathf.Clamp01(sovereignSkill);
            float balance = Mathf.Clamp01(balanceOfPower);
            return Mathf.Clamp01(skill * balance * p.manipulationScale);
        }

        public static float SovereignManipulation(float sovereignSkill, float balanceOfPower)
            => SovereignManipulation(sovereignSkill, balanceOfPower, CourtFactionParams.Default);

        /// <summary>宮廷が安定か（均衡が停滞を上回り、かつ閾値以上）。</summary>
        public static bool IsCourtStable(float balanceOfPower, float governanceParalysis, float threshold)
        {
            float balance = Mathf.Clamp01(balanceOfPower);
            float paralysis = Mathf.Clamp01(governanceParalysis);
            return (balance - paralysis) >= Mathf.Clamp(threshold, -1f, 1f);
        }

        /// <summary>宮廷が安定か（既定閾値0＝均衡が停滞を上回るか）。</summary>
        public static bool IsCourtStable(float balanceOfPower, float governanceParalysis)
            => IsCourtStable(balanceOfPower, governanceParalysis, 0f);
    }
}
