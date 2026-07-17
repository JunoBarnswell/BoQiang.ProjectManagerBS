namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class FlowiseGraphCursor(IReadOnlyList<FlowiseRuntimeNode> nodes)
{
    private int index;

    internal int Position => index;

    internal IReadOnlyList<FlowiseRuntimeNode> Nodes => nodes;

    internal bool TryMoveNext(out FlowiseRuntimeNode? node)
    {
        if (index >= nodes.Count)
        {
            node = null;
            return false;
        }

        node = nodes[index++];
        return true;
    }
}
