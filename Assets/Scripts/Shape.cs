using System.Collections.Generic;
using UnityEngine;

public class Shape : PersistableObject
{

    static int colorPropertyId = Shader.PropertyToID("_Color");

    static MaterialPropertyBlock sharedPropertyBlock; // Property block re-used to alter material values on all shapes in the scene

    public float Age { get; private set; }

    public int InstanceId { get; private set; }

    public bool IsMarkedAsDying // Is the object going to die?
    {
        get
        {
            return Game.Instance.IsMarkedAsDying(this);
        }
    }

    public int SaveIndex { get; set; }

    public int ColorCount // Amount of colours
    {
        get
        {
            return colors.Length;
        }
    }

    public int MaterialId { get; private set; }

    public int ShapeId
    {
        get
        {
            return shapeId;
        }
        set
        {
            if (shapeId == int.MinValue && value != int.MinValue)
            {
                shapeId = value; // ID is changed from default value (int.MinValue) to actual ID
            }
            else // If an ID is already assigned, display an error message
            {
                Debug.LogError("Not allowed to change ShapeId.");
            }
        }
    }

    public ShapeFactory OriginFactory // Obtains information about the shapeFactory this shape came from
    {
        get
        {
            return originFactory;
        }
        set
        {
            if (originFactory == null) // A new originFactory is only assigned if there is not one already, otherwise aborts and displays an error message
            {
                originFactory = value;
            }
            else
            {
                Debug.LogError("Not allowed to change origin factory.");
            }
        }
    }

    ShapeFactory originFactory;

    int shapeId = int.MinValue;

    [SerializeField]
    MeshRenderer[] meshRenderers;

    Color[] colors;

    List<ShapeBehavior> behaviorList = new List<ShapeBehavior>();

    void Awake()
    {
        colors = new Color[meshRenderers.Length];
    }

    public T AddBehavior<T>() where T : ShapeBehavior, new()
    {
        T behavior = ShapeBehaviorPool<T>.Get();
        behaviorList.Add(behavior);
        return behavior;
    }

    public void Die()
    {
        Game.Instance.Kill(this);
    }

    public void GameUpdate() // Updates shape data
    {
        Age += Time.deltaTime;
        for (int i = 0; i < behaviorList.Count; i++)
        {
            if (!behaviorList[i].GameUpdate(this))
            {
                behaviorList[i].Recycle();
                behaviorList.RemoveAt(i--);
            }
        }
    }

    public void MarkAsDying()
    {
        Game.Instance.MarkAsDying(this);
    }

    public void Recycle() // Removes active behaviours and returns shape to object pool
    {
        Age = 0f;
        InstanceId += 1;
        for (int i = 0; i < behaviorList.Count; i++)
        {
            behaviorList[i].Recycle();
        }
        behaviorList.Clear();
        OriginFactory.Reclaim(this);
    }

    public void ResolveShapeInstances()
    {
        for (int i = 0; i < behaviorList.Count; i++)
        {
            behaviorList[i].ResolveShapeInstances();
        }
    }

    public void SetColor(Color color) // Uses property block to assign the shape new material properties, i.e. colour
    {
        if (sharedPropertyBlock == null)
        {
            sharedPropertyBlock = new MaterialPropertyBlock();
        }
        sharedPropertyBlock.SetColor(colorPropertyId, color);
        for (int i = 0; i < meshRenderers.Length; i++) // Assigns new property block data to each meshRenderer in shapes with multiple renderers
        {
            colors[i] = color;
            meshRenderers[i].SetPropertyBlock(sharedPropertyBlock);
        }
    }

    public void SetColor(Color color, int index) // Assigns a colour to a specific meshrenderer on the shape
    {
        if (sharedPropertyBlock == null)
        {
            sharedPropertyBlock = new MaterialPropertyBlock();
        }
        sharedPropertyBlock.SetColor(colorPropertyId, color);
        colors[index] = color;
        meshRenderers[index].SetPropertyBlock(sharedPropertyBlock);
    }

    public void SetMaterial(Material material, int materialId) // Applies a material to the meshrenderers on the shape, and updates the index used to reference the material
    {
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            meshRenderers[i].material = material;
        }
        MaterialId = materialId;
    }

    public override void Save(GameDataWriter writer) // Saves object data, such as transform data, colours, age, active behaviours, etc.
    {
        base.Save(writer);
        writer.Write(colors.Length);
        for (int i = 0; i < colors.Length; i++)
        {
            writer.Write(colors[i]);
        }
        writer.Write(Age);
        writer.Write(behaviorList.Count);
        for (int i = 0; i < behaviorList.Count; i++)
        {
            writer.Write((int)behaviorList[i].BehaviorType);
            behaviorList[i].Save(writer);
        }
    }

    public override void Load(GameDataReader reader)
    {
        base.Load(reader);
        if (reader.Version >= 5) // Runs unique functions for a specific save file type
        {
            LoadColors(reader);
        }
        else
        {
            SetColor(reader.Version > 0 ? reader.ReadColor() : Color.white); // If the save file type supports colour saving, read the colour data, otherwise colour the object white
        }
        if (reader.Version >= 6) // Runs unique functions for a specific save file type
        {
            Age = reader.ReadFloat();
            int behaviorCount = reader.ReadInt();
            for (int i = 0; i < behaviorCount; i++)
            {
                ShapeBehavior behavior = ((ShapeBehaviorType)reader.ReadInt()).GetInstance(); // Identifies shapeBehaviour from index in save file, and adds it to the shape's behaviour list
                behaviorList.Add(behavior);
                behavior.Load(reader);
            }
        }
        else if (reader.Version >= 4) // Runs unique functions for a specific save file type
        {
            AddBehavior<RotationShapeBehavior>().AngularVelocity = reader.ReadVector3(); // Adds functions to move and rotate object based on data in the save file
            AddBehavior<MovementShapeBehavior>().Velocity = reader.ReadVector3();
        }
    }

    void LoadColors(GameDataReader reader) // Loads multiple colours from save data and assigns them to the appropriate shape
    {
        int count = reader.ReadInt();
        int max = count <= colors.Length ? count : colors.Length;
        int i = 0;
        for (; i < max; i++)
        {
            SetColor(reader.ReadColor(), i);
        }
        if (count > colors.Length)
        {
            for (; i < count; i++)
            {
                reader.ReadColor();
            }
        }
        else if (count < colors.Length) // If colour data is not available, set colour to white
        {
            for (; i < colors.Length; i++)
            {
                SetColor(Color.white, i);
            }
        }
    }
}