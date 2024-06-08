using BepInEx.Logging;
using System;
using LethalCompanyTheRedSheep.CustomStateMachineBehaviours;
using Unity.Netcode;
using UnityEngine;
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
    public event Action<string> OnIdleCycleComplete;
    public event Action<string> OnStartTransformation;
    public event Action<string> OnCompleteTransformation;
    
    private void Awake()
    {
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Netcode Controller");
    }

    [ClientRpc]
    public void StartTransformationClientRpc(string receivedRedSheepId)
    {
        OnStartTransformation?.Invoke(receivedRedSheepId);
    }
    
    [ServerRpc]
    public void CompleteTransformationServerRpc(string receivedRedSheepId)
    {
        OnCompleteTransformation?.Invoke(receivedRedSheepId);
    }
    
    [ServerRpc (RequireOwnership = false)]
    public void IdleCycleCompleteServerRpc(string receivedRedSheepId)
    {
        OnIdleCycleComplete?.Invoke(receivedRedSheepId);
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
    
    [ServerRpc(RequireOwnership = false)]
    public void ChangeBehaviourStateServerRpc(string receivedRedSheepId, int newBehaviourStateIndex)
    {
        ChangeBehaviourStateClientRpc(receivedRedSheepId, newBehaviourStateIndex);
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