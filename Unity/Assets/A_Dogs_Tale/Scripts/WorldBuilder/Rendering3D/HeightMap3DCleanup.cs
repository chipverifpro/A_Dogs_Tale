public partial class DungeonGenerator
{
    // if root exists, destroy all 3D objects under it.
    // AKA: clear 3D tiles.
    public void Destroy3D()
    {
        if (root == null) return;
        for (int childIndex = root.childCount - 1; childIndex >= 0; childIndex--)
            Destroy(root.GetChild(childIndex).gameObject);
    }
}
