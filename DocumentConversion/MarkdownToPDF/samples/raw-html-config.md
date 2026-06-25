# Raw HTML Configuration Example

By default, unsupported/raw HTML tags are stripped because this sample is not an HTML renderer.

Run this file with:

```PowerShell
dotnet run -- samples/raw-html-config.md raw-html-config.pdf --overwrite --include-unrendered-html
```

When `--include-unrendered-html` is set, XML-like configuration tags are rendered as literal text:

<setting name="DISPLAY_INTERVAL_SECONDS" type="integer">

Controls how often a fictional lobby display advances to the next community announcement. Defaults to `45` seconds.

</setting>

<setting name="WELCOME_MESSAGE" type="string">

Sets a short message for the top of the display, for example `Welcome to Harbor Hall`.

</setting>
