using UnityEngine;

namespace Ginei
{
    /// <summary>覇権移行（トゥキディデスの罠）の調整係数。</summary>
    public readonly struct HegemonyParams
    {
        /// <summary>危険度の山が最も尖る力の比（既定1.0＝力の交差点で開戦確率がピーク）。</summary>
        public readonly float dangerPeakRatio;
        /// <summary>危険度の山の鋭さ（大きいほど交差点以外で急速に安定する）。</summary>
        public readonly float dangerSharpness;
        /// <summary>覇権国の恐怖に占める「台頭の速さ」の重み（急成長ほど予防戦争へ傾く＝スパルタの恐怖）。</summary>
        public readonly float fearRateWeight;
        /// <summary>台頭国の強硬化に占める「現秩序への不満」の重み（力＋不満＝アテネの野心）。</summary>
        public readonly float assertivenessGrievanceWeight;
        /// <summary>制度的紐帯が罠を緩める強さ（相互依存・国際制度で平和的移行の余地が広がる）。</summary>
        public readonly float institutionEasing;

        public HegemonyParams(float dangerPeakRatio, float dangerSharpness, float fearRateWeight, float assertivenessGrievanceWeight, float institutionEasing)
        {
            this.dangerPeakRatio = Mathf.Max(0.01f, dangerPeakRatio);
            this.dangerSharpness = Mathf.Max(0.01f, dangerSharpness);
            this.fearRateWeight = Mathf.Clamp01(fearRateWeight);
            this.assertivenessGrievanceWeight = Mathf.Clamp01(assertivenessGrievanceWeight);
            this.institutionEasing = Mathf.Clamp01(institutionEasing);
        }

        /// <summary>既定＝山の頂点比1.0・鋭さ8・速さ重み0.5・不満重み0.5・制度緩和0.6。</summary>
        public static HegemonyParams Default => new HegemonyParams(1f, 8f, 0.5f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 覇権移行（トゥキディデスの罠）の純ロジック。台頭国と覇権国の力の「交差の瞬間」に
    /// 開戦確率が最大化する＝追い越しの瞬間が最も危ない。力の比が1.0付近（並ぶ瞬間）で
    /// 危険度が山形にピークし、大差（圧倒的優劣）なら安定する。覇権国は急速な台頭を恐れて
    /// 予防戦争（今叩かねば手遅れ）へ傾き（スパルタの恐怖）、台頭国は力をつけるほど既存秩序へ
    /// 不満を募らせ強硬化する（アテネの野心）。制度的紐帯・相互依存はこの罠を緩め、
    /// 歴史上の16事例中4事例は平和的に移行した。
    /// ArmsRaceRules（軍拡の螺旋＝相互の建艦競争という量の積み上げ）とは別系統＝こちらは
    /// 「力の序列そのものが入れ替わる構造的遷移」を解く。DeterrenceRules（持つ力が損得勘定を
    /// どう変えるか）とも別＝こちらは「力の交差が戦争を呼ぶ」遷移の力学。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HegemonyRules
    {
        /// <summary>
        /// 力の比（0..∞）＝台頭国の力／覇権国の力。1.0で並ぶ＝追い越しの瞬間。
        /// 覇権国の力が0以下なら台頭国が圧倒（大きな比でクランプ＝交差済み）。
        /// </summary>
        public static float PowerRatio(float risingPower, float hegemonPower)
        {
            float rise = Mathf.Max(0f, risingPower);
            float heg = Mathf.Max(0f, hegemonPower);
            if (heg <= 0.0001f) return rise <= 0.0001f ? 1f : 100f;
            return rise / heg;
        }

        /// <summary>
        /// 覇権移行の危険度（0..1）＝力の比が頂点比(既定1.0)に近いほど高い山形。
        /// 並ぶ瞬間（交差点）で開戦確率がピーク、大差なら急速に安定する
        /// ＝「追い越しの瞬間が最も危ない」をガウス型の山で表す。
        /// </summary>
        public static float TransitionDanger(float powerRatio, HegemonyParams p)
        {
            float r = Mathf.Max(0f, powerRatio);
            float d = r - p.dangerPeakRatio;
            return Mathf.Clamp01(Mathf.Exp(-p.dangerSharpness * d * d));
        }

        public static float TransitionDanger(float powerRatio)
            => TransitionDanger(powerRatio, HegemonyParams.Default);

        /// <summary>
        /// 覇権国の恐怖（0..1）＝移行危険度×(基礎＋台頭の速さ×速さ重み)。
        /// 急速に追い上げられるほど「今叩かねば手遅れ」の予防戦争へ傾く＝スパルタの恐怖。
        /// 力の交差が近いほど（危険度が高いほど）恐怖は増幅される。
        /// </summary>
        public static float HegemonFear(float powerRatio, float rateOfChange, HegemonyParams p)
        {
            float danger = TransitionDanger(powerRatio, p);
            float rate = Mathf.Clamp01(rateOfChange);
            float urgency = (1f - p.fearRateWeight) + p.fearRateWeight * rate;
            return Mathf.Clamp01(danger * urgency);
        }

        public static float HegemonFear(float powerRatio, float rateOfChange)
            => HegemonFear(powerRatio, rateOfChange, HegemonyParams.Default);

        /// <summary>
        /// 台頭国の強硬化（0..1）＝(比で測る台頭の度合い)×(基礎＋現秩序への不満×不満重み)。
        /// 力をつけた新興国ほど既存秩序の取り分に不満を抱き要求を強める＝アテネの野心。
        /// 比が1を超え追い越すほど強硬化が立ち上がる。
        /// </summary>
        public static float RisingPowerAssertiveness(float powerRatio, float grievance, HegemonyParams p)
        {
            // 比0で0、比1付近で台頭が顕在化し、超過でさらに強まる（上限1）。
            float reach = Mathf.Clamp01(Mathf.Max(0f, powerRatio));
            float g = Mathf.Clamp01(grievance);
            float drive = (1f - p.assertivenessGrievanceWeight) + p.assertivenessGrievanceWeight * g;
            return Mathf.Clamp01(reach * drive);
        }

        public static float RisingPowerAssertiveness(float powerRatio, float grievance)
            => RisingPowerAssertiveness(powerRatio, grievance, HegemonyParams.Default);

        /// <summary>
        /// 予防戦争の誘惑（0..1）＝覇権国の恐怖×(基礎＋機会の窓が閉じる切迫)。
        /// 今叩けば勝てるが時が経てば追い越され手遅れになる＝窓が閉じる前の先制衝動。
        /// 恐怖がなければ誘惑も生まれない（積構造）。
        /// </summary>
        public static float PreventiveWarTemptation(float hegemonFear, float windowClosing)
        {
            float fear = Mathf.Clamp01(hegemonFear);
            float window = Mathf.Clamp01(windowClosing);
            // 切迫が増すほど誘惑が増幅（窓全開で2倍まで、ただし上限1にクランプ）。
            return Mathf.Clamp01(fear * (1f + window));
        }

        /// <summary>
        /// 平和的移行の可能性（0..1）＝(1−移行危険度)を制度的紐帯が押し上げる。
        /// 制度・相互依存が強いほど力の交差の罠を緩め、戦わずに序列が入れ替わる
        /// ＝16事例中4事例の平和的移行を表す。紐帯ゼロなら危険度の裏返しのまま。
        /// </summary>
        public static float PeacefulTransitionChance(float powerRatio, float institutionalBinding, HegemonyParams p)
        {
            float danger = TransitionDanger(powerRatio, p);
            float baseChance = 1f - danger;
            float binding = Mathf.Clamp01(institutionalBinding);
            // 制度が危険度ぶんの一部を平和側へ取り戻す。
            float eased = baseChance + danger * binding * p.institutionEasing;
            return Mathf.Clamp01(eased);
        }

        public static float PeacefulTransitionChance(float powerRatio, float institutionalBinding)
            => PeacefulTransitionChance(powerRatio, institutionalBinding, HegemonyParams.Default);
    }
}
