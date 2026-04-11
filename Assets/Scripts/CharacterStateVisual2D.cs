using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class CharacterStateVisual2D : MonoBehaviour
{
    // 色変更なし。スプライト本来の色をそのまま表示する。
    public bool isPlayer; // 他スクリプトとの互換用に残す

    private void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.white;
    }
}
