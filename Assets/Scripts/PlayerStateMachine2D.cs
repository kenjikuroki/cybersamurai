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
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        bool horizontalInput = keyboard.aKey.isPressed
            || keyboard.dKey.isPressed
            || keyboard.leftArrowKey.isPressed
            || keyboard.rightArrowKey.isPressed;

        bool verticalInput = keyboard.wKey.isPressed
            || keyboard.sKey.isPressed
            || keyboard.upArrowKey.isPressed
            || keyboard.downArrowKey.isPressed;

        return horizontalInput || verticalInput;
    }

    private void HandleActionInput()
    {
        // アクションロック中（Attack/Parry/Feint モーション中）は入力を全て無視する
        if (IsActionLocked)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.jKey.wasPressedThisFrame)
        {
            Debug.Log("Player Input: J -> Attack", this);
            TriggerAttack();
        }

        if (keyboard.kKey.wasPressedThisFrame)
        {
            Debug.Log("Player Input: K -> Parry", this);
            TriggerParry();
        }

        if (keyboard.lKey.wasPressedThisFrame)
        {
            Debug.Log("Player Input: L -> Feint", this);
            TriggerFeint();
        }
    }

    private void LogCurrentState(bool force = false)
    {
        if (!force && hasLoggedInitialState && lastLoggedState == CurrentStateType)
        {
            return;
        }

        lastLoggedState = CurrentStateType;
        hasLoggedInitialState = true;
        Debug.Log($"Player Current State: {CurrentStateType}", this);
    }
}
