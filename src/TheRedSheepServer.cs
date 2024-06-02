using System;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

namespace LethalCompanyTheRedSheep;

public class TheRedSheepServer : EnemyAI
{
    private ManualLogSource _mls;
    private string _theRedSheepId;
    
#pragma warning disable 0649

    [Header("Controllers")] [Space(5f)] [SerializeField]
    private TheRedSheepNetcodeController netcodeController;
#pragma warning restore 0649

    private void InitializeConfigValues()
    {
        
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        _theRedSheepId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | Vile Vending Machine Server {_theRedSheepId}");
        
        // Initialize the random function and config values
        Random.InitState(StartOfRound.Instance.randomMapSeed + _theRedSheepId.GetHashCode());
        InitializeConfigValues();
        
        netcodeController.SyncRedSheepIdClientRpc(_theRedSheepId);
        LogDebug("Red sheep spawned");
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        if (!IsServer) return;
        _mls?.LogInfo(msg);
        #endif
    }
}