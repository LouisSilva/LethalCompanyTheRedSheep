using System.Collections;
using UnityEngine;

namespace LethalCompanyTheRedSheep.DevTools
{
    public class SlamAnimation : MonoBehaviour
    {
        public DevDeadBodyInfo playerBody;
        public Transform tentacleGrabTarget;
        public Transform startSlamTransform;
        public Transform endSlamTransform;
        public AnimationCurve slamCurve;
        public Vector2 cycleDurationRange = new(0.25f, 0.4f);
        public int slamCycles = 3;
    
        private bool _isSlamming;

        private void Update()
        {//Keyboard.current.kKey.wasPressedThisFrame && 
            if (!_isSlamming)
            {
                StartCoroutine(SlamMotion());
            }
        }


        private IEnumerator SlamMotion()
        {
            Debug.Log("Running slam motion");
            _isSlamming = true;

            for (int i = 0; i < slamCycles; i++)
            {
                float elapsedTime = 0f;
                Vector3 startPosition = startSlamTransform.position;
                Vector3 endPosition = endSlamTransform.position;

                float currentSlamDuration = 1 < slamCycles - 1
                    ? Random.Range(cycleDurationRange.x, cycleDurationRange.y)
                    : cycleDurationRange.x;
                
                while (elapsedTime < currentSlamDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float normalizedTime = elapsedTime / currentSlamDuration;

                    if (i == slamCycles - 1 && normalizedTime >= 0.75f)
                    {
                        DisconnectFromPlayer();
                        //isSlamming = false;
                        yield break;
                    }

                    float x = Mathf.Lerp(startPosition.x, endPosition.x, normalizedTime);
                    float y = Mathf.Lerp(startPosition.y, endPosition.y, normalizedTime) +
                              slamCurve.Evaluate(normalizedTime);

                    tentacleGrabTarget.position = new Vector3(x, y, startPosition.z);
                    yield return null;
                }

                (startSlamTransform, endSlamTransform) = (endSlamTransform, startSlamTransform);
            }

            _isSlamming = false;
            Debug.Log("Slam motion finished");
        }

        private void DisconnectFromPlayer()
        {
            playerBody.attachedLimb = null;
            playerBody.attachedTo = null;
            playerBody.secondaryAttachedLimb = null;
            playerBody.secondaryAttachedTo = null;
        }
    }
}