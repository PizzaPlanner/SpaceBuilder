using Unity.Entities;

public struct GridUpdate : IComponentData, IEnableableComponent
{
    public enum UpdateType
    {
        Add,
        Remove,
        Change
    }

    public UpdateType Type;
    public IntAABB Area;
}

public readonly partial struct GridUpdateAspect : IAspect
{
    readonly EnabledRefRW<GridUpdate> _flag;
    readonly RefRW<GridUpdate> _update;

    public GridUpdate Update
    {
        set
        {        
            _update.ValueRW = value;
            _flag.ValueRW = true;
        }
        get => _update.ValueRW;
    }

    public void Disable()
    {
        _flag.ValueRW = false;
    }
}