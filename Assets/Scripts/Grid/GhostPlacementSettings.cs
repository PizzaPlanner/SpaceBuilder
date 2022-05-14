using UnityEngine;

[CreateAssetMenu(menuName = "GhostPlacementSettings")]
public class GhostPlacementSettings : ScriptableObject
{
    public Material GhostPlacementMaterial;
    public Material GhostBlueprintedMaterial;
    public Material GhostRemovalMaterial;

    public Material GridBuildMaterial;
    public Material GridDestroyMaterial;
}
