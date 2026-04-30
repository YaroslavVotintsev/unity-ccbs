using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController2D : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public Vector2 offset = Vector2.zero;

    [Header("Pan Settings")]
    public bool enablePan = true;
    public float panSpeed = 20f;
    public float panBorderThickness = 10f;
    public bool useEdgePanning = false;
    public Vector2 panLimitMin = new Vector2(-100, -100);
    public Vector2 panLimitMax = new Vector2(100, 100);

    [Header("Zoom Settings")]
    public bool enableZoom = true;
    public float zoomSpeed = 5f;
    public float minZoom = 3f;
    public float maxZoom = 15f;

    [Header("Follow Settings")]
    public bool followPlayer = true;
    public float followSmoothSpeed = 5f;

    private Camera cam;
    private Vector3 dragOrigin;
    private bool isDragging;
    private float currentZoom;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        currentZoom = cam.orthographicSize;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    void LateUpdate()
    {
        HandleZoom();
        HandlePan();
        HandleFollow();
    }

    void HandleZoom()
    {
        if (!enableZoom || Mouse.current == null) return;

        float scrollInput = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            currentZoom -= scrollInput * zoomSpeed * 0.01f;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            cam.orthographicSize = currentZoom;
        }
    }

    void HandlePan()
    {
        if (!enablePan || Mouse.current == null || Keyboard.current == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        bool mouseDown =
            Mouse.current.leftButton.wasPressedThisFrame ||
            Mouse.current.middleButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame;

        bool mouseUp =
            Mouse.current.leftButton.wasReleasedThisFrame ||
            Mouse.current.middleButton.wasReleasedThisFrame ||
            Mouse.current.rightButton.wasReleasedThisFrame;

        if (mouseDown)
        {
            dragOrigin = cam.ScreenToWorldPoint(mousePosition);
            isDragging = true;
        }

        if (mouseUp)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector3 difference = dragOrigin - cam.ScreenToWorldPoint(mousePosition);
            Vector3 newPosition = transform.position + difference;
            transform.position = ClampCameraPosition(newPosition);
        }

        if (useEdgePanning && !isDragging && !followPlayer)
        {
            Vector3 moveDirection = Vector3.zero;

            if (mousePosition.x >= Screen.width - panBorderThickness)
                moveDirection.x += 1;
            if (mousePosition.x <= panBorderThickness)
                moveDirection.x -= 1;
            if (mousePosition.y >= Screen.height - panBorderThickness)
                moveDirection.y += 1;
            if (mousePosition.y <= panBorderThickness)
                moveDirection.y -= 1;

            if (moveDirection != Vector3.zero)
            {
                Vector3 newPosition = transform.position + moveDirection.normalized * panSpeed * Time.deltaTime;
                transform.position = ClampCameraPosition(newPosition);
            }
        }

        Vector2 keyboardMove = Vector2.zero;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            keyboardMove.x -= 1;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            keyboardMove.x += 1;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            keyboardMove.y -= 1;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            keyboardMove.y += 1;

        if (keyboardMove != Vector2.zero && !followPlayer)
        {
            Vector3 move = new Vector3(keyboardMove.x, keyboardMove.y, 0).normalized * panSpeed * Time.deltaTime;
            Vector3 newPosition = transform.position + move;
            transform.position = ClampCameraPosition(newPosition);
        }
    }

    void HandleFollow()
    {
        if (!followPlayer || target == null) return;

        Vector3 targetPosition = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        targetPosition = ClampCameraPosition(targetPosition);

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSmoothSpeed * Time.deltaTime
        );
    }

    Vector3 ClampCameraPosition(Vector3 position)
    {
        float vertExtent = cam.orthographicSize;
        float horzExtent = vertExtent * Screen.width / Screen.height;

        float minX = panLimitMin.x + horzExtent;
        float maxX = panLimitMax.x - horzExtent;
        float minY = panLimitMin.y + vertExtent;
        float maxY = panLimitMax.y - vertExtent;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return position;
    }

    public void CenterOnPlayer()
    {
        if (target == null) return;

        Vector3 targetPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        transform.position = ClampCameraPosition(targetPos);
    }

    public void SetFollow(bool follow)
    {
        followPlayer = follow;
    }

    public void SetZoom(float zoomLevel)
    {
        currentZoom = Mathf.Clamp(zoomLevel, minZoom, maxZoom);
        cam.orthographicSize = currentZoom;
    }
}