using UnityEngine;

[RequireComponent(typeof(CharacterController))] // 角色使用CharacterController移动
public class ObjectCarrySystem : MonoBehaviour
{
    [Header("射线设置")]
    [Tooltip("射线发射点（空物体，作为拾取后的挂载点）")]
    public Transform rayOrigin; // 拖拽你的空物体到这里
    [Tooltip("射线检测距离")]
    public float rayDistance = 2f;
    [Tooltip("射线检测层级（建议只检测可搬运物体）")]
    public LayerMask carryableLayer; // 在Inspector中设置可搬运物体的层级

    [Header("拾取设置")]
    [Tooltip("拾取后物体相对于挂载点的偏移量")]
    public Vector3 carryOffset = new Vector3(0, 0.5f, 0); // 调整这个值来对齐物体
    [Tooltip("拾取时物体的父级（留空则默认使用rayOrigin作为父级）")]
    public Transform carryParent; // 可在Inspector手动赋值的父物体
    [Tooltip("拾取时是否冻结物体旋转")]
    public bool freezeRotationOnCarry = true;

    private Rigidbody _carriedRigidbody; // 当前携带的物体刚体
    private bool _isCarrying = false; // 是否正在携带物体

    private CharacterStateManager stateManager;

    private Animator animator;


    void Start()
    {
        stateManager = GetComponent<CharacterStateManager>();
        animator = GetComponent<Animator>();

        
    }

    /// <summary>
    /// 核心方法：获取任意3D碰撞体的世界空间尺寸（宽、高、深）
    /// </summary>
    /// <param name="collider">目标碰撞体</param>
    /// <returns>世界空间下的尺寸（x=宽，y=高，z=深）</returns>
    private Vector3 GetColliderWorldSize(Collider collider)
    {
        Transform colTransform = collider.transform;
        Vector3 localSize = Vector3.zero;

        // 1. 判断碰撞体具体类型，提取本地尺寸
        if (collider is BoxCollider boxCollider)
        {
            // 盒碰撞体：直接用 size 属性
            localSize = boxCollider.size;
        }
        else if (collider is SphereCollider sphereCollider)
        {
            // 球碰撞体：宽高=直径（2*半径）
            float diameter = sphereCollider.radius * 2;
            localSize = new Vector3(diameter, diameter, diameter);
        }
        else if (collider is CapsuleCollider capsuleCollider)
        {
            // 胶囊碰撞体：高度=总高度，宽度=2*半径（需结合胶囊方向）
            float radius = capsuleCollider.radius;
            float height = capsuleCollider.height;
            localSize = new Vector3(radius * 2, height, radius * 2);

            // （可选）如果胶囊方向是X或Z轴，需调整宽高映射（根据你的需求）
            // if (capsuleCollider.direction == 0) localSize = new Vector3(height, radius*2, radius*2); // X轴方向
            // if (capsuleCollider.direction == 2) localSize = new Vector3(radius*2, radius*2, height); // Z轴方向
        }
        else if (collider is MeshCollider meshCollider)
        {
            // 网格碰撞体：用包围盒 bounds 获取尺寸（适配复杂形状）
            localSize = meshCollider.bounds.size;
            // 注意：bounds 是世界空间的，无需再乘缩放（下面代码会兼容处理）
            return localSize;
        }
        else
        {
            // 其他碰撞体（如 WheelCollider）：默认用包围盒
            localSize = collider.bounds.size;
            return localSize;
        }

        // 2. 将本地尺寸转换为世界尺寸（乘以物体的缩放）
        Vector3 worldScale = colTransform.lossyScale; // lossyScale = 世界空间总缩放（包含父物体缩放）
        Vector3 worldSize = new Vector3(
            Mathf.Abs(localSize.x * worldScale.x),
            Mathf.Abs(localSize.y * worldScale.y),
            Mathf.Abs(localSize.z * worldScale.z)
        );

        return worldSize;
    }
    void Update()
    {
        // 绘制射线（场景视图调试用）
        Debug.DrawRay(rayOrigin.position, rayOrigin.forward * rayDistance, Color.cyan);

        // F键拾取/放下逻辑
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (_isCarrying)
            {
                DropObject(); // 已携带则放下
            }
            else
            {
                TryPickupObject(); // 未携带则尝试拾取
            }
        }

        // 携带时保持物体位置（防止物理偏移）
        if (_isCarrying && _carriedRigidbody != null)
        {
            UpdateCarriedObjectPosition();
        }
    }

    private Vector2 carryingObjectHeightWidth;

    /// <summary>
    /// 尝试拾取前方物体
    /// </summary>
    private void TryPickupObject()
    {
        // 发射射线检测前方物体
        if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, rayDistance, carryableLayer))
        {
            // 检查物体是否有Rigidbody且允许移动（tag为allowtomove）
            Rigidbody targetRb = hit.collider.GetComponent<Rigidbody>();
            if (targetRb != null && hit.collider.CompareTag("allowtomove"))
            {
                //获取搬起物件的所有碰撞体
                Collider[] allColliders = hit.collider.GetComponents<Collider>();

                // 计算复合碰撞体总高总宽
                foreach (var collider in allColliders)
                {
                    Vector3 worldSize = GetColliderWorldSize(collider);
                    float width = worldSize.x;  // 宽度（X轴方向）
                    float height = worldSize.y; // 高度（Y轴方向）
                    float depth = worldSize.z;  // 深度（Z轴方向，可选）

                    carryingObjectHeightWidth = new Vector2(height, width);

                    Debug.Log($"宽度：{width:F2}，高度：{height:F2}，深度：{depth:F2}");
                }

                // 初始化携带状态
                _carriedRigidbody = targetRb;
                _isCarrying = true;
                stateManager.SetState(CharacterStateManager.CharacterState.Carrying);

                //传给animator
                animator.SetBool("IsCarrying", true);
                Debug.Log("当前角色状态：" + stateManager.currentState);

                // 调整物体物理状态
                _carriedRigidbody.useGravity = false;
                _carriedRigidbody.isKinematic = false; // 保持动力学以便调整位置
                _carriedRigidbody.drag = 10f; // 增加拖拽防止滑动
                _carriedRigidbody.angularDrag = 10f;

                // 冻结旋转（可选）
                if (freezeRotationOnCarry)
                {
                    _carriedRigidbody.freezeRotation = true;
                }

                // 设置父物体：优先使用手动赋值的carryParent，未赋值则使用rayOrigin
                Transform targetParent = carryParent != null ? carryParent : rayOrigin;
                _carriedRigidbody.transform.SetParent(targetParent);

                // 立即对齐到挂载点位置
                UpdateCarriedObjectPosition();
            }
        }
    }

    /// <summary>
    /// 放下携带的物体
    /// </summary>
    private void DropObject()
    {
        if (_carriedRigidbody == null) return;

        // 恢复物体物理状态
        _carriedRigidbody.useGravity = true;
        _carriedRigidbody.drag = 0f;
        _carriedRigidbody.angularDrag = 0.05f;
        _carriedRigidbody.freezeRotation = false;

        // 解除父子关系
        
        _carriedRigidbody.transform.SetParent(null);

        // 清空携带状态
        _carriedRigidbody = null;
        _isCarrying = false;

        stateManager.SetState(CharacterStateManager.CharacterState.Normal);

        //传给animator
        animator.SetBool("IsCarrying", false);
        Debug.Log("当前角色状态：" + stateManager.currentState);

    }

    /// <summary>
    /// 更新携带物体的位置（平滑跟随挂载点）
    /// </summary>
    private void UpdateCarriedObjectPosition()
    {
        // 确定目标父物体（优先使用手动赋值的carryParent）
        Transform targetParent = carryParent != null ? carryParent : rayOrigin;

        // 计算目标位置（父物体位置 + 偏移量，基于父物体局部坐标）
        Vector3 targetPosition = targetParent.TransformPoint(carryOffset);

        // 平滑移动到目标位置（使用Lerp让移动更自然）
        _carriedRigidbody.transform.position = Vector3.Lerp(
            _carriedRigidbody.transform.position,
            targetPosition,
            Time.deltaTime * 15f // 移动速度，可调整
        );

        // 可选：让物体面向父物体的前方
        Quaternion targetRotation = Quaternion.Lerp(
            _carriedRigidbody.transform.rotation,
            targetParent.rotation,
            Time.deltaTime * 10f
        );
        _carriedRigidbody.transform.rotation = targetRotation;
    }
}