# Working on HowMuch

## Product
Build a small native Windows 11 WPF widget for foreign-price conversion to KRW and estimated Korean personal-import charges. Keep it always on top, fixed size, draggable, keyboard accessible, and easy to dismiss. Default currency is JPY; persist the last selection locally.

## Environment and commands
- This repository lives on a Windows drive and is edited from WSL 2.
- Target .NET 8 Windows and WPF. Use `dotnet.exe`, not Linux `dotnet`, for Windows builds and execution.
- Build: `dotnet.exe build howmuch.csproj -c Release`.
- Run calculation and service checks: `dotnet.exe run --project tests/HowMuch.Checks.csproj -c Release`.
- Run the native WPF smoke checks (briefly opens a Windows widget): `dotnet.exe run --project tests/WidgetChecks/WidgetChecks.csproj -c Release`.
- Publish: `dotnet.exe publish howmuch.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64`.
- WPF must run in Windows. Do not claim visual or interactive validation from compilation alone. Record Windows manual checks in README.md.

## Implementation
- Keep money and rates as `decimal`. Separate calculations, network access, persistence, and WPF interaction.
- Fetch rates asynchronously over HTTPS; never block the UI. Show publication dates, loading/error states, and an explicit cached-data indicator. Never invent fallback rates.
- Recalculate immediately on input changes. Prevent older requests from replacing the newly selected currency.
- Store preferences and cached rates beneath `%LOCALAPPDATA%/HowMuch`, outside the repo. Tolerate missing, malformed, and inaccessible files.
- Duty depends on classification, origin, shipping, exemptions, and customs exchange rates. Keep assumptions visible and editable, include VAT, and label the result as an estimate. Verify rule changes against official Korea Customs Service sources and document sources in README.md.
- Avoid unnecessary packages and keep the widget compact. Preserve native control keyboard behavior and add accessible names to icon buttons.

## Verification and Git
- Test meaningful calculation boundaries, malformed input, cache validation, and failed network requests with deterministic data. Tests must not require the live rate API.
- Build after implementation changes. Use the Windows SDK from WSL; request execution approval when the sandbox prevents Windows interop.
- Make small, focused Git commits as requested. Inspect diffs before committing; do not include generated output, secrets, or unrelated user changes.
