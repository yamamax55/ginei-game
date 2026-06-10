using UnityEngine;

namespace Ginei
{
    /// <summary>軍拡競争の調整係数。</summary>
    public readonly struct ArmsRaceParams
    {
        /// <summary>脅威1あたりの対抗建艦の基礎係数（リチャードソンの反応係数）。</summary>
        public readonly float reactionGain;
        /// <summary>猜疑心(0..1)が対抗建艦を増幅する幅（1なら猜疑心最大で反応2倍）。</summary>
        public readonly float paranoiaScale;
        /// <summary>建艦率が自然減衰する速度（経済疲労＝リチャードソンの疲労項・per dt）。</summary>
        public readonly float fatigueRate;
        /// <summary>建艦率の上限（経済が支えられる物理的天井）。</summary>
        public readonly float maxBuildRate;
        /// <summary>経済圧迫の係数（建艦率/経済規模に掛かる）。</summary>
        public readonly float burdenScale;
        /// <summary>相互自制の配当係数（双方が同時に削れた建艦率に掛かる）。</summary>
        public readonly float restraintDividend;

        public ArmsRaceParams(float reactionGain, float paranoiaScale, float fatigueRate,
            float maxBuildRate, float burdenScale, float restraintDividend)
        {
            this.reactionGain = Mathf.Max(0f, reactionGain);
            this.paranoiaScale = Mathf.Max(0f, paranoiaScale);
            this.fatigueRate = Mathf.Clamp01(fatigueRate);
            this.maxBuildRate = Mathf.Max(0f, maxBuildRate);
            this.burdenScale = Mathf.Max(0f, burdenScale);
            this.restraintDividend = Mathf.Max(0f, restraintDividend);
        }

        /// <summary>既定＝反応係数1・猜疑増幅1・疲労0.1・建艦上限10・圧迫係数1・自制配当0.5。</summary>
        public static ArmsRaceParams Default => new ArmsRaceParams(1f, 1f, 0.1f, 10f, 1f, 0.5f);
    }

    /// <summary>
    /// 軍拡競争の純ロジック（安全保障のジレンマ）。相手の建艦に建艦で応えるリチャードソン軍拡モデルの
    /// 簡易形：猜疑心(paranoia)は「相手の現有建艦そのもの」を脅威に算入するため、戦力が拮抗していても
    /// 螺旋は止まらない＝双方の建艦率が上限まで競り上がり、経済圧迫だけが積み上がって
    /// <b>相対優位は変わらない</b>。猜疑心ゼロなら拮抗で疲労項が勝ち自然軍縮する。
    /// 相互自制（軍縮）は双方を富ませる＝囚人のジレンマ（<see cref="GameTheoryRules"/>＝利得の一般形）の
    /// 安全保障版の具体形。<see cref="ShipyardRules"/>（建艦キュー/コスト＝生産そのもの）とは分担し、
    /// ここは「どれだけ建てるか」の意思決定圧力のみを扱う（建艦率は plain な float で受け渡す）。
    /// 抑止（DeterrenceRules＝戦力差が開戦を防ぐ効用）と対：軍拡はその均衡へ至るコストの側。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ArmsRaceRules
    {
        /// <summary>
        /// 脅威認知（0..）＝相手の建艦率が自分を超過するぶん。相手が自分以下なら脅威ゼロ。
        /// 負の建艦率は0に丸める。
        /// </summary>
        public static float ThreatPerception(float enemyBuildRate, float ownBuildRate)
        {
            return Mathf.Max(0f, Mathf.Max(0f, enemyBuildRate) - Mathf.Max(0f, ownBuildRate));
        }

        /// <summary>
        /// 対抗建艦率（0..maxBuildRate）＝脅威×反応係数×（1＋猜疑心×増幅幅）。
        /// 猜疑心が同じ脅威への反応を膨らませる＝螺旋のエンジン。
        /// </summary>
        public static float ReactionBuildRate(float threat, float paranoia, ArmsRaceParams p)
        {
            float reaction = Mathf.Max(0f, threat) * p.reactionGain
                * (1f + Mathf.Clamp01(paranoia) * p.paranoiaScale);
            return Mathf.Clamp(reaction, 0f, p.maxBuildRate);
        }

        public static float ReactionBuildRate(float threat, float paranoia)
            => ReactionBuildRate(threat, paranoia, ArmsRaceParams.Default);

        /// <summary>
        /// 双方の建艦率の1tick後（own, enemy のタプル）。各陣営の認知脅威＝建艦超過分＋猜疑心×相手の建艦率
        /// （猜疑心は相手の存在自体を脅威と見る）→対抗建艦で増、疲労項（fatigueRate×自率）で減。
        /// 猜疑心ありなら拮抗からでも双方が競り上がり、猜疑心ゼロの拮抗は自然に軍縮へ向かう。
        /// </summary>
        public static (float own, float enemy) SpiralTick(
            float ownRate, float enemyRate, float paranoiaA, float paranoiaB, float dt, ArmsRaceParams p)
        {
            float a = Mathf.Clamp(ownRate, 0f, p.maxBuildRate);
            float b = Mathf.Clamp(enemyRate, 0f, p.maxBuildRate);
            float pa = Mathf.Clamp01(paranoiaA);
            float pb = Mathf.Clamp01(paranoiaB);
            float step = Mathf.Max(0f, dt);

            float threatA = ThreatPerception(b, a) + pa * b;
            float threatB = ThreatPerception(a, b) + pb * a;
            float newA = a + (ReactionBuildRate(threatA, pa, p) - p.fatigueRate * a) * step;
            float newB = b + (ReactionBuildRate(threatB, pb, p) - p.fatigueRate * b) * step;
            return (Mathf.Clamp(newA, 0f, p.maxBuildRate), Mathf.Clamp(newB, 0f, p.maxBuildRate));
        }

        public static (float own, float enemy) SpiralTick(
            float ownRate, float enemyRate, float paranoiaA, float paranoiaB, float dt)
            => SpiralTick(ownRate, enemyRate, paranoiaA, paranoiaB, dt, ArmsRaceParams.Default);

        /// <summary>
        /// 軍事費の経済圧迫（0..1）＝建艦率×係数／経済規模。経済規模が0以下なら建艦している限り全圧迫(1)。
        /// 螺旋が回るほどこの値だけが双方で積み上がる＝軍拡の本当のコスト。
        /// </summary>
        public static float EconomicBurden(float buildRate, float economySize, ArmsRaceParams p)
        {
            float rate = Mathf.Max(0f, buildRate);
            if (economySize <= 0f) return rate > 0f ? 1f : 0f;
            return Mathf.Clamp01(rate * p.burdenScale / economySize);
        }

        public static float EconomicBurden(float buildRate, float economySize)
            => EconomicBurden(buildRate, economySize, ArmsRaceParams.Default);

        /// <summary>
        /// 相対優位（0..1）＝自軍総戦力の占有率。0.5=拮抗・1=独占。双方ゼロは拮抗(0.5)とみなす。
        /// 対称な螺旋をいくら回してもこの値は動かない＝軍拡の不毛さの物差し。
        /// </summary>
        public static float RelativeAdvantage(float ownTotal, float enemyTotal)
        {
            float own = Mathf.Max(0f, ownTotal);
            float enemy = Mathf.Max(0f, enemyTotal);
            float sum = own + enemy;
            if (sum <= 0f) return 0.5f;
            return own / sum;
        }

        /// <summary>
        /// 相互自制の配当（0..）＝双方が同時に削減できる建艦率（互いの低い方まで＝相対優位を崩さず削れる分）
        /// ×配当係数。各陣営がこのぶん富む＝軍縮は囚人のジレンマの協調解。片方が既に丸腰なら配当ゼロ。
        /// </summary>
        public static float MutualRestraintGain(float rateA, float rateB, ArmsRaceParams p)
        {
            float a = Mathf.Max(0f, rateA);
            float b = Mathf.Max(0f, rateB);
            return Mathf.Min(a, b) * p.restraintDividend;
        }

        public static float MutualRestraintGain(float rateA, float rateB)
            => MutualRestraintGain(rateA, rateB, ArmsRaceParams.Default);
    }
}
