public class UnionFind
{
    private readonly int[] parents;
    private readonly int[] ranks;
    private int components;

    public UnionFind(int count)
    {
        parents = new int[count];
        ranks = new int[count];
        components = count;

        for (int i = 0; i < count; i++)
            parents[i] = i;
    }

    public bool Connected(int a, int b) => Find(a) == Find(b);

    public void Union(int a, int b)
    {
        a = Find(a);
        b = Find(b);
        if (a == b) return;

        if (ranks[a] < ranks[b])
            parents[a] = b;
        else if (ranks[a] > ranks[b])
            parents[b] = a;
        else
        {
            parents[b] = a;
            ranks[a]++;
        }

        components--;
    }

    public int Components => components;

    private int Find(int x)
    {
        return parents[x] == x ? x : (parents[x] = Find(parents[x]));
    }
}
