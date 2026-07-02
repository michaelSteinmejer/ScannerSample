# Scanner Sample WPF (.NET Framework 4.6.2)

Projektet ligger her:

`C:\Users\stein\OneDrive\Dokumenter\Scanner`

Solution:

`C:\Users\stein\OneDrive\Dokumenter\Scanner\ScannerSample.sln`

WPF-projekt:

`C:\Users\stein\OneDrive\Dokumenter\Scanner\src\ScannerSample.Wpf\ScannerSample.Wpf.csproj`

## Hvad samplet viser

- En WPF sample-app til .NET Framework 4.6.2.
- En scanner-abstraktion via `IScannerProvider` og `IScannerSession`.
- En simpel MVVM-struktur med `MainWindowViewModel`, `RelayCommand` og bindbare profiler.
- En `MockScannerProvider`, saa appen kan testes uden fysisk scanner.
- En `WiaScannerProvider` til Windows WIA-scannere, multi-page, feeder og duplex hvor driveren understotter det.
- En `TwainScannerProvider` baseret paa NTwain til Ricoh fi-8040 / fi-8170 / PaperStream IP via TWAIN.
- Progress events via `ScanProgress`, saa UI kan vise status og thumbnails undervejs.
- Output som individuelle PNG-sider eller multi-page TIFF.

## Faelles scan features

`ScanProfile` bruges af begge rigtige providers og indeholder samme feature-oensker:

- DPI
- Farve/graa/sort-hvid
- ADF/feeder
- Auto feed
- Duplex
- Driver UI til/fra
- Driver indicators til/fra
- Max pages, hvor `0` betyder alle sider
- Blank page discard
- Auto deskew
- Auto rotate
- Auto border detection
- Feeder preflight, saa scan kan stoppes med "laeg papir i feederen" foer driveren starter
- Double-feed detection, hvor driveren eksponerer det
- Output som PNG-sider eller multi-page TIFF

Vigtigt: baade TWAIN og WIA er driver-afhaengige. Provideren forsoger at saette hele feature-saettet, men ignorerer settings som den konkrete driver afviser.

## Ricoh integration

Ricoh fi-8040 og fi-8170 / PaperStream IP boer kobles paa via:

`src\ScannerSample.Wpf\Providers\TwainScannerProvider.cs`

Samplet bruger NTwain 3.7.6, som er den seneste stable-version paa NuGet. Hvis Ricoh/PaperStream IP TWAIN-driveren er installeret, boer scanneren fremgaa som en TWAIN-enhed i appens device-liste.

TWAIN-koden koeres fra WPF UI-traaden, som allerede er STA og har message pump. Det er vigtigt for mange scannerdrivere.

## WIA multi-page og duplex

`WiaScannerProvider` forsoger at saette WIA document handling til feeder og duplex via WIA device properties. Naar `UseFeeder` er slaaet til, scanner provideren i en loop og gemmer hver side som en separat PNG, indtil max pages er naaet, feederen melder tom, eller transfer stopper.

Avancerede WIA-features som blank page discard, deskew, rotate, border detection og indicators bliver matchet via WIA property-navne, fordi WIA-drivere varierer meget mellem producenter.

Til robuste batch-jobs paa Ricoh fi-scannere boer TWAIN stadig bruges som primaer provider.

## Feeder preflight og double-feed

Foer scanning kalder ViewModel `ScanApplicationService.Preflight(...)`. Hvis profilen bruger feeder, og provideren kan se at feederen er tom, stoppes scan med en brugerbesked foer driveren startes.

UI'et viser feeder-status i panelet "Feeder preflight":

- Groen indikator: feeder klar eller flatbed valgt.
- Gul indikator: driveren kan ikke rapportere feeder-status sikkert.
- Roed indikator: feederen er tom, og scan stoppes.

Knappen `Check feeder` kan bruges til at opdatere status manuelt foer scanning.

TWAIN-provideren bruger `CapFeederLoaded` til feeder-status og forsoger at aktivere double-feed detection via NTwain capabilities. Standardprofilen bruger ultrasonic double-feed detection med medium sensitivitet og stop-respons, naar driveren understotter det.

WIA-provideren bruger WIA document handling status til feeder-status og forsoger at aktivere double-feed/multifeed via kendte WIA property-navne. Det er driver-afhaengigt og derfor tolerant.

## Koersel

Byg fra roden:

```powershell
dotnet build ScannerSample.sln
```

Koer appen:

```powershell
src\ScannerSample.Wpf\bin\Debug\net462\ScannerSample.Wpf.exe
```

Appen viser altid en mock-scanner. Hvis maskinen har WIA-kompatible scannere installeret, vises de ogsaa.

## Provider-strategi

De tre providers daekker de fleste almindelige scannerbehov:

- `TwainScannerProvider`: primaer provider til Ricoh fi-8040, Ricoh fi-8170, Brother TWAIN og andre dokument-scannere.
- `WiaScannerProvider`: fallback til simple Windows-scannere, MFP'er og scannere hvor TWAIN-driver ikke er installeret.
- `MockScannerProvider`: test/demo uden fysisk scanner.

Andre providers kan give mening, hvis der er et konkret behov:

- `PaperStreamProfileProvider`: hvis PaperStream-profiler skal styre avancerede Ricoh-indstillinger.
- `WatchFolderProvider`: hvis eksisterende scanner-software allerede kan scanne til en mappe.
- `SaneProvider`: hvis scanning senere skal understottes paa Linux.
- Vendor SDK-provider: hvis en bestemt producent tilbyder vigtige features, som TWAIN/WIA ikke eksponerer godt nok.

## MVVM struktur

UI-state og kommandoer ligger i:

`src\ScannerSample.Wpf\ViewModels\MainWindowViewModel.cs`

`MainWindow.xaml.cs` fungerer kun som composition root, hvor scanner providers og `ScanApplicationService` oprettes og gives til ViewModel.
