using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void mainMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScreen");
    }
}
