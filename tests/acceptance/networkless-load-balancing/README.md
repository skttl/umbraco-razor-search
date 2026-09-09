# Networkless SQL Server load-balancing diagnostic

These templates run actual Umbraco applications in **separate OS processes**, with ASP.NET Core TestServer instead of TCP listeners. CMS database cache instructions and separate Examine indexes remain real. Only the RazorSearch rendering HTTP transport uses the publisher's in-process TestServer handler. Phase-marker files coordinate assertions; they do not carry content or index changes between nodes.

Create two fresh temporary fixture directories using the acceptance project and a fresh dedicated SQL Server database. Install the intended RazorSearch packages from a fresh NuGet cache. In each directory:

- Copy `Program.cs.template` and `Diagnostic.cs.template` as `Program.cs` and `Diagnostic.cs`.
- Add `Microsoft.AspNetCore.TestHost` version `10.0.12` to the acceptance project.
- Copy the acceptance Razor view into `Views/acceptance.cshtml` **before building**.
- Set `Acceptance:Enabled=true` and `Acceptance:CoordinationDirectory` to a shared, initially empty temporary directory.
- Use `Acceptance:Role=SchedulingPublisher` for the publisher and `Subscriber` for the frontend.
- Configure the same dedicated SQL database and `Microsoft.Data.SqlClient` provider on both nodes. Keep their application directories and Examine indexes separate.
- Set the fixture public URL, Umbraco application URL, and RazorSearch rendering/public base addresses to `http://localhost`. This logical address is handled by TestServer; no listener is opened.
- Enable unattended installation and upgrades with temporary fixture administrator credentials.

Build both projects. Run the publisher's compiled application in the foreground. Once it writes `initial.ready`, run the frontend's compiled application in a second foreground terminal. Both applications have intrinsic deadlines and stop/dispose their hosts after the assertions finish.

The initial startup gate waits for `IContentRoutingReadiness.IsReady`, which occurs after all `UmbracoApplicationStartingNotification` handlers. `RuntimeLevel.Run` alone is insufficient during background unattended upgrades.

The diagnostic checks fresh frontend index bootstrap, invariant/exact culture results, public URLs, draft isolation, publication with a changed child URL, partial culture unpublish, and full unpublish. Asynchronous index assertions require three consecutive matches within 150 seconds. Totals, items, and URLs are checked from the same response; successful responses are retained for reporting. The frontend also samples ten final query matrices after mutations finish. CMS starts instruction polling after an initial one-minute delay; snapshot completion does not mean provider queries are already current.

Empty search responses produce `LB EMPTY` diagnostic records with the current request's content-cache availability, a direct provider query, published-content availability, snapshot status, readiness, and queue state. These probes run after the public search call and do not create an Umbraco context before it.

Success requires both processes to exit zero and log `PASS all distributed lifecycle phases`. The final expected `commonword` totals are invariant **1**, Danish **1**, and English **2**. This diagnostic does not verify reverse proxies, cross-server HTTP rendering, TLS, or browser UI behavior.

Use a new database and empty marker directory for each complete run. Do not run these fixture mutations against a real site database.
