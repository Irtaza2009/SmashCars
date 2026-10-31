using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class EndGame : MonoBehaviour
{
    public GameObject endGameScreen;
    public TMP_Text winnerText;

    private bool gameEnded = false;

    public void PlayerDied(int playerNumber)
    {
        if (gameEnded) return;

        gameEnded = true;

        endGameScreen.SetActive(true);

        int winner = playerNumber == 1 ? 2:1;

        winnerText.text = "Player " + winner + " wins";

        Time.timeScale = 0f;

    }

}
