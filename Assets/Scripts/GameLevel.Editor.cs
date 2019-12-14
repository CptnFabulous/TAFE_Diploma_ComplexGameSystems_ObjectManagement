  #if UNITY_EDITOR

using UnityEngine;

partial class GameLevel
{

    public bool HasMissingLevelObjects
    {
        get
        {
            if (levelObjects != null)
            {
                for (int i = 0; i < levelObjects.Length; i++)
                {
                    if (levelObjects[i] == null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public bool HasLevelObject(GameLevelObject o) // Checks if an object is part of a level
    {
        if (levelObjects != null)
        {
            for (int i = 0; i < levelObjects.Length; i++)
            {
                if (levelObjects[i] == o)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void RegisterLevelObject(GameLevelObject o) // Adds level object to list
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Do not invoke in play mode!");
            return;
        }

        if (HasLevelObject(o))
        {
            return;
        }

        if (levelObjects == null)
        {
            levelObjects = new GameLevelObject[] { o };
        }
        else
        {
            System.Array.Resize(ref levelObjects, levelObjects.Length + 1);
            levelObjects[levelObjects.Length - 1] = o;
        }
    }

    public void RemoveMissingLevelObjects() // Removes empty level objects from array
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Do not invoke in play mode!");
            return;
        }

        int holes = 0;
        for (int i = 0; i < levelObjects.Length - holes; i++)
        {
            if (levelObjects[i] == null)
            {
                holes += 1;
                System.Array.Copy(
                    levelObjects, i + 1, levelObjects, i,
                    levelObjects.Length - i - holes
                );
                i -= 1;
            }
        }
        System.Array.Resize(ref levelObjects, levelObjects.Length - holes);
    }
}

#endif