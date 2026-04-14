using System.Collections;
using UnityEngine;

/// <summary>
/// ヒットストップ・スロー演出を管理するシングルトン。
///
/// 使い方:
///   HitstopManager.Instance.TriggerHit();          // 通常ヒット
///   HitstopManager.Instance.TriggerGuardBreak();    // ガードブレイク
///   HitstopManager.Instance.TriggerParrySuccess();  // パリィ成功
///
/// 設計方針（参考ゲーム）:
///   攻撃ヒット     … Sekiro 方式：短い完全停止でズシン感
///   ガードブレイク … やや長い完全停止（より重い）
///   パリィ成功     … Ghost of Tsushima + Sekiro 折衷：
///                     極短い完全停止 → スロー → 通常復帰
/// </summary>
public class HitstopManager : MonoBehaviour
{
    public static HitstopManager Instance { get; private set; }

    [Header("Attack Hit")]
    [Tooltip("通常ヒット時の完全停止時間（秒・実時間）")]
    public float hitStopDuration = 0.08f;

    [Header("Guard Break")]
    [Tooltip("ガードブレイク時の完全停止時間（秒・実時間）")]
    public float guardBreakStopDuration = 0.12f;

    [Header("Parry Success")]
    [Tooltip("パリィ成功：最初の完全停止時間（秒・実時間）")]
    public float parryFreezeDuration = 0.06f;
    [Tooltip("パリィ成功：スロー中の timeScale（0=完全停止, 1=通常速度）")]
    public float parrySlowTimeScale = 0.15f;
    [Tooltip("パリィ成功：スロー持続時間（秒・実時間）")]
    public float parrySlowDuration = 0.25f;

    // -------------------------------------------------------------------------

    private Coroutine currentRoutine;

    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        // timeScale が止まったまま残らないよう保険
        if (Instance == this)
        {
            Time.timeScale = 1f;
            Instance = null;
        }
    }

    // =========================================================================
    // 公開メソッド
    // =========================================================================

    /// <summary>通常攻撃ヒット（Vulnerable / Dead など）</summary>
    public void TriggerHit()
    {
        Play(SimpleStop(hitStopDuration));
    }

    /// <summary>ガードブレイク</summary>
    public void TriggerGuardBreak()
    {
        Play(SimpleStop(guardBreakStopDuration));
    }

    /// <summary>パリィ成功：完全停止 → スロー → 通常</summary>
    public void TriggerParrySuccess()
    {
        Play(ParrySuccessRoutine());
    }

    // =========================================================================
    // 内部実装
    // =========================================================================

    private void Play(IEnumerator routine)
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            Time.timeScale = 1f;
        }
        currentRoutine = StartCoroutine(routine);
    }

    private IEnumerator SimpleStop(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        currentRoutine = null;
    }

    /// <summary>
    /// パリィ成功の二段階演出:
    ///   1. 完全停止（parryFreezeDuration 実秒）
    ///   2. スロー（parrySlowTimeScale で parrySlowDuration 実秒）
    ///   3. 通常速度に復帰
    /// </summary>
    private IEnumerator ParrySuccessRoutine()
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(parryFreezeDuration);

        Time.timeScale = parrySlowTimeScale;
        yield return new WaitForSecondsRealtime(parrySlowDuration);

        Time.timeScale = 1f;
        currentRoutine = null;
    }
}
