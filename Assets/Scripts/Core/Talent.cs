using System;

namespace Ginei
{
    /// <summary>
    /// 特技・戦法を司る素養（信長の野望／三国志の「武・知・統・政」を本作流に）。
    /// 特技の冴え（実効量）は、この素養に対応する提督の実効能力で決まる。
    /// 武勇→攻撃／知略→情報／統率→統率／政務→運営。
    /// </summary>
    public enum TalentAspect { 武勇, 知略, 統率, 政務 }

    /// <summary>
    /// 特技の種別。特性＝常時/条件で効くパッシブ（信長の野望の特技寄り）、
    /// 戦法＝能動的に発令するアクティブ（三国志の戦法寄り＝クールダウン制）。
    /// </summary>
    public enum TalentKind { 特性, 戦法 }

    /// <summary>
    /// 特技の格（信長の特技ランク／三国志の戦法ランク＝初級〜神）。格が上がるほど効果量が増し、必要素養も上がる。
    /// </summary>
    public enum TalentGrade { 初, 中, 上, 特, 神 }

    /// <summary>
    /// 特技の効果チャネル（本作の既存システムへ橋渡しする出口）。
    /// 戦闘パッシブの5種は <see cref="AdmiralSkill"/>（#137-140）へそのまま写せる＝数式は二重実装しない。
    /// 戦法系（砲撃/突撃/鼓舞/範囲攻撃）は <see cref="ActiveCommand"/>（#2175）等へ、
    /// マクロ系（兵站/内政/経済）は戦略Tickが実効量を読む。
    /// </summary>
    public enum TalentEffect
    {
        攻撃強化, 防御強化, 機動強化, 士気維持, 索敵強化, // 戦闘パッシブ＝AdmiralSkill の5種に対応
        火力集中,   // 局所火力集中（ランチェスター #LanchesterRules）
        側背強化,   // 側背面与ダメ（CombatModifiers.FlankFactor／包囲 #2178）
        奇襲,       // 不意打ち（索敵 #2180／特殊作戦 #SOF）
        砲撃戦法,   // 一斉砲撃の強化（ActiveCommand.一斉砲撃）
        突撃戦法,   // 突撃の強化（ActiveCommand.突撃）
        鼓舞戦法,   // 士気回復バースト
        範囲攻撃戦法, // 火計/落雷的な範囲ダメ
        兵站,       // 補給（軍要求物資 #2049）
        内政,       // 安定度（GovernanceRules #109）
        経済,       // 産出（ResourceProductionRules #93）
    }

    /// <summary>
    /// 特技・戦法のカタログ定義（本作オリジナル＝信長の野望/三国志を参考にした固有名）。
    /// 純データ。possession（提督が何を所持するか）は <see cref="Talent"/>、解決は <see cref="TalentRules"/>。
    /// </summary>
    [Serializable]
    public class TalentDef
    {
        /// <summary>一意id（カタログのキー）。</summary>
        public string id;

        /// <summary>表示名（例：鬼神／神算／連弩斉射）。</summary>
        public string talentName;

        /// <summary>司る素養（実効量を左右する能力）。</summary>
        public TalentAspect aspect;

        /// <summary>種別（特性＝パッシブ／戦法＝アクティブ）。</summary>
        public TalentKind kind;

        /// <summary>効果チャネル（既存システムへの出口）。</summary>
        public TalentEffect effect;

        /// <summary>発動条件（特性のみ意味を持つ＝常時/劣勢時/交戦時/側背面時）。戦法は常時扱い。</summary>
        public SkillCondition condition = SkillCondition.常時;

        /// <summary>
        /// 効果の基準量（格「中」・平均的素養での効果の大きさ）。
        /// 倍率系は「+割合」（0.10＝+10%）、加算系（士気/索敵/兵站/内政/経済）はその量。
        /// </summary>
        public float baseMagnitude = 0.10f;

        /// <summary>短い説明（フレーバー）。</summary>
        public string description = "";

        public TalentDef() { }

        public TalentDef(string id, string talentName, TalentAspect aspect, TalentKind kind,
                         TalentEffect effect, float baseMagnitude, SkillCondition condition = SkillCondition.常時, string description = "")
        {
            this.id = id; this.talentName = talentName; this.aspect = aspect; this.kind = kind;
            this.effect = effect; this.baseMagnitude = baseMagnitude; this.condition = condition; this.description = description;
        }
    }

    /// <summary>
    /// 提督が所持する1つの特技（カタログ定義id＋格）。possession の最小単位。
    /// 提督アセット <see cref="AdmiralData.talents"/> に持たせ、<see cref="TalentRules"/> が実効量へ解く。
    /// </summary>
    [Serializable]
    public class Talent
    {
        /// <summary>カタログ定義の id（<see cref="TalentCatalog"/> で解決）。</summary>
        public string defId;

        /// <summary>習熟の格（効果量と必要素養を決める）。</summary>
        public TalentGrade grade = TalentGrade.中;

        public Talent() { }
        public Talent(string defId, TalentGrade grade = TalentGrade.中) { this.defId = defId; this.grade = grade; }
    }
}
