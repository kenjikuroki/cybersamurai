using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStateMachine2D : CombatStateMachine2D
{
    private CombatStateType lastLoggedState;
    private bool hasLoggedInitialState;

    protected override void Start()
    {
        base.Start();
        LogCurrentState(force: true);
    }

    protected override void Update()
    {
        HandleActionInput();
        base.Update();
        LogCurrentState();
    }

    protected override bool QueryPrimaryInput()
    {
        // ガード解除：左右移動のみ
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.dKey.isPressed
             || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed)
                return true;
        }

        // ゲームパッド：スティック横 or 十字キー横
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (Mathf.Abs(gp.leftStick.x.ReadValue()) > 0.2f) return true;
            if (gp.dpad.left.isPressed || gp.dpad.right.isPressed) return true;
        }

        return false;
    }

    private void HandleActionInput()
    {
        bool attackPressed = false;
        bool parryPressed  = false;
        bool feintPressed  = false;

        // ── キーボード ──────────────────────────────────────────────────────
        var kb = Keyboard.current;
        if (kb != null)
        {
            attackPressed |= kb.jKey.wasPressedThisFrame;
            parryPressed  |= kb.kKey.wasPressedThisFrame;
            feintPressed  |= kb.lKey.wasPressedThisFrame;
        }

        // ── ゲームパッド ────────────────────────────────────────────────────
        // Xbox: X = buttonWest(攻撃)  Y = buttonNorth(パリィ)  A = buttonSouth(フェイント)
        // PS:   □ = buttonWest(攻撃)  △ = buttonNorth(パリィ)  × = buttonSouth(フェイント)
        var gp = Gamepad.current;
        if (gp != null)
        {
            attackPressed |= gp.buttonWest.wasPressedThisFrame;   // Xbox:X / PS:□
            parryPressed  |= gp.buttonNorth.wasPressedThisFrame;  // Xbox:Y / PS:△
            feintPressed  |= gp.buttonSouth.wasPressedThisFrame;  // Xbox:A / PS:×
        }

        // ── アクション発火 ──────────────────────────────────────────────────
        if (attackPressed)
        {
            Debug.Log("Player Input: Attack", this);
            TriggerAttack();
        }

        if (parryPressed)
        {
            Debug.Log("Player Input: Parry", this);
            TriggerParry();
        }

        if (feintPressed)
        {
            Debug.Log("Player Input: Feint", this);
            TriggerFeint();
        }
    }

    private void LogCurrentState(bool force = false)
    {
        if (!force && hasLoggedInitialState && lastLoggedState == CurrentStateType)
            return;

        lastLoggedState = CurrentStateType;
        hasLoggedInitialState = true;
        Debug.Log($"Player Current State: {CurrentStateType}", this);
    }
}
