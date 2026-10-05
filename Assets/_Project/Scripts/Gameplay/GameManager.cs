using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieStealth.Player;

namespace ZombieStealth.Gameplay
{
    /// <summary>
    /// Win/lose state. Caught or extracted: freeze the game, show the result, then reload the scene.
    /// Only the first result of a run counts (IsGameOver guard).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] float restartDelay = 2.5f; // real seconds

        string message;
        Color messageColor;
        GUIStyle messageStyle;

        public bool IsGameOver { get; private set; }
        public bool PlayerWon { get; private set; }

        public void PlayerCaught() => EndGame("CAUGHT", new Color(0.9f, 0.1f, 0.1f), won: false);
        public void PlayerExtracted() => EndGame("EXTRACTED", new Color(0.2f, 0.95f, 0.4f), won: true);

        void EndGame(string text, Color color, bool won)
        {
            if (IsGameOver)
                return; // the other result already happened this run
            IsGameOver = true;
            PlayerWon = won;
            message = text;
            messageColor = color;

            // Stop player input and freeze everything time-based (zombie AI, NavMesh agents).
            FindAnyObjectByType<PlayerMovement>().enabled = false;
            FindAnyObjectByType<PlayerLook>().enabled = false;
            FindAnyObjectByType<PlayerThrower>().enabled = false;
            Time.timeScale = 0f;

            StartCoroutine(RestartAfterDelay());
        }

        IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSecondsRealtime(restartDelay);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnGUI()
        {
            if (!IsGameOver)
                return;

            messageStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
            };
            messageStyle.normal.textColor = messageColor;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), $"{message}\n<size=20>restarting...</size>", messageStyle);
        }
    }
}
