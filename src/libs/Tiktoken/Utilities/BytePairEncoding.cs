﻿namespace Tiktoken.Utilities;

/// <summary>
/// 
/// </summary>
public static class BytePairEncoding
{
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
    private static byte[] GetSlice(this ReadOnlyMemory<byte> bytes, int from, int to)
    {
        return bytes[from..to].ToArray();
    }
#else
    private static byte[] GetSlice(this byte[] bytes, int from, int to)
    {
        return bytes.Skip(from).Take(to - from).ToArray();
    }
#endif
    
    private static unsafe bool TryFindMinRank((int Index, int Rank)* parts, int count, out int result)
    {
        result = 0;
        var minRank = int.MaxValue;
        for (var i = 0; i < count; i++)
        {
            if (parts[i].Rank < minRank)
            {
                minRank = parts[i].Rank;
                result = i;
            }
        }
        
        return minRank != int.MaxValue;
    }
    
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
    internal static unsafe IReadOnlyCollection<int> BytePairEncode(ReadOnlyMemory<byte> piece, IReadOnlyDictionary<byte[], int> ranks)
#else
    internal static unsafe IReadOnlyCollection<int> BytePairEncode(byte[] piece, IReadOnlyDictionary<byte[], int> ranks)
#endif
    {
        var partsLength = piece.Length + 1;
        var parts = stackalloc (int Index, int Rank)[partsLength];
        for (var i = 0; i < partsLength; i++)
        {
            parts[i] = (i, int.MaxValue);
        }
        for (var i = 0; i < partsLength - 2; i++)
        {
            parts[i] = (parts[i].Index, GetRank(i, parts, partsLength, piece, ranks, length: 2));
        }

        var count = partsLength - 1;
        while (true)
        {
            if (!TryFindMinRank(parts, count, out var i))
            {
                break;
            }

            parts[i] = (parts[i].Index, GetRank(i, parts, count + 1, piece, ranks, length: 3));
            if (i > 0)
            {
                parts[i - 1] = (parts[i - 1].Index, GetRank(i - 1, parts, count + 1, piece, ranks, length: 3));
            }
            for (var j = i + 1; j < count; j++)
            {
                parts[j] = parts[j + 1];
            }
            count--;
        }
        var outList = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            var from = parts[i].Index;
            var to = parts[i + 1].Index;
            var slice = piece.GetSlice(from, to);
            
            outList.Add(ranks[slice]);
        }
        
        return outList;
    }
    
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
    internal static unsafe int BytePairEncodeCountTokens(ReadOnlyMemory<byte> piece, IReadOnlyDictionary<byte[], int> ranks)
#else
    internal static unsafe int BytePairEncodeCountTokens(byte[] piece, IReadOnlyDictionary<byte[], int> ranks)
#endif
    {
        var partsLength = piece.Length + 1;
        var parts = stackalloc (int Index, int Rank)[partsLength];
        for (var i = 0; i < partsLength; i++)
        {
            parts[i] = (i, int.MaxValue);
        }
        for (var i = 0; i < partsLength - 2; i++)
        {
            parts[i] = (parts[i].Index, GetRank(i, parts, partsLength, piece, ranks, length: 2));
        }
        
        var count = partsLength - 1;
        while (true)
        {
            if (!TryFindMinRank(parts, count, out var i))
            {
                break;
            }
            
            parts[i] = (parts[i].Index, GetRank(i, parts, count + 1, piece, ranks, length: 3));
            if (i > 0)
            {
                parts[i - 1] = (parts[i - 1].Index, GetRank(i - 1, parts, count + 1, piece, ranks, length: 3));
            }
            for (var j = i + 1; j < count; j++)
            {
                parts[j] = parts[j + 1];
            }
            count--;
        }
        
        return count;
    }

    private static unsafe int GetRank(
        int startIdx,
        (int Index, int Rank)* parts,
        int count,
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        ReadOnlyMemory<byte> piece,
#else
        byte[] piece,
#endif
        IReadOnlyDictionary<byte[], int> ranks,
        int length)
    {
        if (startIdx + length < count)
        {
            var from = parts[startIdx].Index;
            var to = parts[startIdx + length].Index;
            var slice = piece.GetSlice(from, to);
            if (ranks.TryGetValue(slice, out var rank))
            {
                return rank;
            }
        }
        
        return int.MaxValue;
    }
}