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
- En `MockScannerProvider`, så appen kan testes uden fysisk scanner.
- En `WiaScannerProvider`, som kan finde Windows WIA-scannere og scanne en side.
- En `TwainScannerProvider` baseret på NTwain til Ricoh fi-8040 / PaperStream IP via TWAIN.
- Progress events via `ScanProgress`, så UI kan vise status og thumbnails undervejs.
- Output som individuelle PNG-sider eller multi-page TIFF.

## Ricoh fi-8040 integration

Ricoh fi-8040 / PaperStream IP bør kobles på i:

`src\ScannerSample.Wpf\Providers\TwainScannerProvider.cs`

Samplet bruger NTwain 3.7.6, som er den seneste stable-version på NuGet. Hvis Ricoh fi-8040 og PaperStream IP TWAIN-driveren er installeret, bør scanneren fremgå som en TWAIN-enhed i appens device-liste.

Provideren sætter disse standardindstillinger, når driveren accepterer dem:

- DPI
- Farve/grå/sort-hvid
- ADF/feeder
- Duplex
- Driver UI til/fra

TWAIN-koden køres fra WPF UI-tråden, som allerede er STA og har message pump. Det er vigtigt for mange scannerdrivere.

Bemærk: jeg kan bygge integrationen her, men den endelige funktionstest kræver en maskine med Ricoh fi-8040 og PaperStream IP-driveren installeret.

## Kørsel

Byg fra roden:

```powershell
dotnet build ScannerSample.sln
```

Kør appen:

```powershell
src\ScannerSample.Wpf\bin\Debug\net462\ScannerSample.Wpf.exe
```

Appen viser altid en mock-scanner. Hvis maskinen har WIA-kompatible scannere installeret, vises de også.

## Provider-strategi

De tre providers dækker de fleste almindelige scannerbehov:

- `TwainScannerProvider`: primær provider til Ricoh fi-8040, Ricoh fi-8170, Brother TWAIN og andre dokument-scannere.
- `WiaScannerProvider`: fallback til simple Windows-scannere, MFP'er og scannere hvor TWAIN-driver ikke er installeret.
- `MockScannerProvider`: test/demo uden fysisk scanner.

Andre providers kan give mening, hvis der er et konkret behov:

- `PaperStreamProfileProvider`: hvis PaperStream-profiler skal styre avancerede Ricoh-indstillinger.
- `WatchFolderProvider`: hvis eksisterende scanner-software allerede kan scanne til en mappe.
- `SaneProvider`: hvis scanning senere skal understøttes på Linux.
- Vendor SDK-provider: hvis en bestemt producent tilbyder vigtige features, som TWAIN/WIA ikke eksponerer godt nok.
