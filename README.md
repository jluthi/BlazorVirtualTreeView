# Blazor Virtual Tree View

A high-performance, virtualized tree view component for Blazor with lazy-loading support. This repository contains the reusable `VirtualTreeView` component and a demo app showing how to use it at scale. As the Blazor ecosystem continues to mature, this project helps address a gap in available open-source components.

Project: `BlazorVirtualTreeView.csproj`

Target Framework: .NET 10+

_Nuget package coming soon*_

## Key features

- Virtualization: only visible rows are rendered, keeping the DOM small and scrolling smooth.
- Lazy loading: children are requested on expansion using a `LoadChildren` callback.
- Programmatic navigation: methods such as `SelectNodeAsync` to navigate to a path.
- Selection sync: optional URL query synchronization for the selected node (see example project).
- Context menus: integrate with custom or third party rick-click context menu's using `OnNodeContextMenu` passing `Microsoft.AspNetCore.Components.Web.MouseEventArgs`.
- Small footprint and responsive UI: smooth scrolling and size options (`VirtualTreeViewSize`).
- Demo telemetry: demo shows a running `TotalLoadedNodeCount` to observe lazy-loading behavior.

See the demo app in `examples/BlazorTreeView.Demo` for a working example.

> The demo app included here was built using Radzen demo components (buttons, sliders, menus) for convenience; however, any Blazor component library should work with this component.

## Demo GIF

![Lazy loading demo](demo.gif)

Description: The GIF shows expanding to a deep node, node navigation via URL query string, real-time lazy loading, and virtual scrolling with smooth scrolling enabled.  

## Demo Quick Start

In your IDE, navigate to the demo source folder: `examples/BlazorTreeView.Demo`.

Open the `BlazorTreeView.Demo.csproj` file, set it as the startup project, then build and run the application.



## VirtualTreeView Component - Getting Started

- Provide root nodes via `Roots`.
- Supply a child-loading callback via `LoadChildren="LoadChildrenAsync"`.
- Use component API methods on the `@ref`-ed `VirtualTreeView<T>` instance:
  - `SelectNodeAsync(path)`
  - `AddNode(...)`
  - `RemoveNode()`
  - `RefreshSelectedAsync()`

See `examples/BlazorTreeView.Demo/Components/Pages/Home.razor` for a full usage example.

#### Node flexibility

Although the demo shows a folder / subfolder style tree, the component is data-agnostic and supports any hierarchical model that can be expressed with a stable `Path` per node and a `CanHaveChildren` flag. Examples beyond filesystem-style folders:

- Case management: `cases/2025/12345/documents`
- Organizational charts: `company/division/team/member`
- Product categories and SKUs: `electronics/phones/brand/model`
- Dependency graphs (flattened into a navigable path)
- Time-based buckets: `2025/Q1/Week12/Events`

How to adapt your data:

- Each `VirtualTreeViewNode<T>` carries a `Path` string and user `Value` (your domain object). The component uses the path segments to navigate and expand programmatically (see `SelectNodeAsync`).
- `LoadChildren` can create nodes however you like - map your domain objects into `VirtualTreeViewNode<T>` with any naming or path scheme.
- The tree does not require strictly two-level folder / file semantics: nodes can represent categories, items, groups, or any concept where `CanHaveChildren` indicates the presence of loadable children.

This flexibility means you can present hierarchical data that is not a literal filesystem but still benefits from virtualization and lazy-loading.

#### Node customization

The tree uses **Google Material Design icons** for all built-in node rendering.  
Each icon is defined by a Material icon name (string), and you can override any or all of them with any valid Google Material Design icon.

The component provides simple, built-in icon controls and is easy to customize:

- **Component-level parameters (Material icon names):**
  - `CollapsedNodeIcon` – icon used for nodes that can have children but are currently collapsed  
    *(default: `"folder"`)*
  - `ExpandedNodeIcon` – icon used for nodes that have children and are expanded  
    *(default: `"folder_open"`)*
  - `LeafNodeIcon` – icon used for leaf nodes  
    *(default: `"description"`)*

These parameters are used by the component's internal icon resolver (`ResolveNodeIcon` and `ResolveExpandIcon`) to choose which icon string to render for each row. Example usage in markup:

*Override Default Icons Example:*

```razor
<VirtualTreeView
    NodeIconCollapsed="group_off"
    NodeIconExpanded="group"
    DefaultIcon="person" 
    .../>
```
> Note: Per-node icon customization is not supported out of the box yet.
If you need per-node icons today, you can extend the node model and customize the rendering logic. Currently exploring first-class per-node icon support in a future release, since nodes can represent arbitrary domain data. Things may change here a lot in regard to icon overriding and handling. 



## Project Background

This project began out of necessity.

For my day job, I needed a virtualized TreeView for .NET Blazor that could handle very large data sets in a Blazor Server application. While popular Open Source Blazor Component Libraries like MudBlazor and Radzen offer feature rich TreeView components, none provided true tree view render virtualization - at least as of the time of this writing.

With limited time to study their project structures and source code in order to design such a feature, I decided to build a prototype myself, believing it would be faster. Within a day, I had a promising rough prototype, and after a number of days of testing and refinement, it was released to a production environment.

Afterward, I wanted to take the concept further and give something back to the open-source community. I set out to turn the prototype into a clean, reusable component that others could adapt or extend in their own Blazor applications. After many hours of iteration and refinement, the project was published to GitHub-just in time for Christmas 2025. Hopefully a nice Christmas gift to someone! 

The original VirtualTreeView prototype used MudBlazor; however, for this project, even though the demo is built with Radzen, the core VirtualTreeView was intentionally designed to be library-agnostic and should work with virtually any Blazor UI framework. More refinement to come. 


## Contributing

Contributions are very welcome.

Feel free to submit a pull request or open a GitHub issue. If you’re short on time, issues are just as appreciated, and I’ll do my best to address them when I can 😊





## License

This project is licensed under the MIT License - see the `LICENSE` file for details.
