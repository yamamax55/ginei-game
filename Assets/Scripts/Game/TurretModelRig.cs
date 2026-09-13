using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 浮遊砲台の3Dモデル（<c>Resources/Models/Fortress/FloatingBattery</c>）を砲台に載せ、
    /// <b>見た目だけ</b>実対象へ向ける（ChatGPT 制作・2026-09-10 受領）。
    ///
    /// <b>射界は動かさない</b>のが要点。射界の基準は砲台本体の <c>transform.up</c>（要塞が配置時に決めた外向き）で、
    /// この装置が回すのは<b>子のモデル</b>だけ。モデルの向きを射界の基準にすると、追尾のたびに扇が付いて回り
    /// 射界が際限なく広がる＝砲台を潰しても死角ができなくなる（<see cref="TurretAimRules"/> の注記）。
    ///
    /// モデルの階層：<c>FloatingBattery &gt; TurretYaw &gt; GunPitch &gt; BarrelRecoil &gt; MuzzleLeft/MuzzleRight</c>。
    /// 付属のデモアニメ（<c>FloatingBattery_AimFireDemo.fbx</c>）は<b>取り込まない</b>し、
    /// モデルに <see cref="Animator"/> が付いていても止める＝固定の旋回デモが実際の狙いを上書きしない。
    /// </summary>
    public class TurretModelRig : MonoBehaviour
    {
        /// <summary>Resources のモデルパス（ゲーム用のニュートラル姿勢のほう）。</summary>
        public const string ResourcePath = "Models/Fortress/FloatingBattery";

        [Tooltip("モデル全体の直径（ワールド単位）。砲台の当たり半径に合わせる")]
        public float worldDiameter = 1.6f;

        private Transform model;        // 生成したモデルのルート
        private Transform yaw;          // 旋回（Z軸）
        private Transform recoil;       // 砲身の後退
        private Transform muzzle;       // 砲口（ビームの起点）
        private Vector3 recoilHome;     // 反動の定位置（ローカル）
        private Vector3 recoilAxis = Vector3.down;   // 後退の向き（ローカル）

        private float currentAngle;     // いまモデルが向いている角度（度・ワールド基準）
        private float desiredAngle;     // 向きたい角度
        private float lastFireTime = -999f;
        private bool silenced;

        private TurretAimParams aimParams = TurretAimParams.Default;

        /// <summary>モデルを載せられたか（未納品／読み込み失敗なら false＝従来のスプライト表示のまま）。</summary>
        public bool HasModel => model != null;

        /// <summary>ビームの起点（砲口）。モデルが無ければ砲台本体の位置。</summary>
        public Vector3 MuzzleWorldPosition => muzzle != null ? muzzle.position : transform.position;

        /// <summary>いまモデルが目標を向き終えているか（撃ってよいか）。モデルが無ければ常に true。</summary>
        public bool Aligned => model == null || TurretAimRules.Aligned(currentAngle, desiredAngle, aimParams);

        /// <summary>
        /// モデルを載せる。<paramref name="tint"/> は陣営色（発光部を陣営で染めずモデルの材質を活かす場合は無視）。
        /// 読み込めなければ false を返し、呼び手は従来のスプライト表示を続ける。
        /// </summary>
        public bool Attach(float diameter, Color tint)
        {
            if (model != null) return true;

            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) return false;   // 未納品＝従来表示（警告も出さない＝通常運用）

            worldDiameter = Mathf.Max(0.1f, diameter);
            GameObject go = Instantiate(prefab, transform, false);
            go.name = "FloatingBatteryModel";
            model = go.transform;
            model.localPosition = Vector3.zero;
            model.localScale = Vector3.one;

            // ★デモアニメを再生させない（実際の狙いを固定の旋回で上書きしないため）。
            var animators = go.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++) { animators[i].enabled = false; }
            var legacy = go.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < legacy.Length; i++) { legacy[i].enabled = false; }

            // モデルに付いてくるライト/カメラは 2D Renderer では邪魔なので止める（要塞と同じ扱い）。
            var lights = go.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = false;
            var cams = go.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++) cams[i].enabled = false;

            yaw = FindDeep(model, "TurretYaw") ?? model;
            recoil = FindDeep(model, "BarrelRecoil");
            muzzle = FindDeep(model, "MuzzleLeft") ?? FindDeep(model, "MuzzleRight") ?? FindDeep(model, "AimForward");
            if (recoil != null) recoilHome = recoil.localPosition;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = 3;
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
                FortressMaterialFactory.Apply(renderers[i], tint);
            }

            FitToWorldDiameter(renderers);
            return true;
        }

        /// <summary>調整値を差し替える（砲台側の Inspector 値を反映したいとき）。</summary>
        public void SetParams(in TurretAimParams p) => aimParams = p;

        /// <summary>
        /// この方向へ向く（実対象の方向）。<b>射界の判定には使わない</b>＝見た目だけ。
        /// 標的が消えた／射程外になったら <see cref="ClearAim"/> を呼び、旋回も射撃も止める。
        /// </summary>
        public void AimAt(Vector2 worldDirection)
        {
            if (worldDirection.sqrMagnitude < 1e-8f) return;
            desiredAngle = TurretAimRules.AngleOf(worldDirection);
        }

        /// <summary>狙いを解く（標的なし）。いまの向きで止まる＝勝手に初期位置へ跳ね戻らない。</summary>
        public void ClearAim() => desiredAngle = currentAngle;

        /// <summary>発砲した（反動を始める）。</summary>
        public void OnFired() => lastFireTime = Time.time;

        /// <summary>沈黙（破壊）＝旋回・発光を止め、暗い残骸にする。</summary>
        public void Silence()
        {
            silenced = true;
            if (model == null) return;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                FortressMaterialFactory.ApplyWreck(renderers[i]);
        }

        private void Update()
        {
            if (model == null || silenced) return;

            // game-time で回す＝ポーズで止まり、倍速で速くなる（規約）。
            currentAngle = TurretAimRules.StepAngle(currentAngle, desiredAngle, Time.deltaTime, aimParams);
            if (yaw != null)
            {
                // 砲台本体（固定基準）に対する相対角へ直してから当てる＝本体の向きは絶対に変えない。
                float baseAngle = TurretAimRules.AngleOf(transform.up);
                yaw.localRotation = Quaternion.Euler(0f, 0f, TurretAimRules.SignedDelta(baseAngle, currentAngle));
            }

            if (recoil != null)
            {
                float back = TurretAimRules.RecoilOffset(Time.time - lastFireTime, aimParams);
                recoil.localPosition = recoilHome + recoilAxis * back;
            }
        }

        /// <summary>モデルの実寸を測って指定直径へ合わせる（FBX のインポート倍率に依らない）。</summary>
        private void FitToWorldDiameter(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0 || model == null) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float size = Mathf.Max(b.size.x, b.size.y);
            if (size <= 0.0001f) return;
            model.localScale = Vector3.one * (worldDiameter / size);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
