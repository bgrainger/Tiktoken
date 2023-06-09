﻿using System.Text.RegularExpressions;
using Tiktoken.Utilities;

namespace Tiktoken;

/// <summary>
/// 
/// </summary>
public class CoreBpe
{
    private IReadOnlyDictionary<string, int> SpecialTokensEncoder { get; set; }

    // TODO private max_token_value ??
    private IReadOnlyDictionary<byte[], int> Encoder { get; set; }

    private Regex SpecialRegex { get; set; }
    private Regex Regex { get; set; }

    private IReadOnlyDictionary<int, byte[]> Decoder { get; set; }
    private IReadOnlyDictionary<int, string> SpecialTokensDecoder { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="encoder"></param>
    /// <param name="specialTokensEncoder"></param>
    /// <param name="pattern"></param>
    public CoreBpe(
        IReadOnlyDictionary<byte[], int> encoder,
        IReadOnlyDictionary<string, int> specialTokensEncoder,
        string pattern)
    {
        specialTokensEncoder = specialTokensEncoder ?? throw new ArgumentNullException(nameof(specialTokensEncoder));
        
        Encoder = encoder;
        SpecialTokensEncoder = specialTokensEncoder;
        
        Regex = new Regex(pattern, RegexOptions.Compiled);
        SpecialRegex = new Regex(string.Join("|", specialTokensEncoder.Keys.Select(Regex.Escape)), RegexOptions.Compiled);

        Decoder = Encoder
            .ToDictionary(
                static x => x.Value,
                static x => x.Key);
        SpecialTokensDecoder = specialTokensEncoder
            .ToDictionary(
                static x => x.Value,
                static x => x.Key);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="text"></param>
    /// <param name="allowedSpecial"></param>
    /// <returns></returns>
    public IReadOnlyCollection<int> EncodeNative(string text, HashSet<string> allowedSpecial)
    {
        text = text ?? throw new ArgumentNullException(nameof(text));
        allowedSpecial = allowedSpecial ?? throw new ArgumentNullException(nameof(allowedSpecial));
        
        var ret = new List<int>();

        var start = 0;
        while (true)
        {
            Match nextSpecial;
            var startFind = start;
            while (true)
            {
                nextSpecial = SpecialRegex.Match(text, startFind);
                if (!nextSpecial.Success) break;
                if (allowedSpecial.Contains(text.Substring(nextSpecial.Index, nextSpecial.Length))) break;
                startFind = nextSpecial.Index + 1;
            }
            var end = nextSpecial.Success ? nextSpecial.Index : text.Length;
#if NET7_0_OR_GREATER
            foreach (var match in Regex.EnumerateMatches(text.AsSpan()[start..end]))
            {
                var matchValue = text.AsSpan().Slice(match.Index, match.Length).ToArray();
#else
            foreach (Match match in Regex.Matches(text.Substring(start, end - start)))
            {
                var matchValue = match.Value;
#endif
                var piece = System.Text.Encoding.UTF8.GetBytes(matchValue);
                if (Encoder.TryGetValue(piece, out int token))
                {
                    ret.Add(token);
                    continue;
                }
                var tokens = BytePairEncoding.BytePairEncode(piece, Encoder);
                ret.AddRange(tokens);
            }

            if (nextSpecial.Success)
            {
                var piece = nextSpecial.Value;
                var token = SpecialTokensEncoder[piece];
                ret.Add(token);
                start = nextSpecial.Index + nextSpecial.Length;
            }
            else
            {
                break;
            }
        }

        return ret;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tokens"></param>
    /// <returns></returns>
    public byte[] DecodeNative(IReadOnlyCollection<int> tokens)
    {
        tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        
        var ret = new List<byte>(tokens.Count * 2);
        foreach (var token in tokens)
        {
            byte[] tokenBytes = Array.Empty<byte>();
            if (Decoder.TryGetValue(token, out var value))
            {
                tokenBytes = value;
            } 
            else
            {
                if (SpecialTokensDecoder.TryGetValue(token, out var valueS))
                {
                    tokenBytes = System.Text.Encoding.UTF8.GetBytes(valueS);
                }
            }

            if (tokenBytes.Length > 0)
            {
                ret.AddRange(tokenBytes);
            } 
        }
        return ret.ToArray();
    }
}