using UnityEngine;

namespace LethalCompanyTheRedSheep.CustomStateMachineBehaviours;

public class StartTransformationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Start transformation state entered");
        SheepClient.StartTransformationProcedure();
    }
}