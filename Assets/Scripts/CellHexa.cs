using DG.Tweening;
using TMPro;
using UnityEngine;

public class CellHexa : MonoBehaviour
{
    [Header("Render")]
    [SerializeField] private MeshRenderer _render;
    [SerializeField] private TextMeshPro _txt;
    private HexaItem _item;
    public HexaItem Item => _item;
    public bool IsEmpty => _item == null;
    public void HighLight()
    {
        _render.material.DOColor(Color.yellow, 0.2f); 
        _txt.gameObject.SetActive(true);
    }

    public void HideHighLight()
    {
        Color targetColor;
        if (ColorUtility.TryParseHtmlString("#545050", out targetColor))
        {
            _render.material.DOColor(targetColor, 0.2f);
        }
        _txt.gameObject.SetActive(false);
    }
    public void SetItemHexa(HexaItem item)
    {
        this._item = item;
    }
    public void ClearItem()
    {
        this._item = null;
    }
}
