# Localising AViewer Modern

AViewer uses standard .NET `.resx` resources. UI text is not embedded directly in the main XAML or dialog XAML.

## Resource files

The neutral English resource is:

```text
src/AViewer.App/Resources/Strings.resx
```

The included French example is:

```text
src/AViewer.App/Resources/Strings.fr-FR.resx
```

At runtime, .NET loads the most specific matching resource and falls back to the neutral English resource for missing entries.

## Add a language

1. Copy `Strings.resx`.
2. Rename the copy using a BCP 47/.NET culture name, for example:

```text
Strings.de-DE.resx
Strings.es-ES.resx
Strings.ja-JP.resx
Strings.ar-SA.resx
```

3. Translate only each `<value>`; do not change the `<data name="...">` keys.
4. Preserve placeholders such as `{0}`, `{1}`, and `{2}`.
5. Preserve `_` access-key markers, assigning a suitable unique access key for the translated menu.
6. Build the project. The SDK creates the satellite resource assembly automatically.
7. Add the culture to the Language menu in `MainWindow.xaml`, or load available cultures from configuration if the language list becomes large.

## Use a resource in XAML

Add the application namespace:

```xml
xmlns:local="clr-namespace:AViewer.App"
```

Then use:

```xml
<TextBlock Text="{local:Loc Key=Properties}"/>
```

The `Loc` markup extension uses a binding, so visible text updates immediately when the culture changes.

## Use a resource in C#

For plain text:

```csharp
LocalizationManager.Instance.Get("Ready")
```

For formatted text:

```csharp
LocalizationManager.Instance.Format("SavedFile", fileName)
```

Do not build translated sentences by concatenating fragments. Use one complete resource string with numbered placeholders so translators can change word order.

## Right-to-left languages

`LocalizationManager.FlowDirection` is bound to the windows. Cultures whose `TextInfo.IsRightToLeft` is true automatically use right-to-left layout. Individual API identifiers, code, JSON, and file paths can still set `FlowDirection="LeftToRight"` where appropriate.

## What should remain untranslated

Keep these canonical technical identifiers unchanged unless a product decision says otherwise:

- UIA
- MSAA
- IAccessible2
- ARIA attribute names
- Interface and pattern identifiers
- JSON property names
- Raw roles, states, and object attributes returned by inspected applications

The surrounding UI labels and explanations should be translated.

## Translator checks

Test each language with:

- 100%, 150%, and 200% text/display scaling
- High Contrast
- Keyboard access keys
- Long translations
- Narrow window width
- Right-to-left layout when applicable
- Property text wrapping
- Menus and status messages

The language selection is persisted in `%LOCALAPPDATA%\AViewerModern\app-settings.json`.
