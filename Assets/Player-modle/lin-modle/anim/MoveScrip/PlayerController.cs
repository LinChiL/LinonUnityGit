using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("引用组件")]
    public CharacterController controller;
    public Camera playerCamera;

    [Header("移动设置")]
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 8f;
    [Tooltip("旋转速度（度/秒），180 = 每秒转180度，90度转向需0.5秒")]
    public float rotateSpeed = 180f; // 改为度/秒单位，默认180度/秒（直观易调）
    public float inputDeadZone = 0.1f;

    [Header("跳跃设置")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("第三人称相机设置（固定位置）")]
    public float cameraDistance = 5f;
    public float cameraHeight = 3f;
    public float cameraAngle = 30f;
    public Vector3 cameraOffset = new Vector3(0, 0, -1);
    [Tooltip("相机旋转速度（建议与角色rotateSpeed一致）")]
    public float cameraRotateSpeed = 180f; // 相机独立旋转速度（默认与角色同步）
    public float cameraSmoothSpeed = 10f;
    public LayerMask obstacleMask;

    // 私有变量
    private Vector3 velocity;
    private bool isGrounded;
    private Vector3 currentMoveDirection; // 当前移动方向（平滑过渡）

    private Vector3 cameraTargetPosition;

    private Animator animator;

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        // 【已删除】移除硬编码修改胶囊体尺寸和中心的代码
        // 完全保留Inspector面板中设置的CharacterController参数

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerCamera.transform.parent == transform)
        {
            playerCamera.transform.parent = null;
            Debug.Log("第三人称相机已解除父子关系");
        }

        UpdateCameraTargetPosition();
        playerCamera.transform.position = cameraTargetPosition;
        SetCameraLookAt();

        currentMoveDirection = Vector3.zero;

        animator = GetComponent<Animator>();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;

        HandleMovementAndRotation();
        HandleJump();
        ApplyGravity();

        UpdateCameraPosition();
    }

    void HandleMovementAndRotation()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float speeding = new Vector2(x, z).magnitude;
        // 设置给Animator参数
        animator.SetFloat("WalkSpeed", speeding);

        // 应用死区
        x = Mathf.Abs(x) < inputDeadZone ? 0f : x;
        z = Mathf.Abs(z) < inputDeadZone ? 0f : z;

        // 计算目标移动方向（世界空间，归一化确保所有方向速度一致）
        Vector3 targetMoveDirection = new Vector3(x, 0f, z).normalized;

        if (targetMoveDirection.magnitude > 0.1f)
        {
            // 关键优化1：移动方向插值系数改为 rotateSpeed * 0.01f，避免切换过快
            currentMoveDirection = Vector3.Lerp(
                currentMoveDirection,
                targetMoveDirection,
                rotateSpeed * 0.01f * Time.deltaTime // 调整插值系数，适配新rotateSpeed单位
            );

            // 关键优化2：用Quaternion.RotateTowards替代Lerp，保证角速度一致
            Quaternion targetRotation = Quaternion.LookRotation(targetMoveDirection);
            // 每帧最大旋转角度 = 旋转速度（度/秒）* 帧时间（秒）
            float maxRotationStep = rotateSpeed * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                maxRotationStep // 无论角度差多大，每帧转动角度相同
            );
        }
        else
        {
            // 无输入时，缓慢重置移动方向（保持原插值逻辑，系数同步调整）
            currentMoveDirection = Vector3.Lerp(
                currentMoveDirection,
                Vector3.zero,
                rotateSpeed * 0.02f * Time.deltaTime // 0.02f = 0.01f * 2，保持原衰减速度比例
            );
        }

        // 冲刺逻辑（仅向前时生效）
        float currentSpeed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) && z > 0)
        {
            currentSpeed = sprintSpeed;
        }

        // 直接用世界空间移动方向，确保移动与转向一致
        Vector3 move = currentMoveDirection;
        controller.Move(move * currentSpeed * Time.deltaTime);

        // 有移动输入时更新相机目标位置
        if (currentMoveDirection.magnitude > 0.1f)
        {
            UpdateCameraTargetPosition();
        }
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void UpdateCameraTargetPosition()
    {
        // 【关键修改】读取Inspector面板中设置的胶囊体高度和中心，不再硬编码
        float standingHeight = controller.height; // 直接获取面板设置的高度
        Vector3 characterCenter = transform.position + controller.center; // 直接获取面板设置的中心位置（已包含Y轴偏移）

        float horizontalDistance = cameraDistance * Mathf.Cos(cameraAngle * Mathf.Deg2Rad);
        float verticalOffset = cameraHeight;

        Vector3 fixedOffset = new Vector3(
            cameraOffset.x * horizontalDistance,
            verticalOffset,
            cameraOffset.z * horizontalDistance
        );

        cameraTargetPosition = characterCenter + fixedOffset;

        CheckCameraObstacles(characterCenter);
    }

    void UpdateCameraPosition()
    {
        // 相机位置平滑移动
        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position,
            cameraTargetPosition,
            cameraSmoothSpeed * Time.deltaTime
        );

        // 相机旋转同步优化：用RotateTowards保证角速度一致
        SetCameraLookAt();
    }

    void SetCameraLookAt()
    {
        // 【关键修改】相机看向的目标改为面板设置的胶囊体中心
        Vector3 lookTarget = transform.position + controller.center;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - playerCamera.transform.position);

        // 锁定X轴旋转（保持固定俯视角）
        Vector3 euler = targetRotation.eulerAngles;
        euler.x = cameraAngle;
        targetRotation = Quaternion.Euler(euler);

        // 关键优化3：相机旋转改用RotateTowards，与角色旋转速度统一
        float maxCameraRotationStep = cameraRotateSpeed * Time.deltaTime;
        playerCamera.transform.rotation = Quaternion.RotateTowards(
            playerCamera.transform.rotation,
            targetRotation,
            maxCameraRotationStep // 相机每帧旋转角度与角色一致
        );
    }

    void CheckCameraObstacles(Vector3 characterCenter)
    {
        if (Physics.Linecast(characterCenter, cameraTargetPosition, out RaycastHit hit, obstacleMask))
        {
            cameraTargetPosition = hit.point + hit.normal * 0.1f;
        }
    }

    void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        controller.Move(velocity * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        if (controller != null)
        {
            // 【关键修改】Gizmos显示改为面板设置的胶囊体尺寸，不再硬编码
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + controller.center,
                               new Vector3(controller.radius * 2, controller.height, controller.radius * 2));

            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, currentMoveDirection * 2f);

            if (playerCamera != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 lookTarget = transform.position + controller.center;
                Gizmos.DrawLine(playerCamera.transform.position, lookTarget);
            }
        }
    }
}