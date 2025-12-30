// ================================================= //

//public static class DSU
//
// Simple Union-Find/Disjoint Set (DSU=Disjoint Set Union)
// Created by ChatGPT, for MergeOverlappingRooms function.
// Will probably re-write this using existing hashes.
class DSU
{
    int[] parent;
    int[] rank;

    public DSU(int n)
    {
        parent = new int[n];
        rank = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
    }

    public int Find(int x)
    {
        if (parent[x] != x) parent[x] = Find(parent[x]);
        return parent[x];
    }

    public void Union(int a, int b)
    {
        a = Find(a); b = Find(b);
        if (a == b) return;
        if (rank[a] < rank[b]) parent[a] = b;
        else if (rank[a] > rank[b]) parent[b] = a;
        else { parent[b] = a; rank[a]++; }
    }
}