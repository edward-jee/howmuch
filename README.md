# HowMuch

A native Windows 11 WPF widget for checking overseas shopping prices in Korean won. Always on top, fixed at 384 × 622 device-independent pixels, with a draggable title bar.

## Use

1. Launch `howmuch.exe` in Windows.
2. Enter the price and select a currency. JPY is the initial default; later launches restore your last selection and window position.
3. KRW price, estimated customs duty, VAT, and total update as you type. Use a dot for decimals; grouped commas such as `12,345.67` are accepted.
4. Optionally enter international shipping and insurance in the same currency, and adjust the duty percentage for the item. Include foreign domestic shipping and local tax in the main price.
5. Select the US option only for shipments from the US eligible for list clearance. Currency alone does not determine shipment origin. Disable personal-use exemption if the goods do not qualify.

Drag the dark title bar to move. **−** minimizes to the Windows taskbar so you can restore it later. **×** or Alt+F4 exits; launch the executable again to reopen. Window edges do not resize, and double-clicking the title bar does not maximize.

Currencies: JPY, USD, EUR, CNY, GBP, AUD, CAD, CHF, HKD, SGD, TWD, THB, KRW.

## Rates and offline behavior

The widget uses the HTTPS [Frankfurter v2 API](https://frankfurter.dev/) with no API key or third-party packages. It fetches rates at startup, when switching currencies, every 30 minutes while running, and on **Refresh**. These are the latest published **daily reference rates**, not intraday bank/card quotes. The source and publication dates are shown in the widget; weekends and holidays may retain an earlier publication date.

Price changes recalculate locally without sending your entered amounts to the API. The API is queried only with currency codes. Both KRW and USD rates are fetched; USD determines the exemption threshold. When the network fails, the last valid saved rates for that currency can be used with an explicit **Offline · saved rates** label and their dates. If no valid cache exists, totals are unavailable instead of using an invented rate. Old cached rates are labeled and dated but have no hard expiry.

Preferences and per-currency rate caches are JSON files in `%LOCALAPPDATA%\HowMuch`. Missing, corrupt, and inaccessible files are tolerated. Deleting that folder resets preferences and offline rates.

## Customs estimate

The model covers ordinary qualifying personal-use imports into South Korea:

- Goods price up to and including USD 150 is exempt; eligible US list-clearance shipments use USD 200.
- Separately identified international freight/insurance is excluded from the goods-price threshold but included in the taxable value when the threshold is exceeded.
- Above the threshold, tax applies to the **entire** value, without deducting the threshold.
- Duty = (price in KRW + international shipping/insurance in KRW) × entered duty percentage.
- VAT = (that taxable value + unrounded duty) × 10%.
- Total = price in KRW + shipping/insurance in KRW + estimated duty + estimated VAT. With shipping set to zero, it is price plus import charges.

**8% is an editable starting assumption, not a universal tariff.** HS classification, origin/FTA, actual Korea Customs Service assessment rates and rounding, excluded goods, quantity limits, combined shipments, and additional taxes can change the amount. Alcohol, tobacco, luxury goods, and goods with special taxes need the official calculator. Card conversion fees, brokerage, and other handling fees are excluded. Duty and VAT estimates are individually rounded to the nearest won; conversion calculations retain decimal precision until display.

Rules checked on 2026-10-08 against the [Korea Customs Service overseas-purchase estimator](https://www.customs.go.kr/kcs/ad/tax/BuyTaxCalculation.do) and [express-cargo clearance guidance](https://customs.go.kr/kcs_english/cm/cntnts/cntntsView.do?cntntsId=2733&mi=8057). The widget links to the official estimator for item-specific checks. Market reference rates are used for this approximation; they are not the official weekly customs assessment rates.

## Build and run from WSL 2

Requires the Windows .NET 8 SDK; `dotnet.exe` must be on the WSL PATH. Run from this repository on the Windows drive:

```bash
dotnet.exe build howmuch.csproj -c Release
dotnet.exe run --project howmuch.csproj -c Release
```

This starts the app in native Windows, using WSL Windows interop. Compilation has no external package dependencies. The build output is `bin/Release/net8.0-windows/howmuch.exe` and needs the Windows .NET 8 Desktop Runtime. Keep the accompanying files with the executable.

To create a portable x64 Windows folder including the runtime:

```bash
dotnet.exe publish howmuch.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

Open `artifacts\win-x64\howmuch.exe` in Windows Explorer. Copy the **whole folder** to distribute it. Substitute `win-arm64` and a different output folder for ARM64 Windows. First self-contained publish may download runtime packs.

## Verification

```bash
dotnet.exe run --project tests/HowMuch.Checks.csproj -c Release
dotnet.exe run --project tests/WidgetChecks/WidgetChecks.csproj -c Release
```

The first suite runs 28 deterministic checks for threshold boundaries, shipping, full-value taxes, parsing, persistence, malformed rates, offline behavior, and cancellation. The second briefly opens a native Windows WPF widget with fake rates and temporary settings. It checks initialization, layout height, topmost/fixed-size properties, immediate conversion, invalid input, saved currency, and rapid currency switches. Neither suite calls the live API or modifies your settings. A live JPY → KRW/USD endpoint response was also verified during development.

Manual Windows acceptance checks still needed:

- Drag across monitors, including mixed DPI displays; relaunch with a monitor disconnected.
- Check always-on-top behavior over other normal windows and keyboard tab navigation.
- Minimize/restore, close/reopen, confirm saved currency and position.
- Disconnect Internet and refresh, then select an uncached currency; reconnect and recover.
- Inspect appearance at 100%, 150%, and 200% scaling and with Windows accessibility settings.

Source layout: `Core/` contains calculations; `Services/` contains HTTPS and local storage; `MainWindow.xaml` and its code-behind handle UI interaction. See [AGENTS.md](AGENTS.md) for contributor guidance.
