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


    #region Icons

    /// <summary>
    /// Icon used when this node can have children (IsLeafNode is false) and is collapsed.
    /// Default: "folder".
    /// </summary>
    public string CollapsedIcon { get; set; } = "folder";

    /// <summary>
    /// Icon used when this node can have children (IsLeafNode is false) and is expanded.
    /// Default: "folder_open".
    /// </summary>
    public string ExpandedIcon { get; set; } = "folder_open";

    /// <summary>
    /// Icon used when this node cannot have children (IsLeafNode is true).
    /// Default: "unknown_document".
    /// </summary>
    public string LeafIcon { get; set; } = "unknown_document";

    #endregion


    #region Tree Structure

    /// <summary>
    /// Optional parent reference (runtime-only).
    /// </summary>
    public VirtualTreeViewNode<T>? Parent { get; internal set; }

    /// <summary>
    /// Depth level in the tree (root = 0).
    /// </summary>
    public int Level { get; internal set; }

    /// <summary>
    /// Indicates whether this node is a leaf (cannot have children).
    /// Controls expander visibility and lazy loading when inverted in component logic.
    /// </summary>
    public bool IsLeafNode { get; set; }

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
