using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 貴族特権の調整係数（門閥の世襲特権と社会的歪み・銀英伝の門閥貴族参考）。
    /// 全フィールドを ctor で Clamp（重みは0..1、位階・領地等のスケールは非負/0..1）。
    /// </summary>
    public readonly struct NoblePrivilegeParams
    {
        /// <summary>特権蓄積に占める世襲位階の重み。</summary>
        public readonly float rankWeight;
        /// <summary>特権蓄積に占める宮廷の寵の重み。</summary>
        public readonly float favorWeight;
        /// <summary>特権蓄積に占める領地の重み。</summary>
        public readonly float landWeight;
        /// <summary>免税が平民への負担転嫁を増幅する強さ。</summary>
        public readonly float exemptionPush;
        /// <summary>特権が無能を温存する強さ（特権が実力を問わせない度合い）。</summary>
        public readonly float shelterStrength;
        /// <summary>無能温存が軍指揮を損なう強さ（貴族将校の害）。</summary>
        public readonly float harmStrength;
        /// <summary>不満が改革派の力で特権廃止の圧力へ転じる強さ。</summary>
        public readonly float reformGain;
        /// <summary>特権が維持できなくなる改革圧力の既定閾値。</summary>
        public readonly float sustainThreshold;

        public NoblePrivilegeParams(float rankWeight, float favorWeight, float landWeight,
            float exemptionPush, float shelterStrength, float harmStrength,
            float reformGain, float sustainThreshold)
        {
            this.rankWeight = Mathf.Clamp01(rankWeight);
            this.favorWeight = Mathf.Clamp01(favorWeight);
            this.landWeight = Mathf.Clamp01(landWeight);
            this.exemptionPush = Mathf.Clamp01(exemptionPush);
            this.shelterStrength = Mathf.Clamp01(shelterStrength);
            this.harmStrength = Mathf.Clamp01(harmStrength);
            this.reformGain = Mathf.Clamp01(reformGain);
            this.sustainThreshold = Mathf.Clamp01(sustainThreshold);
        }

        /// <summary>
        /// 既定＝位階0.4/寵0.3/領地0.3（合計1）・免税増幅0.6・無能温存0.7・軍害0.8・改革ゲイン0.7・維持閾値0.6。
        /// </summary>
        public static NoblePrivilegeParams Default =>
            new NoblePrivilegeParams(0.4f, 0.3f, 0.3f, 0.6f, 0.7f, 0.8f, 0.7f, 0.6f);
    }

    /// <summary>
    /// 貴族特権の純ロジック（門閥の世襲特権と社会的歪み・銀英伝の門閥貴族の腐敗と平民の不満を参考）。
    /// 世襲位階×宮廷の寵×領地で特権が蓄積し（<see cref="PrivilegeAccumulation"/>）、特権と免税が平民へ
    /// 負担を転嫁する（<see cref="CommonerBurden"/>）。特権は実力を問わせず無能を温存し（<see cref="IncompetenceShelter"/>）、
    /// 説明責任の欠如で腐敗する（<see cref="Corruption"/>）。負担と腐敗が社会的不満を蓄積し（<see cref="SocialResentment"/>）、
    /// 無能温存は軍の指揮を損ない（<see cref="MilitaryHarm"/>＝貴族将校の害）、不満と改革派の力が特権廃止の圧力に
    /// なる（<see cref="ReformPressure"/>）。圧力が主君の後ろ盾を上回ると特権は維持できない（<see cref="IsPrivilegeSustainable"/>）。
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NoblePrivilegeRules
    {
        /// <summary>
        /// 特権の蓄積（0..1）。世襲位階（hereditaryRank）×宮廷の寵（courtFavor）×領地（landholdings）の
        /// 重み付き和で、門閥貴族が世襲する既得権益の厚みを表す（重みは Params の合計に正規化されない加重和）。
        /// </summary>
        public static float PrivilegeAccumulation(float hereditaryRank, float courtFavor,
            float landholdings, NoblePrivilegeParams p)
        {
            float rank = Mathf.Clamp01(hereditaryRank);
            float favor = Mathf.Clamp01(courtFavor);
            float land = Mathf.Clamp01(landholdings);
            return Mathf.Clamp01(rank * p.rankWeight + favor * p.favorWeight + land * p.landWeight);
        }

        public static float PrivilegeAccumulation(float hereditaryRank, float courtFavor, float landholdings)
            => PrivilegeAccumulation(hereditaryRank, courtFavor, landholdings, NoblePrivilegeParams.Default);

        /// <summary>
        /// 平民への負担転嫁（0..1）。特権（privilegeAccumulation）が大きいほど、また貴族の免税（taxExemption）が
        /// 進むほど、税負担は平民へ転嫁される＝特権階級が税を逃れたぶん平民が肩代わりする。
        /// 免税は転嫁を増幅する（特権×(1+免税×増幅)）。
        /// </summary>
        public static float CommonerBurden(float privilegeAccumulation, float taxExemption, NoblePrivilegeParams p)
        {
            float priv = Mathf.Clamp01(privilegeAccumulation);
            float exempt = Mathf.Clamp01(taxExemption);
            return Mathf.Clamp01(priv * (1f + exempt * p.exemptionPush));
        }

        public static float CommonerBurden(float privilegeAccumulation, float taxExemption)
            => CommonerBurden(privilegeAccumulation, taxExemption, NoblePrivilegeParams.Default);

        /// <summary>
        /// 無能の温存（0..1）。特権（privilegeAccumulation）が大きいほど無能さ（meritlessness）が温存される＝
        /// 特権が実力を問わせず、無能な門閥子弟が地位に居座る。特権が無ければ無能は淘汰される（priv=0で0）。
        /// </summary>
        public static float IncompetenceShelter(float privilegeAccumulation, float meritlessness, NoblePrivilegeParams p)
        {
            float priv = Mathf.Clamp01(privilegeAccumulation);
            float merit = Mathf.Clamp01(meritlessness);
            return Mathf.Clamp01(priv * merit * (1f + p.shelterStrength));
        }

        public static float IncompetenceShelter(float privilegeAccumulation, float meritlessness)
            => IncompetenceShelter(privilegeAccumulation, meritlessness, NoblePrivilegeParams.Default);

        /// <summary>
        /// 腐敗（0..1）。特権（privilegeAccumulation）を説明責任（accountability）で割る＝説明責任が高いほど
        /// 腐敗は抑えられ、説明責任なき特権は腐敗する（特権/(1+説明責任)）。Params は構造上未使用（一貫性のため受ける）。
        /// </summary>
        public static float Corruption(float privilegeAccumulation, float accountability, NoblePrivilegeParams p)
        {
            float priv = Mathf.Clamp01(privilegeAccumulation);
            float acc = Mathf.Clamp01(accountability);
            return Mathf.Clamp01(priv / (1f + acc));
        }

        public static float Corruption(float privilegeAccumulation, float accountability)
            => Corruption(privilegeAccumulation, accountability, NoblePrivilegeParams.Default);

        /// <summary>
        /// 社会的不満の蓄積（0..1）。平民への負担転嫁（commonerBurden）と腐敗（corruption）の相乗で蓄積する＝
        /// 重い負担と腐敗が揃うと不満が燃え上がる。和の半分＋積で、片方だけでは中庸・両方揃うと跳ね上がる。
        /// </summary>
        public static float SocialResentment(float commonerBurden, float corruption, NoblePrivilegeParams p)
        {
            float burden = Mathf.Clamp01(commonerBurden);
            float corrupt = Mathf.Clamp01(corruption);
            return Mathf.Clamp01((burden + corrupt) * 0.5f + burden * corrupt * 0.5f);
        }

        public static float SocialResentment(float commonerBurden, float corruption)
            => SocialResentment(commonerBurden, corruption, NoblePrivilegeParams.Default);

        /// <summary>
        /// 軍の指揮を損なう度合い（0..1＝貴族将校の害）。無能温存（incompetenceShelter）が大きいほど、無能な貴族が
        /// 指揮官の座を占めて軍を損なう。harmStrength で害の効きを調整する。
        /// </summary>
        public static float MilitaryHarm(float incompetenceShelter, NoblePrivilegeParams p)
        {
            float shelter = Mathf.Clamp01(incompetenceShelter);
            return Mathf.Clamp01(shelter * p.harmStrength);
        }

        public static float MilitaryHarm(float incompetenceShelter)
            => MilitaryHarm(incompetenceShelter, NoblePrivilegeParams.Default);

        /// <summary>
        /// 特権廃止の改革圧力（0..1）。社会的不満（socialResentment）と改革派の力（reformistStrength）の積に
        /// reformGain を掛ける＝不満があっても改革派が弱ければ圧力にならず、両者が揃って初めて廃止圧力に転じる。
        /// </summary>
        public static float ReformPressure(float socialResentment, float reformistStrength, NoblePrivilegeParams p)
        {
            float resent = Mathf.Clamp01(socialResentment);
            float reform = Mathf.Clamp01(reformistStrength);
            return Mathf.Clamp01(resent * reform * (1f + p.reformGain));
        }

        public static float ReformPressure(float socialResentment, float reformistStrength)
            => ReformPressure(socialResentment, reformistStrength, NoblePrivilegeParams.Default);

        /// <summary>
        /// 特権が維持できるか（true=維持）。改革圧力（reformPressure）から主君の後ろ盾（sovereignBacking）を
        /// 差し引いた正味の圧力が閾値（threshold）未満なら維持できる＝後ろ盾が圧力を相殺する。圧力が後ろ盾を
        /// 上回って閾値を超えると特権は廃止へ向かう（維持不能）。
        /// </summary>
        public static bool IsPrivilegeSustainable(float reformPressure, float sovereignBacking, float threshold)
        {
            float pressure = Mathf.Clamp01(reformPressure);
            float backing = Mathf.Clamp01(sovereignBacking);
            float net = Mathf.Clamp01(pressure - backing);
            return net < Mathf.Clamp01(threshold);
        }

        /// <summary>既定の維持閾値（<see cref="NoblePrivilegeParams.sustainThreshold"/>）で維持可否を判定する簡易窓口。</summary>
        public static bool IsPrivilegeSustainable(float reformPressure, float sovereignBacking)
            => IsPrivilegeSustainable(reformPressure, sovereignBacking, NoblePrivilegeParams.Default.sustainThreshold);
    }
}
