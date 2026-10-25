using UnityEngine;
using UnityEngine.SceneManagement;

public class NewMonoBehaviourScript : MonoBehaviour
{
    public void StartBattle()
    {
        int selectionSceneIndex = SceneManager.GetActiveScene().buildIndex;

        int totalScenes = SceneManager.sceneCountInBuildSettings;

        int randomScene = Random.Range(
            selectionSceneIndex + 1,
            totalScenes
        );
        SceneManager.LoadScene(randomScene);
    }
}
