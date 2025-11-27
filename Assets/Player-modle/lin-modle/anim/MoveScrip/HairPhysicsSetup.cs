using UnityEngine;

public class HairPhysicsSetup : MonoBehaviour
{
    [Header("物理设置")]
    public float boneMass = 0.1f;
    public float drag = 0.2f;
    public float angularDrag = 0.1f;
    public float springStrength = 100f;
    public float springDamping = 5f;

    [Header("碰撞体设置")]
    public float colliderRadius = 0.02f;
    public float colliderHeight = 0.1f;

    void Start()
    {
        SetupHairPhysics();
    }

    public void SetupHairPhysics()
    {
        // 清除可能存在的旧组件
        ClearOldPhysicsComponents();

        // 为所有名字包含"Hair"的骨骼添加物理
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        foreach (Transform child in allChildren)
        {
            if (child.name.Contains("Hair"))
            {
                AddPhysicsToBone(child);
            }
        }

        // 设置骨骼关节
        SetupBoneJoints();
    }

    void ClearOldPhysicsComponents()
    {
        Rigidbody[] oldRbs = GetComponentsInChildren<Rigidbody>();
        Collider[] oldColliders = GetComponentsInChildren<Collider>();
        Joint[] oldJoints = GetComponentsInChildren<Joint>();

        foreach (var rb in oldRbs) DestroyImmediate(rb);
        foreach (var collider in oldColliders) DestroyImmediate(collider);
        foreach (var joint in oldJoints) DestroyImmediate(joint);
    }

    void AddPhysicsToBone(Transform bone)
    {
        // 添加刚体
        Rigidbody rb = bone.gameObject.AddComponent<Rigidbody>();
        rb.mass = boneMass;
        rb.drag = drag;
        rb.angularDrag = angularDrag;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        // 添加胶囊碰撞体
        CapsuleCollider collider = bone.gameObject.AddComponent<CapsuleCollider>();
        collider.radius = colliderRadius;
        collider.height = colliderHeight;
        collider.direction = 1; // Y轴方向
        collider.center = new Vector3(0, colliderHeight * 0.5f, 0);
    }

    void SetupBoneJoints()
    {
        Transform[] hairBones = GetComponentsInChildren<Transform>();

        foreach (Transform bone in hairBones)
        {
            if (bone.name.Contains("Hair") && bone.parent != null &&
                bone.parent.name.Contains("Hair"))
            {
                AddConfigurableJoint(bone, bone.parent);
            }
        }
    }

    void AddConfigurableJoint(Transform bone, Transform parentBone)
    {
        ConfigurableJoint joint = bone.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = parentBone.GetComponent<Rigidbody>();

        // 配置线性运动
        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;

        // 设置线性限制
        SoftJointLimit linearLimit = new SoftJointLimit();
        linearLimit.limit = 0.05f;
        joint.linearLimit = linearLimit;

        // 设置弹簧
        SoftJointLimitSpring spring = new SoftJointLimitSpring();
        spring.spring = springStrength;
        spring.damper = springDamping;
        joint.linearLimitSpring = spring;

        // 锁定旋转
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;

        // 配置投影，防止骨骼过度拉伸
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        joint.projectionDistance = 0.1f;
    }
}