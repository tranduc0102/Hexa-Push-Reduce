using DG.Tweening;
using UnityEngine;

public class FlyToUIWithTrail : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TrailRenderer trailRenderer;

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void SetColor(Color color)
    {
        spriteRenderer.color = color;
        trailRenderer.startColor = color;
        trailRenderer.endColor = color;
    }

    public void FlyToUI(Transform worldStart, float appearDuration = 0.15f, float flyDuration = 1f, float shrinkDuration = 0.15f)
    {
        gameObject.SetActive(true);
        RectTransform targetUI = UIManager.Instance.TargetUI;
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, targetUI.position);
        Vector3 worldTarget = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z))
        );
        worldTarget.z = 0;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScale(originalScale, appearDuration)
                .SetEase(Ease.OutBack).SetAutoKill(true));
        seq.Append(transform.DOScale(transform.localScale * 0.6f, 0.15f).SetAutoKill(true));
        seq.Join(transform.DOLocalMoveY(0.5f, 0.15f).SetAutoKill(true));

        seq.Append(transform.DOMove(worldTarget, 0.8f).SetAutoKill(true)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => UIManager.Instance.UpdatePointInUI(false)));

        seq.SetAutoKill(true).OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }
}
