# Desktop Clock Widget

Widget jam ringan untuk desktop Windows, dibuat dengan WPF (.NET 10).

## Fitur

- **3 style:** digital minimal, analog, flip.
- **4 animasi:** fade, slide, flip, atau langsung tanpa animasi.
- **Warna:** 10 warna bawaan, dialog warna Windows, dan pipet untuk mengambil warna dari piksel mana pun di layar.
- **Posisi:** 9 titik tetap (pojok, tepi, tengah) per monitor, atau seret bebas dengan mouse.
- **Multi-monitor:** pilih monitor tempat jam ditampilkan.
- **Tampilan:** jam saja atau jam + tanggal, dengan 6 format jam dan 5 format tanggal (bahasa Indonesia).
- **Lapisan:** selalu di atas jendela lain, atau menempel di desktop.
- Ukuran 50–250%, opacity 20–100%, autostart saat Windows menyala, ikon di system tray.

## Menjalankan

Butuh Windows 10/11 dan [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) (x64).

Jalankan `ClockWidget.exe`. Pengaturan dibuka lewat ikon di system tray atau klik kanan pada jam.
Pengaturan tersimpan di `%AppData%\ClockWidget\settings.json`.

## Build

Butuh .NET SDK 10.

```
dotnet publish ClockWidget.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Atau klik dua kali `build.bat`. Hasilnya ada di `publish\ClockWidget.exe`.
