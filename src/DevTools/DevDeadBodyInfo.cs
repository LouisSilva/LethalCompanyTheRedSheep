using UnityEngine;

namespace LethalCompanyTheRedSheep.DevTools;

public class DevDeadBodyInfo : MonoBehaviour
{
    public Rigidbody[] bodyParts;
    public Rigidbody attachedLimb;
    public Transform attachedTo;
    public Rigidbody secondaryAttachedLimb;
    public Transform secondaryAttachedTo;
    public Vector3 spawnPosition;
    public Vector3 forceDirection;
    public float maxVelocity;
    public float speedMultiplier;
    public bool matchPositionExactly = true;
    public bool wasMatchingPosition;
    public Rigidbody previousAttachedLimb;
    public Vector3 previousBodyPosition;
    public bool parentedToShip;
    public float resetBodyPartsTimer;
    public bool lerpBeforeMatchingPosition;
    public float moveToExactPositionTimer;
    public bool isInShip;
    public bool deactivated;
    
    public void Start()
    {
        spawnPosition = transform.position;
        previousBodyPosition = Vector3.zero;
    }
    
    public void Update()
    {
        if (attachedLimb != null && attachedTo != null &&
            matchPositionExactly)
        {
            ResetBodyPositionIfTooFarFromAttachment();
            resetBodyPartsTimer += Time.deltaTime;
            if (resetBodyPartsTimer >= 0.25)
            {
                resetBodyPartsTimer = 0.0f;
                EnableCollisionOnBodyParts();
            }
        }
    }

    public void LateUpdate()
    {
        if (deactivated)
        {
            transform.SetParent(null, true);
        }
        else
        {
            if (this.attachedLimb == null || attachedTo == null ||
                attachedTo.parent == transform)
            {
                moveToExactPositionTimer = 0.0f;
                if (!wasMatchingPosition)
                    return;
                wasMatchingPosition = false;
                if (StartOfRound.Instance.shipBounds.bounds.Contains(transform.position))
                {
                    transform.SetParent(StartOfRound.Instance.elevatorTransform);
                    parentedToShip = true;
                }

                previousAttachedLimb.ResetCenterOfMass();
                previousAttachedLimb.ResetInertiaTensor();
                previousAttachedLimb.freezeRotation = false;
                previousAttachedLimb.isKinematic = false;
                EnableCollisionOnBodyParts();
            }
            else
            {
                if (parentedToShip)
                {
                    parentedToShip = false;
                    transform.SetParent(null, true);
                }

                if (matchPositionExactly)
                {
                    if (lerpBeforeMatchingPosition && moveToExactPositionTimer < 0.30000001192092896)
                    {
                        moveToExactPositionTimer += Time.deltaTime;
                        speedMultiplier = 25f;
                    }
                    else
                    {
                        if (!wasMatchingPosition)
                        {
                            wasMatchingPosition = true;
                            Vector3 vector3 = transform.position - attachedLimb.position;
                            transform.GetComponent<Rigidbody>().position = attachedTo.position + vector3;
                            previousAttachedLimb = attachedLimb;
                            attachedLimb.freezeRotation = true;
                            attachedLimb.isKinematic = true;
                            attachedLimb.transform.position = attachedTo.position;
                            attachedLimb.transform.rotation = attachedTo.rotation;
                            foreach (Rigidbody t in bodyParts)
                            {
                                t.angularDrag = 1f;
                                t.maxAngularVelocity = 2f;
                                t.maxDepenetrationVelocity = 0.3f;
                                t.velocity = Vector3.zero;
                                t.angularVelocity = Vector3.zero;
                                t.WakeUp();
                            }

                            return;
                        }

                        attachedLimb.position = attachedTo.position;
                        attachedLimb.rotation = attachedTo.rotation;
                        attachedLimb.centerOfMass = Vector3.zero;
                        attachedLimb.inertiaTensorRotation = Quaternion.identity;
                        return;
                    }
                }

                forceDirection = Vector3.Normalize(attachedTo.position - this.attachedLimb.position);
                this.attachedLimb.AddForce(
                    forceDirection * (speedMultiplier * Mathf.Clamp(Vector3.Distance(attachedTo.position, this.attachedLimb.position), 0.2f, 2.5f)),
                    ForceMode.VelocityChange);
                Vector3 velocity = this.attachedLimb.velocity;
                if (velocity.sqrMagnitude > (double)maxVelocity)
                {
                    Rigidbody attachedLimb = this.attachedLimb;
                    velocity = this.attachedLimb.velocity;
                    Vector3 vector3 = velocity.normalized * maxVelocity;
                    attachedLimb.velocity = vector3;
                }

                if (this.secondaryAttachedLimb == null || secondaryAttachedTo == null) return;
                forceDirection = Vector3.Normalize(secondaryAttachedTo.position - this.secondaryAttachedLimb.position);
                this.secondaryAttachedLimb.AddForce(
                    forceDirection * (speedMultiplier * Mathf.Clamp(
                        Vector3.Distance(secondaryAttachedTo.position, this.secondaryAttachedLimb.position), 0.2f,
                        2.5f)), ForceMode.VelocityChange);
                velocity = this.secondaryAttachedLimb.velocity;
                if (velocity.sqrMagnitude <= (double)maxVelocity)
                    return;
                Rigidbody secondaryAttachedLimb = this.secondaryAttachedLimb;
                velocity = this.secondaryAttachedLimb.velocity;
                Vector3 vector3_1 = velocity.normalized * maxVelocity;
                secondaryAttachedLimb.velocity = vector3_1;
            }
        }
    }

    public void ResetBodyPositionIfTooFarFromAttachment()
    {
        for (int index = 0; index < bodyParts.Length; ++index)
        {
            if (Vector3.Distance(bodyParts[index].position, attachedTo.position) > 4.0)
            {
                resetBodyPartsTimer = 0.0f;
                bodyParts[index].GetComponent<Collider>().enabled = false;
            }
        }
    }


    public void EnableCollisionOnBodyParts()
    {
        for (int index = 0; index < bodyParts.Length; ++index)
            bodyParts[index].GetComponent<Collider>().enabled = true;
    }

    public void SetBodyPartsKinematic(bool setKinematic = true)
    {
        if (setKinematic)
        {
            foreach (Rigidbody t in bodyParts)
            {
                t.velocity = Vector3.zero;
                t.isKinematic = true;
            }
        }
        else
        {
            foreach (Rigidbody t in bodyParts)
            {
                t.velocity = Vector3.zero;
                if (!(t == attachedLimb) || !matchPositionExactly)
                    t.isKinematic = false;
            }
        }
    }

    public void DeactivateBody(bool setActive)
    {
        gameObject.SetActive(setActive);
        SetBodyPartsKinematic();
        isInShip = false;
        deactivated = true;
    }

    public void ResetRagdollPosition()
    {
        if (attachedLimb != null && attachedTo != null)
            transform.position = attachedTo.position + Vector3.up * 2f;
        else
            transform.position = spawnPosition;
        foreach (Rigidbody t in bodyParts)
        {
            t.velocity = Vector3.zero;
            t.GetComponent<Collider>().enabled = false;
        }
    }

    public void SetRagdollPositionSafely(Vector3 newPosition, bool disableSpecialEffects = false)
    {
        transform.position = newPosition + Vector3.up * 2.5f;
        foreach (Rigidbody t in bodyParts)
            t.velocity = Vector3.zero;
    }

    public void AddForceToBodyPart(int bodyPartIndex, Vector3 force)
    {
        bodyParts[bodyPartIndex].AddForce(force, ForceMode.Impulse);
    }

    public void ChangeMesh(Mesh changeMesh, Material changeMaterial = null)
    {
        gameObject.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh = changeMesh;
        if (!(changeMaterial != null))
            return;
        gameObject.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial = changeMaterial;
    }
}