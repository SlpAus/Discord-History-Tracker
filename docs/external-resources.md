# External resources

Embedded resources can be overridden by files with the same relative path in the application's executable directory. Paths are independent of the current working directory.

| Source file | Override path |
| --- | --- |
| `app/Resources/Tracker/scripts/discord.js` | `Tracker/scripts/discord.js` |
| `app/Resources/Tracker/styles/controller.css` | `Tracker/styles/controller.css` |
| `app/Resources/Viewer/index.html` | `Viewer/index.html` |
| `app/Desktop/Resources/tracker-loader.js` | `tracker-loader.js` |

Copy individual files from the matching source version into these paths to customize them. Missing external files fall back to the embedded resources; an empty file is a valid override. Other read errors are reported. Removing an override restores the embedded version on the next resource read.

Overrides apply to existing embedded resources only. Additional files are not served or appended to the tracking script. Script concatenation retains the original resource order.

## Reloading

- Tracking scripts and styles are read on each injection. Close the existing DHT controller in Discord and inject the tracking script again to apply changes.
- Changes to `tracker-loader.js` take effect the next time **Copy Tracking Script** is used. Userscript loader changes require reinstalling the userscript.
- Viewer overrides are read on each request and served with `Cache-Control: no-store`. Reload the viewer to apply edits. Without an override, the original embedded-resource cache is used; deleting an override returns to that cache.
