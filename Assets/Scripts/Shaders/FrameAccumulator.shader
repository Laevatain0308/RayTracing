Shader "Ray Tracing/FrameAccumulator"
{
    Properties
    {
        _MainTex ("MainTex" , 2D) = "white" {}
    }
    
    SubShader
    {
        Pass
        {
            CGPROGRAM

            #pragma vertex vert;
            #pragma fragment frag;

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _PreFrameTex;
            int _Frame;
            
            
            struct appdate
            {
                float4 positionOS   : POSITION;
                float2 texcoord     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;                
            };

            
            v2f vert(appdate input)
            {
                v2f output = (v2f)0;

                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.texcoord;

                return output;
            }
            
            float4 frag(v2f input) : SV_Target
            {
                float4 preColor = tex2D(_PreFrameTex , input.uv);
                float4 currentColor = tex2D(_MainTex , input.uv);

                // 将结果按已累计的总帧数成比例取平均
                float weight = 1.0 / (_Frame + 1);
                float4 average = preColor * (1.0 - weight) + currentColor * weight;
                
                return average;
            }
            
            ENDCG
        }
    }
}
