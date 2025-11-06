using System.Linq;
using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;

public class HexaDragController : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private GridManager gridManager;

    [SerializeField] private HexaItem currentHexa;
    [SerializeField] private Camera mainCam;

    [Header("Settings")] [SerializeField] private float dragSmooth = 10f;

    private bool isDragging = false;
    private Vector3 dragStartPos;
    private float minX, maxX;
    private CellHexa currentHighlightCell;

    private void Start()
    {
        float minCellX = gridManager.allCells.Min(c => c.transform.position.x);
        float maxCellX = gridManager.allCells.Max(c => c.transform.position.x);

        float padding = 0.4f; 
        minX = minCellX - padding;
        maxX = maxCellX + padding;
    }


    private CellHexa nearest = null;
    private void Update()
    {
        if (currentHexa == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartPos = currentHexa.transform.position;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
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
                    {
                        nearest.HighLight();
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

            gridManager.InsertHexaInColumn(currentHexa, columnX, nearest);
            nearest = null;

            currentHexa = null;
        }
    }

    public void SetCurrentHexa(HexaItem hexa)
    {
        currentHexa = hexa;
    }
}