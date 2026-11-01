using System;
using UnityEngine;

public class LoadCharacter : MonoBehaviour
{
    public GameObject[] characterPrefabs;
    public GameObject SelectedCharacter { get; private set; }

    [Header("Colour Customization")]
    public Color[] availableColours;
    public CarBody[] carBodies;

    public event Action<GameObject> SelectedCharacterChanged;

    private void Awake()
    {
        int selectedCharacter = PlayerPrefs.GetInt("selectedCharacter", 0);

        if (selectedCharacter < 0 || selectedCharacter >= characterPrefabs.Length)
            selectedCharacter = 0;

        for (int i = 0; i < characterPrefabs.Length; i++)
            characterPrefabs[i].SetActive(i == selectedCharacter);

        SelectedCharacter = characterPrefabs[selectedCharacter];

        int selectedColour = PlayerPrefs.GetInt("selectedCarColour", 0);
        selectedColour = Mathf.Clamp(selectedColour, 0, availableColours.Length - 1);

        foreach (Renderer body in carBodies[selectedCharacter].CarBodyParts)
        {
            body.material.color = availableColours[selectedColour];
        }

        SelectedCharacterChanged?.Invoke(SelectedCharacter);

    }
}
