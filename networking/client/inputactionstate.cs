public readonly struct InputActionState
{
    public InputActionState(string actionName, float strength)
    {
        ActionName = actionName;
        Strength = strength;
    }

    public string ActionName { get; }
    public float Strength { get; }
}
