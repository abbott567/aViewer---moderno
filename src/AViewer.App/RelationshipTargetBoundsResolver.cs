using AViewer.Core.Models;

namespace AViewer.App;

/// <summary>
/// Recovers usable relationship-target bounds from the current accessibility
/// snapshots when a provider returns a relationship object with an empty rectangle.
/// </summary>
internal static class RelationshipTargetBoundsResolver
{
    private const double BoundsTolerance = 2;

    public static List<AccessibilityRelationship> Resolve(
        IEnumerable<AccessibilityRelationship> relationships,
        AccessibilityNode source,
        params AccessibilityNode?[] roots)
    {
        var candidates = roots
            .Where(root => root is not null)
            .SelectMany(root => Flatten(root!))
            .Where(HasDrawableBounds)
            .Where(candidate => !IsSourceNode(candidate, source))
            .ToArray();

        var resolved = new List<AccessibilityRelationship>();
        foreach (var relationship in relationships)
        {
            if (HasDrawableBounds(relationship))
            {
                resolved.Add(relationship);
                continue;
            }

            var target = FindBestTarget(relationship, source, candidates);
            resolved.Add(target is null
                ? relationship
                : relationship with
                {
                    TargetX = target.BoundingX,
                    TargetY = target.BoundingY,
                    TargetWidth = target.BoundingWidth,
                    TargetHeight = target.BoundingHeight
                });
        }

        return resolved;
    }

    private static AccessibilityNode? FindBestTarget(
        AccessibilityRelationship relationship,
        AccessibilityNode source,
        IReadOnlyList<AccessibilityNode> candidates)
    {
        if (!string.IsNullOrWhiteSpace(relationship.TargetId))
        {
            var idMatch = candidates.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    relationship.TargetId,
                    StringComparison.Ordinal));
            if (idMatch is not null)
            {
                return idMatch;
            }
        }

        var sourceCentreX = source.BoundingX + (source.BoundingWidth / 2);
        var sourceCentreY = source.BoundingY + (source.BoundingHeight / 2);

        return candidates
            .Select(candidate => new
            {
                Node = candidate,
                Score = MatchScore(relationship, candidate),
                Distance = DistanceFrom(
                    sourceCentreX,
                    sourceCentreY,
                    candidate.BoundingX + (candidate.BoundingWidth / 2),
                    candidate.BoundingY + (candidate.BoundingHeight / 2))
            })
            .Where(item => item.Score >= 4)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Distance)
            .Select(item => item.Node)
            .FirstOrDefault();
    }

    private static int MatchScore(
        AccessibilityRelationship relationship,
        AccessibilityNode candidate)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(relationship.TargetName) &&
            string.Equals(
                relationship.TargetName,
                candidate.Name,
                StringComparison.Ordinal))
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(relationship.TargetControlType) &&
            string.Equals(
                relationship.TargetControlType,
                candidate.ControlType,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        return score;
    }

    private static double DistanceFrom(
        double x1,
        double y1,
        double x2,
        double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static IEnumerable<AccessibilityNode> Flatten(AccessibilityNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsSourceNode(
        AccessibilityNode candidate,
        AccessibilityNode source)
    {
        if (!string.IsNullOrWhiteSpace(candidate.Id) &&
            !string.IsNullOrWhiteSpace(source.Id) &&
            string.Equals(candidate.Id, source.Id, StringComparison.Ordinal))
        {
            return true;
        }

        return ApproximatelySameBounds(candidate, source);
    }

    private static bool ApproximatelySameBounds(
        AccessibilityNode first,
        AccessibilityNode second) =>
        Math.Abs(first.BoundingX - second.BoundingX) <= BoundsTolerance &&
        Math.Abs(first.BoundingY - second.BoundingY) <= BoundsTolerance &&
        Math.Abs(first.BoundingWidth - second.BoundingWidth) <= BoundsTolerance &&
        Math.Abs(first.BoundingHeight - second.BoundingHeight) <= BoundsTolerance;

    private static bool HasDrawableBounds(AccessibilityRelationship relationship) =>
        relationship.TargetWidth > 0 &&
        relationship.TargetHeight > 0 &&
        IsFinite(relationship.TargetX) &&
        IsFinite(relationship.TargetY) &&
        IsFinite(relationship.TargetWidth) &&
        IsFinite(relationship.TargetHeight);

    private static bool HasDrawableBounds(AccessibilityNode node) =>
        node.BoundingWidth > 0 &&
        node.BoundingHeight > 0 &&
        IsFinite(node.BoundingX) &&
        IsFinite(node.BoundingY) &&
        IsFinite(node.BoundingWidth) &&
        IsFinite(node.BoundingHeight);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
