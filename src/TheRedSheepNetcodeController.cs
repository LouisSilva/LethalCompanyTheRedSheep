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
    
    private void Start()
    {
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Netcode Controller");
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