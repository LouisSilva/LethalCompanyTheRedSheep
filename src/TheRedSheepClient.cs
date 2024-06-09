using System;
using System.Collections;
using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyTheRedSheep.CustomStateMachineBehaviours;
using Unity.Netcode;
using Logger = BepInEx.Logging.Logger;
using UnityEngine;

namespace LethalCompanyTheRedSheep;

public class TheRedSheepClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _redSheepId;

    public static readonly int IsWalking = Animator.StringToHash("Walking");
    public static readonly int IsRunning = Animator.StringToHash("Running");
    public static readonly int StartTransformation = Animator.StringToHash("Start Transformation");
    public static readonly int EndTransformation = Animator.StringToHash("End Transformation");
    public static readonly int Idle1 = Animator.StringToHash("Idle1");
    public static readonly int Idle2 = Animator.StringToHash("Idle2");
    public static readonly int Idle3 = Animator.StringToHash("Idle3");
    public static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");
    public static readonly int RunSpeed = Animator.StringToHash("RunSpeed");
    
#pragma warning disable 0649
    [Header("Models")] [Space(5f)]
    [SerializeField] private GameObject normalRedSheepModel;
    [SerializeField] private GameObject transformedRedSheepModel;
    [SerializeField] private GameObject transformedRedSheepEye;

    [Header("Audio Sources")] [Space(5f)] 
    [SerializeField] private AudioSource creatureVoice;
    
    [Header("Controllers")] [Space(5f)]
    [SerializeField] private Animator normalAnimator;
    [SerializeField] private Animator transformedAnimator;
    [SerializeField] private TheRedSheepNetcodeController netcodeController;
#pragma warning restore 0649
    
    [SerializeField] private float walkSpeedThreshold = 4.0f;
    [SerializeField] private float maxWalkAnimationSpeedMultiplier = 1.25f;

    private Animator _currentAnimator;

    private PlayerControllerB _targetPlayer;

    private Vector3 _agentLastPosition;
    
    private int _currentBehaviourStateIndex;

    private float _agentCurrentSpeed;
    

    private void OnEnable()
    {
        netcodeController.OnSyncRedSheepIdentifier += HandleSyncRedSheepIdentifier;
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
        netcodeController.OnChangeBehaviourState += HandleChangeBehaviourStateIndex;
        netcodeController.OnChangeTargetPlayer += HandleChangeTargetPlayer;
        netcodeController.OnEnterDeathState += HandleEnterDeathState;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnStartTransformation += HandleStartTransformation;
        netcodeController.OnIncreaseTargetPlayerFearLevel += HandleIncreaseTargetPlayerFearLevel;
    }

    private void OnDisable()
    {
        netcodeController.OnSyncRedSheepIdentifier -= HandleSyncRedSheepIdentifier;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
        netcodeController.OnChangeBehaviourState -= HandleChangeBehaviourStateIndex;
        netcodeController.OnChangeTargetPlayer -= HandleChangeTargetPlayer;
        netcodeController.OnEnterDeathState -= HandleEnterDeathState;
        netcodeController.OnChangeAnimationParameterBool -= SetBool;
        netcodeController.OnDoAnimation -= SetTrigger;
        netcodeController.OnStartTransformation -= HandleStartTransformation;
        netcodeController.OnIncreaseTargetPlayerFearLevel -= HandleIncreaseTargetPlayerFearLevel;
    }

    private void Start()
    {
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Client {_redSheepId}");
        
        normalRedSheepModel.gameObject.SetActive(true);
        transformedRedSheepModel.gameObject.SetActive(false);

        creatureVoice.gameObject.transform.position = new Vector3(1.538f, 2.446f, 0.067f);

        _currentAnimator = normalAnimator;
        AddStateMachineBehaviours(normalAnimator);
        AddStateMachineBehaviours(transformedAnimator);
    }

    private void Update()
    {
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
        
        switch (_currentBehaviourStateIndex)
        {
            case (int)TheRedSheepServer.States.SearchingForPlayers
                or (int)TheRedSheepServer.States.InvestigatingTargetPosition or (int)TheRedSheepServer.States.Attacking:
            {
                if (_agentCurrentSpeed <= walkSpeedThreshold && _agentCurrentSpeed > 0)
                {
                    SetBool(_redSheepId, IsWalking, true);
                    SetBool(_redSheepId, IsRunning, false);

                    float walkSpeedMultiplier = Mathf.Clamp(_agentCurrentSpeed / walkSpeedThreshold, 0,
                        maxWalkAnimationSpeedMultiplier);
                    SetFloat(_redSheepId, WalkSpeed, walkSpeedMultiplier);
                }
                else if (_agentCurrentSpeed > walkSpeedThreshold)
                {
                    SetBool(_redSheepId, IsRunning, true);

                    float runSpeedMultiplier = Mathf.Clamp(_agentCurrentSpeed / 4f, 0, 5);
                    SetFloat(_redSheepId, RunSpeed, runSpeedMultiplier);
                }
                else
                {
                    SetBool(_redSheepId, IsWalking, false);
                    SetBool(_redSheepId, IsRunning, false);
                }
                
                break;
            }

            case (int)TheRedSheepServer.States.Roaming:
            {
                if (_agentCurrentSpeed > 0)
                {
                    SetBool(_redSheepId, IsWalking, true);

                    float walkSpeedMultiplier = Mathf.Clamp(_agentCurrentSpeed / 1.5f, 0, 5);
                    SetFloat(_redSheepId, WalkSpeed, walkSpeedMultiplier);
                }
                
                break;
            }
        }
    }

    public void HandleStartTransformation(string receivedRedSheepId)
    {
        if (_redSheepId != receivedRedSheepId) return;
        StartCoroutine(TransformationProcedure()); 
    }

    private IEnumerator TransformationProcedure()
    {
        LogDebug("In TransformationProcedure");
        const float startAnimationDuration = 2.2f;
        const float endAnimationDuration = 3.2f;
        
        SetTrigger(_redSheepId, StartTransformation);
        SetBool(_redSheepId, IsWalking, false);
        yield return new WaitForSeconds(startAnimationDuration + 0.2f);
        
        // Todo: add smoke effect and thing that pushes players + enemies back if they are close
        
        yield return new WaitForSeconds(0.5f);
        Destroy(normalRedSheepModel.gameObject);
        transformedRedSheepModel.gameObject.SetActive(true);
        creatureVoice.gameObject.transform.position = new Vector3(0.784f, 2.802f, -0.024f);
        _currentAnimator = transformedAnimator;
        SetTrigger(_redSheepId, EndTransformation);

        yield return new WaitForSeconds(endAnimationDuration);
        if (NetworkManager.Singleton.IsServer && netcodeController.IsOwner) netcodeController.CompleteTransformationServerRpc(_redSheepId);
    }
    
    private void AddStateMachineBehaviours(Animator animator)
    {
        StateMachineBehaviour[] behaviours = animator.GetBehaviours<StateMachineBehaviour>();
        foreach (StateMachineBehaviour behaviour in behaviours)
        {
            if (behaviour is BaseStateMachineBehaviour baseStateMachineBehaviour)
            {
                baseStateMachineBehaviour.Initialize(netcodeController, this);
            }
        }
    }
    
    private void HandleIncreaseTargetPlayerFearLevel(string receivedRedSheepId)
    {
        if (_redSheepId != receivedRedSheepId) return;
        if (GameNetworkManager.Instance.localPlayerController != _targetPlayer) return;
        
        if (_targetPlayer == null)
        {
            return;
        }
        
        if (_targetPlayer.HasLineOfSightToPosition(transformedRedSheepEye.transform.position, 115f, 50, 3f))
        {
            _targetPlayer.JumpToFearLevel(1);
            _targetPlayer.IncreaseFearLevelOverTime(0.8f);
        }
        
        else if (Vector3.Distance(transformedRedSheepEye.transform.position, _targetPlayer.transform.position) < 3)
        {
            _targetPlayer.JumpToFearLevel(0.6f);
            _targetPlayer.IncreaseFearLevelOverTime(0.4f);
        }
    }

    /// <summary>
    /// Changes the target player to the player with the given playerObjectId.
    /// </summary>
    /// <param name="receivedRedSheepId">The aloe id</param>
    /// <param name="targetPlayerObjectId">The target player's object ID</param>
    private void HandleChangeTargetPlayer(string receivedRedSheepId, ulong targetPlayerObjectId)
    {
        if (_redSheepId != receivedRedSheepId) return;
        if (targetPlayerObjectId == 69420)
        {
            _targetPlayer = null;
            return;
        }
        
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        _targetPlayer = player;
        LogDebug($"New target player is: {_targetPlayer.playerUsername}");
    }
    
    /// <summary>
    /// Changes the behaviour state to the given state
    /// </summary>
    /// <param name="receivedRedSheepId">The red sheep Id</param>
    /// <param name="newBehaviourStateIndex">The behaviour state to change to</param>
    private void HandleChangeBehaviourStateIndex(string receivedRedSheepId, int newBehaviourStateIndex)
    {
        if (_redSheepId != receivedRedSheepId) return;
        _currentBehaviourStateIndex = newBehaviourStateIndex;
    }

    private void HandleEnterDeathState(string receivedRedSheepId)
    {
        if (_redSheepId != receivedRedSheepId) return;
    }

    private void SetBool(string receivedRedSheepId, int animationParameter, bool value)
    {
        if (_redSheepId != receivedRedSheepId) return;
        _currentAnimator.SetBool(animationParameter, value);
    }

    private void SetTrigger(string receivedRedSheepId, int animationParameter)
    {
        if (_redSheepId != receivedRedSheepId) return;
        _currentAnimator.SetTrigger(animationParameter);
    }

    private void SetFloat(string receivedRedSheepId, int animationParameter, float value)
    {
        if (_redSheepId != receivedRedSheepId) return;
        _currentAnimator.SetFloat(animationParameter, value);
    }
    
    private void HandleSyncRedSheepIdentifier(string receivedRedSheepId)
    {
        _redSheepId = receivedRedSheepId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource(
            $"{TheRedSheepPlugin.ModGuid} | The Red Sheep Client {_redSheepId}");
        
        LogDebug("Successfully synced red sheep identifier");
    }

    private void HandleInitializeConfigValues(string receivedRedSheepId)
    {
        if (_redSheepId != receivedRedSheepId) return;
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}