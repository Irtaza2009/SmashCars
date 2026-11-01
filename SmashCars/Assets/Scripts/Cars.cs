using TMPro;
using UnityEngine;

public class Cars : MonoBehaviour
{
    [Header("Cars")]
    public GameObject[] cars;
    public int selectedCar = 0;

    [Header("Colour Customization")]
    public CarBody[] carBodies;
    public Color[] availableColours;
    public int selectedColour = 0;

    [Header("Two Player Selection")]
    public TMP_Text playerTitle;
    public TMP_Text nextButtonText;
    public GameObject nextButton;
    public GameObject battleButton;

    private int currentPlayer = 1;

    private string CharacterKey =>
        $"P{currentPlayer}_selectedCharacter";

    private string ColourKey =>
        $"P{currentPlayer}_selectedCarColour";

    private void Start()
    {
        currentPlayer = 1;

        LoadPlayerSelection();

        playerTitle.text = "Player 1";
        nextButtonText.text = "Next";

        nextButton.SetActive(true);
        battleButton.SetActive(false);
    }

    private void LoadPlayerSelection()
    {
        selectedCar = PlayerPrefs.GetInt(CharacterKey, 0);
        selectedColour = PlayerPrefs.GetInt(ColourKey, 0);

        selectedCar = Mathf.Clamp(selectedCar, 0, cars.Length - 1);
        selectedColour = Mathf.Clamp(
            selectedColour, 0, availableColours.Length - 1
        );

        for (int i = 0; i < cars.Length; i++)
            cars[i].SetActive(i == selectedCar);

        ApplyColour();
    }

    public void NextCar()
    {
        cars[selectedCar].SetActive(false);

        selectedCar = (selectedCar + 1) % cars.Length;

        cars[selectedCar].SetActive(true);

        ApplyColour();
    }

    public void PreviousCar()
    {
        cars[selectedCar].SetActive(false);

        selectedCar--;

        if (selectedCar < 0)
            selectedCar = cars.Length - 1;

        cars[selectedCar].SetActive(true);

        ApplyColour();
    }

    public void SetColour(int colourIndex)
    {
        if (colourIndex < 0 || colourIndex >= availableColours.Length)
            return;

        selectedColour = colourIndex;
        ApplyColour();
    }

    private void ApplyColour()
    {
        if (selectedCar >= carBodies.Length ||
            carBodies[selectedCar] == null)
            return;

        foreach (Renderer body in carBodies[selectedCar].CarBodyParts)
        {
            if (body != null)
                body.material.color = availableColours[selectedColour];
        }
    }

    public void SaveCurrentPlayerSelection()
    {
        PlayerPrefs.SetInt(CharacterKey, selectedCar);
        PlayerPrefs.SetInt(ColourKey, selectedColour);
        PlayerPrefs.Save();

        Debug.Log($"Player {currentPlayer} selection saved: Car {selectedCar}, Colour {selectedColour}");
    }

    public void NextPlayer()
    {
        if (currentPlayer != 1)
            return;

        // Save Player 1.
        SaveCurrentPlayerSelection();

        // Switch to Player 2.
        currentPlayer = 2;

        LoadPlayerSelection();

        playerTitle.text = "Player 2";

        nextButton.SetActive(false);
        battleButton.SetActive(true);
    }
}