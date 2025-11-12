using DG.Tweening;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class HexaItem : MonoBehaviour
{
    [Header("Render")]
    [SerializeField] private MeshRenderer m_MeshRenderer;
    [SerializeField] private TextMeshPro m_txtNumber;
    [SerializeField] private Vector3 _baseScale = Vector3.one;
    [SerializeField] private FlyToUIWithTrail m_flyToUI;

    [Header("High Light")]
    [SerializeField] private SpriteRenderer m_SpriteRenderer;
    [SerializeField] private GameObject _dontPushObject;
    private bool _canCollected = true;
    public bool CanCollected => _canCollected;
    private CellHexa _currentCell;
    private Tween moveTween;
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
   

    public void Init(int number, bool playAnim = false)
    {
        _number = number;

        _color = GamePlayManager.Instance.Colors[(int)(number - 1)];
        m_MeshRenderer.material.color = _color;
        m_txtNumber.text = _number.ToString();
        var baseScaleTemp = _baseScale;
        baseScaleTemp.y *= Mathf.Max(1f, _number);
        if (playAnim) {
            transform.DOScale(baseScaleTemp * 1.05f, 0.25f)
              .SetEase(Ease.OutBack)
              .SetLoops(2, LoopType.Yoyo);
        }
        else
        {
            transform.localScale = baseScaleTemp;

        }
        _canCollected = true;
        m_SpriteRenderer.color = _color * 1.2f;
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

        moveTween?.Kill(true);
        transform.DOKill(true);
        moveTween = transform.DOMove(targetCell.transform.position + Vector3.up * 0.1f, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                transform.DOScaleY(transform.localScale.y * 1.25f, 0.2f).SetAutoKill(true)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(2, LoopType.Yoyo);
                transform.SetParent(targetCell.transform);
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
        AudioManager.Instance.PlayCollectCoin();
        transform.DOKill(true);
        var baseScaleTemp = _baseScale;
        baseScaleTemp.y *= Mathf.Max(1f, _number);
        var fx = Instantiate(m_flyToUI, transform);
        fx.transform.localScale = m_flyToUI.transform.localScale;
        fx.transform.localRotation = m_flyToUI.transform.localRotation;
        fx.SetColor((GamePlayManager.Instance.Colors[(int)(_number)]));
        fx.FlyToUI(transform);
        if (_number <= 0)
        {
            if(fx != null)
            {
                fx.transform.SetParent(transform.root);
            }
            transform.DOScaleY(transform.localScale.y * 1.25f, 0.2f).SetAutoKill(true)
                        .SetEase(Ease.InOutSine).SetLoops(2, LoopType.Yoyo)
                .OnComplete(() =>
                {
                    _canCollected = true;
                    transform.DOScale(0, 0.2f)
                        .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    _currentCell?.ClearItem();
                    ClearCell();
                    GamePlayManager.Instance.HexaSpawner.ReturnToPool(this);
                });

                });
        }
        else
        {
            m_txtNumber.text = _number.ToString();
            m_MeshRenderer.material.DOColor(GamePlayManager.Instance.Colors[(int)(_number - 1)], 0.2f).SetAutoKill(true);
            transform.DOScaleY(transform.localScale.y * 1.25f, 0.2f).SetAutoKill(true)
                        .SetEase(Ease.InOutSine).OnComplete(() =>
                        {
                            transform.DOScaleY(baseScaleTemp.y, 0.2f)
                                        .SetEase(Ease.InOutSine).OnComplete(() => { _canCollected = true; });
                        });
        }
       
    }
    public void ShowHightLigh(bool show, bool dontPush = false)
    {
        if(m_SpriteRenderer.gameObject.activeSelf != show)
        {
            m_SpriteRenderer.gameObject.SetActive(show);
        }
        _dontPushObject.gameObject.SetActive(dontPush);
    }
}
