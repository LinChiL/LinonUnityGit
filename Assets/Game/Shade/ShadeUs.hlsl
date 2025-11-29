// 注意：函数名必须带 _float 后缀！
void StylizedShadow_Outline_float(
    float3 positionOS,
    float3 normalOS,
    float OutlineThickness,
    out float3 modifiedPositionOS)
{
    // 归一化法线
    float3 normalizedNormal = normalize(normalOS);
    
    // 应用轮廓膨胀
    modifiedPositionOS = positionOS + normalizedNormal * OutlineThickness;
}