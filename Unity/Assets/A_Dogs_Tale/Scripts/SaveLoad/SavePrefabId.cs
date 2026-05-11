using UnityEngine;

[DisallowMultipleComponent]
public sealed class SavePrefabId : MonoBehaviour
{
    [SerializeField] private string prefabId = "";
    [SerializeField] private string resourcesPath = "";
    [SerializeField] private string assetPath = "";

    public string PrefabId => prefabId;
    public string ResourcesPath => resourcesPath;
    public string AssetPath => assetPath;

    public void SetPrefabIdentity(string newPrefabId, string newResourcesPath, string newAssetPath)
    {
        prefabId = newPrefabId ?? "";
        resourcesPath = newResourcesPath ?? "";
        assetPath = newAssetPath ?? "";
    }
}
