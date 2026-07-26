# Adding links to the Help menu

The Help menu is generated at runtime from:

```text
src/AViewer.App/HelpMenuLinks.json
```

The file is copied beside the application executable when the project is built or published. You can add, remove, or reorder Help menu links without editing XAML or C# event handlers.

## Add a localised link

Add a resource key to `Resources/Strings.resx` and each translated `.resx` file, then add an entry:

```json
{
  "resourceKey": "HelpUserGuide",
  "url": "https://example.org/aviewer/guide"
}
```

For English, add this resource to `Strings.resx`:

```xml
<data name="HelpUserGuide" xml:space="preserve">
  <value>User guide</value>
</data>
```

Translators use the same resource key and translate only the value.

## Add a non-localised link

Use `label` when a label should be identical in every language:

```json
{
  "label": "GitHub",
  "url": "https://github.com/example/project"
}
```

`resourceKey` takes precedence over `label` when both are supplied.

## Add a separator

```json
{
  "isSeparator": true
}
```

## Complete example

```json
[
  {
    "resourceKey": "HelpDocumentation",
    "url": "https://example.org/docs"
  },
  {
    "label": "GitHub",
    "url": "https://github.com/example/project"
  },
  {
    "isSeparator": true
  },
  {
    "resourceKey": "HelpReportIssue",
    "url": "https://github.com/example/project/issues"
  }
]
```

## Validation and security

Only absolute `https://` and `http://` addresses are opened. Other protocols are rejected. Invalid JSON or an unreadable configuration file results in a disabled **No help links configured** item rather than an application crash.

## Deployment overrides

Because `HelpMenuLinks.json` is copied beside the executable, a deployment owner can replace it after publishing to provide organisation-specific documentation and support links. Keep the file name unchanged and restart AViewer after editing it.
