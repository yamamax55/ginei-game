using UnityEngine;

namespace Ginei
{
    /// <summary>統治スタイル＝王道（仁政・徳治）か覇道（武力・強制）か（孟子・MENC-3 #1568）。</summary>
    public enum GovernanceStyle
    {
        王道,
        覇道
    }

    /// <summary>仁政vs覇道の時間動態の調整係数（孟子・MENC-3 #1568）。</summary>
    public readonly struct GovernanceStyleParams
    {
        /// <summary>王道の心服が毎秒積み上がる速さ（徳1のとき・遅効だが盤石）。</summary>
        public readonly float kinglyAccumulationRate;
        /// <summary>覇道の統治力が毎秒陰る速さ（力の維持費1のとき・即効だが減衰）。</summary>
        public readonly float hegemonicDecayRate;
        /// <summary>王道が統治年数で実効統治力を伸ばす幅（age1で +kinglyAgeGain）。</summary>
        public readonly float kinglyAgeGain;
        /// <summary>覇道が統治年数で実効統治力を失う幅（age1で −hegemonicAgeLoss）。</summary>
        public readonly float hegemonicAgeLoss;
        /// <summary>覇道が脆いと判定する持続可能性の下限（これ未満かつ高齢で脆い）。</summary>
        public readonly float brittleThreshold;

        public GovernanceStyleParams(float kinglyAccumulationRate, float hegemonicDecayRate,
            float kinglyAgeGain, float hegemonicAgeLoss, float brittleThreshold)
        {
            this.kinglyAccumulationRate = Mathf.Max(0f, kinglyAccumulationRate);
            this.hegemonicDecayRate = Mathf.Max(0f, hegemonicDecayRate);
            this.kinglyAgeGain = Mathf.Max(0f, kinglyAgeGain);
            this.hegemonicAgeLoss = Mathf.Max(0f, hegemonicAgeLoss);
            this.brittleThreshold = Mathf.Clamp01(brittleThreshold);
        }

        /// <summary>
        /// 既定＝王道蓄積0.1/秒・覇道減衰0.2/秒・王道齢ゲイン0.5・覇道齢ロス0.5・脆さ閾値0.4。
        /// 王道は積みが遅く・覇道は減衰が倍速＝時間が経つほど王道が覇道を追い越す。
        /// </summary>
        public static GovernanceStyleParams Default =>
            new GovernanceStyleParams(0.1f, 0.2f, 0.5f, 0.5f, 0.4f);
    }

    /// <summary>
    /// 仁政vs覇道の<b>時間動態</b>＝孟子「王道（仁政・徳治）は立ち上がりが遅いが長期に持続し、覇道（武力・強制）は
    /// 短期に最強だが長続きしない」の時間トレードオフを式にする（MENC-3 #1568）。覇道は武力×強制で即座に高い統治力を
    /// 得るが（<see cref="HegemonicPower"/>）、力の維持費が要り力が陰れば崩れる（<see cref="HegemonicDecayTick"/>）。
    /// 王道は徳×心服の積み重ねゆえ立ち上がりは低いが（<see cref="KinglyPower"/>）、時間で心服を積んで盤石になる
    /// （<see cref="KinglyAccumulationTick"/>）。統治年数を進めると王道は伸び覇道は陰り、ある時点で優劣が逆転する
    /// （<see cref="EffectivePower"/>／逆転点は <see cref="CrossoverTime"/>）。王道は民の心を得（<see cref="HeartsAndMinds"/>）
    /// 持続可能（<see cref="Sustainability"/>）、覇道は時間で脆くなる（<see cref="IsRegimeBrittle"/>）。
    /// <b>WangDaoRules</b>（王道覇道の主義ドリフトと服従の質・心服vs力服の質）とは別＝こちらは「短期最強vs長期持続」の
    /// <b>時間トレードオフ曲線</b>を解く。<b>GovernanceRules</b>（内政の安定度収束）とも別＝統治スタイルの寿命を扱う。
    /// 同 EPIC の <b>MoralForceRules</b>（浩然之気＝個の精神力）とも分担し、こちらは<b>勢力の統治の時間動態</b>。
    /// 全入力クランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GovernanceStyleRules
    {
        /// <summary>
        /// 覇道の即効的な統治力（0..1）＝武力(0..1)×強制(0..1)。両輪が揃うほど高い＝<b>短期に最強</b>。
        /// 積ゆえ片方が欠けると効かない（武力だけ・強制だけでは押さえ切れない）。立ち上がりは速い。
        /// </summary>
        public static float HegemonicPower(float force, float coercion)
        {
            return Mathf.Clamp01(Mathf.Clamp01(force) * Mathf.Clamp01(coercion));
        }

        /// <summary>
        /// 王道の統治力（0..1）＝徳(0..1)×心服(0..1)。心服は積み重ね（<see cref="KinglyAccumulationTick"/>）で
        /// 徐々にしか上がらないため<b>立ち上がりは低い</b>。だが積もれば徳と相乗して盤石になる（長期に強い）。
        /// </summary>
        public static float KinglyPower(float virtue, float cultivation)
        {
            return Mathf.Clamp01(Mathf.Clamp01(virtue) * Mathf.Clamp01(cultivation));
        }

        /// <summary>
        /// 王道の心服の蓄積（dt後の王道統治力 0..1）。徳(0..1)に比例して毎秒だけ積み上がる＝
        /// <b>王道は時間で心を積んで強くなる（遅効だが盤石）</b>。徳0なら積まない（不仁は心服を得られない）。
        /// </summary>
        public static float KinglyAccumulationTick(float kinglyBase, float virtue, float dt, GovernanceStyleParams p)
        {
            float k = Mathf.Clamp01(kinglyBase);
            float v = Mathf.Clamp01(virtue);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(k + p.kinglyAccumulationRate * v * step);
        }

        public static float KinglyAccumulationTick(float kinglyBase, float virtue, float dt)
            => KinglyAccumulationTick(kinglyBase, virtue, dt, GovernanceStyleParams.Default);

        /// <summary>
        /// 覇道の減衰（dt後の覇道統治力 0..1）。力の維持費(0..1)に比例して毎秒だけ削れる＝
        /// <b>覇道は力が陰れば崩れる（即効だが減衰）</b>。維持費0なら崩れない（が、力を保つ維持費を払い続けるのが
        /// 覇道の宿命）。既定では蓄積より減衰のほうが速い＝時間が覇道の敵。
        /// </summary>
        public static float HegemonicDecayTick(float hegemonicBase, float forceUpkeep, float dt, GovernanceStyleParams p)
        {
            float h = Mathf.Clamp01(hegemonicBase);
            float upkeep = Mathf.Clamp01(forceUpkeep);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(h - p.hegemonicDecayRate * upkeep * step);
        }

        public static float HegemonicDecayTick(float hegemonicBase, float forceUpkeep, float dt)
            => HegemonicDecayTick(hegemonicBase, forceUpkeep, dt, GovernanceStyleParams.Default);

        /// <summary>
        /// 統治年数(age 0..1)に応じた実効統治力（0..1）＝<b>時間軸で優劣が逆転する核</b>。
        /// 王道は年数で伸び（base + age×ageGain）、覇道は年数で陰る（base − age×ageLoss）。
        /// 同じ base でも王道は時の経過が味方・覇道は敵＝<see cref="CrossoverTime"/> で逆転する。
        /// </summary>
        public static float EffectivePower(GovernanceStyle style, float baseLevel, float age, GovernanceStyleParams p)
        {
            float bl = Mathf.Clamp01(baseLevel);
            float a = Mathf.Clamp01(age);
            if (style == GovernanceStyle.王道)
                return Mathf.Clamp01(bl + a * p.kinglyAgeGain);
            return Mathf.Clamp01(bl - a * p.hegemonicAgeLoss);
        }

        public static float EffectivePower(GovernanceStyle style, float baseLevel, float age)
            => EffectivePower(style, baseLevel, age, GovernanceStyleParams.Default);

        /// <summary>
        /// 王道が覇道を追い越す統治年数（&gt;=0）＝<b>時間トレードオフの分岐点</b>。覇道は kinglyRate＋hegemonicDecay
        /// の速さで王道に詰め寄られる（王道は上り・覇道は下りの相対速度）。覇道が初期に勝っているぶんを
        /// 相対速度で割る＝この時点を境に王道が上回る。立ち上がりで王道が既に勝っていれば 0（即座に逆転）。
        /// 相対速度が0（両者とも動かない）なら逆転は来ない＝<see cref="float.PositiveInfinity"/>。
        /// </summary>
        public static float CrossoverTime(float kinglyRate, float hegemonicDecay)
            => CrossoverTime(kinglyRate, hegemonicDecay, 1f);

        /// <summary>
        /// 王道が覇道を追い越す統治年数（&gt;=0）。覇道の初期リード(initialHegemonicLead 0..1)を、王道の上り
        /// (kinglyRate)と覇道の下り(hegemonicDecay)を合わせた相対速度で割る。リード0以下なら即逆転（0）、
        /// 相対速度0なら逆転せず無限大。<b>王道は遅いが必ず追いつく＝時間が長いほど王道有利</b>。
        /// </summary>
        public static float CrossoverTime(float kinglyRate, float hegemonicDecay, float initialHegemonicLead)
        {
            float lead = Mathf.Clamp01(initialHegemonicLead);
            if (lead <= 0f) return 0f;
            float relative = Mathf.Max(0f, kinglyRate) + Mathf.Max(0f, hegemonicDecay);
            if (relative <= 0f) return float.PositiveInfinity;
            return lead / relative;
        }

        /// <summary>
        /// 持続可能性（0..1）。王道は徳(0..1)に比例して高く（心服は崩れない）、覇道は力(0..1)に比例するが
        /// 上限を1未満に抑える＝<b>力に依る統治は本質的に持続しない</b>。同じ高い入力でも王道のほうが必ず持続する。
        /// </summary>
        public static float Sustainability(GovernanceStyle style, float virtue, float force)
        {
            if (style == GovernanceStyle.王道)
                return Mathf.Clamp01(Mathf.Clamp01(virtue));
            // 覇道：力が支えだが、力ずくの統治は持続性の天井が低い（半分まで）。
            return Mathf.Clamp01(Mathf.Clamp01(force) * 0.5f);
        }

        /// <summary>
        /// 王道が民の心を獲得する度合い（dt後の心 0..1）。王道統治力(0..1)に比例して毎秒積み上がる＝
        /// <b>覇道は心を得られない</b>（王道のみが民の心を勝ち取る）。心服こそが王道の持続性の源泉。
        /// </summary>
        public static float HeartsAndMinds(float hearts, float kinglyPower, float dt, GovernanceStyleParams p)
        {
            float h = Mathf.Clamp01(hearts);
            float kp = Mathf.Clamp01(kinglyPower);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(h + p.kinglyAccumulationRate * kp * step);
        }

        public static float HeartsAndMinds(float hearts, float kinglyPower, float dt)
            => HeartsAndMinds(hearts, kinglyPower, dt, GovernanceStyleParams.Default);

        /// <summary>
        /// 覇道が時間で脆くなる判定（true＝政体が脆い）。覇道のみが対象＝統治年数(age 0..1)を重ね、
        /// かつ持続可能性が <see cref="GovernanceStyleParams.brittleThreshold"/> を下回ると脆い＝
        /// <b>覇道は時の経過とともに崩れやすくなる</b>。王道は時間で盤石になるため脆くならない（常に false）。
        /// </summary>
        public static bool IsRegimeBrittle(GovernanceStyle style, float age, float sustainability, GovernanceStyleParams p)
        {
            if (style == GovernanceStyle.王道) return false;
            float a = Mathf.Clamp01(age);
            float s = Mathf.Clamp01(sustainability);
            // 高齢化（年数を重ね）かつ持続可能性が閾値未満なら脆い。
            return a > 0f && s < p.brittleThreshold;
        }

        public static bool IsRegimeBrittle(GovernanceStyle style, float age, float sustainability)
            => IsRegimeBrittle(style, age, sustainability, GovernanceStyleParams.Default);
    }
}
