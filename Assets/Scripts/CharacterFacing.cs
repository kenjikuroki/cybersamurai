using UnityEngine;

/// <summary>
/// キャラクターが常に指定ターゲットの方向を向くように SpriteRenderer を反転させる。
/// プレイヤー：最も近い敵を向く
/// 敵         ：プレイヤーを向く
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterFacing : MonoBehaviour
{
    [Tooltip("true = デフォルトで左向き（Enemy-Punk）、false = デフォルトで右向き（Brawler-Girl）")]
    public bool defaultFacingLeft = false;

    // 外部から設定するターゲット（PlayerFacing は FindClosestEnemy で自動、敵は PlayerTransform を設定）
    public Transform target;

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.01f) return;

        bool targetIsRight = dx > 0f;

        // defaultFacingLeft = true（元が左向き）のとき:
        //   ターゲットが右 → flipX = true（右向きに反転）
        //   ターゲットが左 → flipX = false（元の左向きのまま）
        // defaultFacingLeft = false（元が右向き）のとき:
        //   ターゲットが右 → flipX = false
        //   ターゲットが左 → flipX = true
        sr.flipX = defaultFacingLeft ? targetIsRight : !targetIsRight;
    }
}
