using UnityEngine;
using ZombieStealth.Player;

namespace ZombieStealth.Gameplay
{
    /// <summary>Trigger volume: when the player walks in, the level is won.</summary>
    [RequireComponent(typeof(Collider))]
    public class ExtractionZone : MonoBehaviour
    {
        GameManager gameManager;

        void Awake()
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlayerMovement _))
                gameManager.PlayerExtracted();
        }

#if UNITY_EDITOR
        static GUIStyle labelStyle;

        void OnDrawGizmos()
        {
            labelStyle ??= new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.2f, 0.95f, 0.4f) } };
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4.5f, "EXTRACTION", labelStyle);
        }
#endif
    }
}
