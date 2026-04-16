# ActivityTracker

Windows üzerinde hangi uygulamada ne kadar süre geçirdiğini, aktif/idle durumunu ve klavye-mouse kullanımını arka planda izleyen bir Windows Service uygulaması.

## Özellikler

- **Aktif pencere takibi** — hangi uygulamada çalıştığını ve pencere başlığını kaydeder
- **Süre ölçümü** — her uygulama oturumunun toplam süresini saniye cinsinden loglar
- **Aktif / Idle ayrımı** — 60 saniyeden fazla input yoksa kullanıcıyı idle sayar
- **Klavye & mouse sayacı** — her uygulama oturumundaki tuş basma ve tıklama sayısını tutar
- **Otomatik loglama** — uygulama değiştiğinde `log.txt` dosyasına kayıt düşer
- **Windows Service** — arka planda çalışır, oturum açık olduğu sürece aktiftir

## Log Formatı

```
[2025-04-16 14:32:10] App: chrome | Title: GitHub - ActivityTracker | Süre: 142 sn | Aktif: 118 sn | Idle: 24 sn | Tuş: 37 | Mouse: 12
```

| Alan | Açıklama |
|---|---|
| `App` | Süreç adı |
| `Title` | Pencere başlığı |
| `Süre` | Uygulamada geçen toplam süre |
| `Aktif` | Kullanıcının aktif olduğu süre |
| `Idle` | 60s+ input olmayan süre |
| `Tuş` | Klavyeye basılan tuş sayısı |
| `Mouse` | Sol/sağ/orta tıklama sayısı |

## Gereksinimler

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Kurulum & Çalıştırma

### Geliştirme ortamında çalıştırma

```bash
git clone https://github.com/KULLANICI_ADIN/ActivityTracker.git
cd ActivityTracker
dotnet run
```

### Windows Service olarak yükleme

```bash
dotnet publish -c Release -o ./publish

sc create ActivityTracker binPath="C:\tam\yol\publish\ActivityTracker.exe"
sc start ActivityTracker
```

### Servisi durdurma ve kaldırma

```bash
sc stop ActivityTracker
sc delete ActivityTracker
```
