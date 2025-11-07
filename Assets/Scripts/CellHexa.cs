using DG.Tweening;
using TMPro;
using UnityEngine;

public class CellHexa : MonoBehaviour
{
    [Header("Render")]
    [SerializeField] private MeshRenderer _render;
    [SerializeField] private TextMeshPro _txt;

    [Header("State")]
    private HexaItem _item;
    public HexaItem Item => _item;
    public bool IsEmpty => _item == null;

    [Header("Grid Coords (for logic, not transform)")]
    public int q; // cột (col)
    public int r; // hàng (row)

    private Color _baseColor; // màu gốc để reset lại khi bỏ highlight
    private Tween _colorTween;

    private void Awake()
    {
        // Lưu màu gốc của material để reset nhanh
        _baseColor = _render.material.color;

        // Ẩn text debug ban đầu
        if (_txt != null)
            _txt.gameObject.SetActive(false);
    }
    public void InitCoords(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    public void SetCoords(int q, int r)
    {
        this.q = q;
    }
    public void SetItemHexa(HexaItem item)
    {
        _item = item;
    }

    public void ClearItem()
    {
        _item = null;
    }
    public void HighLight(Color color)
    {
        _colorTween?.Kill();

        _colorTween = _render.material
            .DOColor(color, 0.15f)
            .SetEase(Ease.OutQuad);

        if (_txt != null)
            _txt.gameObject.SetActive(true);
    }

    public void HideHighLight()
    {
        _colorTween?.Kill();

        // Reset về màu nền #545050 (hoặc màu gốc nếu muốn)
        if (ColorUtility.TryParseHtmlString("#545050", out Color targetColor))
            _colorTween = _render.material.DOColor(targetColor, 0.25f);
        else
            _colorTween = _render.material.DOColor(_baseColor, 0.25f);

        if (_txt != null)
            _txt.gameObject.SetActive(false);
    }

    //=========================================================
    // DEBUG UTILS
    //=========================================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
