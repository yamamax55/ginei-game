using UnityEngine;

namespace Ginei
{
    /// <summary>アサビーヤ（集団的連帯）の調整係数（イブン・ハルドゥーン型）。</summary>
    public readonly struct AsabiyyaParams
    {
        /// <summary>繁栄0でも進む基礎の世代減衰/秒（建国の記憶の自然な風化）。</summary>
        public readonly float baseDecayRate;
        /// <summary>繁栄(0..1)が紐帯減衰を増幅する幅（豊かさが連帯を要らなくする）。</summary>
        public readonly float prosperityDecayScale;
        /// <summary>世代を経るほど減衰が増す係数（建国の苦労を知らない世代ほど薄い）。</summary>
        public readonly float generationDecayScale;
        /// <summary>奢侈の蓄積/秒の基礎（繁栄1のとき）。</summary>
        public readonly float luxuryGrowthRate;
        /// <summary>王朝の自然寿命＝この世代数で力尽きる（ハルドゥーンの三〜四世代）。</summary>
        public readonly float dynastyLifespanGenerations;
        /// <summary>新興勢力が逆転に要する紐帯の優位差（これ以上の差で中枢を倒せる）。</summary>
        public readonly float challengerMargin;

        public AsabiyyaParams(float baseDecayRate, float prosperityDecayScale, float generationDecayScale,
            float luxuryGrowthRate, float dynastyLifespanGenerations, float challengerMargin)
        {
            this.baseDecayRate = Mathf.Max(0f, baseDecayRate);
            this.prosperityDecayScale = Mathf.Max(0f, prosperityDecayScale);
            this.generationDecayScale = Mathf.Max(0f, generationDecayScale);
            this.luxuryGrowthRate = Mathf.Max(0f, luxuryGrowthRate);
            this.dynastyLifespanGenerations = Mathf.Max(1f, dynastyLifespanGenerations);
            this.challengerMargin = Mathf.Max(0f, challengerMargin);
        }

        /// <summary>
        /// 既定＝基礎減衰0.02・繁栄増幅0.05・世代増幅0.01・奢侈成長0.05・寿命4世代・逆転差0.1。
        /// 寿命4世代＝1世代30年なら約120年で爛熟＝王朝の自然寿命。
        /// </summary>
        public static AsabiyyaParams Default => new AsabiyyaParams(0.02f, 0.05f, 0.01f, 0.05f, 4f, 0.1f);
    }

    /// <summary>
    /// アサビーヤ（集団的連帯）の純ロジック（イブン・ハルドゥーン型）。建国世代の強い紐帯が辺境の新興勢力に
    /// 中枢を奪わせる原動力だが、その紐帯は<b>繁栄の中で世代ごとに薄れる</b>＝豊かさは連帯を要らなくし、
    /// 建国の苦労を知らない世代が奢侈に溺れて紐帯を腐らせる。爛熟した中枢は、より強い紐帯を持つ辺境の
    /// 新興勢力に取って代わられる＝<b>王朝に自然寿命がある理由</b>（ハルドゥーンの三世代＝建設者→維持者→
    /// 破壊者）。稀に中興の祖が建国の精神を取り戻す（再生）。
    /// <see cref="DynastyRules"/>（天命・腐敗・正統性＝統治の正当性の喪失）とは分担し、ここは
    /// <b>集団紐帯そのものの世代減衰と、それゆえの軍事的活力・新興勢力の優位</b>を扱う（腐敗の制度疲労
    /// ではなく、連帯の摩耗が主役）。すべて plain な float で受け渡す。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AsabiyyaRules
    {
        /// <summary>
        /// 紐帯の世代減衰（dt後のアサビーヤ 0..1）。減衰率＝基礎＋繁栄×繁栄増幅＋世代数×世代増幅。
        /// 繁栄が高いほど・世代を経るほど速く薄れる＝<b>繁栄が紐帯を溶かす</b>。建国直後（世代0・繁栄0）でも
        /// 基礎ぶんだけは風化する。
        /// </summary>
        public static float AsabiyyaDecayTick(float asabiyya, float prosperity, float generationsSinceFounding,
            float dt, AsabiyyaParams p)
        {
            float a = Mathf.Clamp01(asabiyya);
            float prosp = Mathf.Clamp01(prosperity);
            float gen = Mathf.Max(0f, generationsSinceFounding);
            float step = Mathf.Max(0f, dt);

            float rate = p.baseDecayRate
                + prosp * p.prosperityDecayScale
                + gen * p.generationDecayScale;
            return Mathf.Clamp01(a - rate * step);
        }

        public static float AsabiyyaDecayTick(float asabiyya, float prosperity, float generationsSinceFounding, float dt)
            => AsabiyyaDecayTick(asabiyya, prosperity, generationsSinceFounding, dt, AsabiyyaParams.Default);

        /// <summary>
        /// 軍事的活力（0..1）＝アサビーヤそのもの。強い紐帯＝強い軍＝辺境の新興勢力が爛熟した中枢を倒せる源泉。
        /// 数や富ではなく連帯が戦闘力を決める、というハルドゥーンの核を恒等写像として置く（呼び出し側が
        /// 戦力倍率へ掛ける）。
        /// </summary>
        public static float MilitaryVigor(float asabiyya) => Mathf.Clamp01(asabiyya);

        /// <summary>
        /// 奢侈の蓄積（dt後のluxury 0..1）＝繁栄に比例して贅沢が積み上がる。繁栄が贅沢を生み、贅沢が紐帯を
        /// 腐らせる（この値は呼び出し側で<see cref="AsabiyyaDecayTick"/>の繁栄項に重ねて使える）。繁栄0なら
        /// 蓄積しない。
        /// </summary>
        public static float LuxuryCorruptionTick(float luxury, float prosperity, float dt, AsabiyyaParams p)
        {
            float l = Mathf.Clamp01(luxury);
            float prosp = Mathf.Clamp01(prosperity);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(l + p.luxuryGrowthRate * prosp * step);
        }

        public static float LuxuryCorruptionTick(float luxury, float prosperity, float dt)
            => LuxuryCorruptionTick(luxury, prosperity, dt, AsabiyyaParams.Default);

        /// <summary>
        /// 王朝の世代サイクル（0..1＝寿命の進捗）。経過世代÷自然寿命世代。0=建国直後、1=寿命到達（爛熟）。
        /// ハルドゥーンの三世代（建設者→維持者→破壊者）を一本の進捗として表す。寿命を超えても1で頭打ち。
        /// </summary>
        public static float DynastyLifecycle(float generationsSinceFounding, AsabiyyaParams p)
        {
            float gen = Mathf.Max(0f, generationsSinceFounding);
            return Mathf.Clamp01(gen / p.dynastyLifespanGenerations);
        }

        public static float DynastyLifecycle(float generationsSinceFounding)
            => DynastyLifecycle(generationsSinceFounding, AsabiyyaParams.Default);

        /// <summary>
        /// 新興勢力の優位（true＝辺境の挑戦者が中枢を倒せる）。挑戦者の紐帯が中枢の紐帯を<see cref="AsabiyyaParams.challengerMargin"/>
        /// 以上上回るとき成立＝<b>爛熟した中枢 vs 紐帯の強い辺境＝後者が勝つ</b>。中枢が薄れるほど低い挑戦者でも
        /// 倒せる。
        /// </summary>
        public static bool ChallengerAdvantage(float incumbentAsabiyya, float challengerAsabiyya, AsabiyyaParams p)
        {
            float inc = Mathf.Clamp01(incumbentAsabiyya);
            float ch = Mathf.Clamp01(challengerAsabiyya);
            return ch - inc >= p.challengerMargin;
        }

        public static bool ChallengerAdvantage(float incumbentAsabiyya, float challengerAsabiyya)
            => ChallengerAdvantage(incumbentAsabiyya, challengerAsabiyya, AsabiyyaParams.Default);

        /// <summary>
        /// 紐帯の再生量（0..1＝アサビーヤの回復見込み）＝改革意志×（1−現アサビーヤ）。薄れた紐帯ほど伸びしろが
        /// 大きく、強い改革意志があれば取り戻せる（中興の祖＝建国の精神への回帰）。だが意志がなければゼロ＝
        /// 自然には戻らない（減衰の一方通行を破るには能動的な刷新が要る）。
        /// </summary>
        public static float RenewalChance(float asabiyya, float reformWill)
        {
            float a = Mathf.Clamp01(asabiyya);
            float will = Mathf.Clamp01(reformWill);
            return Mathf.Clamp01(will * (1f - a));
        }
    }
}
