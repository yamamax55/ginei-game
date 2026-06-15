using UnityEngine;

namespace Ginei
{
    /// <summary>穏やかな専制（後見的国家による受動化）の調整係数。</summary>
    public readonly struct SoftDespotismParams
    {
        /// <summary>後見的国家の力＝面倒見の範囲と行政浸透の積に掛かる係数（0..1・揺りかごから墓場まで）。</summary>
        public readonly float tutelaryScale;
        /// <summary>後見が市民を受動化する速度（per tutelaryPower per dt・自分で決めなくなる）。</summary>
        public readonly float passivationRate;
        /// <summary>自律が萎縮する強度＝受動化が自由を行使する能力・意欲を削る非線形度（使わない自由は衰える）。</summary>
        public readonly float atrophyScale;
        /// <summary>幼児化が進む速度（per tutelaryPower×dependency per dt・成熟した市民でなくなる）。</summary>
        public readonly float infantilizationRate;
        /// <summary>政治参加・自発的結社が衰える速度（per autonomyAtrophy per dt・AssociationRules と逆）。</summary>
        public readonly float participationDeclineRate;

        public SoftDespotismParams(float tutelaryScale, float passivationRate,
                                   float atrophyScale, float infantilizationRate,
                                   float participationDeclineRate)
        {
            this.tutelaryScale = Mathf.Clamp01(tutelaryScale);
            this.passivationRate = Mathf.Max(0f, passivationRate);
            this.atrophyScale = Mathf.Max(0f, atrophyScale);
            this.infantilizationRate = Mathf.Max(0f, infantilizationRate);
            this.participationDeclineRate = Mathf.Max(0f, participationDeclineRate);
        }

        /// <summary>既定＝後見係数1/受動化0.1/萎縮1/幼児化0.1/参加衰退0.1。</summary>
        public static SoftDespotismParams Default =>
            new SoftDespotismParams(1f, 0.1f, 1f, 0.1f, 0.1f);
    }

    /// <summary>
    /// 穏やかな専制の純ロジック（TOCQ-4 #1492・トクヴィル『アメリカのデモクラシー』参考）。
    /// 民主社会に固有の<b>新しい専制</b>＝暴力的でなく穏やかな専制。後見的な行政国家が市民の面倒を
    /// 全て見る代わりに、彼らを受動的な子供のように飼い慣らし、自由を行使する意欲を静かに去勢する＝
    /// 市民は<b>快適さと引き換えに自律を手放す</b>（鎖でなく繭・穏やかな隷従）。
    /// <see cref="CoupRules"/> が担う<b>暴力的な権力奪取</b>との対称系＝こちらは福祉的後見による自由の去勢
    /// （暴力なき受動化＝鎮圧でなく依存で支配する）。
    /// <see cref="BreadAndCircusesRules"/>（娯楽と配給によるガス抜き＝表出の鎮静）とは別＝ここは後見国家が
    /// 市民の自律そのものを萎縮させる構造を出す。<see cref="HopeRules"/> が担う希望の枯渇・末人とも別＝
    /// 受動化は意味の喪失ではなく主体性の放棄。<see cref="AssociationRules"/>（自発的結社＝自由の学校）とは
    /// 逆向きの力＝参加の衰退として作用する。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SoftDespotismRules
    {
        /// <summary>
        /// 後見的国家の力（0..1）＝面倒を見る範囲（stateProvision）×行政の浸透（administrativeReach）×係数。
        /// 揺りかごから墓場まで＝両者が揃って初めて後見が市民の生活を覆う（片方が0なら後見は届かない）。
        /// </summary>
        public static float TutelaryPower(float stateProvision, float administrativeReach, SoftDespotismParams p)
        {
            float provision = Mathf.Clamp01(stateProvision);
            float reach = Mathf.Clamp01(administrativeReach);
            return Mathf.Clamp01(p.tutelaryScale * provision * reach);
        }

        public static float TutelaryPower(float stateProvision, float administrativeReach)
            => TutelaryPower(stateProvision, administrativeReach, SoftDespotismParams.Default);

        /// <summary>
        /// 市民の受動化の1tick後の値（0..1）＝受動化＋後見力×伸び代(1−受動化)×受動化速度×dt。
        /// 後見が手厚いほど市民は自分で決めなくなる（依存）＝受動化が進むほど伸びは鈍る（飽和）。
        /// </summary>
        public static float CivicPassivation(float civicPassivation, float tutelaryPower, float dt, SoftDespotismParams p)
        {
            float pass = Mathf.Clamp01(civicPassivation);
            float power = Mathf.Clamp01(tutelaryPower);
            float delta = power * (1f - pass) * p.passivationRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(pass + delta);
        }

        public static float CivicPassivation(float civicPassivation, float tutelaryPower, float dt)
            => CivicPassivation(civicPassivation, tutelaryPower, dt, SoftDespotismParams.Default);

        /// <summary>
        /// 自律の萎縮（0..1）＝受動化が自由を行使する能力・意欲を削った度合い。
        /// 使わない自由は衰える＝受動化が深いほど自律は非線形に萎縮する（atrophyScale で曲率）。
        /// </summary>
        public static float AutonomyAtrophy(float civicPassivation, SoftDespotismParams p)
        {
            float pass = Mathf.Clamp01(civicPassivation);
            // 受動化を atrophyScale で増幅して飽和（1−(1−受動化)^(1+萎縮係数)）＝
            // 萎縮係数が大きいほど低い受動化でも自律が速く削れる（使わない自由ほど速く錆びる）。
            float exponent = 1f + p.atrophyScale;
            return Mathf.Clamp01(1f - Mathf.Pow(1f - pass, exponent));
        }

        public static float AutonomyAtrophy(float civicPassivation)
            => AutonomyAtrophy(civicPassivation, SoftDespotismParams.Default);

        /// <summary>
        /// 快適さと自由の取引（0..1）＝快適さ（comfort）と引き換えに手放される自律の量。
        /// 穏やかな隷従の心地よさ＝快適さが高く残存自律が高いほど、市民は進んで自律を手放す
        /// （comfort×autonomy＝差し出せる自律があり、その対価に快適があるときだけ取引が成立する）。
        /// </summary>
        public static float ComfortForFreedomTrade(float comfort, float autonomy)
        {
            float c = Mathf.Clamp01(comfort);
            float a = Mathf.Clamp01(autonomy);
            return Mathf.Clamp01(c * a);
        }

        /// <summary>
        /// 幼児化の1tick後の値（0..1）＝幼児化＋後見力×依存×伸び代(1−幼児化)×幼児化速度×dt。
        /// 市民が政府に依存する子供のようになる＝後見が手厚く依存が深いほど成熟した市民でなくなる。
        /// </summary>
        public static float Infantilization(float infantilization, float tutelaryPower, float dependency, float dt, SoftDespotismParams p)
        {
            float inf = Mathf.Clamp01(infantilization);
            float power = Mathf.Clamp01(tutelaryPower);
            float dep = Mathf.Clamp01(dependency);
            float delta = power * dep * (1f - inf) * p.infantilizationRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(inf + delta);
        }

        public static float Infantilization(float infantilization, float tutelaryPower, float dependency, float dt)
            => Infantilization(infantilization, tutelaryPower, dependency, dt, SoftDespotismParams.Default);

        /// <summary>
        /// 静かな支配の強度（0..1）＝受動化そのものが支配力に転じる度合い。
        /// 暴力でなく受動化による支配＝鎖でなく繭。受動化が進むほど（暴力を一切使わずに）
        /// 体制は市民を御しやすくなる（<see cref="CoupRules"/> の暴力的奪取の対称＝抵抗の意欲が消える）。
        /// </summary>
        public static float SoftControl(float civicPassivation)
        {
            return Mathf.Clamp01(civicPassivation);
        }

        /// <summary>
        /// 政治参加・自発的結社の衰退の1tick後の参加度（0..1）＝参加−自律萎縮×衰退速度×dt。
        /// 自律が萎縮するほど投票も結社も衰える（<see cref="AssociationRules"/> と逆向きの力＝
        /// 自由の学校が空になる）。参加は受動化の鏡像として静かに枯れていく。
        /// </summary>
        public static float ParticipationDecline(float participation, float autonomyAtrophy, float dt, SoftDespotismParams p)
        {
            float part = Mathf.Clamp01(participation);
            float atrophy = Mathf.Clamp01(autonomyAtrophy);
            float delta = atrophy * p.participationDeclineRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(part - delta);
        }

        public static float ParticipationDecline(float participation, float autonomyAtrophy, float dt)
            => ParticipationDecline(participation, autonomyAtrophy, dt, SoftDespotismParams.Default);

        /// <summary>
        /// 穏やかな専制の判定。受動化（civicPassivation）と自律の萎縮（autonomyAtrophy）が
        /// ともに閾値を超えたら true＝受動的市民の上に立つ後見国家が成立している（暴力なき民主的専制）。
        /// 一方だけでは足りない＝飼い慣らされた受動性と去勢された自律の両輪が揃って穏やかな専制になる。
        /// </summary>
        public static bool IsSoftDespotism(float civicPassivation, float autonomyAtrophy, float threshold)
        {
            float t = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(civicPassivation) > t && Mathf.Clamp01(autonomyAtrophy) > t;
        }
    }
}
