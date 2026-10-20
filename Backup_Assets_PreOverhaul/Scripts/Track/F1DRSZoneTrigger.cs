using UnityEngine;

/// <summary>
/// DRS Zone Trigger - place on the track to define DRS activation/deactivation zones.
/// The trigger collider should span the straight where DRS is allowed.
///
/// Attach to: Empty GameObject with BoxCollider (IsTrigger = true) on the track.
/// Dependencies: F1DRSERSSystem
/// </summary>
[RequireComponent(typeof(Collider))]
public class F1DRSZoneTrigger : MonoBehaviour
{
    [Header("═══ ZONE TYPE ═══")]
    [SerializeField] private bool isActivationZone = true; // true = DRS opens, false = DRS closes (detection point)

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var drsSystem = other.GetComponent<F1DRSERSSystem>();
        if (drsSystem == null) drsSystem = other.GetComponentInParent<F1DRSERSSystem>();
        if (drsSystem != null)
        {
            drsSystem.SetDRSZone(isActivationZone);
        }
    }
}
