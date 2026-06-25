# Code and Identifier Handling

Inline code such as `TRAIL_MAP_VERSION` and `WEATHER_ALERT_LEVEL` should render with a light gray background.

Identifiers outside code should remain literal text too: TRAIL_MAP_VERSION, WEATHER_ALERT_LEVEL, and SAMPLE_FILE_NAME.md should not trigger italic formatting.

Valid emphasis should still work: _italic_, **bold**, and ***bold italic***.

```PowerShell
$env:TRAIL_MAP_VERSION = "spring-2026"
dotnet run -- sample.md output.pdf --overwrite
```

```Python
import os
version = os.environ.get("TRAIL_MAP_VERSION", "draft")
print(version)
```
