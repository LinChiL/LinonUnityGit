using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class CameraSkyboxSwitcher : MonoBehaviour
{
    [Header("天空盒设置")]
    public Material customSkybox;  // 拖入另一个颜色的天空盒材质

    [Header("环境光设置")]
    public Color ambientColor = Color.gray;  // 设置你想要的环境光颜色
    public float ambientIntensity = 1.0f;

    // 保存原始设置
    private Material originalSkybox;
    private Color originalAmbientColor;
    private float originalAmbientIntensity;

    void OnEnable()
    {
        // 保存当前的全局设置
        originalSkybox = RenderSettings.skybox;
        originalAmbientColor = RenderSettings.ambientLight;
        originalAmbientIntensity = RenderSettings.ambientIntensity;

        // 应用自定义设置
        ApplyCustomEnvironment();
    }

    void OnDisable()
    {
        // 恢复原始设置
        RestoreOriginalEnvironment();
    }

    void ApplyCustomEnvironment()
    {
        if (customSkybox != null)
        {
            RenderSettings.skybox = customSkybox;
        }

        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = ambientIntensity;

        // 重要：更新全局光照
        DynamicGI.UpdateEnvironment();
    }

    void RestoreOriginalEnvironment()
    {
        RenderSettings.skybox = originalSkybox;
        RenderSettings.ambientLight = originalAmbientColor;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        DynamicGI.UpdateEnvironment();
    }

    // 可选：在Inspector中预览
#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying && enabled && customSkybox != null)
        {
            ApplyCustomEnvironment();
        }
    }
#endif
}