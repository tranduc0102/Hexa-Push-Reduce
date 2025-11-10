using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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



    [Header("Camera Setting")]
    [SerializeField] private Camera _cam;
    [SerializeField] private float _offsetZoomSize;
    [SerializeField] private float _timeZoom;
    [SerializeField] private float _offsetMoveY;
    private Vector3 _dragOffset;
    private bool _isZoom;
    public void Init()
    {
        float minCellX = gridManager.AllCells.Min(c => c.transform.position.x);
        float maxCellX = gridManager.AllCells.Max(c => c.transform.position.x);
        float padding = 0.4f;
        minX = minCellX - padding;
        maxX = maxCellX + padding;
        if (currentHexa)
        {
            GamePlayManager.Instance.HexaSpawner.ReturnToPool(currentHexa);
            currentHexa = null;
        }
    }

    private void Update()
    {
        if (currentHexa == null || GamePlayManager.Instance.State != GamePlayManager.GameState.Playing || IsPointerOverUIElement()) return;

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
                    if(currentHighlightCell != null)
                    {
                        currentHighlightCell.HideHighLight();
                    }
                    return;
                }
            }
            if (!gridAtBaseRotation) return;
            isDragging = true;
            dragStartPos = currentHexa.transform.position;
            Plane groundPlane = new Plane(Vector3.up, dragStartPos);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                _dragOffset = dragStartPos - hitPoint; 
            }
        }

        if (_canDragBoard && Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            float rotateY = -delta.x * rotationSpeed;

            float targetY = GamePlayManager.Instance.Grid.transform.eulerAngles.y + rotateY;
            targetY = NormalizeAngle(targetY);
            targetY = Mathf.Clamp(targetY, baseYRotation - maxAngleOffset, baseYRotation + maxAngleOffset);

            GamePlayManager.Instance.Grid.transform.rotation = Quaternion.Euler(0, targetY, 0);

            _lastMousePos = Input.mousePosition;
            ZoomCamera(true);
            currentHexa.ShowHightLigh(false);
        }

        if (_canDragBoard && Input.GetMouseButtonUp(0))
        {
            _canDragBoard = false;
            if (currentHighlightCell != null)
            {
                currentHexa.ShowHightLigh(true);
                currentHighlightCell.HighLight(currentHexa.Color);
            }
            else
            {
                currentHexa.ShowHightLigh(true, true);

            }
            GamePlayManager.Instance.Grid.transform
                    .DORotate(new Vector3(0, baseYRotation, 0), 0.6f)
                    .SetEase(Ease.OutBack);
            ZoomCamera(false);
        }

        if (!_canDragBoard && !DOTween.IsTweening(GamePlayManager.Instance.Grid.transform))
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
            Plane groundPlane = new Plane(Vector3.up, dragStartPos);
           
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);

                Vector3 targetPos = hitPoint + _dragOffset;
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                targetPos.z = dragStartPos.z;
                targetPos.y = dragStartPos.y;

                currentHexa.transform.position = Vector3.Lerp(
                    currentHexa.transform.position,
                    targetPos,
                    Time.deltaTime * dragSmooth
                );

                nearest = gridManager.GetNearestCellUnder(currentHexa.transform.position);
                if (nearest != currentHighlightCell)
                {
                    if (currentHighlightCell != null)
                    {
                        currentHexa.ShowHightLigh(true, true);
                        currentHighlightCell.HideHighLight();
                    }

                    if (nearest != null)
                    {
                        nearest.HighLight(currentHexa.GetTransparentColor());
                        currentHexa.ShowHightLigh(true);
                    }


                    currentHighlightCell = nearest;
                }
            }
        }

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
            if (placed)
            {
                currentHexa.ShowHightLigh(false);
                currentHexa = null;
            }
            nearest = null;
        }
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
    private void ZoomCamera(bool isZoomOut)
    {
        if (_isZoom == isZoomOut) return; 

        _cam.DOKill(true);
        if (currentHexa != null)
            currentHexa.transform.DOKill(true);

        float duration = 0.5f;
        Ease easeType = Ease.OutCubic; 

        if (isZoomOut)
        {
            _cam.DOOrthoSize(_cam.orthographicSize + _offsetZoomSize, duration)
                .SetEase(easeType);

            if (currentHexa != null)
            {
                currentHexa.transform.DOLocalMoveY(_offsetMoveY, duration)
                    .SetEase(Ease.OutBack);
            }

            _isZoom = true;
        }
        else
        {
            _cam.DOOrthoSize(_cam.orthographicSize - _offsetZoomSize, duration)
                .SetEase(easeType);

            if (currentHexa != null)
            {
                currentHexa.transform.DOLocalMoveY(0f, duration)
                    .SetEase(Ease.OutCubic);
            }

            _isZoom = false;
        }
    }


    public void SetCurrentHexa(HexaItem hexa)
    {
        currentHexa = hexa;
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            nearest = gridManager.GetNearestCellUnder(currentHexa.transform.position);
            if(nearest == null)
            {
                currentHexa.ShowHightLigh(true, true);
            }
            if (nearest != currentHighlightCell)
            {
                if (currentHighlightCell != null)
                {
                    currentHighlightCell.HideHighLight();
                }

                if (nearest != null)
                {
                    nearest.HighLight(currentHexa.GetTransparentColor());
                    currentHexa.ShowHightLigh(true);
                }

                currentHighlightCell = nearest;
            }
        }
    }

    private bool IsPointerOverUIElement()
    {
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        foreach (RaycastResult unused in results)
        {
            if (unused.gameObject.layer == LayerMask.NameToLayer("UI"))
            {
                return true;
            }
        }

        return false;
    }
}
