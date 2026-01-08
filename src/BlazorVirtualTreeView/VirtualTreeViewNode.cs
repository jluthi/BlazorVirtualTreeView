namespace BlazorVirtualTreeView
{
    public class VirtualTreeViewNode<T>
    {
        #region Identity

        /// <summary>
        /// Stable DOM id used for virtualization and scroll targeting. Generated independently of <see cref="Key"/> so the user key is not exposed in the rendered markup.
        /// </summary>
        internal string DomId { get; } = $"tree-node-{Guid.NewGuid():N}";

        /// <summary>
        /// User-defined identifier for this node. Must be unique among siblings (nodes at the same level under the same parent).
        /// </summary>
        public string Key { get; init; } = string.Empty;


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
        public VirtualTreeViewNode<T>? Parent { get; set; }

        /// <summary>
        /// Tree-managed hierarchical path (computed from Parent chain).
        /// </summary>
        public string Path => Parent == null
            ? Key
            : string.IsNullOrEmpty(Parent.Path) ? Key : $"{Parent.Path}/{Key}";

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
        /// True when this node is the tree's current selection.
        /// </summary>
        public bool IsSelected { get; internal set; }

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

}