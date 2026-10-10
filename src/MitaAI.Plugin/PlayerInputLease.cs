using UnityEngine;

namespace MitaAI.Plugin;

internal sealed class PlayerInputLease
{
    private PlayerMove? player;
    private CursorLockMode beforeLock;
    private bool beforeVisible;
    private bool beforeEnabled;
    private PlayerMove? recoveryPlayer;
    internal bool HasPendingCursorRecovery => recoveryPlayer != null;
    internal bool HasLease { get; private set; }

    internal bool IsValid => HasLease && player != null && player.gameObject.activeInHierarchy
        && player.gameObject.scene.isLoaded;

    internal bool GameHasTakenControl => !IsValid || Time.timeScale <= 0
        || HasScriptedControl(player!) || player!.enabled || player.dontMove || player.stopMouseMove
        || player.blockInteractive != 0;

    internal string TakeoverReason => !IsValid ? "player-unavailable"
        : $"timeScale={Time.timeScale} enabled={player!.enabled} dontMove={player.dontMove} stopMouse={player.stopMouseMove} interaction={player.blockInteractive} scripted={HasScriptedControl(player)} cursor={Cursor.lockState}";

    internal static bool CanAcquire(PlayerMove candidate) => candidate.isActiveAndEnabled
        && candidate.gameObject.scene.isLoaded && Time.timeScale > 0
        && !candidate.dontMove && !candidate.stopMouseMove && candidate.blockInteractive == 0
        && !HasScriptedControl(candidate) && Cursor.lockState == CursorLockMode.Locked;

    private static bool HasScriptedControl(PlayerMove candidate) => candidate.animationLoopRun
        || candidate.animationFast || candidate.cameraOtherTarget != null || candidate.lookTarget != null;

    internal void Acquire(PlayerMove candidate)
    {
        if (HasLease || !CanAcquire(candidate)) throw new InvalidOperationException("Player input is busy.");
        recoveryPlayer = null;
        beforeLock = Cursor.lockState;
        beforeVisible = Cursor.visible;
        beforeEnabled = candidate.enabled;
        player = candidate;
        HasLease = true;
        // Suspend only this player's controller while composing. Leave story-owned flags untouched.
        candidate.enabled = false;
        MaintainCursor();
    }

    internal void MaintainCursor()
    {
        if (!HasLease) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    internal void Release(bool restoreCursor, bool deferCursorRecovery = false)
    {
        if (!HasLease) return;
        HasLease = false;
        var previousPlayer = player;
        player = null;
        if (previousPlayer != null && previousPlayer.gameObject.scene.isLoaded && !previousPlayer.enabled)
            previousPlayer.enabled = beforeEnabled;
        recoveryPlayer = deferCursorRecovery && previousPlayer != null && previousPlayer.gameObject.scene.isLoaded
            ? previousPlayer : null;
        if (!restoreCursor) return;
        if (Cursor.lockState == CursorLockMode.None) Cursor.lockState = beforeLock;
        if (Cursor.visible) Cursor.visible = beforeVisible;
    }

    internal bool TryRecoverCursor()
    {
        var previousPlayer = recoveryPlayer;
        if (previousPlayer == null) return false;
        if (!previousPlayer.gameObject.scene.isLoaded || !previousPlayer.gameObject.activeInHierarchy)
        {
            recoveryPlayer = null;
            return false;
        }
        if (!Application.isFocused || Time.timeScale <= 0 || !previousPlayer.isActiveAndEnabled
            || previousPlayer.dontMove || previousPlayer.stopMouseMove || previousPlayer.blockInteractive != 0
            || HasScriptedControl(previousPlayer)) return false;
        recoveryPlayer = null;
        // Recover only our captured cursor after a same-player takeover, never arbitrary unlocked menus.
        if (Cursor.lockState != CursorLockMode.None) return false;
        Cursor.lockState = beforeLock;
        Cursor.visible = beforeVisible;
        return true;
    }

    internal void DiscardCursorRecovery() => recoveryPlayer = null;

}
