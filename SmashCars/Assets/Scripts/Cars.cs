using Unity.VisualScripting;
using UnityEngine;

public class Cars : MonoBehaviour
{
    public GameObject[] cars;

    public int selectedCar = 0;

    void Start()
    {
        for (int i = 0; i < cars.Length; i++)
        {
            cars[i].SetActive(false);
        }

        cars[selectedCar].SetActive(true);
    }

    public void NextCar()
    {
        cars[selectedCar].SetActive(false);
        selectedCar = (selectedCar + 1) % cars.Length;
        cars[selectedCar].SetActive(true);
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
    }

    public void ConfirmSelection()
    {
        PlayerPrefs.SetInt("selectedCharacter", selectedCar);
        PlayerPrefs.Save();
    }

}
