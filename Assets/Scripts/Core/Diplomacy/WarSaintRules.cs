using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 武聖の純ロジック（#武聖・三国志演義＝関羽雲長）。軍神（武聖）として祀られた万人の敵を再現する：
    /// ①<b>限界突破した武勇</b>（実効武勇が100を超える＝万夫不当）、
    /// ②<b>一騎打ちの達人</b>（温酒斬華雄・顔良・文醜を一騎打ちで斬る）、
    /// ③<b>千里行</b>（桃園の誓いゆえ敵に下らず、捕われても主君のもとへ帰参＝五関突破）、
    /// ④<b>傲慢の陥穽</b>（「虎女焉んぞ犬子に嫁がんや＝虎の子はやれぬ」と同盟を拒み、孤立して背後を突かれる＝荊州陥落）。
    /// 上杉謙信の軍神 <see cref="TenchijinRules"/>（天地人の限界突破）とは別の人物 archetype。
    /// 数式は係数を返すだけで `CombatModifiers`#106・`DuelRules`#2316・`CaptivityRules`#154・外交#189 等の既存窓口へ橋渡しする。
    /// 実効値パターン（基準非破壊）・決定論・test-first。
    /// </summary>
    public static class WarSaintRules
    {
        /// <summary>武聖の武勇の限界突破倍率。</summary>
        public const float MartialTranscendMultiplier = 1.3f;
        /// <summary>限界突破した武勇の絶対上限（並は100）。</summary>
        public const int MartialCeiling = 130;
        /// <summary>一騎打ちの強さ倍率（華雄/顔良/文醜を斬る達人）。</summary>
        public const float DuelMastery = 1.5f;
        /// <summary>同盟を失い孤立したときの被ダメ倍率（荊州を背後から突かれる）。</summary>
        public const float IsolationVulnerability = 1.4f;

        /// <summary>実効武勇。武聖は限界突破し100超（<see cref="MartialCeiling"/> まで）。並は100で頭打ち。</summary>
        public static int EffectiveMartial(int martial, bool isWarSaint)
        {
            int m = Mathf.Clamp(martial, 0, 100);
            if (!isWarSaint) return m;
            return Mathf.Min(Mathf.RoundToInt(m * MartialTranscendMultiplier), MartialCeiling);
        }

        /// <summary>一騎打ちの強さ倍率（武聖は達人＝`DuelRules`#2316 の強さに乗る・並は1.0）。</summary>
        public static float DuelStrengthFactor(bool isWarSaint)
            => isWarSaint ? DuelMastery : 1f;

        /// <summary>
        /// 敵の登用を拒むか（千里行＝桃園の誓いゆえ主君に忠で、捕われても下らず帰参する）。
        /// 武聖かつ主君と結ばれている間は true（`CaptivityRules`#154 の登用確率を0にする入口）。
        /// </summary>
        public static bool ResistsEnemyRecruitment(bool isWarSaint, bool bondedToLord)
            => isWarSaint && bondedToLord;

        /// <summary>同盟・縁談を拒むか（虎の子はやれぬ＝傲慢ゆえ外交を蹴る）。荊州孤立の遠因。</summary>
        public static bool RejectsAllianceOffer(bool isWarSaint)
            => isWarSaint;

        /// <summary>
        /// 孤立時の被ダメ倍率（同盟を失い孤立すると背後を突かれる＝荊州陥落・呂蒙の白衣渡江）。
        /// 武聖かつ同盟なしで &gt;1（脆くなる）。同盟があれば1.0。並は1.0。
        /// </summary>
        public static float IsolationVulnerabilityFactor(bool isWarSaint, bool hasAllies)
            => (isWarSaint && !hasAllies) ? IsolationVulnerability : 1f;
    }
}
