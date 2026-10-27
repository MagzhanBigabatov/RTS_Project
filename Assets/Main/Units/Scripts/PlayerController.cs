using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference selectAction;
    [SerializeField] private InputActionReference commandAction;
    [SerializeField] private InputActionReference pointerPositionAction;

    [Header("Scene")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask raycastMask = ~0;
    [SerializeField] private int playerTeamId = 0;

    [Header("Selection")]
    [SerializeField, Min(1f)] private float dragThreshold = 10f;

    private readonly List<RTSUnit> selectedUnits = new();

    private bool isSelecting;
    private bool isDragging;

    private Vector2 dragStartPosition;
    private Vector2 pointerPosition;

    public IReadOnlyList<RTSUnit> SelectedUnits => selectedUnits;

    public RTSUnit PrimarySelectedUnit =>
        selectedUnits.Count > 0 ? selectedUnits[0] : null;

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        SetActionEnabled(selectAction, true);
        SetActionEnabled(commandAction, true);
        SetActionEnabled(pointerPositionAction, true);
    }

    private void OnDisable()
    {
        SetActionEnabled(selectAction, false);
        SetActionEnabled(commandAction, false);
        SetActionEnabled(pointerPositionAction, false);
    }

    private void SetActionEnabled(
        InputActionReference reference,
        bool enabled)
    {
        if (reference == null || reference.action == null)
        {
            return;
        }

        if (enabled)
        {
            reference.action.Enable();
        }
        else
        {
            reference.action.Disable();
        }
    }

    private void Update()
    {
        if (worldCamera == null ||
            selectAction?.action == null ||
            commandAction?.action == null ||
            pointerPositionAction?.action == null)
        {
            return;
        }

        pointerPosition =
            pointerPositionAction.action.ReadValue<Vector2>();

        HandleSelectionInput();

        if (commandAction.action.WasPressedThisFrame())
        {
            IssueCommand(pointerPosition);
        }
    }

    private void HandleSelectionInput()
    {
        if (selectAction.action.WasPressedThisFrame())
        {
            isSelecting = true;
            isDragging = false;
            dragStartPosition = pointerPosition;
        }

        if (isSelecting &&
            Vector2.Distance(dragStartPosition, pointerPosition)
                >= dragThreshold)
        {
            isDragging = true;
        }

        if (selectAction.action.WasReleasedThisFrame() && isSelecting)
        {
            if (isDragging)
            {
                SelectUnitsInRectangle();
            }
            else
            {
                SelectUnitAtPointer(pointerPosition);
            }

            isSelecting = false;
            isDragging = false;
        }
    }

    private void SelectUnitAtPointer(Vector2 position)
    {
        ClearSelection();

        if (!TryGetHit(position, out RaycastHit hit))
        {
            return;
        }

        RTSUnit unit = hit.collider.GetComponentInParent<RTSUnit>();

        if (unit != null && unit.TeamId == playerTeamId)
        {
            AddSelectedUnit(unit);
        }
    }

    private void SelectUnitsInRectangle()
    {
        ClearSelection();

        Rect rect = GetScreenRect(
            dragStartPosition,
            pointerPosition
        );

        RTSUnit[] allUnits =
            FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);

        foreach (RTSUnit unit in allUnits)
        {
            if (unit == null || unit.TeamId != playerTeamId)
            {
                continue;
            }

            Vector3 screenPos =
                worldCamera.WorldToScreenPoint(unit.transform.position);

            if (screenPos.z <= 0f)
            {
                continue;
            }

            if (rect.Contains(new Vector2(screenPos.x, screenPos.y)))
            {
                selectedUnits.Add(unit);
            }
        }

        // The unit nearest to the start of the rectangle is primary.
        selectedUnits.Sort((a, b) =>
            Vector2.Distance(dragStartPosition, GetScreenPosition(a))
                .CompareTo(
                    Vector2.Distance(
                        dragStartPosition,
                        GetScreenPosition(b)
                    )
                )
        );

        foreach (RTSUnit unit in selectedUnits)
        {
            unit.SetSelected(true);
        }
    }

    private Vector2 GetScreenPosition(RTSUnit unit)
    {
        Vector3 pos =
            worldCamera.WorldToScreenPoint(unit.transform.position);

        return new Vector2(pos.x, pos.y);
    }

    private void AddSelectedUnit(RTSUnit unit)
    {
        if (unit == null || selectedUnits.Contains(unit))
        {
            return;
        }

        selectedUnits.Add(unit);
        unit.SetSelected(true);
    }

    private void ClearSelection()
    {
        foreach (RTSUnit unit in selectedUnits)
        {
            if (unit != null)
            {
                unit.SetSelected(false);
            }
        }

        selectedUnits.Clear();
    }

    private void IssueCommand(Vector2 position)
    {
        if (selectedUnits.Count == 0 ||
            !TryGetHit(position, out RaycastHit hit))
        {
            return;
        }

        CommandPoint commandPoint =
            hit.collider.GetComponentInParent<CommandPoint>();

        if (commandPoint != null)
        {
            MoveSelectedUnits(commandPoint.WorldPosition);
            return;
        }

        RTSUnit target =
            hit.collider.GetComponentInParent<RTSUnit>();

        if (target != null && target.TeamId != playerTeamId)
        {
            foreach (RTSUnit unit in selectedUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                if (unit.IsBuilder)
                {
                    unit.CommandMove(target.transform.position);
                }
                else
                {
                    unit.CommandAttack(target);
                }
            }

            return;
        }

        MoveSelectedUnits(hit.point);
    }

    private void MoveSelectedUnits(Vector3 position)
    {
        foreach (RTSUnit unit in selectedUnits)
        {
            if (unit != null)
            {
                unit.CommandMove(position);
            }
        }
    }

    private bool TryGetHit(Vector2 position, out RaycastHit hit)
    {
        Ray ray = worldCamera.ScreenPointToRay(position);

        return Physics.Raycast(
            ray,
            out hit,
            5000f,
            raycastMask
        );
    }

    private static Rect GetScreenRect(Vector2 a, Vector2 b)
    {
        return Rect.MinMaxRect(
            Mathf.Min(a.x, b.x),
            Mathf.Min(a.y, b.y),
            Mathf.Max(a.x, b.x),
            Mathf.Max(a.y, b.y)
        );
    }

    private void OnGUI()
    {
        if (!isSelecting || !isDragging)
        {
            return;
        }

        Rect rect = GetScreenRect(
            dragStartPosition,
            pointerPosition
        );

        rect.y = Screen.height - rect.y - rect.height;

        GUI.Box(rect, GUIContent.none);
    }
}