using System;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;
// ReSharper disable InconsistentNaming

namespace LethalCompanyTheRedSheep;

public class TheRedSheepServer : EnemyAI
{
    private ManualLogSource _mls;
    private string _redSheepId;

    private enum NoiseIDsToAnnoy
    {
        PlayersTalking = 75,
        ShipHorn = 14155,
        Boombox = 5,
        RadarBoosterPing = 1015,
        Jetpack = 41,
    }

    private enum NoiseIDsToIgnore
    {
        DoubleWing = 911,
        Lightning = 11,
        DocileLocustBees = 14152,
        BaboonHawkCaw = 1105
    }
    
    public enum States
    {
        Roaming,
        NormalIdle,
        Transforming,
        TransformedIdle,
        SearchingForPlayers,
        InvestigatingTargetPosition,
        Attacking,
        Dead
    }
    
#pragma warning disable 0649
    [SerializeField] private AISearchRoutine searchForPlayers;
    
    [Header("Transformed Sheep Components")] [Space(5f)]
    [SerializeField] private Animator normalAnimator;
    [SerializeField] private Animator transformedAnimator;
    [SerializeField] private Transform transformedEye;
    
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private TheRedSheepNetcodeController netcodeController;
#pragma warning restore 0649
    
    [SerializeField] private float maxSearchRadius = 100f;
    [SerializeField] private float viewWidth = 135f;
    [SerializeField] private int viewRange = 80;
    [SerializeField] private int proximityAwareness = 3;
    [SerializeField] private float annoyanceDecayRate = 0.35f;
    [SerializeField] private float annoyanceThreshold = 8f;
    [SerializeField] private float noiseAnnoyanceMultiplier = 0.5f;
    [SerializeField] private float proximityAnnoyanceMultiplier = 0.05f;
    [SerializeField] private float proximityAnnoyanceThreshold = 10f;
    [SerializeField] private bool canHearPlayers = true;
    [SerializeField] private float hearingPrecision = 90f;

    private Vector3 _targetPosition;
    private Vector3 _positionWhenTransforming;
    
    private float _agentMaxAcceleration;
    private float _agentMaxSpeed;
    private float _takeDamageCooldown;
    private float _hearNoiseCooldown;
    private float _annoyanceLevel;
    private float _transformedIdleTimer;

    private int _idleStateCyclesLeft;

    private bool _finishingSearch;

    private void OnEnable()
    {
        netcodeController.OnIdleCompleteStateBehaviourCallback += HandleIdleCompleteStateBehaviourCallback;
        netcodeController.OnCompleteTransformation += HandleCompleteTransformation;
    }

    private void OnDisable()
    {
        netcodeController.OnIdleCompleteStateBehaviourCallback -= HandleIdleCompleteStateBehaviourCallback;
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
        netcodeController.SyncRedSheepIdClientRpc(_redSheepId);
        InitializeConfigValues();
        
        creatureAnimator = normalAnimator;
        allAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
        
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
        
        CalculateAgentSpeed();

        _takeDamageCooldown -= Time.deltaTime;
        _hearNoiseCooldown -= Time.deltaTime;

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Transforming:
            {
                transform.position = _positionWhenTransforming;
                break;
            }

            case (int)States.SearchingForPlayers:
            {
                _transformedIdleTimer -= Time.deltaTime;
                
                break;
            }
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
                CalculateProximityAnnoyance();
                if (CalculateTotalAnnoyanceLevel()) break;

                if (TimeOfDay.Instance.normalizedTimeOfDay >= 0.625)
                {
                    SwitchBehaviourStateLocally(States.Transforming);
                    break;
                }
                
                // Check if the sheep has reached its destination
                if (Vector3.Distance(transform.position, _targetPosition) <= 1)
                {
                    // Start to idle for a bit before going to a new place
                    SwitchBehaviourStateLocally(States.NormalIdle);
                    break;
                }
                
                break;
            }

            case (int)States.NormalIdle:
            {
                CalculateProximityAnnoyance();
                if (CalculateTotalAnnoyanceLevel()) break;
                
                if (TimeOfDay.Instance.normalizedTimeOfDay >= 0.625)
                {
                    SwitchBehaviourStateLocally(States.Transforming);
                    break;
                }
                
                break;
            }
            
            case (int)States.SearchingForPlayers:
            {
                if (CheckIfPlayerInLosAndTarget()) break;

                if (_transformedIdleTimer <= 0 && !_finishingSearch)
                {
                    StopSearch(searchForPlayers);
                    _targetPosition = ChooseClosestNodeToPosition(transform.position).position;
                    SetDestinationToPosition(_targetPosition);
                    _finishingSearch = true;
                }
                else if (_finishingSearch)
                {
                    if (Vector3.Distance(transform.position, _targetPosition) <= 1f)
                    {
                        SwitchBehaviourStateLocally(States.TransformedIdle);
                    }
                }
                
                break;
            }
            
            case (int)States.TransformedIdle:
            {
                if (CheckIfPlayerInLosAndTarget()) break;
                
                break;
            }

            case (int)States.InvestigatingTargetPosition:
            {
                if (CheckIfPlayerInLosAndTarget()) break;

                if (Vector3.Distance(transform.position, _targetPosition) <= 1f)
                {
                    SwitchBehaviourStateLocally(States.SearchingForPlayers);
                }
                
                break;
            }

            case (int)States.Attacking:
            {
                // Check for players in LOS
                PlayerControllerB[] playersInLineOfSight = GetAllPlayersInLineOfSight(viewWidth, viewRange, eye, proximityAwareness,
                    layerMask: StartOfRound.Instance.collidersAndRoomMaskAndDefault);
                
                // Check if our target is in LOS
                bool ourTargetFound;
                if (playersInLineOfSight is { Length: > 0 })
                {
                    ourTargetFound = targetPlayer != null && playersInLineOfSight.Any(playerControllerB => playerControllerB == targetPlayer && playerControllerB != null);
                    LogDebug($"Our target found?: {ourTargetFound}");
                }
                // If no players were found, switch to investigation state
                else
                {
                    LogDebug("In attacking state, no players were found. Switching to InvestigatingTargetPosition");
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
                }

                LogDebug("Moving towards target player");
                SetMovingTowardsTargetPlayer(targetPlayer);
                moveTowardsDestination = true;
                _targetPosition = targetPlayer.transform.position;
                netcodeController.IncreaseTargetPlayerFearLevelClientRpc(_redSheepId);
                
                // todo: make attack animation stuff
                
                break;
            }
        }
    }
    
    private bool CalculateTotalAnnoyanceLevel()
    {
        if (_annoyanceLevel > 0)
        {
            _annoyanceLevel -= annoyanceDecayRate * Time.deltaTime;
            _annoyanceLevel = Mathf.Clamp(_annoyanceLevel, 0, Mathf.Infinity);
        }

        if (!(_annoyanceLevel >= annoyanceThreshold)) return false;
        SwitchBehaviourStateLocally(States.Transforming);
        return true;
    }

    private void CalculateProximityAnnoyance()
    {
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (!IsPlayerTargetable(player)) continue;

            float distanceToSheep = Vector3.Distance(transform.position, player.transform.position);
            if (distanceToSheep > proximityAnnoyanceThreshold) continue;

            float proximityAnnoyance = proximityAnnoyanceMultiplier / distanceToSheep;
            _annoyanceLevel += proximityAnnoyance;
            //LogDebug($"{player.playerUsername} is {distanceToSheep} meters away, adding {proximityAnnoyance} annoyance. New annoyance level: {_annoyanceLevel}");
        }
    }
    
    /// <summary>
    /// Returns whether a player is targetable.
    /// This method is a simplified version of Zeeker's function, it's a bit doo doo.
    /// </summary>
    /// <param name="player">The player to check whether they are targetable</param>
    /// <returns>Whether the target player is targetable</returns>
    private static bool IsPlayerTargetable(PlayerControllerB player)
    {
        if (player == null) return false;
        return !player.isPlayerDead &&
               !player.isInsideFactory &&
               player.isPlayerControlled &&
               !(player.sinkingValue >= 0.7300000190734863);
    }


    private bool CheckIfPlayerInLosAndTarget()
    {
        PlayerControllerB seenPlayer = CheckLineOfSightForClosestPlayer(viewWidth, viewRange, proximityAwareness);
        if (seenPlayer != null)
        {
            ChangeTargetPlayer(seenPlayer);
            switch (currentBehaviourStateIndex)
            {
                case (int)States.TransformedIdle or (int)States.InvestigatingTargetPosition or (int)States.SearchingForPlayers:
                {
                    LogDebug($"Player {seenPlayer.playerUsername} was spotted, now targeting ");
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

        eye = transformedEye;
        creatureAnimator = transformedAnimator;
        agent.radius = 2.4f;
        agent.height = 6.85f;
        SwitchBehaviourStateLocally(States.SearchingForPlayers);
    }

    private void HandleIdleCompleteStateBehaviourCallback(string receivedRedSheepId)
    {
        if (_redSheepId != receivedRedSheepId) return;
        if (!IsServer) return;

        _idleStateCyclesLeft -= 1;
        LogDebug($"There are now {_idleStateCyclesLeft} idle state cycles left");
        if (_idleStateCyclesLeft <= 0)
        {
            switch (currentBehaviourStateIndex)
            {
                case (int)States.NormalIdle:
                    SwitchBehaviourStateLocally(States.Roaming);
                    break;
                
                case (int)States.TransformedIdle:
                    SwitchBehaviourStateLocally(States.SearchingForPlayers);
                    break;
            }
        }
        else
        {
            PickRandomIdleAnimation();
        }
    }

    private void GoToRandomNode(float aiNodesLengthMultiplier = 1f)
    {
        aiNodesLengthMultiplier = Mathf.Clamp(aiNodesLengthMultiplier, 0.1f, 1f);
        Transform farAwayTransform = ChooseFarthestNodeFromPosition(transform.position, offset: Random.Range(0, Mathf.Max(1, Mathf.FloorToInt(allAINodes.Length * aiNodesLengthMultiplier))));
        _targetPosition = farAwayTransform.position;
        
        if (!SetDestinationToPosition(farAwayTransform.position, true))
        {
            _mls.LogWarning("This should not happen");
        }
        else LogDebug($"Set destination to: {farAwayTransform.position}");

        moveTowardsDestination = true;
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

    public override void DetectNoise(
        Vector3 noisePosition,
        float noiseLoudness,
        int timesNoisePlayedInOneSpot = 0,
        int noiseID = 0)
    {
        base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
        if (!IsServer) return;
        if (!canHearPlayers) return;
        if ((double)stunNormalizedTimer > 0 || _hearNoiseCooldown > 0.0 || Enum.IsDefined(typeof(NoiseIDsToIgnore), noiseID)) return;
        
        switch (currentBehaviourStateIndex)
        {
            case (int)States.NormalIdle or (int)States.Roaming:
            {
                _hearNoiseCooldown = 0.01f;
                float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
                float noiseThreshold = 15f * noiseLoudness;
                LogDebug($"Heard noise id {noiseID} from {distanceToNoise} meters away | Noise loudness: {noiseLoudness}");

                if (Physics.Linecast(transform.position, noisePosition, 256))
                {
                    noiseLoudness /= 1.5f;
                    noiseThreshold /= 1.5f;
                }
                
                if (noiseLoudness < 0.25f || distanceToNoise > noiseThreshold) return;
                if (Enum.IsDefined(typeof(NoiseIDsToAnnoy), noiseID)) noiseLoudness *= 1.5f;

                float normalizedDistance = distanceToNoise / noiseThreshold;
                _annoyanceLevel += noiseLoudness * normalizedDistance * noiseAnnoyanceMultiplier;
                LogDebug($"Annoyance level: {_annoyanceLevel}");
                break;
            }

            case (int)States.SearchingForPlayers:
            {
                if (timesNoisePlayedInOneSpot > 10) return;
                _hearNoiseCooldown = 0.1f;
                float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
                float noiseThreshold = 8f * noiseLoudness;
                LogDebug($"Heard noise from {distanceToNoise} meters away | Noise loudness: {noiseLoudness}");
                
                if (Physics.Linecast(transform.position, noisePosition, 256))
                {
                    noiseLoudness /= 2f;
                    noiseThreshold /= 2f;
                }

                if (noiseLoudness < 0.25 || distanceToNoise >= noiseThreshold) return;

                float adjustedRadius = Mathf.Clamp(distanceToNoise * (1f - hearingPrecision / 100f), 0.01f, 50f);
                LogDebug($"Adjusted radius {adjustedRadius}");
                _targetPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(noisePosition, adjustedRadius);
                SwitchBehaviourStateLocally(States.InvestigatingTargetPosition);
                break;
            }
        }
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
                case (int)States.Roaming or (int)States.NormalIdle:
                {
                    SwitchBehaviourStateLocally(States.Transforming);
                    break;
                }

                case (int)States.TransformedIdle or (int)States.SearchingForPlayers or (int)States.InvestigatingTargetPosition:
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
        LogDebug($"Target player is now: {newTargetPlayer.playerUsername}");
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
                _agentMaxSpeed = 1.75f; // TODO: Make configurable
                _agentMaxAcceleration = 10f; // TODO: Make configurable
                movingTowardsTargetPlayer = false;
                _targetPosition = default;
                
                // Pick first node to go to
                GoToRandomNode();
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsWalking, true);
                netcodeController.DoAnimationClientRpc(_redSheepId, TheRedSheepClient.ForceWalk);
                
                break;
            }

            case (int)States.NormalIdle:
            {
                _agentMaxAcceleration = 10f;
                _agentMaxSpeed = 0f;
                moveTowardsDestination = false;
                movingTowardsTargetPlayer = false;
                _targetPosition = default;
                _idleStateCyclesLeft = Random.Range(1, 4);
                
                netcodeController.ChangeAnimationParameterBoolClientRpc(_redSheepId, TheRedSheepClient.IsWalking, false);
                PickRandomIdleAnimation();
                
                break;
            }

            case (int)States.Transforming:
            {
                _agentMaxAcceleration = 0f;
                _agentMaxSpeed = 0f;
                moveTowardsDestination = false;
                movingTowardsTargetPlayer = false;
                _targetPosition = default;
                _positionWhenTransforming = transform.position;

                netcodeController.StartTransformationClientRpc(_redSheepId);
                
                break;
            }

            case (int)States.SearchingForPlayers:
            {
                _agentMaxSpeed = 3.5f; // TODO: Make configurable
                _agentMaxAcceleration = 10f; // TODO: Make configurable
                _transformedIdleTimer = Random.Range(15f, 75f);
                _finishingSearch = false;
                movingTowardsTargetPlayer = false;
                moveTowardsDestination = true;
                
                Vector3 startSearchPosition = _targetPosition != default && _targetPosition != Vector3.zero ? _targetPosition : transform.position;
                searchForPlayers.searchWidth = currentBehaviourStateIndex == (int)States.InvestigatingTargetPosition
                    ? 10f
                    : maxSearchRadius;
                
                StartSearch(startSearchPosition, searchForPlayers);
                netcodeController.DoAnimationClientRpc(_redSheepId, TheRedSheepClient.ForceWalk);
                
                break;
            }
            
            case (int)States.TransformedIdle:
            {
                _agentMaxAcceleration = 10f;
                _agentMaxSpeed = 0f;
                movingTowardsTargetPlayer = false;
                moveTowardsDestination = false;
                _idleStateCyclesLeft = Random.Range(1, 2);
                
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                PickRandomIdleAnimation();
                
                break;
            }

            case (int)States.InvestigatingTargetPosition:
            {
                _agentMaxSpeed = 6f;
                _agentMaxAcceleration = 15f;
                moveTowardsDestination = true;
                movingTowardsTargetPlayer = false;
                
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (_targetPosition == default)
                {
                    SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                    break;
                }

                if (!SetDestinationToPosition(_targetPosition, true))
                {
                    LogDebug("Unable to set destination in InitializeState for InvestigatingTargetPosition");
                    SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                    break;
                }
                
                break;
            }

            case (int)States.Attacking:
            {
                _agentMaxSpeed = 8f;
                _agentMaxAcceleration = 20f;
                _targetPosition = default;
                movingTowardsTargetPlayer = true;
                moveTowardsDestination = true;
                
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                
                break;
            }

            case (int)States.Dead:
            {
                _agentMaxSpeed = 0;
                _agentMaxAcceleration = 0;
                movingTowardsTargetPlayer = false;
                moveTowardsDestination = false;
                agent.speed = 0;
                agent.acceleration = 0;
                agent.enabled = false;
                isEnemyDead = true;
                
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
        LogDebug($"Switching to behaviour state {state}");
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
        netcodeController.ChangeBehaviourStateClientRpc(_redSheepId, state);
        InitializeState(state);
    }
    
    /// <summary>
    /// Calculates the agents speed depending on whether the sheep is stunned/dead/not dead
    /// </summary>
    private void CalculateAgentSpeed()
    {
        if (!IsServer) return;
        if (currentBehaviourStateIndex != (int)States.Dead) return;
        
        if (stunNormalizedTimer > 0 || 
            currentBehaviourStateIndex == (int)States.Transforming ||
            currentBehaviourStateIndex == (int)States.NormalIdle ||
            currentBehaviourStateIndex == (int)States.TransformedIdle)
        {
            agent.speed = 0;
            agent.acceleration = _agentMaxAcceleration;
        }
        else
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
        float newSpeed = Mathf.Lerp(agent.speed, _agentMaxSpeed, speedAdjustment);
        LogDebug($"New speed: {newSpeed}. Speed before update: {agent.speed}");
        agent.speed = newSpeed;
        LogDebug($"Speed after update: {agent.speed}");
        
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