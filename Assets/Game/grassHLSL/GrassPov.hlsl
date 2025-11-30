// ==============================================
// 草地排斥体变形核心 Shader 代码（逆向LOD优化版）
// 关键：以相机为中心，只计算LOD范围内的草叶变形，超距直接跳过
// ==============================================

// 1. 全局变量声明（新增LOD和相机位置，与C#脚本对应）
StructuredBuffer<float4> _Repellers; // 排斥体数据：xyz=世界位置，w=最终半径（Collider+extraRadius）
int _RepellerCount; // 活跃排斥体数量
float _LodMaxDistance; // LOD最大距离（相机为中心）
float _LodFadeDistance; // LOD过渡距离
float3 _GrassRepellerCameraPos; // 相机世界位置（逆向LOD核心）

// 2. LOD强度计算（逆向逻辑：草叶到相机的距离决定是否计算变形）
float CalculateLodStrength(float3 worldPos)
{
    // 计算草叶到相机的直线距离（逆向核心）
    float distanceToCamera = length(worldPos - _GrassRepellerCameraPos);
    
    // 超距：直接返回0，不执行任何变形计算
    if (distanceToCamera >= _LodMaxDistance)
        return 0.0f;
    
    // 过渡区间：强度从1渐变到0，避免视觉突变
    float fadeStart = _LodMaxDistance - _LodFadeDistance;
    if (distanceToCamera > fadeStart)
    {
        return 1.0f - (distanceToCamera - fadeStart) / _LodFadeDistance;
    }
    
    // 近距离：强度1，执行完整变形
    return 1.0f;
}

// 3. 草地偏移计算核心函数（带LOD优化）
void CalculateGrassOffset_float(
    float3 worldPos, // 草叶顶点的世界坐标
    float3 axisWeights, // 各轴变形权重 (x, y, z)
    float3 fixxyz, // 高度差修正值
    out float3 offset // 输出最终的草叶偏移量
)
{
    offset = float3(0.0, 0.0, 0.0);
    
    // 第一步：计算LOD强度，超距直接返回（性能关键）
    float lodStrength = CalculateLodStrength(worldPos);
    if (lodStrength <= 0.01f)
        return;

    const float baseStrength = 0.8;
    int maxRepellers = min(_RepellerCount, 16); // 限制最大遍历数，避免过度计算

    // 遍历排斥体（仅对LOD范围内的草叶执行）
    for (int i = 0; i < maxRepellers; i++)
    {
        float3 repellerWorldPos = _Repellers[i].xyz;
        float repelRadius = _Repellers[i].w;

        if (repelRadius <= 0.1f)
            continue;

        float3 correctedRepelPos = repellerWorldPos - fixxyz.xyz;
        float3 dirToRepeller = worldPos - correctedRepelPos;
        float distance = length(dirToRepeller);

        if (distance > 0.01f && distance < repelRadius)
        {
            // 应用LOD强度：过渡区间内降低变形强度
            float influence = (repelRadius - distance) / repelRadius;
            influence = pow(influence, 2.0f) * lodStrength; // 叠加LOD强度

            float3 normalizedDir = normalize(dirToRepeller);
            float adaptiveStrength = baseStrength / repelRadius;
            adaptiveStrength = clamp(adaptiveStrength, 0.2f, 1.5f);
            float3 currentOffset = normalizedDir * influence * adaptiveStrength;

            // 应用轴权重
            currentOffset.x *= axisWeights.x;
            currentOffset.y *= axisWeights.y;
            currentOffset.z *= axisWeights.z;

            offset += currentOffset;
        }
    }

    offset = clamp(offset, -1.0f, 1.0f);
}

// ==============================================
// 函数调用示例（顶点着色器中使用）
// ==============================================
void VertexShaderMain(
    inout float4 vertexPos : POSITION, // 草叶顶点原始位置
    in float3 worldPos : TEXCOORD0, // 草叶顶点世界坐标（需提前计算）
    out float3 grassOffset : TEXCOORD1 // 输出草叶偏移量
)
{
    // 可暴露为Shader属性，方便在Inspector调节
    float3 axisWeights = float3(1.0f, 0.1f, 1.0f); // x/z轴全变形，y轴弱变形
    float3 fixxyz = float3(0.0f, 0.5f, 0.0f); // 高度修正（根据地形调整）

    // 调用排斥体偏移计算（自动跳过超距草叶）
    CalculateGrassOffset_float(worldPos, axisWeights, fixxyz, grassOffset);

    // 应用偏移到顶点位置
    float3 deformedWorldPos = worldPos + grassOffset;
    vertexPos = mul(UNITY_MATRIX_VP, float4(deformedWorldPos, 1.0f));
}