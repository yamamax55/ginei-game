// 要塞メッシュ用の自己陰影シェーダー（#要塞モデル）。
//
// なぜ専用シェーダーが要るか：このプロジェクトの URP は **2D Renderer**（Assets/Settings/Renderer2D.asset）で
// 動いている。2D Renderer は 3D ライトの前方/遅延ライティングパスを持たないため、URP/Lit を貼ると
// 陰影が付かず、白黒の細かな模様のまま平坦に見える（実機QAの症状）。
//
// そこで**光源に頼らず自前で陰影を計算する**。固定のキーライト方向に対する N·L に加え、
// 回り込み（wrap）・フィル・リム・スペキュラを足して球の立体感を作る。実ライトを使わないので
// 2D Renderer でもそのまま出るし、将来 Universal(3D) Renderer や Built-in へ切り替えても同じに見える。
//
// LightMode タグを付けない＝SRPDefaultUnlit として扱われ、2D Renderer の不透明描画に確実に拾われる。
// ZWrite On＝球の前後関係が正しく解決し、面同士のちらつき（砂嵐に見える原因のひとつ）も起きない。
Shader "Ginei/FortressSelfLit"
{
    Properties
    {
        _BaseColor      ("Base Color", Color)              = (0.72, 0.75, 0.80, 1)
        _EmissionColor  ("Emission Color", Color)          = (0, 0, 0, 0)
        _Metallic       ("Metallic", Range(0,1))           = 0.5
        _Smoothness     ("Smoothness", Range(0,1))         = 0.5
        _LightDir       ("Key Light Dir (world)", Vector)  = (-0.45, 0.72, -0.53, 0)
        _AmbientColor   ("Ambient", Color)                 = (0.17, 0.20, 0.27, 1)
        _FillColor      ("Fill (opposite side)", Color)    = (0.10, 0.14, 0.22, 1)
        _RimColor       ("Rim", Color)                     = (0.40, 0.60, 0.95, 1)
        _RimPower       ("Rim Power", Range(0.5, 8))       = 3.0
        _RimStrength    ("Rim Strength", Range(0, 2))      = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _EmissionColor;
            half   _Metallic;
            half   _Smoothness;
            float4 _LightDir;
            fixed4 _AmbientColor;
            fixed4 _FillColor;
            fixed4 _RimColor;
            half   _RimPower;
            half   _RimStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float3 worldNormal: TEXCOORD0;
                float3 worldPos   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.worldNormal);
                float3 L = normalize(_LightDir.xyz);

                // 視線方向。正射影（戦略MAP）でもカメラは十分離れているのでこの近似で足りる。
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 主光源：やわらかい回り込み（wrap lighting）で球の丸みを出す。
                float ndl  = dot(N, L);
                float key  = saturate(ndl * 0.5 + 0.5);
                key = key * key;                       // 明部を締めて陰影の階調を強める

                // 反対側からの弱いフィル＝陰が真っ黒に潰れない（宇宙の反射光の見立て）。
                float fill = saturate(-ndl * 0.5 + 0.5) * 0.55;

                // スペキュラ：金属感に応じたハイライト。装甲パネルの起伏を読ませる。
                float3 H    = normalize(L + V);
                float  gloss= lerp(12.0, 110.0, saturate(_Smoothness));
                float  spec = pow(saturate(dot(N, H)), gloss) * saturate(_Metallic) * 0.9;

                // リム：外周をうっすら光らせて背景（濃紺）から切り離す。
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength;

                float3 lit = _BaseColor.rgb * (_AmbientColor.rgb + key.xxx)
                           + _BaseColor.rgb * _FillColor.rgb * fill
                           + spec.xxx
                           + _RimColor.rgb * rim
                           + _EmissionColor.rgb;

                return fixed4(lit, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
