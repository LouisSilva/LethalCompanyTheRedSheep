using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace LethalCompanyTheRedSheep;

public class StationaryStateBehaviour : StateMachineBehaviour
{
    private ManualLogSource _mls;
    private string _redSheepId;
    
#pragma warning disable 0649
    [SerializeField] private TheRedSheepNetcodeController netcodeController;
#pragma warning restore 0649

    private void OnEnable()
    {
        netcodeController.OnSyncRedSheepIdentifier += HandleSyncRedSheepIdentifier;
    }

    private void OnDisable()
    {
        netcodeController.OnSyncRedSheepIdentifier -= HandleSyncRedSheepIdentifier;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!NetworkManager.Singleton.IsClient || !netcodeController.IsOwner) return;
        netcodeController.IdleCycleCompleteServerRpc(_redSheepId);
    }
    
    private void HandleSyncRedSheepIdentifier(string receivedRedSheepId)
    {
        _redSheepId = receivedRedSheepId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Client {_redSheepId}");
        
        LogDebug("Successfully synced red sheep identifier");
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}