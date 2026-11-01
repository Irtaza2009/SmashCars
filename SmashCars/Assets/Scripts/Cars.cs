using Unity.VisualScripting;
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
    

    void Start()
    {
        selectedCar = PlayerPrefs.GetInt("selectedCharacter", selectedCar);
        selectedColour = PlayerPrefs.GetInt("selectedCarColour",0);

        selectedCar = Mathf.Clamp(selectedCar,0, cars.Length - 1);
        selectedColour = Mathf.Clamp(selectedColour, 0, availableColours.Length - 1);

        for (int i = 0; i < cars.Length; i++)
        {
            cars[i].SetActive(false);
        }

        cars[selectedCar].SetActive(true);

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
        if(selectedCar <0)
        {
            selectedCar += cars.Length;
        }
        cars[selectedCar].SetActive(true);

        ApplyColour();
    }

    public void SetColour(int colourIndex)
    {
        selectedColour = colourIndex;
        ApplyColour();

        PlayerPrefs.SetInt("selectedCarColour", selectedColour);
        PlayerPrefs.Save();
    }

    void ApplyColour()
    {
        foreach (Renderer body in carBodies[selectedCar].CarBodyParts)
        {
           body.material.color = availableColours[selectedColour];
        }
    }
    public void ConfirmSelection()
    {
        PlayerPrefs.SetInt("selectedCharacter", selectedCar);
        PlayerPrefs.SetInt("selectedCarColour", selectedColour);
        PlayerPrefs.Save();
    }

}
