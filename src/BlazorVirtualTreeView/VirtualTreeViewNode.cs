public class VirtualTreeViewNode<T>
{
    #region Identity (immutable)

    /// <summary>
    /// Stable DOM id used for virtualization + scroll targeting.
    /// </summary>
    public string DomId { get; } = $"tree-node-{Guid.NewGuid():N}";

    /// <summary>
    /// Tree-managed hierarchical path
    /// (e.g. "cases/2025/12345/documents").
    /// </summary>
    public string Path { get; init; } = string.Empty;

    #endregion


    #region Domain Data (User Defined)

    /// <summary>
    /// User-defined domain object bound to this node.
    /// </summary>
    public T Value { get; init; } = default!;

    /// <summary>
    /// Display text shown in the tree.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    #endregion


    #region Tree Structure

    /// <summary>
    /// Optional parent reference (runtime-only).
    /// </summary>
    public VirtualTreeViewNode<T>? Parent { get; internal set; }

    /// <summary>
    /// Depth level in the tree (root = 0).
    /// </summary>
    internal int Level { get; set; }

    /// <summary>
    /// Determines whether this node can have children.
    /// Controls expander visibility and lazy loading.
    /// </summary>
    public bool CanHaveChildren { get; set; }

    #endregion


    #region Runtime State

    /// <summary>
    /// Whether this node is currently expanded.
    /// </summary>
    public bool IsExpanded { get; internal set; }

    /// <summary>
    /// Indicates async child loading is in progress.
    /// </summary>
    internal bool IsLoading { get; set; }

    #endregion


    #region Children & Lazy Loading

    /// <summary>
    /// Lazily-loaded child nodes.
    /// </summary>
    public List<VirtualTreeViewNode<T>>? Children { get; internal set; }

    /// <summary>
    /// Indicates whether the child nodes have been loaded.
    /// </summary>
    internal bool ChildrenLoaded { get; set; }

    #endregion
}
