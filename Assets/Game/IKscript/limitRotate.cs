using UnityEngine;

/// <summary>
/// 限制物体在X/Y/Z轴的旋转范围，支持完全锁死到固定角度，兼容物理旋转
/// </summary>
[DisallowMultipleComponent]
public class RotationLimiter : MonoBehaviour
{
    [Header("基础设置")]
    [Tooltip("限制基于世界空间还是局部空间（Local）")]
    public Space rotationSpace = Space.Self;

    [Tooltip("是否在编辑器模式下实时生效（方便调试）")]
    public bool enableInEditor = true;

    [Header("X轴设置")]
    [Tooltip("X轴控制模式：范围限制 / 完全锁死")]
    public RotationControlMode xControlMode = RotationControlMode.RangeLimit;
    [Tooltip("X轴最小旋转角度（度）- 仅范围限制模式生效")]
    [MinMaxSlider(-180f, 180f, "IsXRangeMode")]
    public float minX = -45f;
    [Tooltip("X轴最大旋转角度（度）- 仅范围限制模式生效")]
    [MinMaxSlider(-180f, 180f, "IsXRangeMode")]
    public float maxX = 45f;
    [Tooltip("X轴锁死角度（度）- 仅完全锁死模式生效")]
    [ShowIf("IsXLockMode")]
    public float lockX = 0f;

    [Header("Y轴设置")]
    [Tooltip("Y轴控制模式：范围限制 / 完全锁死")]
    public RotationControlMode yControlMode = RotationControlMode.RangeLimit;
    [Tooltip("Y轴最小旋转角度（度）- 仅范围限制模式生效")]
    [MinMaxSlider(-180f, 180f, "IsYRangeMode")]
    public float minY = -90f;
    [Tooltip("Y轴最大旋转角度（度）- 仅范围限制模式生效")]
    [MinMaxSlider(-180f, 180f, "IsYRangeMode")]
    public float maxY = 90f;
    [Tooltip("Y轴锁死角度（度）- 仅完全锁死模式生效")]
    [ShowIf("IsYLockMode")]
    public float lockY = 0f;

    [Header("Z轴设置")]
    [Tooltip("Z轴控制模式：范围限制 / 完全锁死")]
    public RotationControlMode zControlMode = RotationControlMode.RangeLimit;
    [Tooltip("Z轴最小旋转角度（度）- 仅范围限制模式生效")]
    [MinMaxSlider(-180f, 180f, "IsZRangeMode")]
    public float minZ = -30f;
    [Tooltip("Z轴最大旋转角度（度）- 仅范围限制模式生效")]
    [MinMaxSlider(-180f, 180f, "IsZRangeMode")]
    public float maxZ = 30f;
    [Tooltip("Z轴锁死角度（度）- 仅完全锁死模式生效")]
    [ShowIf("IsZLockMode")]
    public float lockZ = 0f;

    // 旋转控制模式枚举
    public enum RotationControlMode
    {
        RangeLimit,  // 范围限制
        Locked       // 完全锁死
    }

    private Rigidbody _rigidbody; // 物理组件引用

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // 初始化时强制修正旋转到目标状态（锁死/限制范围）
        ClampRotation();
    }

    // 每帧更新后修正旋转（处理直接修改transform的情况）
    private void LateUpdate()
    {
        if (!Application.isPlaying && !enableInEditor) return;
        ClampRotation();
    }

    // 物理更新后修正旋转（处理Rigidbody旋转的情况）
    private void FixedUpdate()
    {
        if (!Application.isPlaying || _rigidbody == null) return;
        ClampRotation();
    }

    /// <summary>
    /// 核心方法：将旋转修正到设定状态（锁死/限制范围）
    /// </summary>
    private void ClampRotation()
    {
        Quaternion targetRotation = rotationSpace == Space.World
            ? transform.rotation
            : transform.localRotation;

        // 将四元数转换为标准化欧拉角（-180~180）
        Vector3 euler = NormalizeEulerAngles(targetRotation.eulerAngles);

        // 处理X轴：锁死或范围限制
        switch (xControlMode)
        {
            case RotationControlMode.Locked:
                euler.x = NormalizeAngle(lockX); // 强制设为锁死角度
                break;
            case RotationControlMode.RangeLimit:
                euler.x = ClampAngle(euler.x, minX, maxX); // 范围限制
                break;
        }

        // 处理Y轴：锁死或范围限制
        switch (yControlMode)
        {
            case RotationControlMode.Locked:
                euler.y = NormalizeAngle(lockY);
                break;
            case RotationControlMode.RangeLimit:
                euler.y = ClampAngle(euler.y, minY, maxY);
                break;
        }

        // 处理Z轴：锁死或范围限制
        switch (zControlMode)
        {
            case RotationControlMode.Locked:
                euler.z = NormalizeAngle(lockZ);
                break;
            case RotationControlMode.RangeLimit:
                euler.z = ClampAngle(euler.z, minZ, maxZ);
                break;
        }

        // 应用修正后的旋转
        Quaternion clampedRotation = Quaternion.Euler(euler);
        if (rotationSpace == Space.World)
        {
            ApplyWorldRotation(clampedRotation);
        }
        else
        {
            ApplyLocalRotation(clampedRotation);
        }
    }

    /// <summary>
    /// 应用世界空间旋转（兼容物理和普通旋转）
    /// </summary>
    private void ApplyWorldRotation(Quaternion targetRotation)
    {
        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            _rigidbody.MoveRotation(targetRotation);
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }

    /// <summary>
    /// 应用局部空间旋转（兼容物理和普通旋转）
    /// </summary>
    private void ApplyLocalRotation(Quaternion targetLocalRotation)
    {
        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            Quaternion worldRotation = transform.parent != null
                ? transform.parent.rotation * targetLocalRotation
                : targetLocalRotation;
            _rigidbody.MoveRotation(worldRotation);
        }
        else
        {
            transform.localRotation = targetLocalRotation;
        }
    }

    /// <summary>
    /// 标准化欧拉角到-180~180范围
    /// </summary>
    private Vector3 NormalizeEulerAngles(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z)
        );
    }

    /// <summary>
    /// 标准化单个角度到-180~180范围
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle > 180f ? angle - 360f : angle < -180f ? angle + 360f : angle;
    }

    /// <summary>
    /// 限制角度在指定范围内（支持跨0度）
    /// </summary>
    private float ClampAngle(float angle, float min, float max)
    {
        if (min > max) (min, max) = (max, min);

        // 处理跨0度的宽范围限制（如170~-170）
        if (max - min < 180f)
        {
            return Mathf.Clamp(angle, min, max);
        }
        else
        {
            if (angle > min || angle < max) return angle;
            return angle > (min + max) / 2f ? max : min;
        }
    }

    #region 编辑器辅助方法（用于属性显示控制）
    public bool IsXRangeMode() => xControlMode == RotationControlMode.RangeLimit;
    public bool IsXLockMode() => xControlMode == RotationControlMode.Locked;
    public bool IsYRangeMode() => yControlMode == RotationControlMode.RangeLimit;
    public bool IsYLockMode() => yControlMode == RotationControlMode.Locked;
    public bool IsZRangeMode() => zControlMode == RotationControlMode.RangeLimit;
    public bool IsZLockMode() => zControlMode == RotationControlMode.Locked;
    #endregion

    #region 编辑器属性绘制扩展（实现条件显示）
    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
    private class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionMethodName { get; }
        public ShowIfAttribute(string conditionMethodName)
        {
            ConditionMethodName = conditionMethodName;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
    private class MinMaxSliderAttribute : PropertyAttribute
    {
        public float Min { get; }
        public float Max { get; }
        public string ConditionMethodName { get; }
        public MinMaxSliderAttribute(float min, float max, string conditionMethodName)
        {
            Min = min;
            Max = max;
            ConditionMethodName = conditionMethodName;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ShowIfAttribute))]
    private class ShowIfDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            ShowIfAttribute attr = (ShowIfAttribute)attribute;
            bool show = GetConditionResult(property, attr.ConditionMethodName);

            if (show)
            {
                UnityEditor.EditorGUI.PropertyField(position, property, label);
            }
        }

        public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
        {
            ShowIfAttribute attr = (ShowIfAttribute)attribute;
            bool show = GetConditionResult(property, attr.ConditionMethodName);
            return show ? UnityEditor.EditorGUI.GetPropertyHeight(property) : 0f;
        }

        private bool GetConditionResult(UnityEditor.SerializedProperty property, string methodName)
        {
            var target = property.serializedObject.targetObject;
            var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            return method != null && (bool)method.Invoke(target, null);
        }
    }

    [UnityEditor.CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    private class MinMaxSliderDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            MinMaxSliderAttribute attr = (MinMaxSliderAttribute)attribute;
            var target = property.serializedObject.targetObject;
            var method = target.GetType().GetMethod(attr.ConditionMethodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            bool enable = method != null && (bool)method.Invoke(target, null);

            using (new UnityEditor.EditorGUI.DisabledScope(!enable))
            {
                UnityEditor.EditorGUI.Slider(position, property, attr.Min, attr.Max, label);
            }
        }
    }
#endif
    #endregion
}