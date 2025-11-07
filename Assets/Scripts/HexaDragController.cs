using System.Linq;
using UnityEngine;
using DG.Tweening;

public class HexaDragController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private HexaItem currentHexa;
    [SerializeField] private Camera mainCam;

    [Header("Settings")]
    [SerializeField] private float dragSmooth = 10f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float maxAngleOffset = 30f;
    [SerializeField] private float baseYRotation = 90f;
    [SerializeField] private float returnSpeed = 2f;
    [SerializeField] private float angleTolerance = 1.5f; 

    private bool isDragging = false;
    private bool _canDragBoard = false;
    private Vector3 dragStartPos;
    private float minX, maxX;
    private CellHexa currentHighlightCell;
    private CellHexa nearest = null;
    private Vector3 _lastMousePos;

    public void Init()
    {
        float minCellX = gridManager.AllCells.Min(c => c.transform.position.x);
        float maxCellX = gridManager.AllCells.Max(c => c.transform.position.x);
        float padding = 0.4f;
        minX = minCellX - padding;
        maxX = maxCellX + padding;
    }

    private void Update()
    {
        if (currentHexa == null) return;

        float currentY = NormalizeAngle(GamePlayManager.Instance.Grid.transform.eulerAngles.y);
        bool gridAtBaseRotation = Mathf.Abs(currentY - baseYRotation) <= angleTolerance;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Board"))
                {
                    _canDragBoard = true;
                    _lastMousePos = Input.mousePosition;
                    return;
                }
            }

            if (!gridAtBaseRotation) return;

            isDragging = true;
            dragStartPos = currentHexa.transform.position;
        }

        if (_canDragBoard && Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            float rotateY = -delta.x * rotationSpeed * Time.deltaTime;

            Vector3 currentRotation = GamePlayManager.Instance.Grid.transform.eulerAngles;
            float targetY = NormalizeAngle(currentRotation.y + rotateY);
            float minY = baseYRotation - maxAngleOffset;
            float maxY = baseYRotation + maxAngleOffset;
            targetY = Mathf.Clamp(targetY, minY, maxY);

            Quaternion targetRot = Quaternion.Euler(0, targetY, 0);
            GamePlayManager.Instance.Grid.transform.rotation = Quaternion.Lerp(
                GamePlayManager.Instance.Grid.transform.rotation,
                targetRot,
                15f * Time.deltaTime
            );

            _lastMousePos = Input.mousePosition;
        }

        if (_canDragBoard && Input.GetMouseButtonUp(0))
        {
            _canDragBoard = false;

            GamePlayManager.Instance.Grid.transform
                .DORotate(new Vector3(0, baseYRotation, 0), 0.6f)
                .SetEase(Ease.OutBack);
        }

        if (!_canDragBoard)
        {
            Quaternion currentRot = GamePlayManager.Instance.Grid.transform.rotation;
            Quaternion targetRot = Quaternion.Euler(0, baseYRotation, 0);
            GamePlayManager.Instance.Grid.transform.rotation = Quaternion.Lerp(
                currentRot,
                targetRot,
                Time.deltaTime * returnSpeed
            );
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            if (!gridAtBaseRotation) return;

            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);

                Vector3 targetPos = currentHexa.transform.position;
                targetPos.x = Mathf.Clamp(hitPoint.x, minX, maxX);
                targetPos.z = dragStartPos.z;
                targetPos.y = currentHexa.transform.position.y;

                currentHexa.transform.position = Vector3.Lerp(
                    currentHexa.transform.position,
                    targetPos,
                    Time.deltaTime * dragSmooth
                );

                nearest = gridManager.GetNearestCellUnder(currentHexa.transform.position);
                if (nearest != currentHighlightCell)
                {
                    if (currentHighlightCell != null)
                        currentHighlightCell.HideHighLight();

                    if (nearest != null)
                        nearest.HighLight(currentHexa.GetTransparentColor());

                    currentHighlightCell = nearest;
                }
            }
        }

        // --- Thả Hexa ---
        if (isDragging && Input.GetMouseButtonUp(0))
        {
            isDragging = false;

            if (currentHighlightCell != null)
            {
                currentHighlightCell.HideHighLight();
                currentHighlightCell = null;
            }

            int columnX = gridManager.GetNearestColumnX(currentHexa.transform.position.x);
            bool placed = gridManager.InsertHexaInColumn(currentHexa, columnX, nearest);
            nearest = null;

            if (placed)
            {
                currentHexa = GamePlayManager.Instance.SpawnNextHexa();
                SetCurrentHexa(currentHexa);
            }
        }
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public void SetCurrentHexa(HexaItem hexa)
    {
        currentHexa = hexa;
    }
}
