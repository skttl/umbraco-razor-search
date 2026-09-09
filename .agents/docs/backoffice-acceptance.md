# Backoffice acceptance

This is the outstanding visual check for the installed beta package. It has not been marked as passed. Run it on both Umbraco majors using an isolated SQL Server site and the exact NuGet candidate, with the packaged client assets.

Prepare an administrator, an editor with publish permission restricted to one start node, and an editor without publish permission. Include published invariant content, an English/Danish document and a document outside the restricted editor's start node.

| User and action | Expected result |
| --- | --- |
| Administrator opens Settings, Advanced, RazorSearch | The Operations view loads without client or API errors. |
| Administrator selects Backfill published content | Discovery and rendering progress appear; the page stays responsive and reaches an idle state. |
| Open document status | Cultures and job attempts are distinguishable. The Snapshot and Expected index content tabs show the corresponding data. The latter does not claim to show the live provider index. |
| Administrator inspects a snapshot | Full snapshot HTML is available. Long text and errors remain readable without breaking the layout. |
| Restricted editor opens the document action menu inside the allowed subtree | Queue RazorSearch job is available. Queue this document and Queue document and descendants submit the selected scope. Cancel queues nothing. |
| Restricted editor accesses global operations or an out-of-scope document | Access is denied; API responses do not expose raw HTML. Hiding a menu item alone is insufficient evidence. |
| Editor without publish permission opens the action menu | The queue action is unavailable, and a direct queue request is denied. |
| Administrator observes a controlled render failure, then recovery | The last usable snapshot remains visible; the latest attempt/error and subsequent successful job are accurately reported. |
| Navigate away during progress and reopen the view | Current status loads and updates again. No duplicate updates or stale loading state remain. |

Exercise dialogs with the keyboard: initial focus, tab order, cancel/close and focus return. Check the Operations view and snapshot dialogs at a narrower viewport for clipped controls or unreadable status text.

Capture Marketplace screenshots from the installed package showing the Operations view and a populated document status view. Use test content and exclude account identifiers, tokens and private HTML. Record the CMS version, package version, roles tested and observed failures alongside the screenshots.

Search visibility may lag behind successful snapshot jobs. Check eventual search results separately; the UI should not imply that an idle render queue proves every frontend searcher is already refreshed.
