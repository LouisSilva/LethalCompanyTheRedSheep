using BepInEx.Logging;
using System;
using Unity.Netcode;
using Logger = BepInEx.Logging.Logger;

namespace LethalCompanyTheRedSheep;

public class TheRedSheepNetcodeController : NetworkBehaviour
{
    private ManualLogSource _mls;
    
    public event Action<string> OnInitializeConfigValues;
    public event Action<string> OnSyncRedSheepIdentifier;
    public event Action<string, ulong> OnChangeTargetPlayer;
    public event Action<string> OnEnterDeathState;
    public event Action<string, int> OnChangeBehaviourState;
    public event Action<string, int> OnDoAnimation;
    public event Action<string, int, bool> OnChangeAnimationParameterBool;
    
    private void Start()
    {
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Netcode Controller");
    }

    [ClientRpc]
    public void ChangeAnimationParameterBoolClientRpc(string receivedRedSheepId, int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(receivedRedSheepId, animationId, value);
    }

    [ClientRpc]
    public void DoAnimationClientRpc(string receivedRedSheepId, int animationId)
    {
        OnDoAnimation?.Invoke(receivedRedSheepId, animationId);
    }
    
    [ClientRpc]
    public void ChangeBehaviourStateClientRpc(string receivedRedSheepId, int newBehaviourStateIndex)
    {
        OnChangeBehaviourState?.Invoke(receivedRedSheepId, newBehaviourStateIndex);
    }
    
    [ClientRpc]
    public void ChangeTargetPlayerClientRpc(string receivedRedSheepId, ulong targetPlayerObjectId)
    {
        OnChangeTargetPlayer?.Invoke(receivedRedSheepId, targetPlayerObjectId);
    }
    
    [ClientRpc]
    public void EnterDeathStateClientRpc(string receivedRedSheepId)
    {
        OnEnterDeathState?.Invoke(receivedRedSheepId);
    }
    
    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string receivedRedSheepId)
    {
        OnInitializeConfigValues?.Invoke(receivedRedSheepId);
    }
    
    [ClientRpc]
    public void SyncRedSheepIdClientRpc(string receivedRedSheepId)
    {
        OnSyncRedSheepIdentifier?.Invoke(receivedRedSheepId);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}