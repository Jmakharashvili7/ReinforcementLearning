using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.IO;
using System.Linq;

/*
 * Anaconda helper functions:
 * Change Directory to this proejct - cd Documents\FYP\Applying_EANNs\TrainingData
 * Print Training Data - tensorboard --logdir results
 * Start new Training - mlagents-learn configuration.yaml --run-id=
 */


public class MLCar : Agent
{
    [SerializeField]
    public GameObject CheckpointParent = null;

    [SerializeField]
    public bool TrainingMode = true;

    public bool isRaceFinished = false;

    public CarMovement Movement 
    { 
        get;
        private set;
    }

    public double[] CurrentControlInputs
    {
        get { return Movement.CurrentInputs; }
    }

    /// <summary>
    /// The cached SpriteRenderer of this car
    /// </summary>
    public SpriteRenderer SpriteRenderer 
    { 
        get; 
        private set; 
    }

    public int CurrCheckpoint
    {
        get { return currCheckpoint; }
        private set { }
    }

    public int CurrLap
    {
        get { return currLap; }
        private set { }
    }

    public float DistanceToNextCheckpoint
    {
        get 
        {
            if (currCheckpoint+1 < checkpointList.Count)
                return Vector3.Distance(transform.position, checkpointList[currCheckpoint + 1].transform.position);
            else
                return Vector3.Distance(transform.position, checkpointList[0].transform.position);
        }
        private set { }
    }

    // Checkpoint Data
    private List<Checkpoint> checkpointList;
    private int currCheckpoint;
    private float timeSinceLastCheckpoint;
    private float distanceToNextCheckpoint;
    
    // Race Track Data
    private int lapCount;
    private int currLap;
    private int raceCount;

    // Start Data
    private Vector3 startPos;
    private Quaternion startRotation;

    // Data for statistics
    struct DriveStats
    {
        public float completionTime;
        public float timeOffTrack;
        public int crashes;
        public int collisionCount;

        public void Reset()
        {
            completionTime = 0.0f;
            timeOffTrack = 0.0f;
            crashes = 0;
            collisionCount = 0;
        }

        public DriveStats(float completeTime = 0.0f, float time = 0.0f, int crashCount = 0, int collision = 0)
        {
            completionTime = completeTime;
            timeOffTrack = time;
            crashes = crashCount;
            collisionCount = collision;
        }
    }
    private List<DriveStats> driveStats;
    private DriveStats currDriveStats;

    public override void OnActionReceived(ActionBuffers actions)
    {
        double[] movementAxis = new double[2];

        movementAxis[0] = actions.ContinuousActions[0];
        movementAxis[1] = actions.ContinuousActions[1];

        // remove movement if race is over
        if (isRaceFinished)
        {
            movementAxis[0] = 0.0f;
            movementAxis[1] = 0.0f;
        }

        Movement.SetInputs(movementAxis);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continousActions = actionsOut.ContinuousActions;
        continousActions[0] = Input.GetAxis("Horizontal");
        continousActions[1] = Input.GetAxis("Vertical");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1 observation
        sensor.AddObservation(Movement.NomalizedVelocity);

        // 3 observations
        sensor.AddObservation(transform.up);
 
        // 4 observations       
        sensor.AddObservation(Movement.Rotation.normalized);

        // 1 observation
        //sensor.AddObservation(currCheckpoint);
        
        // 1 observation
        float dot = Vector3.Dot(transform.up, GetNextCheckpoint().transform.right);
        sensor.AddObservation(dot);
    }

    public override void OnEpisodeBegin()
    {
        if (TrainingMode)
        {
            isRaceFinished = false;
            raceCount++;
            transform.gameObject.GetComponent<BoxCollider2D>().enabled = true;
        }
    }

    private void ResetCar()
    {
        transform.position = startPos;
        transform.rotation = startRotation;

        timeSinceLastCheckpoint = 0.0f;
        currCheckpoint = 0;
        currLap = 0;

        transform.gameObject.GetComponent<BoxCollider2D>().enabled = false;

        currDriveStats.Reset();
        Movement.Stop();
    }

    void Awake()
    {
        // Cache components
        Movement = GetComponent<CarMovement>();
        SpriteRenderer = GetComponent<SpriteRenderer>();

        driveStats = new List<DriveStats>();
        currDriveStats = new DriveStats();

        startPos = transform.position;
        startRotation = transform.rotation;
        lapCount = transform.parent.parent.GetComponent<RaceManager>().LapCount;
        currLap = 0;

        if (TrainingMode)
        {
            // Init checkpoint array and index
            if (CheckpointParent)
            {
                checkpointList = CheckpointParent.GetComponentsInChildren<Checkpoint>().ToList();
                currCheckpoint = 0;
            }
            else
            {
                Debug.Log("Checkpoints missing");
            }
        }
    }

    // Update is called once per frame
    private void Start()
    {
        Movement.HitWall += PunishCrash;
        Movement.HitCar += PunishCollision;
        Movement.OffTrack += PunishOffTrack;
    }

    private void Update()
    {
        AddReward(-0.1f * Time.deltaTime); // punish for taking long

        timeSinceLastCheckpoint += Time.deltaTime;
        currDriveStats.completionTime += Time.deltaTime;
        // punish not moving
        if (timeSinceLastCheckpoint >= 10.0f)
        {
            ResetEpisode();
        }

        // Check if car is facing the wrong way
        float dir = Vector3.Dot(transform.up, GetNextCheckpoint().transform.right);
        if (dir < -0.5)
        {
            AddReward(-1.0f * Time.deltaTime);
        }

        // punish if the car is going away from the next checkpoint
        float currDistance = Vector3.Distance(transform.position, GetNextCheckpoint().transform.position);
        if (currDistance > distanceToNextCheckpoint)
        {
            AddReward(-1.0f * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        // Race completed
        if (currCheckpoint >= checkpointList.Count)
        {
            AddReward(7.0f);
            currCheckpoint = 0;
            currLap++;

            if (currLap >= lapCount)
            {
                RaceFinished();
            }
        }
    }

    private void ResetEpisode()
    {
        AddReward(-1.0f);
        isRaceFinished = true;
        ResetCar();
    }
        
    private void RaceFinished()
    {
        isRaceFinished = true;
        driveStats.Add(currDriveStats);
        WriteStatsToFile();
        ResetCar();
    }

    public void RestartRace()
    {
        ResetCar();
        EndEpisode();
    }

    private void PunishCollision()
    {
        AddReward(-2.0f);
        currDriveStats.collisionCount++;
        Movement.Stop();
    }

    private void PunishCrash()
    {
        currDriveStats.crashes++;
        AddReward(-3.0f);
        Movement.Stop();
    }

    private void PunishOffTrack()
    {
        AddReward(-0.1f * Time.deltaTime);
        currDriveStats.timeOffTrack += Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (TrainingMode)
        {
            if (other.gameObject == checkpointList[currCheckpoint].gameObject)
            {
                AddReward(0.25f);
                currCheckpoint++;
                timeSinceLastCheckpoint = 0.0f;
                distanceToNextCheckpoint = Vector3.Distance(transform.position, GetNextCheckpoint().transform.position);
            }
        }
    }

    private Checkpoint GetNextCheckpoint()
    {
        // 1 observation
        if (currCheckpoint + 1 < checkpointList.Count)
            return checkpointList[currCheckpoint];
        else
            return checkpointList[0];
    }

    private void WriteStatsToFile()
    {
        string filePath = "RunData/" + raceCount + "/" + transform.parent.parent.name + "/" + transform.name;
        string directoryPath = Path.GetDirectoryName(filePath);
        
        // Create the directory if it doesn't exist
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        
        // Write the text to the file
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            int counter = 0;
            foreach (DriveStats stats in driveStats)
            {
                counter++;
                writer.WriteLine("Episode: " + counter);
                writer.WriteLine("CompletionTime: " + stats.completionTime + " (in seconds)");
                writer.WriteLine("Collisions with other cars: " + stats.collisionCount);
                writer.WriteLine("Crashes into wall: " + stats.crashes);
                writer.WriteLine("Time Offtrack: " + stats.timeOffTrack);
            }
        }
    }
}
