using UnityEngine;

namespace Ginei
{
    /// <summary>上位権威（義帝＝楚の懐王）の純データ。誰がこの権威を正統に代弁するかを競う。</summary>
    public struct MetaAuthority
    {
        /// <summary>上位権威の威光（0..1）。義帝の名そのものの重み＝奉じる旗印の価値の源泉。</summary>
        public float authorityPrestige;
        /// <summary>自陣の代弁権主張（0..1）。「我こそ義帝の正統な代弁者」という主張の強さ。</summary>
        public float spokesmanClaim;
        /// <summary>権威への敬意の示し方（0..1）。義帝を厚く奉じるほど大義を得る（蔑ろは逆効果）。</summary>
        public float reverence;

        public MetaAuthority(float authorityPrestige, float spokesmanClaim, float reverence)
        {
            this.authorityPrestige = Mathf.Clamp01(authorityPrestige);
            this.spokesmanClaim = Mathf.Clamp01(spokesmanClaim);
            this.reverence = Mathf.Clamp01(reverence);
        }
    }

    /// <summary>大義名分の競合の調整係数。</summary>
    public readonly struct MetaLegitimacyParams
    {
        /// <summary>大義名分（代弁の正統性）が動員・支持へ与えるボーナスの最大幅（旗印の力）。</summary>
        public readonly float championingBonusScale;
        /// <summary>権威への冒涜のペナルティの最大幅（義帝弑殺で失う大義の最大）。</summary>
        public readonly float desecrationPenaltyScale;
        /// <summary>弔い合戦（討伐）の正統性ボーナスの最大幅（劉邦の義帝弔い）。</summary>
        public readonly float avengerBonusScale;
        /// <summary>権威依存の係数（権威が威光を失うと、依存した正統性がこの割合まで揺らぐ）。</summary>
        public readonly float dependenceScale;
        /// <summary>傀儡掌握の簒奪誘惑の係数（権威を握り自力が強いほど廃して自ら立つ誘惑）。</summary>
        public readonly float puppetMasterScale;
        /// <summary>正統な代弁者と認められる正統性の閾値。</summary>
        public readonly float championThreshold;

        public MetaLegitimacyParams(float championingBonusScale, float desecrationPenaltyScale,
                                    float avengerBonusScale, float dependenceScale,
                                    float puppetMasterScale, float championThreshold)
        {
            this.championingBonusScale = Mathf.Max(0f, championingBonusScale);
            this.desecrationPenaltyScale = Mathf.Max(0f, desecrationPenaltyScale);
            this.avengerBonusScale = Mathf.Max(0f, avengerBonusScale);
            this.dependenceScale = Mathf.Clamp01(dependenceScale);
            this.puppetMasterScale = Mathf.Max(0f, puppetMasterScale);
            this.championThreshold = Mathf.Clamp01(championThreshold);
        }

        /// <summary>既定＝旗印ボーナス0.5・冒涜ペナルティ0.7・弔い合戦0.4・依存0.6・傀儡誘惑0.5・代弁者閾値0.5。</summary>
        public static MetaLegitimacyParams Default =>
            new MetaLegitimacyParams(0.5f, 0.7f, 0.4f, 0.6f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 大義名分の競合の純ロジック（KORY-3 #1411・項羽と劉邦）。外部の上位権威（義帝＝楚の懐王）を
    /// 「誰が正統に<b>代弁</b>するか」を争う力学をモデル化する。項羽も劉邦も名目上は義帝を奉じたが、
    /// その代弁権を争った＝義帝を厚く奉じる者が大義名分を得（<see cref="SpokesmanLegitimacy"/>／
    /// <see cref="AuthorityChampioningBonus"/>＝旗印の力）、その権威を害した者は大義を失い（項羽の義帝弑殺＝
    /// <see cref="DesecrationPenalty"/>）、競合が権威を害したときは弔い合戦・討伐を旗印にして正統性を得る
    /// （劉邦の義帝弔い＝<see cref="AvengerLegitimacy"/>）。上位権威に依存した正統性は権威が失墜すると共倒れし
    /// （<see cref="AuthorityDependence"/>）、権威を傀儡として握れば、いずれ廃して自ら立つ誘惑が生じる
    /// （<see cref="PuppetMasterRisk"/>＝簒奪は <see cref="RegencyRules"/> の摂政・傀儡へ接続）。
    /// 会戦指揮への将兵の服従＝指揮権の正統性（<see cref="CommandLegitimacyRules"/> #898・生成済み）・
    /// 開戦の口実と厭戦（<see cref="WarGoalRules"/>）とは別系統＝<b>義帝を奉じる型の上位権威の代弁争い</b>を扱い、
    /// その中核データが <see cref="MetaAuthority"/>。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MetaLegitimacyRules
    {
        /// <summary>
        /// 自陣がその上位権威を正統に代弁する正統性（0..1）＝上位権威の威光 × 敬意の示し方。
        /// 義帝を厚く奉じる者が大義を得る。競合 rivalClaim が代弁権を主張するほど自陣の取り分は薄まる
        /// （同じ権威を二人が奉じれば、敬意で勝る方がより正統＝代弁権の按分）。
        /// </summary>
        public static float SpokesmanLegitimacy(float authorityPrestige, float reverence, float rivalClaim)
        {
            float prestige = Mathf.Clamp01(authorityPrestige);
            float rev = Mathf.Clamp01(reverence);
            float rival = Mathf.Clamp01(rivalClaim);
            // 敬意で代弁権を按分：自陣の取り分 = rev /(rev + rival)。競合不在なら満額。
            float share = (rev + rival) <= 0f ? 0f : rev / (rev + rival);
            return Mathf.Clamp01(prestige * rev * share);
        }

        /// <summary>
        /// 大義名分（上位権威の代弁）が動員・支持へ与えるボーナス（0..championingBonusScale）。
        /// 旗印の力＝正統な代弁者ほど人が集まる。基準値へ上乗せして使う（実効値パターン・基準非破壊）。
        /// </summary>
        public static float AuthorityChampioningBonus(float spokesmanLegitimacy, MetaLegitimacyParams p)
        {
            return Mathf.Clamp01(spokesmanLegitimacy) * p.championingBonusScale;
        }

        public static float AuthorityChampioningBonus(float spokesmanLegitimacy)
            => AuthorityChampioningBonus(spokesmanLegitimacy, MetaLegitimacyParams.Default);

        /// <summary>
        /// 上位権威を害する（弑する・蔑ろにする）と大義名分を失うペナルティ（0..desecrationPenaltyScale）＝
        /// 冒涜の度合い × 害した権威の威光。威光ある権威ほど、害したときの反動が大きい
        /// （項羽の義帝弑殺＝正統性喪失）。基準正統性から差し引いて使う。
        /// </summary>
        public static float DesecrationPenalty(float reverenceViolation, float authorityPrestige, MetaLegitimacyParams p)
        {
            float viol = Mathf.Clamp01(reverenceViolation);
            float prestige = Mathf.Clamp01(authorityPrestige);
            return viol * prestige * p.desecrationPenaltyScale;
        }

        public static float DesecrationPenalty(float reverenceViolation, float authorityPrestige)
            => DesecrationPenalty(reverenceViolation, authorityPrestige, MetaLegitimacyParams.Default);

        /// <summary>
        /// 弔い合戦・討伐の正統性（0..1）＝競合の冒涜 rivalDesecration × 自陣の弔いの応え方 championingResponse。
        /// 競合が権威を害したとき、それを討つ旗印を掲げるほど正統性を得る（劉邦の義帝弔い合戦）。
        /// 競合が害していなければ（rivalDesecration=0）弔い合戦の名分は立たない。
        /// </summary>
        public static float AvengerLegitimacy(float rivalDesecration, float championingResponse, MetaLegitimacyParams p)
        {
            float des = Mathf.Clamp01(rivalDesecration);
            float resp = Mathf.Clamp01(championingResponse);
            return Mathf.Clamp01(des * resp * p.avengerBonusScale + 0f);
        }

        public static float AvengerLegitimacy(float rivalDesecration, float championingResponse)
            => AvengerLegitimacy(rivalDesecration, championingResponse, MetaLegitimacyParams.Default);

        /// <summary>
        /// 代弁権の競合度（0..1）＝同じ上位権威の代弁権を複数が争うほど高い。自陣・競合の主張が
        /// 拮抗するほど激しい（どちらが正統な代弁者か未決＝1に近づく）。一方が圧倒すれば決着して低い。
        /// </summary>
        public static float ClaimContest(float ownClaim, float rivalClaim)
        {
            float own = Mathf.Clamp01(ownClaim);
            float rival = Mathf.Clamp01(rivalClaim);
            float sum = own + rival;
            if (sum <= 0f) return 0f;
            // 拮抗度＝2×min/sum（双方が等しいとき1、片方0で0）×両者の存在の強さ。
            float balance = 2f * Mathf.Min(own, rival) / sum;
            float intensity = Mathf.Clamp01(sum * 0.5f);
            return Mathf.Clamp01(balance * intensity);
        }

        /// <summary>
        /// 上位権威への依存度（0..1）＝代弁による正統性のうち、権威の威光に支えられている割合。
        /// 権威が高威光なほど依存が深く、権威が失墜すれば依存した正統性ほど大きく揺らぐ（傀儡の権威が
        /// 崩れると共倒れ）。自前の地力でなく旗印に頼った正統性の脆さを示す。
        /// </summary>
        public static float AuthorityDependence(float spokesmanLegitimacy, float authorityPrestige, MetaLegitimacyParams p)
        {
            float legit = Mathf.Clamp01(spokesmanLegitimacy);
            float prestige = Mathf.Clamp01(authorityPrestige);
            return Mathf.Clamp01(legit * prestige * p.dependenceScale);
        }

        public static float AuthorityDependence(float spokesmanLegitimacy, float authorityPrestige)
            => AuthorityDependence(spokesmanLegitimacy, authorityPrestige, MetaLegitimacyParams.Default);

        /// <summary>
        /// 傀儡掌握の簒奪誘惑（0..1）＝上位権威の掌握度 authorityControl × 自力 ownPower。
        /// 権威を傀儡として握り、自らの地力が強いほど「もはや奉じる必要なし＝廃して自ら立つ」誘惑が膨らむ
        /// （簒奪へ＝<see cref="RegencyRules.UsurpationTemptation"/> と同方針）。係数で誘惑の強さを調整。
        /// </summary>
        public static float PuppetMasterRisk(float authorityControl, float ownPower, MetaLegitimacyParams p)
        {
            float ctrl = Mathf.Clamp01(authorityControl);
            float power = Mathf.Clamp01(ownPower);
            return Mathf.Clamp01(ctrl * power * p.puppetMasterScale);
        }

        public static float PuppetMasterRisk(float authorityControl, float ownPower)
            => PuppetMasterRisk(authorityControl, ownPower, MetaLegitimacyParams.Default);

        /// <summary>上位権威の正統な代弁者と認められるか＝代弁の正統性が閾値以上。</summary>
        public static bool IsLegitimateChampion(float spokesmanLegitimacy, MetaLegitimacyParams p)
        {
            return Mathf.Clamp01(spokesmanLegitimacy) >= p.championThreshold;
        }

        public static bool IsLegitimateChampion(float spokesmanLegitimacy)
            => IsLegitimateChampion(spokesmanLegitimacy, MetaLegitimacyParams.Default);
    }
}
