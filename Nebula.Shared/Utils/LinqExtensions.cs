namespace Nebula.Shared.Utils;

public static class LinqExtensions
{
    public static IEnumerable<IEnumerable<T>> SplitIntoNChunks<T>(this List<T> source, int n)
    {
        if (n <= 0) 
            throw new ArgumentOutOfRangeException(nameof(n), "Chunks count must be greater than zero.");

        var totalItems = source.Count;
        var defaultChunkSize = totalItems / n;
        var remainder = totalItems % n;

        var currentIndex = 0;

        for (var i = 0; i < n; i++)
        {
            var currentChunkSize = defaultChunkSize + (i < remainder ? 1 : 0);
            
            if (currentChunkSize == 0) break; 

            yield return source.GetRange(currentIndex, currentChunkSize);
            currentIndex += currentChunkSize;
        }
    }
}