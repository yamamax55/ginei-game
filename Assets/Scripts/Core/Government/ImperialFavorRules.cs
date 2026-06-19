using UnityEngine;

namespace Ginei
{
    /// <summary>君寵（君主の寵愛）の純ロジックの調整係数。</summary>
    public readonly struct ImperialFavorParams
    {
        /// <summary>寵獲得における功績の重み（実力で得る寵の割合）。</summary>
        public readonly float serviceWeight;
        /// <summary>寵獲得における追従（おべっか）の重み。</summary>
        public readonly float flatteryWeight;
        /// <summary>寵獲得における個人的親密さの重み。</summary>
        public readonly float rapportWeight;
        /// <summary>寵臣の権勢で寵が君主権力を借りる強さ（虎の威を借る倍率）。</summary>
        public readonly float influenceLeverage;
        /// <summary>寵の不安定さの基準＝君主の気まぐれに対する感度。</summary>
        public readonly float volatilityScale;
        /// <summary>寵の根拠（功績の実質）が薄いほど不安定さが増す効き（根拠の下駄＝割り算の発散防止）。</summary>
        public readonly float basisFloor;
        /// <summary>増長の強さ（権勢×人格の低さに掛ける）。</summary>
        public readonly float arroganceScale;
        /// <summary>周囲の追従の強さ（権勢に応じてすり寄る度合い）。</summary>
        public readonly float sycophancyScale;
        /// <summary>周囲の反感の強さ（増長×追従に掛ける）。</summary>
        public readonly float resentmentScale;
        /// <summary>寵愛喪失（失脚）の起きやすさ（不安定さ×増長×反感に掛ける）。</summary>
        public readonly float lossScale;
        /// <summary>寵が安泰とみなす既定閾値（IsFavorSecure 委譲版）。</summary>
        public readonly float secureThreshold;

        public ImperialFavorParams(float serviceWeight, float flatteryWeight, float rapportWeight,
                                   float influenceLeverage, float volatilityScale, float basisFloor,
                                   float arroganceScale, float sycophancyScale, float resentmentScale,
                                   float lossScale, float secureThreshold)
        {
            this.serviceWeight = Mathf.Clamp01(serviceWeight);
            this.flatteryWeight = Mathf.Clamp01(flatteryWeight);
            this.rapportWeight = Mathf.Clamp01(rapportWeight);
            this.influenceLeverage = Mathf.Max(0f, influenceLeverage);
            this.volatilityScale = Mathf.Max(0f, volatilityScale);
            this.basisFloor = Mathf.Clamp(basisFloor, 0.01f, 1f);
            this.arroganceScale = Mathf.Max(0f, arroganceScale);
            this.sycophancyScale = Mathf.Max(0f, sycophancyScale);
            this.resentmentScale = Mathf.Max(0f, resentmentScale);
            this.lossScale = Mathf.Max(0f, lossScale);
            this.secureThreshold = Mathf.Clamp01(secureThreshold);
        }

        /// <summary>既定＝功績0.5/追従0.3/親密0.2・虎の威1.0・不安定1.0・根拠下駄0.2・増長1.0・追従1.0・反感1.0・失墜1.0・安泰閾値0.4。</summary>
        public static ImperialFavorParams Default =>
            new ImperialFavorParams(0.5f, 0.3f, 0.2f, 1.0f, 1.0f, 0.2f, 1.0f, 1.0f, 1.0f, 1.0f, 0.4f);
    }

    /// <summary>
    /// 君寵（君主の寵愛）とその不安定さの純ロジック。功績・追従・個人的親密さで寵を獲得し、寵臣は
    /// 君主の権力を借りて権勢を振るう（虎の威を借る）。だが寵は君主の気まぐれで揺らぎ、根拠（実質的功績）
    /// が薄いほど不安定になる。人格の低い寵臣は権勢を笠に着て増長し、周囲はすり寄りつつ（追従）
    /// 内心で反感を募らせ、不安定さ・増長・反感が重なると寵愛喪失（失脚）が起きる。乱数なし・決定論。
    /// 倍率/度合いは基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ImperialFavorRules
    {
        /// <summary>
        /// 寵の獲得（0..1）＝功績×追従×個人的親密さの加重和。功績の実力だけでなく、おべっかと
        /// 個人的な親しさが君主の寵を引き寄せる。重みは Params（既定 功績0.5/追従0.3/親密0.2）。
        /// </summary>
        public static float FavorGain(float service, float flattery, float personalRapport, ImperialFavorParams p)
        {
            float s = Mathf.Clamp01(service);
            float f = Mathf.Clamp01(flattery);
            float r = Mathf.Clamp01(personalRapport);
            return Mathf.Clamp01(s * p.serviceWeight + f * p.flatteryWeight + r * p.rapportWeight);
        }

        public static float FavorGain(float service, float flattery, float personalRapport)
            => FavorGain(service, flattery, personalRapport, ImperialFavorParams.Default);

        /// <summary>
        /// 寵臣の権勢（0..1）＝寵×君主の権力×虎の威の倍率。寵そのものより、君主がどれだけ実権を
        /// 握っているかが権勢を決める＝弱い君主の寵臣は威を借りきれない。
        /// </summary>
        public static float FavoriteInfluence(float favor, float sovereignPower, ImperialFavorParams p)
        {
            float fav = Mathf.Clamp01(favor);
            float pow = Mathf.Clamp01(sovereignPower);
            return Mathf.Clamp01(fav * pow * p.influenceLeverage);
        }

        public static float FavoriteInfluence(float favor, float sovereignPower)
            => FavoriteInfluence(favor, sovereignPower, ImperialFavorParams.Default);

        /// <summary>
        /// 寵の不安定さ（0..1）＝君主の気まぐれ÷寵の実質的根拠（functonal floor で発散防止）。
        /// 君主が気まぐれなほど、また寵の根拠（実績）が薄いほど、寵はいつ覆るか分からない。
        /// </summary>
        public static float FavorVolatility(float sovereignCapriciousness, float favorBasis, ImperialFavorParams p)
        {
            float cap = Mathf.Clamp01(sovereignCapriciousness);
            float basis = Mathf.Max(p.basisFloor, Mathf.Clamp01(favorBasis));
            return Mathf.Clamp01(cap / basis * p.volatilityScale);
        }

        public static float FavorVolatility(float sovereignCapriciousness, float favorBasis)
            => FavorVolatility(sovereignCapriciousness, favorBasis, ImperialFavorParams.Default);

        /// <summary>
        /// 増長（0..1）＝寵臣の権勢×(1-人格)。人格が低い寵臣ほど権勢を笠に着て驕り高ぶる。
        /// 人格が満点なら権勢があっても増長しない（character=1で0）。
        /// </summary>
        public static float Arrogance(float favoriteInfluence, float character, ImperialFavorParams p)
        {
            float inf = Mathf.Clamp01(favoriteInfluence);
            float c = Mathf.Clamp01(character);
            return Mathf.Clamp01(inf * (1f - c) * p.arroganceScale);
        }

        public static float Arrogance(float favoriteInfluence, float character)
            => Arrogance(favoriteInfluence, character, ImperialFavorParams.Default);

        /// <summary>
        /// 周囲の追従（0..1）＝権勢に応じて人々がすり寄る度合い。権勢ある寵臣の周りには
        /// おもねる者が集まる（権勢が大きいほど追従も大きい）。
        /// </summary>
        public static float Sycophancy(float favoriteInfluence, ImperialFavorParams p)
        {
            float inf = Mathf.Clamp01(favoriteInfluence);
            return Mathf.Clamp01(inf * p.sycophancyScale);
        }

        public static float Sycophancy(float favoriteInfluence)
            => Sycophancy(favoriteInfluence, ImperialFavorParams.Default);

        /// <summary>
        /// 周囲の反感（0..1）＝増長×追従。表向きすり寄る者ほど（追従が高いほど）内心の反感は
        /// 増長を糧に募る＝鼻持ちならない寵臣への嫉妬と憎悪。
        /// </summary>
        public static float PeerResentment(float arrogance, float sycophancy, ImperialFavorParams p)
        {
            float a = Mathf.Clamp01(arrogance);
            float s = Mathf.Clamp01(sycophancy);
            return Mathf.Clamp01(a * s * p.resentmentScale);
        }

        public static float PeerResentment(float arrogance, float sycophancy)
            => PeerResentment(arrogance, sycophancy, ImperialFavorParams.Default);

        /// <summary>
        /// 寵愛喪失（失脚）の度合い（0..1）＝不安定さ×増長×反感。寵が揺らぎ（不安定）、本人が驕り
        /// （増長）、周囲が憎む（反感）――この三つが揃うとき寵臣は一気に失脚する。
        /// </summary>
        public static float FavorLoss(float favorVolatility, float arrogance, float peerResentment, ImperialFavorParams p)
        {
            float v = Mathf.Clamp01(favorVolatility);
            float a = Mathf.Clamp01(arrogance);
            float r = Mathf.Clamp01(peerResentment);
            return Mathf.Clamp01(v * a * r * p.lossScale);
        }

        public static float FavorLoss(float favorVolatility, float arrogance, float peerResentment)
            => FavorLoss(favorVolatility, arrogance, peerResentment, ImperialFavorParams.Default);

        /// <summary>
        /// 寵が安泰か＝寵が閾値を上回り、かつ失脚の度合いが (1-閾値) 未満のとき true。寵が厚くても
        /// 失脚の度合いが高ければ安泰ではない（明日には覆りうる）。
        /// </summary>
        public static bool IsFavorSecure(float favor, float favorLoss, float threshold)
        {
            float t = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(favor) > t && Mathf.Clamp01(favorLoss) < 1f - t;
        }

        public static bool IsFavorSecure(float favor, float favorLoss)
            => IsFavorSecure(favor, favorLoss, ImperialFavorParams.Default.secureThreshold);
    }
}
