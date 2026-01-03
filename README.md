# Blazor Virtual Tree View

A high-performance, virtualized tree view component for Blazor with lazy-loading support. This repository contains the reusable `VirtualTreeView` component and a demo app showing how to use it at scale. As the Blazor ecosystem continues to mature, this project helps address a gap in available open-source components.

Project: `BlazorVirtualTreeView.csproj`

Target Framework: .NET 10+

_NuGet package coming soon*_

## Key features

- <u>Virtualization (<u>Key Feature</u>)</u>: Only visible rows are rendered, keeping the DOM small and scrolling responsive with both small and large datasets.
- <u>Lazy Loading</u>: Children are requested on expansion using a `LoadChildren` callback method.
- <u>Programmatic Navigation</u>: Expands ancestors, selects, and focuses a given node via the `SelectNodeAsync` method.
- <u>Context Menu Support</u>: Integrate with custom or third-party right-click context menus using `OnNodeContextMenu`, passing `Microsoft.AspNetCore.Components.Web.MouseEventArgs`.
- <u>Performance & Style Customizations</u>: A variety of exposed parameters to fine-tune look and feel.

See the demo app in `examples/BlazorTreeView.Demo` for a working example.

> The demo app included here was built using Radzen demo components (buttons, sliders, menus, etc.) for convenience; however, any Blazor component library should work with this component.

## Demo GIF

![Lazy loading demo](demo.gif)

Description: The GIF shows expanding to a deep node, node navigation via URL query string, real-time lazy loading, and virtual scrolling with smooth scrolling enabled.  

## Run Demo (Optional)

In your IDE, navigate to the demo source folder: `examples/BlazorTreeView.Demo`.

Open the `BlazorTreeView.Demo.csproj` file, set it as the startup project, then build and run the application.



## VirtualTreeView Component - Getting Started

### Prerequisites
This component uses **Google Material Design** for built-in icons. Make sure the following stylesheet is included in your host page else default icons will not render.


Add:

```
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined" rel="stylesheet" />
```

to either `Pages/App.razor` for Blazor Server **or** `wwwroot/index.html` for Blazor WebAssembly (Standalone or Hosted)



### Basic Usage

- Provide root nodes via `Roots`.
- Supply a child-loading callback via `LoadChildren="LoadChildrenAsync"`.
- Use component API methods on the `@ref`-ed `VirtualTreeView<T>` instance as needed:
  - `SelectNodeAsync(..)` - programmatically expand ancestors, select, and scroll to a node.
  - `AddNode(..)` - add a node under an explicit parent or to root if not specified.
  - `RemoveNode(..)` - remove a node.
  - `RefreshSelectedAsync()` - reload children of the currently selected node.
  - `ClearSelection()` - clear the current selection.
  - `CollapseAll()` - collapse all root nodes and any loaded descendants.
- Common events and properties to integrate with:
  - `SelectedNodeChanged` - notified when selection changes.
  - `OnNodeContextMenu` - invoked for right-click/context menu actions.
  - `VirtualTreeView<T>.SelectedNode` - read-only property exposing the currently selected node.
  -  And more...


See `examples/BlazorTreeView.Demo/Components/Pages/Home.razor` for a full usage example.


### Node Flexibility

Although the demo shows a folder / subfolder style tree, the component is data-agnostic and supports any hierarchical model that can be expressed with a stable `Path` per node and a `CanHaveChildren` flag. Examples beyond filesystem-style folders:

- Case management: `cases/2025/12345/documents`
- Organizational charts: `company/division/team/member`
- Product categories and SKUs: `electronics/phones/brand/model`
- Time-based buckets: `2025/Q1/Week12/Events`

How to adapt your data model:

- Each `VirtualTreeViewNode<T>` carries a `Path` string and user `Value` (your domain object). The component uses the path segments to navigate and expand programmatically (see `SelectNodeAsync`).
- `LoadChildren` can create nodes however you like - map your domain objects into `VirtualTreeViewNode<T>` with any naming or path scheme.
- The tree does not require strictly two-level folder / file semantics: nodes can represent categories, items, groups, or any concept where `CanHaveChildren` indicates the presence of loadable children.

This flexibility means you can present hierarchical data that is not a literal filesystem but still benefits from virtualization and lazy-loading.

**Additional Information about expandability and icons (`IsLeafNode`)**

`VirtualTreeViewNode<T>.IsLeafNode` controls both whether a node is considered expandable and which built-in icons the component displays:

- `IsLeafNode == true` - the node is a leaf: it cannot be expanded, the expand/collapse affordance is hidden, and `LeafNodeIcon` is used for the main node icon.
- `IsLeafNode == false` - the node may have children: the expand affordance is shown (when appropriate) and the component selects `CollapsedNodeIcon` or `ExpandedNodeIcon` depending on loaded/expanded state.


### Node Customization

The tree uses **Google Material Design icons** for all built-in node rendering.  
Each icon is defined by a Material icon name (string), and you can override any or all of them with any valid Google Material Design icon.

The component provides simple, built-in icon controls and is easy to customize:

- Component-level parameters (Material icon names):
  - `CollapsedNodeIcon` – icon used for nodes that can have children but are currently collapsed  
    *(default: `"folder"`)*  
  - `ExpandedNodeIcon` – icon used for nodes that have children and are expanded  
    *(default: `"folder_open"`)*  
  - `LeafNodeIcon` – icon used for leaf nodes  
    *(default: `"description"`)*

These parameters are used by the component's internal icon resolver to choose which icon string to render for each row.
<br/>
*Note: These parameters are of course ignored if you provide a custom `NodeTemplate` render fragment.*

Override Default Icons Example:

```razor
<VirtualTreeView
    CollapsedNodeIcon="group_off"
    ExpandedNodeIcon="group"
    LeafNodeIcon="person" 
    .../>
```

**Render Fragment (Per-node Template)**

The `VirtualTreeView` supports a `NodeTemplate` render fragment so you can fully customize how each node is rendered. The template receives a `VirtualTreeViewNode<T>` as `Context` and should stay lightweight because templates are created/destroyed frequently due to virtualization.

Short example:

```
<VirtualTreeView ... >
    <NodeTemplate Context="node">
        <div style="display:flex;align-items:center;gap:8px;width:100%;">
            <span class="material-symbols-outlined" style="font-size:20px;flex:0 0 auto;">
                @(node.IsLeafNode ? "description" : (node.IsExpanded ? "folder_open" : "folder"))
            </span>
            <div style="flex:1 1 auto;min-width:0;overflow:hidden;white-space:nowrap;text-overflow:ellipsis;">
                @node.Text
            </div>
        </div>
    </NodeTemplate>
</VirtualTreeView>
```

<br/>

*Note: The Expand Icon (Expand/Collapse Arrow) is still rendered by the component outside of the node template. No customization of this icon is publicly supported or planned at this time.*

## Project Background

This project began out of necessity.

For my day job, I needed a virtualized TreeView for .NET Blazor that could handle very large data sets in a Blazor Server application. While popular Open Source Blazor Component Libraries like MudBlazor and Radzen offer feature rich TreeView components, none provided true tree view render virtualization - at least as of the time of this writing.

With limited time to study their project structures and source code in order to design such a feature, I decided to build a prototype myself, believing it would be faster. Within a day, I had a promising rough prototype, and after a number of days of testing and refinement, it was released to a production environment.

Afterward, I wanted to take the concept further and give something back to the open-source community. I set out to turn the prototype into a clean, reusable component that others could adapt or extend in their own Blazor applications. After many hours of iteration and refinement, the project was published to GitHub - just in time for Christmas 2025. Hopefully a nice Christmas gift to someone! 

The original VirtualTreeView prototype used MudBlazor; however, for this project, even though the demo is built with Radzen, the core VirtualTreeView was intentionally designed to be library-agnostic and should work with virtually any Blazor UI framework. More refinement to come. 


## Contributing

Contributions are very welcome.

Feel free to submit a pull request or open a GitHub issue. If you’re short on time, issues are just as appreciated, and I’ll do my best to address them when I can 😊





## License

This project is licensed under the MIT License - see the `LICENSE` file for details.
