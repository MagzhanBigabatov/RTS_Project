
using UnityEngine;
using UnityEngine.InputSystem;

public class RTSCameraController : MonoBehaviour
{
    [Header("Movement Input")]
    [SerializeField] private InputActionReference moveAction;

    [Header("Camera Rotation Input")]
    [SerializeField] private InputActionReference rotateAction;
    [SerializeField] private InputActionReference lookDeltaAction;

    [Header("Camera Zoom Input")]
    [SerializeField] private InputActionReference zoomAction;

    [Header("Camera")]
    [SerializeField] private Transform viewCamera;
    [SerializeField] private float moveSpeed = 25f;
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSensitivity = 0.2f;
    [SerializeField] private float minPitch = 25f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minCameraHeight = 4f;
    [SerializeField] private float maxCameraHeight = 25f;

    private Vector3 targetPosition;
    private Vector3 smoothVelocity;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        if (viewCamera == null)
        {
            viewCamera = GetComponent<Camera>() != null
                ? transform
                : Camera.main != null ? Camera.main.transform : transform;
        }

        targetPosition = transform.position;

        Vector3 angles = transform.eulerAngles;

        yaw = angles.y;
        pitch = angles.x;

        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void OnEnable()
    {
        SetActionEnabled(moveAction, true);
        SetActionEnabled(rotateAction, true);
        SetActionEnabled(lookDeltaAction, true);
        SetActionEnabled(zoomAction, true);
    }

    private void OnDisable()
    {
        SetActionEnabled(moveAction, false);
        SetActionEnabled(rotateAction, false);
        SetActionEnabled(lookDeltaAction, false);
        SetActionEnabled(zoomAction, false);
    }

    private void SetActionEnabled(
        InputActionReference actionReference,
        bool enabled)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return;
        }

        if (enabled)
        {
            actionReference.action.Enable();
        }
        else
        {
            actionReference.action.Disable();
        }
    }

    private void Update()
    {
        HandleRotation();
        HandleMovement();
        HandleZoom();
    }

    private void HandleMovement()
    {
        if (moveAction == null || moveAction.action == null)
        {
            return;
        }

        Vector2 input = moveAction.action.ReadValue<Vector2>();

        Vector3 direction = new Vector3(input.x, 0f, input.y);

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        Vector3 worldDirection =
            Quaternion.Euler(0f, yaw, 0f) * direction;

        targetPosition += worldDirection * moveSpeed * Time.deltaTime;
    }

    private void HandleRotation()
    {
        if (rotateAction == null ||
            rotateAction.action == null ||
            lookDeltaAction == null ||
            lookDeltaAction.action == null)
        {
            return;
        }

        // Rotate only while the middle mouse button is held.
        if (!rotateAction.action.IsPressed())
        {
            return;
        }

        Vector2 mouseDelta =
            lookDeltaAction.action.ReadValue<Vector2>();

        yaw += mouseDelta.x * rotationSensitivity;
        pitch -= mouseDelta.y * rotationSensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleZoom()
    {
        if (zoomAction == null || zoomAction.action == null)
        {
            return;
        }

        Vector2 scroll = zoomAction.action.ReadValue<Vector2>();

        if (Mathf.Abs(scroll.y) < 0.01f)
        {
            return;
        }

        // Normalize devices that report wheel movement in 120-unit steps.
        float scrollAmount = scroll.y;

        if (Mathf.Abs(scrollAmount) > 1f)
        {
            scrollAmount /= 120f;
        }

        Vector3 zoomDirection = viewCamera != null
            ? viewCamera.forward
            : transform.forward;

        targetPosition += zoomDirection * scrollAmount * zoomSpeed;

        // Keep the camera within the configured height limits.
        targetPosition.y = Mathf.Clamp(
            targetPosition.y,
            minCameraHeight,
            maxCameraHeight
        );
    }

    private void LateUpdate()
    {
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref smoothVelocity,
            smoothTime
        );
    }
}
