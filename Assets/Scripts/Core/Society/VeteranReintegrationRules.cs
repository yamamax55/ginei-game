using UnityEngine;

namespace Ginei
{
    /// <summary>退役兵の社会復帰（復員と再適応）の調整係数。</summary>
    public readonly struct VeteranReintegrationParams
    {
        /// <summary>軍務の断絶が再適応を阻む重み（serviceDisruption の寄与・大きいほど社会から離れていた）。</summary>
        public readonly float disruptionWeight;
        /// <summary>戦争の傷（トラウマ）が再適応を阻む重み（traumaBurden の寄与・心身の傷）。</summary>
        public readonly float traumaWeight;
        /// <summary>就労支援が成功に寄与する重み（職業訓練×経済機会の利き）。</summary>
        public readonly float employmentWeight;
        /// <summary>社会の受け入れが成功に寄与する重み（感謝×汚名なさの利き）。</summary>
        public readonly float acceptanceWeight;
        /// <summary>適応失敗が不満を生む重み（つまずきの政治化への入口）。</summary>
        public readonly float grievanceWeight;
        /// <summary>反故にされた約束が不満を増幅する重み（恩給未払い・約束された職の欠如）。</summary>
        public readonly float brokenPromiseWeight;
        /// <summary>復員兵の数が政治化を増幅する重み（数が多いほど政治勢力になる）。</summary>
        public readonly float numbersWeight;

        public VeteranReintegrationParams(float disruptionWeight, float traumaWeight, float employmentWeight,
                                          float acceptanceWeight, float grievanceWeight, float brokenPromiseWeight,
                                          float numbersWeight)
        {
            this.disruptionWeight = Mathf.Clamp01(disruptionWeight);
            this.traumaWeight = Mathf.Clamp01(traumaWeight);
            this.employmentWeight = Mathf.Clamp01(employmentWeight);
            this.acceptanceWeight = Mathf.Clamp01(acceptanceWeight);
            this.grievanceWeight = Mathf.Clamp01(grievanceWeight);
            this.brokenPromiseWeight = Mathf.Clamp01(brokenPromiseWeight);
            this.numbersWeight = Mathf.Clamp01(numbersWeight);
        }

        /// <summary>既定＝断絶0.5/トラウマ0.6/就労0.55/受入0.45/不満0.7/約束0.5/数0.6。</summary>
        public static VeteranReintegrationParams Default =>
            new VeteranReintegrationParams(0.5f, 0.6f, 0.55f, 0.45f, 0.7f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 退役兵の社会復帰（復員と再適応）の純ロジック。戦争を生き延びた兵が軍服を脱いで民間社会へ戻るとき、
    /// 軍務による社会との断絶と戦争の傷（心身のトラウマ）が再適応を難しくする。就労支援（職業訓練×経済機会）と
    /// 社会の受け入れ（感謝×汚名のなさ）が成功を支えるが、適応に失敗し約束（恩給・雇用）が反故にされると
    /// 復員兵は不満を抱き、数が多ければそれは政治勢力（退役軍人の政治化）に転じる。戦友会（戦友の支え）は
    /// 復帰の困難をやわらげる。「復員兵の社会復帰の成否＝就労×受け入れ−困難で決まり、失敗と裏切られた約束は
    /// 不満を生み、その不満は数の多さで政治化するが、戦友会の絆がそれを支える」を式に出す。
    /// 練度の <see cref="VeterancyRules"/>（会戦経験の熟練度）・戦友愛の KameradschaftRules（小集団の凝集）とは
    /// 別系統＝こちらは退役後・民間社会への復帰そのもの。出力は係数/0..1。乱数なし決定論・全入力クランプ。
    /// 配列null/空安全・符号付き出力は-1..1にクランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class VeteranReintegrationRules
    {
        /// <summary>
        /// 再適応の困難（0..1）。軍務による社会との断絶 serviceDisruption と戦争の傷 traumaBurden が
        /// それぞれの重みで困難を積む＝長く軍にいて心身に傷を負うほど民間社会へ戻りにくい。
        /// </summary>
        public static float ReintegrationDifficulty(float serviceDisruption, float traumaBurden,
                                                    VeteranReintegrationParams p)
        {
            float d = Mathf.Clamp01(serviceDisruption);
            float t = Mathf.Clamp01(traumaBurden);
            return Mathf.Clamp01(d * p.disruptionWeight + t * p.traumaWeight);
        }

        public static float ReintegrationDifficulty(float serviceDisruption, float traumaBurden)
            => ReintegrationDifficulty(serviceDisruption, traumaBurden, VeteranReintegrationParams.Default);

        /// <summary>
        /// 復員兵の就労（0..1）。就労支援策 jobPrograms と経済機会 economicOpportunity の積×重み
        /// ＝制度（職業訓練・斡旋）と地合い（雇用のある経済）の両方が揃って初めて職に就ける（一方が欠ければ弱い）。
        /// </summary>
        public static float EmploymentSupport(float jobPrograms, float economicOpportunity,
                                              VeteranReintegrationParams p)
        {
            float j = Mathf.Clamp01(jobPrograms);
            float e = Mathf.Clamp01(economicOpportunity);
            return Mathf.Clamp01(j * e * (1f + p.employmentWeight));
        }

        public static float EmploymentSupport(float jobPrograms, float economicOpportunity)
            => EmploymentSupport(jobPrograms, economicOpportunity, VeteranReintegrationParams.Default);

        /// <summary>
        /// 社会の受け入れ（0..1）。世間の感謝 publicGratitude ×（1−戦争の汚名 warStigma）
        /// ＝英雄として迎えられるか、敗戦や不人気な戦争の汚名を着せられるかで受け入れが変わる。
        /// </summary>
        public static float SocialAcceptance(float publicGratitude, float warStigma,
                                             VeteranReintegrationParams p)
        {
            float g = Mathf.Clamp01(publicGratitude);
            float s = Mathf.Clamp01(warStigma);
            return Mathf.Clamp01(g * (1f - s) * (0.5f + p.acceptanceWeight));
        }

        public static float SocialAcceptance(float publicGratitude, float warStigma)
            => SocialAcceptance(publicGratitude, warStigma, VeteranReintegrationParams.Default);

        /// <summary>
        /// 社会復帰の成功（0..1）。就労 employmentSupport と受け入れ socialAcceptance の平均から
        /// 再適応の困難 reintegrationDifficulty を差し引く＝支えが困難を上回れば復帰は進む。
        /// </summary>
        public static float AdjustmentSuccess(float employmentSupport, float socialAcceptance,
                                              float reintegrationDifficulty, VeteranReintegrationParams p)
        {
            float emp = Mathf.Clamp01(employmentSupport);
            float acc = Mathf.Clamp01(socialAcceptance);
            float diff = Mathf.Clamp01(reintegrationDifficulty);
            float support = 0.5f * (emp + acc);
            return Mathf.Clamp01(support - diff);
        }

        public static float AdjustmentSuccess(float employmentSupport, float socialAcceptance,
                                              float reintegrationDifficulty)
            => AdjustmentSuccess(employmentSupport, socialAcceptance, reintegrationDifficulty,
                                 VeteranReintegrationParams.Default);

        /// <summary>
        /// 復員兵の不満（0..1）。適応失敗 adjustmentFailure と反故にされた約束 brokenPromises がそれぞれの重みで
        /// 不満を積む＝食えず居場所もなく、約束された恩給や職が果たされなければ恨みが募る。
        /// </summary>
        public static float VeteranGrievance(float adjustmentFailure, float brokenPromises,
                                             VeteranReintegrationParams p)
        {
            float f = Mathf.Clamp01(adjustmentFailure);
            float b = Mathf.Clamp01(brokenPromises);
            return Mathf.Clamp01(f * p.grievanceWeight + b * p.brokenPromiseWeight);
        }

        public static float VeteranGrievance(float adjustmentFailure, float brokenPromises)
            => VeteranGrievance(adjustmentFailure, brokenPromises, VeteranReintegrationParams.Default);

        /// <summary>
        /// 政治化（退役軍人の政治勢力化・0..1）。不満 veteranGrievance ×復員兵の数 veteranNumbers
        /// ＝不満が一人なら泣き寝入りだが、数が揃えば組織された政治圧力（退役軍人団体・運動）になる。
        /// </summary>
        public static float Politicization(float veteranGrievance, float veteranNumbers,
                                           VeteranReintegrationParams p)
        {
            float g = Mathf.Clamp01(veteranGrievance);
            float n = Mathf.Clamp01(veteranNumbers);
            return Mathf.Clamp01(g * n * (1f + p.numbersWeight));
        }

        public static float Politicization(float veteranGrievance, float veteranNumbers)
            => Politicization(veteranGrievance, veteranNumbers, VeteranReintegrationParams.Default);

        /// <summary>
        /// 戦友の支え（0..1）。戦友会 veteranAssociations ×共有アイデンティティ sharedIdentity
        /// ＝同じ戦場をくぐった者だけが分かり合える紐帯（戦友会）が、復帰の孤独と困難をやわらげる。
        /// </summary>
        public static float ComradeNetworkSupport(float veteranAssociations, float sharedIdentity)
        {
            float a = Mathf.Clamp01(veteranAssociations);
            float i = Mathf.Clamp01(sharedIdentity);
            return Mathf.Clamp01(a * i);
        }

        /// <summary>社会復帰が安定的か（適応成功が十分高く、政治化が閾値未満）。</summary>
        public static bool IsReintegrationStable(float adjustmentSuccess, float politicization, float threshold)
        {
            float s = Mathf.Clamp01(adjustmentSuccess);
            float pol = Mathf.Clamp01(politicization);
            float th = Mathf.Clamp01(threshold);
            return s >= th && pol < th;
        }

        /// <summary>既定閾値0.5で社会復帰の安定を判定。</summary>
        public static bool IsReintegrationStable(float adjustmentSuccess, float politicization)
            => IsReintegrationStable(adjustmentSuccess, politicization, 0.5f);
    }
}
