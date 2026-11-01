using System;
using UnityEngine;

public class LoadCharacter : MonoBehaviour
{
    [Header("Player")]
    [Range(1, 2)]
    public int playerIndex = 1;

    [Header("Characters")]
    public GameObject[] characterPrefabs;
    public GameObject SelectedCharacter { get; private set; }

    [Header("Colour Customization")]
    public Color[] availableColours;
    public CarBody[] carBodies;

    public event Action<GameObject> SelectedCharacterChanged;

    private string CharacterKey => $"P{playerIndex}_selectedCharacter";
    private string ColourKey => $"P{playerIndex}_selectedCarColour";

    private void Awake()
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0)
        {
            Debug.LogError($"{name}: No character prefabs assigned.", this);
            return;
        }

        // Load this player's character.
        int selectedCharacter = PlayerPrefs.GetInt(CharacterKey, 0);

        if (selectedCharacter < 0 || selectedCharacter >= characterPrefabs.Length)
            selectedCharacter = 0;

        // Activate only the selected character.
        for (int i = 0; i < characterPrefabs.Length; i++)
        {
            if (characterPrefabs[i] != null)
                characterPrefabs[i].SetActive(i == selectedCharacter);
        }

        SelectedCharacter = characterPrefabs[selectedCharacter];

        if (SelectedCharacter == null)
        {
            Debug.LogError($"{name}: Selected character is missing.", this);
            return;
        }

        // Load and apply this player's colour.
        if (availableColours != null &&
            availableColours.Length > 0 &&
            carBodies != null &&
            selectedCharacter < carBodies.Length &&
            carBodies[selectedCharacter] != null)
        {
            int selectedColour = PlayerPrefs.GetInt(ColourKey, 0);
            selectedColour = Mathf.Clamp(
                selectedColour, 0, availableColours.Length - 1
            );

            foreach (Renderer body in carBodies[selectedCharacter].CarBodyParts)
            {
                if (body != null)
                    body.material.color = availableColours[selectedColour];
            }
        }

        // Notify the car controller.
        SelectedCharacterChanged?.Invoke(SelectedCharacter);
    }
}
