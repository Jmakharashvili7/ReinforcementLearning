using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
    [SerializeField]
    public GameObject CarParent;
    [SerializeField]
    public GameObject CheckpointParent;
    [SerializeField]
    public int LapCount;
    [SerializeField]
    public TMP_Text LeaderBoardText;

    private List<MLCar> m_cars = new List<MLCar>();

    // Start is called before the first frame update
    void Awake()
    {
        // Set the car parent if one was provided otherwise throw an argument null exception
        if (CarParent != null)
            m_cars = CarParent.GetComponentsInChildren<MLCar>().ToList();
        else
            throw new ArgumentNullException("cars parent not found", nameof(CarParent));
    }  

    // Update is called once per frame
    void Update()
    {
        // Sort the cars by checkpoint and then by distance to next checkpoint
        if (LeaderBoardText != null)
        {
            var sortedCars = m_cars.OrderByDescending(c => c.CurrLap).ThenByDescending(c => c.CurrCheckpoint).ThenBy(c => c.DistanceToNextCheckpoint).ToList();
            LeaderBoardText.text = GetLeaderboardText(sortedCars);
        }

        UpdateRaceCondition();
    }

    private void UpdateRaceCondition()
    {
        if (CheckTrackFinish())
        {
            foreach(MLCar car in m_cars)
            {
                car.RestartRace();
            }
        }
    }

    private string GetLeaderboardText(List<MLCar> ranking)
    {
        string result = "";
        int counter = 1;
        foreach (MLCar car in ranking)
        {
            result += counter + ". " + car.transform.name;
            result += "\n";
            counter++;
        }

        return result;
    }

    // Returns true if all cars have finished the race
    private bool CheckTrackFinish()
    {
        foreach (MLCar car in m_cars)
        {
            if (!car.isRaceFinished)
                return false;
        }
        return true;
    }
}
