using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Game : PersistableObject
{

    const int saveVersion = 7;

    public static Game Instance { get; private set; }

    [SerializeField] ShapeFactory[] shapeFactories;

    [SerializeField] KeyCode createKey = KeyCode.C;
    [SerializeField] KeyCode destroyKey = KeyCode.X;
    [SerializeField] KeyCode newGameKey = KeyCode.N;
    [SerializeField] KeyCode saveKey = KeyCode.S;
    [SerializeField] KeyCode loadKey = KeyCode.L;

    [SerializeField] PersistentStorage storage;

    [SerializeField] int levelCount;

    [SerializeField] bool reseedOnLoad;

    [SerializeField] Slider creationSpeedSlider;
    [SerializeField] Slider destructionSpeedSlider;

    [SerializeField] float destroyDuration;

    public float CreationSpeed { get; set; }

    public float DestructionSpeed { get; set; }

    List<Shape> shapes;

    List<ShapeInstance> killList, markAsDyingList;

    float creationProgress, destructionProgress;

    int loadedLevelBuildIndex;

    Random.State mainRandomState;

    bool inGameUpdateLoop;

    int dyingShapeCount;

    void OnEnable()
    {
        Instance = this;
        if (shapeFactories[0].FactoryId != 0)
        {
            for (int i = 0; i < shapeFactories.Length; i++)
            {
                shapeFactories[i].FactoryId = i;
            }
        }
    }

    void Start()
    {
        mainRandomState = Random.state;
        shapes = new List<Shape>();
        killList = new List<ShapeInstance>();
        markAsDyingList = new List<ShapeInstance>();

        if (Application.isEditor) // If game is being run in the editor rather than as a build
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) // Looks through scenes
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.name.Contains("Level ")) // If the loaded scene is a game level (designated by name)
                {
                    SceneManager.SetActiveScene(loadedScene); // Set this scene to active and update the index to show which scene is meant to be active
                    loadedLevelBuildIndex = loadedScene.buildIndex;
                    return;
                }
            }
        }

        BeginNewGame(); // Refresh game level
        StartCoroutine(LoadLevel(1)); // Load level 1
    }

    void Update()
    {
        if (Input.GetKeyDown(createKey)) // Spawn objects into the currently selected level when createKey is pressed
        {
            GameLevel.Current.SpawnShapes();
        }
        else if (Input.GetKeyDown(destroyKey)) // Prepare object for destruction when destroyKey is pressed
        {
            DestroyShape();
        }
        else if (Input.GetKeyDown(newGameKey)) // Refresh game level when button is pressed
        {
            BeginNewGame();
            StartCoroutine(LoadLevel(loadedLevelBuildIndex));
        }
        else if (Input.GetKeyDown(saveKey)) // Save data in current level if button is pressed
        {
            storage.Save(this, saveVersion);
        }
        else if (Input.GetKeyDown(loadKey))
        {
            BeginNewGame(); // Refresh game level, then load object data from save file
            storage.Load(this);
        }
        else
        {
            for (int i = 1; i <= levelCount; i++) // Checks through levels
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i)) // Checks if a number button is pressed that corresponds to a level
                {
                    BeginNewGame(); // Refresh scene and load a new level
                    StartCoroutine(LoadLevel(i));
                    return;
                }
            }
        }
    }

    void FixedUpdate()
    {
        inGameUpdateLoop = true; // Updates shape data, a bool is used to ensure certain processes do not run while the shapes are updating
        for (int i = 0; i < shapes.Count; i++)
        {
            shapes[i].GameUpdate();
        }
        GameLevel.Current.GameUpdate();
        inGameUpdateLoop = false;

        creationProgress += Time.deltaTime * CreationSpeed; // Updates timer for spawning objects
        while (creationProgress >= 1f) // A while statement is used instead of an if statement to account for if the timer counts up enough to permit the action being run multiple times
        {
            creationProgress -= 1f; // Subtracts from timer by appropriate amount for amount of times the code needs to be run, so the timer can reset
            GameLevel.Current.SpawnShapes(); // Spawn shapes into current level
        }

        destructionProgress += Time.deltaTime * DestructionSpeed; // Updates timer for destroying objects
        while (destructionProgress >= 1f) // A while statement is used instead of an if statement to account for if the timer counts up enough to permit the action being run multiple times
        {
            destructionProgress -= 1f;
            DestroyShape();
        }

        int limit = GameLevel.Current.PopulationLimit;
        if (limit > 0)
        {
            while (shapes.Count - dyingShapeCount > limit) // If too many shapes exist in the level (excluding shapes in the process of 'dying' that are already going to disappear), destroy excess shapes
            {
                DestroyShape();
            }
        }

        if (killList.Count > 0)
        {
            for (int i = 0; i < killList.Count; i++)
            {
                if (killList[i].IsValid)
                {
                    KillImmediately(killList[i].Shape);
                }
            }
            killList.Clear();
        }

        if (markAsDyingList.Count > 0)
        {
            for (int i = 0; i < markAsDyingList.Count; i++)
            {
                if (markAsDyingList[i].IsValid)
                    MarkAsDyingImmediately(markAsDyingList[i].Shape);
            }
            markAsDyingList.Clear();
        }
    }

    void BeginNewGame()
    {
        Random.state = mainRandomState;
        int seed = Random.Range(0, int.MaxValue) ^ (int)Time.unscaledTime;
        mainRandomState = Random.state;
        Random.InitState(seed);

        // Resets creation and destruction speed values and sliders
        creationSpeedSlider.value = CreationSpeed = 0;
        destructionSpeedSlider.value = DestructionSpeed = 0;

        for (int i = 0; i < shapes.Count; i++) // Recycle and reset shapes
        {
            shapes[i].Recycle();
        }
        shapes.Clear(); // Empty active shape list
        dyingShapeCount = 0;
    }

    IEnumerator LoadLevel(int levelBuildIndex)
    {
        enabled = false; // Disable this script so level data can be loaded
        if (loadedLevelBuildIndex > 0) // If build index is greater than zero and therefore a level (main menu is assigned to zero)
        {
            yield return SceneManager.UnloadSceneAsync(loadedLevelBuildIndex); // Unload current level, wait until complete before performing new tasks
        }
        yield return SceneManager.LoadSceneAsync(levelBuildIndex, LoadSceneMode.Additive); // Load new level alongside existing scenes, wait until complete before performing new tasks
        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(levelBuildIndex)); // Set new level as active scene
        loadedLevelBuildIndex = levelBuildIndex; // Update index for level reference
        enabled = true; // Reenables this script
    }

    void DestroyShape()
    {
        if (shapes.Count - dyingShapeCount > 0) // Checks if shapes exist and are not already dying
        {
            Shape shape = shapes[Random.Range(dyingShapeCount, shapes.Count)]; // Randomly selects shape
            if (destroyDuration <= 0f) // If duration has expired, destroy shape immediately
            {
                KillImmediately(shape);
            }
            else
            {
                shape.AddBehavior<DyingShapeBehavior>().Initialize(shape, destroyDuration); // Adds behaviour so shape goes through dying process
            }
        }
    }

    public void AddShape(Shape shape) // Creates new shape and gives it a save index
    {
        shape.SaveIndex = shapes.Count;
        shapes.Add(shape);
    }

    public Shape GetShape(int index) // Obtains a shape from the shape list using an index
    {
        return shapes[index];
    }

    public void Kill(Shape shape) // Immediately kill a shape
    {
        if (inGameUpdateLoop) // Adds shape to list to be killed
        {
            killList.Add(shape);
        }
        else
        {
            KillImmediately(shape);
        }
    }

    void KillImmediately(Shape shape) // Shape is finally removed from scene, either by being recycled or destroyed
    {
        int index = shape.SaveIndex;
        shape.Recycle(); // Shape is returned to object pool

        if (index < dyingShapeCount && index < --dyingShapeCount)
        {
            shapes[dyingShapeCount].SaveIndex = index;
            shapes[index] = shapes[dyingShapeCount];
            index = dyingShapeCount;
        }

        int lastIndex = shapes.Count - 1;
        if (index < lastIndex)
        {
            shapes[lastIndex].SaveIndex = index;
            shapes[index] = shapes[lastIndex];
        }
        shapes.RemoveAt(lastIndex); // Entry in list with destroyed shape is removed
    }

    public bool IsMarkedAsDying(Shape shape) // Checks if a shape is going to die
    {
        return shape.SaveIndex < dyingShapeCount;
    }

    public void MarkAsDying(Shape shape) // Marks shape to begin dying process
    {
        if (inGameUpdateLoop) // Either marks shape immediately, or adds shape to queue if update loop is in progress
        {
            markAsDyingList.Add(shape);
        }
        else
        {
            MarkAsDyingImmediately(shape);
        }
    }

    void MarkAsDyingImmediately(Shape shape)
    {
        int index = shape.SaveIndex;
        if (index < dyingShapeCount)
        {
            return;
        }
        shapes[dyingShapeCount].SaveIndex = index;
        shapes[index] = shapes[dyingShapeCount];
        shape.SaveIndex = dyingShapeCount;
        shapes[dyingShapeCount++] = shape;
    }

    public override void Save(GameDataWriter writer) // Writes essential data for current scene
    {
        writer.Write(shapes.Count);
        writer.Write(Random.state);
        writer.Write(CreationSpeed);
        writer.Write(creationProgress);
        writer.Write(DestructionSpeed);
        writer.Write(destructionProgress);
        writer.Write(loadedLevelBuildIndex);
        GameLevel.Current.Save(writer);

        for (int i = 0; i < shapes.Count; i++) // For each shape in the shapes list
        {
            writer.Write(shapes[i].OriginFactory.FactoryId); // Save the shape factory it came from
            writer.Write(shapes[i].ShapeId); // Save shape type
            writer.Write(shapes[i].MaterialId); // Save material used to render shape
            shapes[i].Save(writer);
        }
    }

    public override void Load(GameDataReader reader)
    {
        int version = reader.Version; // Checks save version against game version, does not load newer save versions to prevent compatibility issues
        if (version > saveVersion)
        {
            Debug.LogError("Unsupported future save version " + version);
            return;
        }
        StartCoroutine(LoadGame(reader)); // Loads game level with save data from reader  
    }

    IEnumerator LoadGame(GameDataReader reader)
    {
        int version = reader.Version; // Checks version
        int count = version <= 0 ? -version : reader.ReadInt();

        if (version >= 3) // If version 3 save data, perform exclusive actions
        {
            Random.State state = reader.ReadRandomState();
            if (!reseedOnLoad)
            {
                Random.state = state;
            }
            creationSpeedSlider.value = CreationSpeed = reader.ReadFloat();
            creationProgress = reader.ReadFloat();
            destructionSpeedSlider.value = DestructionSpeed = reader.ReadFloat();
            destructionProgress = reader.ReadFloat();
        }

        yield return LoadLevel(version < 2 ? 1 : reader.ReadInt()); // Loads game level with data, pauses function while this is running
        if (version >= 3)
        {
            GameLevel.Current.Load(reader);
        }

        for (int i = 0; i < count; i++) // Loads shapes into game world from appropriate data
        {
            int factoryId = version >= 5 ? reader.ReadInt() : 0;
            int shapeId = version > 0 ? reader.ReadInt() : 0;
            int materialId = version > 0 ? reader.ReadInt() : 0;
            Shape instance = shapeFactories[factoryId].Get(shapeId, materialId);
            instance.Load(reader);
        }

        for (int i = 0; i < shapes.Count; i++)
        {
            shapes[i].ResolveShapeInstances();
        }
    }
}