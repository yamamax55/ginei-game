using UnityEngine;

namespace Ginei
{
    /// <summary>一般意志（共同体の公共善を志向する意志）の純データ（ROUS-1 #1462・ルソー『社会契約論』参考）。可変フィールド。</summary>
    [System.Serializable]
    public struct GeneralWillState
    {
        /// <summary>公益志向（0..1・一般意志が公共善へ向く度合い）。</summary>
        public float publicOrientation;

        /// <summary>派閥による捕獲（0..1・部分社会＝派閥が一般意志を私益で乗っ取る度合い）。</summary>
        public float factionalCapture;

        /// <summary>市民の直接参加（0..1・ルソー＝代表でなく直接参加が一般意志の正統性を支える）。</summary>
        public float participation;

        public GeneralWillState(float publicOrientation, float factionalCapture, float participation)
        {
            this.publicOrientation = Mathf.Clamp01(publicOrientation);
            this.factionalCapture = Mathf.Clamp01(factionalCapture);
            this.participation = Mathf.Clamp01(participation);
        }
    }

    /// <summary>一般意志汚染指標の調整値（#1462・マジックナンバー回避）。`Default` を既定に使う。top-level。</summary>
    public readonly struct GeneralWillParams
    {
        /// <summary>派閥汚染の感度（≥0・派閥の数×強さがこの倍率で一般意志を汚染する）。</summary>
        public readonly float contaminationScale;

        /// <summary>特殊意志が時間で一般意志を蝕む速度（≥0・秒あたり）。</summary>
        public readonly float corruptionRate;

        /// <summary>市民の直接参加が正統性に寄与する重み（0..1・残りは間接的審議の重み）。</summary>
        public readonly float participationWeight;

        /// <summary>一般意志が汚染されたと判定する純度の既定閾値（これ未満で公共善が失われた）。</summary>
        public readonly float corruptedThreshold;

        public GeneralWillParams(float contaminationScale, float corruptionRate, float participationWeight, float corruptedThreshold)
        {
            this.contaminationScale = Mathf.Max(0f, contaminationScale);
            this.corruptionRate = Mathf.Max(0f, corruptionRate);
            this.participationWeight = Mathf.Clamp01(participationWeight);
            this.corruptedThreshold = Mathf.Clamp01(corruptedThreshold);
        }

        /// <summary>既定＝派閥汚染感度1・腐敗速度0.1/秒・直接参加の重み0.6（ルソー＝直接参加重視）・汚染閾値0.3。</summary>
        public static GeneralWillParams Default => new GeneralWillParams(
            contaminationScale: 1f,
            corruptionRate: 0.1f,
            participationWeight: 0.6f,
            corruptedThreshold: 0.3f);
    }

    /// <summary>
    /// 一般意志汚染指標の純ロジック（ROUS-1 #1462・ルソー『社会契約論』参考）。一般意志（volonté générale）＝<b>共同体全体の公共善を志向する意志</b>であり、
    /// 個々の特殊意志（私益）の総和である「全体意志（<see cref="WillOfAll"/>）」とは異なる。<b>派閥（部分社会）が形成されると一般意志が特殊意志に汚染され</b>
    /// （<see cref="FactionalContamination"/>）、もはや公共善でなく派閥の利益が国家を動かす（<see cref="GeneralWillPurity"/>＝公益志向−派閥捕獲の純度・<see cref="IsGeneralWillCorrupted"/>）。
    /// 政策は一般意志の純度に応じて公共善へ沿い（<see cref="PublicGoodAlignment"/>）、市民の直接参加が一般意志の正統性を高め（<see cref="ParticipationLegitimacy"/>・代表でなく直接参加）、
    /// 特殊意志は時間で一般意志を蝕み（<see cref="CorruptionByParticularWill"/>）、一般意志に従わせる強制は個人の特殊意志を公共善へ向ける逆説となる（<see cref="ForcedToBeFree"/>＝「自由を強制される」）。
    /// 分担：<see cref="CommonGoodOrientationRules"/>(政体が公益志向か私益志向かの品質スコア)／`LobbyRules`(圧力団体＝部分社会の政策歪み)／`CivicFaithRules`(市民宗教＝一般意志を支える共同体の絆・同EPIC ROUS)／
    /// `FactionMultiplicityRules`(派閥の多数性・既存)とは別＝<b>一般意志vs特殊意志の汚染</b>（<see cref="GeneralWillState"/> が中核データ）。
    /// 乱数なし決定論・全入力クランプ・配列null/空安全。調整値は <see cref="GeneralWillParams"/>（既定 <see cref="GeneralWillParams.Default"/>）。test-first。
    /// </summary>
    public static class GeneralWillRules
    {
        /// <summary>
        /// 一般意志の純度（0..1・汚染されていない度＝公益志向−派閥捕獲）。
        /// 共同体が公共善へ向くほど（publicOrientation 高）+1へ、派閥に乗っ取られるほど（factionalCapture 高）0へ。
        /// </summary>
        public static float GeneralWillPurity(float publicOrientation, float factionalCapture)
            => Mathf.Clamp01(Mathf.Clamp01(publicOrientation) - Mathf.Clamp01(factionalCapture));

        /// <summary>
        /// 全体意志（will of all・0..1）＝特殊意志（個々の私益）の総和の平均。<b>一般意志（公共善）とは異なる</b>。
        /// ルソー＝全体意志は私益の寄せ集めにすぎず、たまたま私益が打ち消し合えば一般意志に近づくが、派閥が残れば歪む。
        /// null/空は0（私益が一つも無ければ全体意志も無い）。手書きループ。
        /// </summary>
        public static float WillOfAll(float[] particularInterests)
        {
            if (particularInterests == null || particularInterests.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < particularInterests.Length; i++)
                sum += Mathf.Clamp01(particularInterests[i]);
            return Mathf.Clamp01(sum / particularInterests.Length);
        }

        /// <summary>派閥による一般意志の汚染度（既定 Params）。</summary>
        public static float FactionalContamination(float factionCount, float factionStrength)
            => FactionalContamination(factionCount, factionStrength, GeneralWillParams.Default);

        /// <summary>
        /// 派閥（部分社会）が一般意志を汚染する度合い（0..1）＝派閥の数×強さ×感度。
        /// <b>派閥が多く強いほど公共善が特殊意志に歪められる</b>（ルソー＝部分社会が増えるほど一般意志は失われる）。
        /// 数か強さのどちらかが0なら汚染も0（派閥が無ければ汚染されない）。
        /// </summary>
        public static float FactionalContamination(float factionCount, float factionStrength, GeneralWillParams p)
        {
            float count = Mathf.Clamp01(factionCount);
            float strength = Mathf.Clamp01(factionStrength);
            return Mathf.Clamp01(count * strength * p.contaminationScale);
        }

        /// <summary>
        /// 政策が一般意志（公共善）に沿う度合い（0..1）＝一般意志の純度×政策の公共性。
        /// 一般意志が汚染されているか（純度低）、政策が私的（公共性低）なら、政策は公共善から外れる＝両者の積。
        /// </summary>
        public static float PublicGoodAlignment(float generalWillPurity, float policyPublicness)
            => Mathf.Clamp01(Mathf.Clamp01(generalWillPurity) * Mathf.Clamp01(policyPublicness));

        /// <summary>市民の直接参加が高める一般意志の正統性（既定 Params）。</summary>
        public static float ParticipationLegitimacy(float participation, float directDeliberation)
            => ParticipationLegitimacy(participation, directDeliberation, GeneralWillParams.Default);

        /// <summary>
        /// 市民の直接参加が一般意志の正統性を高める度合い（0..1）＝直接参加×重み＋直接審議×(1−重み)。
        /// <b>ルソー＝一般意志は代表されえない</b>。市民が直接参加し直接審議するほど一般意志の正統性が立つ（代議制では失われる）。
        /// </summary>
        public static float ParticipationLegitimacy(float participation, float directDeliberation, GeneralWillParams p)
        {
            float part = Mathf.Clamp01(participation);
            float delib = Mathf.Clamp01(directDeliberation);
            float w = Mathf.Clamp01(p.participationWeight);
            return Mathf.Clamp01(part * w + delib * (1f - w));
        }

        /// <summary>特殊意志による一般意志の腐敗の進行（既定 Params）。</summary>
        public static float CorruptionByParticularWill(float factionalContamination, float dt)
            => CorruptionByParticularWill(factionalContamination, dt, GeneralWillParams.Default);

        /// <summary>
        /// 特殊意志が時間で一般意志を蝕む量（≥0）＝派閥汚染×腐敗速度×経過時間。
        /// <b>派閥に汚染された一般意志は放置すれば時間とともに公共善を失う</b>（呼び出し側は純度・正統性からこの分を差し引く想定）。
        /// dt が0以下なら0（時間が進まなければ腐敗しない）。
        /// </summary>
        public static float CorruptionByParticularWill(float factionalContamination, float dt, GeneralWillParams p)
        {
            if (dt <= 0f) return 0f;
            return Mathf.Max(0f, Mathf.Clamp01(factionalContamination) * p.corruptionRate * dt);
        }

        /// <summary>
        /// 一般意志に従わせる強制（0..1・ルソーの逆説「自由を強制される」）＝一般意志の純度×個人の逸脱。
        /// <b>個人の特殊意志（私益への逸脱）を公共善へ強制的に向ける</b>のが「自由の強制」。
        /// 一般意志が純粋（公共善）なほど、また個人が逸脱するほど、その強制は正当化される＝両者の積（汚染された一般意志への強制は専制にすぎない）。
        /// </summary>
        public static float ForcedToBeFree(float generalWillPurity, float individualDeviation)
            => Mathf.Clamp01(Mathf.Clamp01(generalWillPurity) * Mathf.Clamp01(individualDeviation));

        /// <summary>一般意志が派閥に汚染され公共善が失われたか（既定閾値）。</summary>
        public static bool IsGeneralWillCorrupted(float generalWillPurity)
            => IsGeneralWillCorrupted(generalWillPurity, GeneralWillParams.Default.corruptedThreshold);

        /// <summary>
        /// 一般意志が派閥に汚染され公共善が失われた判定（純度が threshold 未満で true）。
        /// <b>純度が閾値を割ると、国家はもはや公共善でなく派閥の利益で動く</b>（一般意志の死）。
        /// </summary>
        public static bool IsGeneralWillCorrupted(float generalWillPurity, float threshold)
            => Mathf.Clamp01(generalWillPurity) < Mathf.Clamp01(threshold);
    }
}
