using System;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace LethalCompanyTheRedSheep.CustomStateMachineBehaviours;

public abstract class BaseStateMachineBehaviour : StateMachineBehaviour
{
    private ManualLogSource _mls;
    protected string RedSheepId;
    
    protected TheRedSheepNetcodeController NetcodeController;
    protected TheRedSheepClient SheepClient;

    protected void OnEnable()
    {
        if (NetcodeController == null) return;
        NetcodeController.OnSyncRedSheepIdentifier += HandleSyncRedSheepIdentifier;
    }

    protected void OnDisable()
    {
        if (NetcodeController == null) return;
        NetcodeController.OnSyncRedSheepIdentifier -= HandleSyncRedSheepIdentifier;
    }

    public void Initialize(TheRedSheepNetcodeController receivedNetcodeController, TheRedSheepClient receivedSheepClient)
    {
        NetcodeController = receivedNetcodeController;
        SheepClient = receivedSheepClient;
        OnEnable();
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        throw new NotImplementedException("OnStateEnter is not implemented.");
    }

    private void HandleSyncRedSheepIdentifier(string receivedRedSheepId)
    {
        RedSheepId = receivedRedSheepId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Stationary State Behaviour {RedSheepId}");
        
        LogDebug("Successfully synced red sheep identifier");
    }
    
    protected void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}