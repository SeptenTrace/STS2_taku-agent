using Godot;

namespace TakuAgentMod.State.Support;

internal static class GodotNodeSearch
{
    public static List<T> FindAll<T>(Node start) where T : Node
    {
        var found = new List<T>();
        if (GodotObject.IsInstanceValid(start))
        {
            FindAllRecursive(start, found);
        }

        return found;
    }

    public static List<T> FindAllSortedByPosition<T>(Node start) where T : Control
    {
        List<T> found = FindAll<T>(start);
        found.Sort((left, right) =>
        {
            int rowCompare = left.GlobalPosition.Y.CompareTo(right.GlobalPosition.Y);
            return rowCompare != 0 ? rowCompare : left.GlobalPosition.X.CompareTo(right.GlobalPosition.X);
        });
        return found;
    }

    public static T? FindFirst<T>(Node start) where T : Node
    {
        if (!GodotObject.IsInstanceValid(start))
        {
            return null;
        }

        if (start is T match)
        {
            return match;
        }

        foreach (Node child in start.GetChildren())
        {
            T? result = FindFirst<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static void FindAllRecursive<T>(Node node, List<T> found) where T : Node
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            return;
        }

        if (node is T match)
        {
            found.Add(match);
        }

        foreach (Node child in node.GetChildren())
        {
            FindAllRecursive(child, found);
        }
    }
}
