using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class DropdownPopulator : MonoBehaviour
{
    // Assign your TextMeshPro Dropdown here via the Inspector
    public TMP_Dropdown dropdown;

    private List<int> placeIds = new List<int>();

    private const string API_URL = "https://backend-protel-nasdem.vercel.app/api/locations"; // Replace with your API endpoint

    private void Start()
    {
        // Initially, set the loading text and disable the dropdown
        dropdown.captionText.GetComponent<TextMeshProUGUI>().text = "Loading...";
        dropdown.interactable = false;

        FetchDataFromAPI();
    }

    private void FetchDataFromAPI()
    {
        StartCoroutine(GetDataCoroutine());
    }

    private IEnumerator GetDataCoroutine()
    {
        UnityWebRequest request = UnityWebRequest.Get(API_URL);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            PopulateDropdown(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError("Failed to fetch data from API: " + request.error);
            // Handle error here if needed
        }

        dropdown.captionText.GetComponent<TextMeshProUGUI>().text = "Pilih Lokasi"; // Clear the loading text
        dropdown.interactable = true;
    }

    private void PopulateDropdown(string jsonString)
    {
        PlaceData[] places = JsonHelper.FromJson<PlaceData>(jsonString);
        dropdown.options.Clear();
        placeIds.Clear();

        foreach (var place in places)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData(place.places_name));
            placeIds.Add(place.id);
        }

        dropdown.RefreshShownValue();
    }

    public int GetSelectedPlaceId()
    {
        int selectedIndex = dropdown.value;
        if (selectedIndex < 0 || selectedIndex >= placeIds.Count)
        {
            Debug.LogError("Invalid dropdown index!");
            return -1;
        }
        return placeIds[selectedIndex];
    }
}


[System.Serializable]
public class PlaceData
{
    public int id;
    public string places_name;
    public float latitude;
    public float longitude;
    public int total_nodes;
}

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string newJson = "{ \"array\": " + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.array;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] array;
    }
}
