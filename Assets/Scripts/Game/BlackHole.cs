using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 特殊地形：ブラックホール（A-5）。
    /// - pullRadius 内のすべての艦艇（旗艦＋配下艦）に引力をかける。
    /// - coreRadius に入った艦艇は TakeDamage(大ダメージ) で戦闘除外する。
    /// - 引力は LateUpdate（FleetMovement.Update 後）にトランスフォームへ直接加算する外力。
    ///   FleetMovement の移動ロジックには一切触れない。
    /// - 艦の列挙は FleetRegistry.AllTargets（陣営非依存の全艦在庫）で行う（FindObjectsByType 不使用）。
    /// - 実行時生成のスプライト・マテリアルはアプリ寿命で共有し、インスタンス固有分は OnDestroy で破棄。
    /// - Battle シーン開始時に [RuntimeInitializeOnLoadMethod] が 1 体だけ自動配置する（シーン手置き不要）。
    /// </summary>
    public class BlackHole : MonoBehaviour
    {
        // ────────────────────────────────────────────────
        // 公開チューナブル
        // ────────────────────────────────────────────────

        [Header("物理設定")]
        [Tooltip("引力が働く最大半径（ワールド単位）")]
        public float pullRadius = 12f;

        [Tooltip("艦艇が戦闘除外される消滅コアの半径（ワールド単位）")]
        public float coreRadius = 2.0f;

        [Tooltip("引力の強さ（単位/秒、距離 0 でのピーク値）")]
        public float pullStrength = 10f;

        [Tooltip("コア近傍(coreRadius×この倍率)では最低でも pullStrength で確実に吸い込む（縁での停滞防止）")]
        public float coreCaptureScale = 2.0f;

        [Header("配置")]
        [Tooltip("出現時、艦隊の初期位置(引力圏)に重ならないよう安全な場所へ自動でずらす")]
        public bool avoidFleetsOnSpawn = true;

        [Tooltip("艦隊から確保する追加クリアランス（pullRadius に加算した距離だけ旗艦から離す）")]
        public float spawnClearance = 4f;

        [Header("ビジュアル設定")]
        [Tooltip("コア（暗黒円）の表示半径スケール（ワールド単位）")]
        public float coreVisualRadius = 1.5f;

        [Tooltip("降着円盤（引力圏可視化リング）の表示半径スケール")]
        public float accretionVisualRadius = 12f;

        [Tooltip("コアの色")]
        public Color coreColor = new Color(0.03f, 0.01f, 0.08f, 1f);

        [Tooltip("降着円盤の色")]
        public Color accretionColor = new Color(0.6f, 0.2f, 1.0f, 0.18f);

        [Tooltip("コアの回転速度（度/秒）")]
        public float rotationSpeed = 20f;

        [Tooltip("降着円盤のパルス速度")]
        public float pulseSpeed = 1.2f;

        [Tooltip("降着円盤のパルス強度（0=なし）")]
        public float pulseAmount = 0.06f;

        [Header("吸引エフェクト")]
        [Tooltip("渦を巻いて中心へ吸い込まれる吸引パーティクルを表示するか")]
        public bool showPullEffect = true;

        [Tooltip("吸引パーティクルの色")]
        public Color pullEffectColor = new Color(0.7f, 0.45f, 1f, 0.9f);

        [Tooltip("吸引パーティクルの毎秒放出数")]
        public float pullEffectRate = 90f;

        [Tooltip("吸引パーティクルの寿命（秒）。中心へ到達するまでの時間の目安")]
        public float pullEffectLifetime = 2.5f;

        [Tooltip("吸引パーティクルが中心へ向かう速さ（単位/秒）")]
        public float pullEffectInwardSpeed = 5f;

        [Tooltip("吸引パーティクルの渦巻きの強さ（velocityOverLifetime.orbitalZ。大きいほど強く回る）")]
        public float pullEffectSwirl = 2.0f;

        [Header("消滅演出")]
        [Tooltip("コア吸収時の爆発パーティクルを生成するか")]
        public bool spawnAbsorbEffect = true;

        // ────────────────────────────────────────────────
        // 自動配置用スタティック制御
        // ────────────────────────────────────────────────

        /// <summary>false にすると RuntimeInitializeOnLoadMethod による自動配置を無効化できる。</summary>
        public static bool AutoSpawnEnabled = true;

        // ────────────────────────────────────────────────
        // 静的レジストリ（シーン内の全 BlackHole。AI の回避判定が参照する）
        // ────────────────────────────────────────────────

        private static readonly List<BlackHole> active = new List<BlackHole>();

        /// <summary>シーン内に存在する生存中の BlackHole 一覧（FleetAI の回避に使用）。</summary>
        public static IReadOnlyList<BlackHole> All => active;

        // ────────────────────────────────────────────────
        // 定数
        // ────────────────────────────────────────────────

        // ソーティングオーダー（背景 -100 より手前、艦 ~0 より後ろ）
        private const int CoreSortingOrder = -50;
        private const int AccretionSortingOrder = -51;
        private const int PullFxSortingOrder = -50; // 降着円盤より手前・艦より後ろ

        // コア吸収ダメージ（大ダメージで旗艦＝BeginRetreat・配下艦＝Destroy を発動させる）
        private const int CoreKillDamage = int.MaxValue / 2;

        // ────────────────────────────────────────────────
        // 共有スタティックスプライト／マテリアル（アプリ寿命で1個のみ生成）
        // ────────────────────────────────────────────────

        // コアとリングの共有スプライト（ラジアルグラデーション）
        private static Sprite sharedCoreSprite;
        private static Sprite sharedRingSprite;
        // 消滅演出用パーティクルマテリアル
        private static Material sharedAbsorbMaterial;
        // 吸引エフェクト用パーティクルマテリアル
        private static Material sharedPullMaterial;

        // ────────────────────────────────────────────────
        // インスタンスごとのマテリアル（OnDestroy で破棄する）
        // ────────────────────────────────────────────────

        private Material coreMaterial;
        private Material accretionMaterial;

        // ────────────────────────────────────────────────
        // ランタイム参照
        // ────────────────────────────────────────────────

        private Transform coreTransform;
        private Transform ringTransform;
        private ParticleSystem pullEffect;

        // 初回 LateUpdate での安全位置への退避を一度だけ行うためのフラグ
        private bool spawnPositioned = false;

        // コアに吸収済みの艦（同フレームの二重処理防止）
        private readonly HashSet<IShipTarget> absorbedThisSession = new HashSet<IShipTarget>();

        // ────────────────────────────────────────────────
        // Unity ライフサイクル
        // ────────────────────────────────────────────────

        private void Awake()
        {
            CreateVisuals();
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this); // AI 回避用レジストリへ登録
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        private void LateUpdate()
        {
            // 初回のみ：旗艦が Start でレジストリ登録された後（＝この最初の LateUpdate 時点）に、
            // 引力圏が艦隊の初期位置に重ならない安全な場所へ退避する。描画前なのでちらつかない。
            if (!spawnPositioned)
            {
                spawnPositioned = true;
                if (avoidFleetsOnSpawn) PositionClearOfFleets();
            }

            ApplyGravity();
        }

        private void OnDestroy()
        {
            // インスタンス固有のマテリアルのみ破棄（共有スプライト・共有マテリアルはアプリ寿命で維持）
            if (coreMaterial != null) Destroy(coreMaterial);
            if (accretionMaterial != null) Destroy(accretionMaterial);
        }

        // ────────────────────────────────────────────────
        // ビジュアル生成
        // ────────────────────────────────────────────────

        /// <summary>コアディスクと降着円盤をランタイムで生成する。</summary>
        private void CreateVisuals()
        {
            // ── 降着円盤（引力圏リング。コアより後ろに描画）──
            GameObject ringObj = new GameObject("AccretionRing");
            ringObj.transform.SetParent(transform);
            ringObj.transform.localPosition = Vector3.zero;
            ringObj.transform.localScale = Vector3.one * (accretionVisualRadius * 2f);

            SpriteRenderer ringSR = ringObj.AddComponent<SpriteRenderer>();
            ringSR.sprite = GetRingSprite();
            ringSR.sortingOrder = AccretionSortingOrder;

            // インスタンス固有マテリアル（色をインスタンス値で制御するため）
            accretionMaterial = new Material(Shader.Find("Sprites/Default"));
            accretionMaterial.color = accretionColor;
            ringSR.material = accretionMaterial;

            ringTransform = ringObj.transform;

            // ── コアディスク（暗黒本体）──
            GameObject coreObj = new GameObject("BlackHoleCore");
            coreObj.transform.SetParent(transform);
            coreObj.transform.localPosition = Vector3.zero;
            coreObj.transform.localScale = Vector3.one * (coreVisualRadius * 2f);

            SpriteRenderer coreSR = coreObj.AddComponent<SpriteRenderer>();
            coreSR.sprite = GetCoreSprite();
            coreSR.sortingOrder = CoreSortingOrder;

            coreMaterial = new Material(Shader.Find("Sprites/Default"));
            coreMaterial.color = coreColor;
            coreSR.material = coreMaterial;

            coreTransform = coreObj.transform;

            // ── 吸引エフェクト（渦を巻いて中心へ流れ込むパーティクル）──
            if (showPullEffect) BuildPullEffect();
        }

        /// <summary>
        /// 渦を巻きながら中心へ吸い込まれる吸引パーティクルを生成する。
        /// pullRadius の円盤全体から放出し、radial で中心へ・orbitalZ で渦を巻かせる。
        /// timeScale 追従（ポーズで停止・倍速で加速）。
        /// </summary>
        private void BuildPullEffect()
        {
            GameObject fx = new GameObject("PullEffect");
            fx.transform.SetParent(transform);
            fx.transform.localPosition = Vector3.zero;
            fx.transform.localRotation = Quaternion.identity;

            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.duration = 3f;
            main.startLifetime = pullEffectLifetime;
            main.startSpeed = 0f; // 速度は velocityOverLifetime で与える
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = pullEffectColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // radial/orbital を中心基準に
            main.maxParticles = 1000;
            main.useUnscaledTime = false; // timeScale 追従

            var emission = ps.emission;
            emission.rateOverTime = pullEffectRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = pullRadius;
            shape.radiusThickness = 1f; // 円盤全体から放出
            shape.arc = 360f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(-pullEffectInwardSpeed); // 中心へ
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(pullEffectSwirl);       // 渦巻き

            var colOL = ps.colorOverLifetime;
            colOL.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(pullEffectColor, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                });
            colOL.color = grad;

            var sizeOL = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

            // 共有マテリアル（アプリ寿命で1個のみ生成、インスタンスでは破棄しない）
            if (sharedPullMaterial == null)
                sharedPullMaterial = new Material(Shader.Find("Sprites/Default"));
            var psr = fx.GetComponent<ParticleSystemRenderer>();
            psr.material = sharedPullMaterial;
            psr.sortingOrder = PullFxSortingOrder;

            ps.Play();
            pullEffect = ps;
        }

        // ────────────────────────────────────────────────
        // 引力処理（LateUpdate: FleetMovement.Update 後に外力として適用）
        // ────────────────────────────────────────────────

        /// <summary>
        /// pullRadius 内のすべての生存艦艇を中心へ引き寄せる。
        /// coreRadius 以内に入った艦は TakeDamage で戦闘除外する。
        /// </summary>
        private void ApplyGravity()
        {
            Vector3 center = transform.position;
            float dt = Time.deltaTime; // timeScale に追従（ポーズ=0、倍速対応）

            // コアの回転アニメーション（scaled time）
            if (coreTransform != null)
            {
                coreTransform.Rotate(0f, 0f, rotationSpeed * dt);
            }
            // 降着円盤のパルスアニメーション（逆回転）
            if (ringTransform != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                ringTransform.localScale = Vector3.one * (accretionVisualRadius * 2f * pulse);
                ringTransform.Rotate(0f, 0f, -rotationSpeed * 0.4f * dt);
            }

            if (dt <= 0f) return; // ポーズ中は引力計算をスキップ

            // ── フィールド上のすべての IShipTarget を列挙（FleetRegistry 経由） ──
            // ブラックホールは陣営を問わず全艦を引き寄せる。多勢力対応の単一在庫から全艦を処理する。
            ProcessFactionTargets(FleetRegistry.AllTargets, center, dt);
        }

        // スナップショット用バッファ（フレームごとに再利用してアロケーションを抑える）
        private readonly List<IShipTarget> _snapshot = new List<IShipTarget>();

        /// <summary>指定した IShipTarget リストに対して引力・コア消滅を処理する。</summary>
        private void ProcessFactionTargets(IReadOnlyList<IShipTarget> targets, Vector3 center, float dt)
        {
            // FleetRegistry の注意書き通り「列挙中に Unregister しないこと」を守るため、
            // TakeDamage 呼び出し前にリストのスナップショットを取って安全に列挙する。
            _snapshot.Clear();
            for (int k = 0; k < targets.Count; k++) _snapshot.Add(targets[k]);

            int count = _snapshot.Count;
            for (int i = 0; i < count; i++)
            {
                IShipTarget t = _snapshot[i];

                // 破棄済み・退却済みは対象外
                if (!ShipCombat.IsValidTarget(t)) continue;

                Vector3 pos = t.Transform.position;
                float dist = Vector2.Distance(pos, center); // XY 平面上の距離

                if (dist > pullRadius) continue; // 影響圏外はスキップ

                // ── コア消滅判定 ──
                if (dist <= coreRadius)
                {
                    if (!absorbedThisSession.Contains(t))
                    {
                        absorbedThisSession.Add(t);
                        if (spawnAbsorbEffect) SpawnAbsorbEffect(pos);
                        AbsorbTarget(t);
                    }
                    continue;
                }

                // ── 引力変位（外縁=0、コア縁=pullStrength の線形補間） ──
                float span = pullRadius - coreRadius;
                if (span < 0.0001f) continue; // coreRadius >= pullRadius の不正設定を防ぐ

                float ratio = Mathf.Clamp01(1f - (dist - coreRadius) / span);
                float force = pullStrength * ratio * dt;

                // コア近傍では最低でも pullStrength で確実に吸い込む（縁で停滞して残らないように）
                if (dist <= coreRadius * coreCaptureScale)
                    force = Mathf.Max(force, pullStrength * dt);

                // 中心を通り越して反対側へ飛ばないようにのみクランプ（コア内への進入は許可）。
                // コア内に入れば次フレームの消滅判定(dist <= coreRadius)で戦闘除外される。
                float maxDisplace = Mathf.Max(0f, dist - 0.01f);
                force = Mathf.Min(force, maxDisplace);
                if (force <= 0f) continue;

                Vector3 dir = (center - pos).normalized;
                t.Transform.position += dir * force;
            }
        }

        // ────────────────────────────────────────────────
        // 出現時の安全配置（艦隊の初期位置に重ならないよう退避）
        // ────────────────────────────────────────────────

        // 既定位置が艦隊と重なるときに試す代替候補（戦場周辺。原点対称で偏りを抑える）
        private static readonly Vector2[] SpawnCandidates =
        {
            new Vector2(8f, 8f), new Vector2(-8f, 8f), new Vector2(8f, -8f), new Vector2(-8f, -8f),
            new Vector2(0f, 16f), new Vector2(0f, -16f), new Vector2(16f, 0f), new Vector2(-16f, 0f),
            new Vector2(12f, 12f), new Vector2(-12f, 12f), new Vector2(12f, -12f), new Vector2(-12f, -12f),
            new Vector2(0f, 22f), new Vector2(0f, -22f), new Vector2(22f, 0f), new Vector2(-22f, 0f),
        };

        /// <summary>
        /// 現在位置が旗艦の引力圏(pullRadius＋spawnClearance)に重なっていれば、
        /// 候補位置の中から全旗艦に対して十分離れた場所へ移動する。
        /// 完全に安全な候補が無ければ、最も旗艦から遠い候補を選ぶ。
        /// </summary>
        private void PositionClearOfFleets()
        {
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            if (flagships == null || flagships.Count == 0) return; // 艦隊が無ければ既定位置のまま

            float clearance = pullRadius + spawnClearance;
            Vector2 current = transform.position;

            // 既定位置が既に安全ならそのまま（従来の見た目を尊重）
            if (MinDistanceToFleets(current, flagships) >= clearance) return;

            Vector2 best = current;
            float bestMin = MinDistanceToFleets(current, flagships);

            for (int i = 0; i < SpawnCandidates.Length; i++)
            {
                Vector2 c = SpawnCandidates[i];
                float m = MinDistanceToFleets(c, flagships);
                if (m >= clearance)
                {
                    transform.position = new Vector3(c.x, c.y, transform.position.z);
                    return;
                }
                if (m > bestMin) { bestMin = m; best = c; }
            }

            transform.position = new Vector3(best.x, best.y, transform.position.z);
        }

        /// <summary>指定座標から最も近い生存旗艦までの距離を返す（旗艦が無ければ大きな値）。</summary>
        private static float MinDistanceToFleets(Vector2 pos, IReadOnlyList<FleetStrength> flagships)
        {
            float min = float.MaxValue;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || !fs.IsAlive) continue;
                float d = Vector2.Distance(pos, fs.transform.position);
                if (d < min) min = d;
            }
            return min;
        }

        /// <summary>
        /// コアに到達した艦を消滅させる。
        /// 旗艦は TakeDamage だと「退却（IsAlive=false）」になりブラックホール内に居座るため、
        /// ブラックホールでは部隊（旗艦＋配下艦）ごと即時破棄して確実に消滅させる。
        /// 配下艦は従来どおり撃沈処理（TakeDamage → Die → Destroy）。
        /// </summary>
        private void AbsorbTarget(IShipTarget t)
        {
            if (t is FleetStrength flagship)
            {
                Destroy(flagship.gameObject); // OnDestroy で FleetRegistry から解除（配下艦も各自解除）
            }
            else
            {
                t.TakeDamage(CoreKillDamage);
            }
        }

        // ────────────────────────────────────────────────
        // 消滅演出
        // ────────────────────────────────────────────────

        /// <summary>コアに吸収された艦の位置に短い吸収エフェクトを生成する。</summary>
        private void SpawnAbsorbEffect(Vector3 pos)
        {
            GameObject go = new GameObject("AbsorbEffect");
            go.transform.position = pos;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            main.startSize = 0.2f;
            main.startColor = new Color(0.8f, 0.3f, 1.0f, 1f); // 紫
            main.stopAction = ParticleSystemStopAction.Destroy;  // 終了後に自動破棄

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            // 共有マテリアル（アプリ寿命で1個のみ生成、インスタンスでは破棄しない）
            if (sharedAbsorbMaterial == null)
            {
                sharedAbsorbMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = sharedAbsorbMaterial;
            psr.sortingOrder = 10;

            ps.Play();
        }

        // ────────────────────────────────────────────────
        // スプライト生成（共有・アプリ寿命）
        // ────────────────────────────────────────────────

        /// <summary>
        /// コア用のラジアルグラデーション円スプライトを返す（中心=白、外周=透明）。
        /// 色は SpriteRenderer.color で制御。生成は1回のみで以降は共有。
        /// </summary>
        private static Sprite GetCoreSprite()
        {
            if (sharedCoreSprite != null) return sharedCoreSprite;

            const int size = 64;
            const float center = (size - 1) / 2f;
            const float radius = size / 2f;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float t = Mathf.Clamp01(dist / radius);
                    // 中心→外周にかけてシャープな減衰（ブラックホールの輪郭感）
                    float alpha = Mathf.Clamp01(1f - t * t);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            sharedCoreSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedCoreSprite;
        }

        /// <summary>
        /// 降着円盤用のリングスプライト（外縁が光るドーナツ形）を返す。
        /// 色は SpriteRenderer.color で制御。生成は1回のみで以降は共有。
        /// </summary>
        private static Sprite GetRingSprite()
        {
            if (sharedRingSprite != null) return sharedRingSprite;

            const int size = 128;
            const float center = (size - 1) / 2f;
            const float outerR = size / 2f;
            const float innerR = size * 0.4f; // ドーナツの内半径

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    if (dist > outerR || dist < innerR * 0.6f)
                    {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                        continue;
                    }
                    // リング中央（innerR〜outerR の中間）が最も明るい
                    float midR = (innerR + outerR) * 0.5f;
                    float halfWidth = (outerR - innerR) * 0.5f;
                    float fromMid = Mathf.Abs(dist - midR);
                    float alpha = Mathf.Clamp01(1f - fromMid / halfWidth);
                    alpha = alpha * alpha; // 滑らかなフォールオフ
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            sharedRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedRingSprite;
        }

        // ────────────────────────────────────────────────
        // 自動配置（Battle シーンのみ）
        // ────────────────────────────────────────────────

        /// <summary>
        /// Battle シーン読み込みのたびに、自動的に BlackHole を 1 体だけ配置する。
        /// RuntimeInitializeOnLoadMethod はアプリ起動時に1回しか呼ばれないため、
        /// Title→Battle のような実行時のシーン遷移にも対応できるよう sceneLoaded を購読する。
        /// AutoSpawnEnabled = false にすることで無効化できる。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            // 起動直後に既に Battle なら即配置（Battle シーンを直接再生した場合に対応）
            TrySpawn(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TrySpawn(scene);
        }

        /// <summary>Battle シーンに BlackHole が無ければ1体だけ配置する（重複防止）。</summary>
        private static void TrySpawn(Scene scene)
        {
            if (!AutoSpawnEnabled) return;
            if (scene.name != "Battle") return;
            if (BattleHandoff.IsSystemView) return; // 非戦闘のシステムビューにはブラックホールを湧かせない
            if (Object.FindAnyObjectByType<BlackHole>() != null) return;

            GameObject go = new GameObject("BlackHole");
            // 戦場の中心 (0,0) から離れた位置に配置（最初から艦隊を飲み込まないように）
            go.transform.position = new Vector3(8f, 8f, 0f);
            go.AddComponent<BlackHole>();
        }
    }
}
