using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorVirtualTreeView
{
    /// <summary>
    /// A reusable virtualized tree view component that supports lazy-loading of child nodes,
    /// selection, expansion/collapse, and programmatic scrolling to a node.
    /// </summary>
    public partial class VirtualTreeView<T> : ComponentBase
    {
        // Injected Services
        [Inject]
        private IJSRuntime JS { get; set; } = default!;

        // Parameters
        /// <summary>
        /// The top-level nodes displayed by the tree. Provide a stable collection of root nodes.
        /// The component reads and renders this collection as a starting point. Each node should have a
        /// unique <see cref="VirtualTreeViewNode{T}.Path"/> so programmatic navigation works.
        /// </summary>
        [Parameter, EditorRequired]
        public IReadOnlyList<VirtualTreeViewNode<T>> Roots { get; set; }
            = Array.Empty<VirtualTreeViewNode<T>>();

        /// <summary>
        /// Callback used to lazy-load children for a node. The component calls this function when
        /// a node is expanded for the first time. Return an ordered list of child nodes. This populates the component.
        /// </summary>
        [Parameter, EditorRequired]
        public Func<VirtualTreeViewNode<T>, Task<IReadOnlyList<VirtualTreeViewNode<T>>>> LoadChildren { get; set; }
            = default!;

        /// <summary>
        /// Event invoked when the selected node changes. Use this to synchronize selection with
        /// parent state or to react to user selection.
        /// </summary>
        [Parameter]
        public EventCallback<VirtualTreeViewNode<T>> SelectedNodeChanged { get; set; }

        /// <summary>
        /// Event invoked when the user opens a context menu on a node (right-click).
        /// The callback receives the mouse event args and the node under the pointer.
        /// </summary>
        [Parameter]
        public EventCallback<(MouseEventArgs MouseArgs, VirtualTreeViewNode<T> Node)> OnNodeContextMenu { get; set; }

        /// <summary>
        /// Controls how the tree attempts to align a scrolled-to node in the viewport.
        /// Default is <see cref="ScrollAlignments.Center"/>.
        /// </summary>
        [Parameter]
        public ScrollAlignments ScrollAlignment { get; set; } = ScrollAlignments.Center;

        /// <summary>
        /// Material icon name used for nodes that can have children but are currently collapsed.
        /// Default: "folder".
        /// </summary>
        [Parameter] public string CollapsedNodeIcon { get; set; } = "folder";
        
        /// <summary>
        /// Material icon name used for nodes that can have children and are currently expanded.
        /// Default: "folder_open".
        /// </summary>
        [Parameter] public string ExpandedNodeIcon { get; set; } = "folder_open";

        /// <summary>
        /// Material icon name used for leaf nodes (nodes that cannot have children).
        /// Default: "Description".
        /// </summary>
        [Parameter] public string LeafNodeIcon { get; set; } = "Description";

        /// <summary>
        /// If true, a double-click on a node will expand/collapse it. When false, a double-click
        /// will not toggle expansion and a single click may toggle depending on settings.
        /// </summary>
        [Parameter] public bool ExpandOnNodeDoubleClick { get; set; }

        /// <summary>
        /// CSS height for the tree container (for example "500px" or "50vh"). Used to calculate
        /// virtualization viewport and scrolling.
        /// </summary>
        [Parameter] public string Height { get; set; } = "500px";

        /// <summary>
        /// CSS width for the tree container (for example "500px" or "100%").
        /// </summary>
        [Parameter] public string Width { get; set; } = "500px";

        /// <summary>
        /// Visual size variant for the tree. Affects row height, icon size and indent.
        /// Default: <see cref="VirtualTreeViewSize.Medium"/>.
        /// </summary>
        [Parameter]
        public VirtualTreeViewSize Size { get; set; } = VirtualTreeViewSize.Medium;

        /// <summary>
        /// When true, scrolling performed by the component (programmatic scroll-to-node) will
        /// use smooth animated scrolling where supported by the browser.
        /// </summary>
        [Parameter]
        public bool SmoothScrolling { get; set; } = true;

        // Public state
        /// <summary>
        /// Currently selected node in the tree, or null when nothing is selected.
        /// Read-only; use <see cref="SelectedNodeChanged"/> event and component APIs to update.
        /// </summary>
        public VirtualTreeViewNode<T>? SelectedNode => _selectedNode;

        /// <summary>
        /// Number of nodes currently represented in the virtualized viewport. This reflects the
        /// flattened (visible) node count after expansion state is applied and is useful for
        /// debugging or telemetry in demos.
        /// </summary>
        public int VirtualizedNodeCount => _virtualizedNodeCount;

        // Private state
        private VirtualTreeViewNode<T>? _selectedNode;
        private VirtualTreeViewNode<T>? _pendingScrollTarget;

        private bool _scrollRequested;
        private bool _isToggleInProgress;
        private bool _suppressAutoScroll;

        private int _virtualizedNodeCount;

        private readonly List<VirtualTreeViewNode<T>> _visibleNodes = new();

        // Layout helpers
        private int RowHeight => Size switch
        {
            VirtualTreeViewSize.Small => 24,
            VirtualTreeViewSize.Medium => 28,
            VirtualTreeViewSize.Large => 34,
            _ => 28
        };

        private int IconSizePx => Size switch
        {
            VirtualTreeViewSize.Small => 18,
            VirtualTreeViewSize.Medium => 20,
            VirtualTreeViewSize.Large => 24,
            _ => 20
        };

        private int IndentSizePx => Size switch
        {
            VirtualTreeViewSize.Small => 14,
            VirtualTreeViewSize.Medium => 18,
            VirtualTreeViewSize.Large => 22,
            _ => 18
        };

        protected override void OnParametersSet() => Rebuild();
     
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {

            if (!_scrollRequested || _pendingScrollTarget == null)
                return;

            _scrollRequested = false;

            int index = _visibleNodes.IndexOf(_pendingScrollTarget);
            if (index >= 0)
            {
                await JS.InvokeVoidAsync(
                    "lazyTreeScrollToIndex",
                    "lazy-virt-tree-container",
                    index,
                    RowHeight);
            }

            await JS.InvokeVoidAsync(
                "lazyTreeScrollToNode",
                "lazy-virt-tree-container",
                _pendingScrollTarget.DomId,
                SmoothScrolling,
                ScrollAlignment == ScrollAlignments.Center ? "center" : "start");
        }

        // Rendering helpers
        private string ResolveNodeIcon(VirtualTreeViewNode<T> node)
        {
            if (!node.CanHaveChildren)
                return LeafNodeIcon;

            return (node.Children != null && node.Children.Count > 0) ? ExpandedNodeIcon : CollapsedNodeIcon;
        }


        private string ResolveExpandIcon(VirtualTreeViewNode<T> node)
        {
            if (!node.CanHaveChildren)
                return string.Empty; // or null / placeholder

            if (node.IsLoading)
                return "progress_activity";

            if (node.ChildrenLoaded && (node.Children == null || node.Children.Count == 0))
            {
                return string.Empty;
            }

            return node.IsExpanded ? "expand_more" : "chevron_right";
        }


        // UI event handlers
        private async Task OnRowClicked(VirtualTreeViewNode<T> node)
        {
            if (_isToggleInProgress)
                return;

            _suppressAutoScroll = true;

            await OnSelect(node);

            if (!ExpandOnNodeDoubleClick)
                await ToggleAsync(node);

            _suppressAutoScroll = false;
        }

        private async Task OnRowDoubleClicked(VirtualTreeViewNode<T> node)
        {
            if (_isToggleInProgress)
                return;

            _suppressAutoScroll = true;

            await OnSelect(node);

            if (ExpandOnNodeDoubleClick)
                await ToggleAsync(node);

            _suppressAutoScroll = false;
        }

        private async Task OnContextMenuAsync(MouseEventArgs e, VirtualTreeViewNode<T> node)
        {
            if (OnNodeContextMenu.HasDelegate)
                await OnNodeContextMenu.InvokeAsync((e, node));
        }

        // Tree behavior
        private async Task ToggleAsync(VirtualTreeViewNode<T> node)
        {
            if (_isToggleInProgress)
                return;

            _isToggleInProgress = true;

            try
            {
                if (node.IsExpanded)
                {
                    node.IsExpanded = false;
                    Rebuild();
                    return;
                }
                
                if (node.CanHaveChildren)
                {
                    // Only perform load if it hasn't done it before.
                    if (node.ChildrenLoaded == false)
                    {
                        node.IsLoading = true;
                        Rebuild();

                        var children = await LoadChildren(node);

                        node.Children = children.Count == 0
                            ? null
                            : children.Select(c =>
                            {
                                c.Level = node.Level + 1;
                                return c;
                            }).ToList();


                        node.ChildrenLoaded = true;
                        node.IsLoading = false;
                    }


                    if (node?.Children?.Count > 0)
                    {
                        node.IsExpanded = true;

                    }

                }


                Rebuild();
                QueueScrollToNode(node);
            }
            finally
            {
                _isToggleInProgress = false;
            }
        }

        private async Task OnSelect(VirtualTreeViewNode<T> node)
        {
            _selectedNode = node;

            if (SelectedNodeChanged.HasDelegate)
                await SelectedNodeChanged.InvokeAsync(node);
        }

        private void Rebuild()
        {
            _visibleNodes.Clear();

            // Ensure Parent/Level are correct for whatever graph we currently have
            foreach (var root in Roots)
                NormalizeLinksRecursive(root, parent: null, level: 0);

            foreach (var root in Roots)
                Flatten(root);

            StateHasChanged();
        }

        // Ensure Parent and Level properties are correct in the current tree structure
        private static void NormalizeLinksRecursive(
            VirtualTreeViewNode<T> node,
            VirtualTreeViewNode<T>? parent,
            int level)
        {
            node.Parent = parent;
            node.Level = level;

            if (node.Children == null)
                return;

            foreach (var child in node.Children)
                NormalizeLinksRecursive(child, node, level + 1);
        }


        private void Flatten(VirtualTreeViewNode<T> node)
        {
            _visibleNodes.Add(node);

            if (!node.IsExpanded || node.Children == null)
                return;

            foreach (var child in node.Children)
                Flatten(child);
        }

        // Programmatic navigation
        public async Task<bool> SelectNodeAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var root = Roots.FirstOrDefault(r =>
                SegmentEquals(GetLastPathSegment(r.Path), segments[0]));

            if (root == null)
                return false;

            var current = root;

            if (!current.IsExpanded)
                await ToggleAsync(current);

            for (int i = 1; i < segments.Length; i++)
            {
                if (current.Children == null)
                    await ToggleAsync(current);

                var next = current.Children?
                    .FirstOrDefault(c =>
                        SegmentEquals(GetLastPathSegment(c.Path), segments[i]));

                if (next == null)
                    return false;

                current = next;

                if (!current.IsExpanded)
                    await ToggleAsync(current);
            }

            await OnSelect(current);
            QueueScrollToNode(current);

            return true;
        }

        // Mutation APIs: Add / Remove / Refresh

        /// <summary>
        /// Adds a new node under the currently selected node.
        /// If no node is selected, the node is added to the root level.
        /// </summary>
        public void AddNode(VirtualTreeViewNode<T> newNode, bool selectNewNode = true)
        {
            if (newNode == null)
                throw new ArgumentNullException(nameof(newNode));

            // Case 1: No selection => add to roots
            if (_selectedNode == null)
            {
                if (Roots is not IList<VirtualTreeViewNode<T>> rootList)
                    throw new InvalidOperationException("Roots collection is not mutable.");

                newNode.Level = 0;
                rootList.Add(newNode);

                if (selectNewNode)
                {
                    _selectedNode = newNode;
                    _pendingScrollTarget = newNode;
                    _scrollRequested = true;
                }

                Rebuild();
                return;
            }

            // Case 2: Add as child of selected node
            var parent = _selectedNode;

            parent.Children ??= new List<VirtualTreeViewNode<T>>();

            newNode.Level = parent.Level + 1;
            parent.Children.Add(newNode);

            parent.IsExpanded = true;

            if (selectNewNode)
            {
                _selectedNode = newNode;
                _pendingScrollTarget = newNode;
                _scrollRequested = true;
            }

            Rebuild();
        }

        /// <summary>
        /// Removes the currently selected node from the tree.
        /// Only affects already-loaded nodes.
        /// </summary>
        public void RemoveNode()
        {
            var node = _selectedNode;
            if (node == null)
                return;

            int removedCount = CountLoadedSubtree(node);

            _pendingScrollTarget = null;
            _scrollRequested = false;

            // Case 1: selected node is a root
            if (Roots is IList<VirtualTreeViewNode<T>> rootList && rootList.Remove(node))
            {
                _selectedNode = null;
                Rebuild();
                return;
            }

            // Case 2: selected node is a child of a loaded parent
            foreach (var root in Roots)
            {
                if (RemoveFromParent(root, node))
                {
                    _selectedNode = null;
                    Rebuild();
                    return;
                }
            }
        }

        /// <summary>
        /// Refreshes the currently selected node by reloading its children.
        /// Only affects already-loaded state and does not refresh the entire tree.
        /// </summary>
        public async Task RefreshSelectedAsync()
        {
            var node = _selectedNode;
            if (node == null)
                return;

           
            node.Children = null;
            node.IsExpanded = true;
            node.IsLoading = true;

            // IMPORTANT: if you're reloading, you probably want this false until load completes
            node.ChildrenLoaded = false;

            Rebuild();

            var children = await LoadChildren(node);

            node.Children = children.Count == 0
                ? null
                : children.Select(c =>
                {
                    c.Level = node.Level + 1;
                    return c;
                }).ToList();

            node.ChildrenLoaded = true;


            node.IsLoading = false;

            Rebuild();
        }

        private int CountLoadedSubtree(VirtualTreeViewNode<T> node)
        {
            int count = 1;

            if (node.Children == null)
                return count;

            foreach (var child in node.Children)
                count += CountLoadedSubtree(child);

            return count;
        }

        private bool RemoveFromParent(VirtualTreeViewNode<T> parent, VirtualTreeViewNode<T> target)
        {
            if (parent.Children == null)
                return false;

            if (parent.Children.Remove(target))
            {
                if (parent.Children.Count == 0)
                {
                    parent.Children = null;
                    parent.IsExpanded = false;
                    // NOTE: Consider whether you want to also reset:
                    // parent.ChildrenLoaded = false; (depends on your semantics)
                }

                return true;
            }

            foreach (var child in parent.Children)
            {
                if (RemoveFromParent(child, target))
                    return true;
            }

            return false;
        }

        // Collapse / clear
        public void ClearSelection()
        {
            _selectedNode = null;
            StateHasChanged();
        }

        public void CollapseAll()
        {
            _pendingScrollTarget = null;
            _scrollRequested = false;

            foreach (var root in Roots)
                CollapseNodeRecursivelyLoadedOnly(root);

            Rebuild();
        }

        private void CollapseNodeRecursivelyLoadedOnly(VirtualTreeViewNode<T> node)
        {
            node.IsExpanded = false;
            node.IsLoading = false;

            if (node.Children == null)
                return;

            foreach (var child in node.Children)
                CollapseNodeRecursivelyLoadedOnly(child);
        }

        // Utilities
        private void QueueScrollToNode(VirtualTreeViewNode<T> node)
        {
            if (_suppressAutoScroll)
                return;

            _pendingScrollTarget = node;
            _scrollRequested = true;
        }

        private static string GetLastPathSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? string.Empty : parts[^1];
        }

        private static bool SegmentEquals(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
