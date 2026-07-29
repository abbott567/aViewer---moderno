# Add links to the Help menu

Edit:

```text
src/AViewer.App/HelpMenuLinks.json
```

A link entry is:

```json
{
  "label": "User guide",
  "url": "https://example.org/guide"
}
```

A separator is:

```json
{
  "isSeparator": true
}
```

Only absolute HTTP and HTTPS addresses are opened. Invalid JSON results in an empty Help menu rather than an application crash.
