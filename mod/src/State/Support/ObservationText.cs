using System.Text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace TakuAgentMod.State.Support;

internal static class ObservationText
{
    public static string? SafeGetText(Func<object?> getter)
    {
        try
        {
            object? value = getter();
            return ToReadableText(value);
        }
        catch
        {
            return null;
        }
    }

    public static string? SafeGetCardDescription(CardModel card, PileType pile = PileType.Hand)
    {
        try
        {
            return StripRichTextTags(card.GetDescriptionForPile(pile)).Replace("\n", " ");
        }
        catch
        {
            return SafeGetText(() => card.Description)?.Replace("\n", " ");
        }
    }

    public static string? ToReadableText(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return StripRichTextTags(text);
        }

        if (value is LocString locString)
        {
            try
            {
                string formatted = locString.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    return StripRichTextTags(formatted);
                }
            }
            catch
            {
            }

            try
            {
                string raw = locString.GetRawText();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return StripRichTextTags(raw);
                }
            }
            catch
            {
            }

            return $"{locString.LocTable}:{locString.LocEntryKey}";
        }

        return value.ToString();
    }

    public static string StripRichTextTags(string text)
    {
        var builder = new StringBuilder();
        int index = 0;
        while (index < text.Length)
        {
            if (text[index] == '[')
            {
                if (text.AsSpan(index).StartsWith("[img]"))
                {
                    int contentStart = index + 5;
                    int closeTag = text.IndexOf("[/img]", contentStart, StringComparison.Ordinal);
                    if (closeTag >= 0)
                    {
                        string path = text[contentStart..closeTag];
                        int lastSlash = path.LastIndexOf('/');
                        string fileName = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
                        builder.Append('[').Append(fileName).Append(']');
                        index = closeTag + 6;
                        continue;
                    }
                }

                int tagEnd = text.IndexOf(']', index);
                if (tagEnd >= 0)
                {
                    index = tagEnd + 1;
                    continue;
                }
            }

            builder.Append(text[index]);
            index++;
        }

        return builder.ToString();
    }
}
