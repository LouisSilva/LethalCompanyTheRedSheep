using System;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;
// ReSharper disable InconsistentNaming

namespace LethalCompanyTheRedSheep;

public class TheRedSheepServer : EnemyAI
{
    private ManualLogSource _mls;
    private string _redSheepId;
    
    public enum States
    {
        Roaming,
        NIdle,
        Transforming,
        TIdle,
        InvestigatingTargetPosition,
        SearchingForPlayers,
        Attacking,
        Dead
    }

    [SerializeField] private AISearchRoutine searchForPlayers;
    
#pragma warning disable 0649
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private Animator normalAnimator;
    [SerializeField] private Animator transformedAnimator;
    [SerializeField] private Transform transformedRedSheepEye;
    [SerializeField] private TheRedSheepNetcodeController netcodeController;
#pragma warning restore 0649
    
    [SerializeField] private float maxSearchRadius = 100f;
    [SerializeField] private float viewWidth = 135f;
    [SerializeField] private int viewRange = 150;
    [SerializeField] private int proximityAwareness = 3;

    private Vector3 _targetPosition;
    
    private float _agentMaxAcceleration;
    private float _agentMaxSpeed;
    private float _takeDamageCooldown;
    private float _transformedIdleTimer;

    private int _idleStateCyclesLeft;

    private void OnEnable()
    {
        netcodeController.OnIdleCycleComplete += HandleIdleCycleComplete;
        netcodeController.OnCompleteTransformation += HandleCompleteTransformation;
    }

    private void OnDisable()
    {
        netcodeController.OnIdleCycleComplete -= HandleIdleCycleComplete;
        netcodeController.OnCompleteTransformation -= HandleCompleteTransformation;
    }

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

        creatureAnimator = normalAnimator;
        netcodeController.SyncRedSheepIdClientRpc(_redSheepId);
        InitializeState((int)States.Roaming);
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

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Roaming:
            {
                break;
            }

            case (int)States.NIdle:
            {
                break;
            }

            case (int)States.Transforming:
            {
                agent.speed = 0f;
                break;
            }
        }
        
        CalculateAgentSpeed();
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
                if (Vector3.Distance(transform.position, _targetPosition) <= 3)
                {
                    // Start to idle for a bit before going to a new place
                    SwitchBehaviourStateLocally(States.NIdle);
                }
                
                break;
            }

            case (int)States.NIdle:
            {
                break;
            }

            case (int)States.TIdle:
            {
                CheckIfPlayerInLosAndTarget();
                
                break;
            }

            case (int)States.InvestigatingTargetPosition:
            {
                if (CheckIfPlayerInLosAndTarget()) break;

                if (Vector3.Distance(transform.position, _targetPosition) <= 1)
                {
                    SwitchBehaviourStateLocally(States.SearchingForPlayers);
                }
                
                break;
            }

            case (int)States.SearchingForPlayers:
            {
                CheckIfPlayerInLosAndTarget();
                
                break;
            }

            case (int)States.Attacking:
            {
                // Check for players in LOS
                PlayerControllerB[] playersInLineOfSight = GetAllPlayersInLineOfSight(viewWidth, viewRange, eye, proximityAwareness,
                    layerMask: StartOfRound.Instance.collidersAndRoomMaskAndDefault);
                
                // Check if our target is in LOS
                bool ourTargetFound = false;
                if (playersInLineOfSight is { Length: > 0 })
                {
                    ourTargetFound = targetPlayer != null && playersInLineOfSight.Any(playerControllerB => playerControllerB == targetPlayer && playerControllerB != null);
                }
                // If no players were found, switch to investigation state
                else
                {
                    SwitchBehaviourStateLocally((int)States.InvestigatingTargetPosition);
                    break;
                }
                
                // If our target wasn't found, switch target to the closest player in los
                if (!ourTargetFound)
                {
                    PlayerControllerB playerInLos =
                        CheckLineOfSightForClosestPlayer(viewWidth, viewRange, proximityAwareness);
                    if (playerInLos == null)
                    {
                        SwitchBehaviourStateLocally(States.InvestigatingTargetPosition);
                        break;
                    }
                    
                    ChangeTargetPlayer(playerInLos);
                    SetMovingTowardsTargetPlayer(playerInLos);
                }

                _targetPosition = targetPlayer.transform.position;
                // todo: if target player is close enough and can see the sheep, increase their fear
                
                // todo: make attack animation stuff
                
                break;
            }
        }
    }

    private bool CheckIfPlayerInLosAndTarget()
    {
        PlayerControllerB seenPlayer = CheckLineOfSightForClosestPlayer(viewWidth, viewRange, proximityAwareness);
        if (seenPlayer != null)
        {
            ChangeTargetPlayer(seenPlayer);
            switch (currentBehaviourStateIndex)
            {
                case (int)States.TIdle or (int)States.InvestigatingTargetPosition or (int)States.SearchingForPlayers:
                {
                    SwitchBehaviourStateLocally(States.Attacking);
                    break;
                }
            }
        }
        else
        {
            if (currentBehaviourStateIndex == (int)States.Attacking) SwitchBehaviourStateLocally(States.InvestigatingTargetPosition);
        }

        return seenPlayer != null;
    }

    private void HandleCompleteTransformation(string receivedRedSheepId)
    {
        if (_redSheepId != receivedRedSheepId) return;

        eye = transformedRedSheepEye;
        creatureAnimator = transformedAnimator;
        SwitchBehaviourStateLocally(States.SearchingForPlayers);
    }

    private void HandleIdleCycleComplete(string receivedRedSheepId)
    {
        if (_redSheepId != receivedRedSheepId) return;
        if (!IsServer) return;
        if (currentBehaviourStateIndex != (int)States.NIdle) return;

        _idleStateCyclesLeft--;
        LogDebug($"There are now {_idleStateCyclesLeft} idle state cycles left");
        if (_idleStateCyclesLeft <= 0)
        {
            switch (currentBehaviourStateIndex)
            {
                case (int)States.NIdle:
                    SwitchBehaviourStateLocally(States.Roaming);
                    break;
                
                case (int)States.TIdle:
                    SwitchBehaviourStateLocally(States.SearchingForPlayers);
                    break;
            }
        }
        else
        {
            PickRandomIdleAnimation();
        }
    }

    private void GoToFarAwayNode(bool random = false)
    {
        int maxOffset = Mathf.Max(1, Mathf.FloorToInt(allAINodes.Length * 0.1f));
        Transform farAwayTransform = random ? ChooseFarthestNodeFromPosition(transform.position, offset: Random.Range(0, maxOffset)) : ChooseFarthestNodeFromPosition(transform.position);
        targetNode = farAwayTransform;
        _targetPosition = farAwayTransform.position;
        
        if (!SetDestinationToPosition(farAwayTransform.position, true))
        {
            _mls.LogWarning("This should not happen");
        }
    }

    private void PickRandomIdleAnimation()
    {
        if (!IsServer) return;

        int animationToPlay = Random.Range(1, 4);
        int animationIdToPlay = animationToPlay switch
        {
            1 => TheRedSheepClient.Idle1,
            2 => TheRedSheepClient.Idle2,
            3 => TheRedSheepClient.Idle3,
            _ => 0,
        };

        if (animationIdToPlay == 0)
        {
            LogDebug($"Unable to play animation with random number: {animationToPlay}");
            return;
        }
        
        LogDebug($"Playing animation with id: ({animationToPlay}, {animationIdToPlay})");
        netcodeController.DoAnimationClientRpc(_redSheepId, animationIdToPlay);
    }
    
    public override void FinishedCurrentSearchRoutine()
    {
        base.FinishedCurrentSearchRoutine();
        if (!IsServer) return;
        if (searchForPlayers.inProgress)
            searchForPlayers.searchWidth = Mathf.Clamp(searchForPlayers.searchWidth + 10f, 1f, maxSearchRadius);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false,
        int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer) return;
        if (isEnemyDead || currentBehaviourStateIndex == (int)States.Dead) return;
        if (_takeDamageCooldown > 0) return;

        //enemyHP -= force;
        _takeDamageCooldown = 0.05f;
        ChangeTargetPlayer(playerWhoHit);
        if (enemyHP > 0)
        {
            switch (currentBehaviourStateIndex)
            {
                case (int)States.Roaming or (int)States.NIdle:
                {
                    SwitchBehaviourStateLocally(States.Transforming);
                    break;
                }

                case (int)States.TIdle or (int)States.SearchingForPlayers:
                {
                    SwitchBehaviourStateLocally(States.Attacking);
                    break;
                }
            }
        }
        else
        {
            netcodeController.EnterDeathStateClientRpc(_redSheepId);
            KillEnemyClientRpc(false);
            SwitchBehaviourStateLocally(States.Dead);
        }
    }

    private void ChangeTargetPlayer(PlayerControllerB newTargetPlayer)
    {
        if (newTargetPlayer == null) return;
        if (newTargetPlayer == targetPlayer) return;
        targetPlayer = newTargetPlayer;
        netcodeController.ChangeTargetPlayerClientRpc(_redSheepId, newTargetPlayer.playerClientId);
    }

    /// <summary>
    /// Resets the required variables and runs setup functions for each particular behaviour state
    /// </summary>
    /// <param name="state">The state to switch to</param>
    private void InitializeState(int state)
    {
        if (!IsServer) return;
        LogDebug($"Initializing state: {state}");
        switch (state)
        {
            case (int)States.Roaming:
            {
                _agentMaxSpeed = 2f; // TODO: Make configurable
                _agentMaxAcceleration = 10f; // TODO: Make configurable
                
                // Pick first node to go to
                GoToFarAwayNode(true);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsWalking, true);
                
                break;
            }

            case (int)States.NIdle:
            {
                _agentMaxAcceleration = 10f;
                _agentMaxSpeed = 0f;
                agent.speed = 0f;
                agent.acceleration = 0f;
                moveTowardsDestination = false;
                _idleStateCyclesLeft = Random.Range(1, 4);
                
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsWalking, false);
                PickRandomIdleAnimation();
                
                break;
            }

            case (int)States.Transforming:
            {
                _agentMaxAcceleration = 0f;
                _agentMaxSpeed = 0f;
                agent.speed = 0f;
                agent.acceleration = 0f;
                moveTowardsDestination = false;

                netcodeController.StartTransformationClientRpc(_redSheepId);
                
                break;
            }

            case (int)States.TIdle:
            {
                _agentMaxAcceleration = 10f;
                _agentMaxSpeed = 0f;
                agent.speed = 0f;
                agent.acceleration = 0f;
                moveTowardsDestination = false;
                _idleStateCyclesLeft = Random.Range(1, 2);
                
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsWalking, false);
                PickRandomIdleAnimation();
                
                break;
            }

            case (int)States.SearchingForPlayers:
            {
                _agentMaxSpeed = 3.5f; // TODO: Make configurable
                _agentMaxAcceleration = 10f; // TODO: Make configurable

                Vector3 startSearchPosition = _targetPosition != default && _targetPosition != Vector3.zero ? _targetPosition : transform.position;
                StartSearch(startSearchPosition, searchForPlayers);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsWalking, true);
                
                break;
            }

            case (int)States.InvestigatingTargetPosition:
            {
                _agentMaxSpeed = 4f;
                _agentMaxAcceleration = 15f;
                
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsWalking, true);
                
                break;
            }

            case (int)States.Attacking:
            {
                _agentMaxSpeed = 4.5f;
                _agentMaxAcceleration = 20f;
                
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsWalking, false);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsRunning, true);
                
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
        if (stunNormalizedTimer > 0 || currentBehaviourStateIndex == (int)States.Transforming)
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