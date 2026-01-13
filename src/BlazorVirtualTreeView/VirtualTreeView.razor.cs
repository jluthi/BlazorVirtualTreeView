using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Linq.Expressions;

namespace BlazorVirtualTreeView
{
    /// <summary>
    /// A reusable virtualized tree view component that supports lazy-loading of child nodes,
    /// selection, expansion/collapse, and programmatic scrolling to a node.
    /// </summary>
    public partial class VirtualTreeView<T> : ComponentBase
    {
        #region Dependencies

        [Inject]
        private IJSRuntime JS { get; set; } = default!;

        #endregion


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
        /// Raised when <see cref="SelectedNode"/> changes. Used by <c>@bind-SelectedNode</c>.
        /// </summary>
        [Parameter]
        public EventCallback<VirtualTreeViewNode<T>?> SelectedNodeChanged { get; set; }

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
        /// Currently selected node in the tree, or null when nothing is selected.
        /// Supports <c>@bind-SelectedNode</c>.
        /// </summary>
        [Parameter]
        public VirtualTreeViewNode<T>? SelectedNode { get; set; }

        /// <summary>
        /// Used for forms/validation scenarios with <c>@bind-SelectedNode</c>.
        /// </summary>
        [Parameter]
        public Expression<Func<VirtualTreeViewNode<T>?>>? SelectedNodeExpression { get; set; }


        /// <summary>
        /// When true, the tree renders a single synthetic root node that
        /// contains all provided <see cref="Roots"/> as its children.
        /// </summary>
        [Parameter]
        public bool ShowRootNode { get; set; } = false;

        /// <summary>
        /// Display text for the synthetic root node. Does nothing if <see cref="ShowRootNode"/> is false.
        /// Default: "Root".
        /// </summary>
        [Parameter]
        public string RootNodeText { get; set; } = "Root";

        /// <summary>
        /// Optional icon for the synthetic root node. Does nothing if <see cref="ShowRootNode"/> is false.
        /// Defaut: "folder_open".
        /// </summary>
        [Parameter]
        public string? RootNodeIcon { get; set; } = "folder_open";

        /// <summary>
        /// If true, a double-click on a node will expand or collapse it.
        /// </summary>
        [Parameter]
        public bool ExpandNodeOnDoubleClick { get; set; } = false;

        // =====================================================
        // Layout, sizing & scrolling
        // =====================================================

        /// <summary>
        /// Visual size variant for the tree view. Affects row height, icon size, indentation, etc. aka node density.
        /// Default: <see cref="VirtualTreeViewSize.Medium"/>.
        /// </summary>
        [Parameter]
        public VirtualTreeViewSize Size { get; set; } = VirtualTreeViewSize.Medium;

        /// <summary>
        /// CSS height for the tree container (for example "500px" or "50vh").
        /// Used to calculate the virtualization viewport.
        /// </summary>
        [Parameter]
        public string Height { get; set; } = "100%";

        /// <summary>
        /// CSS width for the tree container (for example "500px" or "100%").
        /// </summary>
        [Parameter]
        public string Width { get; set; } = "100%";

        /// <summary>
        /// Controls how the tree attempts to align a scrolled-to node in the viewport.
        /// Default is <see cref="ScrollAlignments.Top"/>.
        /// Note: Top alignment is recommended, as it tends to look smother to the user when node load latency is very low (quick).
        /// </summary>
        [Parameter]
        public ScrollAlignments AutoScrollAlignment { get; set; } = ScrollAlignments.Top;

        /// <summary>
        /// When true, programmatic scrolling uses smooth animated scrolling where supported.
        /// </summary>
        [Parameter]
        public bool DisableSmoothScrolling { get; set; } = false;


        /// <summary>
        /// Optional Razor template used to render a node.
        /// If not provided, the default built-in renderer is used.
        /// </summary>
        [Parameter]
        public RenderFragment<VirtualTreeViewNode<T>>? NodeTemplate { get; set; }

        #endregion


        #region Private Members
        private ElementReference _containerRef;

        /// <summary>
        /// Single authoritative internal root that everything attaches to.
        /// This is never shown unless <see cref="ShowRootNode"/> is enabled (then it becomes the visible root row).
        /// </summary>
        private readonly VirtualTreeViewNode<T> _syntheticRoot = new()
        {
            Text = "Root",
            IsLeafNode = false,
            Level = -1
        };

        private bool _pendingScrollToTop;

        private VirtualTreeViewNode<T>? _pendingScrollTarget;

        private bool _scrollRequested;
        private bool _keyboardNavigationRequested;
        private bool _isToggleInProgress;
        private bool _suppressAutoScroll;

        private List<VirtualTreeViewNode<T>> _visibleNodes = new();

        private bool _rootsInitialized;

        // Tracks last-known parameter values so runtime toggles trigger a rebuild.
        private bool _lastShowRootNode;

        // Layout helpers
        // Layout helpers
        private int RowHeight => Size switch
        {
            VirtualTreeViewSize.ExtraSmall => 28,
            VirtualTreeViewSize.Small => 30,
            VirtualTreeViewSize.Medium => 32,
            VirtualTreeViewSize.Large => 35,
            VirtualTreeViewSize.ExtraLarge => 38,
            _ => 28
        };

        private int IndentSizePx => Size switch
        {
            VirtualTreeViewSize.ExtraSmall => 12,
            VirtualTreeViewSize.Small => 14,
            VirtualTreeViewSize.Medium => 16,
            VirtualTreeViewSize.Large => 18,
            VirtualTreeViewSize.ExtraLarge => 20,
            _ => 16
        };

        private int IconSizePx => Size switch
        {
            VirtualTreeViewSize.ExtraSmall => 20,
            VirtualTreeViewSize.Small => 22,
            VirtualTreeViewSize.Medium => 24,
            VirtualTreeViewSize.Large => 26,
            VirtualTreeViewSize.ExtraLarge => 28,
            _ => 16
        };

        private int FontSizePx => Size switch
        {
            VirtualTreeViewSize.ExtraSmall => 12,
            VirtualTreeViewSize.Small => 14,
            VirtualTreeViewSize.Medium => 16,
            VirtualTreeViewSize.Large => 18,
            VirtualTreeViewSize.ExtraLarge => 20,
            _ => 16
        };

        #endregion


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

            // If consumer changed SelectedNode via binding, reflect it in internal flags.
            if (SelectedNode != null && !SelectedNode.IsSelected)
                SelectedNode.IsSelected = true;
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

            // After the index-based materialization, use a robust ensure-visible routine
            // for keyboard navigation to avoid clipping; fall back to scrollToNode otherwise.
            if (_keyboardNavigationRequested)
            {
                // If consumer requested center alignment, always center during keyboard navigation.
                if (AutoScrollAlignment == ScrollAlignments.Center)
                {
                    // Use a direct scrollToNode with center alignment (non-smooth for keyboard ops).
                    await JS.InvokeVoidAsync(
                        "lazyTreeScrollToNode",
                        _containerRef,
                        _pendingScrollTarget.DomId,
                        false, // non-smooth for keyboard
                        "center");
                }
                else
                {
                    // Use padding equal to a row height so the keyboard-focused item doesn't end up just out of view.
                    int topPaddingPx = RowHeight + (RowHeight / 2);
                    await JS.InvokeVoidAsync(
                        "lazyTreeScrollEnsureTopVisible",
                        _containerRef,
                        _pendingScrollTarget.DomId,
                        topPaddingPx); // non-smooth for keyboard
                }
            }
            else
            {
                bool smooth = !DisableSmoothScrolling;
                string align = AutoScrollAlignment == ScrollAlignments.Center ? "center" : "start";

                await JS.InvokeVoidAsync(
                    "lazyTreeScrollToNode",
                    _containerRef,
                    _pendingScrollTarget.DomId,
                    smooth,
                    align);
            }

            // Clear keyboard flag after performing the scroll
            _keyboardNavigationRequested = false;
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
                // If the synthetic root is visible but currently collapsed, expand it first so
                // descendant root nodes become materialized and visible for subsequent toggles.
                if (ShowRootNode && !_syntheticRoot.IsExpanded)
                {
                    var prevSuppress = _suppressAutoScroll;
                    // Allow the viewport to follow the expansion of the synthetic root.
                    _suppressAutoScroll = false;
                    QueueScrollToNode(_syntheticRoot);
                    await InvokeAsync(StateHasChanged);
                    await Task.Yield();
                    _suppressAutoScroll = prevSuppress;

                    await ToggleAsync(_syntheticRoot);

                    // Let the UI stabilize after toggling the synthetic root.
                    await InvokeAsync(StateHasChanged);
                    await Task.Yield();
                }

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
                    {
                        // Allow the viewport to follow progress downwards by temporarily
                        // lifting suppression and requesting a scroll to the current node.
                        var prevSuppress = _suppressAutoScroll;
                        _suppressAutoScroll = false;
                        QueueScrollToNode(current);
                        await InvokeAsync(StateHasChanged);
                        await Task.Yield();
                        _suppressAutoScroll = prevSuppress;

                        await ToggleAsync(current);
                    }

                    // Give the renderer a chance to update visible structures so the next child can be found.
                    await InvokeAsync(StateHasChanged);
                    await Task.Yield();

                    var next = current.Children?
                        .FirstOrDefault(c =>
                            SegmentEquals(GetLastPathSegment(c.Path), segments[i]));

                    if (next == null)
                        return false;

                    current = next;

                    // Before potentially expanding further, allow the viewport to follow this newly selected node
                    // so the user sees progress even if deeper loads are slow.
                    if (!current.IsExpanded)
                    {
                        var prevSuppress = _suppressAutoScroll;
                        _suppressAutoScroll = false;
                        QueueScrollToNode(current);
                        await InvokeAsync(StateHasChanged);
                        await Task.Yield();
                        _suppressAutoScroll = prevSuppress;

                        await ToggleAsync(current);
                    }

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

                    // Ensure the tree container receives focus so keyboard navigation works.
                    // Use JS interop helper to avoid ElementReference.FocusAsync compatibility issues.
                    await JS.InvokeVoidAsync("lazyTreeFocus", _containerRef);
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
                SetSelectedNodeInternal(newNode);
                QueueScrollToNode(newNode);
            }

            RebuildVisibleNodes();
            StateHasChanged();
        }

        /// <summary>
        /// Removes the specified node from the tree.
        /// Only affects already-materialized nodes.
        /// </summary>
        /// <param name="node">
        /// </param>
        public void RemoveNode(VirtualTreeViewNode<T>? node)
        {
            if (node is null)
                throw new ArgumentNullException(nameof(node));

            // Never allow removing the internal root
            if (ReferenceEquals(node, _syntheticRoot))
                return;

            var parent = node.Parent;
            if (parent?.Children == null)
                return;

            // Clear pending scroll — the node will no longer exist
            _pendingScrollTarget = null;
            _scrollRequested = false;

            // Remove from parent's children
            if (!parent.Children.Remove(node))
                return;

            // Clear selection if we removed it
            if (ReferenceEquals(SelectedNode, node))
                SetSelectedNodeInternal(null);

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
            SetSelectedNodeInternal(null);
            StateHasChanged();
        }

        /// <summary>
        /// Collapses all root nodes and their loaded descendants in the tree, hiding their child nodes from view.
        /// By default this also collapses the internal synthetic root; set <paramref name="keepImplicitRootExpanded"/>
        /// to true to preserve the synthetic root's expanded state.
        /// </summary>
        /// <param name="keepImplicitRootExpanded">If true, the internal synthetic root remains expanded.</param>
        /// <remarks>This method affects only nodes that are currently loaded. Unloaded or virtualized
        /// nodes are not affected. After calling this method, only the root nodes will remain visible in the
        /// tree (unless <paramref name="keepImplicitRootExpanded"/> is true).</remarks>
        public void CollapseAll(bool keepImplicitRootExpanded = false)
        {
            _pendingScrollTarget = null;
            _scrollRequested = false;

            foreach (var root in Roots)
                CollapseNodeRecursivelyLoadedOnly(root);

            if (!keepImplicitRootExpanded)
            {
                CollapseNodeRecursivelyLoadedOnly(_syntheticRoot);
            }

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
            SetSelectedNodeInternal(node);

            if (SelectedNodeChanged.HasDelegate)
                await SelectedNodeChanged.InvokeAsync(node);
        }

        /// <summary>
        /// The internal handler for toggling a node's expanded/ccollapsed state and whether to load its children.
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
                        await InvokeAsync(StateHasChanged);
                        await Task.Yield();

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

        private void SetSelectedNodeInternal(VirtualTreeViewNode<T>? node)
        {
            if (ReferenceEquals(SelectedNode, node))
                return;

            if (SelectedNode != null)
                SelectedNode.IsSelected = false;

            SelectedNode = node;

            if (SelectedNode != null)
                SelectedNode.IsSelected = true;
        }

        private void AttachNodeInternal(VirtualTreeViewNode<T> node, VirtualTreeViewNode<T> parent)
        {
            if (node is null)
                throw new ArgumentNullException(nameof(node));

            if (parent is null)
                throw new ArgumentNullException(nameof(parent));

            ValidateNodeKey(node.Key);

            parent.Children ??= new List<VirtualTreeViewNode<T>>();

            if (parent.Children.Any(c => SegmentEquals(c.Key, node.Key)))
            {
                throw new InvalidOperationException(
                    $"Duplicate node Key '{node.Key}' under parent '{parent.Path}'. Keys must be unique among siblings (case-insensitive).");
            }

            // Always set Parent so selection, traversal, and assertions are correct.
            node.Parent = parent;

            // Levels: internal root is -1; its children become 0.
            node.Level = parent.Level + 1;

            parent.Children.Add(node);

        }

        private static void ValidateNodeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("VirtualTreeViewNode Key must be non-empty.");

            if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException($"VirtualTreeViewNode Key '{key}' must not contain leading or trailing whitespace.");

            if (key.Contains('/'))
                throw new InvalidOperationException($"VirtualTreeViewNode Key '{key}' must not contain '/'. '/' is reserved as the path separator.");
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
                return node.LeafIcon;

            return (node.ChildrenLoaded && node.IsExpanded) ? node.ExpandedIcon : node.CollapsedIcon;
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

        /// <summary>
        /// Handles keyboard navigation on the tree container.
        /// Left/Right operate on the parent node: left collapses, right expands into the selected node or first child.
        /// Up/Down move selection through the visible list and scroll when off-screen.
        /// </summary>
        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e is null)
                return;

            // Handle navigation keys.
            switch (e.Key)
            {
                case "ArrowLeft":
                    await HandleArrowLeftAsync();
                    break;

                case "ArrowRight":
                    await HandleArrowRightAsync();
                    break;

                case "ArrowUp":
                    await HandleArrowUpAsync();
                    break;

                case "ArrowDown":
                    await HandleArrowDownAsync();
                    break;
            }
        }

        private async Task HandleArrowLeftAsync()
        {
            var parent = SelectedNode?.Parent;
            if (parent == null)
                return;

            // Do not attempt to toggle the internal synthetic root if it's not shown.
            if (ReferenceEquals(parent, _syntheticRoot) && !ShowRootNode)
                return;

            // If parent is expanded, collapse it. Otherwise move selection to parent.
            if (parent.IsExpanded)
            {
                if (!_isToggleInProgress)
                    await ToggleAsync(parent);

                await OnSelect(parent);
                QueueScrollToNode(parent, keyboard: true);
                await InvokeAsync(StateHasChanged);
            }
            else
            {
                // Parent already collapsed — move selection up to parent.
                await OnSelect(parent);
                QueueScrollToNode(parent, keyboard: true);
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task HandleArrowRightAsync()
        {
            var node = SelectedNode;
            if (node == null)
                return;

            // If selected node is synthetic root and not shown, ignore.
            if (ReferenceEquals(node, _syntheticRoot) && !ShowRootNode)
                return;

            // If the selected node can have children and is collapsed -> expand it.
            if (!node.IsLeafNode && !node.IsExpanded)
            {
                if (!_isToggleInProgress)
                    await ToggleAsync(node);

                // Keep selection on the node after expanding.
                QueueScrollToNode(node, keyboard: true);
                await InvokeAsync(StateHasChanged);
                return;
            }

            // If already expanded, move into first child (if any).
            if (!node.IsLeafNode && node.IsExpanded && node.Children != null && node.Children.Count > 0)
            {
                var firstChild = node.Children[0];
                await OnSelect(firstChild);
                QueueScrollToNode(firstChild, keyboard: true);
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task HandleArrowUpAsync()
        {
            if (_visibleNodes == null || _visibleNodes.Count == 0)
                return;

            int currentIndex = SelectedNode == null
                ? _visibleNodes.Count // will decrement to last
                : _visibleNodes.IndexOf(SelectedNode);

            VirtualTreeViewNode<T>? target = null;

            if (currentIndex <= 0)
            {
                // If at the start or not found, wrap to first element (or simply do nothing).
                // Choose to move to last when no selection, otherwise stay at first.
                if (SelectedNode == null)
                    target = _visibleNodes[^1];
                else
                    target = _visibleNodes[0];
            }
            else
            {
                target = _visibleNodes[currentIndex - 1];
            }

            if (target == null)
                return;

            await OnSelect(target);
            QueueScrollToNode(target, keyboard: true);
            await InvokeAsync(StateHasChanged);
        }

        private async Task HandleArrowDownAsync()
        {
            if (_visibleNodes == null || _visibleNodes.Count == 0)
                return;

            int currentIndex = SelectedNode == null
                ? -1
                : _visibleNodes.IndexOf(SelectedNode);

            VirtualTreeViewNode<T>? target = null;

            if (currentIndex < 0)
            {
                // No current selection — move to the first visible node.
                target = _visibleNodes[0];
            }
            else if (currentIndex >= _visibleNodes.Count - 1)
            {
                // Already at the end — nothing to do.
                return;
            }
            else
            {
                target = _visibleNodes[currentIndex + 1];
            }

            if (target == null)
                return;

            await OnSelect(target);
            QueueScrollToNode(target, keyboard: true);
            await InvokeAsync(StateHasChanged);
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
            _keyboardNavigationRequested = false;
        }

        /// <summary>
        /// Queues a request to scroll the view to the specified node in the virtual tree.
        /// When <paramref name="keyboard"/> is true the scroll will use immediate/non-smooth
        /// behavior and "nearest" alignment to avoid visual jiggle for keyboard navigation.
        /// </summary>
        private void QueueScrollToNode(VirtualTreeViewNode<T> node, bool keyboard)
        {
            if (_suppressAutoScroll)
                return;

            _pendingScrollTarget = node;
            _scrollRequested = true;
            _keyboardNavigationRequested = keyboard;
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



    }
}