// 声明全局变量（与 C# 脚本中传入的变量名一致）
StructuredBuffer<float4> _Repellers;
int _RepellerCount;

void CalculateGrassOffset_float(
    float3 worldPos,
    float3 axisWeights, // 各轴权重 (x, y, z)
    float repelRadius, // 影响距离
    float3 fixxyz, // 修正值
    out float3 offset
)
{
    offset = float3(0, 0, 0);
    const float strength = 0.8;

    for (int i = 0; i < _RepellerCount && i < 16; i++)
    {
        float3 repPos = _Repellers[i].xyz - fixxyz.xyz; // 修正高度差
        
        // 计算三维方向向量
        float3 dir = worldPos - repPos;
        float dist = length(dir);
        
        if (dist > 0.01 && dist < repelRadius)
        {
            float influence = (repelRadius - dist) / repelRadius;
            float3 dirNorm = dir / dist;
            float3 currentOffset = dirNorm * influence * strength;
            
            // 应用各轴权重
            currentOffset.x *= axisWeights.x;
            currentOffset.y *= axisWeights.y;
            currentOffset.z *= axisWeights.z;
            
            offset += currentOffset;
        }
    }
    
    // 限制总偏移量
    offset = clamp(offset, -1.0, 1.0);
}

