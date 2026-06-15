using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 特技・戦法の解決の唯一の窓口（本作オリジナル＝信長の野望/三国志を参考）。
    /// 「格」と「素養（提督の実効能力）」で実効量を解き、<b>既存システムへ橋渡し</b>する：
    /// ・戦闘パッシブ（攻撃/防御/機動/士気/索敵）→ <see cref="AdmiralSkill"/> を生成し <see cref="AdmiralSkillRules"/> が数式を担う（二重実装しない）。
    /// ・戦法（砲撃/突撃）→ <see cref="ActiveCommand"/> を強化、鼓舞/範囲攻撃は独自戦法。
    /// ・マクロ（兵站/内政/経済）→ 戦略Tickが実効量を読む。
    /// 実効値パターン（基準値非破壊）。有能な提督ほど特技が冴える。test-first。
    /// </summary>
    public static class TalentRules
    {
        /// <summary>格→効果倍率（初0.5／中1.0／上1.5／特2.0／神3.0）。基準量に掛ける。</summary>
        public static float GradePotency(TalentGrade grade)
        {
            switch (grade)
            {
                case TalentGrade.初: return 0.5f;
                case TalentGrade.中: return 1.0f;
                case TalentGrade.上: return 1.5f;
                case TalentGrade.特: return 2.0f;
                case TalentGrade.神: return 3.0f;
                default: return 1.0f;
            }
        }

        /// <summary>格→必要素養（その格を扱える最低能力。神は90＝極一握り）。</summary>
        public static float RequiredStat(TalentGrade grade)
        {
            switch (grade)
            {
                case TalentGrade.初: return 0f;
                case TalentGrade.中: return 35f;
                case TalentGrade.上: return 55f;
                case TalentGrade.特: return 75f;
                case TalentGrade.神: return 90f;
                default: return 0f;
            }
        }

        /// <summary>素養に対応する提督の実効能力（武勇→攻撃／知略→情報／統率→統率／政務→運営）。</summary>
        public static float AspectStat(AdmiralData admiral, TalentAspect aspect)
        {
            if (admiral == null) return 0f;
            switch (aspect)
            {
                case TalentAspect.武勇: return admiral.EffectiveAttack;
                case TalentAspect.知略: return admiral.EffectiveIntelligence;
                case TalentAspect.統率: return admiral.EffectiveLeadership;
                case TalentAspect.政務: return admiral.EffectiveOperation;
                default: return 0f;
            }
        }

        /// <summary>素養による効き具合（CombatModifiers.AbilityFactor を流用し 0.5〜1.5 にクランプ＝有能ほど冴える）。</summary>
        public static float AspectScaling(float stat)
            => Mathf.Clamp(CombatModifiers.AbilityFactor(stat), 0.5f, 1.5f);

        /// <summary>提督がこの格の特技を扱えるか（素養が必要値以上）。</summary>
        public static bool MeetsRequirement(TalentAspect aspect, TalentGrade grade, AdmiralData admiral)
            => AspectStat(admiral, aspect) >= RequiredStat(grade);

        /// <summary>所持特技がこの提督に適合するか（定義解決＋素養ゲート）。</summary>
        public static bool CanWield(Talent talent, AdmiralData admiral)
        {
            TalentDef def = TalentCatalog.Get(talent);
            return def != null && MeetsRequirement(def.aspect, talent.grade, admiral);
        }

        /// <summary>
        /// 実効量＝基準量 × 格倍率 × 素養補正。倍率系は「+割合」、加算系（士気/索敵/内政）はその量。
        /// 素養ゲート未達なら 0（扱えない）。
        /// </summary>
        public static float EffectiveMagnitude(TalentDef def, TalentGrade grade, AdmiralData admiral)
        {
            if (def == null) return 0f;
            if (!MeetsRequirement(def.aspect, grade, admiral)) return 0f;
            return def.baseMagnitude * GradePotency(grade) * AspectScaling(AspectStat(admiral, def.aspect));
        }

        /// <summary>効果チャネルが加算系（生の量で効く＝士気/索敵/内政）か。残りは「+割合」。</summary>
        public static bool IsAdditive(TalentEffect effect)
            => effect == TalentEffect.士気維持 || effect == TalentEffect.索敵強化 || effect == TalentEffect.内政;

        /// <summary>効果チャネルが戦闘パッシブ5種（AdmiralSkill へ写せる）か。</summary>
        public static bool MapsToAdmiralSkill(TalentEffect effect, out SkillEffectType type)
        {
            switch (effect)
            {
                case TalentEffect.攻撃強化: type = SkillEffectType.攻撃倍率; return true;
                case TalentEffect.防御強化: type = SkillEffectType.防御倍率; return true;
                case TalentEffect.機動強化: type = SkillEffectType.機動倍率; return true;
                case TalentEffect.士気維持: type = SkillEffectType.士気維持; return true;
                case TalentEffect.索敵強化: type = SkillEffectType.索敵範囲; return true;
                default: type = SkillEffectType.攻撃倍率; return false;
            }
        }

        /// <summary>
        /// 特技を <see cref="AdmiralSkill"/> へ写す（戦闘パッシブ5種のみ・数式は AdmiralSkillRules が担う）。
        /// 倍率系は magnitude=1+割合、加算系は magnitude=量。扱えない/対象外チャネルは null。
        /// </summary>
        public static AdmiralSkill ToAdmiralSkill(TalentDef def, TalentGrade grade, AdmiralData admiral)
        {
            if (def == null || !MapsToAdmiralSkill(def.effect, out SkillEffectType type)) return null;
            float mag = EffectiveMagnitude(def, grade, admiral);
            if (mag <= 0f) return null; // ゲート未達＝効かない
            bool additive = IsAdditive(def.effect);
            return new AdmiralSkill
            {
                skillName = def.talentName,
                effectType = type,
                condition = def.condition,
                magnitude = additive ? mag : (1f + mag),
            };
        }

        /// <summary>
        /// 所持特技群を <see cref="AdmiralSkill"/> リストへ写す（戦闘パッシブのみ＝AdmiralSkillRules へ流し込む橋）。
        /// こうして特技由来の修正子を既存のパッシブスキル数式（#137-140）と同じパイプラインで合算できる。
        /// </summary>
        public static List<AdmiralSkill> ToAdmiralSkills(IList<Talent> talents, AdmiralData admiral)
        {
            var result = new List<AdmiralSkill>();
            if (talents == null) return result;
            for (int i = 0; i < talents.Count; i++)
            {
                TalentDef def = TalentCatalog.Get(talents[i]);
                AdmiralSkill s = ToAdmiralSkill(def, talents[i].grade, admiral);
                if (s != null) result.Add(s);
            }
            return result;
        }

        /// <summary>戦法チャネルが既存アクティブ指揮（#2175）へ対応するか（砲撃→一斉砲撃／突撃→突撃）。</summary>
        public static bool TryGetActiveCommand(TalentEffect effect, out ActiveCommand cmd)
        {
            switch (effect)
            {
                case TalentEffect.砲撃戦法: cmd = ActiveCommand.一斉砲撃; return true;
                case TalentEffect.突撃戦法: cmd = ActiveCommand.突撃; return true;
                default: cmd = ActiveCommand.一斉砲撃; return false;
            }
        }

        /// <summary>このチャネルが戦法（アクティブ）か（カタログ定義の kind に依らず効果から判定）。</summary>
        public static bool IsTacticEffect(TalentEffect effect)
        {
            switch (effect)
            {
                case TalentEffect.砲撃戦法:
                case TalentEffect.突撃戦法:
                case TalentEffect.鼓舞戦法:
                case TalentEffect.範囲攻撃戦法:
                    return true;
                default:
                    return false;
            }
        }
    }
}
