using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class EndGame : MonoBehaviour
{
    public GameObject endGameScreen;
    public TMP_Text winnerText;
    public TMP_Text winnerShadowText;

    private bool gameEnded = false;

    public void PlayerDied(int playerNumber)
    {
        if (gameEnded) return;

        gameEnded = true;

        endGameScreen.SetActive(true);

        int winner = playerNumber == 1 ? 2:1;

        winnerText.text = "PLAYER " + winner + " WINS";
        winnerShadowText.text = "PLAYER " + winner + " WINS";

        Time.timeScale = 0f;

    }

}
