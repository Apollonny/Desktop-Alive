# Kabul ve doğrulama matrisi

## Otomatik kontroller

`CoreChecks.Run` 23 kontrol içerir: Z-order/örtülme, ayak payı, ilk iniş yüzeyi, erişilebilirlik, tutma önceliği, duraklama, tıklama tepkileri, medya debounce ve kaynak filtresi, yerel pencere bağlantısı, destek kaybı, negatif monitör koordinatları ve üst kenarda kaybolmayı önleme.

`Tests --desktop` üretimdeki WindowsHost sınıfını kullanır ve ayrı süreçte gerçek HWND'lerle 10 kontrol çalıştırır. Pencere keşfi, üzerine iniş, yerel bağın taşınması, resize, tool-window örtülmesi, minimize/restore, kapatma ve native bellek sorgusu geçmiştir. Bu test kullanıcının açık uygulamalarını değiştirmez; kendi geçici test pencerelerini oluşturup kapatır.

`Tests --media` gerçek Windows medya oturumu yöneticisi ve named pipe üzerinden üç geçerli veri paketi bekler. Bu kontrol uygulamalar arası play/pause kapsamının tamamını doğrulamaz.

## Windows kabulü — tamamı geçmeden MVP kabul edilmez

| Senaryo | Gereken gözlem | Durum |
|---|---|---|
| Chrome/Discord/Spotify/Steam üzerinde boş alan tıklaması | Alttaki uygulama tıklamayı alır | Elle doğrulanacak |
| Yazarken Mika hareketi | Klavye odağı değişmez | Elle doğrulanacak |
| Mika'yı farklı pozlardan sürükleme | Tutma kesilmez; mouse bırakınca serbest kalır | Elle doğrulanacak |
| Tam yaka çekilmesi ve sarkma | Sanat teslimiyle görsel kabul | İçerik eksik |
| Native pencere taşıma/resize/minimize/close | Yerel bağ korunur veya güvenli düşüş | Kontrollü gerçek HWND testleri geçti; uygulama matrisi bekliyor |
| Taskbar auto-hide | Karakter alt sınırda erişilebilir | Elle doğrulanacak |
| %100/125/150/200 DPI | Ayak ve imleç aynı fiziksel koordinatta | Cihaz matrisi bekliyor |
| Monitor çıkarma/değiştirme | Birincil monitöre güvenli dönüş | Elle doğrulanacak |
| Spotify ve seçili tarayıcı play/pause | Kaynak filtresi ve gecikme doğru | Canlı uygulama matrisi bekliyor |
| Tekrarlayan hatırlatıcı | Zamanında Windows bildirimi ve uygulama kartı; erteleme/tamamlama ve yeniden açılışta saklama | Elle doğrulanacak |
| Fullscreen | Gizlenir; çıkınca geçerli yüzeyde döner | Elle doğrulanacak |
| Explorer yeniden başlatma | Tepsi simgesi tekrar eklenir | Elle doğrulanacak |
| İki saat çalıştırma | Sürekli bellek artışı ve kaybolma yok | Henüz yapılmadı |

## Ölçümler

CPU, GPU, RAM ve VRAM için referans makine bilgisiyle beraber 30/60 FPS ölçümü kaydedilmelidir. Kısa smoke testi performans veya iki saat dayanıklılık kabulü değildir. Minimum sistem gereksinimi henüz belirlenmedi.

## Otomatik açılış testi

Windows paketine `--mika-smoke --mika-capture <tam-png-yolu> -logFile <tam-log-yolu>` verildiğinde beş saniye sonra yalnızca uygulamanın Live2D render'ı PNG olarak kaydedilir ve uygulama kapanır. Log'da `MIKA_READY` aranır. Masaüstü veya başka uygulama görüntüsü kaydedilmez.
