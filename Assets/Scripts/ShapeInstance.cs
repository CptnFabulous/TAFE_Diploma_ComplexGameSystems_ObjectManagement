[System.Serializable]
public struct ShapeInstance // Used to differentiate individual shapes
{

    public bool IsValid
    {
        get
        {
            return Shape && instanceIdOrSaveIndex == Shape.InstanceId; // Checks if IDs match
        }
    }

    public Shape Shape { get; private set; }

    int instanceIdOrSaveIndex; // A variable used to differentiate shapes

    public ShapeInstance(Shape shape)
    {
        Shape = shape;
        instanceIdOrSaveIndex = shape.InstanceId;
    }

    public ShapeInstance(int saveIndex)
    {
        Shape = null;
        instanceIdOrSaveIndex = saveIndex;
    }

    public void Resolve() // Fix shape having non-valid ID
    {
        if (instanceIdOrSaveIndex >= 0)
        {
            Shape = Game.Instance.GetShape(instanceIdOrSaveIndex);
            instanceIdOrSaveIndex = Shape.InstanceId;
        }
    }

    public static implicit operator ShapeInstance(Shape shape)
    {
        return new ShapeInstance(shape);
    }
}