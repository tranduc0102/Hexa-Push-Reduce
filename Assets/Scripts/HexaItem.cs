using DG.Tweening;
using TMPro;
using UnityEngine;

public class HexaItem : MonoBehaviour
{
    [Header("Render")]
    [SerializeField] private MeshRenderer m_MeshRenderer;
    [SerializeField] private TextMeshPro m_txtNumber;
    [SerializeField] private Vector3 _baseScale = Vector3.one;
    private bool _canCollected = true;
    public bool CanCollected => _canCollected;
    private CellHexa _currentCell;
    private Tween moveTween;
    private bool _isChecking = false;
    public bool IsChecking
    {
        get {  return _isChecking; }
        set { _isChecking = value; }
    }
    private int _number;
    private Color _color;

    public int Number => _number;
    public Color Color => _color;
    public Color GetTransparentColor(float alpha = 0.5f, float addValue = 25f)
    {
        float delta = addValue / 255f;
        float r = Mathf.Clamp01(_color.r + delta);
        float g = Mathf.Clamp01(_color.g + delta);
        float b = Mathf.Clamp01(_color.b + delta);
        return new Color(r, g, b, alpha);
    }

    public CellHexa CurrentCell => _currentCell;
   

    public void Init(int number)
    {
        _number = number;

        _color = GamePlayManager.Instance.Colors[(int)(number - 1)];
        m_MeshRenderer.material.color = _color;
        m_txtNumber.text = _number.ToString();
        var baseScaleTemp = _baseScale;
        baseScaleTemp.y *= Mathf.Max(1f, _number);
        transform.localScale = baseScaleTemp;
    }

 
    public void SetCellHexa(CellHexa cell)
    {
        _currentCell = cell;
        _currentCell?.SetItemHexa(this);
    }

    public void ClearCell()
    {
        if (_currentCell != null)
        {
            _currentCell.ClearItem();
            _currentCell = null;
        }
    }

    public void MoveToCell(CellHexa targetCell, float duration = 0.3f)
    {
        if (targetCell == null) return;

        if (_currentCell != null)
            _currentCell.ClearItem();

        _currentCell = targetCell;
        targetCell.SetItemHexa(this);

        moveTween?.Kill();
        moveTween = transform.DOMove(targetCell.transform.position + Vector3.up * 0.1f, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                transform.DOScaleY(transform.localScale.y * 1.25f, 0.2f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(2, LoopType.Yoyo);
                transform.SetParent(targetCell.transform);
                IsChecking = false; 
            });

        transform.rotation = targetCell.transform.rotation;
    }
    private void OnDestroy()
    {
        transform.DOKill();
    }
    public void Collect()
    {
        if(!_canCollected) return;
        _canCollected = false;
        _number -= 1;
        GamePlayManager.Instance.Total += 1;
        Debug.LogError(GamePlayManager.Instance.Total);
        if (_number <= 0)
        {
            transform.DOScaleY(transform.localScale.y * 1.25f, 0.2f)
                        .SetEase(Ease.InOutSine).SetLoops(2, LoopType.Yoyo)
                .OnComplete(() =>
                {
                    transform.DOScale(0, 0.2f)
                        .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    _currentCell?.ClearItem();
                    ClearCell();
                    _canCollected = true;
                    GamePlayManager.Instance.HexaSpawner.ReturnToPool(this);
                });
                  
                });
        }
        else
        {
            m_txtNumber.text = _number.ToString();
            m_MeshRenderer.material.DOColor(GamePlayManager.Instance.Colors[(int)(_number - 1)], 0.2f);
            transform.DOScaleY(transform.localScale.y * 1.25f, 0.2f)
                        .SetEase(Ease.InOutSine).OnComplete(() =>
                        {
                            var baseScaleTemp = _baseScale;
                            baseScaleTemp.y *= Mathf.Max(1f, _number);
                            transform.DOScaleY(baseScaleTemp.y * 1.25f, 0.2f)
                                        .SetEase(Ease.InOutSine).OnComplete(() => { _canCollected = true;});

                        });
        }
    }
}
