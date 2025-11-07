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
    public int q;
    public int r; 
    private Color _baseColor;
    private Tween _colorTween;
    public bool IsHidden => !_render.enabled;
    private void Awake()
    {
        _baseColor = _render.material.color;
        if (_txt != null)
            _txt.gameObject.SetActive(false);
    }
    public void InitCoords(int q, int r)
    {
        this.q = q;
        this.r = r;
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

        _colorTween = _render.material.DOColor(_baseColor, 0.25f);

        if (_txt != null)
            _txt.gameObject.SetActive(false);
    }
    public void HideVisual()
    {
        if (_render) _render.enabled = false;
    }

}
