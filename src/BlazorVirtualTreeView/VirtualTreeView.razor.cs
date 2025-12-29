using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Diagnostics;

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

        #region Parameters

        // =====================================================
        // Data & required inputs
        // =====================================================

        /// <summary>
        /// The top-level nodes displayed by the tree. Provide a stable collection of root nodes.
        /// The component reads and renders this collection as a starting point. Each node must have a
        /// unique <see cref="VirtualTreeViewNode{T}.Path"/> so programmatic navigation works.
        /// </summary>
        [Parameter, EditorRequired]
        public IReadOnlyList<VirtualTreeViewNode<T>> Roots { get; set; }
            = Array.Empty<VirtualTreeViewNode<T>>();

        /// <summary>
        /// Callback used to lazy-load children for a node. The component calls this function when
        /// a node is expanded for the first time. Return an ordered list of child nodes.
        /// </summary>
        [Parameter, EditorRequired]
        public Func<VirtualTreeViewNode<T>, Task<IReadOnlyList<VirtualTreeViewNode<T>>>> LoadChildren { get; set; }
            = default!;


        // =====================================================
        // Events & interaction callbacks
        // =====================================================

        /// <summary>
        /// Event invoked when the selected node changes.
        /// </summary>
        [Parameter]
        public EventCallback<VirtualTreeViewNode<T>> SelectedNodeChanged { get; set; }

        /// <summary>
        /// Event invoked when the user opens a context menu on a node (right-click).
        /// The callback receives the mouse event args and the node under the pointer.
        /// </summary>
        [Parameter]
        public EventCallback<(MouseEventArgs MouseArgs, VirtualTreeViewNode<T> Node)> OnNodeContextMenu { get; set; }


        // =====================================================
        // Tree structure & behavior
        // =====================================================

        /// <summary>
        /// When true, the tree renders a single synthetic root node that
        /// contains all provided <see cref="Roots"/> as its children.
        /// </summary>
        [Parameter]
        public bool ShowRootNode { get; set; } = false;

        /// <summary>
        /// Display text for the synthetic root node.
        /// Default: "Root".
        /// </summary>
        [Parameter]
        public string RootNodeText { get; set; } = "Root";

        /// <summary>
        /// Optional icon for the synthetic root node.
        /// If null, normal folder logic applies.
        /// </summary>
        [Parameter]
        public string? RootNodeIcon { get; set; } = "folder_open";

        /// <summary>
        /// If true, a double-click on a node will expand or collapse it.
        /// </summary>
        [Parameter]
        public bool ExpandNodeOnDoubleClick { get; set; } = false;


        // =====================================================
        // Icons
        // =====================================================

        /// <summary>
        /// Icon used for nodes that can have children but are currently collapsed.
        /// Default: "folder".
        /// </summary>
        [Parameter]
        public string CollapsedNodeIcon { get; set; } = "folder";

        /// <summary>
        /// Icon used for nodes that can have children and are currently expanded.
        /// Default: "folder_open".
        /// </summary>
        [Parameter]
        public string ExpandedNodeIcon { get; set; } = "folder_open";

        /// <summary>
        /// Icon used for leaf nodes (nodes that cannot have children).
        /// Default: "Description".
        /// </summary>
        [Parameter]
        public string LeafNodeIcon { get; set; } = "Description";


        // =====================================================
        // Layout, sizing & scrolling
        // =====================================================

        /// <summary>
        /// Visual size variant for the tree. Affects row height, icon size, and indent.
        /// Default: <see cref="VirtualTreeViewSize.Medium"/>.
        /// </summary>
        [Parameter]
        public VirtualTreeViewSize Size { get; set; } = VirtualTreeViewSize.Medium;

        /// <summary>
        /// CSS height for the tree container (for example "500px" or "50vh").
        /// Used to calculate the virtualization viewport.
        /// </summary>
        [Parameter]
        public string Height { get; set; } = "500px";

        /// <summary>
        /// CSS width for the tree container (for example "500px" or "100%").
        /// </summary>
        [Parameter]
        public string Width { get; set; } = "500px";

        /// <summary>
        /// Controls how the tree attempts to align a scrolled-to node in the viewport.
        /// Default is <see cref="ScrollAlignments.Center"/>.
        /// </summary>
        [Parameter]
        public ScrollAlignments ScrollAlignment { get; set; } = ScrollAlignments.Center;

        /// <summary>
        /// When true, programmatic scrolling uses smooth animated scrolling where supported.
        /// </summary>
        [Parameter]
        public bool SmoothScrolling { get; set; } = true;



        #endregion

        private ElementReference _containerRef;

        /// <summary>
        /// Single authoritative internal root that everything attaches to.
        /// This is never shown unless <see cref="ShowRootNode"/> is enabled (then it becomes the visible root row).
        /// </summary>
        private readonly VirtualTreeViewNode<T> _syntheticRoot = new()
        {
            Text = "Root",
            Path = string.Empty,
            IsLeafNode = false,
            Level = -1
        };

        private bool _pendingScrollToTop;

        /// <summary>
        /// Currently selected node in the tree, or null when nothing is selected.
        /// Read-only; use <see cref="SelectedNodeChanged"/> event and component APIs to update.
        /// </summary>
        public VirtualTreeViewNode<T>? SelectedNode {get; internal set;}

        private VirtualTreeViewNode<T>? _pendingScrollTarget;

        private bool _scrollRequested;
        private bool _isToggleInProgress;
        private bool _suppressAutoScroll;

        private List<VirtualTreeViewNode<T>> _visibleNodes = new();

        private bool _rootsInitialized;

        // Tracks last-known parameter values so runtime toggles trigger a rebuild.
        private bool _lastShowRootNode;

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


        #region Component Lifecyle

        protected override void OnParametersSet()
        {
            // Always sync visual-only properties
            _syntheticRoot.Text = RootNodeText;

            bool showRootChanged = _lastShowRootNode != ShowRootNode;

            if (!_rootsInitialized)
            {
                InitializeRoots();
                return;
            }

            if (showRootChanged)
            {
                _lastShowRootNode = ShowRootNode;
                if (ShowRootNode && _syntheticRoot.Children?.Count > 0)
                    _syntheticRoot.IsExpanded = true;
                RebuildVisibleNodes();
            }
        }


        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (_pendingScrollToTop)
            {
                _pendingScrollToTop = false;

                await JS.InvokeVoidAsync(
                    "lazyTreeScrollToTop",
                    _containerRef);
            }


            if (!_scrollRequested || _pendingScrollTarget == null)
                return;

            _scrollRequested = false;

            int index = _visibleNodes.IndexOf(_pendingScrollTarget);
            if (index >= 0)
            {
                await JS.InvokeVoidAsync(
                    "lazyTreeScrollToIndex",
                    _containerRef,
                    index,
                    RowHeight);
            }

            await JS.InvokeVoidAsync(
                "lazyTreeScrollToNode",
                _containerRef,
                _pendingScrollTarget.DomId,
                SmoothScrolling,
                ScrollAlignment == ScrollAlignments.Center ? "center" : "start");
        }


        /// <summary>
        /// Initializes the root nodes of the tree structure and updates the synthetic root's state.
        /// </summary>
        /// <remarks>Call this method to reset and attach all root nodes to the synthetic root. This
        /// method also updates the visibility and expansion state of the root node collection. Intended for internal
        /// use during tree setup or refresh operations.</remarks>
        private void InitializeRoots()
        {
            _syntheticRoot.Children = null;

            foreach (var node in Roots)
                AttachNodeInternal(node, _syntheticRoot);

            _syntheticRoot.ChildrenLoaded = true;
            _syntheticRoot.IsExpanded = Roots.Count > 0;

            _rootsInitialized = true;
            _lastShowRootNode = ShowRootNode;

            RebuildVisibleNodes();
        }

        #endregion


        #region Public API: navigation & mutations

        /// <summary>
        /// Programmatically selects the node at the specified path, expanding ancestor nodes as needed. 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
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
            VirtualTreeViewNode<T>? scrollTarget = null;

            // Suppress automatic scroll while unfolding the ancestor chain so we don't
            // jump the viewport between intermediate expansions.
            _suppressAutoScroll = true;
            try
            {
                // Ensure the root is expanded / its children are loaded.
                if (!current.IsExpanded)
                    await ToggleAsync(current);

                // Allow the render cycle to pick up newly materialized children
                // (Virtualize may need a render pass to reflect updated children).
                await InvokeAsync(StateHasChanged);
                await Task.Yield();

                for (int i = 1; i < segments.Length; i++)
                {
                    // If children are not yet materialized, expand current to trigger load.
                    if (current.Children == null)
                        await ToggleAsync(current);

                    // Give the renderer a chance to update visible structures so the next child can be found.
                    await InvokeAsync(StateHasChanged);
                    await Task.Yield();

                    var next = current.Children?
                        .FirstOrDefault(c =>
                            SegmentEquals(GetLastPathSegment(c.Path), segments[i]));

                    if (next == null)
                        return false;

                    current = next;

                    if (!current.IsExpanded)
                        await ToggleAsync(current);

                    // After expanding, let the UI stabilize before continuing deeper.
                    await InvokeAsync(StateHasChanged);
                    await Task.Yield();
                }

                await OnSelect(current);

                // Record the node we want to scroll to — do NOT call QueueScrollToNode while suppressed.
                scrollTarget = current;

                return true;
            }
            finally
            {
                // Lift suppression first, then queue the scroll so QueueScrollToNode can set the flags.
                _suppressAutoScroll = false;

                if (scrollTarget != null)
                {
                    QueueScrollToNode(scrollTarget);

                    // Force a render so OnAfterRenderAsync runs and the JS scroll is invoked.
                    // Virtualize needs at least one render pass to materialize the target DOM row.
                    await InvokeAsync(StateHasChanged);
                }
            }
        }


        /// <summary>
        /// Adds a new node under the specified parent node.
        /// If <paramref name="parentNode"/> is null, the node is added to the root.
        /// </summary>
        /// <param name="newNode">The node to add.</param>
        /// <param name="parentNode">
        /// Optional explicit parent to attach to. If null, the node is added to the root.
        /// </param>
        /// <param name="selectNewNode">
        /// If true, the newly added node is selected and scrolled into view.
        /// </param>
        public void AddNode(
         VirtualTreeViewNode<T> newNode,
         VirtualTreeViewNode<T>? parentNode = null,
         bool selectNewNode = true)
        {
            if (newNode is null)
                throw new ArgumentNullException(nameof(newNode));

            // If no parent specified, attach to the internal root
            var parent = parentNode ?? _syntheticRoot;

            AttachNodeInternal(newNode, parent);
            parent.IsExpanded = true;

            if (selectNewNode)
            {
                SelectedNode = newNode;
                QueueScrollToNode(newNode);
            }

            RebuildVisibleNodes();
            StateHasChanged();
        }

        /// <summary>
        /// Removes the specified node from the tree.
        /// If <paramref name="node"/> is null, the currently selected node is removed.
        /// Only affects already-materialized nodes.
        /// </summary>
        /// <param name="node">
        /// Optional explicit node to remove. If null, the selected node is used.
        /// </param>
        public void RemoveNode(VirtualTreeViewNode<T>? node = null)
        {
            var target = node ?? SelectedNode;
            if (target == null)
                return;

            // Never allow removing the internal root
            if (ReferenceEquals(target, _syntheticRoot))
                return;

            var parent = target.Parent;
            if (parent?.Children == null)
                return;

            // Clear pending scroll — the node will no longer exist
            _pendingScrollTarget = null;
            _scrollRequested = false;

            // Remove from parent's children
            if (!parent.Children.Remove(target))
                return;

            // Clear selection if we removed it
            if (ReferenceEquals(SelectedNode, target))
                SelectedNode = null;

            RebuildVisibleNodes();
            StateHasChanged();
        }

        /// <summary>
        /// Refreshes the currently selected node by reloading its children.
        /// Only affects already-loaded state and does not refresh the entire tree.
        /// </summary>
        public async Task RefreshSelectedAsync()
        {
            var node = SelectedNode;
            if (node == null)
                return;

            node.Children = null;
            node.IsExpanded = true;
            node.IsLoading = true;

            // IMPORTANT: This tells the ToggleAsync method to call LoadChildren to force load. Without setting to false, no re-load takes place.
            node.ChildrenLoaded = false;

            RebuildVisibleNodes();

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

            RebuildVisibleNodes();
        }

        /// <summary>
        /// Clears the currently selected node in the TreeView
        /// </summary>
        public void ClearSelection()
        {
            SelectedNode = null;
            StateHasChanged();
        }

        /// <summary>
        /// Collapses all root nodes and their loaded descendants in the tree, hiding their child nodes from view.
        /// </summary>
        /// <remarks>This method affects only nodes that are currently loaded. Unloaded or virtualized
        /// nodes are not affected. After calling this method, only the root nodes will remain visible in the
        /// tree.</remarks>
        public void CollapseAll()
        {
            _pendingScrollTarget = null;
            _scrollRequested = false;

            foreach (var root in Roots)
                CollapseNodeRecursivelyLoadedOnly(root);

            RebuildVisibleNodes();

            _pendingScrollToTop = true;
            StateHasChanged();

        }


        #endregion


        #region Event Handlers & Interactions 

        /// <summary>
        /// An internal handler for when a row is clicked. Raises selection and toggles expansion as needed.
        /// </summary>
        private async Task OnRowClicked(VirtualTreeViewNode<T> node)
        {
            if (_isToggleInProgress)
                return;

            _suppressAutoScroll = true;

            await OnSelect(node);

            if (!ExpandNodeOnDoubleClick)
                await ToggleAsync(node);

            _suppressAutoScroll = false;
        }

        /// <summary>
        /// An internal handler for when a row is double-clicked. Raises selection and toggles expansion as needed.
        /// </summary>
        private async Task OnRowDoubleClicked(VirtualTreeViewNode<T> node)
        {
            if (_isToggleInProgress)
                return;

            _suppressAutoScroll = true;

            await OnSelect(node);

            if (ExpandNodeOnDoubleClick)
                await ToggleAsync(node);

            _suppressAutoScroll = false;
        }

        /// <summary>
        /// internal handler for when a context menu is requested on a node. Raises the external event for consumers.
        /// </summary>
        private async Task OnContextMenuAsync(MouseEventArgs e, VirtualTreeViewNode<T> node)
        {
            if (OnNodeContextMenu.HasDelegate)
                await OnNodeContextMenu.InvokeAsync((e, node));
        }

        /// <summary>
        /// An internal handler for when a node is selected. Raises the external event for consumers.
        /// </summary>
        private async Task OnSelect(VirtualTreeViewNode<T> node)
        {
            SelectedNode = node;

            if (SelectedNodeChanged.HasDelegate)
                await SelectedNodeChanged.InvokeAsync(node);
        }

        /// <summary>
        /// The internal handler for toggling a node's expanded/collapsed state and whether to load its children.
        /// </summary>
        private async Task ToggleAsync(VirtualTreeViewNode<T> node)
        {
            if (_isToggleInProgress)
                return;

            if (node.IsLeafNode)
                return;

            _isToggleInProgress = true;

            try
            {
                // Root row toggle should work too when shown
                if (ReferenceEquals(node, _syntheticRoot) && !ShowRootNode)
                    return;

                if (!node.IsExpanded)
                {
                    RebuildVisibleNodes();
                    await InvokeAsync(StateHasChanged);

                    if (!node.ChildrenLoaded)
                    {
                        node.IsLoading = true;

                        try
                        {
                            var children = await LoadChildren(node);

                            node.ChildrenLoaded = true;
                            node.Children = new List<VirtualTreeViewNode<T>>(children.Count);
                            foreach (var child in children)
                                AttachNodeInternal(child, node);
                        }
                        finally
                        {
                            node.IsLoading = false;
                        }
                    }
                }

                if (node.IsExpanded)
                {
                    node.IsExpanded = false;
                }
                else
                {
                    node.IsExpanded = node.Children != null && node.Children.Count > 0; // Can't expand a childless parent
                }

                RebuildVisibleNodes();
                await InvokeAsync(StateHasChanged);
            }
            finally
            {
                _isToggleInProgress = false;
            }
        }


        #endregion


        #region internal helpers 

        private void AttachNodeInternal(VirtualTreeViewNode<T> node, VirtualTreeViewNode<T> parent)
        {
            // Always set Parent so selection, traversal, and assertions are correct.
            node.Parent = parent;

            // Levels: internal root is -1; its children become 0.
            node.Level = parent.Level + 1;

            parent.Children ??= new List<VirtualTreeViewNode<T>>();
            parent.Children.Add(node);

            DebugAssertNodeAttached(node);
        }

        private void RebuildVisibleNodes()
        {
            // Preallocate capacity to reduce reallocations during frequent rebuilds.
            // 256 is a heuristic, not a hard limit.
            var list = new List<VirtualTreeViewNode<T>>(capacity: 256);

            // When ShowRootNode is enabled, we render the internal root row as level 0 visually.
            // Otherwise we render root children directly.
            if (ShowRootNode)
            {
                list.Add(_syntheticRoot);

                if (_syntheticRoot.Children != null && _syntheticRoot.IsExpanded)
                    FlattenExpanded(_syntheticRoot.Children, list);
            }
            else
            {
                if (_syntheticRoot.Children != null)
                    FlattenExpanded(_syntheticRoot.Children, list);
            }

            _visibleNodes = list;
        }

        /// <summary>
        /// Flattens a hierarchical tree structure into a linear list containing only
        /// the currently visible nodes.
        /// </summary>
        private static void FlattenExpanded(
                List<VirtualTreeViewNode<T>> nodes,
                List<VirtualTreeViewNode<T>> output)
        {
            foreach (var node in nodes)
            {
                output.Add(node);

                if (node.IsExpanded && node.Children != null && node.Children.Count > 0)
                    FlattenExpanded(node.Children, output);
            }
        }

        /// <summary>
        /// Determines the appropriate icon to display for the specified tree node based on its state.
        /// </summary>
        private string ResolveNodeIcon(VirtualTreeViewNode<T> node)
        {
            if (node.IsLeafNode)
                return LeafNodeIcon;

            return (node.ChildrenLoaded && node.IsExpanded) ? ExpandedNodeIcon : CollapsedNodeIcon;
        }

        /// <summary>
        /// Determines the appropriate icon identifier to display for expanding or collapsing the specified tree node.
        /// </summary>
        private string ResolveExpandIcon(VirtualTreeViewNode<T> node)
        {
            if (node.IsLeafNode)
                return string.Empty;

            if (node.IsLoading)
                return "progress_activity";

            if (node.ChildrenLoaded && (node.Children == null || node.Children.Count == 0))
                return string.Empty;

            return node.IsExpanded ? "expand_more" : "chevron_right";
        }

        #endregion


        #region Utilities

        /// <summary>
        /// Collapses the specified node and all of its loaded descendant nodes in the virtual tree view.
        /// </summary>
        private void CollapseNodeRecursivelyLoadedOnly(VirtualTreeViewNode<T> node)
        {
            node.IsExpanded = false;
            node.IsLoading = false;

            if (node.Children == null)
                return;

            foreach (var child in node.Children)
                CollapseNodeRecursivelyLoadedOnly(child);
        }

        /// <summary>
        /// Queues a request to scroll the view to the specified node in the virtual tree.
        /// </summary>
        private void QueueScrollToNode(VirtualTreeViewNode<T> node)
        {
            if (_suppressAutoScroll)
                return;

            _pendingScrollTarget = node;
            _scrollRequested = true;
        }

        /// <summary>
        /// Extracts the last segment from a path string, using the forward slash ('/') as a separator.
        /// </summary>
        private static string GetLastPathSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? string.Empty : parts[^1];
        }

        /// <summary>
        /// Checks if two path segments are equal, ignoring case sensitivity.
        /// </summary>
        private static bool SegmentEquals(string seg1, string seg2)
            => string.Equals(seg1, seg2, StringComparison.OrdinalIgnoreCase);

        #endregion


        #region Debug

        [Conditional("DEBUG")]
        private void DebugAssertNodeAttached(VirtualTreeViewNode<T> node)
        {
            //Debug.Assert(
            //    node.Parent != null,
            //    "VirtualTreeView invariant violated: node.Parent is null after attachment.");

            //Debug.Assert(
            //    IsDescendantOf(node, _syntheticRoot),
            //    "VirtualTreeView invariant violated: node is not attached to the internal root. " +
            //    "This will break selection and virtualization (the node may render but cannot be found for selection).");
        }

        #endregion
    }
}