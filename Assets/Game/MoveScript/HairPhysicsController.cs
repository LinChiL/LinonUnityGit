using UnityEngine;

public class HairPhysicsController : MonoBehaviour
{
    [Header("物理引用")]
    public Transform characterTransform;
    public CharacterController characterController;
    public Transform hairRoot;

    [Header("物理参数")]
    public bool enableHairPhysics = true;
    [Range(0.1f, 2.0f)]
    public float hairDragMultiplier = 1.0f;
    [Range(0.1f, 3.0f)]
    public float maxHairDrag = 2f;

    [Header("SpringJoint 控制")]
    public bool useSpringJoints = true;
    [Range(0.1f, 5.0f)]
    public float springMultiplier = 1.0f;
    [Range(10f, 500f)]
    public float springStrength = 100f;
    [Range(0.1f, 10f)]
    public float springDamping = 5f;
    [Range(50f, 500f)]
    public float rootSpringStrength = 200f;

    [Header("旋转约束")]
    public bool freezeAllRotation = true;
    [Range(0f, 180f)]
    public float maxRotationAngle = 30f;

    [Header("碰撞体设置")]
    public bool enableColliders = true;
    public float colliderRadius = 0.02f;
    public float colliderHeight = 0.1f;

    [Header("调试")]
    public bool showDebugInfo = false;
    public bool logPhysicsDetails = false;

    private Rigidbody[] hairRigidbodies;
    private SpringJoint[] hairSpringJoints;
    private Collider[] hairColliders;
    private Vector3[] initialLocalPositions;
    private Quaternion[] initialLocalRotations;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float currentSpeed;

    void Start()
    {
        InitializeHairPhysics();
    }

    void Update()
    {
        if (enableHairPhysics)
        {
            UpdateHairPhysics();
        }
    }

    [ContextMenu("初始化头发物理")]
    public void InitializeHairPhysics()
    {
        if (characterTransform == null)
            characterTransform = transform;

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

        if (hairRoot == null)
            FindHairRoot();

        if (hairRoot == null)
        {
            Debug.LogError("未找到头发根骨骼！");
            return;
        }

        // 获取现有的物理组件
        hairRigidbodies = hairRoot.GetComponentsInChildren<Rigidbody>();
        hairSpringJoints = hairRoot.GetComponentsInChildren<SpringJoint>();
        hairColliders = hairRoot.GetComponentsInChildren<Collider>();

        // 保存初始位置和旋转
        SaveInitialTransforms();

        // 确保根骨骼有正确的约束
        EnsureRootBoneConstraint();

        // 确保有碰撞体
        if (enableColliders && hairColliders.Length == 0)
        {
            AddCollidersToHair();
        }

        // 初始化物理组件
        InitializeRigidbodies();
        InitializeSpringJoints();

        // 启用物理
        UpdateHairPhysics(true);

        lastPosition = characterTransform.position;
        lastRotation = characterTransform.rotation;

        Debug.Log($"头发物理初始化完成: {hairRigidbodies.Length} 个刚体, {hairSpringJoints.Length} 个SpringJoint, {hairColliders.Length} 个碰撞体");

        if (logPhysicsDetails)
        {
            LogPhysicsDetails();
        }
    }

    void SaveInitialTransforms()
    {
        initialLocalPositions = new Vector3[hairRigidbodies.Length];
        initialLocalRotations = new Quaternion[hairRigidbodies.Length];

        for (int i = 0; i < hairRigidbodies.Length; i++)
        {
            if (hairRigidbodies[i] != null)
            {
                initialLocalPositions[i] = hairRigidbodies[i].transform.localPosition;
                initialLocalRotations[i] = hairRigidbodies[i].transform.localRotation;
            }
        }
    }

    void EnsureRootBoneConstraint()
    {
        Rigidbody rootRb = hairRoot.GetComponent<Rigidbody>();
        if (rootRb == null) return;

        SpringJoint rootJoint = hairRoot.GetComponent<SpringJoint>();
        if (rootJoint == null || rootJoint.connectedBody == null)
        {
            Debug.LogWarning("根骨骼缺少连接到头部的约束！请点击'修复根骨骼约束'");
        }
    }

    [ContextMenu("修复根骨骼约束")]
    public void FixRootBoneConstraint()
    {
        if (hairRoot == null) return;

        Rigidbody rootRb = hairRoot.GetComponent<Rigidbody>();
        if (rootRb == null)
        {
            Debug.LogError("根骨骼没有Rigidbody！");
            return;
        }

        Transform headBone = hairRoot.parent;
        if (headBone == null)
        {
            Debug.LogError("找不到头部骨骼！");
            return;
        }

        // 确保头部骨骼有Kinematic Rigidbody
        Rigidbody headRb = headBone.GetComponent<Rigidbody>();
        if (headRb == null)
        {
            headRb = headBone.gameObject.AddComponent<Rigidbody>();
            headRb.isKinematic = true;
            headRb.useGravity = false;
            // 头部刚体也要冻结旋转
            headRb.constraints = RigidbodyConstraints.FreezeAll;
            Debug.Log($"为头部骨骼 {headBone.name} 添加了Kinematic Rigidbody");
        }

        // 添加或修复根骨骼的SpringJoint
        SpringJoint rootJoint = hairRoot.GetComponent<SpringJoint>();
        if (rootJoint == null)
        {
            rootJoint = hairRoot.gameObject.AddComponent<SpringJoint>();
            Debug.Log($"为根骨骼 {hairRoot.name} 添加了SpringJoint");
        }

        rootJoint.connectedBody = headRb;
        rootJoint.spring = rootSpringStrength;
        rootJoint.damper = 10f;
        rootJoint.minDistance = 0f;
        rootJoint.maxDistance = 0.05f;
        rootJoint.tolerance = 0.02f;
        rootJoint.enableCollision = true;

        Debug.Log($"根骨骼 {hairRoot.name} 已连接到头部骨骼 {headBone.name}");

        RescanHairBones();
    }

    void InitializeRigidbodies()
    {
        foreach (Rigidbody rb in hairRigidbodies)
        {
            if (rb != null)
            {
                rb.mass = 0.1f;
                rb.drag = 0.2f;
                rb.angularDrag = 2f; // 增加角阻力来限制旋转
                rb.useGravity = false;
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

                // 关键：严格限制旋转
                if (freezeAllRotation)
                {
                    rb.constraints = RigidbodyConstraints.FreezeRotation;
                }
                else
                {
                    // 或者只冻结X和Z轴旋转，允许Y轴轻微旋转
                    rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                }
            }
        }
    }

    void InitializeSpringJoints()
    {
        if (hairSpringJoints.Length == 0) return;

        foreach (SpringJoint joint in hairSpringJoints)
        {
            if (joint != null)
            {
                // 根骨骼使用更强的弹簧
                if (joint.transform == hairRoot)
                {
                    joint.spring = rootSpringStrength;
                    joint.damper = 10f;
                    joint.maxDistance = 0.05f;
                }
                else
                {
                    joint.spring = springStrength;
                    joint.damper = springDamping;
                    joint.maxDistance = 0.15f; // 减少最大距离
                }

                joint.minDistance = 0f;
                joint.tolerance = 0.05f;
                joint.enableCollision = true;

                // 确保关节连接到正确的刚体
                if (joint.connectedBody == null && joint.transform.parent != null)
                {
                    Rigidbody parentRb = joint.transform.parent.GetComponent<Rigidbody>();
                    if (parentRb != null)
                    {
                        joint.connectedBody = parentRb;
                    }
                }
            }
        }
    }

    void AddCollidersToHair()
    {
        foreach (Rigidbody rb in hairRigidbodies)
        {
            if (rb != null && rb.GetComponent<Collider>() == null)
            {
                CapsuleCollider collider = rb.gameObject.AddComponent<CapsuleCollider>();
                collider.radius = colliderRadius;
                collider.height = colliderHeight;
                collider.direction = 1; // Y轴
                collider.center = Vector3.zero;
            }
        }
        hairColliders = hairRoot.GetComponentsInChildren<Collider>();
    }

    void UpdateHairPhysics()
    {
        CalculateMovementSpeed();
        AdjustHairByMovement();
        ApplyRotationConstraints(); // 应用旋转约束
        lastPosition = characterTransform.position;
        lastRotation = characterTransform.rotation;
    }

    void CalculateMovementSpeed()
    {
        Vector3 positionDelta = characterTransform.position - lastPosition;
        float positionSpeed = positionDelta.magnitude / Time.deltaTime;

        float rotationDelta = Quaternion.Angle(characterTransform.rotation, lastRotation);
        float rotationSpeed = rotationDelta / Time.deltaTime;

        currentSpeed = positionSpeed + (rotationSpeed * 0.01f);
    }

    void AdjustHairByMovement()
    {
        float normalizedSpeed = Mathf.Clamp01(currentSpeed / 5f);

        // 调整刚体参数
        foreach (Rigidbody rb in hairRigidbodies)
        {
            if (rb == null) continue;

            if (rb.isKinematic)
            {
                rb.isKinematic = false;
            }

            float targetDrag = Mathf.Lerp(0.1f, maxHairDrag, normalizedSpeed) * hairDragMultiplier;
            rb.drag = Mathf.Lerp(rb.drag, targetDrag, Time.deltaTime * 3f);

            // 根据速度调整角阻力
            float targetAngularDrag = Mathf.Lerp(2f, 5f, normalizedSpeed);
            rb.angularDrag = Mathf.Lerp(rb.angularDrag, targetAngularDrag, Time.deltaTime * 3f);
        }

        // 调整SpringJoint参数
        if (useSpringJoints && hairSpringJoints.Length > 0)
        {
            foreach (SpringJoint joint in hairSpringJoints)
            {
                if (joint != null && joint.connectedBody != null)
                {
                    float speedFactor = 1f + normalizedSpeed * 0.5f;
                    float dampingFactor = 1f + normalizedSpeed * 0.3f;

                    float baseSpring = (joint.transform == hairRoot) ? rootSpringStrength : springStrength;

                    joint.spring = baseSpring * springMultiplier * speedFactor;
                    joint.damper = springDamping * dampingFactor;

                    float baseMaxDistance = (joint.transform == hairRoot) ? 0.05f : 0.15f;
                    joint.maxDistance = Mathf.Lerp(baseMaxDistance, baseMaxDistance * 0.7f, normalizedSpeed);
                }
            }
        }

        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"头发物理 - 速度: {currentSpeed:F2}, 标准化: {normalizedSpeed:F2}");
        }
    }

    void ApplyRotationConstraints()
    {
        // 限制骨骼旋转角度
        for (int i = 0; i < hairRigidbodies.Length; i++)
        {
            if (hairRigidbodies[i] != null && initialLocalRotations[i] != null)
            {
                Transform bone = hairRigidbodies[i].transform;

                // 计算当前旋转与初始旋转的差异
                float angle = Quaternion.Angle(initialLocalRotations[i], bone.localRotation);

                // 如果旋转角度过大，强制限制
                if (angle > maxRotationAngle)
                {
                    bone.localRotation = Quaternion.RotateTowards(bone.localRotation, initialLocalRotations[i], angle - maxRotationAngle);

                    // 同时重置角速度
                    hairRigidbodies[i].angularVelocity = Vector3.zero;

                    if (showDebugInfo && Time.frameCount % 120 == 0)
                    {
                        Debug.LogWarning($"限制骨骼旋转: {bone.name}, 角度: {angle:F1}");
                    }
                }
            }
        }
    }

    [ContextMenu("启用头发物理")]
    public void EnableHairPhysics()
    {
        enableHairPhysics = true;
        UpdateHairPhysics(true);
        Debug.Log("头发物理已启用");
    }

    [ContextMenu("禁用头发物理")]
    public void DisableHairPhysics()
    {
        enableHairPhysics = false;
        UpdateHairPhysics(false);
        Debug.Log("头发物理已禁用");
    }

    void UpdateHairPhysics(bool enabled)
    {
        if (hairRigidbodies == null) return;

        foreach (Rigidbody rb in hairRigidbodies)
        {
            if (rb != null)
            {
                rb.isKinematic = !enabled;
                rb.detectCollisions = enabled;

                if (enabled)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        foreach (Collider collider in hairColliders)
        {
            if (collider != null)
            {
                collider.enabled = enabled;
            }
        }
    }

    void LogPhysicsDetails()
    {
        Debug.Log("=== 头发物理详细信息 ===");
        for (int i = 0; i < Mathf.Min(hairRigidbodies.Length, 3); i++)
        {
            Rigidbody rb = hairRigidbodies[i];
            if (rb != null)
            {
                Debug.Log($"刚体 {i}: {rb.name}, 约束: {rb.constraints}, 角阻力: {rb.angularDrag}");
            }
        }
        Debug.Log("========================");
    }

    void FindHairRoot()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        string[] possibleNames = {
            "hair_root", "hairroot", "hair_base", "hairbase",
            "hair", " hairs", "head_hair", "hair_01", "hair01",
            "ponytail", "tail", "hair_rig", "hair_system"
        };

        foreach (Transform child in allChildren)
        {
            string childNameLower = child.name.ToLower();
            foreach (string name in possibleNames)
            {
                if (childNameLower.Contains(name) && child.childCount > 0)
                {
                    hairRoot = child;
                    Debug.Log($"找到头发根骨骼: {child.name}");
                    return;
                }
            }
        }
    }

    [ContextMenu("重新扫描头发骨骼")]
    public void RescanHairBones()
    {
        if (hairRoot != null)
        {
            hairRigidbodies = hairRoot.GetComponentsInChildren<Rigidbody>();
            hairSpringJoints = hairRoot.GetComponentsInChildren<SpringJoint>();
            hairColliders = hairRoot.GetComponentsInChildren<Collider>();
            SaveInitialTransforms();
            InitializeRigidbodies();
            InitializeSpringJoints();
            Debug.Log($"重新扫描完成: {hairRigidbodies.Length} 个刚体, {hairSpringJoints.Length} 个SpringJoint");
        }
    }

    [ContextMenu("重置物理参数")]
    public void ResetPhysicsParameters()
    {
        if (hairRigidbodies != null)
        {
            foreach (Rigidbody rb in hairRigidbodies)
            {
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = false;
                }
            }
        }
        Debug.Log("物理参数已重置");
    }

    [ContextMenu("强制唤醒刚体")]
    public void WakeUpRigidbodies()
    {
        if (hairRigidbodies != null)
        {
            foreach (Rigidbody rb in hairRigidbodies)
            {
                if (rb != null)
                {
                    rb.WakeUp();
                    rb.isKinematic = false;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
        Debug.Log("刚体已强制唤醒");
    }

    [ContextMenu("修复所有旋转约束")]
    public void FixAllRotationConstraints()
    {
        if (hairRigidbodies != null)
        {
            foreach (Rigidbody rb in hairRigidbodies)
            {
                if (rb != null)
                {
                    // 强制冻结所有旋转
                    rb.constraints = RigidbodyConstraints.FreezeRotation;
                    rb.angularVelocity = Vector3.zero;

                    // 重置到初始旋转
                    rb.transform.localRotation = Quaternion.identity;
                }
            }
        }
        Debug.Log("所有旋转约束已修复");
    }
}