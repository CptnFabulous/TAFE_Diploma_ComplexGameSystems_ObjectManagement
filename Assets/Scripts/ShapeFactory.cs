using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu]
public class ShapeFactory : ScriptableObject
{

    public int FactoryId // Used to get and set the shape factory's ID
    {
        get
        {
            return factoryId;
        }
        set
        {
            if (factoryId == int.MinValue && value != int.MinValue) // A new ID can only be assigned if the ID is a default value (int.MinValue). If an ID is already assigned, an error message is displayed
            {
                factoryId = value;
            }
            else
            {
                Debug.Log("Not allowed to change factoryId.");
            }
        }
    }

    [System.NonSerialized]
    int factoryId = int.MinValue;

    [SerializeField]
    Shape[] prefabs;

    [SerializeField]
    Material[] materials;

    [SerializeField]
    bool recycle;

    List<Shape>[] pools;

    Scene poolScene;

    public Shape Get(int shapeId = 0, int materialId = 0) // Used to create new shapes
    {
        Shape instance;
        if (recycle) // If recycling and object pooling is enabled
        {
            if (pools == null)
            {
                CreatePools();
            }
            List<Shape> pool = pools[shapeId]; // Checks object pool for a version of the desired gameObject to spawn, and if one is present, it is enabled and removed from the pool
            int lastIndex = pool.Count - 1;
            if (lastIndex >= 0)
            {
                instance = pool[lastIndex];
                instance.gameObject.SetActive(true);
                pool.RemoveAt(lastIndex);
            }
            else // If an appropriate object is not present in the pool, a new object is instantiated and assigned a shape factory and ID, then moved to the appropriate scene
            {
                instance = Instantiate(prefabs[shapeId]);
                instance.OriginFactory = this;
                instance.ShapeId = shapeId;
                SceneManager.MoveGameObjectToScene(
                    instance.gameObject, poolScene
                );
            }
        }
        else // Otherwise, simply instantiate a new shape and give it an ID
        {
            instance = Instantiate(prefabs[shapeId]);
            instance.ShapeId = shapeId;
        }

        instance.SetMaterial(materials[materialId], materialId); // Applies desired materials to new shape prefab
        Game.Instance.AddShape(instance);
        return instance;
    }

    public Shape GetRandom() // Returns a randomly created shape
    {
        return Get(Random.Range(0, prefabs.Length), Random.Range(0, materials.Length));
    }

    public void Reclaim(Shape shapeToRecycle) // Returns a shape to the object pool. Used when an object needs to be 'destroyed'.
    {
        if (shapeToRecycle.OriginFactory != this) // Checks that the factory actually created the shape it is trying to reclaim
        {
            Debug.LogError("Tried to reclaim shape with wrong factory.");
            return;
        }
        if (recycle) // If recycling and object pooling is enabled
        {
            if (pools == null) // Checks for an existing pool, and creates one if it does not exist.
            {
                CreatePools();
            }
            pools[shapeToRecycle.ShapeId].Add(shapeToRecycle); // Disables shape gameObject and adds it to the pool
            shapeToRecycle.gameObject.SetActive(false);
        }
        else
        {
            Destroy(shapeToRecycle.gameObject);
        }
    }

    void CreatePools() // Creates a new object pool
    {
        pools = new List<Shape>[prefabs.Length];
        for (int i = 0; i < pools.Length; i++)
        {
            pools[i] = new List<Shape>();
        }

        if (Application.isEditor) // If game is running in an editor
        {
            poolScene = SceneManager.GetSceneByName(name); // Obtains a dedicated object pool scene, that corresponds to the shape factory by having the same name
            if (poolScene.isLoaded)
            {
                GameObject[] rootObjects = poolScene.GetRootGameObjects();
                for (int i = 0; i < rootObjects.Length; i++)
                {
                    Shape pooledShape = rootObjects[i].GetComponent<Shape>();
                    if (!pooledShape.gameObject.activeSelf)
                    {
                        pools[pooledShape.ShapeId].Add(pooledShape);
                    }
                }
                return;
            }
        }

        poolScene = SceneManager.CreateScene(name);
    }
}