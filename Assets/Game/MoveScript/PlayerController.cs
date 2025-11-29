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
    public float rotateSpeed = 180f;
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
    public float cameraRotateSpeed = 180f;
    public float cameraSmoothSpeed = 10f;
    public LayerMask obstacleMask;

    [Header("碰撞忽略设置")]
    [Tooltip("选择希望角色胶囊体忽略碰撞的层（例如：装饰物、特效、穿墙区域等）")]
    public LayerMask ignoreCollisionLayers; // 新增：可忽略的层

    // 私有变量
    private Vector3 velocity;
    private bool isGrounded;
    private Vector3 currentMoveDirection;
    private Vector3 cameraTargetPosition;
    private Animator animator;

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = Camera.main;

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

        // 【新增】初始化忽略指定层的碰撞
        IgnoreCollisionsWithLayers();
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
        animator.SetFloat("WalkSpeed", speeding);

        x = Mathf.Abs(x) < inputDeadZone ? 0f : x;
        z = Mathf.Abs(z) < inputDeadZone ? 0f : z;

        Vector3 targetMoveDirection = new Vector3(x, 0f, z).normalized;

        if (targetMoveDirection.magnitude > 0.1f)
        {
            currentMoveDirection = Vector3.Lerp(
                currentMoveDirection,
                targetMoveDirection,
                rotateSpeed * 0.01f * Time.deltaTime
            );

            Quaternion targetRotation = Quaternion.LookRotation(targetMoveDirection);
            float maxRotationStep = rotateSpeed * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                maxRotationStep
            );
        }
        else
        {
            currentMoveDirection = Vector3.Lerp(
                currentMoveDirection,
                Vector3.zero,
                rotateSpeed * 0.02f * Time.deltaTime
            );
        }

        float currentSpeed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) && z > 0)
        {
            currentSpeed = sprintSpeed;
        }

        Vector3 move = currentMoveDirection;
        controller.Move(move * currentSpeed * Time.deltaTime);

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
        float standingHeight = controller.height;
        Vector3 characterCenter = transform.position + controller.center;

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
        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position,
            cameraTargetPosition,
            cameraSmoothSpeed * Time.deltaTime
        );

        SetCameraLookAt();
    }

    void SetCameraLookAt()
    {
        Vector3 lookTarget = transform.position + controller.center;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - playerCamera.transform.position);

        Vector3 euler = targetRotation.eulerAngles;
        euler.x = cameraAngle;
        targetRotation = Quaternion.Euler(euler);

        float maxCameraRotationStep = cameraRotateSpeed * Time.deltaTime;
        playerCamera.transform.rotation = Quaternion.RotateTowards(
            playerCamera.transform.rotation,
            targetRotation,
            maxCameraRotationStep
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

    // 【新增】忽略与指定 Layer 的所有碰撞体的碰撞
    public void IgnoreCollisionsWithLayers()
    {
        if (ignoreCollisionLayers == 0 || controller == null) return;

        Collider[] allColliders = FindObjectsOfType<Collider>();

        foreach (Collider col in allColliders)
        {
            if (col == null) continue;

            // 检查该碰撞体所属层是否在 ignoreCollisionLayers 中
            if ((ignoreCollisionLayers.value & (1 << col.gameObject.layer)) != 0)
            {
                Physics.IgnoreCollision(controller.GetComponent<Collider>(), col, true);
            }
        }

        Debug.Log($"[PlayerController] 已忽略与 LayerMask {ignoreCollisionLayers} 的碰撞");
    }

    // 可选：提供恢复碰撞的方法（用于临时效果结束时）
    public void RestoreCollisionsWithLayers()
    {
        if (ignoreCollisionLayers == 0 || controller == null) return;

        Collider[] allColliders = FindObjectsOfType<Collider>();

        foreach (Collider col in allColliders)
        {
            if (col == null) continue;

            if ((ignoreCollisionLayers.value & (1 << col.gameObject.layer)) != 0)
            {
                Physics.IgnoreCollision(controller.GetComponent<Collider>(), col, false);
            }
        }

        Debug.Log($"[PlayerController] 已恢复与 LayerMask {ignoreCollisionLayers} 的碰撞");
    }

    void OnDrawGizmosSelected()
    {
        if (controller != null)
        {
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