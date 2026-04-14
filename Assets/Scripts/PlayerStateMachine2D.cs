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

        // ガード解除は左右移動のみ（W/S は疑似3D移動に使うため除外）
        bool horizontalInput = keyboard.aKey.isPressed
            || keyboard.dKey.isPressed
            || keyboard.leftArrowKey.isPressed
            || keyboard.rightArrowKey.isPressed;

        return horizontalInput;
    }

    private void HandleActionInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // J キー：Attack 中はコンボバッファとして受け付ける必要があるため
        // IsActionLocked では弾かない。TriggerAttack() 内部でロック判定を行う。
        if (keyboard.jKey.wasPressedThisFrame)
        {
            Debug.Log("Player Input: J -> Attack", this);
            TriggerAttack();
        }

        // K / L は Attack/Parry/Feint 中は無効。TriggerParry/Feint() 内のガードに任せる。
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
