using UnityEngine;

/// <summary>
/// Y座標を奥行きとして扱う疑似3Dシステム。
/// Y が小さいほど手前（画面下）= 高い sortingOrder で描画される。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PseudoZ : MonoBehaviour
{
    [Tooltip("Y=minY のときの sortingOrder（最手前）")]
    public int frontSortingOrder = 10;
    [Tooltip("Y=maxY のときの sortingOrder（最奥）")]
    public int backSortingOrder  = 0;

    [Tooltip("Y の移動範囲（最手前）")]
    public float minY = -0.7f;
    [Tooltip("Y の移動範囲（最奥）")]
    public float maxY =  0.4f;

    [Tooltip("奥に行くほど縮小するか（0 = 縮小なし）")]
    [Range(0f, 0.5f)]
    public float scaleDepthFactor = 0.15f;

    private SpriteRenderer sr;
    private Vector3 baseScale;

    private void Awake()
    {
        sr        = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    private void LateUpdate()
    {
        float y     = transform.position.y;
        float range = Mathf.Max(maxY - minY, 0.001f);
        float t     = Mathf.InverseLerp(minY, maxY, y); // 0=手前, 1=奥

        // sortingOrder: 手前ほど大きい値（上に描画される）
        sr.sortingOrder = Mathf.RoundToInt(Mathf.Lerp(frontSortingOrder, backSortingOrder, t));

        // スケール：奥ほど少し小さく（遠近感）
        if (scaleDepthFactor > 0f)
        {
            float scale = 1f - t * scaleDepthFactor;
            transform.localScale = baseScale * scale;
        }
    }
}
