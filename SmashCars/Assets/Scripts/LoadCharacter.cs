using UnityEngine;

public class LoadCharacter : MonoBehaviour
{
    public GameObject[] characterPrefabs;

    void Start()
    {
        int selectedCharacter = PlayerPrefs.GetInt("selectedCharacter", 0);

        if (selectedCharacter < 0 || selectedCharacter >= characterPrefabs.Length)
        {
            selectedCharacter = 0;
        }

        for (int i = 0; i < characterPrefabs.Length; i++)
        {
            characterPrefabs[i].SetActive(i == selectedCharacter);
        }




    }
}
