# Third-party notices · Hinweise zu Drittanbietern · Уведомления о сторонних компонентах

## English

Kaktus Backup & Sync targets .NET 10 and WPF. The self-contained Windows x64
release includes Microsoft .NET runtime and Windows Desktop runtime components.
Their licenses and third-party notices are supplied in each release ZIP as
`LICENSE.txt` and `ThirdPartyNotices.txt`. Keep these files with the application.

The project has no declared third-party NuGet package references. This does not
replace checking the generated package and its dependency manifest for each
release. Recheck the bundled runtime notices whenever the .NET SDK/runtime or
published dependencies change.

The optional startup recording is not included in the public source snapshot.
Its redistribution permission has not been documented; do not add it to a
public package until the rights are confirmed. The public source uses the
owner-approved mascot image with private generation metadata removed. See [asset notices](ASSET-NOTICES.md).

The Windows CI workflow uses GitHub Actions `checkout` and `setup-dotnet`.
Their licenses apply to those services/actions; they are not bundled in the app.

## Deutsch

Kaktus Backup & Sync verwendet .NET 10 und WPF. Das eigenständige Windows-x64-
Paket enthält Komponenten der Microsoft .NET- und Windows-Desktop-Laufzeit. Die
zugehörigen Lizenz- und Drittanbieterdateien stehen in jeder ZIP unter
`LICENSE.txt` und `ThirdPartyNotices.txt`. Beide Dateien mit der Anwendung
aufbewahren.

Das Projekt hat keine deklarierten Drittanbieter-NuGet-Paketverweise. Trotzdem
müssen das erzeugte Paket und sein Abhängigkeitsmanifest für jeden Release
geprüft werden. Hinweise bei Änderungen an .NET SDK, Laufzeit oder Abhängigkeiten
erneut prüfen.

Die optionale Startaufnahme ist nicht im öffentlichen Quelltextexport enthalten.
Ihre Weitergaberechte sind nicht dokumentiert; vor einer Aufnahme in ein
öffentliches Paket müssen sie geklärt werden. Der öffentliche Quelltext verwendet
das vom Eigentümer freigegebene Maskottchenbild ohne private Generierungsmetadaten. Siehe [Asset-Hinweise](ASSET-NOTICES.md).

Die Windows-CI verwendet `checkout` und `setup-dotnet` von GitHub Actions. Deren
Bedingungen gelten für diese Dienste und Actions; sie sind nicht Teil der App.

## Русский

Kaktus Backup & Sync использует .NET 10 и WPF. В переносимый выпуск для Windows
x64 входят компоненты среды Microsoft .NET и Windows Desktop. Их лицензии и
уведомления находятся в каждом ZIP в файлах `LICENSE.txt` и
`ThirdPartyNotices.txt`. Сохраняйте их рядом с программой.

В проекте не объявлены сторонние NuGet-пакеты. Перед каждым выпуском всё равно
проверяйте собранный пакет и его манифест зависимостей. При обновлении .NET SDK,
среды выполнения или зависимостей проверяйте уведомления заново.

Дополнительная запись для запуска не включена в публичный экспорт исходников.
Право на её распространение не подтверждено; не добавляйте её в публичный пакет,
пока не выяснены условия. Публичный исходник использует утверждённый владельцем
маскот без приватных метаданных генерации. См. [уведомления об изображениях](ASSET-NOTICES.md).

Windows CI использует `checkout` и `setup-dotnet` в GitHub Actions. Их условия
относятся к этим сервисам и actions; сами они в программу не входят.
