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
    private string _redSheepId;
    
    private enum States
    {
        Roaming,
        Transforming,
        Transformed,
        Dead
    }
    
#pragma warning disable 0649

    [Header("Controllers")] [Space(5f)] [SerializeField]
    private TheRedSheepNetcodeController netcodeController;
#pragma warning restore 0649
    
    [SerializeField] private float maxRoamingRadius = 100f;
    [SerializeField] private float viewWidth = 135f;
    [SerializeField] private int viewRange = 150;
    [SerializeField] private int proximityAwareness = 3;
    
    private float _agentMaxAcceleration;
    private float _agentMaxSpeed;
    private float _takeDamageCooldown;

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        _redSheepId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Server {_redSheepId}");
        
        // Initialize the random function and config values
        Random.InitState(StartOfRound.Instance.randomMapSeed + _redSheepId.GetHashCode());
        InitializeConfigValues();
        
        netcodeController.SyncRedSheepIdClientRpc(_redSheepId);
        SwitchBehaviourStateLocally(States.Roaming);
        LogDebug("Red sheep spawned");
    }

    /// <summary>
    /// This function is called every frame
    /// </summary>
    public override void Update()
    {
        base.Update();
        if (!IsServer) return;

        _takeDamageCooldown -= Time.deltaTime;
        CalculateAgentSpeed();

        switch (currentBehaviourStateIndex)
        {
            
        }
    }

    /// <summary>
    /// Handles most of the main AI logic
    /// The logic in this method is not run every frame
    /// </summary>
    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!IsServer) return;
        if (StartOfRound.Instance.livingPlayers == 0 || isEnemyDead || currentBehaviourStateIndex == (int)States.Dead) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Roaming:
            {
                // Check if the sheep has reached its destination
                if (Vector3.Distance(transform.position, targetNode.position) <= 3)
                {
                    // Pick new destination
                    ChooseFarAwayNode(true);
                }
                
                break;
            }
        }
    }

    private void ChooseFarAwayNode(bool random = false)
    {
        int maxOffset = Mathf.Max(1, Mathf.FloorToInt(allAINodes.Length * 0.1f));
        Transform farAwayTransform = random ? ChooseFarthestNodeFromPosition(transform.position, offset: Random.Range(0, maxOffset)) : ChooseFarthestNodeFromPosition(transform.position);
        targetNode = farAwayTransform;
        
        if (!SetDestinationToPosition(farAwayTransform.position, true))
        {
            _mls.LogWarning("This should not happen");
        }
    }

    /// <summary>
    /// Resets the required variables and runs setup functions for each particular behaviour state
    /// </summary>
    /// <param name="state">The state to switch to</param>
    private void InitializeState(int state)
    {
        if (!IsServer) return;
        switch (state)
        {
            case (int)States.Roaming:
            {
                _agentMaxSpeed = 2f; // TODO: Make configurable
                _agentMaxAcceleration = 10f; // TODO: Make configurable
                
                // Pick first node to go to
                ChooseFarAwayNode(true);

                break;
            }
        }
    }
    
    /// <summary>
    /// Switches to the given behaviour state represented by the state enum
    /// </summary>
    /// <param name="state">The state enum to change to</param>
    private void SwitchBehaviourStateLocally(States state)
    {
        SwitchBehaviourStateLocally((int)state);
    }
    
    /// <summary>
    /// Switches to the given behaviour state represented by an integer
    /// </summary>
    /// <param name="state">The state integer to change to</param>
    private void SwitchBehaviourStateLocally(int state)
    {
        if (!IsServer || currentBehaviourStateIndex == state) return;
        LogDebug($"Switched to behaviour state {state}!");
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
        InitializeState(state);
        netcodeController.ChangeBehaviourStateClientRpc(_redSheepId, state);
        LogDebug($"Switch to behaviour state {state} complete!");
    }
    
    /// <summary>
    /// Calculates the agents speed depending on whether the sheep is stunned/dead/not dead
    /// </summary>
    private void CalculateAgentSpeed()
    {
        if (!IsServer) return;
        if (stunNormalizedTimer > 0)
        {
            agent.speed = 0;
            agent.acceleration = _agentMaxAcceleration;
            return;
        }

        if (currentBehaviourStateIndex != (int)States.Dead)
        {
            MoveWithAcceleration();
        }
    }
    
    /// <summary>
    /// Makes the agent move by using interpolation to make the movement smooth
    /// </summary>
    private void MoveWithAcceleration()
    {
        if (!IsServer) return;
        
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, _agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, _agentMaxAcceleration, accelerationAdjustment);
    }
    
    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    private void InitializeConfigValues()
    {
        netcodeController.InitializeConfigValuesClientRpc(_redSheepId);
    }
    
    /// <summary>
    /// Only logs the given message if the assembly version is in debug, not release
    /// </summary>
    /// <param name="msg">The debug message to log</param>
    private void LogDebug(string msg)
    {
        #if DEBUG
        if (!IsServer) return;
        _mls?.LogInfo(msg);
        #endif
    }
}