sampler2D texDepth : register(s0);
sampler2D texLabel : register(s1);
sampler2D texDepthColor : register(s2);
sampler2D texLabelColor : register(s3);

float4 main(float2 uv : TEXCOORD) : COLOR 
{ 
	float depth = tex2D(texDepth, uv).r;
	float label = tex2D(texLabel, uv).r;
	float4 depthIntensity = tex2D(texDepthColor, float2(depth, 0));
	
	if(label > 0)
		return tex2D(texLabelColor, float2(label - 0.001953, 0)) * depthIntensity;
	
	return depthIntensity;
}