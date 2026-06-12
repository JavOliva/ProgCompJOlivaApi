namespace ProgCompJOlivaApi.Controllers.Common;

/// <summary>
/// Reorder payload: the full set of child ids in their desired order. Used to reorder a
/// contest's problems and a training's contests. The list must be a permutation of the
/// items currently linked to the parent.
/// </summary>
public class OrderedIdsRequest
{
    public List<Guid> OrderedIds { get; set; } = [];
}
