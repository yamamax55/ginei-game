using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政体類型＝モンテスキュー『法の精神』の三政体（共和政・君主政・専制政）。
    /// 各政体は固有の経路で腐敗する＝共和政は徳の喪失で寡頭/衆愚へ、君主政は中間権力の破壊で専制へ、
    /// 専制政は恣意の極まりで崩壊へ向かう（<see cref="PolityCorruptionRules.DegenerateTarget"/>）。
    /// </summary>
    public enum PolityType
    {
        /// <summary>共和政（徳を原理とする）＝徳の喪失で富者の寡頭政、または極端な平等で衆愚政へ堕ちる。</summary>
        共和政,
        /// <summary>君主政（名誉を原理とする）＝中間権力（貴族・法・中間団体）の破壊で専制へ堕ちる。</summary>
        君主政,
        /// <summary>専制政（恐怖を原理とする）＝恣意の極まりと恐怖の枯渇で崩壊へ向かう。</summary>
        専制政
    }

    /// <summary>各政体が腐敗して向かう先（堕落の標的＝固有の終着形態）。</summary>
    public enum DegenerationKind
    {
        /// <summary>寡頭政＝共和政が徳を失い富者の少数支配へ堕ちた形態。</summary>
        寡頭政,
        /// <summary>衆愚政＝共和政が極端な平等で暴民支配へ堕ちた形態。</summary>
        衆愚政,
        /// <summary>専制政＝君主政が中間権力を失い王の恣意支配へ堕ちた形態。</summary>
        専制政,
        /// <summary>崩壊＝専制政が恣意の極まりと恐怖の枯渇で機能を失った終末。</summary>
        崩壊
    }

    /// <summary>政体腐化の調整係数（MONT-2 #1440・モンテスキュー『法の精神』参考）。</summary>
    public readonly struct PolityCorruptionParams
    {
        /// <summary>共和政が寡頭化と衆愚化のどちらへ傾くかの閾値（不平等がこれ以上で寡頭、未満で衆愚へ）。</summary>
        public readonly float oligarchyTilt;
        /// <summary>政体腐敗の基本速度（制度的歯止め0のときの腐敗進行/秒）。</summary>
        public readonly float baseVelocity;
        /// <summary>引き返せる窓を閉じる腐敗進行の閾値（これ以上に進むと改革では戻れない＝不可逆）。</summary>
        public readonly float pointOfNoReturn;
        /// <summary>政体が末期腐敗に至る既定の腐敗進行閾値。</summary>
        public readonly float terminalThreshold;

        public PolityCorruptionParams(float oligarchyTilt, float baseVelocity,
            float pointOfNoReturn, float terminalThreshold)
        {
            this.oligarchyTilt = Mathf.Clamp01(oligarchyTilt);
            this.baseVelocity = Mathf.Max(0f, baseVelocity);
            this.pointOfNoReturn = Mathf.Clamp01(pointOfNoReturn);
            this.terminalThreshold = Mathf.Clamp01(terminalThreshold);
        }

        /// <summary>
        /// 既定＝寡頭化傾き0.5・腐敗基本速度0.1・引き返せない点0.7・末期閾値0.85。
        /// </summary>
        public static PolityCorruptionParams Default =>
            new PolityCorruptionParams(0.5f, 0.1f, 0.7f, 0.85f);
    }

    /// <summary>
    /// 政体腐化の純ロジック（MONT-2 #1440・モンテスキュー『法の精神』参考）。
    /// 政体は類型ごとに固有の経路で腐敗する：①共和政は徳の喪失と不平等の拡大で腐敗し、富者の寡頭政または
    /// 極端な平等の衆愚政へ堕ちる（<see cref="RepublicCorruption"/>）。②君主政は中間権力（貴族・法・中間団体）の
    /// 破壊で専制へ堕ちる＝王と人民の間の緩衝が消えると君主が暴君になる（<see cref="MonarchyCorruption"/>）。
    /// ③専制政は恣意の極まりと恐怖の枯渇で崩壊する＝誰も働かなくなり恐怖も効かなくなる（<see cref="DespotismDecay"/>）。
    /// 「政体は類型ごとに固有の経路で腐敗する」を式に出す＝腐敗経路・堕落の標的・速度が政体ごとに異なる。
    /// <see cref="DynastyRules"/>（王朝サイクル＝腐敗が制度疲労で一様に進む単線）とは別＝こちらは政体類型別の固有経路。
    /// <see cref="AnacyclosisRules"/>（ポリュビオスの六政体循環論＝形態の遷移輪）とも別＝こちらは三政体ごとの腐敗型。
    /// 政体の原理（徳/名誉/恐怖）そのものの腐化は <see cref="GovernmentPrincipleRules"/>、中間権力の動態は
    /// <see cref="IntermediatePowerRules"/>（いずれも同EPIC MONT）へ委譲する。
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PolityCorruptionRules
    {
        /// <summary>
        /// 共和政の腐敗（0..1）。共和政の原理は徳＝公共心の喪失で腐敗し、不平等の拡大が加速させる。
        /// 徳の喪失（virtueLoss）と不平等（inequality）の両輪で腐敗が進む＝富の偏在が徳を蝕み公共心を私利へ変える。
        /// 堕落の向かう先（寡頭/衆愚）は <see cref="DegenerateTarget"/> が不平等の度合いで分岐する。
        /// </summary>
        public static float RepublicCorruption(float virtueLoss, float inequality)
        {
            float vl = Mathf.Clamp01(virtueLoss);
            float ineq = Mathf.Clamp01(inequality);
            // 徳の喪失が主因（0.6）、不平等が加速因（0.4）＝徳なき共和政は腐敗し、不平等がそれを早める。
            return Mathf.Clamp01(vl * 0.6f + vl * ineq * 0.4f);
        }

        /// <summary>
        /// 君主政の腐敗（0..1）。君主政の原理は名誉＝中間権力（貴族・法・中間団体）が王と人民の緩衝になる。
        /// 中間権力の破壊（intermediaryPowerLoss）と法の侵食（lawErosion）で専制へ堕ちる＝緩衝が消えると王が暴君になる。
        /// 両方が揃うほど急速に腐敗する（中間権力が消え法も無いと、もはや君主を縛るものが何も無い）。
        /// </summary>
        public static float MonarchyCorruption(float intermediaryPowerLoss, float lawErosion)
        {
            float ipl = Mathf.Clamp01(intermediaryPowerLoss);
            float le = Mathf.Clamp01(lawErosion);
            // 中間権力の破壊が主因（0.55）、法の侵食が従因（0.25）、両者が揃う相乗（0.2）＝緩衝喪失で暴君化。
            return Mathf.Clamp01(ipl * 0.55f + le * 0.25f + ipl * le * 0.2f);
        }

        /// <summary>
        /// 専制政の崩壊（0..1）。専制政の原理は恐怖＝恣意（arbitrariness）が極まると誰も安んじて働けなくなり、
        /// 恐怖の枯渇（fearExhaustion＝慣れ・麻痺）で恐怖も効かなくなる＝専制の唯一の支柱が崩れて崩壊する。
        /// 恣意と恐怖枯渇の両輪で崩壊が進む＝恣意が荒野を生み、恐怖が効かなくなれば統治の手段が尽きる。
        /// </summary>
        public static float DespotismDecay(float arbitrariness, float fearExhaustion)
        {
            float arb = Mathf.Clamp01(arbitrariness);
            float fe = Mathf.Clamp01(fearExhaustion);
            // 恣意の極まりが主因（0.5）、恐怖の枯渇が従因（0.3）、両者の相乗（0.2）＝恣意×無恐怖で統治不能。
            return Mathf.Clamp01(arb * 0.5f + fe * 0.3f + arb * fe * 0.2f);
        }

        /// <summary>
        /// 政体類型ごとの固有の腐敗の進み方（型別経路）。同じストレス（stress 0..1）でも政体ごとに腐敗の式が違う＝
        /// 共和政は徳の喪失（ストレスが徳と不平等を同時に蝕む）、君主政は中間権力の破壊、専制政は恣意の極まり。
        /// 各政体の固有経路（<see cref="RepublicCorruption"/>/<see cref="MonarchyCorruption"/>/<see cref="DespotismDecay"/>）
        /// へストレスを写して腐敗を算出する＝政体類型ごとに腐敗の型が異なることを一本の窓口で表す。
        /// </summary>
        public static float CorruptionPath(PolityType type, float stress)
        {
            float s = Mathf.Clamp01(stress);
            switch (type)
            {
                // 共和政＝ストレスが徳の喪失と不平等の拡大の双方を進める。
                case PolityType.共和政: return RepublicCorruption(s, s);
                // 君主政＝ストレスが中間権力の破壊と法の侵食を進める。
                case PolityType.君主政: return MonarchyCorruption(s, s);
                // 専制政＝ストレスが恣意の極まりと恐怖の枯渇を進める。
                default: return DespotismDecay(s, s);
            }
        }

        /// <summary>
        /// 各政体が堕落して向かう先（固有の終着形態）。共和政は不平等の度合いで寡頭政（富者支配）か衆愚政
        /// （極端な平等の暴民支配）へ分岐し、君主政は専制政へ、専制政は崩壊へ向かう＝堕落の標的が政体ごとに固有。
        /// 共和政の分岐は <see cref="PolityCorruptionParams.oligarchyTilt"/> を閾値に不平等で決まる（高不平等→寡頭）。
        /// </summary>
        public static DegenerationKind DegenerateTarget(PolityType type, float inequality, PolityCorruptionParams p)
        {
            switch (type)
            {
                case PolityType.共和政:
                    // 不平等が高ければ富者の寡頭化、低ければ極端な平等の衆愚化。
                    return Mathf.Clamp01(inequality) >= p.oligarchyTilt
                        ? DegenerationKind.寡頭政
                        : DegenerationKind.衆愚政;
                case PolityType.君主政:
                    return DegenerationKind.専制政; // 中間権力の破壊で専制へ。
                default:
                    return DegenerationKind.崩壊;   // 専制は恣意の極まりで崩壊へ。
            }
        }

        /// <summary>
        /// 共和政の堕落先を既定の不平等中央値で判定する簡易窓口（不平等0.5＝寡頭傾きと同値で寡頭政、君主/専制は固定）。
        /// </summary>
        public static DegenerationKind DegenerateTarget(PolityType type)
            => DegenerateTarget(type, 0.5f, PolityCorruptionParams.Default);

        /// <summary>
        /// 腐敗の速度（0..1スケール/秒）。制度的歯止め（safeguardStrength）が無いほど腐敗が速い＝共和政の徳の習俗、
        /// 君主政の中間権力、専制政には固有の歯止めが乏しい。型ごとに固有速度倍率が違う＝専制政は最も速く崩れ、
        /// 君主政は中間権力が残れば緩慢、共和政は中庸。歯止めは速度を緩める（止めはしない＝腐敗は不可避的に進む）。
        /// </summary>
        public static float CorruptionVelocity(PolityType type, float safeguardStrength, PolityCorruptionParams p)
        {
            float guard = Mathf.Clamp01(safeguardStrength);
            float typeMul;
            switch (type)
            {
                case PolityType.共和政: typeMul = 1.0f; break; // 中庸＝徳の習俗が緩衝。
                case PolityType.君主政: typeMul = 0.8f; break; // 緩慢＝中間権力が緩衝。
                default: typeMul = 1.4f; break;                // 専制は固有の歯止めが乏しく最も速い。
            }
            return Mathf.Clamp01(p.baseVelocity * typeMul * (1f - guard) * 2f);
        }

        public static float CorruptionVelocity(PolityType type, float safeguardStrength)
            => CorruptionVelocity(type, safeguardStrength, PolityCorruptionParams.Default);

        /// <summary>
        /// 腐敗を改革で引き返せる窓（0..1＝余地の大きさ）。腐敗が進む前（corruptionProgress が小さい）なら改革能力
        /// （reformCapacity）で引き返せるが、引き返せない点（<see cref="PolityCorruptionParams.pointOfNoReturn"/>）を
        /// 超えると窓は閉じる（0＝手遅れ＝不可逆）。窓＝(引き返せない点までの残り)×改革能力で、進むほど狭まる。
        /// </summary>
        public static float ReversibilityWindow(float corruptionProgress, float reformCapacity, PolityCorruptionParams p)
        {
            float prog = Mathf.Clamp01(corruptionProgress);
            float reform = Mathf.Clamp01(reformCapacity);
            if (prog >= p.pointOfNoReturn) return 0f; // 引き返せない点を越えたら不可逆。
            // 引き返せない点までの残り余地（0..1正規化）×改革能力＝進むほど窓が狭まる。
            float room = (p.pointOfNoReturn - prog) / Mathf.Max(0.0001f, p.pointOfNoReturn);
            return Mathf.Clamp01(room * reform);
        }

        public static float ReversibilityWindow(float corruptionProgress, float reformCapacity)
            => ReversibilityWindow(corruptionProgress, reformCapacity, PolityCorruptionParams.Default);

        /// <summary>
        /// 政体が固有の腐敗で末期に至ったか（腐敗進行が threshold 以上）。末期＝共和政なら寡頭/衆愚への堕落が確定、
        /// 君主政なら専制化が確定、専制政なら崩壊が確定する段階＝<see cref="DegenerateTarget"/> へ移る機が熟す。
        /// type は将来の型別閾値補正の余地として受ける（現状は共通閾値＝政体非依存の末期判定）。
        /// </summary>
        public static bool IsTerminalCorruption(float corruptionProgress, PolityType type, float threshold)
        {
            return Mathf.Clamp01(corruptionProgress) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定の末期閾値（<see cref="PolityCorruptionParams.terminalThreshold"/>）で末期腐敗を判定する簡易窓口。</summary>
        public static bool IsTerminalCorruption(float corruptionProgress, PolityType type)
            => IsTerminalCorruption(corruptionProgress, type, PolityCorruptionParams.Default.terminalThreshold);
    }
}
