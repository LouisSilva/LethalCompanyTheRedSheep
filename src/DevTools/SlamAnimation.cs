using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LethalCompanyTheRedSheep.DevTools
{
    public class SlamAnimation : MonoBehaviour
    {
        public DevDeadBodyInfo playerBody;
        public Transform tentacleGrabTarget;
        public Transform startSlamTransform;
        public Transform endSlamTransform;
        public AnimationCurve slamCurve;
        public Vector2 cycleDurationRange = new(2f, 2f); // new(0.25f, 0.4f);
        public int slamCycles = 4;
        public float launchForce = 200f;
    
        private bool _isSlamming;
        
        private bool _lastCycleDirectionRight = true;

        private void Update()
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame && !_isSlamming)
            {
                StartCoroutine(SlamMotion());
            }
        }
        
        private IEnumerator SlamMotion()
        {
            Debug.Log("Running slam motion");
            _isSlamming = true;

            Vector3 currentEndPosition = GetRandomPointOnLine(startSlamTransform.position, endSlamTransform.position);

            for (int i = 0; i < slamCycles; i++)
            {
                float elapsedTime = 0f;

                float currentSlamDuration = 1 < slamCycles - 1
                    ? Random.Range(cycleDurationRange.x, cycleDurationRange.y)
                    : cycleDurationRange.x;
                
                // Get the new end position for the current cycle
                Vector3 currentStartPosition = currentEndPosition;
                currentEndPosition = GetNextRandomPointOnLine(startSlamTransform.position, endSlamTransform.position, _lastCycleDirectionRight);
                _lastCycleDirectionRight = !_lastCycleDirectionRight;
                
                while (elapsedTime < currentSlamDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float normalizedTime = elapsedTime / currentSlamDuration;

                    if (i == slamCycles - 1 && normalizedTime >= 0.375f)
                    {
                        DisconnectFromPlayer(currentStartPosition, currentEndPosition);
                        _isSlamming = false;
                        yield break;
                    }

                    // Calculate the position based on the curve
                    float x = Mathf.Lerp(currentStartPosition.x, currentEndPosition.x, normalizedTime);
                    float y = Mathf.Lerp(currentStartPosition.y, currentEndPosition.y, normalizedTime) + slamCurve.Evaluate(normalizedTime);

                    tentacleGrabTarget.position = new Vector3(x, y, currentStartPosition.z);
                    yield return null;
                }
            }

            _isSlamming = false;
            Debug.Log("Slam motion finished");
        }

        private void DisconnectFromPlayer(Vector3 start, Vector3 end)
        {
            playerBody.attachedLimb = null;
            playerBody.attachedTo = null;
            playerBody.secondaryAttachedLimb = null;
            playerBody.secondaryAttachedTo = null;
            
            // Apply force
            Vector3 direction = (end - start).normalized;
            Vector3 force = direction * launchForce;
            force.y += launchForce * 0.5f;
            playerBody.bodyParts[(int)TheRedSheepClient.DeadPlayerBodyParts.Root].AddForce(force, ForceMode.Impulse);
        }
        
        private static Vector3 GetRandomPointOnLine(Vector3 start, Vector3 end)
        {
            float t = Random.Range(0f, 1f);
            return Vector3.Lerp(start, end, t);
        }

        private static Vector3 GetNextRandomPointOnLine(Vector3 start, Vector3 end, bool lastCycleDirectionRight)
        {
            float t =
                // Pick a point between 0 and the previous end position (left to right)
                lastCycleDirectionRight ? Random.Range(0f, 0.8f) : // Ensure it is always less than 1
                // Pick a point between the previous end position and 1 (right to left)
                Random.Range(0.001f, 1f); // Ensure it is always more than 0

            return Vector3.Lerp(start, end, t);
        }
    }
}