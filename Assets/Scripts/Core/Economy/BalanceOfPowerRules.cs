using UnityEngine;

namespace Ginei
{
    /// <summary>多極均衡・勢力均衡圧力（#1103）の調整係数。</summary>
    public readonly struct BalanceOfPowerParams
    {
        /// <summary>一強と見なし始める突出度の閾値（最大国力の全体シェアがこれ未満なら連衡圧力ゼロ＝皆が動かない）。</summary>
        public readonly float hegemonThreshold;
        /// <summary>連衡圧力の鋭さ（突出が閾値を超えてからどれだけ急に弱小が結束するか）。</summary>
        public readonly float coalitionSharpness;
        /// <summary>バンドワゴンに占める「連衡の信頼性の欠如」の重み（連衡が頼りないほど勝ち馬に乗る）。</summary>
        public readonly float bandwagonCredibilityWeight;
        /// <summary>均衡回帰の速さ（圧力が一強の国力をどれだけの率で削り弱小へ移すか・dt当たり）。</summary>
        public readonly float equilibriumRate;

        public BalanceOfPowerParams(float hegemonThreshold, float coalitionSharpness, float bandwagonCredibilityWeight, float equilibriumRate)
        {
            this.hegemonThreshold = Mathf.Clamp01(hegemonThreshold);
            this.coalitionSharpness = Mathf.Max(0.01f, coalitionSharpness);
            this.bandwagonCredibilityWeight = Mathf.Clamp01(bandwagonCredibilityWeight);
            this.equilibriumRate = Mathf.Clamp01(equilibriumRate);
        }

        /// <summary>既定＝一強閾値0.4・連衡鋭さ3・バンドワゴン信頼重み0.7・均衡回帰率0.1。</summary>
        public static BalanceOfPowerParams Default => new BalanceOfPowerParams(0.4f, 3f, 0.7f, 0.1f);
    }

    /// <summary>
    /// 多極均衡・勢力均衡圧力の純ロジック（三国志演義の合従連衡・#1103）。複数勢力の世界で
    /// 一強が突出すると、残りの弱小勢力が結束して対抗する＝システムが自動で均衡へ向かう圧力を解く。
    /// 「最強は皆に包囲される」＝多極世界では突出した者が標的になり、力の差が縮む方向へ回帰する。
    /// 連衡（バランシング）が頼りなければ弱小は勝ち馬に乗る（バンドワゴン）。二極・多極は安定し、
    /// 一強は不安定＝突出そのものが均衡を崩す。
    /// <see cref="DiplomacyRules"/>（二国間の opinion・条約状態）とは別＝こちらは多極の<b>システム圧力</b>
    /// （個々の関係でなく勢力分布全体が生む自動均衡）。<see cref="HegemonyRules"/>（覇権移行＝二者間の
    /// 力の交差の罠）とも別＝こちらは「N極の中で最強が皆に抑えられる」分布の力学。
    /// <see cref="BufferStateRules"/>（緩衝国＝二大国に挟まれた一国の生存）とも別＝こちらは
    /// 勢力配列全体の均衡を扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BalanceOfPowerRules
    {
        /// <summary>勢力配列の総国力（負はゼロにクランプ）。</summary>
        public static float TotalPower(float[] powers)
        {
            if (powers == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < powers.Length; i++)
            {
                sum += Mathf.Max(0f, powers[i]);
            }
            return sum;
        }

        /// <summary>最強勢力の添字（同値は先勝ち・空/全ゼロは-1）。</summary>
        public static int HegemonIndex(float[] powers)
        {
            if (powers == null || powers.Length == 0) return -1;
            int best = -1;
            float bestPower = 0f;
            for (int i = 0; i < powers.Length; i++)
            {
                float p = Mathf.Max(0f, powers[i]);
                if (p > bestPower)
                {
                    bestPower = p;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 一強の突出度（0..1）＝最大国力が全体に占める割合。皆が警戒する「一強の度合い」。
        /// 1勢力なら1（独占）、均等に分かれるほど低い（多極＝突出なし）。
        /// </summary>
        public static float HegemonThreat(float[] powers)
        {
            float total = TotalPower(powers);
            if (total <= 0.0001f) return 0f;
            int hi = HegemonIndex(powers);
            if (hi < 0) return 0f;
            return Mathf.Clamp01(Mathf.Max(0f, powers[hi]) / total);
        }

        /// <summary>
        /// 弱小勢力が結束する圧力（0..1）＝一強の突出が閾値を超えるほど残りが連衡へ向かう＝バランシング。
        /// 突出度が閾値以下なら0（皆が動かない）、超えた分を鋭さで増幅し1へ近づく。
        /// 一強が強いほど連衡圧力が強い＝「最強は包囲される」を式に出す。
        /// </summary>
        public static float CoalitionPressure(float[] powers, int hegemonIndex, BalanceOfPowerParams p)
        {
            if (powers == null || hegemonIndex < 0 || hegemonIndex >= powers.Length) return 0f;
            // 一強以外が2勢力未満なら連衡そのものが成立しない（結束する相手がいない）。
            int others = 0;
            for (int i = 0; i < powers.Length; i++)
            {
                if (i == hegemonIndex) continue;
                if (Mathf.Max(0f, powers[i]) > 0.0001f) others++;
            }
            if (others < 2) return 0f;

            float threat = HegemonThreat(powers);
            float excess = threat - p.hegemonThreshold;
            if (excess <= 0f) return 0f;
            // 閾値超過分を鋭さで増幅（突出が大きいほど結束が固まる）。
            return Mathf.Clamp01(excess * p.coalitionSharpness);
        }

        public static float CoalitionPressure(float[] powers, int hegemonIndex)
            => CoalitionPressure(powers, hegemonIndex, BalanceOfPowerParams.Default);

        /// <summary>
        /// 包囲されるべき標的の添字＝最強勢力（皆が警戒する者）。連衡が向かう相手。
        /// <see cref="HegemonIndex"/> と同義だが「均衡圧力の標的」という意味づけの窓口。
        /// </summary>
        public static int BalancingTarget(float[] powers)
            => HegemonIndex(powers);

        /// <summary>
        /// 勝ち馬に乗る誘惑（0..1）＝自勢力が一強に対し弱いほど、かつ連衡が頼りないほど強い＝バンドワゴン。
        /// 連衡の信頼性が高ければ皆で抑える側に回り（バランシング）、低ければ強者に従う（バンドワゴン）。
        /// 自勢力が一強と同等以上なら誘惑はほぼ消える（従う理由がない）。
        /// </summary>
        public static float BandwagonTemptation(float ownPower, float hegemonPower, float balanceCredibility, BalanceOfPowerParams p)
        {
            float own = Mathf.Max(0f, ownPower);
            float heg = Mathf.Max(0f, hegemonPower);
            if (heg <= 0.0001f) return 0f;
            // 力の劣勢度（0..1）＝相手が圧倒的なほど1へ。
            float weakness = Mathf.Clamp01(1f - own / heg);
            float credibility = Mathf.Clamp01(balanceCredibility);
            // 連衡が頼りない（信頼の欠如）ほどバンドワゴンへ傾く。
            float unreliability = (1f - p.bandwagonCredibilityWeight) + p.bandwagonCredibilityWeight * (1f - credibility);
            return Mathf.Clamp01(weakness * unreliability);
        }

        public static float BandwagonTemptation(float ownPower, float hegemonPower, float balanceCredibility)
            => BandwagonTemptation(ownPower, hegemonPower, balanceCredibility, BalanceOfPowerParams.Default);

        /// <summary>
        /// 多極システムの安定度（0..1）＝1−一強の突出度。二極・多極（力が分散）は安定、
        /// 一強（突出）は不安定＝突出が均衡を崩す。総国力ゼロは中立で1扱い。
        /// </summary>
        public static float SystemStability(float[] powers)
        {
            float total = TotalPower(powers);
            if (total <= 0.0001f) return 1f;
            return Mathf.Clamp01(1f - HegemonThreat(powers));
        }

        /// <summary>
        /// 均衡への動き＝連衡圧力が一強の国力を削り、弱小へ移す方向へ国力配列を更新した新配列を返す
        /// （元配列は非破壊）。圧力が国力差を縮める＝システムは均衡へ回帰する。
        /// 一強から (圧力×回帰率×dt) の割合を取り、残り勢力へ国力比で再分配する（総国力は保存）。
        /// </summary>
        public static float[] EquilibriumShift(float[] powers, float coalitionPressure, float dt, BalanceOfPowerParams p)
        {
            if (powers == null) return new float[0];
            var result = new float[powers.Length];
            for (int i = 0; i < powers.Length; i++)
            {
                result[i] = Mathf.Max(0f, powers[i]);
            }

            int hi = HegemonIndex(result);
            float pressure = Mathf.Clamp01(coalitionPressure);
            float step = Mathf.Max(0f, dt);
            if (hi < 0 || pressure <= 0f || step <= 0f) return result;

            // 一強以外の総国力（再分配の母数）。
            float othersTotal = 0f;
            for (int i = 0; i < result.Length; i++)
            {
                if (i != hi) othersTotal += result[i];
            }
            if (othersTotal <= 0.0001f) return result; // 移す先がない＝独占はそのまま。

            float transfer = result[hi] * Mathf.Clamp01(pressure * p.equilibriumRate * step);
            result[hi] -= transfer;
            // 弱小へ国力比で再分配（差を縮める＝均衡へ）。
            for (int i = 0; i < result.Length; i++)
            {
                if (i == hi) continue;
                result[i] += transfer * (result[i] / othersTotal);
            }
            return result;
        }

        public static float[] EquilibriumShift(float[] powers, float coalitionPressure, float dt)
            => EquilibriumShift(powers, coalitionPressure, dt, BalanceOfPowerParams.Default);
    }
}
