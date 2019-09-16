// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "jzEngine/CopyScreen" 
{
    Properties 
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _BaseTex ("Base (RGB)", 2D) = "white" {}
    }

    SubShader 
    {
        CGINCLUDE
        
        #include "UnityCG.cginc"
        
        sampler2D _MainTex;
        half4 _MainTex_TexelSize;

        sampler2D _BaseTex;
        half4 _BaseTex_TexelSize;
        
        struct v2f 
        {
            float4 pos : SV_POSITION; 
            half2 uv : TEXCOORD0;
        };  
        
        v2f vs(appdata_img v) 
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);  
            o.uv = v.texcoord;
                            
            return o; 
        }
        
        fixed4 ps(v2f i) : SV_Target 
        {
            return tex2D(_BaseTex, i.uv.xy);
        } 
        
        ENDCG
        
        ZTest Always Cull Off ZWrite Off
        
        Pass {  
            CGPROGRAM  
            #pragma vertex vs  
            #pragma fragment ps  
            
            ENDCG  
        }
    }

    FallBack Off
}
