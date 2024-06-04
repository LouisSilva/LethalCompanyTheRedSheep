using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace LethalCompanyTheRedSheep;

public class StationaryStateBehaviour : StateMachineBehaviour
{
    private ManualLogSource _mls;
    private string _redSheepId;
    
    private TheRedSheepNetcodeController _netcodeController;

    private void OnEnable()
    {
        if (_netcodeController == null) return;
        _netcodeController.OnSyncRedSheepIdentifier += HandleSyncRedSheepIdentifier;
    }

    private void OnDisable()
    {
        if (_netcodeController == null) return;
        _netcodeController.OnSyncRedSheepIdentifier -= HandleSyncRedSheepIdentifier;
    }

    public void Initialize(TheRedSheepNetcodeController receivedNetcodeController)
    {
        _netcodeController = receivedNetcodeController;
        OnEnable();
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_netcodeController == null)
        {
            LogDebug("Netcode Controller is null");
            return;
        }
        
        if (!NetworkManager.Singleton.IsClient || !_netcodeController.IsOwner) return;
        LogDebug("Idle cycle complete");
        _netcodeController.IdleCycleCompleteServerRpc(_redSheepId);
    }
    
    private void HandleSyncRedSheepIdentifier(string receivedRedSheepId)
    {
        _redSheepId = receivedRedSheepId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Stationary State Behaviour {_redSheepId}");
        
        LogDebug("Successfully synced red sheep identifier");
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}