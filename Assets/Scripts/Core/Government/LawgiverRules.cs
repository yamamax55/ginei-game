using UnityEngine;

namespace Ginei
{
    /// <summary>立法者パラドックスの調整係数（#1464・ルソー『社会契約論』立法者 législateur）。</summary>
    public readonly struct LawgiverParams
    {
        /// <summary>構成のパラドックスの強さ（良き法↔良き市民の循環依存の重み）。</summary>
        public readonly float paradoxStrength;
        /// <summary>制度の刻印の持続率（建国時の型がどれだけ長く残るか＝経路依存）。</summary>
        public readonly float imprintPersistence;
        /// <summary>立法者の退場速度（法を与えたら権力を手放す傾き）。</summary>
        public readonly float selfRemovalRate;
        /// <summary>成功した建国の判定閾値（建国の好機×退場の最低線）。</summary>
        public readonly float foundingThreshold;

        public LawgiverParams(float paradoxStrength, float imprintPersistence,
            float selfRemovalRate, float foundingThreshold)
        {
            this.paradoxStrength = Mathf.Clamp01(paradoxStrength);
            this.imprintPersistence = Mathf.Clamp01(imprintPersistence);
            this.selfRemovalRate = Mathf.Max(0f, selfRemovalRate);
            this.foundingThreshold = Mathf.Clamp01(foundingThreshold);
        }

        /// <summary>既定＝パラドックス強度1.0・刻印持続0.9・退場速度0.5・建国閾値0.5。</summary>
        public static LawgiverParams Default =>
            new LawgiverParams(1f, 0.9f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 立法者パラドックスの純ロジック（#1464・ルソー『社会契約論』立法者 législateur・ROUS-2）。
    /// 建国にあたり法と制度を作る非凡な人物＝<b>立法者</b>のパラドックスを式に出す：人民を法に従う良き市民へ
    /// 作り変えるには既に良き法が要るが、良き法を制定し受け入れさせるには既に良き市民が要る
    /// （<see cref="ConstitutiveParadox"/>＝鶏と卵の循環）。立法者はこの循環を断つため<b>制度の外に立つ
    /// 一回性の存在</b>（リュクルゴス・モーセ型）＝自ら統治せず法だけ与える権力なき権威
    /// （<see cref="ExtraInstitutionalAuthority"/>）。理性でなく権威・聖性で法を受け入れさせ
    /// （<see cref="CharismaticPersuasion"/>＝神の名を借りる）、建国という<b>一回性の制度初期化</b>
    /// （<see cref="OneTimeInitialization"/>）で最初の型を刻み（<see cref="InstitutionalImprint"/>＝経路依存）、
    /// 法を与えたら身を引く（<see cref="LawgiverSelfRemoval"/>）。
    /// <see cref="ConstitutionRules"/>（憲法の制約範囲＝権力分立／法の支配で権力を縛る）とは別＝こちらは
    /// 制度を<b>作る者</b>が制度の外に立つ一回性の憲法制定権力。
    /// <see cref="FounderTrajectoryRules"/>（建国者の自己廃絶＝権力を握り続けるか移譲するかの軌道）とは別＝
    /// こちらは立法者が自ら統治せず<b>法だけを与えて去る</b>（権力を持たない権威）＝退場は整合する。
    /// 同EPIC ROUS の <c>GeneralWillRules</c>（一般意志＝人民全体の意志の集約）とは別＝こちらは一般意志に
    /// 形を与える法の制定者。<see cref="HerrschaftRules"/>（カリスマ的支配＝ウェーバー）とは別＝こちらは
    /// 支配の正統性ではなく建国時の一回性の制度初期化。
    /// 全入力クランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class LawgiverRules
    {
        /// <summary>
        /// 構成のパラドックス（0..1）＝良き法には良き市民が要り良き市民には良き法が要る循環の強さ（鶏と卵）。
        /// 両方が不足するほど循環依存は強い＝双方の欠如度の積（×paradoxStrength）。どちらかが既に満ちていれば
        /// 循環は弱まる（足場がある）。両方ゼロで最大＝立法者が断たねば誰も先に進めない。
        /// </summary>
        public static float ConstitutiveParadox(float needGoodLaws, float needGoodCitizens, LawgiverParams p)
        {
            float laws = Mathf.Clamp01(needGoodLaws);
            float citizens = Mathf.Clamp01(needGoodCitizens);
            // 欠如度の積＝両方欠けているほど循環が強い
            float lack = (1f - laws) * (1f - citizens);
            return Mathf.Clamp01(lack * p.paradoxStrength);
        }

        public static float ConstitutiveParadox(float needGoodLaws, float needGoodCitizens)
            => ConstitutiveParadox(needGoodLaws, needGoodCitizens, LawgiverParams.Default);

        /// <summary>
        /// 建国の好機（0..1）＝立法者の知恵×人民の機運で建国の好機（制度初期化の好機）。
        /// 非凡な立法者と機の熟した人民が揃ってはじめて建国は成る＝両者の積。どちらかが欠ければ好機は閉じる
        /// （賢者なき機運も機運なき賢者も建国に至らない）。
        /// </summary>
        public static float FoundingMomentValue(float lawgiverWisdom, float popularReadiness)
        {
            float wisdom = Mathf.Clamp01(lawgiverWisdom);
            float readiness = Mathf.Clamp01(popularReadiness);
            return Mathf.Clamp01(wisdom * readiness);
        }

        /// <summary>
        /// 制度外の権威（0..1）＝立法者は通常の権力構造の外に立つ（自ら統治せず法だけ与える＝権力を持たない権威）。
        /// 知恵が高く、かつ通常の権力（命令・強制の手段）を持たないほど純粋な制度外の権威となる＝
        /// 知恵×(1−通常権力)。権力を握れば立法者ではなく支配者に堕す（リュクルゴスは王位に就かない）。
        /// </summary>
        public static float ExtraInstitutionalAuthority(float lawgiverWisdom, float ordinaryPower)
        {
            float wisdom = Mathf.Clamp01(lawgiverWisdom);
            float power = Mathf.Clamp01(ordinaryPower);
            return Mathf.Clamp01(wisdom * (1f - power));
        }

        /// <summary>
        /// 一回性の初期化（0..1）＝制度の一回性の初期化（建国は繰り返せない＝最初の刻印）。
        /// 建国の好機がそのまま最初の制度の型として刻まれる＝一回限りの初期値。値そのものを初期刻印として返す
        /// （好機が高いほど深く正しい型が刻まれる）。建国はやり直せない＝この一刻みが後を決める。
        /// </summary>
        public static float OneTimeInitialization(float foundingMomentValue)
        {
            return Mathf.Clamp01(foundingMomentValue);
        }

        /// <summary>
        /// カリスマ的説得（0..1）＝立法者は理性でなく権威・聖性で法を受け入れさせる（ルソー＝神の名を借りる）。
        /// 制度外の権威（自ら統治しない純粋さ）と神聖さの訴求が合わさって人民は法を進んで受け入れる＝
        /// 権威×聖性。論証では動かぬ人民も、権威ある聖なる声には従う（モーセの十戒）。
        /// </summary>
        public static float CharismaticPersuasion(float lawgiverAuthority, float divineAppeal)
        {
            float authority = Mathf.Clamp01(lawgiverAuthority);
            float divine = Mathf.Clamp01(divineAppeal);
            return Mathf.Clamp01(authority * divine);
        }

        /// <summary>
        /// 制度の刻印（0..1）＝建国時に刻まれた制度の型がその後も持続する（最初の設計が長く効く＝経路依存）。
        /// 初期刻印が時間とともに imprintPersistence の率で減衰しつつ残る＝oneTimeInit × persistence^dt 相当を
        /// 線形近似で1tickぶん（init − init×(1−persistence)×dt）。最初の型は完全には消えない＝建国の刻印は長く効く。
        /// </summary>
        public static float InstitutionalImprint(float oneTimeInit, float dt, LawgiverParams p)
        {
            float init = Mathf.Clamp01(oneTimeInit);
            float d = Mathf.Max(0f, dt);
            float decay = (1f - p.imprintPersistence) * d; // 持続率が高いほど減りは小さい
            return Mathf.Clamp01(init - init * decay);
        }

        public static float InstitutionalImprint(float oneTimeInit, float dt)
            => InstitutionalImprint(oneTimeInit, dt, LawgiverParams.Default);

        /// <summary>
        /// 立法者の退場（0..1）＝立法者は法を与えたら身を引く（権力に居座らない＝FounderTrajectory の自己廃絶と
        /// 整合）。残存する立法者権力が selfRemovalRate × dt の速さで手放されていく＝power − power×rate×dt。
        /// 立法者は統治者にならず去る＝時間とともに権力ゼロへ向かう（リュクルゴスの亡命）。
        /// </summary>
        public static float LawgiverSelfRemoval(float lawgiverPower, float dt, LawgiverParams p)
        {
            float power = Mathf.Clamp01(lawgiverPower);
            float d = Mathf.Max(0f, dt);
            return Mathf.Clamp01(power - power * p.selfRemovalRate * d);
        }

        public static float LawgiverSelfRemoval(float lawgiverPower, float dt)
            => LawgiverSelfRemoval(lawgiverPower, dt, LawgiverParams.Default);

        /// <summary>
        /// 成功した建国の判定＝立法者が良き法を与え身を引いた成功した建国か。建国の好機が閾値以上に高く
        /// （良き法を与えた）、かつ退場が進んで立法者権力が閾値以下に下がっている（身を引いた）こと。
        /// 法だけ残して権力を手放す＝立法者は循環を断ち制度を初期化して去る、というルソーの理想形の真偽版。
        /// </summary>
        public static bool IsSuccessfulFounding(float foundingMomentValue, float lawgiverSelfRemoval,
            float threshold, LawgiverParams p)
        {
            float moment = Mathf.Clamp01(foundingMomentValue);
            float remaining = Mathf.Clamp01(lawgiverSelfRemoval);
            float t = Mathf.Clamp01(threshold);
            // 良き法を与え（好機が閾値以上）かつ身を引いた（残存権力が閾値以下＝1−閾値以上手放した）
            return moment >= t && remaining <= (1f - t);
        }

        public static bool IsSuccessfulFounding(float foundingMomentValue, float lawgiverSelfRemoval, float threshold)
            => IsSuccessfulFounding(foundingMomentValue, lawgiverSelfRemoval, threshold, LawgiverParams.Default);
    }
}
