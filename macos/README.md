# aViewer for macOS

A native macOS build of aViewer that inspects applications through the macOS
accessibility API (`AXUIElement`).

This is not the Windows application recompiled. macOS exposes a different
accessibility API with different roles, attributes and relationships, so the
inspection layer is written from scratch against `AXUIElement` while the
features, the interface and the export formats follow the Windows build.

## Requirements

- macOS 13 or later
- Swift 5.9 or later — the Command Line Tools are enough to build the app;
  full Xcode is only needed for universal binaries and for `swift test`

## Build and run

```bash
cd macos
./build-app.sh
open build/aViewer.app
```

`build-app.sh` compiles the Swift package and assembles `build/aViewer.app`.
There is no Xcode project: the interface is built in code, which keeps the app
buildable from a plain Command Line Tools installation.

For development you can also run `swift build && swift run`, but the
accessibility permission is granted per application bundle, so day-to-day use
should go through the bundle produced by `build-app.sh`.

## Accessibility permission

aViewer is an assistive client. Without accessibility access it can see
nothing — an empty tree and an empty property grid — so the app asks on first
launch and again from any inspection command. Grant it in **System Settings >
Privacy & Security > Accessibility**.

Two things about this permission cause more confusion than everything else in
the application put together.

**macOS answers the trust question once per launch.** Switching the toggle on
while aViewer is running does not reach the running process. The app polls for
fifteen seconds and then says so, but the fix is always the same: quit aViewer
and open it again.

**The app must not be sandboxed.** The App Sandbox blocks the accessibility
client API outright, which is why there is no entitlements file here.

### Why the Accessibility permission keeps being revoked

When you grant Accessibility access, macOS records the application's
*designated requirement*, not its path. For an ad-hoc signed build — the
default here — that requirement is a hash of the binary itself:

```
# designated => cdhash H"4157a9d9ace86f76acd57a859e62693f9f8e91e8"
```

Every rebuild produces a different hash, so the recorded requirement stops
matching and the permission is silently dead. The toggle in System Settings
still shows as on, which makes this look like a bug in the app or in macOS. It
is neither, and no amount of clicking in System Settings will fix it.

Signing with a real certificate changes the requirement to one based on the
bundle identifier and the signing certificate, which does not depend on the
binary's contents — so the grant survives rebuilds. A free, self-signed
certificate is enough; it does not need to come from Apple.

In **Keychain Access > Certificate Assistant > Create a Certificate…**:

- **Name:** `aViewer Local Signing`
- **Identity Type:** Self Signed Root
- **Certificate Type:** Code Signing

`build-app.sh` picks up an identity with exactly that name automatically, so
after creating it just build as usual:

```bash
./build-app.sh
```

Grant Accessibility access to that build once and it will keep working across
rebuilds. The script prints the designated requirement after signing and warns
you when it is hash-based.

If a permission does get into a stuck state, clear it and start again:

```bash
tccutil reset Accessibility org.aviewer.moderno.mac
```

Shipping the app to other people is a different problem and needs a Developer
ID signature and notarisation:

```bash
SIGN_IDENTITY="Developer ID Application: Your Org (TEAMID)" ./build-app.sh
```

## Feature parity

| Windows feature | macOS build | Notes |
|---|---|---|
| Inspect element under pointer | Yes | `AXUIElementCopyElementAtPosition` |
| Inspect element with keyboard focus | Yes | System-wide `AXFocusedUIElement` |
| Configurable descendant depth (0–4) | Yes | |
| Accessible tree and property grid | Yes | `NSOutlineView` and `NSTableView` |
| Up one level | Yes | Follows `AXParent` directly |
| Complete application tree | Yes | Loads the real `AXApplication` root |
| Relationship visualisation | Yes | Grouped connectors, multi-target aware |
| Focus-order recording with overlay | Yes | Tab, Shift+Tab and optional arrow keys |
| Focus ring around the inspected element | Yes | |
| Copy / save tree as JSON | Yes | Keys match the Windows export |
| Copy HTML and Copy HTML subtree | Yes | Inferred from AX role, subrole and DOM attributes |
| Choose properties (show / hide) | Yes | With a search filter |
| Always on top | Yes | |
| Localisation with live switching | Yes | Built-in English, sample French |
| Configurable Help menu links | Yes | `Contents/Resources/HelpMenuLinks.json` |
| High-contrast aware overlays | Yes | Follows Increase Contrast |
| UIA / MSAA / IAccessible2 tabs | **No** | See below |

## What is different on macOS, and why

**One accessibility API, not four.** Windows exposes UIA, MSAA and
IAccessible2, and aViewer shows a tab per API. macOS has exactly one:
`NSAccessibility` / `AXUIElement`. MSAA and IAccessible2 have no macOS
counterpart, so there is nothing to put in those tabs and the tab strip is
gone. The tree is always the AX tree.

**Attributes are discovered, not assumed.** The Windows build queries a fixed
list of UIA properties. This build calls `AXUIElementCopyAttributeNames` and
reports whatever the element actually publishes, then sorts the results into
sections. On macOS the attribute set varies widely between AppKit, Electron and
each browser engine, and a fixed list would silently hide exactly the
web-specific attributes that matter most — `AXDOMIdentifier`, `AXDOMClassList`,
the `AXARIA*` family, and engine-specific extras. Anything unrecognised still
appears, under **Other**.

**Chromium applications need waking up.** Chrome, Edge, Electron and VS Code
expose only a stub tree until an assistive client asks for the full one.
aViewer sets `AXManualAccessibility` on each application it inspects, which is
what VoiceOver does; without it those applications look almost empty.

A second, older switch — `AXEnhancedUserInterface` — is available under
**View > Request enhanced user interface** but is **off by default**, because
some AppKit applications change their layout when it is set. A tool meant to
observe should not disturb what it observes. Turn it on only if an application
exposes an incomplete tree without it.

**Parent navigation is exact.** The Windows build re-resolves the element at
the selected node's screen bounds and takes its parent, which it documents as
best effort. AX gives durable element references, so **Up one level** follows
`AXParent` directly and lands on the real parent every time.

**Two keyboard shortcuts differ deliberately.** Copy JSON is
<kbd>⇧</kbd><kbd>⌘</kbd><kbd>C</kbd> rather than <kbd>⌘</kbd><kbd>C</kbd>, and
Copy HTML subtree adds <kbd>⌥</kbd>, so that plain <kbd>⌘</kbd><kbd>C</kbd>
keeps its standard meaning of copying selected text out of the property grid.

**Tree size is capped.** A full browser document can be hundreds of thousands
of elements. Captures stop at 15,000 nodes and the status line says so — the
limit is reported, never applied silently.

## How ARIA states reach macOS

Some ARIA states are published by name and some are not. macOS has no
equivalent of the UI Automation `AriaProperties` string the Windows build
reads, so the mapping is uneven and worth knowing before you trust an absence.

| ARIA | macOS AX | Named by the provider |
|---|---|---|
| `aria-current` | `AXARIACurrent` | Yes |
| `aria-invalid` | `AXInvalid` | Yes |
| `aria-required` | `AXRequired` | Yes |
| `aria-expanded` | `AXExpanded` | Yes |
| `aria-live`, `aria-atomic`, `aria-relevant` | `AXARIALive` and friends | Yes |
| `aria-pressed` | `AXRole` AXCheckBox + `AXSubrole` AXToggle + `AXValue` | **No** |
| `aria-checked` | `AXRole` AXCheckBox or AXRadioButton + `AXValue` | **No** |
| `aria-disabled` | `AXEnabled` false | **No** |

For the two that carry a tri-state value, aViewer names them for you in a
separate **ARIA (derived)** tab, which always states what each value was
derived from:

```
aria-pressed   true — derived from AXSubrole AXToggle and AXValue
```

The Windows build uses its tab strip for the three Windows accessibility APIs.
macOS has one API, so the same affordance does a different job here: it keeps
observation and interpretation apart. The **AX** tab is what the application
published. The **ARIA (derived)** tab is what aViewer worked out, and it is
labelled as such at the top of the tab, with a count in the tab title so an
empty one is obvious without opening it.

**The derived tab is verified against Safari and WebKit only.** Chromium may
map these states differently, and that has not been confirmed — treat derived
values from other engines as unconfirmed and check the source. Nothing in the
**AX** tab is affected by this: it reports what the provider published,
whatever the engine.

Derivation only runs on web content, identified by the element publishing
`AXDOM*` attributes, so a native checkbox is never reported as though it
carried ARIA.

`aria-disabled` is deliberately not derived: it is indistinguishable from a
natively disabled control at the AX layer, and guessing would produce findings
the page never earned. Read `AXEnabled` and check the source.

`docs/aria-state-test-page.html` exercises each of these states if you want to
confirm the behaviour of a particular browser version yourself.

## Keyboard shortcuts

| Command | Shortcut |
|---|---|
| Inspect element under pointer | <kbd>⇧</kbd><kbd>⌘</kbd><kbd>P</kbd> or <kbd>F7</kbd> |
| Inspect element with keyboard focus | <kbd>⇧</kbd><kbd>⌘</kbd><kbd>F</kbd> or <kbd>F8</kbd> |
| Record focus order | <kbd>⇧</kbd><kbd>⌘</kbd><kbd>O</kbd> or <kbd>F9</kbd> |
| Stop recording or inspecting | <kbd>Esc</kbd> |
| Up one level | <kbd>⌘</kbd><kbd>↑</kbd> |
| Complete application tree | <kbd>⇧</kbd><kbd>⌘</kbd><kbd>T</kbd> |
| Copy HTML | <kbd>⇧</kbd><kbd>⌘</kbd><kbd>H</kbd> |
| Copy HTML subtree | <kbd>⌥</kbd><kbd>⇧</kbd><kbd>⌘</kbd><kbd>H</kbd> |
| Copy JSON | <kbd>⇧</kbd><kbd>⌘</kbd><kbd>C</kbd> |
| Save JSON | <kbd>⌘</kbd><kbd>S</kbd> |

## Overlay colours

Meanings are shared with the Windows build:

- **Gold** — Tab and Shift+Tab focus transitions
- **Blue** — arrow-key transitions inside a composite widget
- **Green** — the most recent recorded focus stop
- **Red** — the relationship source element
- **Teal** — relationship targets

Overlays follow the system **Increase Contrast** setting.

## Export formats

JSON export uses the same key names as the Windows build wherever the concept
exists on both platforms — `Api`, `Id`, `Name`, `ControlType`, `Properties`,
`Relationships`, `Children` and the `Bounding*` values — so a macOS capture and
a Windows capture of the same web page can be diffed directly. Keys with no
macOS counterpart are omitted rather than emitted empty, and AX-only keys
(`Subrole`, `RoleDescription`, `Identifier`) are added alongside.

HTML export is **not** a DOM dump. macOS accessibility does not expose
`outerHTML`. The tag is inferred from the AX role and subrole, `id` and `class`
come from `AXDOMIdentifier` and `AXDOMClassList`, and ARIA attributes come from
the `AXARIA*` family. Exact source capture would need a browser integration
such as the Chrome DevTools Protocol.

## Tests

```bash
swift test
```

21 tests covering the attribute catalogue, the connector routing geometry, both
export services, the property filter and the coordinate conversion.

**`swift test` requires full Xcode**, because XCTest does not ship with the
Command Line Tools. `swift build` and `build-app.sh` work either way.

If Xcode is installed but `xcodebuild` fails to load its own libraries, its
system components are older than the installed Xcode. Repair them with:

```bash
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -runFirstLaunch
```

## Layout

```
Sources/AViewerMac/
  Model/          Accessibility node and focus-order types
  Accessibility/  AXUIElement reading, attribute catalogue, tree capture
  Services/       JSON and HTML export, settings, property filter, localisation
  Overlay/        Transparent click-through overlays and connector geometry
  Input/          Accessibility permission, global navigation-key monitor
  UI/             Window, tree, property grid, menus, property chooser
```

`Accessibility/` and `Services/` correspond to the Windows `AViewer.Core`
project; `UI/`, `Overlay/` and `Input/` correspond to `AViewer.App`.

## Known limitations

- Universal (Intel + Apple silicon) binaries need full Xcode, because SwiftPM
  drives them through `xcbuild`. With Command Line Tools only, `build-app.sh`
  builds for the host architecture and says so rather than failing.
- Element identity is derived from the process ID and the element reference's
  hash. AX has no runtime identifier, so identity is stable within a session
  but not across relaunches of the inspected application.
- Applications that are not accessibility-instrumented expose little or
  nothing. That is a finding about the application, not a defect in the tool.
