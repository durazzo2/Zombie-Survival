using UnityEngine;
using ZombieStealth.Player;

namespace ZombieStealth.Gameplay
{
    /// <summary>Walk into it to get one rock (only if not already full). Then it disappears.</summary>
    [RequireComponent(typeof(Collider))]
    public class RockPickup : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlayerThrower thrower) && thrower.TryAddRock())
                gameObject.SetActive(false);
        }
    }
}
