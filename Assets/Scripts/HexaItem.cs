using DG.Tweening;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class HexaItem : MonoBehaviour
{
    [Header("Render")]
    [SerializeField] private MeshRenderer m_MeshRenderer;
    [SerializeField] private TextMeshPro m_txtNumber;
    [SerializeField] private Vector3 _baseScale;
    private CellHexa _cellHexa;
    private int _number;
    private Tween moveTween;

    public void Init(ColorHexa color, int number)
    {
        m_MeshRenderer.material.color = GamePlayManager.Instance.Colors[(int)color];
        m_txtNumber.text = number.ToString();
        _baseScale.y *= number;
        transform.localScale = _baseScale;
        _number = number;
    }
    public void SetCellHexa(CellHexa cellHexa)
    {
        _cellHexa = cellHexa;
        _cellHexa.SetItemHexa(this);
    }
    public void Collect()
    {
        _number -= 1;

        if (_number <= 0)
        {
            transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
            {
                _cellHexa?.ClearItem();
                Destroy(gameObject);
            });
        }
        else
        {
            m_txtNumber.text = _number.ToString();
            _baseScale.y *= _number;
            transform.DOScale(_baseScale, 0.25f).SetEase(Ease.OutBack);
        }
    }
    public void MoveToCell(CellHexa targetCell, float duration = 0.25f)
    {
        if (_cellHexa != null)
            _cellHexa.ClearItem();

        _cellHexa = targetCell;
        targetCell.SetItemHexa(this);

        moveTween?.Kill();

        moveTween = transform.DOMove(targetCell.transform.position + Vector3.up * 0.1f, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                _baseScale.y *= _number;
                transform.DOScale(_baseScale, 0.15f)
                         .SetEase(Ease.OutBack)
                         .SetLoops(2, LoopType.Yoyo);
            });
    }
}
public enum ColorHexa
{
    Red = 0,
    Blue = 1,
    Yellow = 2,
    Green = 3,
    Pink = 4,
    Purple = 5,
    Orange = 6,
    LightBlue = 7
}