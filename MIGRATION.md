# Migration from the Delphi aViewer

| Original capability | Modern replacement | Status |
|---|---|---|
| Delphi VCL user interface | .NET 8 WPF | Implemented |
| UI Automation property inspection | FlaUI UIA3 adapter | Implemented |
| Element under pointer | UIA `FromPoint` | Implemented |
| Focused object inspection | UIA focused-element query | Implemented |
| Accessibility hierarchy | Bounded recursive UIA tree | Implemented |
| HTML report | Structured JSON export | Implemented; HTML export planned |
| MSAA | `oleacc.dll` adapter | Planned |
| IAccessible2 | Generated COM adapter | Planned |
| Browser DOM / ISimpleDOM | Prefer platform accessibility mappings; optional browser adapter | Planned |
| Focus rectangle window | Non-activating overlay | Planned |
| INI-based property selection | JSON settings and profiles | Planned |
| Single-instance IPC | .NET named pipe | Planned |

## macOS

The macOS application is a separate native build rather than a port of the
Windows presentation layer. The table above describes Windows behaviour; the
macOS equivalents, and the places where the two platforms deliberately differ,
are documented in [macos/README.md](macos/README.md).

| Original capability | macOS replacement | Status |
|---|---|---|
| UI Automation property inspection | `AXUIElement` attribute discovery | Implemented |
| Element under pointer | `AXUIElementCopyElementAtPosition` | Implemented |
| Focused object inspection | System-wide `AXFocusedUIElement` | Implemented |
| Accessibility hierarchy | Bounded recursive `AXChildren` walk | Implemented |
| HTML report | JSON and HTML export | Implemented |
| MSAA | No macOS counterpart | Not applicable |
| IAccessible2 | No macOS counterpart | Not applicable |
| Browser DOM / ISimpleDOM | `AXDOM*` and `AXARIA*` attributes where published | Implemented |
| Focus rectangle window | Click-through overlay window | Implemented |
| INI-based property selection | JSON settings and profiles | Implemented |

## Design changes

- API-specific COM code is isolated behind adapters.
- Inspection results use immutable, serializable models.
- Tree depth is bounded to avoid freezing on very large desktop trees.
- Exceptions from inaccessible or disappearing elements are contained at property boundaries.
- The interface uses native controls, explicit labels, keyboard-operable commands and scalable text.
