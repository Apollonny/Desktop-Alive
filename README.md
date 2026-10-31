# MIKA LIVES HERE

Windows 11 masaüstü karakteri. Unity 6000.6.0f1, C#, Live2D Cubism BiRP 5-r.4.2 ve Windows medya oturumları kullanır.

## Çalıştırma

`Builds/Windows/Mika.exe` geliştirme paketidir. EXE'yi yanındaki veri klasörleriyle birlikte taşıyın. Mika'nın üstünde sol tuşla sürükleyin; kısa tıklama tepki verir. Sağ tıklama veya sistem tepsisi simgesi ayarlar, duraklatma, geri getirme ve çıkış sunar.

ZIP paketini açtıysanız bu README'nin yanındaki `Mika.exe` dosyasını çalıştırın; kaynak projedeki `Builds/Windows` yolunu ayrıca oluşturmayın.

Başlangıçta 30 FPS, 320 mantıksal piksel, Spotify tepkileri ve fullscreen'de gizleme açıktır. Windows ile başlatma kapalıdır. Ayarlar `%USERPROFILE%/AppData/LocalLow/Mika/MIKA LIVES HERE/settings.json` içinde saklanır.

Ayarlar penceresi **Genel**, **Davranış**, **Hatırlatıcılar** ve **Uygulama** bölümlerine ayrılır. Hatırlatıcılar 15 dakika, 30 dakika, 1 saat, 2 saat veya 4 saat aralıklarla tekrarlanır; zamanı geldiğinde Windows bildirim alanında bildirim ve Mika penceresinde erteleme/tamamlama kartı gösterilir. Hatırlatıcılar ve sonraki zamanları uygulama ayarlarında saklanır.

## Durum

Bu paket **geliştirme sürümüdür; kabul edilmiş MVP değildir**. Shock, Release, Fall, Land, Recover ve React için mevcut model parametreleriyle yeni geliştirme animasyonları üretildi. Gerçek yaka deformasyonu, sarkık gövde, Collar Grab ve Grabbed Idle için kaynak model/rig teslimi gereklidir. Oturma, kulaklıkla dinleme ve uyku ikinci aşamadır.

## Animasyon stüdyosu

İniş artık çarpma hızına tepki verir: kısa bırakmalar hafif, yüksek düşüşler daha belirgin Land/Recover oynatır. Stüdyodaki **20 / 180 / 360 px yükseklikten bırak** düğmeleriyle karşılaştırılabilir. Ayarlar ve ölçümler `Docs/LANDING_RESPONSE.md` içindedir.

Tutma sırasında küçük bacak salınımı taslağı aktiftir. Yatay sürüklemeye tepki verir, sabit tutulunca sakinleşir ve bırakma pozuna yumuşak geçer. Stüdyoda **Tutarken yatay taşıma önizlemesi** ile denenebilir. Bu, tam yaka deformasyonu değildir; ayrıntılar `Docs/GRAB_SWAY_DRAFT.md` içindedir.

Etkileşim senaryolarında **Pencereye bırak** ile düşüş/iniş zincirini gerçek konum değişimiyle izleyin. Mika pencereye indikten sonra **Destek penceresini kaldır** şaşırma ve yere düşüşü başlatır. **Tut / uzun tutmayı dene** üç saniyede surat asma, sekiz saniyede kızgın ifadeyi çalıştırır; **Bırak** ile devam edilir. Bu senaryolar masaüstü uygulamasıyla aynı `MikaController` sınıfını kullanır. Yön düğmesiyle aynalanmış hareketler ve bağlantı noktaları incelenebilir. Eksik yaka deformasyonu bu senaryolarda da onaylanmış sayılmaz.

Paket içindeki `Open Animation Studio.cmd` dosyasını açın veya `Mika.exe --mika-animation-lab` çalıştırın. Durum seçimi, duraklatma, zaman çizelgesinde gezinme, 0.25× / 1× / 2× hız, yüz ifadeleri, ayak/yaka bağlantı noktaları ve PNG kaydı bulunur. Bu pencere masaüstü host'unu veya medya yardımcısını başlatmaz. Grabbed seçildiğinde eksik rig açıkça belirtilir.

Hareket eşlemeleri ve süreleri `Mika_Data/StreamingAssets/Mika/animation-profile.json` içindedir; kaynak projede karşılığı `MikaUnity/Assets/StreamingAssets/Mika/animation-profile.json`. Yeni klipler aynı klasörün `authored` altındadır. İçerik değiştirme akışı `Docs/ANIMATION_PIPELINE.md` içinde açıklanır.

Klasör kökündeki özgün Live2D dosyalarına dokunulmadı. Unity kopyası `MikaUnity/Assets/StreamingAssets/Mika` altındadır. Mevcut hareket ve ifade bağlantıları bu kopyada eklenmiştir.

## Kaynak ve derleme

- Unity Hub ile `MikaUnity` klasörünü açın; `Assets/Mika/Scenes/Desktop.unity` sahnesi Windows build'inin girişidir. Runtime yükleyici boş bir sahnede de otomatik başlar.
- Çekirdek simülasyon `Assets/Mika/Core`, Windows P/Invoke ve medya istemcisi `Assets/Mika/Windows`, uygulama ve Live2D render'ı `Assets/Mika/Runtime` altındadır.
- `MediaHost` bağımsız, self-contained .NET 8 Windows uygulamasıdır. Ses veya pencere içeriği kaydetmez; yalnızca kaynak uygulama kimliği ve oynatım durumu aktarır.
- `rtk proxy powershell -NoProfile -File Tools/build.ps1` çekirdek testlerini, medya yardımcı sürecini ve Unity Windows build'ini üretir.
- Unity editöründe `Mika.Editor.BuildMika.Build` aynı Windows üretimini yapar. Log: `Artifacts/unity-build.log`.
- `rtk proxy .tools/dotnet/dotnet.exe run --project Tests/Mika.CoreTests.csproj -c Release` platformdan bağımsız kontrolleri çalıştırır. Sistem .NET SDK'sı varsa onun `dotnet` komutu da kullanılabilir.
- Testlere `-- --media MikaUnity/Assets/StreamingAssets/MediaHost/Mika.MediaHost.exe` eklemek canlı Windows medya/pipe entegrasyonunu kontrol eder.
- Testlere `-- --desktop` eklemek ayrı süreçte geçici Windows pencereleri oluşturur; gerçek pencere keşfi, iniş, taşıma, resize, örtülme, minimize/restore ve kapatmayı doğrular. Test pencereleri sonunda otomatik kapatılır.

## SDK sabitleme

Resmî paket: https://cubism.live2d.com/sdk-unity/bin/CubismSdkForUnity-5-r.4.2.unitypackage

`Tools/bootstrap.py` indirilen paketi `Artifacts` içinden aktarır, örnek projeleri dışarıda bırakır ve özgün Mika varlıklarını kopyalar. Unity 6000.6 uyumluluğu için SDK'nın üç editör dosyasında eski instance ID çağrıları güncellenir; runtime SDK kaynakları değiştirilmez. SDK motion-fade import yolu bu uygulamada kullanılmaz; hareketler runtime'da örneklenir. Kaynak paket tekrar aktarılırsa bootstrap bu düzeltmeleri yeniden uygular.

Live2D lisans ve bildirim dosyaları SDK ile birlikte korunur. Steam yayını için modelin ticari dağıtım hakları ve geçerli SDK yayın koşulları ayrıca doğrulanmalıdır.

## Teknik notlar

- Masaüstü koordinatları fiziksel pikseldir; seçili monitörde DPI ölçeği uygulanır.
- Görünür pencere kenarları Z sırası ile kesilir. Mika'nın pencereye bağlı X konumu yereldir.
- Üstünde Mika'nın boyu kadar boşluk olmayan pencere kenarları destek sayılmaz. Özellikle maximize edilmiş pencerenin ekranın en üstündeki kenarına çıkıp kaybolmasına izin verilmez. Sürükleme seçili monitörde sınırlandırılır; boyut kullanılabilir yüksekliğe sığdırılır.
- Windows host DWM/Direct3D11 şeffaflık kullanır; flip-model swapchain kapalıdır.
- Live2D 512×768 RenderTexture'a çizilir. Giriş alanı 128×192 alfa örneklemesiyle belirlenir; boş dikdörtgen alan kullanıcı tıklamalarını sahiplenmez.
- Medya yardımcı süreci yalnızca mevcut kullanıcıya açık rastgele adlandırılmış named pipe kullanır. Üç saniye veri alınmazsa müzik tepkisi kapanır.
- Fullscreen, foreground pencerenin seçili monitörü kaplamasıyla belirlenir. Kenarlı/kenarsız özel uygulamalar için gerçek cihaz kabul testleri gerekir.
- Davranış süreleri ve ağırlıkları `StreamingAssets/behavior.json` dosyasındadır.

Kabul matrisi: `Docs/ACCEPTANCE.md`. Rig teslimi: `Docs/ART_HANDOFF.md`.

## Tanılama seçenekleri

- `--mika-smoke --mika-capture <tam PNG yolu>`: beş saniyelik açılış, alfa hit-mask kontrolü ve render çıktısı, ardından kapanış.
- `-batchmode --mika-smoke --mika-gallery --mika-capture <tam PNG yolu>`: Windows overlay'i açmadan GPU üzerinde idle/walk/dance/jump/angry render kontrolü.
- `--mika-profile --mika-fps 30` veya `60`: beş saniye ısınma, 15 saniye ölçüm ve otomatik kapanış. Kullanıcı ayarlarını değiştirmez. Sonuç kullanıcı veri klasörüne yazılır.
- `Tools/audit_assets.py`: runtime'daki 30 dosya referansını ve özgün sanat dosyalarıyla byte eşitliğini kontrol eder.
